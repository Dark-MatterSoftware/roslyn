' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class Utf8StringTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub Utf8String_EmitsBytes()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim bytes = ""hi""u8
        System.Console.Write(bytes.Length)
        System.Console.Write(bytes(0))
        System.Console.Write(bytes(1))
        Dim quoted = ""a""""b""U8
        System.Console.Write(quoted.Length)
        System.Console.Write(quoted(1))
        Dim raw = r""""""hi""""""u8
        System.Console.Write(raw.Length)
        Dim empty = """"u8
        System.Console.Write(empty.Length)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="210410533420").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Utf8String_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim bytes = ""hi""u8
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedEOS, "u8").WithLocation(4, 25))
        End Sub
    End Class
End Namespace
