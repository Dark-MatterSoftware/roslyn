' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableLambdaParameterTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Enabled As VisualBasicCompilationOptions = TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable)

        <Fact>
        Public Sub ExplicitNullableLambdaParameter_IsANullableReference()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As String?) As Object
                       Return item.Length
                   End Function
    End Sub
End Module
")
            Assert.DoesNotContain(compilation.GetDiagnostics(), Function(d) d.Id = "BC33101")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Id = "BC42512")
            Dim parameter = ParameterOf(compilation, "item")
            Assert.Contains("item As String?", parameter.ToDisplayString(), StringComparison.Ordinal)
            Assert.True(parameter.Type.IsReferenceType)
            Assert.Equal(NullableAnnotation.Annotated, parameter.NullableAnnotation)
            Dim typeInfo = TypeInfoOf(compilation, "item")
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, typeInfo.Nullability.FlowState)
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Type.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub InferredNullableDelegateParameter_WarnsOnUncheckedUse()
            Dim compilation = Compile("
Delegate Function Take(value As String?) As Integer
Module M
    Sub Use()
        Dim read As Take = Function(item) item.Length
        Dim result = read(Nothing)
    End Sub
End Module
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Id = "BC42512")
            Dim parameter = ParameterOf(compilation, "item")
            Assert.Contains("item As String?", parameter.ToDisplayString(), StringComparison.Ordinal)
            Assert.Equal(NullableAnnotation.Annotated, parameter.NullableAnnotation)
            Dim typeInfo = TypeInfoOf(compilation, "item")
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.MaybeNull, typeInfo.Nullability.FlowState)
        End Sub

        <Fact>
        Public Sub ExplicitNullableLambdaParameter_RefinesAfterANullCheck()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As String?) As Object
                       If item IsNot Nothing Then
                           Return item
                       End If
                       Return Nothing
                   End Function
    End Sub
End Module
")
            Dim typeInfo = TypeInfoOf(compilation, "item", 1)
            Assert.Equal(NullableAnnotation.Annotated, typeInfo.Nullability.Annotation)
            Assert.Equal(NullableFlowState.NotNull, typeInfo.Nullability.FlowState)
            Assert.Equal(NullableAnnotation.NotAnnotated, typeInfo.Type.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub MethodNullableParameter_RemainsTheReferenceContract()
            Dim compilation = Compile("
Module M
    Sub Use(item As String?)
        Dim n = item.Length
    End Sub
End Module
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Id = "BC42512")
            Dim parameter = ParameterOf(compilation, "item")
            Assert.Equal(NullableAnnotation.Annotated, parameter.NullableAnnotation)
            Assert.True(parameter.Type.IsReferenceType)
            Assert.Equal(NullableFlowState.MaybeNull, TypeInfoOf(compilation, "item").Nullability.FlowState)
        End Sub

        <Fact>
        Public Sub ExplicitNullableValueTypeLambdaParameter_StaysNullableOfT()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As Integer?) As Integer
                       Return If(item, 0)
                   End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Dim parameter = ParameterOf(compilation, "item")
            Assert.Equal("Integer?", parameter.Type.ToDisplayString())
            Assert.False(parameter.Type.IsReferenceType)
            Assert.Equal(NullableAnnotation.None, parameter.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ExplicitNonNullableLambdaParameter_StartsNotNull()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As String) As Integer
                       Return item.Length
                   End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Dim parameter = ParameterOf(compilation, "item")
            Assert.Equal(NullableAnnotation.NotAnnotated, parameter.NullableAnnotation)
            Assert.Equal(NullableFlowState.NotNull, TypeInfoOf(compilation, "item").Nullability.FlowState)
        End Sub

        <Fact>
        Public Sub ExplicitParameter_DoesNotTakeTheDelegateAnnotation()
            Dim compilation = Compile("
Delegate Function Take(value As String?) As Integer
Module M
    Sub Use()
        Dim read As Take = Function(item As String) item.Length
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Assert.Equal(NullableAnnotation.NotAnnotated, ParameterOf(compilation, "item").NullableAnnotation)
        End Sub

        <Fact>
        Public Sub SingleLineNullableLambdaParameter_IsANullableReference()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As String?) item
    End Sub
End Module
")
            Assert.DoesNotContain(compilation.GetDiagnostics(), Function(d) d.Id = "BC33101")
            Assert.Equal(NullableAnnotation.Annotated, ParameterOf(compilation, "item").NullableAnnotation)
            Assert.Equal(NullableFlowState.MaybeNull, TypeInfoOf(compilation, "item").Nullability.FlowState)
        End Sub

        <Fact>
        Public Sub NestedNullableLambdaParameter_KeepsItsAnnotation()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim outer = Function(item As String?) As Object
                        Dim inner = Function(value As String?) As Object
                                        Return value
                                    End Function
                        Return inner(item)
                    End Function
    End Sub
End Module
")
            Assert.DoesNotContain(compilation.GetDiagnostics(), Function(d) d.Id = "BC33101")
            Assert.Equal(NullableAnnotation.Annotated, ParameterOf(compilation, "item").NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, ParameterOf(compilation, "value").NullableAnnotation)
        End Sub

        <Fact>
        Public Sub LegacyLambda_DoesNotTreatStringQuestionAsAnAnnotation()
            Dim compilation = CreateCompilation("
Module M
    Sub Use()
        Dim read = Function(item As String?) item
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Id = "BC33101")
        End Sub

        <Fact>
        Public Sub NullableContextDisabled_DoesNotTreatStringQuestionAsAnAnnotation()
            Dim compilation = CreateCompilation("
Module M
    Sub Use()
        Dim read = Function(item As String?) item
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable), parseOptions:=VbNext)
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Id = "BC33101")
        End Sub

        <Fact>
        Public Sub ExplicitNullableLambda_InvokeParameterIsAnnotated()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As String?) As Integer
                       If item Is Nothing Then
                           Return 0
                       End If

                       Return item.Length
                   End Function

        Dim result = read(Nothing)
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Dim invoke = DelegateInvoke(compilation, "read")
            Assert.Contains("As String?", invoke.Parameters(0).ToDisplayString(), StringComparison.Ordinal)
            Assert.Equal(NullableAnnotation.Annotated, invoke.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, invoke.Parameters(0).Type.NullableAnnotation)
            Assert.Equal("Integer", invoke.ReturnType.ToDisplayString())
            Assert.Equal(NullableAnnotation.None, invoke.ReturnNullableAnnotation)
        End Sub

        <Fact>
        Public Sub ExplicitNonNullableLambda_InvokeParameterWarnsOnNothing()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As String) As Integer
                       Return item.Length
                   End Function

        Dim result = read(Nothing)
    End Sub
End Module
")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullArgument, "Nothing").WithArguments("item"))
            Dim invoke = DelegateInvoke(compilation, "read")
            Assert.Equal(NullableAnnotation.NotAnnotated, invoke.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, invoke.Parameters(0).Type.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ExplicitNonNullableLambda_InvokeParameterWarnsOnMaybeNull()
            Dim compilation = Compile("
Module M
    Function Maybe() As String?
        Return Nothing
    End Function

    Sub Use()
        Dim read = Function(item As String) As Integer
                       Return item.Length
                   End Function

        Dim result = read(Maybe())
    End Sub
End Module
")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullArgument, "Maybe()").WithArguments("item"))
            Assert.Equal(NullableAnnotation.NotAnnotated, DelegateInvoke(compilation, "read").Parameters(0).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub SingleLineNonNullableLambda_InvokeParameterWarnsOnNothing()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As String) item.Length
        Dim result = read(Nothing)
    End Sub
End Module
")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullArgument, "Nothing").WithArguments("item"))
            Assert.Equal(NullableAnnotation.NotAnnotated, DelegateInvoke(compilation, "read").Parameters(0).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub NamedDelegateControls_KeepTheirInvokeAnnotations()
            Dim compilation = Compile("
Delegate Function OptionalRead(item As String?) As Integer
Delegate Function RequiredRead(item As String) As Integer
Module M
    Sub Use()
        Dim optionalRead As OptionalRead = Function(item As String?) As Integer
                                               If item Is Nothing Then
                                                   Return 0
                                               End If

                                               Return item.Length
                                           End Function
        Dim requiredRead As RequiredRead = Function(item As String) As Integer
                                               Return item.Length
                                           End Function
        Dim optionalResult = optionalRead(Nothing)
        Dim requiredResult = requiredRead(Nothing)
    End Sub
End Module
")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullArgument, "Nothing").WithArguments("item"))
            Assert.Equal(NullableAnnotation.Annotated, DelegateInvoke(compilation, "optionalRead").Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DelegateInvoke(compilation, "requiredRead").Parameters(0).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub NullableValueTypeLambda_InvokeParameterStaysOblivious()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(item As Integer?) As Integer
                       Return If(item, 0)
                   End Function

        Dim result = read(Nothing)
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Dim parameter = DelegateInvoke(compilation, "read").Parameters(0)
            Assert.Equal("Integer?", parameter.Type.ToDisplayString())
            Assert.Equal(NullableAnnotation.None, parameter.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub DistinctNullableLambdas_DoNotShareInvokeAnnotations()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim optionalRead = Function(item As String?) As Integer
                               Return If(item Is Nothing, 0, item.Length)
                           End Function
        Dim requiredRead = Function(item As String) As Integer
                               Return item.Length
                           End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Dim optionalInvoke = DelegateInvoke(compilation, "optionalRead")
            Dim requiredInvoke = DelegateInvoke(compilation, "requiredRead")
            Assert.Equal(NullableAnnotation.Annotated, optionalInvoke.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, requiredInvoke.Parameters(0).NullableAnnotation)
            Assert.True(SymbolEqualityComparer.Default.Equals(optionalInvoke.ContainingType, requiredInvoke.ContainingType))
            Assert.Equal(SymbolEqualityComparer.Default.GetHashCode(optionalInvoke.ContainingType), SymbolEqualityComparer.Default.GetHashCode(requiredInvoke.ContainingType))
            Assert.True(SymbolEqualityComparer.IncludeNullability.Equals(optionalInvoke.ContainingType, requiredInvoke.ContainingType))
            Assert.True(SymbolEqualityComparer.Default.Equals(optionalInvoke.Parameters(0).Type, requiredInvoke.Parameters(0).Type))
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(optionalInvoke.Parameters(0).Type, requiredInvoke.Parameters(0).Type))
        End Sub

        <Fact>
        Public Sub ReverseConstructionOrder_DoesNotContaminateInvokeAnnotations()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim requiredRead = Function(item As String) As Integer
                               Return item.Length
                           End Function
        Dim optionalRead = Function(item As String?) As Integer
                               Return If(item Is Nothing, 0, item.Length)
                           End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Assert.Equal(NullableAnnotation.NotAnnotated, DelegateInvoke(compilation, "requiredRead").Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DelegateInvoke(compilation, "optionalRead").Parameters(0).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub NestedLambdas_KeepInvokeAnnotations()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim outer = Function(item As String?) As Object
                        Dim inner = Function(value As String) As Integer
                                        Return value.Length
                                    End Function
                        Return inner(item)
                    End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullArgument, "item").WithArguments("value"))
            Assert.Equal(NullableAnnotation.Annotated, DelegateInvoke(compilation, "outer").Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DelegateInvoke(compilation, "inner").Parameters(0).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ByRefNonNullableLambda_InvokeParameterWarnsOnNothing()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function(ByRef item As String) As Integer
                       Return item.Length
                   End Function
        Dim value As String = Nothing
        Dim result = read(value)
    End Sub
End Module
")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("value"),
                Diagnostic(ERRID.WRN_PossibleNullArgument, "value").WithArguments("item"))
            Dim parameter = DelegateInvoke(compilation, "read").Parameters(0)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.Ref, parameter.RefKind)
            Assert.Equal(NullableAnnotation.NotAnnotated, parameter.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ClassConstrainedTypeParameter_InvokeKeepsTheDeclarationAnnotation()
            Dim compilation = Compile("
Module M
    Sub Use(Of T As Class)()
        Dim optionalRead = Function(item As T?) As T
                               Return item
                           End Function
        Dim requiredRead = Function(item As T) As T
                               Return item
                           End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReturn, "item"))
            Assert.Equal(NullableAnnotation.Annotated, DelegateInvoke(compilation, "optionalRead").Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DelegateInvoke(compilation, "requiredRead").Parameters(0).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ExplicitNullableLambdaReturn_IsANullableReference()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function() As String?
                       Return Nothing
                   End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Dim invoke = DelegateInvoke(compilation, "read")
            Assert.Contains("As String?", invoke.ToDisplayString(), StringComparison.Ordinal)
            Assert.Equal(NullableAnnotation.Annotated, invoke.ReturnNullableAnnotation)
            Assert.DoesNotContain(compilation.GetDiagnostics(), Function(d) d.Id = "BC33101")
        End Sub

        <Fact>
        Public Sub ExplicitNonNullableLambdaReturn_WarnsOnNothing()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function() As String
                       Return Nothing
                   End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReturn, "Nothing"))
            Assert.Equal(NullableAnnotation.NotAnnotated, DelegateInvoke(compilation, "read").ReturnNullableAnnotation)
        End Sub

        <Fact>
        Public Sub ExplicitNullableValueTypeLambdaReturn_StaysNullableOfT()
            Dim compilation = Compile("
Module M
    Sub Use()
        Dim read = Function() As Integer?
                       Return Nothing
                   End Function
    End Sub
End Module
")
            compilation.VerifyDiagnostics()
            Dim invoke = DelegateInvoke(compilation, "read")
            Assert.Equal("Integer?", invoke.ReturnType.ToDisplayString())
            Assert.Equal(NullableAnnotation.None, invoke.ReturnNullableAnnotation)
        End Sub

        <Fact>
        Public Sub LegacyAndDisabledContexts_DoNotExposeReferenceAnnotations()
            Dim legacy = CreateCompilation("
Module M
    Sub Use()
        Dim read = Function(item As String) As Integer
                       Return item.Length
                   End Function
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            legacy.VerifyDiagnostics()
            Assert.Equal(NullableAnnotation.None, DelegateInvoke(legacy, "read").Parameters(0).NullableAnnotation)

            Dim disabled = CreateCompilation("
Module M
    Sub Use()
        Dim read = Function(item As String) As Integer
                       Return item.Length
                   End Function
        Dim result = read(Nothing)
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable), parseOptions:=VbNext)
            disabled.VerifyDiagnostics()
            Assert.Equal(NullableAnnotation.None, DelegateInvoke(disabled, "read").Parameters(0).NullableAnnotation)
        End Sub

        Private Shared Function Compile(source As String) As Compilation
            Return CreateCompilation(source, options:=Enabled, parseOptions:=VbNext)
        End Function

        Private Shared Function DelegateInvoke(compilation As Compilation, variableName As String) As IMethodSymbol
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim syntax = tree.GetRoot().DescendantNodes().OfType(Of ModifiedIdentifierSyntax)().Single(Function(identifier) identifier.Identifier.ValueText = variableName)
            Dim local = DirectCast(model.GetDeclaredSymbol(syntax), ILocalSymbol)
            Return DirectCast(local.Type, INamedTypeSymbol).DelegateInvokeMethod
        End Function

        Private Shared Function ParameterOf(compilation As Compilation, name As String) As IParameterSymbol
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim syntax = tree.GetRoot().DescendantNodes().OfType(Of ParameterSyntax)().Single(Function(parameter) parameter.Identifier.Identifier.ValueText = name)
            Return DirectCast(model.GetDeclaredSymbol(syntax), IParameterSymbol)
        End Function

        Private Shared Function TypeInfoOf(compilation As Compilation, name As String, Optional occurrence As Integer = 0) As TypeInfo
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim syntax = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().Where(Function(identifier) identifier.Identifier.ValueText = name).ElementAt(occurrence)
            Return model.GetTypeInfo(syntax)
        End Function
    End Class
End Namespace
