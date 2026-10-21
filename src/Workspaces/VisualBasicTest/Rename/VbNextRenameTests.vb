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
