' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class TargetTypedDefaultTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub TargetTypedDefault_InLocalAssignmentReturnAndArgument()
            Dim compilation = CreateCompilation("
Class Program
    Shared Function Make() As Integer
        Return Default
    End Function

    Shared Sub Take(value As Integer)
        System.Console.Write(value)
    End Sub

    Shared Sub Main()
        Dim number As Integer = Default
        System.Console.Write(number)
        number = Default
        System.Console.Write(number)
        System.Console.Write(Make())
        Take(Default)
        Dim text As String = Default
        System.Console.Write(If(text Is Nothing, ""n"", ""x""))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0000n").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TargetTypedDefault_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim number As Integer = Default
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(4, 33))
        End Sub

    End Class

End Namespace
