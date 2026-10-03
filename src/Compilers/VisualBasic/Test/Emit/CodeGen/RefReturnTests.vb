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

        <Fact>
        Public Sub ByRefProperty_ReturnsAndAssignsArrayElement()
            Dim compilation = CreateCompilation("
Module Program
    Dim items() As Integer = {1, 2, 3}

    Sub Main()
        System.Console.Write(First)
        First = 9
        System.Console.Write(items(0))
        System.Console.Write(First)
    End Sub

    ByRef ReadOnly Property First As Integer
        Get
            Return items(0)
        End Get
    End Property
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="199").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefAutoProperty_AssignsBackingField()
            Dim compilation = CreateCompilation("
Module Program
    ByRef ReadOnly Property Value As Integer

    Sub Main()
        Value = 4
        System.Console.Write(Value)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefProperty_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    ByRef ReadOnly Property First As Integer
        Get
            Return 0
        End Get
    End Property
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_Syntax, "ByRef").WithLocation(3, 5),
                Diagnostic(ERRID.ERR_ExpectedDeclaration, "Get").WithLocation(4, 9),
                Diagnostic(ERRID.ERR_ExecutableAsDeclaration, "Return 0").WithLocation(5, 13),
                Diagnostic(ERRID.ERR_InvalidEndGet, "End Get").WithLocation(6, 9),
                Diagnostic(ERRID.ERR_InvalidEndProperty, "End Property").WithLocation(7, 5))
        End Sub
    End Class
End Namespace
