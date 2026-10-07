' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class XmlStatementBlockTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub XmlBlock_ForEachYieldsElements()
            Dim compilation = CreateCompilationWithMscorlib40("
Imports System.Xml.Linq
Module Program
    Sub Main()
        Dim genres = {""Rock"", ""Jazz""}
        Dim list = <ul>
                       <%= For Each genre In genres
                               Yield <li><%= genre %></li>
                           Next %>
                   </ul>
        System.Console.Write(list.ToString(SaveOptions.DisableFormatting))
    End Sub
End Module
", references:=XmlReferences.Append(MsvbRef), options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="<ul><li>Rock</li><li>Jazz</li></ul>").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub XmlBlock_SelectCaseReturns()
            Dim compilation = CreateCompilationWithMscorlib40("
Imports System.Xml.Linq
Module Program
    Function Render(count As Integer) As XElement
        Return <ul>
                   <%= Select Case count
                           Case 1
                               Return <li>One Item</li>
                           Case Else
                               Return <li>Many Items</li>
                       End Select %>
               </ul>
    End Function
    Sub Main()
        System.Console.Write(Render(1).ToString(SaveOptions.DisableFormatting))
        System.Console.Write(Render(5).ToString(SaveOptions.DisableFormatting))
    End Sub
End Module
", references:=XmlReferences.Append(MsvbRef), options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="<ul><li>One Item</li></ul><ul><li>Many Items</li></ul>").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub XmlBlock_DimAndIfBlock()
            Dim compilation = CreateCompilationWithMscorlib40("
Imports System.Xml.Linq
Module Program
    Function Render(name As String) As XElement
        Return <p><%= Dim trimmed = name.Trim()
                      If trimmed.Length = 0 Then
                          Return ""(none)""
                      End If
                      Return trimmed %></p>
    End Function
    Sub Main()
        System.Console.Write(Render(""  Ada "").ToString() & Render("" "").ToString())
    End Sub
End Module
", references:=XmlReferences.Append(MsvbRef), options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="<p>Ada</p><p>(none)</p>").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub XmlBlock_ExpressionsKeepTheirMeaning()
            Dim compilation = CreateCompilationWithMscorlib40("
Module Program
    Sub Main()
        Dim value = ""x""
        Dim e = <p a=<%= value = ""x"" %>><%= If(value Is Nothing, ""none"", value) %></p>
        System.Console.Write(e.ToString())
    End Sub
End Module
", references:=XmlReferences.Append(MsvbRef), options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="<p a=""true"">x</p>").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub XmlBlock_YieldInNestedLambdaStaysThere()
            Dim compilation = CreateCompilationWithMscorlib40("
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main()
        Dim e = <p><%= Dim items = Iterator Function() As IEnumerable(Of Integer)
                                       Yield 1
                                       Yield 2
                                   End Function
                       Return items().Sum() %></p>
        System.Console.Write(e.ToString())
    End Sub
End Module
", references:=XmlReferences.Append(MsvbRef), options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="<p>3</p>").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub XmlBlock_DebugBuildEmits()
            Dim compilation = CreateCompilationWithMscorlib40("
Imports System.Xml.Linq
Module Program
    Sub Main()
        Dim genres = {""Rock"", ""Jazz""}
        Dim list = <ul><%= For Each genre In genres
                               Yield <li><%= genre %></li>
                           Next %><%= Select Case genres.Length
                                          Case 2
                                              Return <li>Two</li>
                                      End Select
                                      Return Nothing %></ul>
        System.Console.Write(list.ToString(SaveOptions.DisableFormatting))
    End Sub
End Module
", references:=XmlReferences.Append(MsvbRef), options:=TestOptions.DebugExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="<ul><li>Rock</li><li>Jazz</li><li>Two</li></ul>").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub XmlBlock_SyntaxShape()
            Dim source = "
Module Program
    Dim x = <ul><%= For Each g In {1, 2}
                        Yield <li/>
                    Next %></ul>
    Dim y = <p><%= Return 1 %></p>
End Module
"
            Dim tree = VisualBasicSyntaxTree.ParseText(source, VbNext)
            tree.GetDiagnostics().Verify()
            Assert.Equal(source, tree.GetRoot().ToFullString())

            Dim lambdas = tree.GetRoot().DescendantNodes().OfType(Of MultiLineLambdaExpressionSyntax)().ToArray()
            Assert.Equal(2, lambdas.Length)

            Assert.Equal(SyntaxKind.IteratorKeyword, lambdas(0).SubOrFunctionHeader.Modifiers.Single().Kind())
            Assert.True(lambdas(0).SubOrFunctionHeader.DeclarationKeyword.IsMissing)
            Assert.True(lambdas(0).EndSubOrFunctionStatement.EndKeyword.IsMissing)
            Assert.IsType(Of ForEachBlockSyntax)(lambdas(0).Statements.Single())

            Assert.Empty(lambdas(1).SubOrFunctionHeader.Modifiers)
            Assert.IsType(Of ReturnStatementSyntax)(lambdas(1).Statements.Single())
            Assert.IsType(Of InvocationExpressionSyntax)(lambdas(1).Parent.Parent)
        End Sub

        <Fact>
        Public Sub XmlBlock_RejectedOn17_13()
            Dim tree = VisualBasicSyntaxTree.ParseText("
Module Program
    Dim y = <p><%= Return 1 %></p>
End Module
", Vb17_13)
            Assert.NotEmpty(tree.GetDiagnostics())
            Assert.Empty(tree.GetRoot().DescendantNodes().OfType(Of MultiLineLambdaExpressionSyntax)())
        End Sub

    End Class

End Namespace
