' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RawStringTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub RawString_SingleLineIsAString()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Const Greeting As String = r""""""hello""""""
        System.Console.Write(Greeting)
        System.Console.Write(r""""""say ""hi"" now"""""")
        System.Console.Write(r""""""{name}"""""")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hellosay ""hi"" now{name}").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RawString_MultiLineStripsClosingIndent()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim text = r""""""
    hello
      world
    """"""
        For Each ch In text
            If ch = Microsoft.VisualBasic.Strings.ChrW(10) Then
                System.Console.Write("".""c)
            ElseIf ch <> Microsoft.VisualBasic.Strings.ChrW(13) Then
                System.Console.Write(ch)
            End If
        Next
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hello.  world").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RawString_OrdinaryQuotesStillIncludeAQuote()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        System.Console.Write(""""""hello"""""")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="""hello""").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RawString_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim s = r""""""hello""""""
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotDeclared1, "r").WithArguments("r").WithLocation(4, 17),
                Diagnostic(ERRID.ERR_ExpectedEOS, """""""hello""""""").WithLocation(4, 18))
        End Sub

        <Fact>
        Public Sub RawString_InterpolationHole()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim name = ""Ann""
        System.Console.Write($r""""""Hello {name}"""""")
        System.Console.Write($r""""""Say ""hi"" {name}"""""")
        System.Console.Write($r""""""{{ok}}{name}"""""")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Hello AnnSay ""hi"" Ann{ok}Ann").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RawString_InterpolationHoleStripsClosingIndent()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim name = ""Ann""
        System.Console.Write($r""""""
    Hello {name}
    """""")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Hello Ann").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RawString_InterpolationRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim name = ""Ann""
        System.Console.Write($r""""""Hello {name}"""""")
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(5, 30),
                Diagnostic(ERRID.ERR_IllegalChar, "$").WithLocation(5, 30))
        End Sub
    End Class
End Namespace
