' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Linq
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class NameOfAttributeParameterTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const NoteAttribute As String = "
<System.AttributeUsage(System.AttributeTargets.All)>
Class NoteAttribute
    Inherits System.Attribute
    Public Sub New(text As String)
    End Sub
End Class
"

        <Fact>
        Public Sub NameOfFindsParametersInMethodParameterAndReturnAttributes()
            Dim compilation = CreateCompilation(NoteAttribute & "
Class C
    <Note(NameOf(count))>
    Sub Counted(count As Integer)
    End Sub

    Sub Check(<Note(NameOf(value))> value As String)
    End Sub

    Function Echo(value As String) As <Note(NameOf(value))> String
        Return value
    End Function
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim c = compilation.GetTypeByMetadataName("C")
            Dim counted = c.GetMembers("Counted").OfType(Of IMethodSymbol)().Single()
            Dim check = c.GetMembers("Check").OfType(Of IMethodSymbol)().Single()
            Dim echo = c.GetMembers("Echo").OfType(Of IMethodSymbol)().Single()
            Assert.Equal("count", counted.GetAttributes().Single().ConstructorArguments.Single().Value)
            Assert.Equal("value", check.Parameters.Single().GetAttributes().Single().ConstructorArguments.Single().Value)
            Assert.Equal("value", echo.GetReturnTypeAttributes().Single().ConstructorArguments.Single().Value)

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim argument = tree.GetRoot().DescendantNodes().OfType(Of NameOfExpressionSyntax)().First().Argument
            Assert.Equal("count As Integer", model.GetSymbolInfo(argument).Symbol.ToDisplayString())
        End Sub

        <Fact>
        Public Sub ParametersStayOutOfScopeOutsideNameOf()
            Dim compilation = CreateCompilation(NoteAttribute & "
Class C
    <Note(text)>
    Sub Written(text As String)
    End Sub

    <Note(NameOf(value))>
    Private field As Integer
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotDeclared1, "text").WithArguments("text").WithLocation(10, 11),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "value").WithArguments("value").WithLocation(14, 18))
        End Sub

        <Fact>
        Public Sub RejectedOn17_13()
            Dim compilation = CreateCompilation(NoteAttribute & "
Class C
    Sub Check(<Note(NameOf(value))> value As String)
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotDeclared1, "value").WithArguments("value").WithLocation(10, 28))
        End Sub
    End Class

End Namespace
