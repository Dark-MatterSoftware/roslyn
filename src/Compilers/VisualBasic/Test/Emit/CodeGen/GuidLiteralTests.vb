' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class GuidLiteralTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub GuidLiteral_BuildsTheValue()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim id = &G{936DA01F-9ABD-4d9d-80C7-02AF85C822A8}
        Dim compact = &g{936da01f9abd4d9d80c702af85c822a8}
        If id = compact Then
            System.Console.Write(""yes"")
        End If
        If id = New System.Guid(""936DA01F-9ABD-4d9d-80C7-02AF85C822A8"") Then
            System.Console.Write(""match"")
        End If
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="yesmatch").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub GuidLiteral_NameAfterAmpersandStaysConcatenation()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim Goo = ""z""
        Dim name = ""x"" &Goo
        System.Console.Write(name)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="xz").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub GuidLiteral_RejectedWhenMalformed()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim id = &G{nope}
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_InvalidGuidLiteral, "&G{nope}").WithLocation(4, 18))
        End Sub

        <Fact>
        Public Sub GuidLiteral_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim id = &G{936DA01F-9ABD-4d9d-80C7-02AF85C822A8}
    End Sub
End Class
", parseOptions:=Vb17_13)

            Assert.True(compilation.GetDiagnostics().Any(Function(d) d.Severity = DiagnosticSeverity.Error))
        End Sub
    End Class
End Namespace
