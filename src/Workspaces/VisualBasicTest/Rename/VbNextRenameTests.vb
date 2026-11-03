' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.Rename
Imports Microsoft.CodeAnalysis.Rename.ConflictEngine
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.Workspaces.UnitTests.Rename
    <UseExportProvider>
    <Trait(Traits.Feature, Traits.Features.Rename)>
    Public Class VbNextRenameTests
        Private Const Code As String = "
Module M
    Sub F(items As (String, Integer)())
        Dim total = 0
        For Each (name, count) In items
            total += count
        Next
    End Sub
End Module
"

        Private Shared Async Function RenameAsync(identifier As String, newName As String) As Task(Of ConflictResolution)
            Using workspace = New AdhocWorkspace()
                Dim project = workspace.CurrentSolution.AddProject("Project", "Project.dll", LanguageNames.VisualBasic).
                    WithParseOptions(VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)).
                    WithCompilationOptions(New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionInfer:=True)).
                    AddMetadataReference(MetadataReference.CreateFromFile(GetType(Object).Assembly.Location))
                Dim document = project.AddDocument("Document.vb", SourceText.From(Code))
                Dim root = Await document.GetSyntaxRootAsync()
                Dim model = Await document.GetSemanticModelAsync()
                Dim declaration = root.DescendantNodes().OfType(Of ModifiedIdentifierSyntax)().Single(Function(n) n.Identifier.ValueText = identifier)
                Dim symbol = model.GetDeclaredSymbol(declaration)
                Assert.NotNull(symbol)
                Return Await Renamer.RenameSymbolAsync(document.Project.Solution, symbol, newName, New SymbolRenameOptions(), CancellationToken.None)
            End Using
        End Function

        Private Shared Async Function RenameInAsync(source As String, findSymbol As Func(Of SemanticModel, SyntaxNode, ISymbol), newName As String, Optional expectConflicts As Boolean = False) As Task(Of String)
            Using workspace = New AdhocWorkspace()
                Dim project = workspace.CurrentSolution.AddProject("Project", "Project.dll", LanguageNames.VisualBasic).
                    WithParseOptions(VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)).
                    WithCompilationOptions(New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionInfer:=True)).
                    AddMetadataReference(MetadataReference.CreateFromFile(GetType(Object).Assembly.Location))
                Dim document = project.AddDocument("Document.vb", SourceText.From(source))
                Dim root = Await document.GetSyntaxRootAsync()
                Dim model = Await document.GetSemanticModelAsync()
                Dim symbol = findSymbol(model, root)
                Assert.NotNull(symbol)
                Dim resolution = Await Renamer.RenameSymbolAsync(document.Project.Solution, symbol, newName, New SymbolRenameOptions(), CancellationToken.None)
                Assert.True(resolution.IsSuccessful)
                Dim newText = (Await resolution.NewSolution.Projects.Single().Documents.Single().GetTextAsync()).ToString()
                Dim conflicts = resolution.RelatedLocations.
                    Where(Function(location) (location.Type And RelatedLocationType.UnresolvedConflict) <> 0).
                    Select(Function(location) newText.Substring(location.ConflictCheckSpan.Start, location.ConflictCheckSpan.Length))
                Assert.True(expectConflicts = conflicts.Any(), "Conflicts: " & String.Join(", ", conflicts) & vbCrLf & newText)
                Return newText
            End Using
        End Function

        Private Const AliasSource As String = "
Imports {0}

Module M
    Function Shift(s As Span2) As Span2
        Dim copy As Span2 = s
        Return copy
    End Function
End Module
"

        <Theory>
        <InlineData("Span2 = (Start As Integer, Length As Integer)")>
        <InlineData("Span2 = Integer()")>
        Public Async Function RenameAnAliasOfATupleOrArrayRenamesItsUses(importsClause As String) As Task
            Dim source = String.Format(AliasSource, importsClause)
            Dim text = Await RenameInAsync(
                source,
                Function(model, root) model.GetDeclaredSymbol(root.DescendantNodes().OfType(Of TypeAliasImportsClauseSyntax)().Single()),
                "Window")

            Assert.Equal(source.Replace("Span2", "Window"), text)
        End Function

        <Theory>
        <InlineData("Span2 = (Start As Integer, Length As Integer)")>
        <InlineData("Span2 = Integer()")>
        Public Async Function FindReferencesFindsTheUsesOfAnAliasOfATupleOrArray(importsClause As String) As Task
            Dim source = String.Format(AliasSource, importsClause)
            Using workspace = New AdhocWorkspace()
                Dim document = AddDocument(workspace, source)
                Dim root = Await document.GetSyntaxRootAsync()
                Dim model = Await document.GetSemanticModelAsync()
                Dim [alias] = model.GetDeclaredSymbol(root.DescendantNodes().OfType(Of TypeAliasImportsClauseSyntax)().Single())
                Dim references = Await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindReferencesAsync([alias], document.Project.Solution, CancellationToken.None)
                Dim spans = references.SelectMany(Function(r) r.Locations).Select(Function(l) source.Substring(l.Location.SourceSpan.Start, l.Location.SourceSpan.Length)).ToArray()

                Assert.Equal({"Span2", "Span2", "Span2"}, spans)
            End Using
        End Function

        Private Const RecordSource As String = "
Class Shape(Of T)
    Public Sub New(origin As T)
    End Sub
End Class

Record Class Point(X As Integer, Y As Integer)
    Inherits Shape(Of Integer)(X)

    Function Sum() As Integer
        Return X + Y
    End Function
End Class

Module M
    Function F() As Integer
        Dim p = New Point(X:=1, Y:=2)
        Return p.X
    End Function
End Module
"

        <Fact>
        Public Async Function RenameARecordPositionalPropertyRenamesItsParameter() As Task
            Dim text = Await RenameInAsync(
                RecordSource,
                Function(model, root) model.GetSymbolInfo(root.DescendantNodes().OfType(Of MemberAccessExpressionSyntax)().Single(Function(n) n.Name.Identifier.ValueText = "X")).Symbol,
                "Left")

            Assert.Equal(RecordSource.Replace("X", "Left"), text)
        End Function

        <Fact>
        Public Async Function RenameARecordParameterRenamesItsProperty() As Task
            Dim text = Await RenameInAsync(
                RecordSource,
                Function(model, root) model.GetDeclaredSymbol(root.DescendantNodes().OfType(Of ParameterSyntax)().Single(Function(n) n.Identifier.Identifier.ValueText = "X")),
                "Left")

            Assert.Equal(RecordSource.Replace("X", "Left"), text)
        End Function

        Private Shared Function AddDocument(workspace As AdhocWorkspace, source As String) As Document
            Dim project = workspace.CurrentSolution.AddProject("Project", "Project.dll", LanguageNames.VisualBasic).
                WithParseOptions(VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)).
                WithCompilationOptions(New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionInfer:=True)).
                AddMetadataReference(MetadataReference.CreateFromFile(GetType(Object).Assembly.Location))
            Return project.AddDocument("Document.vb", SourceText.From(source))
        End Function

        <Fact>
        Public Async Function RenameAPropertyThatAnAnonymousTypeCopySets() As Task
            Dim source = "
Module M
    Function F() As Integer
        Dim label = New With {.Name = ""a"", .Size = 1}
        Dim bigger = label With {.Size = 3}
        Return bigger.Size
    End Function
End Module
"
            Dim text = Await RenameInAsync(
                source,
                Function(model, root) model.GetSymbolInfo(root.DescendantNodes().OfType(Of IdentifierNameSyntax)().Last(Function(n) n.Identifier.ValueText = "Size")).Symbol,
                "Width")

            Assert.Equal(source.Replace("Size", "Width"), text)
        End Function

        Private Shared Function HasUnresolvedConflict(resolution As ConflictResolution) As Boolean
            Return resolution.RelatedLocations.Any(Function(location) (location.Type And RelatedLocationType.UnresolvedConflict) <> 0)
        End Function

        <Fact>
        Public Async Function RenameAForEachDeconstructionName() As Task
            Dim resolution = Await RenameAsync("count", "amount")

            Assert.True(resolution.IsSuccessful)
            Assert.False(HasUnresolvedConflict(resolution))
            Dim text = Await resolution.NewSolution.Projects.Single().Documents.Single().GetTextAsync()
            Assert.Equal(Code.Replace("count", "amount"), text.ToString())
        End Function

        <Fact>
        Public Async Function RenameAForEachDeconstructionNameToAnOuterLocalConflicts() As Task
            Dim resolution = Await RenameAsync("count", "total")

            Assert.True(resolution.IsSuccessful)
            Assert.True(HasUnresolvedConflict(resolution))
        End Function

        <Fact>
        Public Async Function RenameAnOuterLocalToAForEachDeconstructionNameConflicts() As Task
            Dim resolution = Await RenameAsync("total", "name")

            Assert.True(resolution.IsSuccessful)
            Assert.True(HasUnresolvedConflict(resolution))
        End Function
    End Class
End Namespace
