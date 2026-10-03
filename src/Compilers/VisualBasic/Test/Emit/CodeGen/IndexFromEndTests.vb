' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class IndexFromEndTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub IndexFromEnd_OnArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim items = {10, 20, 30}
        System.Console.Write(items(^1))
        System.Console.Write(items(^3))
        items(^2) = 5
        System.Console.Write(items(^2))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="30105").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub IndexFromEnd_EvaluatesReceiverOnce()
            Dim compilation = CreateCompilation("
Class Program
    Shared Function Items() As Integer()
        System.Console.Write(""X"")
        Return {1, 2, 3}
    End Function

    Shared Sub Main()
        System.Console.Write(Items()(^1))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="X3").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Exponentiation_StillUsesCaret()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        System.Console.Write(2 ^ 3)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="8").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub IndexFromEnd_RequiresArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub F(value As Integer)
    End Sub

    Shared Sub Main()
        F(^1)
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_IndexFromEndRequiresArray, "^1").WithLocation(7, 11))
        End Sub

        <Fact>
        Public Sub IndexFromEnd_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim items = {10}
        System.Console.Write(items(^1))
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(5, 36))
        End Sub
    End Class
End Namespace
