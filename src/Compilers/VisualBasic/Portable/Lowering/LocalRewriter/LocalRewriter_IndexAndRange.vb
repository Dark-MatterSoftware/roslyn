' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class LocalRewriter
        ''' <summary>
        ''' ^n that no index consumed is the value New Index(n, fromEnd:=True).
        ''' </summary>
        Public Overrides Function VisitFromEndIndexExpression(node As BoundFromEndIndexExpression) As BoundNode
            Dim operand = VisitExpressionNode(node.Operand)
            Return MakeIndex(node, operand, fromEnd:=True, node.Type)
        End Function

        ''' <summary>
        ''' a..b that no slice consumed is the value New Range(start, end), where a missing start is the first
        ''' element and a missing end is ^0.
        ''' </summary>
        Public Overrides Function VisitRangeExpression(node As BoundRangeExpression) As BoundNode
            Dim indexType = Compilation.GetWellKnownType(WellKnownType.System_Index)
            Dim int32Type = Compilation.GetSpecialType(SpecialType.System_Int32)
            Dim start = RangeBound(node, node.LeftOperandOpt, fromEndWhenMissing:=False, indexType, int32Type)
            Dim [end] = RangeBound(node, node.RightOperandOpt, fromEndWhenMissing:=True, indexType, int32Type)

            Dim constructor As MethodSymbol = Nothing
            If Not TryGetWellknownMember(constructor, WellKnownMember.System_Range__ctor, node.Syntax) Then
                Return New BoundBadExpression(node.Syntax, LookupResultKind.NotReferencable, ImmutableArray(Of Symbol).Empty, ImmutableArray.Create(start, [end]), node.Type, hasErrors:=True)
            End If

            Return New BoundObjectCreationExpression(node.Syntax, constructor, ImmutableArray.Create(start, [end]), Nothing, node.Type)
        End Function

        Private Function RangeBound(node As BoundRangeExpression, bound As BoundExpression, fromEndWhenMissing As Boolean, indexType As TypeSymbol, int32Type As TypeSymbol) As BoundExpression
            If bound Is Nothing Then
                Return MakeIndex(node, New BoundLiteral(node.Syntax, ConstantValue.Create(0), int32Type), fromEndWhenMissing, indexType)
            End If

            Dim rewritten = VisitExpressionNode(bound)
            If TypeSymbol.Equals(rewritten.Type, indexType, TypeCompareKind.ConsiderEverything) Then
                Return rewritten
            End If

            Return MakeIndex(bound, rewritten, fromEnd:=False, indexType)
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
