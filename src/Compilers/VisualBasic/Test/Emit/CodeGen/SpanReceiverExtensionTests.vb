' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class SpanReceiverExtensionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const LibrarySource As String = "
using System;

public static class SpanExtensions
{
    public static int CountOf<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
    {
        var count = 0;
        foreach (var item in span)
        {
            if (item.Equals(value)) count++;
        }
        return count;
    }

    public static void FillWith<T>(this Span<T> span, T value) => span.Fill(value);

    public static int Vowels(this ReadOnlySpan<char> text)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (""aeiou"".IndexOf(c) >= 0) count++;
        }
        return count;
    }

    public static string Which(this ReadOnlySpan<int> span) => ""span"";
}

public static class ArrayExtensions
{
    public static string Which(this int[] array) => ""array"";
}
"

        Private Function CreateLibrary() As MetadataReference
            Dim library = CreateCSharpCompilation(LibrarySource,
                compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90))
            library.VerifyDiagnostics()
            Return library.EmitToImageReference()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ArraysCallSpanExtensions()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim a As Integer() = {1, 2, 2, 3}
        Dim s As Span(Of Integer) = a
        Console.Write($""{a.CountOf(2)} {s.CountOf(3)} {""education"".Vowels()} {a.Which()} "")
        a.FillWith(7)
        Console.Write($""{a(0)}{a(3)}"")
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CreateLibrary()}, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2 1 5 array 77", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExistingExtensionsKeepWinning()
            Dim compilation = CreateCompilation("
Imports System.Linq

Module Program
    Sub Main()
        Dim a As Integer() = {1, 2, 3}
        Dim found = a.Contains(2)
        Dim which = a.Which()
        Dim count = a.CountOf(2)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CreateLibrary()}, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees(0)
            Dim model = compilation.GetSemanticModel(tree)
            Dim calls = tree.GetRoot().DescendantNodes().OfType(Of InvocationExpressionSyntax)().ToArray()
            Assert.Equal("Enumerable", DirectCast(model.GetSymbolInfo(calls(0)).Symbol, IMethodSymbol).ContainingType.Name)
            Assert.Equal("ArrayExtensions", DirectCast(model.GetSymbolInfo(calls(1)).Symbol, IMethodSymbol).ContainingType.Name)
            Dim countOf = DirectCast(model.GetSymbolInfo(calls(2)).Symbol, IMethodSymbol)
            Assert.Equal("SpanExtensions", countOf.ContainingType.Name)
            Assert.Equal("System.ReadOnlySpan(Of System.Int32)", countOf.ReceiverType.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub SpanReceiverErrors()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a As Integer() = {1, 2, 3}
        Dim grid As Integer(,) = {{1}}
        Dim x = a.CountOf(""text"")
        Dim y = grid.CountOf(1)
        Dim z = New System.Collections.Generic.List(Of Integer)().CountOf(1)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CreateLibrary()}, options:=TestOptions.ReleaseDll.WithOptionInfer(True).WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30512: Option Strict On disallows implicit conversions from 'String' to 'Integer'.
        Dim x = a.CountOf("text")
                          ~~~~~~
BC30456: 'CountOf' is not a member of 'Integer(*,*)'.
        Dim y = grid.CountOf(1)
                ~~~~~~~~~~~~
BC30456: 'CountOf' is not a member of 'List(Of Integer)'.
        Dim z = New System.Collections.Generic.List(Of Integer)().CountOf(1)
                ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub SpanReceiversNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a As Integer() = {1, 2, 3}
        Dim count = a.CountOf(2)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CreateLibrary()}, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30456: 'CountOf' is not a member of 'Integer()'.
        Dim count = a.CountOf(2)
                    ~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
