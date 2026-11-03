' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    ''' <summary>
    ''' Data flow analysis of a region that is a Vb.Next form, or a part of one, as extract method asks for.
    ''' </summary>
    Public Class VbNextRegionAnalysisTests
        Inherits BasicTestBase

        Private Shared ReadOnly s_vbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Shared Function Analyze(source As String, region As String, Optional occurrence As Integer = 0) As DataFlowAnalysis
            Dim compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=s_vbNext)
            compilation.VerifyDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim expression = tree.GetRoot().DescendantNodes().OfType(Of ExpressionSyntax)().
                Where(Function(e) e.ToString() = region).
                Skip(occurrence).First()
            Dim analysis = model.AnalyzeDataFlow(expression)
            Assert.True(analysis.Succeeded, $"The analysis of {region} did not succeed.")
            Return analysis
        End Function

        Private Shared Function Names(symbols As IEnumerable(Of ISymbol)) As String
            Return String.Join(", ", symbols.Select(Function(s) s.Name).OrderBy(Function(n) n))
        End Function

        Private Const Indexes As String = "
Imports System
Imports System.Collections.Generic

Module M
    Function F(list As List(Of String), items As String(), s As String, depth As Integer) As String
        Dim letters As ReadOnlySpan(Of Char) = s.AsSpan()
        Dim tail As Index = ^depth
        Dim middle As Range = 1..^depth
        Dim buffer As Memory(Of String) = items
        list(^2) &= ""!""
        Return list(^1) & items(^depth) & s(1..depth) & s(..depth) & letters(tail) & letters(middle).ToString() & buffer(middle).Length & items(middle).Length
    End Function
End Module
"

        <Theory>
        <InlineData("^1", 0, "")>
        <InlineData("^2", 0, "")>
        <InlineData("^depth", 2, "depth")>
        <InlineData("1..depth", 0, "depth")>
        <InlineData("..depth", 0, "depth")>
        <InlineData("list", 0, "list")>
        <InlineData("list", 1, "list")>
        <InlineData("letters", 0, "letters")>
        <InlineData("letters", 1, "letters")>
        <InlineData("tail", 0, "tail")>
        <InlineData("middle", 0, "middle")>
        <InlineData("middle", 1, "middle")>
        <InlineData("middle", 2, "middle")>
        <InlineData("buffer", 0, "buffer")>
        Public Sub IndexesAndRanges(region As String, occurrence As Integer, readInside As String)
            Dim analysis = Analyze(Indexes, region, occurrence)
            Assert.Equal(readInside, Names(analysis.ReadInside))
        End Sub

        Private Const Collections As String = "
Imports System.Collections.Generic

Module M
    Function Total(values As List(Of Integer)) As Integer
        Return values.Count
    End Function

    Function F(head As Integer()) As Integer
        Dim names As List(Of String) = {""x"", ""y""}
        Dim unique As HashSet(Of Integer) = {1, 1, 2}
        Dim all As List(Of Integer) = {..head, 3}
        Dim joined As Integer() = {..head, 4}
        Return names.Count + unique.Count + all.Count + joined.Length + Total({4, 5}) + Total((..head, 6))
    End Function
End Module
"

        <Theory>
        <InlineData("{""x"", ""y""}", "")>
        <InlineData("{1, 1, 2}", "")>
        <InlineData("{..head, 3}", "head")>
        <InlineData("..head", "head")>
        <InlineData("{..head, 4}", "head")>
        <InlineData("{4, 5}", "")>
        <InlineData("(..head, 6)", "head")>
        <InlineData("..head", "head", 2)>
        Public Sub CollectionLiterals(region As String, readInside As String, Optional occurrence As Integer = 0)
            Dim analysis = Analyze(Collections, region, occurrence)
            Assert.Equal(readInside, Names(analysis.ReadInside))
        End Sub

        Private Const Guard As String = "
Module M
    Function Label(value As Object) As String
        If TypeOf value IsNot String Into text Then Return ""not text""

        Return $""text of length {text.Length}""
    End Function
End Module
"

        <Fact>
        Public Sub TheNameOfAGuardIfIsReadAfterIt()
            Dim compilation = CreateCompilation(Guard, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=s_vbNext)
            compilation.VerifyDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim guardIf = tree.GetRoot().DescendantNodes().OfType(Of SingleLineIfStatementSyntax)().Single()

            Dim analysis = model.AnalyzeDataFlow(guardIf, guardIf)
            Assert.True(analysis.Succeeded)
            Assert.Equal("text", Names(analysis.VariablesDeclared))
            Assert.Equal("text", Names(analysis.ReadOutside))
            Assert.Equal("text", Names(analysis.DataFlowsOut))
            Assert.Equal("text", Names(analysis.WrittenInside))

            Dim use = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().Last(Function(n) n.Identifier.ValueText = "text")
            Assert.Equal(analysis.VariablesDeclared.Single(), model.GetSymbolInfo(use).Symbol)
        End Sub
    End Class
End Namespace
