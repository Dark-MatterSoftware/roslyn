' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class LambdaOptionalParameterTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub OptionalAndParamArrayLambdaParameters()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim f = Function(Optional x As Integer = 3) x * 2
        Dim g = Function(ParamArray xs() As Integer) xs.Length
        Dim h = Function(prefix As String, Optional suffix As String = ""!"") prefix & suffix
        Dim other = Function(Optional y As Integer = 4) y
        Console.Write(f() & "" "" & f(5) & "" "" & g() & "" "" & g(1, 2, 3) & "" "" & h(""hi"") & "" "" & h(""hi"", ""?"") & "" "" & other())
        Dim typed As Func(Of Integer, Integer) = Function(Optional x As Integer = 9) x + 1
        Console.Write("" "" & typed(1))
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="6 10 0 3 hi! hi? 4 2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub OptionalAndParamArrayLambdaParameterErrors()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a = Function(Optional x = 3) x
        Dim b = Function(Optional x As Integer) x
        Dim c = Function(Optional x As Integer = System.Environment.TickCount) x
        Dim d = Function(ParamArray x As Integer) x
        Dim e = Function(ParamArray xs() As Integer, y As Integer) y
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC33010: 'Lambda' parameters cannot be declared 'Optional'.
        Dim a = Function(Optional x = 3) x
                         ~~~~~~~~
BC30201: Expression expected.
        Dim b = Function(Optional x As Integer) x
                                              ~
BC30812: Optional parameters must specify a default value.
        Dim b = Function(Optional x As Integer) x
                                              ~
BC30059: Constant expression is required.
        Dim c = Function(Optional x As Integer = System.Environment.TickCount) x
                                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC30050: ParamArray parameter must be an array.
        Dim d = Function(ParamArray x As Integer) x
                                    ~
BC30192: End of parameter list expected. Cannot define parameters after a paramarray parameter.
        Dim e = Function(ParamArray xs() As Integer, y As Integer) y
                                                     ~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub OptionalAndParamArrayLambdaParametersNeedVbNext()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim f = Function(Optional x As Integer = 3) x
        Dim g = Function(ParamArray xs() As Integer) xs.Length
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC33010: 'Lambda' parameters cannot be declared 'Optional'.
        Dim f = Function(Optional x As Integer = 3) x
                         ~~~~~~~~
BC33009: 'Lambda' parameters cannot be declared 'ParamArray'.
        Dim g = Function(ParamArray xs() As Integer) xs.Length
                         ~~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
