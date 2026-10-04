' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class UncheckedTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub Unchecked_WrapsAtRunTime()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim x = Integer.MaxValue
        System.Console.Write(Unchecked(x + 1))
        System.Console.Write("" "")
        System.Console.Write(Unchecked(x * 2))
        System.Console.Write("" "")
        Try
            System.Console.Write(x + 1)
        Catch ex As System.OverflowException
            System.Console.Write(""overflow"")
        End Try
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="-2147483648 -2 overflow").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Unchecked_WrapsAConstant()
            Dim compilation = CreateCompilation("
Class Program
    Const Wrapped As Integer = Unchecked(Integer.MaxValue + 1)

    Shared Sub Main()
        System.Console.Write(Wrapped)
        System.Console.Write("" "")
        System.Console.Write(Unchecked(-Integer.MinValue))
        System.Console.Write("" "")
        System.Console.Write(Unchecked(CInt(4294967301L)))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="-2147483648 -2147483648 5").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Unchecked_ConvertsWithoutAnOverflowCheck()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim value As Long = 4294967301L
        System.Console.Write(Unchecked(CInt(value)))
        System.Console.Write("" "")
        Try
            System.Console.Write(CInt(value))
        Catch ex As System.OverflowException
            System.Console.Write(""overflow"")
        End Try
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5 overflow").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Unchecked_AnOverflowOutsideTheOperatorIsStillAnError()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim y = Unchecked(Integer.MaxValue + 1) - 1
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpressionOverflow1, "Unchecked(Integer.MaxValue + 1) - 1").WithArguments("Integer").WithLocation(4, 17))
        End Sub

        <Fact>
        Public Sub Unchecked_ADeclaredSymbolKeepsItsMeaning()
            Dim compilation = CreateCompilation("
Class Program
    Shared Function Unchecked(value As Integer) As Integer
        Return value * 2
    End Function

    Shared Sub Main()
        System.Console.Write(Unchecked(3))
        Dim x = Integer.MaxValue
        Try
            System.Console.Write([Unchecked](x))
        Catch ex As System.OverflowException
            System.Console.Write("" overflow"")
        End Try
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="6 overflow").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Unchecked_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim x = Unchecked(1)
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotDeclared1, "Unchecked").WithArguments("Unchecked").WithLocation(4, 17))
        End Sub

    End Class

End Namespace
