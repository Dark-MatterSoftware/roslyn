' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ConstantInterpolatedStringTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ConstantInterpolatedString_FoldsStringHoles()
            Dim compilation = CreateCompilation("
Class Program
    Const Greeting As String = $""hello {""world""}""
    Const Joined As String = $""hello "" & $""{""there""}""
    Const Part As String = ""x""
    Const Nested As String = $""a{Part}{$""{""b""}""}""

    Shared Sub Main()
        Dim name = ""vb""
        System.Console.Write(Greeting)
        System.Console.Write(Joined)
        System.Console.Write(Nested)
        System.Console.Write($"" {name}"")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hello worldhello thereaxb vb").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ConstantInterpolatedString_NumberHoleIsNotConstant()
            Dim compilation = CreateCompilation("
Class Program
    Const Greeting As String = $""hello {1}""
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_RequiredConstExpr, "$""hello {1}""").WithLocation(3, 32))
        End Sub

        <Fact>
        Public Sub ConstantInterpolatedString_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Const Greeting As String = $""hello {""world""}""
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_RequiredConstExpr, "$""hello {""world""}""").WithLocation(3, 32))
        End Sub
    End Class
End Namespace
