' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class LocalRewriter
        Public Overrides Function VisitIndexOffset(node As BoundIndexOffset) As BoundNode
            Return Visit(node.Value)
        End Function

        Public Overrides Function VisitPatternSlice(node As BoundPatternSlice) As BoundNode
            Return Visit(node.Value)
        End Function

        ''' <summary>
        ''' ^n that no index consumed is the value New Index(n, fromEnd:=True). Of a nullable n, it is Nothing when
        ''' n is Nothing.
        ''' </summary>
        Public Overrides Function VisitFromEndIndexExpression(node As BoundFromEndIndexExpression) As BoundNode
            Dim operand = VisitExpressionNode(node.Operand)
            If Not node.Type.IsNullableType() Then
                Return MakeIndex(node, operand, fromEnd:=True, node.Type)
            End If

            Dim indexType = node.Type.GetNullableUnderlyingType()
            Return LiftIndexOrRange(node, ImmutableArray.Create(operand), Function(values) MakeIndex(node, values(0), fromEnd:=True, indexType))
        End Function

        ''' <summary>
        ''' a..b that no slice consumed is the value New Range(start, end), where a missing start is the first
        ''' element and a missing end is ^0. With a nullable bound, it is Nothing when a bound is Nothing.
        ''' </summary>
        Public Overrides Function VisitRangeExpression(node As BoundRangeExpression) As BoundNode
            Dim left = If(node.LeftOperandOpt Is Nothing, Nothing, VisitExpressionNode(node.LeftOperandOpt))
            Dim right = If(node.RightOperandOpt Is Nothing, Nothing, VisitExpressionNode(node.RightOperandOpt))
            If Not node.Type.IsNullableType() Then
                Return MakeRange(node, left, right, node.Type)
            End If

            Dim rangeType = node.Type.GetNullableUnderlyingType()
            Dim operands = ArrayBuilder(Of BoundExpression).GetInstance()
            If left IsNot Nothing Then
                operands.Add(left)
            End If

            If right IsNot Nothing Then
                operands.Add(right)
            End If

            Return LiftIndexOrRange(node, operands.ToImmutableAndFree(),
                Function(values)
                    Dim position = 0
                    Dim start = If(left Is Nothing, Nothing, values(position))
                    If left IsNot Nothing Then
                        position += 1
                    End If

                    Dim [end] = If(right Is Nothing, Nothing, values(position))
                    Return MakeRange(node, start, [end], rangeType)
                End Function)
        End Function

        ''' <summary>
        ''' Evaluates every operand once, in order, and makes the value from the operands, unwrapped, when every
        ''' nullable operand has a value, or else Nothing.
        ''' </summary>
        Private Function LiftIndexOrRange(node As BoundExpression, operands As ImmutableArray(Of BoundExpression), make As Func(Of ImmutableArray(Of BoundExpression), BoundExpression)) As BoundExpression
            Dim temps As ArrayBuilder(Of LocalSymbol) = Nothing
            Dim inits As ArrayBuilder(Of BoundExpression) = Nothing
            Dim condition As BoundExpression = Nothing
            Dim values = ArrayBuilder(Of BoundExpression).GetInstance(operands.Length)

            For Each operand In operands
                If Not operand.Type.IsNullableType() Then
                    values.Add(CaptureNullableIfNeeded(operand, temps, inits, doNotCaptureLocals:=True))
                    Continue For
                End If

                If HasNoValue(operand) Then
                    values.Free()
                    Return MakeSequenceOfOperands(node, operands, NullableNull(node.Syntax, node.Type))
                End If

                Dim hasValue As BoundExpression = Nothing
                values.Add(ProcessNullableOperand(operand, hasValue, temps, inits, doNotCaptureLocals:=True))
                condition = If(condition Is Nothing, hasValue, MakeBooleanBinaryExpression(node.Syntax, BinaryOperatorKind.AndAlso, condition, hasValue))
            Next

            Dim whenHasValue = WrapInNullable(make(values.ToImmutableAndFree()), node.Type)
            Dim result = If(condition Is Nothing, whenHasValue, MakeTernaryConditionalExpression(node.Syntax, condition, whenHasValue, NullableNull(node.Syntax, node.Type)))

            If temps Is Nothing Then
                Return result
            End If

            Return New BoundSequence(node.Syntax, temps.ToImmutableAndFree(), inits.ToImmutableAndFree(), result, result.Type)
        End Function

        ''' <summary>
        ''' When an operand is known to be Nothing, the operands are still evaluated for their side effects.
        ''' </summary>
        Private Function MakeSequenceOfOperands(node As BoundExpression, operands As ImmutableArray(Of BoundExpression), value As BoundExpression) As BoundExpression
            Dim result = value
            For i = operands.Length - 1 To 0 Step -1
                result = MakeSequence(node.Syntax, operands(i), result)
            Next

            Return result
        End Function

        Private Function MakeRange(node As BoundExpression, start As BoundExpression, [end] As BoundExpression, rangeType As TypeSymbol) As BoundExpression
            Dim indexType = Compilation.GetWellKnownType(WellKnownType.System_Index)
            Dim startIndex = RangeBound(node, start, fromEndWhenMissing:=False, indexType)
            Dim endIndex = RangeBound(node, [end], fromEndWhenMissing:=True, indexType)

            Dim constructor As MethodSymbol = Nothing
            If Not TryGetWellknownMember(constructor, WellKnownMember.System_Range__ctor, node.Syntax) Then
                Return New BoundBadExpression(node.Syntax, LookupResultKind.NotReferencable, ImmutableArray(Of Symbol).Empty, ImmutableArray.Create(startIndex, endIndex), rangeType, hasErrors:=True)
            End If

            Return New BoundObjectCreationExpression(node.Syntax, constructor, ImmutableArray.Create(startIndex, endIndex), Nothing, rangeType)
        End Function

        Private Function RangeBound(node As BoundExpression, bound As BoundExpression, fromEndWhenMissing As Boolean, indexType As TypeSymbol) As BoundExpression
            If bound Is Nothing Then
                Return MakeIndex(node, New BoundLiteral(node.Syntax, ConstantValue.Create(0), Compilation.GetSpecialType(SpecialType.System_Int32)), fromEndWhenMissing, indexType)
            End If

            If TypeSymbol.Equals(bound.Type, indexType, TypeCompareKind.ConsiderEverything) Then
                Return bound
            End If

            Return MakeIndex(bound, bound, fromEnd:=False, indexType)
        End Function

        Private Function MakeIndex(node As BoundNode, value As BoundExpression, fromEnd As Boolean, indexType As TypeSymbol) As BoundExpression
            Dim constructor As MethodSymbol = Nothing
            If Not TryGetWellknownMember(constructor, WellKnownMember.System_Index__ctor, node.Syntax) Then
                Return New BoundBadExpression(node.Syntax, LookupResultKind.NotReferencable, ImmutableArray(Of Symbol).Empty, ImmutableArray.Create(value), indexType, hasErrors:=True)
            End If

            Dim fromEndValue = New BoundLiteral(node.Syntax, ConstantValue.Create(fromEnd), Compilation.GetSpecialType(SpecialType.System_Boolean))
            Return New BoundObjectCreationExpression(node.Syntax, constructor, ImmutableArray.Create(value, fromEndValue), Nothing, indexType)
        End Function
    End Class
End Namespace
