' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.Completion.Providers
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.Completion.KeywordRecommenders.Declarations
    ''' <summary>
    ''' Recommends "ByRef" before a field of a ByRef Structure, and after "=" in an assignment in its Sub New, on vbnext
    ''' </summary>
    Friend Class ByRefFieldKeywordRecommender
        Inherits AbstractKeywordRecommender

        Private Shared ReadOnly s_byRefField As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("ByRef", VBFeaturesResources.Declares_a_field_that_refers_to_a_variable_instead_of_holding_its_own_value))

        Private Shared ReadOnly s_readOnly As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("ReadOnly", VBFeaturesResources.Prevents_writes_through_a_ByRef_field))

        Private Shared ReadOnly s_byRefAssignment As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("ByRef", VBFeaturesResources.Makes_a_ref_field_refer_to_a_variable))

        Protected Overrides Function RecommendKeywords(context As VisualBasicSyntaxContext, cancellationToken As CancellationToken) As ImmutableArray(Of RecommendedKeyword)
            If Not context.SyntaxTree.IsVbNext() Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Dim token = context.TargetToken
            Dim structureBlock = token.GetAncestor(Of StructureBlockSyntax)()
            If structureBlock Is Nothing OrElse Not structureBlock.StructureStatement.Modifiers.Any(SyntaxKind.ByRefKeyword) Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            If token.Kind = SyntaxKind.ByRefKeyword Then
                Dim modifiers = GetMemberModifiers(token)
                If modifiers.Contains(token) AndAlso token = modifiers.Last() AndAlso
                   Not modifiers.Any(SyntaxKind.ReadOnlyKeyword) AndAlso
                   Not modifiers.Any(SyntaxKind.SharedKeyword) AndAlso
                   Not modifiers.Any(SyntaxKind.ConstKeyword) Then
                    Return s_readOnly
                End If

                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            If context.IsTypeMemberDeclarationKeywordContext Then
                Dim modifiers = context.ModifierCollectionFacts
                If modifiers.CouldApplyToOneOf(PossibleDeclarationTypes.Field) AndAlso
                   modifiers.SharedKeyword.Kind = SyntaxKind.None AndAlso
                   modifiers.MutabilityOrWithEventsKeyword.Kind <> SyntaxKind.ConstKeyword AndAlso
                   modifiers.MutabilityOrWithEventsKeyword.Kind <> SyntaxKind.WithEventsKeyword AndAlso
                   Not HasByRefBefore(token) Then
                    Return s_byRefField
                End If

                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Dim assignment = TryCast(token.Parent, AssignmentStatementSyntax)
            If token.Kind = SyntaxKind.EqualsToken AndAlso assignment IsNot Nothing AndAlso
               assignment.Kind = SyntaxKind.SimpleAssignmentStatement AndAlso assignment.OperatorToken = token AndAlso
               TypeOf token.GetAncestor(Of MethodBlockBaseSyntax)()?.BlockStatement Is SubNewStatementSyntax AndAlso
               token.GetAncestor(Of MethodBlockBaseSyntax)().GetAncestor(Of StructureBlockSyntax)() Is structureBlock Then
                Return s_byRefAssignment
            End If

            Return ImmutableArray(Of RecommendedKeyword).Empty
        End Function

        Private Shared Function GetMemberModifiers(token As SyntaxToken) As SyntaxTokenList
            Select Case token.Parent?.Kind
                Case SyntaxKind.IncompleteMember
                    Return DirectCast(token.Parent, IncompleteMemberSyntax).Modifiers
                Case SyntaxKind.FieldDeclaration
                    Return DirectCast(token.Parent, FieldDeclarationSyntax).Modifiers
            End Select

            Return Nothing
        End Function

        Private Shared Function HasByRefBefore(token As SyntaxToken) As Boolean
            Dim statement = token.GetAncestor(Of StatementSyntax)()
            If statement Is Nothing Then
                Return False
            End If

            For Each previous In statement.DescendantTokens()
                If previous.SpanStart > token.SpanStart Then
                    Exit For
                End If

                If previous.Kind = SyntaxKind.ByRefKeyword Then
                    Return True
                End If
            Next

            Return False
        End Function
    End Class
End Namespace
