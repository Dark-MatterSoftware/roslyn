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
    ''' On vbnext, recommends "Function", "Sub", "Property", "Operator", "Widening", and "Narrowing" after
    ''' "Shared MustOverride" or "Shared Overridable" in an interface, "Operator" after "Widening" or "Narrowing" there,
    ''' "Function" and "Sub" after "ReadOnly" in a structure, and "Get" after "ReadOnly" where a structure property's
    ''' accessor starts.
    ''' </summary>
    Friend Class ReadOnlyMemberKeywordRecommender
        Inherits AbstractKeywordRecommender

        Private Shared ReadOnly s_function As New RecommendedKeyword("Function", VBFeaturesResources.Declares_the_name_parameters_and_code_that_define_a_Function_procedure_that_is_a_procedure_that_returns_a_value_to_the_calling_code)
        Private Shared ReadOnly s_sub As New RecommendedKeyword("Sub", VBFeaturesResources.Declares_the_name_parameters_and_code_that_define_a_Sub_procedure_that_is_a_procedure_that_does_not_return_a_value_to_the_calling_code)
        Private Shared ReadOnly s_property As New RecommendedKeyword("Property", VBFeaturesResources.Declares_the_name_of_a_property_and_the_property_procedures_used_to_store_and_retrieve_the_value_of_the_property)
        Private Shared ReadOnly s_operator As New RecommendedKeyword("Operator", VBFeaturesResources.Declares_the_operator_symbol_operands_and_code_that_define_an_operator_procedure_on_a_class_or_structure)
        Private Shared ReadOnly s_get As New RecommendedKeyword("Get", VBFeaturesResources.Declares_a_Get_property_procedure_that_is_used_to_return_the_current_value_of_a_property)
        Private Shared ReadOnly s_widening As New RecommendedKeyword("Widening", VBFeaturesResources.Indicates_that_a_conversion_operator_CType_converts_a_class_or_structure_to_a_type_that_can_hold_all_possible_values_of_the_original_class_or_structure)
        Private Shared ReadOnly s_narrowing As New RecommendedKeyword("Narrowing", VBFeaturesResources.Indicates_that_a_conversion_operator_CType_converts_a_class_or_structure_to_a_type_that_might_not_be_able_to_hold_some_of_the_possible_values_of_the_original_class_or_structure)

        Private Shared ReadOnly s_sharedInterfaceMembers As ImmutableArray(Of RecommendedKeyword) = ImmutableArray.Create(s_function, s_sub, s_property, s_operator, s_widening, s_narrowing)
        Private Shared ReadOnly s_readOnlyMethods As ImmutableArray(Of RecommendedKeyword) = ImmutableArray.Create(s_function, s_sub)

        Protected Overrides Function RecommendKeywords(context As VisualBasicSyntaxContext, cancellationToken As CancellationToken) As ImmutableArray(Of RecommendedKeyword)
            If Not context.SyntaxTree.IsVbNext() Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Dim token = context.TargetToken
            Dim modifiers = GetMemberModifiers(token)
            If modifiers.Count = 0 OrElse token <> modifiers.Last() Then
                Return ImmutableArray(Of RecommendedKeyword).Empty
            End If

            Select Case token.Kind
                Case SyntaxKind.MustOverrideKeyword, SyntaxKind.OverridableKeyword
                    ' Interface I : Shared MustOverride |
                    If modifiers.Any(SyntaxKind.SharedKeyword) AndAlso IsDirectlyIn(Of InterfaceBlockSyntax)(token) Then
                        Return s_sharedInterfaceMembers
                    End If

                Case SyntaxKind.WideningKeyword, SyntaxKind.NarrowingKeyword
                    ' Interface I : Shared MustOverride Widening |
                    If modifiers.Any(SyntaxKind.SharedKeyword) AndAlso
                       (modifiers.Any(SyntaxKind.MustOverrideKeyword) OrElse modifiers.Any(SyntaxKind.OverridableKeyword)) AndAlso
                       IsDirectlyIn(Of InterfaceBlockSyntax)(token) Then
                        Return ImmutableArray.Create(s_operator)
                    End If

                Case SyntaxKind.ReadOnlyKeyword
                    If modifiers.Any(SyntaxKind.SharedKeyword) OrElse modifiers.Any(SyntaxKind.ByRefKeyword) OrElse
                       Not IsDirectlyIn(Of StructureBlockSyntax)(token) Then
                        Return ImmutableArray(Of RecommendedKeyword).Empty
                    End If

                    ' Property P As Integer : ReadOnly |
                    ' After an auto-implemented property the line may also start a new member.
                    Dim isInPropertyBlock As Boolean
                    If FollowsPropertyThatTakesGet(modifiers, isInPropertyBlock) Then
                        Return If(isInPropertyBlock, ImmutableArray.Create(s_get), ImmutableArray.Create(s_get, s_function, s_sub))
                    End If

                    ' Structure S : Public ReadOnly |
                    Return s_readOnlyMethods
            End Select

            Return ImmutableArray(Of RecommendedKeyword).Empty
        End Function

        Private Shared Function IsDirectlyIn(Of TBlock As SyntaxNode)(token As SyntaxToken) As Boolean
            Dim typeBlock = token.GetAncestor(Of TypeBlockSyntax)()
            Return TypeOf typeBlock Is TBlock
        End Function

        ''' <summary>
        ''' Whether the modifiers start a line inside a property that may still declare its Get.
        ''' </summary>
        Private Shared Function FollowsPropertyThatTakesGet(modifiers As SyntaxTokenList, <Runtime.InteropServices.Out> ByRef isInPropertyBlock As Boolean) As Boolean
            isInPropertyBlock = False
            Dim previousToken = modifiers.First().GetPreviousToken()
            Dim propertyBlock = previousToken.GetAncestor(Of PropertyBlockSyntax)()
            If propertyBlock IsNot Nothing AndAlso previousToken.GetAncestor(Of EndBlockStatementSyntax)() Is propertyBlock.EndPropertyStatement Then
                Return False
            End If

            Dim propertyStatement = If(propertyBlock?.PropertyStatement, previousToken.GetAncestor(Of PropertyStatementSyntax)())
            If propertyStatement Is Nothing OrElse propertyStatement.Modifiers.Any(SyntaxKind.WriteOnlyKeyword) Then
                Return False
            End If

            isInPropertyBlock = propertyBlock IsNot Nothing
            Return propertyBlock Is Nothing OrElse Not propertyBlock.Accessors.Any(Function(a) a.IsKind(SyntaxKind.GetAccessorBlock))
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
