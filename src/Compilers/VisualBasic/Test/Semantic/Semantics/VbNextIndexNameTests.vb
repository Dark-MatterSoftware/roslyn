' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Semantics

    ''' <summary>
    ''' The names in the receiver and the operand of an index from the end or a slice have the symbol and type that
    ''' they have without the index.
    ''' </summary>
    Public Class VbNextIndexNameTests
        Inherits BasicTestBase

        Private Const Source As String = "
Imports System.Collections.Generic

Module M
    Function Make() As List(Of Integer)
        Return New List(Of Integer) From {1, 2, 3, 4}
    End Function

    ReadOnly Property Items As List(Of Integer)
        Get
            Return Make()
        End Get
    End Property

    Sub Run()
        Dim a = Make()(^3)
        Dim b = Make()(2)
        Make()(^2) += 5
        Make()(1) += 5
        Dim c = Items(^1)
        Dim d = Items(0)
    End Sub
End Module

Class C
    Private items As New List(Of Integer) From {1, 2, 3, 4}
    Private text As String = ""hello""

    ReadOnly Property Depth As Integer
        Get
            Return 1
        End Get
    End Property

    Sub Run()
        Dim e = items(^Depth)
        Dim f = items(Depth)
        Dim g = items(1..Depth)
        Dim h = text(1..^Depth)
        Dim k = text(Depth)
    End Sub
End Class
"

        <Theory>
        <InlineData("Dim a = Make()(^3)", "Make", "Dim b = Make()(2)")>
        <InlineData("Make()(^2) += 5", "Make", "Make()(1) += 5")>
        <InlineData("Dim c = Items(^1)", "Items", "Dim d = Items(0)")>
        <InlineData("Dim e = items(^Depth)", "items", "Dim f = items(Depth)")>
        <InlineData("Dim e = items(^Depth)", "Depth", "Dim f = items(Depth)")>
        <InlineData("Dim g = items(1..Depth)", "items", "Dim f = items(Depth)")>
        <InlineData("Dim g = items(1..Depth)", "Depth", "Dim f = items(Depth)")>
        <InlineData("Dim h = text(1..^Depth)", "text", "Dim k = text(Depth)")>
        <InlineData("Dim h = text(1..^Depth)", "Depth", "Dim k = text(Depth)")>
        Public Sub NamesHaveTheSymbolAndTypeThatTheyHaveWithoutTheIndex(statement As String, name As String, plainStatement As String)
            Dim compilation = CreateCompilation(Source, targetFramework:=TargetFramework.Net90, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))
            compilation.VerifyDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)

            Dim indexed = FindName(tree, statement, name)
            Dim plain = FindName(tree, plainStatement, name)
            Dim indexedInfo = model.GetSymbolInfo(indexed)
            Dim plainInfo = model.GetSymbolInfo(plain)

            Assert.NotNull(plainInfo.Symbol)
            Assert.Equal(plainInfo.Symbol, indexedInfo.Symbol)
            Assert.Empty(indexedInfo.CandidateSymbols)
            Assert.Equal(model.GetTypeInfo(plain).Type, model.GetTypeInfo(indexed).Type)
            Assert.Equal(model.GetAliasInfo(plain), model.GetAliasInfo(indexed))
        End Sub

        <Fact>
        Public Sub TheOperationsOfTheIndexesStayAsTheyAre()
            Dim compilation = CreateCompilation(Source, targetFramework:=TargetFramework.Net90, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)

            Dim describe = Function(statement As String) As String
                               Dim access = tree.GetRoot().DescendantNodes().OfType(Of StatementSyntax)().
                                   Single(Function(s) s.ToString() = statement).
                                   DescendantNodes().OfType(Of InvocationExpressionSyntax)().First()
                               Dim operation = model.GetOperation(access)
                               Dim indexer = TryCast(operation, IImplicitIndexerReferenceOperation)
                               If indexer Is Nothing Then
                                   Return operation.Kind.ToString()
                               End If

                               Return $"{operation.Kind}|{indexer.Instance.Kind}|{indexer.Instance.Syntax}|{indexer.Argument.Kind}"
                           End Function

            Assert.Equal({
                "ImplicitIndexerReference|Invocation|Make()|Unary",
                "ImplicitIndexerReference|PropertyReference|Items|Unary",
                "ImplicitIndexerReference|FieldReference|items|Unary",
                "ImplicitIndexerReference|FieldReference|items|Range",
                "PropertyReference"},
                {describe("Dim a = Make()(^3)"), describe("Dim c = Items(^1)"), describe("Dim e = items(^Depth)"), describe("Dim g = items(1..Depth)"), describe("Dim f = items(Depth)")})
        End Sub

        Private Shared Function FindName(tree As SyntaxTree, statement As String, name As String) As IdentifierNameSyntax
            Return tree.GetRoot().DescendantNodes().OfType(Of StatementSyntax)().
                Single(Function(s) s.ToString() = statement).
                DescendantNodes().OfType(Of IdentifierNameSyntax)().
                First(Function(n) n.Identifier.ValueText = name)
        End Function
    End Class
End Namespace
