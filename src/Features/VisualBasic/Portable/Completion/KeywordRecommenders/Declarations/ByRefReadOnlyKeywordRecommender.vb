' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.Completion.Providers
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Completion.KeywordRecommenders.Declarations
    ''' <summary>
    ''' On vbnext, recommends "ReadOnly" after the "ByRef" of a parameter or of a member, "Function" after a member's
    ''' "ByRef" or "ByRef ReadOnly", and "Overridable" after "Shared" in an interface.
    ''' </summary>
    Friend Class ByRefReadOnlyKeywordRecommender
        Inherits AbstractKeywordRecommender

        Private Shared ReadOnly s_readOnlyParameter As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("ReadOnly", VBFeaturesResources.Prevents_writes_through_a_ByRef_parameter))

        Private Shared ReadOnly s_readOnlyFunction As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("ReadOnly", VBFeaturesResources.Prevents_writes_through_the_reference_a_ByRef_function_returns),
                                  New RecommendedKeyword("Function", VBFeaturesResources.Declares_the_name_parameters_and_code_that_define_a_Function_procedure_that_is_a_procedure_that_returns_a_value_to_the_calling_code))

        Private Shared ReadOnly s_function As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("Function", VBFeaturesResources.Declares_the_name_parameters_and_code_that_define_a_Function_procedure_that_is_a_procedure_that_returns_a_value_to_the_calling_code))

        Private Shared ReadOnly s_overridable As ImmutableArray(Of RecommendedKeyword) =
            ImmutableArray.Create(New RecommendedKeyword("Overridable", VBFeaturesResources.Gives_a_shared_interface_member_a_default_body_that_implementing_types_may_replace))

        Protected Overrides Function RecommendKeywords(context As VisualBasicSyntaxContext, cancellationToken As CancellationToken) As ImmutableArray(Of RecommendedKeyword)
            If Not context.SyntaxTree.IsVbNext() Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Dim token = context.TargetToken

            ' Sub M(ByRef |
            Dim parameter = TryCast(token.Parent, ParameterSyntax)
            If parameter IsNot Nothing Then
                If token.Kind = SyntaxKind.ByRefKeyword AndAlso
                   Not parameter.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) AndAlso
                   TypeOf parameter.Parent?.Parent IsNot LambdaHeaderSyntax Then
                    Return s_readOnlyParameter
                End If

                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Dim modifiers = GetMemberModifiers(token)
            If modifiers.Count = 0 OrElse token <> modifiers.Last() Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            ' A field of a ByRef Structure is handled by the ref field recommender.
            Dim structureBlock = token.GetAncestor(Of StructureBlockSyntax)()
            Dim inByRefStructure = structureBlock IsNot Nothing AndAlso structureBlock.StructureStatement.Modifiers.Any(SyntaxKind.ByRefKeyword)

            Select Case token.Kind
                Case SyntaxKind.ByRefKeyword
                    If inByRefStructure Then
                        Return s_function
                    End If

                    Return If(modifiers.Any(SyntaxKind.ReadOnlyKeyword), s_function, s_readOnlyFunction)

                Case SyntaxKind.ReadOnlyKeyword
                    If modifiers.Any(SyntaxKind.ByRefKeyword) Then
                        Return s_function
                    End If

                Case SyntaxKind.SharedKeyword
                    If token.GetAncestor(Of InterfaceBlockSyntax)() IsNot Nothing AndAlso
                       Not modifiers.Any(SyntaxKind.OverridableKeyword) AndAlso
                       Not modifiers.Any(SyntaxKind.MustOverrideKeyword) Then
                        Return s_overridable
                    End If
            End Select

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
    End Class
End Namespace
