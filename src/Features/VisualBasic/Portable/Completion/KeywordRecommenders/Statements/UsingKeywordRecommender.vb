' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.Completion.Providers
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Completion.KeywordRecommenders.Statements
    ''' <summary>
    ''' Recommends the "Using" keyword at the beginning of a statement, and after "Async" there on vbnext.
    ''' </summary>
    Friend Class UsingKeywordRecommender
        Inherits AbstractKeywordRecommender

        Private Shared ReadOnly s_keywords As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("Using", VBFeaturesResources.A_Using_block_does_three_things_colon_it_creates_and_initializes_variables_in_the_resource_list_it_runs_the_code_in_the_block_and_it_disposes_of_the_variables_before_exiting_Resources_used_in_the_Using_block_must_implement_System_IDisposable_Using_resource1_bracket_resource2_bracket_End_Using))

        Private Shared ReadOnly s_asyncUsingKeywords As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("Using", VBFeaturesResources.Releases_the_resource_with_an_awaited_DisposeAsync_when_the_block_ends))

        Protected Overrides Function RecommendKeywords(context As VisualBasicSyntaxContext, cancellationToken As CancellationToken) As ImmutableArray(Of RecommendedKeyword)
            If context.IsMultiLineStatementContext Then
                Return s_keywords
            End If

            ' Async Using on vbnext, where Async starts the statement.
            Dim targetToken = context.TargetToken
            If context.SyntaxTree.IsVbNext() AndAlso
               targetToken.HasMatchingText(SyntaxKind.AsyncKeyword) AndAlso
               targetToken.GetAncestor(Of StatementSyntax)()?.GetFirstToken() = targetToken AndAlso
               targetToken.GetAncestor(Of MethodBlockBaseSyntax)() IsNot Nothing Then
                Return s_asyncUsingKeywords
            End If

            Return ImmutableArray(Of RecommendedKeyword).Empty
        End Function
    End Class
End Namespace
