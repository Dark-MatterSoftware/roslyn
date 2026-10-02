' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NothingCoalescingAssignmentTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub NothingCoalescingAssignment_AssignsOnlyWhenNothing()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim text As String = Nothing
        text ?= ""a""
        Dim kept As String = ""b""
        kept ?= ""c""
        Dim number As Integer? = Nothing
        number ?= 4
        Dim present As Integer? = 5
        present ?= 6
        System.Console.Write(text)
        System.Console.Write(kept)
        System.Console.Write(number)
        System.Console.Write(present)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ab45").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NothingCoalescingAssignment_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim text As String = Nothing
        text ?= ""a""
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_LanguageVersion, "?=").WithArguments("17.13", "nothing-coalescing assignment", "vbnext").WithLocation(5, 14))
        End Sub
    End Class
End Namespace
