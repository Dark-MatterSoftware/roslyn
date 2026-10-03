' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class CollectionExpressionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub CollectionExpression_TargetsArray()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer() = (1, 2, 3)
        System.Console.Write(values(0))
        System.Console.Write(values(1))
        System.Console.Write(values(2))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="123").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_EmptyArray()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer() = ()
        System.Console.Write(values.Length)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_NestedArray()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer()() = ((1, 2), (3))
        System.Console.Write(values(0)(1))
        System.Console.Write(values(1)(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="23").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_ParenthesesAroundIdentifier()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim name = ""ok""
        Dim values As String() = (name)
        System.Console.Write(values(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_TupleWithoutTargetStaysTuple()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values = (1, 2)
        System.Console.Write(values.Item1)
        System.Console.Write(values.Item2)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="12").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_EscapedIdentifierStaysIdentifier()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim [Stop] = 4
        System.Console.Write([Stop])
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_EmptyRequiresTargetType()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values = ()
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_CollectionExpressionRequiresTarget, "()").WithLocation(4, 22))
        End Sub

        <Fact>
        Public Sub CollectionExpression_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer() = (1, 2)
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TypeMismatch2, "(1, 2)").WithArguments("(Integer, Integer)", "Integer()").WithLocation(4, 35))
        End Sub
    End Class
End Namespace
