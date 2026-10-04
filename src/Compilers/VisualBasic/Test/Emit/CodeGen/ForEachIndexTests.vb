' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ForEachIndexTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ForEach_IndexNumbersEachItem()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        For Each item With index In {""a"", ""b""}
            System.Console.Write(index)
            System.Console.Write(item)
        Next
        Dim items As New System.Collections.Generic.List(Of String) From {""c"", ""d""}
        For Each item As String With position In items
            System.Console.Write(position)
            System.Console.Write(item)
        Next
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0a1b0c1d").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEach_ContinueForStillAdvancesTheIndex()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        For Each item With index In {""a"", ""b"", ""c""}
            If item = ""b"" Then Continue For
            System.Console.Write(index)
        Next
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="02").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEach_IndexCannotReuseALocal()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim index = 1
        For Each item With index In {""a""}
        Next
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DuplicateLocals1, "index").WithArguments("index").WithLocation(5, 28))
        End Sub

        <Fact>
        Public Sub ForEach_IndexRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        For Each item With index In {""a""}
        Next
    End Sub
End Class
", parseOptions:=Vb17_13)

            Assert.True(compilation.GetDiagnostics().Any(Function(d) d.Severity = DiagnosticSeverity.Error))
        End Sub
    End Class
End Namespace
