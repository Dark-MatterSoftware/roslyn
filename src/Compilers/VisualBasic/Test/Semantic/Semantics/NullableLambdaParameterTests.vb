' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
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
            Assert.Equal("String", parameter.Type.ToDisplayString())
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
            Assert.Equal("String", parameter.Type.ToDisplayString())
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

        Private Shared Function Compile(source As String) As Compilation
            Return CreateCompilation(source, options:=Enabled, parseOptions:=VbNext)
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
