' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator

    ''' <summary>
    ''' Debugger-only rewrite for Vb.Next array slices in expression evaluation. Replaces the
    ''' <see cref="System.Array.Copy"/> fill that <c>BindArrayRange</c> binds with a call to
    ''' <see cref="ArraySliceCopyHelperSymbol"/>, which copies elements with GetValue/SetValue
    ''' (and falls back to <c>Array.Copy</c> only where that call would throw).
    ''' The replacement stays inside the original BoundSequence so slices keep their expression
    ''' context: conditional and short-circuit operands that are not evaluated do not run the copy.
    ''' Compiled application lowering is unchanged.
    ''' </summary>
    Friend NotInheritable Class ArraySliceRewriter

        Friend Shared Function Rewrite(compilation As VisualBasicCompilation, method As MethodSymbol, block As BoundBlock) As BoundBlock
            Dim helper = FindHelper(method)
            If helper Is Nothing Then
                Return block
            End If

            Dim statements = block.Statements
            For i = 0 To statements.Length - 1
                Dim returnStatement = TryCast(statements(i), BoundReturnStatement)
                If returnStatement Is Nothing OrElse returnStatement.ExpressionOpt Is Nothing Then
                    Continue For
                End If

                Dim expressionRewriter As New SliceExpressionRewriter(helper)
                Dim rewrittenExpression = DirectCast(expressionRewriter.Visit(returnStatement.ExpressionOpt), BoundExpression)
                If Not expressionRewriter.RewroteAnySlice Then
                    Continue For
                End If

                Dim rewrittenReturn = New BoundReturnStatement(
                    returnStatement.Syntax,
                    rewrittenExpression,
                    returnStatement.FunctionLocalOpt,
                    returnStatement.ExitLabelOpt).MakeCompilerGenerated()

                Return block.Update(block.StatementListSyntax, block.Locals, statements.SetItem(i, rewrittenReturn))
            Next

            Return block
        End Function

        Private Shared Function FindHelper(method As MethodSymbol) As MethodSymbol
            Dim container = TryCast(method.ContainingType, EENamedTypeSymbol)
            If container Is Nothing Then
                Return Nothing
            End If

            For Each candidate In container.Methods
                If candidate.Name = ArraySliceCopyHelperSymbol.MethodName Then
                    Return candidate
                End If
            Next

            Return Nothing
        End Function

        ''' <summary>
        ''' The shape that BindArrayRange binds: ..., result = New T(end - start) {}, Array.Copy(source, start, result, 0, end - start), with result as the value.
        ''' </summary>
        Private Shared Function TryMatch(sequence As BoundSequence, ByRef copy As BoundCall) As Boolean
            Dim arrayType = TryCast(sequence.Type, ArrayTypeSymbol)
            Dim result = TryCast(sequence.ValueOpt, BoundLocal)
            Dim sideEffects = sequence.SideEffects
            If arrayType Is Nothing OrElse Not arrayType.IsSZArray OrElse result Is Nothing OrElse sideEffects.Length < 2 Then
                Return False
            End If

            copy = TryCast(sideEffects(sideEffects.Length - 1), BoundCall)
            If copy Is Nothing OrElse copy.ReceiverOpt IsNot Nothing OrElse Not IsArrayCopy(copy.Method) OrElse copy.Arguments.Length <> 5 Then
                Return False
            End If

            Dim source = StripConversion(copy.Arguments(0))
            Dim start = TryCast(copy.Arguments(1), BoundLocal)
            Dim destination = TryCast(StripConversion(copy.Arguments(2)), BoundLocal)
            Dim destinationIndex = copy.Arguments(3).ConstantValueOpt
            Dim length = TryCast(copy.Arguments(4), BoundBinaryOperator)
            If (source.Kind <> BoundKind.Local AndAlso source.Kind <> BoundKind.Parameter) OrElse
               Not TypeSymbol.Equals(source.Type, arrayType, TypeCompareKind.ConsiderEverything) OrElse
               start Is Nothing OrElse destination Is Nothing OrElse destination.LocalSymbol IsNot result.LocalSymbol OrElse
               destinationIndex Is Nothing OrElse Not destinationIndex.IsIntegral OrElse destinationIndex.Int32Value <> 0 OrElse
               length Is Nothing OrElse length.OperatorKind <> BinaryOperatorKind.Subtract Then
                Return False
            End If

            Dim lengthEnd = TryCast(length.Left, BoundLocal)
            Dim lengthStart = TryCast(length.Right, BoundLocal)
            If lengthEnd Is Nothing OrElse lengthStart Is Nothing OrElse lengthStart.LocalSymbol IsNot start.LocalSymbol Then
                Return False
            End If

            Dim allocation = TryCast(sideEffects(sideEffects.Length - 2), BoundAssignmentOperator)
            Dim allocated = If(allocation Is Nothing, Nothing, TryCast(allocation.Left, BoundLocal))
            Dim creation = If(allocation Is Nothing, Nothing, TryCast(allocation.Right, BoundArrayCreation))
            If allocated Is Nothing OrElse allocated.LocalSymbol IsNot result.LocalSymbol OrElse
               creation Is Nothing OrElse creation.Bounds.Length <> 1 OrElse creation.InitializerOpt IsNot Nothing Then
                Return False
            End If

            Dim locals = sequence.Locals
            If Not locals.Contains(start.LocalSymbol) OrElse Not locals.Contains(lengthEnd.LocalSymbol) OrElse Not locals.Contains(result.LocalSymbol) Then
                Return False
            End If

            Return True
        End Function

        Private Shared Function IsArrayCopy(method As MethodSymbol) As Boolean
            If Not method.IsShared OrElse method.Name <> "Copy" OrElse method.ContainingType.SpecialType <> SpecialType.System_Array OrElse method.ParameterCount <> 5 Then
                Return False
            End If

            Dim parameters = method.Parameters
            Return parameters(0).Type.SpecialType = SpecialType.System_Array AndAlso
                parameters(1).Type.SpecialType = SpecialType.System_Int32 AndAlso
                parameters(2).Type.SpecialType = SpecialType.System_Array AndAlso
                parameters(3).Type.SpecialType = SpecialType.System_Int32 AndAlso
                parameters(4).Type.SpecialType = SpecialType.System_Int32
        End Function

        Private Shared Function StripConversion(expression As BoundExpression) As BoundExpression
            If expression.Kind = BoundKind.Conversion Then
                Return DirectCast(expression, BoundConversion).Operand
            End If

            Return expression
        End Function

        ''' <summary>
        ''' Bottom-up in-place rewrite: each matching BindArrayRange sequence keeps its locals and
        ''' side effects through allocation; only the trailing Array.Copy call becomes a helper call.
        ''' Nested slices rewrite first so each remains inside its original expression position.
        ''' </summary>
        Private NotInheritable Class SliceExpressionRewriter
            Inherits BoundTreeRewriterWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator

            Private ReadOnly _helper As MethodSymbol

            Friend Sub New(helper As MethodSymbol)
                _helper = helper
            End Sub

            Friend Property RewroteAnySlice As Boolean

            Public Overrides Function VisitSequence(node As BoundSequence) As BoundNode
                Dim visited = DirectCast(MyBase.VisitSequence(node), BoundSequence)
                Dim copy As BoundCall = Nothing
                If Not TryMatch(visited, copy) Then
                    Return visited
                End If

                Dim helperCall = New BoundCall(
                    copy.Syntax,
                    _helper,
                    methodGroupOpt:=Nothing,
                    receiverOpt:=Nothing,
                    arguments:=ImmutableArray.Create(
                        copy.Arguments(0),
                        copy.Arguments(1),
                        copy.Arguments(2),
                        copy.Arguments(4)),
                    constantValueOpt:=Nothing,
                    suppressObjectClone:=True,
                    type:=_helper.ReturnType).MakeCompilerGenerated()

                RewroteAnySlice = True
                Return visited.Update(
                    visited.Locals,
                    visited.SideEffects.SetItem(visited.SideEffects.Length - 1, helperCall),
                    visited.ValueOpt,
                    visited.Type)
            End Function
        End Class
    End Class
End Namespace
