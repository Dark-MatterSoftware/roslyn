' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ArrayRangeTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ArrayRange_OnArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim items = {10, 20, 30, 40, 50}
        Write(items(1..4))
        Write(items(..2))
        Write(items(3..))
        Write(items(^2..))
        Write(items(..^1))
        Write(items(^4..^1))
        Write(items(2..2))
    End Sub

    Shared Sub Write(values As Integer())
        For Each value In values
            System.Console.Write(value)
        Next
        System.Console.Write("".""c)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="203040.1020.4050.4050.10203040.203040..").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ArrayRange_EvaluatesReceiverOnce()
            Dim compilation = CreateCompilation("
Class Program
    Shared Function Items() As Integer()
        System.Console.Write(""X"")
        Return {10, 20, 30, 40}
    End Function

    Shared Sub Main()
        For Each value In Items()(1..3)
            System.Console.Write(value)
        Next
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="X2030").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ArrayRange_RequiresArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub F(value As Integer)
    End Sub

    Shared Sub Main()
        F(1..2)
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_RangeRequiresArray, "1..2").WithLocation(7, 11))
        End Sub

        <Fact>
        Public Sub ArrayRange_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim items = {10, 20, 30}
        System.Console.Write(items(1..2))
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedIdentifier, "").WithLocation(5, 38))
        End Sub
        <Fact>
        Public Sub XmlDescendantAxis_ParsesOnVbNextAnd17_13()
            Dim source = "
Module Program
    Sub Main()
        Dim x = <r><a><b>1</b></a><b>2</b></r>
        Dim d = x...<b>
        System.Console.WriteLine(x...<b>.Count())
        Dim p = (x...<b>).Count()
        Dim q = From e In x...<b> Where e.Value <> """" Select e.Value
        Dim r = x.<a>...<b>
        With x
            Dim w = ...<b>
        End With
    End Sub
End Module
"
            For Each options In {Vb17_13, VbNext}
                Dim tree = VisualBasicSyntaxTree.ParseText(source, options)
                tree.GetDiagnostics().Verify()
                Assert.Equal(6, tree.GetRoot().DescendantNodes().Count(Function(n) n.Kind() = SyntaxKind.XmlDescendantAccessExpression))
                Assert.Empty(tree.GetRoot().DescendantTokens().Where(Function(t) t.Kind() = SyntaxKind.DotDotToken))
            Next
        End Sub

        <Fact>
        Public Sub RangeAndIndexFromEnd_StillParseOnVbNext()
            Dim tree = VisualBasicSyntaxTree.ParseText("
Module Program
    Sub Main()
        Dim items = {10, 20, 30, 40}
        Dim a = items(1..3)
        Dim b = items(..2)
        Dim c = items(1..)
        Dim d = items(..^1)
        Dim e = items(^1)
        Dim f = (3..)
    End Sub
End Module
", VbNext)
            tree.GetDiagnostics().Verify()
            Assert.Equal(5, tree.GetRoot().DescendantNodes().OfType(Of RangeExpressionSyntax)().Count())
            Assert.Equal(5, tree.GetRoot().DescendantTokens().Count(Function(t) t.Kind() = SyntaxKind.DotDotToken))
            Assert.Equal(2, tree.GetRoot().DescendantNodes().Count(Function(n) n.Kind() = SyntaxKind.FromEndIndexExpression))
        End Sub

        <ConditionalFact(GetType(DesktopOnly))>
        Public Sub XmlDescendantAxis_RunsOnVbNextAnd17_13()
            Dim source = <compilation>
                             <file name="a.vb"><![CDATA[
Imports System.Linq
Imports <xmlns:ns="urn:n">
Module Program
    Sub Main()
        Dim x = <r><a><b>1</b></a><b>2</b><ns:b>3</ns:b></r>
        System.Console.Write(x...<b>.Count())
        System.Console.Write((x...<b>).Count())
        System.Console.Write(String.Join("", From e In x...<b> Select e.Value))
        System.Console.Write(x.<a>...<b>.Count())
        System.Console.Write(x...<ns:b>.Count())
        With x
            System.Console.Write(...<b>.Count())
        End With
    End Sub
End Module
]]></file>
                         </compilation>
            For Each options In {Vb17_13, VbNext}
                Dim compilation = CreateCompilationWithMscorlib40AndVBRuntime(source, additionalRefs:=XmlReferences, options:=TestOptions.ReleaseExe, parseOptions:=options)
                CompileAndVerify(compilation, expectedOutput:="2212112").VerifyDiagnostics()
            Next
        End Sub
    End Class
End Namespace
