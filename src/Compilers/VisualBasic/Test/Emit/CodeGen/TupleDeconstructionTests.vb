' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class TupleDeconstructionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub TupleDeconstruction_DeclaresLocals()
            Dim compilation = CreateCompilation("
Class Program
    Shared n As Integer
    Shared Function NextPoint() As (Integer, Integer)
        n += 1
        Return (n, n + 10)
    End Function
    Shared Sub Main()
        Dim point = (X:=10, Y:=20)
        Dim (x, y) = point
        Dim (a, b, c) = (1, 2, 3)
        Dim (p, q) = NextPoint()
        System.Console.Write(x)
        System.Console.Write(y)
        System.Console.Write(a)
        System.Console.Write(b)
        System.Console.Write(c)
        System.Console.Write(p)
        System.Console.Write(q)
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="10201231111").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleDeconstruction_AssignsExistingLocals()
            Dim compilation = CreateCompilation("
Class Program
    Shared n As Integer
    Shared Function NextPoint() As (Integer, Integer)
        n += 1
        Return (n, n + 10)
    End Function
    Shared Sub Main()
        Dim x = 0
        Dim y = 0
        Dim a = 0
        Dim b = 0
        Dim c = 0
        (x, y) = NextPoint()
        (a, b, c) = (1, 2, 3)
        System.Console.Write(x)
        System.Console.Write(y)
        System.Console.Write(a)
        System.Console.Write(b)
        System.Console.Write(c)
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1111231").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_RequiresATuple()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim x = 0
        Dim y = 0
        (x, y) = 1
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DeconstructionRequiresTuple, "= 1").WithArguments("2").WithLocation(6, 16))
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim x = 0
        Dim y = 0
        (x, y) = (10, 20)
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_Syntax, "(").WithLocation(6, 9))
        End Sub

        <Fact>
        Public Sub TupleDeconstruction_RequiresATuple()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim (x, y) = 1
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DeconstructionRequiresTuple, "= 1").WithArguments("2").WithLocation(4, 20))
        End Sub

        <Fact>
        Public Sub TupleDeconstruction_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim point = (10, 20)
        Dim (x, y) = point
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedIdentifier, "").WithLocation(5, 13),
                Diagnostic(ERRID.ERR_InferringNonArrayType1, "").WithArguments("(Integer, Integer)").WithLocation(5, 13),
                Diagnostic(ERRID.ERR_InitWithExplicitArraySizes, "(x, y)").WithLocation(5, 13),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "x").WithArguments("x").WithLocation(5, 14),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "y").WithArguments("y").WithLocation(5, 17),
                Diagnostic(ERRID.ERR_TypeMismatch2, "point").WithArguments("(Integer, Integer)", "Object(*,*)").WithLocation(5, 22))
        End Sub
    End Class
End Namespace
