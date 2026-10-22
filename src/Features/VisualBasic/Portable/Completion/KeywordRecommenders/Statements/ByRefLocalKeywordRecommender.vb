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
    ''' Recommends "ByRef" after "Dim", and "ReadOnly" after "Dim ByRef", in a local declaration on vbnext
    ''' </summary>
    Friend Class ByRefLocalKeywordRecommender
        Inherits AbstractKeywordRecommender

        Private Shared ReadOnly s_byRef As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("ByRef", VBFeaturesResources.Declares_a_local_that_refers_to_a_variable_instead_of_holding_its_own_value))

        Private Shared ReadOnly s_readOnly As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("ReadOnly", VBFeaturesResources.Prevents_writes_through_a_ByRef_local))

        Protected Overrides Function RecommendKeywords(context As VisualBasicSyntaxContext, cancellationToken As CancellationToken) As ImmutableArray(Of RecommendedKeyword)
            If Not context.SyntaxTree.IsVbNext() Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Dim token = context.TargetToken
            Dim declaration = TryCast(token.Parent, LocalDeclarationStatementSyntax)
            If declaration Is Nothing OrElse declaration.Modifiers.Count = 0 OrElse token <> declaration.Modifiers.Last() OrElse
               declaration.Modifiers.Any(SyntaxKind.StaticKeyword) OrElse declaration.Modifiers.Any(SyntaxKind.ConstKeyword) Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Select Case token.Kind
                Case SyntaxKind.DimKeyword
                    If Not declaration.Modifiers.Any(SyntaxKind.ByRefKeyword) Then
                        Return s_byRef
                    End If

                Case SyntaxKind.ByRefKeyword
                    If declaration.Modifiers.Any(SyntaxKind.DimKeyword) AndAlso Not declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) Then
                        Return s_readOnly
                    End If
            End Select

            Return ImmutableArray(Of RecommendedKeyword).Empty
        End Function
    End Class
End Namespace
