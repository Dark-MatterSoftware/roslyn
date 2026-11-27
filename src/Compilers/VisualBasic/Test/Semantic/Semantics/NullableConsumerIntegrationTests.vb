' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableConsumerIntegrationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Enabled As VisualBasicCompilationOptions = TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable)
        Private Shared ReadOnly Disabled As VisualBasicCompilationOptions = TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable)

        <Fact>
        Public Sub EnabledDisplay_ShowsThePublicAnnotation()
            Dim compilation = Compile("
Class Box(Of T)
End Class
Module M
    Function Read(value As String?, values As String?(), list As Box(Of String)?, cells As (Item As String?, Count As Integer)) As String?
        Return value
    End Function
End Module
")
            Dim method = MethodOf(compilation, "Read")
            Dim text = method.ToDisplayString()
            Assert.Contains("value As String?", text, StringComparison.Ordinal)
            Assert.Contains("values As String?()", text, StringComparison.Ordinal)
            Assert.Contains("list As Box(Of String)?", text, StringComparison.Ordinal)
            Assert.Contains("Item As String?", text, StringComparison.Ordinal)
            Assert.Contains("As String?", text.Substring(text.LastIndexOf(" As ", StringComparison.Ordinal)), StringComparison.Ordinal)
        End Sub

        <Fact>
        Public Sub NonnullableAndValueNullable_DoNotGainAReferenceMarker()
            Dim compilation = Compile("
Module M
    Function Read(value As String, count As Integer?) As String
        Return value
    End Function
End Module
")
            Dim method = MethodOf(compilation, "Read")
            Assert.Equal("String", method.Parameters(0).Type.ToDisplayString())
            Assert.Equal(NullableAnnotation.NotAnnotated, method.Parameters(0).NullableAnnotation)
            Assert.Equal("Integer?", method.Parameters(1).Type.ToDisplayString())
            Assert.Equal(NullableAnnotation.None, method.Parameters(1).NullableAnnotation)
            Assert.DoesNotContain("Nullable(Of String)", method.ToDisplayString(), StringComparison.Ordinal)
        End Sub

        <Fact>
        Public Sub LegacyAndDisabled_DoNotShowNullableReferenceText()
            Dim legacy = CreateCompilation("
Module M
    Function Read(value As String) As String
        Return value
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            Assert.Equal("Public Function Read(value As String) As String", MethodOf(legacy, "Read").ToDisplayString())

            Dim nullableDisabled = CreateCompilation("
Module M
    Function Read(value As String) As String
        Return value
    End Function
End Module
", options:=Disabled, parseOptions:=VbNext)
            Assert.Equal("Public Function Read(value As String) As String", MethodOf(nullableDisabled, "Read").ToDisplayString())
        End Sub

        <Fact>
        Public Sub MinimalDisplay_UsesTheSemanticModelAndKeepsDeclarationDistinctFromFlow()
            Dim compilation = Compile("
Module M
    Function Read(value As String?) As String?
        If value IsNot Nothing Then
            Return value
        End If
        Return Nothing
    End Function
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim names = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().Where(Function(name) name.Identifier.ValueText = "value").ToArray()
            Dim parameter = DirectCast(model.GetSymbolInfo(names(0)).Symbol, IParameterSymbol)
            Dim minimal = parameter.Type.ToMinimalDisplayParts(model, names(0).SpanStart, SymbolDisplayFormat.MinimallyQualifiedFormat).ToDisplayString()
            Assert.Equal("String?", minimal)

            Dim guarded = model.GetTypeInfo(names(1))
            Assert.Equal(NullableAnnotation.Annotated, parameter.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, guarded.Type.NullableAnnotation)
            Assert.Equal(NullableFlowState.NotNull, guarded.Nullability.FlowState)
            Assert.Equal(NullableAnnotation.Annotated, guarded.Nullability.Annotation)
        End Sub

        <Fact>
        Public Sub ExplicitLambdaParameters_KeepDistinctAnnotationsInDisplay()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim optionalRead = Function(item As String?) item
        Dim requiredRead = Function(item As String) item
    End Sub
End Module
")
            Dim optionalParameter = ParameterOf(compilation, "item", 0)
            Dim requiredParameter = ParameterOf(compilation, "item", 1)
            Assert.Contains("item As String?", optionalParameter.ToDisplayString(), StringComparison.Ordinal)
            Assert.Equal(NullableAnnotation.Annotated, optionalParameter.NullableAnnotation)
            Assert.Contains("item As String", requiredParameter.ToDisplayString(), StringComparison.Ordinal)
            Assert.DoesNotContain("String?", requiredParameter.ToDisplayString(), StringComparison.Ordinal)
            Assert.Equal(NullableAnnotation.NotAnnotated, requiredParameter.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub Analyzer_ReadsPublicAnnotationsWithoutASecondFlowEngine()
            Dim compilation = Compile("
Class Box(Of T)
End Class
Module M
    Function Read(value As String?) As Box(Of String?)
        Return Nothing
    End Function
End Module
")
            Dim before = compilation.GetDiagnostics().Select(Function(diagnostic) diagnostic.Id).ToArray()
            Dim analyzer = New AnnotationProbeAnalyzer()
            Dim withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(Of DiagnosticAnalyzer)(analyzer))
            Dim analyzerDiagnostics = withAnalyzers.GetAnalyzerDiagnosticsAsync().Result
            Assert.Contains(analyzerDiagnostics, Function(diagnostic) diagnostic.GetMessage() = "parameter:Annotated")
            Assert.Contains(analyzerDiagnostics, Function(diagnostic) diagnostic.GetMessage() = "return:NotAnnotated")
            Assert.Contains(analyzerDiagnostics, Function(diagnostic) diagnostic.GetMessage() = "argument:Annotated")
            Assert.Contains(analyzerDiagnostics, Function(diagnostic) diagnostic.GetMessage() = "flow:MaybeNull")
            Assert.Equal(before, compilation.GetDiagnostics().Select(Function(diagnostic) diagnostic.Id).ToArray())
        End Sub

        <Fact>
        Public Sub Queries_DoNotChangeNullableDiagnostics()
            Dim compilation = Compile("
Module M
    Function Read(value As String?) As String?
        Return value
    End Function
End Module
")
            Dim first = Picture(compilation)
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim name = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().First(Function(identifier) identifier.Identifier.ValueText = "value")
            Dim unused = model.GetTypeInfo(name)
            Assert.Equal(first, Picture(compilation))
            Assert.Equal(first, Picture(compilation))
        End Sub

        <Fact>
        Public Sub BoundedConcurrentQueries_DoNotCrossResults()
            Dim compilation = Compile("
Module M
    Function Read(value As String?) As String?
        If value IsNot Nothing Then
            Return value
        End If
        Return Nothing
    End Function
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim names = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().Where(Function(name) name.Identifier.ValueText = "value").ToArray()
            Dim expectedAnnotation = model.GetTypeInfo(names(1)).Nullability.Annotation
            Dim expectedFlow = model.GetTypeInfo(names(1)).Nullability.FlowState
            Dim annotationMismatches = 0
            Dim flowMismatches = 0
            Dim exceptions = 0
            Dim workers = Enumerable.Range(0, 4).Select(Function(index) Threading.Tasks.Task.Run(
                Sub()
                    For iteration = 1 To 20
                        Try
                            Dim info = model.GetTypeInfo(names(1))
                            If info.Nullability.Annotation <> expectedAnnotation Then
                                Interlocked.Increment(annotationMismatches)
                            End If
                            If info.Nullability.FlowState <> expectedFlow Then
                                Interlocked.Increment(flowMismatches)
                            End If
                        Catch
                            Interlocked.Increment(exceptions)
                        End Try
                    Next
                End Sub)).ToArray()
            Threading.Tasks.Task.WaitAll(workers)
            Assert.Equal(0, annotationMismatches)
            Assert.Equal(0, flowMismatches)
            Assert.Equal(0, exceptions)
        End Sub

        <Fact>
        Public Sub AlreadyCancelledAnalyzer_DoesNotPublishAndLaterCallSucceeds()
            Dim compilation = Compile("
Module M
    Function Read(value As String?) As String
        Return value
    End Function
End Module
")
            Dim analyzer = New AnnotationProbeAnalyzer()
            Dim canceled = New CancellationToken(canceled:=True)
            Dim threw = False
            Try
                compilation.WithAnalyzers(ImmutableArray.Create(Of DiagnosticAnalyzer)(analyzer)).GetAnalyzerDiagnosticsAsync(canceled).GetAwaiter().GetResult()
            Catch ex As OperationCanceledException
                threw = True
            End Try
            Assert.True(threw)
            Dim diagnostics = compilation.WithAnalyzers(ImmutableArray.Create(Of DiagnosticAnalyzer)(analyzer)).GetAnalyzerDiagnosticsAsync().Result
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.GetMessage() = "parameter:Annotated")
        End Sub

        Private Shared Function Compile(source As String) As Compilation
            Return CreateCompilation(source, options:=Enabled, parseOptions:=VbNext)
        End Function

        Private Shared Function MethodOf(compilation As Compilation, name As String) As IMethodSymbol
            Return compilation.GetTypeByMetadataName("M").GetMembers(name).OfType(Of IMethodSymbol)().Single()
        End Function

        Private Shared Function ParameterOf(compilation As Compilation, name As String, occurrence As Integer) As IParameterSymbol
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim identifier = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().Where(Function(candidate) candidate.Identifier.ValueText = name).ElementAt(occurrence)
            Return DirectCast(model.GetSymbolInfo(identifier).Symbol, IParameterSymbol)
        End Function

        Private Shared Function Picture(compilation As Compilation) As String
            Return String.Join("|", compilation.GetDiagnostics().
                Where(Function(diagnostic) diagnostic.Severity <> DiagnosticSeverity.Hidden).
                Select(Function(diagnostic) diagnostic.Id & ":" & diagnostic.Location.SourceSpan.Start.ToString()))
        End Function

        Private NotInheritable Class AnnotationProbeAnalyzer
            Inherits DiagnosticAnalyzer

            Private Shared ReadOnly s_descriptor As New DiagnosticDescriptor(
                "VBNT0001",
                "Nullable probe",
                "{0}",
                "Test",
                DiagnosticSeverity.Info,
                isEnabledByDefault:=True)

            Public Overrides ReadOnly Property SupportedDiagnostics As ImmutableArray(Of DiagnosticDescriptor)
                Get
                    Return ImmutableArray.Create(s_descriptor)
                End Get
            End Property

            Public Overrides Sub Initialize(context As AnalysisContext)
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)
                context.EnableConcurrentExecution()
                context.RegisterCompilationStartAction(
                    Sub(start)
                        start.RegisterSymbolAction(AddressOf VisitSymbol, SymbolKind.Method)
                        start.RegisterSyntaxNodeAction(AddressOf VisitReturn, SyntaxKind.ReturnStatement)
                    End Sub)
            End Sub

            Private Shared Sub VisitSymbol(context As SymbolAnalysisContext)
                Dim method = TryCast(context.Symbol, IMethodSymbol)
                If method Is Nothing OrElse method.MethodKind <> MethodKind.Ordinary OrElse method.Parameters.IsEmpty Then
                    Return
                End If

                Report(context, "parameter:" & method.Parameters(0).NullableAnnotation.ToString())
                Report(context, "return:" & method.ReturnNullableAnnotation.ToString())
                Dim named = TryCast(method.ReturnType, INamedTypeSymbol)
                If named IsNot Nothing AndAlso named.TypeArguments.Length = 1 Then
                    Report(context, "argument:" & named.TypeArguments(0).NullableAnnotation.ToString())
                End If
            End Sub

            Private Shared Sub VisitReturn(context As SyntaxNodeAnalysisContext)
                Dim statement = DirectCast(context.Node, ReturnStatementSyntax)
                If statement.Expression Is Nothing Then
                    Return
                End If

                Dim info = context.SemanticModel.GetTypeInfo(statement.Expression, context.CancellationToken)
                Report(context, "flow:" & info.Nullability.FlowState.ToString())
            End Sub

            Private Shared Sub Report(context As SymbolAnalysisContext, message As String)
                context.ReportDiagnostic(CodeAnalysis.Diagnostic.Create(s_descriptor, context.Symbol.Locations.FirstOrDefault(), message))
            End Sub

            Private Shared Sub Report(context As SyntaxNodeAnalysisContext, message As String)
                context.ReportDiagnostic(CodeAnalysis.Diagnostic.Create(s_descriptor, context.Node.GetLocation(), message))
            End Sub
        End Class
    End Class
End Namespace
