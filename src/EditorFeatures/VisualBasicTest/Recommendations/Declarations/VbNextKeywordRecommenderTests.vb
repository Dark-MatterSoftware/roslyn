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

        <Fact>
        Public Sub InAfterForEachDeconstruction()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F()
        For Each (name, count) |
    End Sub
End Module</File>, "In")
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F()
        For Each (name As String, count As Integer) |
    End Sub
End Module</File>, "In")
        End Sub

        <Fact>
        Public Sub AsAfterANameInForEachDeconstruction()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F()
        For Each (name, count |
    End Sub
End Module</File>, "As")
        End Sub

        <Fact>
        Public Sub ByRefAfterDimAndReadOnlyAfterDimByRef()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F()
        Dim |
    End Sub
End Module</File>, "ByRef")
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F()
        Dim ByRef |
    End Sub
End Module</File>, "ReadOnly")
        End Sub

        <Fact>
        Public Sub ByRefMissingAfterStaticAndOn17_13()
            VerifyRecommendationsMissing(<File VbNext="True">Module M
    Sub F()
        Static |
    End Sub
End Module</File>, "ByRef")
            VerifyRecommendationsMissing(<File>Module M
    Sub F()
        Dim |
    End Sub
End Module</File>, "ByRef")
        End Sub

        <Fact>
        Public Sub ByRefBeforeFieldOfByRefStructure()
            VerifyRecommendationsContain(<File VbNext="True">ByRef Structure Cell
    Private |
End Structure</File>, "ByRef")
            VerifyRecommendationsContain(<File VbNext="True">ByRef Structure Cell
    Private ByRef |
End Structure</File>, "ReadOnly")
        End Sub

        <Fact>
        Public Sub ByRefFieldMissingOutsideByRefStructureAndOn17_13()
            VerifyRecommendationsMissing(<File VbNext="True">Structure Plain
    Private |
End Structure</File>, "ByRef")
            VerifyRecommendationsMissing(<File VbNext="True">ByRef Structure Cell
    Private Shared |
End Structure</File>, "ByRef")
            VerifyRecommendationsMissing(<File VbNext="True">ByRef Structure Cell
    Private ByRef |
End Structure</File>, "ByRef")
            VerifyRecommendationsMissing(<File>ByRef Structure Cell
    Private |
End Structure</File>, "ByRef")
        End Sub

        <Fact>
        Public Sub ByRefAfterEqualsInSubNewOfByRefStructure()
            VerifyRecommendationsContain(<File VbNext="True">ByRef Structure Cell
    Private ByRef _value As Integer

    Sub New(ByRef value As Integer)
        _value = |
    End Sub
End Structure</File>, "ByRef")
            VerifyRecommendationsMissing(<File VbNext="True">ByRef Structure Cell
    Private ByRef _value As Integer

    Sub Reset(ByRef value As Integer)
        _value = |
    End Sub
End Structure</File>, "ByRef")
        End Sub

        <Fact>
        Public Sub ReadOnlyAfterByRefParameter()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub F(ByRef |
End Module</File>, "ReadOnly")
            VerifyRecommendationsMissing(<File>Module M
    Sub F(ByRef |
End Module</File>, "ReadOnly")
        End Sub

        <Fact>
        Public Sub ReadOnlyAndFunctionAfterByRefMember()
            VerifyRecommendationsContain(<File VbNext="True">Class C
    Public ByRef |
End Class</File>, "ReadOnly", "Function")
            VerifyRecommendationsContain(<File VbNext="True">Class C
    Public ByRef ReadOnly |
End Class</File>, "Function")
        End Sub

        <Fact>
        Public Sub OverridableAfterSharedInInterface()
            VerifyRecommendationsContain(<File VbNext="True">Interface I
    Shared |
End Interface</File>, "Overridable")
            VerifyRecommendationsMissing(<File VbNext="True">Interface I
    Shared Overridable |
End Interface</File>, "Overridable")
            VerifyRecommendationsMissing(<File>Interface I
    Shared |
End Interface</File>, "Overridable")
        End Sub

        <Fact>
        Public Sub ByRefInTypeParameterConstraint()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Function Echo(Of T As |)(value As T) As T
    End Function
End Module</File>, "ByRef", "Class", "Structure", "New")
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub Close(Of T As {System.IDisposable, |})(value As T)
    End Sub
End Module</File>, "ByRef")
            VerifyRecommendationsMissing(<File VbNext="True">Module M
    Sub Close(Of T As {Class, |})(value As T)
    End Sub
End Module</File>, "ByRef")
            VerifyRecommendationsMissing(<File VbNext="True">Module M
    Sub Close(Of T As {ByRef, |})(value As T)
    End Sub
End Module</File>, "ByRef")
            VerifyRecommendationsMissing(<File>Module M
    Function Echo(Of T As |)(value As T) As T
    End Function
End Module</File>, "ByRef")
        End Sub

        <Fact>
        Public Sub WideningAndNarrowingAfterSharedMustOverrideOrOverridableInInterface()
            VerifyRecommendationsContain(<File VbNext="True">Interface I(Of T As I(Of T))
    Shared MustOverride |
End Interface</File>, "Widening", "Narrowing")
            VerifyRecommendationsContain(<File VbNext="True">Interface I(Of T As I(Of T))
    Shared Overridable |
End Interface</File>, "Widening", "Narrowing")
            VerifyRecommendationsMissing(<File VbNext="True">Interface I(Of T As I(Of T))
    |
End Interface</File>, "Widening", "Narrowing")
            VerifyRecommendationsMissing(<File>Interface I(Of T As I(Of T))
    Shared MustOverride |
End Interface</File>, "Widening", "Narrowing")
        End Sub

        <Fact>
        Public Sub OperatorAfterWideningInInterface()
            VerifyRecommendationsContain(<File VbNext="True">Interface I(Of T As I(Of T))
    Shared MustOverride Widening |
End Interface</File>, "Operator")
            VerifyRecommendationsContain(<File VbNext="True">Interface I(Of T As I(Of T))
    Shared Overridable Narrowing |
End Interface</File>, "Operator")
            VerifyRecommendationsMissing(<File>Interface I(Of T As I(Of T))
    Shared MustOverride Widening |
End Interface</File>, "Operator")
        End Sub

        <Fact>
        Public Sub AccessibilityInInterface()
            VerifyRecommendationsContain(<File VbNext="True">Interface I
    |
End Interface</File>, "Public", "Private", "Friend", "Protected", "Protected Friend")
            VerifyRecommendationsContain(<File VbNext="True">Interface I
    Protected |
End Interface</File>, "Friend", "Function", "Sub", "Property")
            VerifyRecommendationsMissing(<File VbNext="True">Interface I
    Private |
End Interface</File>, "Public", "Private", "Friend", "Protected")
            VerifyRecommendationsMissing(<File>Interface I
    |
End Interface</File>, "Public", "Private", "Friend", "Protected")
        End Sub

        <Fact>
        Public Sub CustomEventInInterface()
            VerifyRecommendationsContain(<File VbNext="True">Interface I
    |
End Interface</File>, "Custom Event")
            VerifyRecommendationsContain(<File VbNext="True">Interface I
    Custom |
End Interface</File>, "Event")
            VerifyRecommendationsMissing(<File>Interface I
    |
End Interface</File>, "Custom Event")
        End Sub

        <Fact>
        Public Sub OptionalAndParamArrayInLambdaParameters()
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub Use()
        Dim f = Function(|
    End Sub
End Module</File>, "Optional", "ParamArray", "ByVal")
            VerifyRecommendationsContain(<File VbNext="True">Module M
    Sub Use()
        Dim f = Function(Optional x As Integer = 1, |
    End Sub
End Module</File>, "Optional")
            VerifyRecommendationsMissing(<File>Module M
    Sub Use()
        Dim f = Function(|
    End Sub
End Module</File>, "Optional", "ParamArray")
        End Sub

        <Fact>
        Public Sub MembersAfterSharedInInterface()
            VerifyRecommendationsContain(<File VbNext="True">Interface I
    Shared |
End Interface</File>, "Function", "Sub", "Property")
        End Sub

        <Fact>
        Public Sub AccessorsInInterfaceProperty()
            VerifyRecommendationsContain(<File VbNext="True">Interface I
    Property P As Integer
        |
    End Property
End Interface</File>, "Get", "Set")
            VerifyRecommendationsContain(<File VbNext="True">Interface I
    ReadOnly Property P As Integer
        |
    End Property
End Interface</File>, "Get")
            VerifyRecommendationsMissing(<File>Interface I
    Property P As Integer
        |
End Interface</File>, "Get", "Set")
        End Sub

        <Fact>
        Public Sub OperatorAfterSharedMustOverrideOrOverridableInInterface()
            VerifyRecommendationsContain(<File VbNext="True">Interface I(Of T As I(Of T))
    Shared MustOverride |
End Interface</File>, "Operator", "Function", "Property")
            VerifyRecommendationsContain(<File VbNext="True">Interface I(Of T As I(Of T))
    Shared Overridable |
End Interface</File>, "Operator", "Function", "Sub", "Property")
            VerifyRecommendationsMissing(<File VbNext="True">Interface I(Of T As I(Of T))
    |
End Interface</File>, "Operator")
            VerifyRecommendationsMissing(<File>Interface I(Of T As I(Of T))
    Shared MustOverride |
End Interface</File>, "Operator")
        End Sub

        <Fact>
        Public Sub FunctionAndSubAfterReadOnlyInStructure()
            VerifyRecommendationsContain(<File VbNext="True">Structure S
    Public ReadOnly |
End Structure</File>, "Function", "Sub", "Property")
            VerifyRecommendationsMissing(<File VbNext="True">Class C
    Public ReadOnly |
End Class</File>, "Function", "Sub")
            VerifyRecommendationsMissing(<File VbNext="True">Structure S
    Shared ReadOnly |
End Structure</File>, "Function", "Sub")
            VerifyRecommendationsMissing(<File>Structure S
    Public ReadOnly |
End Structure</File>, "Function", "Sub")
        End Sub

        <Fact>
        Public Sub ReadOnlyBeforeGetInStructureProperty()
            VerifyRecommendationsContain(<File VbNext="True">Structure S
    Property P As Integer
        |
    End Property
End Structure</File>, "ReadOnly", "Get", "Set")
            VerifyRecommendationsContain(<File VbNext="True">Structure S
    Property P As Integer
        ReadOnly |
    End Property
End Structure</File>, "Get")
            VerifyRecommendationsMissing(<File VbNext="True">Structure S
    Property P As Integer
        ReadOnly |
    End Property
End Structure</File>, "Set")
            VerifyRecommendationsMissing(<File VbNext="True">Class C
    Property P As Integer
        ReadOnly |
    End Property
End Class</File>, "Get")
            VerifyRecommendationsMissing(<File>Structure S
    Property P As Integer
        ReadOnly |
    End Property
End Structure</File>, "Get")
        End Sub

        <Fact>
        Public Sub InAfterParenthesizedForEachControlVariableMissingOn17_13()
            VerifyRecommendationsMissing(<File>Module M
    Sub F()
        For Each (name, count) |
    End Sub
End Module</File>, "In")
        End Sub
    End Class
End Namespace
