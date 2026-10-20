' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend NotInheritable Class LocalRewriter

        ''' <summary>
        ''' An interpolated string handler argument can read the receiver and earlier arguments of the same call.
        ''' 
        '''     receiver.M(a, $"...")
        ''' 
        ''' becomes
        ''' 
        '''     tReceiver = receiver
        '''     tA = a
        '''     tReceiver.M(tA, handler built with tReceiver and tA)
        ''' 
        ''' so each value is evaluated once, in order. Returns Nothing when no argument reads another.
        ''' </summary>
        Private Function SpillInterpolatedStringHandlerArguments(node As BoundCall) As BoundExpression
            Dim lastHandlerIndex = -1
            Dim referenced = PooledHashSet(Of Integer).GetInstance()

            For i = 0 To node.Arguments.Length - 1
                If ReadsHandlerArguments(node.Arguments(i), referenced) Then
                    lastHandlerIndex = i
                End If
            Next

            If lastHandlerIndex < 0 Then
                referenced.Free()
                Return Nothing
            End If

            Dim locals = ArrayBuilder(Of LocalSymbol).GetInstance()
            Dim sideEffects = ArrayBuilder(Of BoundExpression).GetInstance()
            Dim replacements = New Dictionary(Of Integer, BoundExpression)()
            Dim receiver = node.ReceiverOpt

            If referenced.Contains(-1) AndAlso receiver IsNot Nothing Then
                If receiver.Type.IsValueType AndAlso receiver.IsLValue Then
                    ' A structure the call may change is read again, rather than copied before the call.
                    replacements(-1) = receiver.MakeRValue()
                Else
                    Dim receiverTemp = StoreInTemporary(receiver, locals, sideEffects)
                    replacements(-1) = NewTemporaryRead(receiverTemp, receiver.Syntax)
                    receiver = NewTemporaryRead(receiverTemp, receiver.Syntax)
                End If
            End If

            Dim arguments = node.Arguments.ToBuilder()
            For i = 0 To lastHandlerIndex - 1
                Dim argument = arguments(i)
                If argument.Kind = BoundKind.ByRefArgumentWithCopyBack OrElse
                   (node.Method.Parameters(i).IsByRef AndAlso argument.IsLValue) OrElse
                   ReadsHandlerArguments(argument, Nothing) Then
                    ' A ByRef argument keeps its place. The binder does not let a handler read one.
                    Continue For
                End If

                If argument.IsConstant AndAlso Not referenced.Contains(i) Then
                    Continue For
                End If

                Dim argumentTemp = StoreInTemporary(argument, locals, sideEffects)
                arguments(i) = NewTemporaryRead(argumentTemp, argument.Syntax)
                If referenced.Contains(i) Then
                    replacements(i) = NewTemporaryRead(argumentTemp, argument.Syntax)
                End If
            Next

            referenced.Free()

            Dim replacer = New ArgumentPlaceholderReplacer(replacements)
            For i = 0 To arguments.Count - 1
                arguments(i) = DirectCast(replacer.Visit(arguments(i)), BoundExpression)
            Next

            Dim rewrittenCall = node.Update(node.Method, node.MethodGroupOpt, receiver, arguments.ToImmutable(),
                                            node.DefaultArguments, node.ConstantValueOpt, node.IsLValue, node.SuppressObjectClone, node.Type)

            If node.Method.IsSub Then
                sideEffects.Add(rewrittenCall)
                Return New BoundSequence(node.Syntax, locals.ToImmutableAndFree(), sideEffects.ToImmutableAndFree(), Nothing, rewrittenCall.Type)
            End If

            Return New BoundSequence(node.Syntax, locals.ToImmutableAndFree(), sideEffects.ToImmutableAndFree(), rewrittenCall, rewrittenCall.Type)
        End Function

        ''' <summary>
        ''' The binder puts placeholders only in an argument that is an interpolated string converted to a handler type.
        ''' </summary>
        Private Shared Function ReadsHandlerArguments(argument As BoundExpression, indices As PooledHashSet(Of Integer)) As Boolean
            If argument.Kind <> BoundKind.Conversion Then
                Return False
            End If

            Dim conversion = DirectCast(argument, BoundConversion)
            If conversion.Operand.Kind <> BoundKind.InterpolatedStringExpression OrElse Not conversion.Type.IsInterpolatedStringHandlerType() Then
                Return False
            End If

            Return ArgumentPlaceholderCollector.Collect(conversion.Operand, indices)
        End Function

        Private Function StoreInTemporary(value As BoundExpression, locals As ArrayBuilder(Of LocalSymbol), sideEffects As ArrayBuilder(Of BoundExpression)) As LocalSymbol
            Dim temp = New SynthesizedLocal(_currentMethodOrLambda, value.Type, SynthesizedLocalKind.LoweringTemp)
            locals.Add(temp)
            sideEffects.Add(New BoundAssignmentOperator(value.Syntax,
                                                        New BoundLocal(value.Syntax, temp, isLValue:=True, type:=value.Type),
                                                        value.MakeRValue(),
                                                        suppressObjectClone:=True,
                                                        type:=value.Type))
            Return temp
        End Function

        Private Shared Function NewTemporaryRead(temp As LocalSymbol, syntax As SyntaxNode) As BoundExpression
            Return New BoundLocal(syntax, temp, isLValue:=False, type:=temp.Type)
        End Function

        ''' <summary>
        ''' Adds the argument indices that interpolated string handler placeholders read, and returns whether there are any.
        ''' </summary>
        Private NotInheritable Class ArgumentPlaceholderCollector
            Inherits BoundTreeWalkerWithStackGuard

            Private ReadOnly _indices As PooledHashSet(Of Integer)
            Private _found As Boolean

            Private Sub New(indices As PooledHashSet(Of Integer))
                _indices = indices
            End Sub

            Public Shared Function Collect(node As BoundExpression, indices As PooledHashSet(Of Integer)) As Boolean
                Dim collector As New ArgumentPlaceholderCollector(indices)
                collector.Visit(node)
                Return collector._found
            End Function

            Public Overrides Function VisitInterpolatedStringArgumentPlaceholder(node As BoundInterpolatedStringArgumentPlaceholder) As BoundNode
                _found = True
                _indices?.Add(node.ArgumentIndex)
                Return Nothing
            End Function

            Public Overrides Function VisitInterpolatedStringExpression(node As BoundInterpolatedStringExpression) As BoundNode
                Visit(node.ConstructionOpt)
                Return Nothing
            End Function
        End Class

        Private NotInheritable Class ArgumentPlaceholderReplacer
            Inherits BoundTreeRewriterWithStackGuard

            Private ReadOnly _replacements As Dictionary(Of Integer, BoundExpression)

            Public Sub New(replacements As Dictionary(Of Integer, BoundExpression))
                _replacements = replacements
            End Sub

            Public Overrides Function VisitInterpolatedStringArgumentPlaceholder(node As BoundInterpolatedStringArgumentPlaceholder) As BoundNode
                Dim replacement As BoundExpression = Nothing
                If _replacements.TryGetValue(node.ArgumentIndex, replacement) Then
                    Return replacement
                End If

                Return node
            End Function

            Public Overrides Function VisitInterpolatedStringExpression(node As BoundInterpolatedStringExpression) As BoundNode
                Return node.Update(node.Contents, DirectCast(Visit(node.ConstructionOpt), BoundExpression), node.ConstantValueOpt, node.Type)
            End Function
        End Class
    End Class
End Namespace
