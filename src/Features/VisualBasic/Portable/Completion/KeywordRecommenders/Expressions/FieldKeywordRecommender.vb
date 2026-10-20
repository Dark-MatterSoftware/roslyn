' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.Completion.Providers
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Completion.KeywordRecommenders.Expressions
    ''' <summary>
    ''' Recommends "Field" in the Get and Set bodies of a property without parameters on vbnext
    ''' </summary>
    Friend Class FieldKeywordRecommender
        Inherits AbstractKeywordRecommender

        Private Shared ReadOnly s_keywords As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("Field", VBFeaturesResources.Refers_to_the_storage_that_the_compiler_declares_for_this_property))

        Protected Overrides Function RecommendKeywords(context As VisualBasicSyntaxContext, cancellationToken As CancellationToken) As ImmutableArray(Of RecommendedKeyword)
            If Not context.SyntaxTree.IsVbNext() OrElse Not (context.IsAnyExpressionContext OrElse context.IsStatementContext) Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Dim accessor = context.TargetToken.GetAncestor(Of AccessorBlockSyntax)()
            If accessor Is Nothing OrElse Not accessor.IsKind(SyntaxKind.GetAccessorBlock, SyntaxKind.SetAccessorBlock) Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Dim propertyBlock = TryCast(accessor.Parent, PropertyBlockSyntax)
            Dim parameterList = propertyBlock?.PropertyStatement.ParameterList
            If propertyBlock Is Nothing OrElse (parameterList IsNot Nothing AndAlso parameterList.Parameters.Count > 0) Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Return s_keywords
        End Function
    End Class
End Namespace
