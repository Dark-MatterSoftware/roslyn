' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ArrayRangeTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ArrayRange_OnArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim items = {10, 20, 30, 40, 50}
        Write(items(1..4))
        Write(items(..2))
        Write(items(3..))
        Write(items(^2..))
        Write(items(..^1))
        Write(items(^4..^1))
        Write(items(2..2))
    End Sub

    Shared Sub Write(values As Integer())
        For Each value In values
            System.Console.Write(value)
        Next
        System.Console.Write("".""c)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="203040.1020.4050.4050.10203040.203040..").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ArrayRange_EvaluatesReceiverOnce()
            Dim compilation = CreateCompilation("
Class Program
    Shared Function Items() As Integer()
        System.Console.Write(""X"")
        Return {10, 20, 30, 40}
    End Function

    Shared Sub Main()
        For Each value In Items()(1..3)
            System.Console.Write(value)
        Next
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="X2030").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ArrayRange_RequiresArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub F(value As Integer)
    End Sub

    Shared Sub Main()
        F(1..2)
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_RangeRequiresArray, "1..2").WithLocation(7, 11))
        End Sub

        <Fact>
        Public Sub ArrayRange_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim items = {10, 20, 30}
        System.Console.Write(items(1..2))
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedIdentifier, "").WithLocation(5, 38))
        End Sub
    End Class
End Namespace
