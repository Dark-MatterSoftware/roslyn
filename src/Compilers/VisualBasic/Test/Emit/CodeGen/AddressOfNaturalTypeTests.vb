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
        Public Sub OptionInferOffStoresTheDelegateInObject()
            Dim compilation = CreateCompilation("
Option Infer Off
Option Strict Off

Module Program
    Sub Hello()
        System.Console.Write(""hello"")
    End Sub

    Sub Main()
        Dim h = AddressOf Hello
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim declarator = tree.GetRoot().DescendantNodes().OfType(Of VariableDeclaratorSyntax)().Single().Names(0)
            Assert.Equal("Object", DirectCast(model.GetDeclaredSymbol(declarator), ILocalSymbol).Type.ToDisplayString())
        End Sub

        <Fact>
        Public Sub ConvertsToObjectDelegateAndMulticastDelegate()
            Dim compilation = CreateCompilation("
Module Program
    Sub Hello()
        System.Console.Write(""hello "")
    End Sub

    Function Twice(x As Integer) As Integer
        Return x * 2
    End Function

    Sub Run(d As System.Delegate)
        System.Console.Write(d.DynamicInvoke(21) & "" "")
    End Sub

    Sub RunMulti(d As System.MulticastDelegate)
        System.Console.Write(d.Method.Name & "" "")
    End Sub

    Sub Main()
        Dim o As Object = AddressOf Hello
        DirectCast(o, System.Delegate).DynamicInvoke()
        Run(AddressOf Twice)
        RunMulti(AddressOf Hello)
        Dim c = CType(AddressOf Twice, Object)
        Dim t = TryCast(AddressOf Twice, System.Delegate)
        System.Console.Write(c.GetType().Name.StartsWith(""VB$AnonymousDelegate"") & "" "" & (t IsNot Nothing))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hello 42 Hello True True").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub PrefersADelegateParameterOverObject()
            Dim compilation = CreateCompilation("
Module Program
    Sub Hello()
    End Sub

    Sub Pick(a As System.Action)
        System.Console.Write(""action"")
    End Sub

    Sub Pick(o As Object)
        System.Console.Write(""object"")
    End Sub

    Sub Main()
        Pick(AddressOf Hello)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="action").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub OverloadedGroupStillHasNoConversionToObject()
            Dim compilation = CreateCompilation("
Module Program
    Sub Log(value As Integer)
    End Sub

    Sub Log(value As String)
    End Sub

    Sub Run(d As System.Delegate)
    End Sub

    Sub Main()
        Dim o As Object = AddressOf Log
        Run(AddressOf Log)
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Log").WithArguments("Object").WithLocation(13, 27),
                Diagnostic(ERRID.ERR_AddressOfNotCreatableDelegate1, "AddressOf Log").WithArguments("System.Delegate").WithLocation(14, 13))
        End Sub

        <Fact>
        Public Sub InfersTheTypeOfIfAndArrayLiterals()
            Dim compilation = CreateCompilation("
Module Program
    Sub Hello()
        System.Console.Write(""hello "")
    End Sub

    Sub Bye()
        System.Console.Write(""bye "")
    End Sub

    Function Twice(x As Integer) As Integer
        Return x * 2
    End Function

    Function Square(x As Integer) As Integer
        Return x * x
    End Function

    Sub Main()
        Dim pick = If(System.Environment.TickCount <> 0, AddressOf Hello, AddressOf Bye)
        pick()
        Dim handlers = {AddressOf Hello, AddressOf Bye}
        For Each h In handlers
            h()
        Next
        Dim maths = {AddressOf Twice, AddressOf Square, Function(x As Integer) x + 1}
        System.Console.Write(maths(0)(3) & "" "" & maths(1)(3) & "" "" & maths(2)(3) & "" "")
        Dim mixed = If(False, AddressOf Twice, Function(x As Integer) x - 1)
        System.Console.Write(mixed(10))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hello hello bye 6 9 4 9").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub DifferentSignaturesHaveNoCommonType()
            Dim compilation = CreateCompilation("
Option Strict On

Module Program
    Sub Hello()
    End Sub

    Function Twice(x As Integer) As Integer
        Return x * 2
    End Function

    Sub Main()
        Dim pick = If(True, AddressOf Hello, AddressOf Twice)
        Dim handlers = {AddressOf Hello, AddressOf Twice}
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_IfNoTypeObjectDisallowed, "If(True, AddressOf Hello, AddressOf Twice)").WithLocation(13, 20),
                Diagnostic(ERRID.ERR_ArrayInitNoTypeObjectDisallowed, "{AddressOf Hello, AddressOf Twice}").WithLocation(14, 24))
        End Sub

        <Fact>
        Public Sub IfAndArrayLiteralsRejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Sub Hello()
    End Sub

    Sub Bye()
    End Sub

    Sub Main()
        Dim pick = If(True, AddressOf Hello, AddressOf Bye)
        Dim handlers = {AddressOf Hello, AddressOf Bye}
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_IfNoType, "If(True, AddressOf Hello, AddressOf Bye)").WithLocation(10, 20),
                Diagnostic(ERRID.ERR_ArrayInitNoType, "{AddressOf Hello, AddressOf Bye}").WithLocation(11, 24),
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Hello").WithArguments("Object").WithLocation(11, 25),
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Bye").WithArguments("Object").WithLocation(11, 42))
        End Sub

        <Fact>
        Public Sub RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Sub Hello()
    End Sub

    Sub Main()
        Dim h = AddressOf Hello
        Dim o As Object = AddressOf Hello
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Hello").WithArguments("Object").WithLocation(7, 17),
                Diagnostic(ERRID.ERR_AddressOfNotDelegate1, "AddressOf Hello").WithArguments("Object").WithLocation(8, 27))
        End Sub
    End Class

End Namespace
