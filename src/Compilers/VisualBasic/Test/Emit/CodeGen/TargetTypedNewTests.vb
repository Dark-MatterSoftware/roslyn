' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class TargetTypedNewTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub TargetTypedNew_InLocalDeclaration()
            Dim compilation = CreateCompilation("
Class Point
    Public ReadOnly X As Integer
    Public ReadOnly Y As Integer
    Public Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub
End Class
Class Program
    Shared Sub Main()
        Dim p As Point = New(1, 2)
        System.Console.Write(p.X)
        System.Console.Write(p.Y)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            Dim creation = compilation.SyntaxTrees.Single().GetRoot().DescendantNodes().OfType(Of ObjectCreationExpressionSyntax)().Single()
            Assert.True(creation.Type.IsMissing)
            Assert.Equal(2, creation.ArgumentList.Arguments.Count)

            CompileAndVerify(compilation, expectedOutput:="12").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TargetTypedNew_ParameterlessConstructor()
            Dim compilation = CreateCompilation("
Structure Point
    Public Shared Function Make() As Point
        Dim p As Point = New()
        Return p
    End Function
End Structure
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TargetTypedNew_RequiresTargetType()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim p = New(1, 2)
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TargetTypedNewRequiresTargetType, "New").WithLocation(4, 17))
        End Sub

        <Fact>
        Public Sub TargetTypedNew_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Point
    Public Sub New(x As Integer, y As Integer)
    End Sub
End Class
Class Program
    Shared Sub Main()
        Dim p As Point = New(1, 2)
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TypeMismatch2, "New(1, 2)").WithArguments("(?, ?)", "Point").WithLocation(8, 26),
                Diagnostic(ERRID.ERR_NewWithTupleTypeSyntax, "(1, 2)").WithLocation(8, 29),
                Diagnostic(ERRID.ERR_UnrecognizedType, "").WithLocation(8, 30),
                Diagnostic(ERRID.ERR_UnrecognizedType, "").WithLocation(8, 33))
        End Sub

    End Class

End Namespace
