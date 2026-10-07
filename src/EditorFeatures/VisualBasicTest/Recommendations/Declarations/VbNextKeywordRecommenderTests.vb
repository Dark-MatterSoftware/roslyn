' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Recommendations.Declarations
    <Trait(Traits.Feature, Traits.Features.KeywordRecommending)>
    Public Class VbNextKeywordRecommenderTests
        Inherits RecommenderTests

        <Fact>
        Public Sub InitOnlyAndRequiredInClass()
            VerifyRecommendationsContain(<ClassDeclaration VbNext="True">|</ClassDeclaration>, "InitOnly", "Required")
        End Sub

        <Fact>
        Public Sub InitOnlyAndRequiredMissingOn17_13()
            VerifyRecommendationsMissing(<ClassDeclaration>|</ClassDeclaration>, "InitOnly", "Required", "Record")
        End Sub

        <Fact>
        Public Sub PropertyAfterInitOnly()
            VerifyRecommendationsContain(<ClassDeclaration VbNext="True">Public InitOnly |</ClassDeclaration>, "Property", "Required")
        End Sub

        <Fact>
        Public Sub NoFieldOrMethodAfterInitOnly()
            VerifyRecommendationsMissing(<ClassDeclaration VbNext="True">Public InitOnly |</ClassDeclaration>, "Sub", "Function", "ReadOnly", "WriteOnly", "InitOnly", "Const")
        End Sub

        <Fact>
        Public Sub PropertyAfterRequired()
            VerifyRecommendationsContain(<ClassDeclaration VbNext="True">Public Required |</ClassDeclaration>, "Property", "InitOnly")
        End Sub

        <Fact>
        Public Sub RequiredMissingAfterShared()
            VerifyRecommendationsMissing(<ClassDeclaration VbNext="True">Shared |</ClassDeclaration>, "Required")
        End Sub

        <Fact>
        Public Sub InitOnlyButNotRequiredInInterface()
            VerifyRecommendationsContain(<InterfaceDeclaration VbNext="True">|</InterfaceDeclaration>, "InitOnly")
            VerifyRecommendationsMissing(<InterfaceDeclaration VbNext="True">|</InterfaceDeclaration>, "Required")
        End Sub

        <Fact>
        Public Sub RecordInNamespace()
            VerifyRecommendationsContain(<NamespaceDeclaration VbNext="True">|</NamespaceDeclaration>, "Record")
        End Sub

        <Fact>
        Public Sub ClassAndStructureAfterRecord()
            VerifyRecommendationsAreExactly(<NamespaceDeclaration VbNext="True">Record |</NamespaceDeclaration>, "Class", "Structure")
        End Sub

        <Fact>
        Public Sub ExtensionInModule()
            VerifyRecommendationsContain(<ModuleDeclaration VbNext="True">|</ModuleDeclaration>, "Extension")
        End Sub

        <Fact>
        Public Sub ExtensionMissingInClass()
            VerifyRecommendationsMissing(<ClassDeclaration VbNext="True">|</ClassDeclaration>, "Extension")
        End Sub

        <Fact>
        Public Sub ExtensionMissingAfterModifier()
            VerifyRecommendationsMissing(<ModuleDeclaration VbNext="True">Public |</ModuleDeclaration>, "Extension")
        End Sub

        <Fact>
        Public Sub ExtensionMissingOn17_13()
            VerifyRecommendationsMissing(<ModuleDeclaration>|</ModuleDeclaration>, "Extension")
        End Sub
    End Class
End Namespace
