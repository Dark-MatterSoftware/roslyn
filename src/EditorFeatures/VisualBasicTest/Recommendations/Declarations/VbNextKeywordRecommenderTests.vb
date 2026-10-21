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

        <Fact>
        Public Sub EachAfterAwaitInAsyncMethod()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Async Sub F()
        Await |
    End Sub
End Module</File>, "Each")
        End Sub

        <Fact>
        Public Sub EachAfterAwaitMissingOn17_13()
            VerifyRecommendationsMissing(<File>Module M
    Async Sub F()
        Await |
    End Sub
End Module</File>, "Each")
        End Sub

        <Fact>
        Public Sub UsingAfterAsyncInAsyncMethod()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Async Sub F()
        Async |
    End Sub
End Module</File>, "Using")
        End Sub

        <Fact>
        Public Sub IteratorAfterAsyncAndAsyncAfterIterator()
            VerifyRecommendationsContain(<ClassDeclaration VbNext="True">Async |</ClassDeclaration>, "Iterator")
            VerifyRecommendationsContain(<ClassDeclaration VbNext="True">Iterator |</ClassDeclaration>, "Async")
        End Sub

        <Fact>
        Public Sub IteratorAfterAsyncMissingOn17_13()
            VerifyRecommendationsMissing(<ClassDeclaration>Async |</ClassDeclaration>, "Iterator")
        End Sub

        <Fact>
        Public Sub IntoAfterCaseIsTypeOf()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F(o As Object)
        Select Case o
            Case Is TypeOf String |
        End Select
    End Sub
End Module</File>, "Into")
        End Sub

        <Fact>
        Public Sub IntoAfterTypeOfInIfCondition()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F(o As Object)
        If TypeOf o Is String |
    End Sub
End Module</File>, "Into")
        End Sub

        <Fact>
        Public Sub IntoMissingAfterTypeOfOutsideACondition()
            VerifyRecommendationsMissing(<File VbNext="True">Module M
    Sub F(o As Object)
        Dim b As Boolean = TypeOf o Is String |
    End Sub
End Module</File>, "Into")
        End Sub

        <Fact>
        Public Sub IteratorAfterAsyncInALambda()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F()
        Dim numbers = Async |
    End Sub
End Module</File>, "Iterator", "Function", "Sub")
        End Sub

        <Fact>
        Public Sub IteratorAfterAsyncInALambdaMissingOn17_13()
            VerifyRecommendationsMissing(<File>Module M
    Sub F()
        Dim numbers = Async |
    End Sub
End Module</File>, "Iterator")
        End Sub

        <Fact>
        Public Sub IntoAfterTypeOfIsNotInAGuardIf()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F(o As Object)
        If TypeOf o IsNot String |
    End Sub
End Module</File>, "Into")
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F(o As Object)
        If (TypeOf o IsNot String |
    End Sub
End Module</File>, "Into")
        End Sub

        <Fact>
        Public Sub IntoMissingAfterTypeOfIsNotOutsideAGuard()
            VerifyRecommendationsMissing(<File VbNext="True">Module M
    Sub F(o As Object)
        If o Is Nothing Then
        ElseIf TypeOf o IsNot String |
    End Sub
End Module</File>, "Into")
            VerifyRecommendationsMissing(<File VbNext="True">Module M
    Sub F(o As Object)
        If o IsNot Nothing AndAlso TypeOf o IsNot String |
    End Sub
End Module</File>, "Into")
        End Sub

        <Fact>
        Public Sub IntoAfterTypeOfMissingOn17_13()
            VerifyRecommendationsMissing(<File>Module M
    Sub F(o As Object)
        If TypeOf o Is String |
    End Sub
End Module</File>, "Into")
        End Sub

        <Fact>
        Public Sub FieldInPropertyAccessor()
            VerifyRecommendationsContain(<File VbNext="True">Class C
    Property P As Integer
        Get
            Return |
        End Get
        Set
        End Set
    End Property
End Class</File>, "Field")
        End Sub

        <Fact>
        Public Sub FieldMissingOutsidePropertyAccessorAndOn17_13()
            VerifyRecommendationsMissing(<File VbNext="True">Class C
    Function F() As Integer
        Return |
    End Function
End Class</File>, "Field")
            VerifyRecommendationsMissing(<File>Class C
    ReadOnly Property P As Integer
        Get
            Return |
        End Get
    End Property
End Class</File>, "Field")
        End Sub
    End Class
End Namespace
