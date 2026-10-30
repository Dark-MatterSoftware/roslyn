' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Linq
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class IndexRangeValueTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub IndexAndRangeAreValues()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Function Describe(i As Index) As String
        Return i.Value & If(i.IsFromEnd, ""e"", ""s"")
    End Function

    Function Show(r As Range) As String
        Return Describe(r.Start) & ""-"" & Describe(r.End)
    End Function

    Sub Main()
        Dim a As Index = ^1
        Dim b = ^2
        Dim r As Range = 1..3
        Dim n = 2
        Console.Write(Describe(a) & "" "" & Describe(b) & "" "" & Show(r) & "" "" & Show(..n) & "" "" & Show(^2..) & "" "")
        Dim o As Object = 1..^1
        Console.Write(o.ToString() & "" "" & (^3).Value & "" "" & Describe(5) & "" "" & b.GetType().Name & "" "" & r.GetType().Name)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1e 2e 1s-3s 0s-2s 2e-0e 1..^1 3 5s Index Range", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub IndexesAndSlicesStillConsumeThem()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic

Module Program
    Sub Main()
        Dim arr = {10, 20, 30, 40}
        Dim list As New List(Of Integer) From {1, 2, 3, 4}
        Dim s = ""hello""
        Dim span As ReadOnlySpan(Of Integer) = arr
        arr(^1) = 41
        Console.Write(arr(^1) & "" "" & arr(1..^1).Length & "" "" & list(^2) & "" "" & list(1..3).Count & "" "" & s(1..^1) & "" "" & span(^2) & "" "" & span(..2).Length)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="41 2 3 2 ell 30 2", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub IndexesAndSlicesTakeIndexAndRangeValues()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic

Module Program
    Private Calls As Integer

    Function Make() As List(Of Integer)
        Calls += 1
        Return New List(Of Integer) From {10, 20, 30, 40}
    End Function

    Function Last() As Index
        Calls += 100
        Return ^1
    End Function

    Sub Main()
        Dim arr = {1, 2, 3, 4, 5}
        Dim list As New List(Of Integer) From {1, 2, 3, 4}
        Dim s = ""hello""
        Dim span As ReadOnlySpan(Of Integer) = arr
        Dim i As Index = ^2
        Dim r As Range = 1..^1
        Dim fromStart As Index = 1
        arr(i) = 40
        list(i) = 30
        Console.Write(arr(i) & "" "" & list(i) & "" "" & s(i) & "" "" & span(i) & "" "")
        Console.Write(String.Join("","", arr(r)) & "" "" & String.Join("","", list(r)) & "" "" & s(r) & "" "" & span(r).Length & "" "")
        Console.Write(String.Join("","", arr(fromStart..i)) & "" "" & s(i..) & "" "" & span(..i).Length & "" "")
        Console.Write(Make()(Last()) & "" "" & Make()(r).Count & "" "" & Calls)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="40 30 l 40 2,3,40 2,30 ell 3 2,3 lo 3 40 2 102", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub DeclaredIndexDefaultPropertyWinsOverThePattern()
            Dim compilation = CreateCompilation("
Imports System

Class Ring
    Public ReadOnly Property Count As Integer = 3

    Default Public ReadOnly Property Item(position As Integer) As String
        Get
            Return ""int"" & position
        End Get
    End Property

    Default Public ReadOnly Property Item(position As Index) As String
        Get
            Return ""index"" & position.ToString()
        End Get
    End Property
End Class

Module Program
    Sub Main()
        Dim ring As New Ring()
        Dim i As Index = ^2
        Console.Write(ring(1) & "" "" & ring(^1) & "" "" & ring(i))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="int1 index^1 index^2", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SemanticModelGivesTheTypes()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a = ^1
        Dim r = 1..3
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim fromEnd = tree.GetRoot().DescendantNodes().OfType(Of UnaryExpressionSyntax)().Single()
            Dim range = tree.GetRoot().DescendantNodes().OfType(Of RangeExpressionSyntax)().Single()
            Assert.Equal("System.Index", model.GetTypeInfo(fromEnd).Type.ToTestDisplayString())
            Assert.Equal("System.Range", model.GetTypeInfo(range).Type.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub SemanticModelFindsIndexAndRangeVariablesInAccesses()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Sub Main(list As List(Of Integer), arr As Integer(), s As String)
        Dim i = ^1
        Dim r = 1..3
        Dim a = list(i)
        Dim b = arr(i)
        Dim c = s(r)
        Dim d = arr(r)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim arguments = tree.GetRoot().DescendantNodes().OfType(Of InvocationExpressionSyntax)().Select(Function(invocation) invocation.ArgumentList.Arguments.Single().GetExpression()).ToArray()
            Assert.Equal({"i As System.Index", "i As System.Index", "r As System.Range", "r As System.Range"},
                         arguments.Select(Function(argument) model.GetSymbolInfo(argument).Symbol.ToTestDisplayString()).ToArray())
            Assert.Equal({"Integer", "Integer", "String", "Integer()"},
                         tree.GetRoot().DescendantNodes().OfType(Of InvocationExpressionSyntax)().Select(Function(invocation) model.GetTypeInfo(invocation).Type.ToDisplayString()).ToArray())
        End Sub

        <Fact>
        Public Sub ValuesThatDoNotConvertStillReportTheErrors()
            Dim compilation = CreateCompilation("
Module Program
    Sub TakesInteger(value As Integer)
    End Sub

    Sub Main()
        Dim x As Integer = ^1
        Dim y As String = 1..2
        TakesInteger(^1)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_IndexFromEndRequiresArray, "^1").WithLocation(7, 28),
                Diagnostic(ERRID.ERR_RangeRequiresArray, "1..2").WithLocation(8, 27),
                Diagnostic(ERRID.ERR_TypeMismatch2, "^1").WithArguments("System.Index", "Integer").WithLocation(9, 22))
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ExpressionTreesAndOverloads()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Linq.Expressions

Module Program
    Function Pick(value As Integer) As String
        Return ""Integer""
    End Function

    Function Pick(value As Index) As String
        Return ""Index""
    End Function

    Function Pick(value As Range) As String
        Return ""Range""
    End Function

    Sub Main()
        Dim e As Expression(Of Func(Of Index)) = Function() ^1
        Dim f As Expression(Of Func(Of Range)) = Function() 1..^2
        Console.Write(e.Compile()().ToString() & "" "" & f.Compile()().ToString() & "" "" & Pick(1) & "" "" & Pick(^1) & "" "" & Pick(..2))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="^1 1..^2 Integer Index Range", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim a As Index = ^1
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(6, 26))
        End Sub
    End Class

End Namespace
