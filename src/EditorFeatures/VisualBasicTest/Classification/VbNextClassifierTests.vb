' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Classification
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Classification
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Classification.FormattedClassifications
Imports Microsoft.CodeAnalysis.Remote.Testing
Imports Microsoft.CodeAnalysis.Text

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Classification
    <Trait(Traits.Feature, Traits.Features.Classification)>
    Public Class VbNextClassifierTests
        Inherits AbstractVisualBasicClassifierTests

        Private Shared ReadOnly s_vbNext As ParseOptions() = {VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)}

        Protected Overrides Async Function GetClassificationSpansAsync(code As String, spans As ImmutableArray(Of TextSpan), parseOptions As ParseOptions, testHost As TestHost) As Task(Of ImmutableArray(Of ClassifiedSpan))
            Using workspace = CreateWorkspace(code, testHost, parseOptions)
                Dim document = workspace.CurrentSolution.Projects.First().Documents.First()
                Return Await GetSyntacticClassificationsAsync(document, spans)
            End Using
        End Function

        Private Shared Function [Operator](text As String) As FormattedClassification
            Return New FormattedClassification(text, ClassificationTypeNames.Operator)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestInitOnlyProperty(testHost As TestHost) As Task
            Await TestInClassAsync("Public InitOnly Property Name As String",
                testHost,
                s_vbNext,
                Keyword("Public"),
                Keyword("InitOnly"),
                Keyword("Property"),
                [Property]("Name"),
                Keyword("As"),
                Keyword("String"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestRequiredProperty(testHost As TestHost) As Task
            Await TestInClassAsync("Public Required Property Name As String",
                testHost,
                s_vbNext,
                Keyword("Public"),
                Keyword("Required"),
                Keyword("Property"),
                [Property]("Name"),
                Keyword("As"),
                Keyword("String"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestRecordClass(testHost As TestHost) As Task
            Await TestAsync("Record Class Point
End Class",
                testHost,
                s_vbNext,
                Keyword("Record"),
                Keyword("Class"),
                [Class]("Point"),
                Keyword("End"),
                Keyword("Class"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestExtensionBlock(testHost As TestHost) As Task
            Await TestAsync("Module M
    Extension(s As String)
    End Extension
End Module",
                testHost,
                s_vbNext,
                Keyword("Module"),
                [Module]("M"),
                Keyword("Extension"),
                Punctuation.OpenParen,
                Parameter("s"),
                Keyword("As"),
                Keyword("String"),
                Punctuation.CloseParen,
                Keyword("End"),
                Keyword("Extension"),
                Keyword("End"),
                Keyword("Module"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestGuidLiteral(testHost As TestHost) As Task
            Await TestInMethodAsync("Dim g = &G{00000000-0000-0000-0000-000000000001}",
                testHost,
                s_vbNext,
                Keyword("Dim"),
                Local("g"),
                Operators.Equals,
                Number("&G{00000000-0000-0000-0000-000000000001}"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestRange(testHost As TestHost) As Task
            Await TestInMethodAsync("Dim s = items(1..^1)",
                testHost,
                s_vbNext,
                Keyword("Dim"),
                Local("s"),
                Operators.Equals,
                Identifier("items"),
                Punctuation.OpenParen,
                Number("1"),
                [Operator](".."),
                [Operator]("^"),
                Number("1"),
                Punctuation.CloseParen)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestNothingCoalescingAssignment(testHost As TestHost) As Task
            Await TestInMethodAsync("x ?= y",
                testHost,
                s_vbNext,
                Identifier("x"),
                [Operator]("?="),
                Identifier("y"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestRawAndUtf8Strings(testHost As TestHost) As Task
            Await TestInMethodAsync("Dim t = r""""""say ""hi"" now""""""
Dim b = ""hi""u8",
                testHost,
                s_vbNext,
                Keyword("Dim"),
                Local("t"),
                Operators.Equals,
                [String]("r""""""say ""hi"" now"""""""),
                Keyword("Dim"),
                Local("b"),
                Operators.Equals,
                [String]("""hi""u8"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestAsyncUsingAndAwaitEach(testHost As TestHost) As Task
            Await TestInMethodAsync("Async Using r = Open()
End Using
Await Each n In items
Next",
                testHost,
                s_vbNext,
                Keyword("Async"),
                Keyword("Using"),
                Identifier("r"),
                Operators.Equals,
                Identifier("Open"),
                Punctuation.OpenParen,
                Punctuation.CloseParen,
                Keyword("End"),
                Keyword("Using"),
                Keyword("Await"),
                ControlKeyword("Each"),
                Identifier("n"),
                ControlKeyword("In"),
                Identifier("items"),
                ControlKeyword("Next"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestIntoAfterTypeTests(testHost As TestHost) As Task
            Await TestInMethodAsync("If TypeOf o Is String Into s Then
End If
Select Case o
    Case Is TypeOf Integer Into n
End Select",
                testHost,
                s_vbNext,
                ControlKeyword("If"),
                Keyword("TypeOf"),
                Identifier("o"),
                Keyword("Is"),
                Keyword("String"),
                Keyword("Into"),
                Identifier("s"),
                ControlKeyword("Then"),
                ControlKeyword("End"),
                ControlKeyword("If"),
                ControlKeyword("Select"),
                ControlKeyword("Case"),
                Identifier("o"),
                ControlKeyword("Case"),
                Keyword("Is"),
                Keyword("TypeOf"),
                Keyword("Integer"),
                Keyword("Into"),
                Identifier("n"),
                ControlKeyword("End"),
                ControlKeyword("Select"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestAsyncIteratorLambdaAndGuard(testHost As TestHost) As Task
            Await TestInMethodAsync("Dim numbers = Async Iterator Function()
                  Yield 1
              End Function
If TypeOf o IsNot String Into s Then Return",
                testHost,
                s_vbNext,
                Keyword("Dim"),
                Local("numbers"),
                Operators.Equals,
                Keyword("Async"),
                Keyword("Iterator"),
                Keyword("Function"),
                Punctuation.OpenParen,
                Punctuation.CloseParen,
                ControlKeyword("Yield"),
                Number("1"),
                Keyword("End"),
                Keyword("Function"),
                ControlKeyword("If"),
                Keyword("TypeOf"),
                Identifier("o"),
                Keyword("IsNot"),
                Keyword("String"),
                Keyword("Into"),
                Identifier("s"),
                ControlKeyword("Then"),
                ControlKeyword("Return"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestByRefLocal(testHost As TestHost) As Task
            Await TestInMethodAsync("Dim ByRef ReadOnly first = values(0)",
                testHost,
                s_vbNext,
                Keyword("Dim"),
                Keyword("ByRef"),
                Keyword("ReadOnly"),
                Local("first"),
                Operators.Equals,
                Identifier("values"),
                Punctuation.OpenParen,
                Number("0"),
                Punctuation.CloseParen)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestRefField(testHost As TestHost) As Task
            Await TestAsync("ByRef Structure Cell
    Private ByRef ReadOnly _value As Integer
    Sub New(ByRef value As Integer)
        _value = ByRef value
    End Sub
End Structure",
                testHost,
                s_vbNext,
                Keyword("ByRef"),
                Keyword("Structure"),
                Struct("Cell"),
                Keyword("Private"),
                Keyword("ByRef"),
                Keyword("ReadOnly"),
                Field("_value"),
                Keyword("As"),
                Keyword("Integer"),
                Keyword("Sub"),
                Keyword("New"),
                Punctuation.OpenParen,
                Keyword("ByRef"),
                Parameter("value"),
                Keyword("As"),
                Keyword("Integer"),
                Punctuation.CloseParen,
                Identifier("_value"),
                Operators.Equals,
                Keyword("ByRef"),
                Identifier("value"),
                Keyword("End"),
                Keyword("Sub"),
                Keyword("End"),
                Keyword("Structure"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestWaveElevenForms(testHost As TestHost) As Task
            Await TestAsync("Interface I
    Shared Overridable Function N() As String
    End Function
    ByRef ReadOnly Function F(ByRef ReadOnly v As Integer) As Integer
End Interface",
                testHost,
                s_vbNext,
                Keyword("Interface"),
                [Interface]("I"),
                Keyword("Shared"),
                Keyword("Overridable"),
                Keyword("Function"),
                Method("N"),
                [Static]("N"),
                Punctuation.OpenParen,
                Punctuation.CloseParen,
                Keyword("As"),
                Keyword("String"),
                Keyword("End"),
                Keyword("Function"),
                Keyword("ByRef"),
                Keyword("ReadOnly"),
                Keyword("Function"),
                Method("F"),
                Punctuation.OpenParen,
                Keyword("ByRef"),
                Keyword("ReadOnly"),
                Parameter("v"),
                Keyword("As"),
                Keyword("Integer"),
                Punctuation.CloseParen,
                Keyword("As"),
                Keyword("Integer"),
                Keyword("End"),
                Keyword("Interface"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestForEachDeconstruction(testHost As TestHost) As Task
            Await TestInMethodAsync("For Each (name, count As Integer) In items
Next",
                testHost,
                s_vbNext,
                ControlKeyword("For"),
                ControlKeyword("Each"),
                Punctuation.OpenParen,
                Identifier("name"),
                Punctuation.Comma,
                Identifier("count"),
                Keyword("As"),
                Keyword("Integer"),
                Punctuation.CloseParen,
                ControlKeyword("In"),
                Identifier("items"),
                ControlKeyword("Next"))
        End Function
    End Class

    <Trait(Traits.Feature, Traits.Features.Classification)>
    Public Class VbNextSemanticClassifierTests
        Inherits AbstractVisualBasicClassifierTests

        Private Shared ReadOnly s_vbNext As ParseOptions() = {VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)}

        Protected Overrides Async Function GetClassificationSpansAsync(code As String, spans As ImmutableArray(Of TextSpan), parseOptions As ParseOptions, testHost As TestHost) As Task(Of ImmutableArray(Of ClassifiedSpan))
            Using workspace = CreateWorkspace(code, testHost, parseOptions)
                Dim document = workspace.CurrentSolution.Projects.First().Documents.First()
                Return Await GetSemanticClassificationsAsync(document, spans)
            End Using
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestFieldInPropertyBody(testHost As TestHost) As Task
            Await TestAsync("Class C
    Private _other As Integer
    Property Name As String
        Get
            Return Field
        End Get
        Set
            Field = Value
        End Set
    End Property
    ReadOnly Property Other As Integer
        Get
            Return _other
        End Get
    End Property
End Class",
                testHost,
                s_vbNext,
                Keyword("Field"),
                Keyword("Field"),
                Parameter("Value"),
                Field("_other"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestForEachDeconstructionAndLambdaParameterAttribute(testHost As TestHost) As Task
            Await TestAsync("Class FromQueryAttribute
    Inherits System.Attribute
End Class
Module M
    Sub F(items As (String, Integer)())
        Dim total = 0
        For Each (name, count) In items
            total += count
        Next
        Dim find = Function(<FromQuery> id As Integer) id
    End Sub
End Module",
                testHost,
                s_vbNext,
                [Namespace]("System"),
                [Class]("Attribute"),
                Parameter("items"),
                Local("total"),
                Local("count"),
                [Class]("FromQuery"),
                Parameter("id"))
        End Function
    End Class
End Namespace
