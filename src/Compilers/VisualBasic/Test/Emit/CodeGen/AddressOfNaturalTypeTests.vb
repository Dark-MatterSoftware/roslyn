' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Linq
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class AddressOfNaturalTypeTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub InfersAnAnonymousDelegateFromTheOnlyMethod()
            Dim compilation = CreateCompilation("
Class Counter
    Public Total As Integer
    Public Sub Add(amount As Integer)
        Total += amount
    End Sub
End Class

Module Program
    Sub Hello()
        System.Console.Write(""hello "")
    End Sub

    Function Twice(x As Integer) As Integer
        Return x * 2
    End Function

    Sub Bump(ByRef x As Integer, Optional amount As Integer = 5)
        x += amount
    End Sub

    Function Echo(Of T)(value As T) As T
        Return value
    End Function

    Sub Main()
        Dim h = AddressOf Hello
        h()
        Dim t = AddressOf Twice
        System.Console.Write(t(21) & "" "")
        Dim b = AddressOf Bump
        Dim n = 1
        b(n)
        b(n, 10)
        System.Console.Write(n & "" "")
        Dim c As New Counter()
        Dim add = AddressOf c.Add
        add(3)
        add(4)
        System.Console.Write(c.Total & "" "")
        Dim e = AddressOf Echo(Of String)
        System.Console.Write(e(""x""))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hello 42 16 7 x").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub InfersTheTypeALambdaWithTheSameSignatureInfers()
            Dim compilation = CreateCompilation("
Module Program
    Function Twice(x As Integer) As Integer
        Return x * 2
    End Function

    Sub Main()
        Dim t = AddressOf Twice
        Dim l = Function(x As Integer) x * 2
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim declarators = tree.GetRoot().DescendantNodes().OfType(Of VariableDeclaratorSyntax)().Select(Function(d) d.Names(0)).ToArray()
            Dim fromAddressOf = DirectCast(model.GetDeclaredSymbol(declarators(0)), ILocalSymbol).Type
            Dim fromLambda = DirectCast(model.GetDeclaredSymbol(declarators(1)), ILocalSymbol).Type
            Assert.Equal("Function <generated method>(x As Integer) As Integer", fromAddressOf.ToDisplayString())
            Assert.True(fromAddressOf.IsAnonymousType)
            Assert.Equal(fromLambda, fromAddressOf)
        End Sub

        <Fact>
        Public Sub InfersFromTheOnlyExtensionMethod()
            Dim compilation = CreateCompilation("
Imports System.Runtime.CompilerServices

Module Extensions
    <Extension>
    Function Shout(value As String, mark As String) As String
        Return value.ToUpperInvariant() & mark
    End Function
End Module

Module Program
    Sub Main()
        Dim s = AddressOf ""hi"".Shout
        System.Console.Write(s(""!""))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="HI!").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub OverloadsAndOpenGenericMethodsHaveNoType()
            Dim compilation = CreateCompilation("
Module Program
    Sub Log(value As Integer)
    End Sub

    Sub Log(value As String)
    End Sub

    Function Echo(Of T)(value As T) As T
        Return value
    End Function

    Sub Main()
        Dim l = AddressOf Log
        Dim e = AddressOf Echo
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Log").WithArguments("Object").WithLocation(14, 17),
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Echo").WithArguments("Object").WithLocation(15, 17))
        End Sub

        <Fact>
        Public Sub OptionInferOffKeepsObject()
            Dim compilation = CreateCompilation("
Option Infer Off

Module Program
    Sub Hello()
    End Sub

    Sub Main()
        Dim h = AddressOf Hello
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Hello").WithArguments("Object").WithLocation(9, 17))
        End Sub

        <Fact>
        Public Sub RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Sub Hello()
    End Sub

    Sub Main()
        Dim h = AddressOf Hello
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Hello").WithArguments("Object").WithLocation(7, 17))
        End Sub
    End Class

End Namespace
