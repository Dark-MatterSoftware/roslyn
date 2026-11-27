' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableSemanticModelTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Enabled As VisualBasicCompilationOptions = TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable)

        <Fact>
        Public Sub DeclaredNullableReference_ReportsAnnotationAndMaybeNullFlow()
            Dim typeInfo = TypeInfoOf("
Module M
    Function Read(value As String?) As String?
        Return value
    End Function
End Module
", "value")

            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, typeInfo.Nullability.FlowState)
            AssertInvariant(typeInfo)
        End Sub

        <Fact>
        Public Sub NonnullBranch_RefinesFlowWithoutChangingDeclaredAnnotation()
            Dim typeInfo = TypeInfoOf("
Module M
    Sub Use(value As String?)
        If value IsNot Nothing Then
            Dim inside As Object = value
        End If
    End Sub
End Module
", "value", occurrence:=1)

            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.NotNull, typeInfo.Nullability.FlowState)
            Assert.Equal(NullableAnnotation.NotAnnotated, typeInfo.Type.NullableAnnotation)
            AssertInvariant(typeInfo)
        End Sub

        <Fact>
        Public Sub AfterBranch_ReportsMergedMaybeNull()
            Dim outside = TypeInfoOf("
Module M
    Sub Use(value As String?)
        If value IsNot Nothing Then
            Dim inside As Object = value
        End If
        Dim outside As Object = value
    End Sub
End Module
", "value", occurrence:=2)

            Assert.Equal(NullableAnnotation.Annotated, outside.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, outside.Nullability.FlowState)
            AssertInvariant(outside)
        End Sub

        <Fact>
        Public Sub OriginalAndConverted_AreDistinguishable()
            Dim typeInfo = TypeInfoOf("
Module M
    Sub Use(value As String?)
        Dim box As Object = value
    End Sub
End Module
", "value")

            Assert.Equal("String", typeInfo.Type.Name)
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, typeInfo.Nullability.FlowState)
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Type.NullableAnnotation)
            Assert.Equal("Object", typeInfo.ConvertedType.Name)
            Assert.Equal(NullableAnnotation.NotAnnotated, typeInfo.ConvertedNullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, typeInfo.ConvertedNullability.FlowState)
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.ConvertedType.NullableAnnotation)
            AssertInvariant(typeInfo)
        End Sub

        <Fact>
        Public Sub PublicType_SatisfiesFlowAnnotationInvariant()
            Dim typeInfo = TypeInfoOf("
Module M
    Function Read(value As String?) As String?
        Return value
    End Function
End Module
", "value")

            AssertInvariant(typeInfo)
            Assert.Equal(typeInfo.Nullability.FlowState.ToAnnotation(), typeInfo.Type.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub DeclarationSymbol_KeepsDeclaredAnnotation()
            Dim compilation = Compile("
Module M
    Sub Use(value As String?)
        If value IsNot Nothing Then
            Dim inside As Object = value
        End If
    End Sub
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim use = Identifiers(tree, "value").ElementAt(1)
            Dim info = model.GetTypeInfo(use)
            Assert.Equal(NullableFlowState.NotNull, info.Nullability.FlowState)
            Dim parameter = DirectCast(model.GetSymbolInfo(Identifiers(tree, "value").First()).Symbol, IParameterSymbol)
            Assert.Equal(NullableAnnotation.Annotated, parameter.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, parameter.Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, info.Type.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub LegacyVersions_DoNotReportExpressionFlow()
            Const source = "
Module M
    Sub Use(value As String)
        Dim text As String = value
    End Sub
End Module
"
            For Each language In {TestOptions.Regular, TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13), TestOptions.Regular.WithLanguageVersion(LanguageVersion.Latest)}
                Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=language)
                compilation.VerifyDiagnostics()
                Dim typeInfo = TypeInfoOf(compilation, "value")
                Assert.Equal(NullableFlowState.None, typeInfo.Nullability.FlowState)
                Assert.Equal(NullableFlowState.None, typeInfo.ConvertedNullability.FlowState)
                Assert.Equal(NullableAnnotation.None, typeInfo.Type.NullableAnnotation)
                AssertInvariant(typeInfo)
            Next
        End Sub

        <Fact>
        Public Sub QueryOrder_DoesNotChangeFlow()
            Dim compilation = Compile("
Module M
    Sub Use(value As String?)
        If value IsNot Nothing Then
            Dim inside As Object = value
        End If
        Dim outside As Object = value
    End Sub
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim names = Identifiers(tree, "value").ToArray()
            Dim reverse = model.GetTypeInfo(names(2))
            Dim forward = model.GetTypeInfo(names(1))
            Dim again = model.GetTypeInfo(names(2))
            Assert.Equal(NullableFlowState.NotNull, forward.Nullability.FlowState)
            Assert.Equal(NullableFlowState.MaybeNull, reverse.Nullability.FlowState)
            Assert.Equal(reverse.Nullability, again.Nullability)
        End Sub

        <Fact>
        Public Sub SemanticModelQuery_DoesNotChangeDiagnostics()
            Dim compilation = Compile("
Module M
    Sub Use(value As String?)
        Dim text As String = value
    End Sub
End Module
")
            Dim before = compilation.GetDiagnostics()
            Dim ignored = TypeInfoOf(compilation, "value")
            Dim after = compilation.GetDiagnostics()
            Assert.Equal(before.Select(Function(d) d.ToString()), after.Select(Function(d) d.ToString()))
        End Sub

        <Fact>
        Public Sub Assignment_UpdatesLaterUseAndLeavesEarlierUse()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim value As String? = Nothing
        Dim before As Object = value
        value = ""x""
        Dim after As Object = value
    End Sub
End Module
")
            Dim before = TypeInfoOf(compilation, "value", 0)
            Dim after = TypeInfoOf(compilation, "value", 2)
            Assert.Equal(NullableAnnotation.Annotated, before.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, before.Nullability.FlowState)
            Assert.Equal(NullableAnnotation.Annotated, after.Nullability.Annotation)
            Assert.Equal(NullableFlowState.NotNull, after.Nullability.FlowState)
            AssertInvariant(before)
            AssertInvariant(after)
        End Sub

        <Fact>
        Public Sub Literals_ReportReferenceNullState()
            Dim nothingInfo = LiteralInfo("
Module M
    Function Read() As String?
        Return Nothing
    End Function
End Module
", SyntaxKind.NothingLiteralExpression)
            Assert.Equal(NullableAnnotation.Annotated, nothingInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, nothingInfo.Nullability.FlowState)
            AssertInvariant(nothingInfo)

            Dim text = LiteralInfo("
Module M
    Function Read() As String
        Return ""x""
    End Function
End Module
", SyntaxKind.StringLiteralExpression)
            Assert.Equal(NullableFlowState.NotNull, text.Nullability.FlowState)
            AssertInvariant(text)
        End Sub

        <Fact>
        Public Sub ValueType_IsNotNullWithoutReferenceAnnotation()
            Dim typeInfo = TypeInfoOf("
Module M
    Function Read(value As Integer) As Integer
        Return value
    End Function
End Module
", "value")
            Assert.Equal(NullableAnnotation.None, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.NotNull, typeInfo.Nullability.FlowState)
            Assert.Equal(NullableAnnotation.NotAnnotated, typeInfo.Type.NullableAnnotation)
            AssertInvariant(typeInfo)
        End Sub

        <Fact>
        Public Sub StandaloneSpeculation_DoesNotInventEnclosingFlow()
            Dim compilation = Compile("
Module M
    Sub Use(value As String?)
        Dim copy As Object = value
    End Sub
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim position = Identifiers(tree, "value").First().SpanStart
            Dim speculated = model.GetSpeculativeTypeInfo(position, SyntaxFactory.ParseExpression("value"), SpeculativeBindingOption.BindAsExpression)
            Assert.Equal("String", speculated.Type.Name)
            Assert.Equal(NullableFlowState.None, speculated.Nullability.FlowState)
            Assert.Equal(NullableFlowState.None, speculated.ConvertedNullability.FlowState)
            AssertInvariant(speculated)
        End Sub

        <Fact>
        Public Sub LambdaParameter_ExposesTheSymbolAnnotationAndAnalyzedFlow()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As String?) As Object
                       Return item
                   End Function
    End Sub
End Module
")
            Dim typeInfo = TypeInfoOf(compilation, "item")
            Dim parameter = DirectCast(compilation.GetSemanticModel(compilation.SyntaxTrees.Single()).GetSymbolInfo(Identifiers(compilation.SyntaxTrees.Single(), "item").Single()).Symbol, IParameterSymbol)
            Assert.Equal("String", parameter.Type.ToDisplayString())
            Assert.True(parameter.Type.IsReferenceType)
            Assert.Equal(NullableAnnotation.Annotated, parameter.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, typeInfo.Nullability.FlowState)
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.ConvertedType.NullableAnnotation)
            AssertInvariant(typeInfo)
        End Sub

        <Fact>
        Public Sub LoopBody_UsesTheConvergedState()
            Dim typeInfo = TypeInfoOf("
Module M
    Sub Use()
        Dim current As String? = ""x""
        For i = 1 To 2
            Dim inside As Object = current
            current = Nothing
        Next
    End Sub
End Module
", "current")
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, typeInfo.Nullability.FlowState)
            AssertInvariant(typeInfo)
        End Sub

        <Fact>
        Public Sub SeparateSemanticModels_Agree()
            Dim compilation = Compile("
Module M
    Sub Use(value As String?)
        If value IsNot Nothing Then
            Dim inside As Object = value
        End If
    End Sub
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim first = compilation.GetSemanticModel(tree).GetTypeInfo(Identifiers(tree, "value").ElementAt(1))
            Dim second = compilation.GetSemanticModel(tree).GetTypeInfo(Identifiers(tree, "value").ElementAt(1))
            Assert.Equal(NullableFlowState.NotNull, first.Nullability.FlowState)
            Assert.Equal(first.Nullability, second.Nullability)
        End Sub

        <Fact>
        Public Sub ConcurrentQueries_Agree()
            Dim compilation = Compile("
Module M
    Sub Use(value As String?)
        If value IsNot Nothing Then
            Dim inside As Object = value
        End If
        Dim outside As Object = value
    End Sub
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim inside = Identifiers(tree, "value").ElementAt(1)
            Dim outside = Identifiers(tree, "value").ElementAt(2)
            Dim failures = 0
            System.Threading.Tasks.Parallel.For(0, 80, Sub(iteration As Integer)
                Dim selected = If(iteration Mod 2 = 0, inside, outside)
                Dim expected = If(iteration Mod 2 = 0, NullableFlowState.NotNull, NullableFlowState.MaybeNull)
                If model.GetTypeInfo(selected).Nullability.FlowState <> expected Then
                    Threading.Interlocked.Increment(failures)
                End If
            End Sub)
            Assert.Equal(0, failures)
        End Sub

        <Fact>
        Public Sub Cancellation_DoesNotPublishALaterInconsistentResult()
            Dim compilation = Compile("
Module M
    Sub Use(value As String?)
        If value IsNot Nothing Then
            Dim inside As Object = value
        End If
    End Sub
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim inside = Identifiers(tree, "value").ElementAt(1)
            Assert.Throws(Of OperationCanceledException)(Function() model.GetTypeInfo(inside, New Threading.CancellationToken(canceled:=True)))
            Assert.Equal(NullableFlowState.NotNull, model.GetTypeInfo(inside).Nullability.FlowState)
        End Sub

        <Fact>
        Public Sub TypeInfoQuery_DoesNotChangeMethodIL()
            Const source = "
Module M
    Function Read(value As String?) As String?
        Return value
    End Function
End Module
"
            Dim before = CompileAndVerify(Compile(source), verify:=Verification.Skipped).VisualizeIL("M.Read")
            Dim queried = Compile(source)
            Dim ignored = TypeInfoOf(queried, "value")
            Dim after = CompileAndVerify(queried, verify:=Verification.Skipped).VisualizeIL("M.Read")
            Assert.Equal(before, after)

            Const plain = "
Module M
    Function Read(value As String) As String
        Return value
    End Function
End Module
"
            Dim legacy = CompileAndVerify(CreateCompilation(plain, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)), verify:=Verification.Skipped).VisualizeIL("M.Read")
            Dim modern = CompileAndVerify(CreateCompilation(plain, options:=TestOptions.ReleaseDll, parseOptions:=VbNext), verify:=Verification.Skipped).VisualizeIL("M.Read")
            Assert.Equal(legacy, modern)
        End Sub

        <Fact>
        Public Sub RepeatedQuery_ReusesTheCapturedResult()
            Dim compilation = Compile("
Module M
    Sub Use(value As String?)
        If value IsNot Nothing Then
            Dim inside As Object = value
        End If
        Dim outside As Object = value
    End Sub
End Module
")
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim inside = Identifiers(tree, "value").ElementAt(1)
            Dim first = model.GetTypeInfo(inside)
            Dim second = first
            For index = 1 To 100
                second = model.GetTypeInfo(inside)
            Next
            Assert.Equal(first.Nullability, second.Nullability)
            Assert.Equal(NullableFlowState.NotNull, second.Nullability.FlowState)
        End Sub

        <Fact>
        Public Sub MissingName_DoesNotThrow()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim text = missing
    End Sub
End Module
")
            Dim typeInfo = TypeInfoOf(compilation, "missing")
            Assert.NotEqual(NullableFlowState.NotNull, typeInfo.Nullability.FlowState)
            AssertInvariant(typeInfo)
        End Sub

        Private Shared Function Compile(source As String) As Compilation
            Return CreateCompilation(source, options:=Enabled, parseOptions:=VbNext)
        End Function

        Private Shared Function TypeInfoOf(source As String, name As String, Optional occurrence As Integer = 0) As TypeInfo
            Return TypeInfoOf(Compile(source), name, occurrence)
        End Function

        Private Shared Function TypeInfoOf(compilation As Compilation, name As String, Optional occurrence As Integer = 0) As TypeInfo
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Return model.GetTypeInfo(Identifiers(tree, name).Skip(occurrence).First())
        End Function

        Private Shared Function LiteralInfo(source As String, kind As SyntaxKind) As TypeInfo
            Dim compilation = Compile(source)
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim expression = tree.GetRoot().DescendantNodes().OfType(Of LiteralExpressionSyntax)().First(Function(candidate) candidate.Kind = kind)
            Return model.GetTypeInfo(expression)
        End Function

        Private Shared Function Identifiers(tree As SyntaxTree, name As String) As IEnumerable(Of IdentifierNameSyntax)
            Return tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().Where(Function(e) e.Identifier.ValueText = name)
        End Function

        Private Shared Sub AssertInvariant(info As TypeInfo)
            If info.Type IsNot Nothing Then
                Assert.Equal(info.Nullability.FlowState.ToAnnotation(), info.Type.NullableAnnotation)
            End If

            If info.ConvertedType IsNot Nothing Then
                Assert.Equal(info.ConvertedNullability.FlowState.ToAnnotation(), info.ConvertedType.NullableAnnotation)
            End If
        End Sub
    End Class
End Namespace
