' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class PositionalRelationalPatternTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub RelationalAndRangeElements_Match()
            Dim compilation = CreateCompilation("
Imports System

Class Point
    Public X As Integer
    Public Y As Integer
    Public Sub Deconstruct(ByRef x As Integer, ByRef y As Integer)
        x = Me.X : y = Me.Y
    End Sub
End Class

Module Program
    Function Describe(order As (Integer, String)) As String
        Select Case order
            Case Is (> 100, ""open"")
                Return ""big open""
            Case Is (1 To 9, ""draft"")
                Return ""small draft""
            Case Is (<> 0, ""open"")
                Return ""open""
            Case Is ((2 + 3), ""five"")
                Return ""five""
            Case Is (0, ""closed"")
                Return ""closed""
        End Select
        Return ""other""
    End Function

    Function Nested(n As ((Integer, Integer), String)) As String
        Select Case n
            Case Is ((> 0, <= 0), ""q"")
                Return ""quadrant""
            Case Is ((0, 0), ""o"")
                Return ""origin""
        End Select
        Return ""none""
    End Function

    Function Shape(value As Object) As String
        Select Case value
            Case Is TypeOf Point(> 0, 1 To 3) Into pt
                Return ""point "" & pt.X
        End Select
        Return ""no""
    End Function

    Sub Main()
        Console.Write(Describe((500, ""open"")) & "", "" & Describe((5, ""draft"")) & "", "" & Describe((7, ""open"")) & "", "" &
                      Describe((5, ""five"")) & "", "" & Describe((0, ""closed"")) & "", "" & Describe((10, ""draft"")))
        Console.Write("" | "" & Nested(((2, -1), ""q"")) & "", "" & Nested(((0, 0), ""o"")) & "", "" & Nested(((0, 1), ""q"")))
        Console.Write("" | "" & Shape(New Point With {.X = 4, .Y = 2}) & "", "" & Shape(New Point With {.X = 4, .Y = 9}))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="big open, small draft, open, five, closed, other | quadrant, origin, none | point 4, no").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RelationalElement_FollowsOptionCompare()
            Dim source = "
Module Program
    Function After(value As (String, Integer)) As String
        Select Case value
            Case Is (> ""a"", 1)
                Return ""after""
        End Select
        Return ""before""
    End Function

    Sub Main()
        System.Console.Write(After((""B"", 1)))
    End Sub
End Module
"
            CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseExe, parseOptions:=VbNext), expectedOutput:="before").VerifyDiagnostics()
            CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseExe.WithOptionCompareText(True), parseOptions:=VbNext), expectedOutput:="after").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ElementValueThatDoesNotConvert_IsAnError()
            Dim compilation = CreateCompilation("
Module Program
    Function Check(value As (Integer, String)) As String
        Select Case value
            Case Is (> New Object(), ""a"")
                Return ""a""
            Case Is (1 To New Object(), ""b"")
                Return ""b""
        End Select
        Return """"
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            Assert.NotEmpty(compilation.GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error))
        End Sub

        <Fact>
        Public Sub RelationalAndRangeElements_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Function Check(value As (Integer, String)) As String
        Select Case value
            Case Is (> 0, ""a"")
                Return ""a""
        End Select
        Return """"
    End Function
End Module
", parseOptions:=Vb17_13)

            ' Visual Basic 17.13 has no positional pattern, so it reads Is as the start of a relational clause.
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.ERR_ExpectedRelational)
        End Sub
    End Class
End Namespace
