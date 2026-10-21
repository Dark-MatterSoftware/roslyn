' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.Completion.Providers
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Completion.KeywordRecommenders.Queries
    ''' <summary>
    ''' Recommends the "Into" keyword.
    ''' </summary>
    Friend Class IntoKeywordRecommender
        Inherits AbstractKeywordRecommender

        Private Shared ReadOnly s_keywords As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("Into", VBFeaturesResources.Specifies_an_identifier_that_can_serve_as_a_reference_to_the_results_of_a_join_or_grouping_subexpression))

        Private Shared ReadOnly s_typeTestKeywords As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("Into", VBFeaturesResources.Names_the_result_of_a_type_test_as_a_local_of_the_tested_type))

        Protected Overrides Function RecommendKeywords(context As VisualBasicSyntaxContext, cancellationToken As CancellationToken) As ImmutableArray(Of RecommendedKeyword)
            ' "Into" after the type of Case Is TypeOf, or of TypeOf ... Is in an If or ElseIf condition, on vbnext
            If context.SyntaxTree.IsVbNext() AndAlso IsAfterNameableTypeTest(context.TargetToken) Then
                Return s_typeTestKeywords
            End If

            ' "Into" for Group By is easy
            If context.SyntaxTree.IsFollowingCompleteExpression(Of GroupByClauseSyntax)(
               context.Position, context.TargetToken, Function(g) g.Keys.LastRangeExpression(), cancellationToken) Then
                Return s_keywords
            End If

            ' "Into" for Group Join is also easy
            If context.SyntaxTree.IsFollowingCompleteExpression(Of GroupJoinClauseSyntax)(
               context.Position, context.TargetToken, Function(g) g.JoinConditions.LastJoinKey(), cancellationToken) Then
                Return s_keywords
            End If

            ' "Into" for Aggregate is annoying, since it can be following after any number of arbitrary clauses
            If context.IsQueryOperatorContext Then
                Dim token = context.TargetToken.GetPreviousToken()
                Dim aggregateQuery = token.GetAncestor(Of AggregateClauseSyntax)()

                If aggregateQuery IsNot Nothing AndAlso (aggregateQuery.IntoKeyword.IsMissing OrElse aggregateQuery.IntoKeyword = token) Then
                    Return s_keywords
                End If
            End If

            Return ImmutableArray(Of RecommendedKeyword).Empty
        End Function

        Private Shared Function IsAfterNameableTypeTest(targetToken As SyntaxToken) As Boolean
            Dim typeClause = targetToken.GetAncestor(Of TypeCaseClauseSyntax)()
            If typeClause IsNot Nothing Then
                Dim lastTestToken = If(typeClause.CloseParenToken.Kind = SyntaxKind.CloseParenToken, typeClause.CloseParenToken, typeClause.Type.GetLastToken())
                Return targetToken = lastTestToken AndAlso typeClause.IntoKeyword.Kind = SyntaxKind.None
            End If

            Dim typeTest = targetToken.GetAncestor(Of TypeOfExpressionSyntax)()
            If typeTest Is Nothing OrElse targetToken <> typeTest.Type.GetLastToken() Then
                Return False
            End If

            If typeTest.IsKind(SyntaxKind.TypeOfIsNotExpression) Then
                Return IsGuardCondition(typeTest)
            End If

            If Not typeTest.IsKind(SyntaxKind.TypeOfIsExpression) Then
                Return False
            End If

            ' The test is the condition, or an operand of its AndAlso chain, through parentheses.
            Dim expression As SyntaxNode = typeTest
            Do While expression.Parent.IsKind(SyntaxKind.AndAlsoExpression, SyntaxKind.ParenthesizedExpression)
                expression = expression.Parent
            Loop

            Select Case expression.Parent.Kind
                Case SyntaxKind.IfStatement
                    Return DirectCast(expression.Parent, IfStatementSyntax).Condition Is expression
                Case SyntaxKind.ElseIfStatement
                    Return DirectCast(expression.Parent, ElseIfStatementSyntax).Condition Is expression
                Case SyntaxKind.SingleLineIfStatement
                    Return DirectCast(expression.Parent, SingleLineIfStatementSyntax).Condition Is expression
            End Select

            Return False
        End Function

        ''' <summary>
        ''' TypeOf ... IsNot names its result only as the whole condition of an If, through parentheses, which then
        ''' guards the rest of the block.
        ''' </summary>
        Private Shared Function IsGuardCondition(typeTest As TypeOfExpressionSyntax) As Boolean
            Dim expression As SyntaxNode = typeTest
            Do While expression.Parent.IsKind(SyntaxKind.ParenthesizedExpression)
                expression = expression.Parent
            Loop

            Select Case expression.Parent.Kind
                Case SyntaxKind.IfStatement
                    Return DirectCast(expression.Parent, IfStatementSyntax).Condition Is expression
                Case SyntaxKind.SingleLineIfStatement
                    Return DirectCast(expression.Parent, SingleLineIfStatementSyntax).Condition Is expression
            End Select

            Return False
        End Function
    End Class
End Namespace
