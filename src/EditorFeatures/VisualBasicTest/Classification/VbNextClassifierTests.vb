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
        Public Async Function TestWaveTwelveForms(testHost As TestHost) As Task
            Await TestAsync("Interface I(Of T As I(Of T))
    Shared MustOverride Operator +(a As T, b As T) As T
End Interface
Structure S
    ReadOnly Function N() As Integer
    End Function
    Property P As Integer
        ReadOnly Get
        End Get
    End Property
End Structure",
                testHost,
                s_vbNext,
                Keyword("Interface"),
                [Interface]("I"),
                Punctuation.OpenParen,
                Keyword("Of"),
                TypeParameter("T"),
                Keyword("As"),
                Identifier("I"),
                Punctuation.OpenParen,
                Keyword("Of"),
                Identifier("T"),
                Punctuation.CloseParen,
                Punctuation.CloseParen,
                Keyword("Shared"),
                Keyword("MustOverride"),
                Keyword("Operator"),
                Operators.Plus,
                Punctuation.OpenParen,
                Parameter("a"),
                Keyword("As"),
                Identifier("T"),
                Punctuation.Comma,
                Parameter("b"),
                Keyword("As"),
                Identifier("T"),
                Punctuation.CloseParen,
                Keyword("As"),
                Identifier("T"),
                Keyword("End"),
                Keyword("Interface"),
                Keyword("Structure"),
                Struct("S"),
                Keyword("ReadOnly"),
                Keyword("Function"),
                Method("N"),
                Punctuation.OpenParen,
                Punctuation.CloseParen,
                Keyword("As"),
                Keyword("Integer"),
                Keyword("End"),
                Keyword("Function"),
                Keyword("Property"),
                [Property]("P"),
                Keyword("As"),
                Keyword("Integer"),
                Keyword("ReadOnly"),
                Keyword("Get"),
                Keyword("End"),
                Keyword("Get"),
                Keyword("End"),
                Keyword("Property"),
                Keyword("End"),
                Keyword("Structure"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestWaveThirteenForms(testHost As TestHost) As Task
            Await TestAsync("Interface I(Of T As I(Of T))
    Shared MustOverride Widening Operator CType(value As Integer) As T
    ReadOnly Property Name As String
        Get
        End Get
    End Property
    Shared Function Describe() As String
    End Function
End Interface",
                testHost,
                s_vbNext,
                Keyword("Interface"),
                [Interface]("I"),
                Punctuation.OpenParen,
                Keyword("Of"),
                TypeParameter("T"),
                Keyword("As"),
                Identifier("I"),
                Punctuation.OpenParen,
                Keyword("Of"),
                Identifier("T"),
                Punctuation.CloseParen,
                Punctuation.CloseParen,
                Keyword("Shared"),
                Keyword("MustOverride"),
                Keyword("Widening"),
                Keyword("Operator"),
                Keyword("CType"),
                Punctuation.OpenParen,
                Parameter("value"),
                Keyword("As"),
                Keyword("Integer"),
                Punctuation.CloseParen,
                Keyword("As"),
                Identifier("T"),
                Keyword("ReadOnly"),
                Keyword("Property"),
                [Property]("Name"),
                Keyword("As"),
                Keyword("String"),
                Keyword("Get"),
                Keyword("End"),
                Keyword("Get"),
                Keyword("End"),
                Keyword("Property"),
                Keyword("Shared"),
                Keyword("Function"),
                Method("Describe"),
                [Static]("Describe"),
                Punctuation.OpenParen,
                Punctuation.CloseParen,
                Keyword("As"),
                Keyword("String"),
                Keyword("End"),
                Keyword("Function"),
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
        Public Async Function TestWaveSixteenForms(testHost As TestHost) As Task
            Await TestAsync("Imports Num = Integer
Imports Ints = Integer()
Structure Point
    Public Left As Integer
End Structure
Class Box
    Public Count As Integer
End Class
Module M
    Sub F(p As Point, b As Box, values As Ints)
        Dim n As Num = 1
        Dim q = p With {.Left = n}
        Dim a = New With {.Size = 1}
        Dim c = a With {.Size = 2}
        Dim same = (1, 2) = (n, 2)
        b?.Count += 1
        Dim list As System.Collections.Generic.List(Of Integer) = {1, 2}
    End Sub
End Module",
                testHost,
                s_vbNext,
                Struct("Num"),
                FormattedClassifications.Array("Ints"),
                Struct("Point"),
                [Class]("Box"),
                FormattedClassifications.Array("Ints"),
                Struct("Num"),
                Parameter("p"),
                Field("Left"),
                Local("n"),
                [Property]("Size"),
                Local("a"),
                [Property]("Size"),
                Local("n"),
                Parameter("b"),
                Field("Count"),
                [Namespace]("System"),
                [Namespace]("Collections"),
                [Namespace]("Generic"),
                [Class]("List"))
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

        <Theory, CombinatorialData>
        Public Async Function TestWaveTwentySixForms(testHost As TestHost) As Task
            Await TestAsync("Class NoteAttribute
    Inherits System.Attribute
    Sub New(name As String)
    End Sub
End Class
Module M
    <Note(NameOf(count))>
    Sub F(count As Integer, values As System.Collections.Generic.List(Of Integer), s As String)
        Dim last = values(^1)
        Dim part = s(1..3)
    End Sub
End Module",
                testHost,
                s_vbNext,
                [Namespace]("System"),
                [Class]("Attribute"),
                [Class]("Note"),
                Parameter("count"),
                [Namespace]("System"),
                [Namespace]("Collections"),
                [Namespace]("Generic"),
                [Class]("List"),
                Parameter("values"),
                Parameter("s"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestWaveThirtyThreeForms(testHost As TestHost) As Task
            Await TestAsync("Module M
    Async Function NextId(order As (Integer, String), low As Integer, high As Integer) As System.Threading.Tasks.Task(Of Integer)
        Static count As Integer
        Select Case order
            Case Is (> low, ""a""), Is (low To high, ""b"")
                count += 1
        End Select
        Await System.Threading.Tasks.Task.Yield()
        Return count
    End Function
End Module",
                testHost,
                s_vbNext,
                [Namespace]("System"),
                [Namespace]("Threading"),
                [Namespace]("Tasks"),
                [Class]("Task"),
                Parameter("order"),
                Parameter("low"),
                Parameter("low"),
                Parameter("high"),
                Local("count"),
                [Static]("count"),
                [Namespace]("System"),
                [Namespace]("Threading"),
                [Namespace]("Tasks"),
                [Class]("Task"),
                Method("Yield"),
                [Static]("Yield"),
                Local("count"),
                [Static]("count"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestWaveThirtyOneForms(testHost As TestHost) As Task
            Await TestAsync("Class Box
End Class
Module M
    Sub F()
        Dim ((item As Box, n As Long), label As String) = ((New Box(), 1), ""a"")
        System.Console.Write(n & label)
    End Sub
End Module",
                testHost,
                s_vbNext,
                [Class]("Box"),
                [Class]("Box"),
                [Namespace]("System"),
                [Class]("Console"),
                Method("Write"),
                [Static]("Write"),
                Local("n"),
                Local("label"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestWaveThirtyForms(testHost As TestHost) As Task
            Await TestAsync("Class Box
    Public Width As Integer
End Class
Module M
    Sub F(box As Box)
        Dim ((x, y), label) = ((1, 2), ""a"")
        (box.Width, ) = (x + y, label)
        For Each ((p, ), q) In {((1, 2), ""b"")}
            System.Console.Write(p & q)
        Next
    End Sub
End Module",
                testHost,
                s_vbNext,
                [Class]("Box"),
                Parameter("box"),
                Field("Width"),
                Local("x"),
                Local("y"),
                Local("label"),
                [Namespace]("System"),
                [Class]("Console"),
                Method("Write"),
                [Static]("Write"),
                Local("p"),
                Local("q"))
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestWaveTwentySevenForms(testHost As TestHost) As Task
            Await TestAsync("Class NoteAttribute
    Inherits System.Attribute
    Sub New(name As String)
    End Sub
End Class
Class Table
    <Note(NameOf(key))>
    Default ReadOnly Property Item(<Note(NameOf(key))> key As String) As Integer
        Get
            Return 0
        End Get
    End Property
End Class
Delegate Function Mapper(<Note(NameOf(value))> value As String) As String",
                testHost,
                s_vbNext,
                [Namespace]("System"),
                [Class]("Attribute"),
                [Class]("Note"),
                Parameter("key"),
                [Class]("Note"),
                Parameter("key"),
                [Class]("Note"),
                Parameter("value"))
        End Function
    End Class
End Namespace
