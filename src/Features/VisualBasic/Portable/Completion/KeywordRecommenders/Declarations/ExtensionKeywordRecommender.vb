' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.Completion.Providers
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery

Namespace Microsoft.CodeAnalysis.VisualBasic.Completion.KeywordRecommenders.Declarations
    ''' <summary>
    ''' Recommends the "Extension" keyword that starts an extension block in a module on vbnext
    ''' </summary>
    Friend Class ExtensionKeywordRecommender
        Inherits AbstractKeywordRecommender

        Private Shared ReadOnly s_keywords As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("Extension", VBFeaturesResources.Declares_an_extension_block_whose_members_extend_the_receiver_type))

        Protected Overrides Function RecommendKeywords(context As VisualBasicSyntaxContext, cancellationToken As CancellationToken) As ImmutableArray(Of RecommendedKeyword)
            Dim targetToken = context.TargetToken

            If context.SyntaxTree.IsVbNext() AndAlso
               context.IsTypeMemberDeclarationKeywordContext AndAlso
               targetToken.FollowsEndOfStatement(context.Position) AndAlso
               targetToken.GetInnermostDeclarationContext().IsKind(SyntaxKind.ModuleBlock) Then
                Return s_keywords
            End If

            Return ImmutableArray(Of RecommendedKeyword).Empty
        End Function
    End Class
End Namespace
