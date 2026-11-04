' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.Text

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.CodeRefactorings.VbNext
    ''' <summary>
    ''' Extract method, introduce variable, and introduce parameter on a name in the receiver or the operand of an index
    ''' from the end or a slice offer nothing that they do not offer on the name without the index, and their edits add
    ''' the errors that they add there. Extract method and introduce variable add none. Introduce variable is not offered
    ''' on the operand of ^n or a..b, which, like the operand of another unary operator, it does not take as replaceable.
    ''' </summary>
    <UseExportProvider>
    Public NotInheritable Class VbNextIndexNameRefactoringTests

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

    ReadOnly Property Depth As Integer
        Get
            Return 1
        End Get
    End Property

    Sub Run()
        Dim e = items(^Depth)
        Dim f = items(Depth)
        Dim g = items(1..Depth)
    End Sub
End Class
"

        <Theory>
        <InlineData("Dim a = Make()(^3)", "Dim b = Make()(2)", "Make")>
        <InlineData("Make()(^2) += 5", "Make()(1) += 5", "Make")>
        <InlineData("Dim c = Items(^1)", "Dim d = Items(0)", "Items")>
        <InlineData("Dim e = items(^Depth)", "Dim f = items(Depth)", "items")>
        <InlineData("Dim e = items(^Depth)", "Dim f = items(Depth)", "Depth")>
        <InlineData("Dim g = items(1..Depth)", "Dim f = items(Depth)", "items")>
        <InlineData("Dim g = items(1..Depth)", "Dim f = items(Depth)", "Depth")>
        Public Async Function NameOffersWhatItOffersWithoutTheIndex(statement As String, plainStatement As String, name As String) As Task
            Using workspace = TestWorkspace.CreateVisualBasic(Array.Empty(Of String)(), composition:=FeaturesTestCompositions.Features)
                Dim solution = workspace.CurrentSolution
                Dim projectId = solution.ProjectIds.Single()
                solution = solution.WithProjectParseOptions(projectId, VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)).
                    WithProjectCompilationOptions(projectId, New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionStrict:=OptionStrict.On, optionInfer:=True, globalImports:=GlobalImport.Parse({"System"}))).
                    WithProjectMetadataReferences(projectId, TargetFrameworkUtil.GetReferences(TargetFramework.Net90))
                Dim sourceId = DocumentId.CreateNewId(projectId)
                solution = solution.AddDocument(sourceId, "Source.vb", SourceText.From(Source))
                Dim document = solution.GetDocument(sourceId)
                Assert.Empty((Await document.GetSemanticModelAsync()).GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error))

                Dim providers = workspace.ExportProvider.GetExports(Of CodeRefactoringProvider, CodeChangeProviderMetadata)().
                    Select(Function(p) p.Value).
                    Where(Function(p) {"ExtractMethodCodeRefactoringProvider", "IntroduceVariableCodeRefactoringProvider", "VisualBasicIntroduceParameterCodeRefactoringProvider"}.Contains(p.GetType().Name)).
                    ToArray()
                Assert.Equal(3, providers.Length)

                For Each provider In providers
                    Dim indexed = Await ApplyAsync(provider, document, NameSpan(statement, name))
                    Dim plain = Await ApplyAsync(provider, document, NameSpan(plainStatement, name))
                    Dim providerName = provider.GetType().Name

                    Assert.True(indexed.Keys.All(Function(t) plain.ContainsKey(t)),
                                $"{providerName} offers [{String.Join("; ", indexed.Keys.Where(Function(t) Not plain.ContainsKey(t)))}] on {statement} but not on {plainStatement}.")
                    Assert.True(indexed.Count > 0 OrElse plain.Count = 0 OrElse IsOperand(statement, name),
                                $"{providerName} offers nothing on {name} in {statement}, and [{String.Join("; ", plain.Keys)}] on {plainStatement}.")
                    For Each title In indexed.Keys
                        Assert.True(plain(title) = indexed(title), $"{providerName} '{title}' adds [{indexed(title)}] on {statement} and [{plain(title)}] on {plainStatement}.")
                        If providerName <> "VisualBasicIntroduceParameterCodeRefactoringProvider" Then
                            Assert.True(indexed(title) = "", $"{providerName} '{title}' adds [{indexed(title)}] on {statement}.")
                        End If
                    Next
                Next
            End Using
        End Function

        Private Shared Function IsOperand(statement As String, name As String) As Boolean
            Return statement.Contains("^" & name) OrElse statement.Contains(".." & name)
        End Function

        Private Shared Function NameSpan(statement As String, name As String) As TextSpan
            Dim statementStart = Source.IndexOf(statement, StringComparison.Ordinal)
            Assert.True(statementStart >= 0, statement)
            Dim nameStart = Source.IndexOf(name, statementStart, StringComparison.Ordinal)
            Return New TextSpan(nameStart, name.Length)
        End Function

        ''' <summary>
        ''' For each action offered at the span, the ids of the errors that its edit adds, in order.
        ''' </summary>
        Private Shared Async Function ApplyAsync(provider As CodeRefactoringProvider, document As Document, span As TextSpan) As Task(Of Dictionary(Of String, String))
            Dim found As New List(Of CodeAction)
            Await provider.ComputeRefactoringsAsync(New CodeRefactoringContext(document, span, Sub(a) found.Add(a), CancellationToken.None))

            Dim result As New Dictionary(Of String, String)
            For Each action In Flatten(found)
                If TypeOf action Is CodeActionWithOptions Then
                    Continue For
                End If

                Dim added As New List(Of String)
                For Each operation In (Await action.GetOperationsAsync(CancellationToken.None)).OfType(Of ApplyChangesOperation)()
                    Dim changed = operation.ChangedSolution.GetDocument(document.Id)
                    Dim model = Await changed.GetSemanticModelAsync()
                    added.AddRange(model.GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error).Select(Function(d) d.Id).OrderBy(Function(id) id))
                Next

                result(action.Title) = String.Join(",", added)
            Next

            Return result
        End Function

        Private Shared Iterator Function Flatten(actions As IEnumerable(Of CodeAction)) As IEnumerable(Of CodeAction)
            For Each action In actions
                If action.NestedActions.IsDefaultOrEmpty Then
                    Yield action
                Else
                    For Each nested In Flatten(action.NestedActions)
                        Yield nested
                    Next
                End If
            Next
        End Function
    End Class
End Namespace
