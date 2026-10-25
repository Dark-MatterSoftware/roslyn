' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class ParamsCollectionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const CSharpLibrary As String = "
using System;
using System.Collections.Generic;

public static class Calls
{
    public static int Sum(params ReadOnlySpan<int> values)
    {
        var total = 0;
        foreach (var v in values) total += v;
        return total;
    }

    public static int Count(params IEnumerable<string> values)
    {
        var n = 0;
        foreach (var v in values) n++;
        return n;
    }

    public static T First<T>(params ReadOnlySpan<T> values) => values[0];

    public static string Pick(params int[] values) => ""array"";
    public static string Pick(params ReadOnlySpan<int> values) => ""span"";
}
"

        Private Function CSharpLibraryReference() As MetadataReference
            Return CreateCSharpCompilation(CSharpLibrary, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CallsParamsCollectionsWithSeparateArguments()
            Dim compilation = CreateCompilation("
Imports System
Imports System.IO

Module Program
    Sub Main()
        Dim values As Integer() = {4, 5}
        Console.Write(Calls.Sum(1, 2, 3) & "" "" & Calls.Sum() & "" "" & Calls.Sum(New ReadOnlySpan(Of Integer)(values)))
        Console.Write("" "" & Calls.Count(""a"", ""b"") & "" "" & Calls.First(""x"", ""y"") & "" "" & Calls.First(7, 8))
        Console.Write("" "" & Calls.Pick(1, 2) & "" "" & Calls.Pick(values))
        Console.Write("" "" & String.Concat(""a"", ""b"", ""c"", ""d"", ""e"") & "" "" & String.Join(""-"", ""p"", ""q"", ""r""))
        Console.Write("" "" & Path.Combine(""a"", ""b"", ""c"", ""d"", ""e"").Length)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibraryReference()}, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="6 0 9 2 x 7 array array abcde p-q-r 9", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub FixedArgumentOverloadsBeatParamsSpans()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim n = 3
        Console.Write($""number {n + 1}"" & "" "" & $""{n}{n}{n}{n}{n}"")
        Console.Write("" "" & String.Format(""a{0}"", n) & "" "" & String.Format(""{0}{1}{2}{3}"", 1, 2, 3, 4))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="number 4 33333 a3 1234", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CSharpCallsVisualBasicParamArrayCollection()
            Dim library = CreateCompilation("
Public Module Collect
    Public Function Total(ParamArray values As System.ReadOnlySpan(Of Integer)) As Integer
        Dim sum = 0
        For Each v In values
            sum += v
        Next
        Return sum
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public static class Use
{
    public static int Call() => Collect.Total(1, 2, 3);
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub VisualBasic17_13PassesTheCollectionOnly()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Function Use(names As String()) As Integer
        Return Calls.Count(names) + Calls.Count(values:=names) + Calls.Count(""a"", ""b"")
    End Function
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibraryReference()}, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30057: Too many arguments to 'Public Shared Overloads Function Count(ParamArray values As IEnumerable(Of String)) As Integer'.
        Return Calls.Count(names) + Calls.Count(values:=names) + Calls.Count("a", "b")
                                                                                  ~~~
</expected>)
        End Sub

    End Class

End Namespace
