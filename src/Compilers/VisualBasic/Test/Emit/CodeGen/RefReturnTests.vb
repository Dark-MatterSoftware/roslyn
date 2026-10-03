' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RefReturnTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ByRefFunction_ReturnsAndAssignsArrayElement()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim items = {1, 2, 3}
        System.Console.Write(First(items))
        First(items) = 9
        System.Console.Write(items(0))
        System.Console.Write(First(items))
    End Sub

    ByRef Function First(items As Integer()) As Integer
        Return items(0)
    End Function
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="199").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefFunction_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    ByRef Function First(items As Integer()) As Integer
        Return items(0)
    End Function
End Module
", parseOptions:=Vb17_13)

        compilation.VerifyDiagnostics(
            Diagnostic(ERRID.ERR_Syntax, "ByRef").WithLocation(3, 5),
            Diagnostic(ERRID.ERR_ExecutableAsDeclaration, "Return items(0)").WithLocation(4, 9),
            Diagnostic(ERRID.ERR_InvalidEndFunction, "End Function").WithLocation(5, 5))
        End Sub
    End Class
End Namespace
