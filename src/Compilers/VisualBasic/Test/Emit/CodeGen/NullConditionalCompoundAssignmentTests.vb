' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class NullConditionalCompoundAssignmentTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub CompoundAssignThroughNullConditional()
            Dim compilation = CreateCompilation("
Imports System

Class Box
    Public Count As Integer
    Public Property Total As Integer
    Public Property Name As String
    Public Items As Integer() = {1, 2}
    Public Inner As Box
End Class

Module Program
    Private _boxes As Integer

    Function GetBox(b As Box) As Box
        _boxes += 1
        Return b
    End Function

    Function Trace(value As Integer) As Integer
        Console.Write(""v"")
        Return value
    End Function

    Sub Main()
        Dim missing As Box = Nothing
        missing?.Count += Trace(1)
        missing?.Name &= ""!""
        missing?.Inner?.Count -= 1

        Dim b As New Box With {.Inner = New Box()}
        b?.Count += 5
        b?.Count -= 2
        b?.Count *= Trace(3)
        b?.Total += 4
        b?.Total ^= 2
        b?.Name &= ""x""
        b?.Name &= ""y""
        b?.Items(1) += 10
        b?.Inner?.Count += 7
        b?.Inner?.Count += 1
        missing?.Inner?.Total = 9
        b?.Inner?.Total = 3
        b?.Inner.Total -= 1
        GetBox(b)?.Count \= 3
        GetBox(Nothing)?.Count += 1
        Console.Write($"" {b.Count} {b.Total} {b.Name} {b.Items(1)} {b.Inner.Count} {b.Inner.Total} {_boxes}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="v 3 16 xy 12 8 2 2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NothingCoalescingAssignThroughNullConditional()
            Dim compilation = CreateCompilation("
Imports System

Class Box
    Public Name As String
    Public Property Label As String
    Public Inner As Box
End Class

Module Program
    Function Trace(value As String) As String
        Console.Write(""v"")
        Return value
    End Function

    Sub Main()
        Dim missing As Box = Nothing
        missing?.Name ?= Trace(""x"")
        missing?.Inner?.Name ?= Trace(""x"")

        Dim c As New Box With {.Inner = New Box()}
        c?.Name ?= Trace(""first"")
        c?.Name ?= ""second""
        c?.Label ?= ""label""
        c?.Inner?.Name ?= ""inner""
        Console.Write($"" {c.Name} {c.Label} {c.Inner.Name}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="v first label inner").VerifyDiagnostics()
        End Sub

        <Theory>
        <InlineData("b?.Count += 1")>
        <InlineData("b?.Total += 1")>
        <InlineData("b?.Total ^= 2")>
        <InlineData("b?.Name &= ""x""")>
        <InlineData("b?.Items(1) += 10")>
        <InlineData("b?.Inner?.Count += 7")>
        <InlineData("b?.Inner.Total -= 1")>
        <InlineData("b?.Count \= 3")>
        <InlineData("b?.Inner?.Count = 7")>
        <InlineData("b?.Name ?= ""x""")>
        <InlineData("b?.Inner?.Name ?= ""x""")>
        Public Sub EachFormBindsAndEmits(statement As String)
            Dim compilation = CreateCompilation("
Class Box
    Public Count As Integer
    Public Property Total As Integer
    Public Property Name As String
    Public Items As Integer() = {1, 2}
    Public Inner As Box
End Class

Module Program
    Sub Main()
        Dim b As New Box()
        " & statement & "
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            CompileAndVerify(compilation).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CompoundAssignThroughNullConditionalErrors()
            Dim compilation = CreateCompilation("
Class Box
    Public Count As Integer
    Public ReadOnly Property Fixed As Integer
        Get
            Return 1
        End Get
    End Property
    Public Name As String
End Class

Module Program
    Sub Main()
        Dim b As New Box()
        b?.Fixed += 1
        b?.Count += 1.5
        b?.Name -= 1
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30526: Property 'Fixed' is 'ReadOnly'.
        b?.Fixed += 1
        ~~~~~~~~~~~~~
BC30512: Option Strict On disallows implicit conversions from 'Double' to 'Integer'.
        b?.Count += 1.5
        ~~~~~~~~~~~~~~~
BC30512: Option Strict On disallows implicit conversions from 'Double' to 'String'.
        b?.Name -= 1
        ~~~~~~~~~~~~
BC30512: Option Strict On disallows implicit conversions from 'String' to 'Double'.
        b?.Name -= 1
          ~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub CompoundAssignThroughNullConditionalNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Class Box
    Public Count As Integer
End Class

Module Program
    Sub Main()
        Dim b As New Box()
        b?.Count += 1
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        b?.Count += 1
        ~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
