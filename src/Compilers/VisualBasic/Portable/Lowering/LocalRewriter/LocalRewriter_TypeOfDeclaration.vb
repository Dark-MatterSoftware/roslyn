' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class LocalRewriter

        ''' <summary>
        ''' TypeOf operand Is T Into name is rewritten as
        ''' 
        '''      temp = operand
        '''      TypeOf temp Is T AndAlso (name = DirectCast(temp, T) : True)
        ''' 
        ''' so the operand is evaluated once and the local is assigned only when the test succeeds.
        ''' </summary>
        Public Overrides Function VisitTypeOfDeclaration(node As BoundTypeOfDeclaration) As BoundNode
            Dim syntax = node.Syntax
            Dim booleanType = node.Type
            Dim local = node.Local
            Dim targetType = local.Type
            Dim operand = VisitExpressionNode(node.Operand)
            Dim operandType = operand.Type

            Dim temp = New SynthesizedLocal(_currentMethodOrLambda, operandType, SynthesizedLocalKind.LoweringTemp)
            Dim store = New BoundAssignmentOperator(syntax, New BoundLocal(syntax, temp, isLValue:=True, type:=operandType), operand, suppressObjectClone:=True, type:=operandType)

            Dim test = VisitExpressionNode(New BoundTypeOf(syntax, New BoundLocal(syntax, temp, isLValue:=False, type:=operandType), isTypeOfIsNotExpression:=False, targetType, booleanType))
            Dim value = VisitExpressionNode(New BoundDirectCast(syntax, New BoundLocal(syntax, temp, isLValue:=False, type:=operandType), node.Conversion, targetType))
            Dim assign = New BoundAssignmentOperator(syntax, New BoundLocal(syntax, local, isLValue:=True, type:=targetType), value, suppressObjectClone:=True, type:=targetType)
            Dim assignAndSucceed = New BoundSequence(syntax,
                                                     ImmutableArray(Of LocalSymbol).Empty,
                                                     ImmutableArray.Create(Of BoundExpression)(assign),
                                                     New BoundLiteral(syntax, ConstantValue.True, booleanType),
                                                     booleanType)

            Dim matched = New BoundBinaryOperator(syntax, BinaryOperatorKind.AndAlso, test, assignAndSucceed, checked:=False, type:=booleanType)
            Return New BoundSequence(syntax, ImmutableArray.Create(Of LocalSymbol)(temp), ImmutableArray.Create(Of BoundExpression)(store), matched, booleanType)
        End Function
    End Class
End Namespace
