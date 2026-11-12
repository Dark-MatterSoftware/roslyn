' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator

    ''' <summary>
    ''' Experimental, run only for an evaluation that asks for it (useArraySliceLoop). When the whole evaluator expression is a Vb.Next
    ''' array slice, the elements are copied with a loop instead of the Array.Copy call that BindArrayRange binds.
    ''' The slice's own side effects run first, unchanged and in order. When the source is Nothing, the start is negative,
    ''' or the end is past the source's length, the original Array.Copy call runs instead, so that it throws.
    ''' A slice anywhere else in the expression is left as bound.
    ''' </summary>
    Friend NotInheritable Class ArraySliceRewriter

        Friend Shared Function Rewrite(compilation As VisualBasicCompilation, method As MethodSymbol, block As BoundBlock) As BoundBlock
            Dim statements = block.Statements
            For i = 0 To statements.Length - 1
                Dim returnStatement = TryCast(statements(i), BoundReturnStatement)
                If returnStatement Is Nothing Then
                    Continue For
                End If

                ' Only a slice that is the complete returned expression.
                Dim sequence = TryCast(returnStatement.ExpressionOpt, BoundSequence)
                Dim parts As SliceParts = Nothing
                If sequence Is Nothing OrElse Not TryMatch(sequence, parts) Then
                    Continue For
                End If

                Dim replacement = RewriteSlice(compilation, method, returnStatement, sequence, parts)
                Return block.Update(block.StatementListSyntax, block.Locals, statements.SetItem(i, replacement))
            Next

            Return block
        End Function

        Private Structure SliceParts
            Public Source As BoundExpression
            Public StartLocal As LocalSymbol
            Public EndLocal As LocalSymbol
            Public ResultLocal As LocalSymbol
            Public ArrayType As ArrayTypeSymbol
            Public Copy As BoundCall
        End Structure

        ''' <summary>
        ''' The shape that BindArrayRange binds: ..., result = New T(end - start) {}, Array.Copy(source, start, result, 0, end - start), with result as the value.
        ''' </summary>
        Private Shared Function TryMatch(sequence As BoundSequence, ByRef parts As SliceParts) As Boolean
            Dim arrayType = TryCast(sequence.Type, ArrayTypeSymbol)
            Dim result = TryCast(sequence.ValueOpt, BoundLocal)
            Dim sideEffects = sequence.SideEffects
            If arrayType Is Nothing OrElse Not arrayType.IsSZArray OrElse result Is Nothing OrElse sideEffects.Length < 2 Then
                Return False
            End If

            Dim copy = TryCast(sideEffects(sideEffects.Length - 1), BoundCall)
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

            ' Start, end and result are the sequence's own locals.
            Dim locals = sequence.Locals
            If Not locals.Contains(start.LocalSymbol) OrElse Not locals.Contains(lengthEnd.LocalSymbol) OrElse Not locals.Contains(result.LocalSymbol) Then
                Return False
            End If

            parts = New SliceParts With {
                .Source = source,
                .StartLocal = start.LocalSymbol,
                .EndLocal = lengthEnd.LocalSymbol,
                .ResultLocal = result.LocalSymbol,
                .ArrayType = arrayType,
                .Copy = copy}
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
        ''' { side effects through the allocation
        '''   copySource = source
        '''   If copySource Is Nothing GoTo fallback : If start &lt; 0 GoTo fallback : If end &gt; copySource.Length GoTo fallback
        '''   i = 0 : GoTo check
        '''   loop: result(i) = copySource(start + i) : i = i + 1
        '''   check: If i &lt; end - start GoTo loop
        '''   GoTo done
        '''   fallback: Array.Copy(copySource, start, result, 0, end - start)
        '''   done: Return result }
        ''' The original Array.Copy call reads its source argument once, after the side effects and before the call. The
        ''' source is read into copySource at that same point, once, and everything after it uses copySource, so a ByRef
        ''' parameter, or a captured or Static local's field, that changes during the copy does not change what is copied.
        ''' </summary>
        Private Shared Function RewriteSlice(compilation As VisualBasicCompilation, method As MethodSymbol, returnStatement As BoundReturnStatement, sequence As BoundSequence, parts As SliceParts) As BoundStatement
            Dim syntax = sequence.Syntax
            Dim int32Type = parts.StartLocal.Type
            Dim booleanType = compilation.GetSpecialType(SpecialType.System_Boolean)
            Dim elementType = parts.ArrayType.ElementType
            Dim indexLocal As LocalSymbol = New SynthesizedLocal(method, int32Type, SynthesizedLocalKind.LoweringTemp)
            Dim copySourceLocal As LocalSymbol = New SynthesizedLocal(method, parts.ArrayType, SynthesizedLocalKind.LoweringTemp)

            Dim fallbackLabel = New GeneratedLabelSymbol("sliceFallback")
            Dim loopLabel = New GeneratedLabelSymbol("sliceLoop")
            Dim checkLabel = New GeneratedLabelSymbol("sliceCheck")
            Dim doneLabel = New GeneratedLabelSymbol("sliceDone")

            Dim statements = ArrayBuilder(Of BoundStatement).GetInstance()

            ' The slice's own side effects, unchanged and in order, through the allocation of the result.
            For i = 0 To sequence.SideEffects.Length - 2
                Dim sideEffect = sequence.SideEffects(i)
                statements.Add(New BoundExpressionStatement(sideEffect.Syntax, sideEffect).MakeCompilerGenerated())
            Next

            ' copySource = source, where the original Array.Copy call reads its source argument.
            statements.Add(Assign(New BoundLocal(syntax, copySourceLocal, isLValue:=True, type:=parts.ArrayType).MakeCompilerGenerated(), ReadSource(parts.Source, syntax), syntax, parts.ArrayType))

            ' Where Array.Copy throws, run it.
            Dim sourceIsNothing = New BoundBinaryOperator(syntax, BinaryOperatorKind.Is, ReadLocal(copySourceLocal, syntax), New BoundLiteral(syntax, ConstantValue.Null, parts.ArrayType).MakeCompilerGenerated(), False, booleanType).MakeCompilerGenerated()
            statements.Add(New BoundConditionalGoto(syntax, sourceIsNothing, True, fallbackLabel).MakeCompilerGenerated())
            Dim startIsNegative = New BoundBinaryOperator(syntax, BinaryOperatorKind.LessThan, ReadLocal(parts.StartLocal, syntax), Int32Literal(0, syntax, int32Type), False, booleanType).MakeCompilerGenerated()
            statements.Add(New BoundConditionalGoto(syntax, startIsNegative, True, fallbackLabel).MakeCompilerGenerated())
            Dim endIsPastSource = New BoundBinaryOperator(syntax, BinaryOperatorKind.GreaterThan, ReadLocal(parts.EndLocal, syntax), New BoundArrayLength(syntax, ReadLocal(copySourceLocal, syntax), int32Type).MakeCompilerGenerated(), False, booleanType).MakeCompilerGenerated()
            statements.Add(New BoundConditionalGoto(syntax, endIsPastSource, True, fallbackLabel).MakeCompilerGenerated())

            ' i = 0 : GoTo check
            statements.Add(Assign(New BoundLocal(syntax, indexLocal, isLValue:=True, type:=int32Type).MakeCompilerGenerated(), Int32Literal(0, syntax, int32Type), syntax, int32Type))
            statements.Add(New BoundGotoStatement(syntax, checkLabel, Nothing).MakeCompilerGenerated())

            ' loop: result(i) = source(start + i) : i = i + 1
            statements.Add(New BoundLabelStatement(syntax, loopLabel).MakeCompilerGenerated())
            Dim sourceIndex = New BoundBinaryOperator(syntax, BinaryOperatorKind.Add, ReadLocal(parts.StartLocal, syntax), ReadLocal(indexLocal, syntax), False, int32Type).MakeCompilerGenerated()
            Dim element = New BoundArrayAccess(syntax, ReadLocal(copySourceLocal, syntax), ImmutableArray.Create(Of BoundExpression)(sourceIndex), False, elementType).MakeCompilerGenerated()
            Dim target = New BoundArrayAccess(syntax, ReadLocal(parts.ResultLocal, syntax), ImmutableArray.Create(Of BoundExpression)(ReadLocal(indexLocal, syntax)), True, elementType).MakeCompilerGenerated()
            statements.Add(Assign(target, element, syntax, elementType))
            Dim nextIndex = New BoundBinaryOperator(syntax, BinaryOperatorKind.Add, ReadLocal(indexLocal, syntax), Int32Literal(1, syntax, int32Type), False, int32Type).MakeCompilerGenerated()
            statements.Add(Assign(New BoundLocal(syntax, indexLocal, isLValue:=True, type:=int32Type).MakeCompilerGenerated(), nextIndex, syntax, int32Type))

            ' check: If i < end - start GoTo loop
            statements.Add(New BoundLabelStatement(syntax, checkLabel).MakeCompilerGenerated())
            Dim count = New BoundBinaryOperator(syntax, BinaryOperatorKind.Subtract, ReadLocal(parts.EndLocal, syntax), ReadLocal(parts.StartLocal, syntax), False, int32Type).MakeCompilerGenerated()
            Dim more = New BoundBinaryOperator(syntax, BinaryOperatorKind.LessThan, ReadLocal(indexLocal, syntax), count, False, booleanType).MakeCompilerGenerated()
            statements.Add(New BoundConditionalGoto(syntax, more, True, loopLabel).MakeCompilerGenerated())
            statements.Add(New BoundGotoStatement(syntax, doneLabel, Nothing).MakeCompilerGenerated())

            ' fallback: the original Array.Copy call, with copySource as its source argument
            statements.Add(New BoundLabelStatement(syntax, fallbackLabel).MakeCompilerGenerated())
            Dim copy = parts.Copy
            Dim fallbackSource = ReplaceSource(copy.Arguments(0), ReadLocal(copySourceLocal, syntax))
            Dim fallbackCopy = copy.Update(copy.Method, copy.MethodGroupOpt, copy.ReceiverOpt, copy.Arguments.SetItem(0, fallbackSource), copy.DefaultArguments, copy.ConstantValueOpt, copy.IsLValue, copy.SuppressObjectClone, copy.Type)
            statements.Add(New BoundExpressionStatement(copy.Syntax, fallbackCopy).MakeCompilerGenerated())

            ' done: Return result
            statements.Add(New BoundLabelStatement(syntax, doneLabel).MakeCompilerGenerated())
            statements.Add(New BoundReturnStatement(returnStatement.Syntax, ReadLocal(parts.ResultLocal, syntax), returnStatement.FunctionLocalOpt, returnStatement.ExitLabelOpt).MakeCompilerGenerated())

            Return New BoundBlock(syntax, Nothing, sequence.Locals.Add(indexLocal).Add(copySourceLocal), statements.ToImmutableAndFree()).MakeCompilerGenerated()
        End Function

        Private Shared Function Assign(left As BoundExpression, right As BoundExpression, syntax As SyntaxNode, type As TypeSymbol) As BoundStatement
            Dim assignment = New BoundAssignmentOperator(syntax, left, right, suppressObjectClone:=True, type:=type).MakeCompilerGenerated()
            Return New BoundExpressionStatement(syntax, assignment).MakeCompilerGenerated()
        End Function

        Private Shared Function ReadLocal(local As LocalSymbol, syntax As SyntaxNode) As BoundExpression
            Return New BoundLocal(syntax, local, isLValue:=False, type:=local.Type).MakeCompilerGenerated()
        End Function

        Private Shared Function ReadSource(source As BoundExpression, syntax As SyntaxNode) As BoundExpression
            If source.Kind = BoundKind.Local Then
                Return ReadLocal(DirectCast(source, BoundLocal).LocalSymbol, source.Syntax)
            End If

            Dim parameter = DirectCast(source, BoundParameter)
            Return New BoundParameter(source.Syntax, parameter.ParameterSymbol, isLValue:=False, type:=source.Type).MakeCompilerGenerated()
        End Function

        ''' <summary>
        ''' The Array.Copy call's source argument, the source converted to System.Array, with copySource in place of the source.
        ''' </summary>
        Private Shared Function ReplaceSource(argument As BoundExpression, copySource As BoundExpression) As BoundExpression
            If argument.Kind <> BoundKind.Conversion Then
                Return copySource
            End If

            Dim conversion = DirectCast(argument, BoundConversion)
            Return conversion.Update(copySource, conversion.ConversionKind, conversion.Checked, conversion.ExplicitCastInCode, conversion.ConstantValueOpt, conversion.ExtendedInfoOpt, conversion.Type)
        End Function

        Private Shared Function Int32Literal(value As Integer, syntax As SyntaxNode, int32Type As TypeSymbol) As BoundExpression
            Return New BoundLiteral(syntax, ConstantValue.Create(value), int32Type).MakeCompilerGenerated()
        End Function
    End Class
End Namespace