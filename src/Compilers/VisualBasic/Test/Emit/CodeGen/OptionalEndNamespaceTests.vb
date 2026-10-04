' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class OptionalEndNamespaceTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub Namespace_MayRunToEndOfFile()
            Dim compilation = CreateCompilation("
Namespace Sample
Module Program
    Sub Main()
        System.Console.Write(GetType(Program).FullName)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Sample.Program").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Namespace_SecondNamespaceStillNests()
            Dim compilation = CreateCompilation("
Namespace One
Namespace Two
Module Program
    Sub Main()
        System.Console.Write(GetType(Program).FullName)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="One.Two.Program").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Namespace_ExplicitEndStillCompiles()
            Dim compilation = CreateCompilation("
Namespace Sample
Module Program
    Sub Main()
        System.Console.Write(""yes"")
    End Sub
End Module
End Namespace
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="yes").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Namespace_EndRequiredOn17_13()
            Dim compilation = CreateCompilation("
Namespace Sample
Module Program
    Sub Main()
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedEndNamespace, "Namespace Sample").WithLocation(2, 1))
        End Sub
    End Class
End Namespace
