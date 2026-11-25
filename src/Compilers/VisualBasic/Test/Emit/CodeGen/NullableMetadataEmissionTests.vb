' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Reflection
Imports System.Reflection.Metadata
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableMetadataEmissionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        Private Const ApiSource As String = "
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Public Class Api
    Public Function Echo(text As String) As String
        Return text
    End Function
    Public Function Find(text As String?) As String?
        Return text
    End Function
    Public Function Mixed(text As String) As String?
        Return text
    End Function
    Public Function NonNullFromMaybe(text As String?) As String
        Return If(text, """")
    End Function
    Public Sub SetValue(text As String?)
    End Sub
    Public Sub Update(ByRef text As String?)
    End Sub
    Public Function MaybeIds() As String?()
        Return Nothing
    End Function
    Public Function Grid() As String?(,)
        Return Nothing
    End Function
    Public Function Jagged() As String?()()
        Return Nothing
    End Function
    Public Function Nested() As List(Of String?)
        Return Nothing
    End Function
    Public Function NestedNull() As List(Of String)?
        Return Nothing
    End Function
    Public Function Map() As Dictionary(Of String, String?)
        Return Nothing
    End Function
    Public Function LoadAsync() As Task(Of String?)
        Return Nothing
    End Function
    Public Async Function LoadAsyncBody() As Task(Of String?)
        Dim value As String? = Nothing
        Return value
    End Function
    Public Function Items() As IEnumerable(Of String?)
        Return Nothing
    End Function
    Public Iterator Function ItemSequence() As IEnumerable(Of String?)
        Yield Nothing
    End Function
    Public Function Id(Of T As Class)(value As T?) As T
        Return value
    End Function
    Public Function StructId(Of T As Structure)(value As T?) As T?
        Return value
    End Function
    Public Function Count() As Integer
        Return 0
    End Function
    Public Sub TakeList(values As List(Of String?))
    End Sub
End Class
"

        <Fact>
        Public Sub StandardAttributes_ArePlacedOnEachTypeContract()
            Dim api = EmitApi()
            Dim echo = Method(api, "Echo")
            AssertNoCombinedAttribute(echo)
            AssertFlags(echo.GetReturnTypeAttributes(), 1)
            AssertFlags(echo.Parameters(0).GetAttributes(), 1)

            Dim find = Method(api, "Find")
            AssertNoCombinedAttribute(find)
            AssertFlags(find.GetReturnTypeAttributes(), 2)
            AssertFlags(find.Parameters(0).GetAttributes(), 2)

            Dim mixed = Method(api, "Mixed")
            AssertNoCombinedAttribute(mixed)
            AssertFlags(mixed.GetReturnTypeAttributes(), 2)
            AssertFlags(mixed.Parameters(0).GetAttributes(), 1)

            Dim recovered = Method(api, "NonNullFromMaybe")
            AssertFlags(recovered.GetReturnTypeAttributes(), 1)
            AssertFlags(recovered.Parameters(0).GetAttributes(), 2)

            Dim setValue = Method(api, "SetValue")
            AssertNoCombinedAttribute(setValue)
            Assert.DoesNotContain(setValue.GetReturnTypeAttributes(), Function(attribute) attribute.AttributeClass.Name = "NullableAttribute")
            AssertFlags(setValue.Parameters(0).GetAttributes(), 2)

            AssertFlags(Method(api, "Update").Parameters(0).GetAttributes(), 2)
            AssertFlags(Method(api, "MaybeIds").GetReturnTypeAttributes(), 1, 2)
            AssertFlags(Method(api, "Grid").GetReturnTypeAttributes(), 1, 2)
            AssertFlags(Method(api, "Jagged").GetReturnTypeAttributes(), 1, 1, 2)
            AssertFlags(Method(api, "Nested").GetReturnTypeAttributes(), 1, 2)
            AssertFlags(Method(api, "NestedNull").GetReturnTypeAttributes(), 2, 1)
            AssertFlags(Method(api, "Map").GetReturnTypeAttributes(), 1, 1, 2)
            AssertFlags(Method(api, "LoadAsync").GetReturnTypeAttributes(), 1, 2)
            AssertFlags(Method(api, "LoadAsyncBody").GetReturnTypeAttributes(), 1, 2)
            AssertFlags(Method(api, "Items").GetReturnTypeAttributes(), 1, 2)
            AssertFlags(Method(api, "ItemSequence").GetReturnTypeAttributes(), 1, 2)
            AssertFlags(Method(api, "Id").Parameters(0).GetAttributes(), 2)
            AssertFlags(Method(api, "Id").GetReturnTypeAttributes(), 1)
            Assert.DoesNotContain(Method(api, "StructId").GetReturnTypeAttributes(), Function(attribute) attribute.AttributeClass.Name = "NullableAttribute")
            Assert.DoesNotContain(Method(api, "StructId").Parameters(0).GetAttributes(), Function(attribute) attribute.AttributeClass.Name = "NullableAttribute")
            Assert.DoesNotContain(Method(api, "Count").GetReturnTypeAttributes(), Function(attribute) attribute.AttributeClass.Name = "NullableAttribute")
            AssertFlags(Method(api, "TakeList").Parameters(0).GetAttributes(), 1, 2)
        End Sub

        <Fact>
        Public Sub RoundTrip_PreservesNestedAnnotations()
            Dim api = EmitApi()
            Dim nested = DirectCast(Method(api, "Nested").ReturnType, INamedTypeSymbol)
            Dim nestedNull = DirectCast(Method(api, "NestedNull").ReturnType, INamedTypeSymbol)
            Dim map = DirectCast(Method(api, "Map").ReturnType, INamedTypeSymbol)
            Dim task = DirectCast(Method(api, "LoadAsync").ReturnType, INamedTypeSymbol)
            Dim items = DirectCast(Method(api, "Items").ReturnType, INamedTypeSymbol)
            Dim ids = DirectCast(Method(api, "MaybeIds").ReturnType, IArrayTypeSymbol)
            Dim grid = DirectCast(Method(api, "Grid").ReturnType, IArrayTypeSymbol)
            Dim jagged = DirectCast(Method(api, "Jagged").ReturnType, IArrayTypeSymbol)
            Dim id = Method(api, "Id")

            Assert.Equal(NullableAnnotation.NotAnnotated, Method(api, "Nested").ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, nested.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, Method(api, "NestedNull").ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, nestedNull.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, map.TypeArgumentNullableAnnotations(0))
            Assert.Equal(NullableAnnotation.Annotated, map.TypeArgumentNullableAnnotations(1))
            Assert.Equal("System.Threading.Tasks.Task(Of String)", task.ToDisplayString())
            Assert.Equal(NullableAnnotation.Annotated, task.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, items.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, ids.ElementNullableAnnotation)
            Assert.Equal(2, grid.Rank)
            Assert.Equal(NullableAnnotation.Annotated, grid.ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(jagged.ElementType, IArrayTypeSymbol).ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, id.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, id.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, Method(api, "StructId").ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, Method(api, "Count").ReturnNullableAnnotation)
        End Sub

        <Fact>
        Public Sub ContextModes_EmitOnlyStoredAnnotations()
            Dim enabled = EmitApi(NullableContextOptions.Annotations)
            AssertFlags(Method(enabled, "Find").GetReturnTypeAttributes(), 2)

            Dim warnings = CreateCompilation("
Public Class Api
    Public Function Echo(text As String) As String
        Return text
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Warnings), parseOptions:=VbNext)
            warnings.VerifyDiagnostics()
            AssertNoNullableAttributes(Method(EmitAndImport(warnings), "Echo"))

            Dim disabled = CreateCompilation("
#Nullable Disable
Public Class Quiet
    Public Function Echo(text As String) As String
        Return text
    End Function
End Class
#Nullable Enable
Public Class Loud
    Public Function Find(text As String?) As String?
        Return text
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim image = disabled.EmitToImageReference()
            Dim consumer = CreateCompilation("", references:={image}, parseOptions:=VbNext)
            AssertNoNullableAttributes(DirectCast(consumer.GetTypeByMetadataName("Quiet").GetMembers("Echo").Single(), IMethodSymbol))
            AssertFlags(DirectCast(consumer.GetTypeByMetadataName("Loud").GetMembers("Find").Single(), IMethodSymbol).GetReturnTypeAttributes(), 2)

            Dim legacy = CreateCompilation("
Public Class Api
    Public Function Echo(text As String) As String
        Return text
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17)
            legacy.VerifyDiagnostics()
            AssertNoNullableAttributes(Method(EmitAndImport(legacy), "Echo"))
        End Sub

        <Fact>
        Public Sub EmbeddedRuntime_DoesNotReceiveNullableMetadata()
            Dim compilation = CreateCompilation("
Public Class Api
    Public Function Find(text As String?) As String?
        Return text
    End Function
    Public Sub UseRuntime()
        Microsoft.VisualBasic.CompilerServices.ProjectData.SetProjectError(Nothing)
    End Sub
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable).WithEmbedVbCoreRuntime(True), parseOptions:=VbNext)
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
            Dim image = compilation.EmitToImageReference()
            Dim consumer = CreateCompilation("", references:={image}, options:=TestOptions.ReleaseDll.WithMetadataImportOptions(MetadataImportOptions.All), parseOptions:=VbNext)
            Dim projectData = consumer.GetTypeByMetadataName("Microsoft.VisualBasic.CompilerServices.ProjectData")
            Assert.NotNull(projectData)
            Dim setProjectError = projectData.GetMembers("SetProjectError").Cast(Of IMethodSymbol)().First(Function(method) method.Parameters.Length = 1)
            AssertNoNullableAttributes(setProjectError)
            AssertFlags(DirectCast(consumer.GetTypeByMetadataName("Api").GetMembers("Find").Single(), IMethodSymbol).GetReturnTypeAttributes(), 2)
        End Sub

        <Fact>
        Public Sub CSharpConsumer_MatchesAnEquivalentContract()
            Dim vbReference = CreateCompilation(ApiSource, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext).EmitToImageReference()
            Dim csReference = CompileCSharp("
#nullable enable
using System.Collections.Generic;
public class Api
{
    public string Echo(string text) => text;
    public string? Find(string? text) => text;
    public string? Mixed(string text) => text;
    public string NonNullFromMaybe(string? text) => text ?? """";
    public void SetValue(string? text) { }
}
", TargetFramework.Net100)

            Assert.Equal(UseWarnings(csReference), UseWarnings(vbReference))
        End Sub

        <Fact>
        Public Sub VisualBasicConsumer_ReadsEmittedContracts()
            Dim reference = CreateCompilation(ApiSource, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext).EmitToImageReference()
            Dim consumer = CreateCompilation("
Public Class Use
    Public Sub M(api As Api)
        Dim plain As String = api.Find(Nothing)
        Dim marked As String? = api.Find(Nothing)
        api.SetValue(Nothing)
        api.Echo(""text"")
    End Sub
End Class
", references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            consumer.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "api.Find(Nothing)").WithArguments("plain").WithLocation(4, 31))
        End Sub

        <Fact>
        Public Sub Reflection_ReadsMixedNestedAndSubContracts()
            Dim compilation = CreateCompilation(ApiSource, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim bytes = compilation.EmitToArray().ToArray()
            Dim loaded = System.Reflection.Assembly.Load(bytes)
            Dim api = loaded.GetType("Api")
            Dim context As New NullabilityInfoContext()

            Dim mixed = context.Create(api.GetMethod("Mixed").ReturnParameter)
            Assert.Equal(NullabilityState.Nullable, mixed.ReadState)
            Dim mixedParameter = context.Create(api.GetMethod("Mixed").GetParameters()(0))
            Assert.Equal(NullabilityState.NotNull, mixedParameter.ReadState)

            Dim setValue = context.Create(api.GetMethod("SetValue").GetParameters()(0))
            Assert.Equal(NullabilityState.Nullable, setValue.ReadState)
            Assert.Equal(0, api.GetMethod("SetValue").ReturnParameter.GetCustomAttributesData().Where(Function(attribute) attribute.AttributeType.Name = "NullableAttribute").Count())

            Dim nested = context.Create(api.GetMethod("Nested").ReturnParameter)
            Assert.Equal(NullabilityState.NotNull, nested.ReadState)
            Assert.Equal(NullabilityState.Nullable, nested.GenericTypeArguments.Single().ReadState)

            Dim ids = context.Create(api.GetMethod("MaybeIds").ReturnParameter)
            Assert.Equal(NullabilityState.NotNull, ids.ReadState)
            Assert.Equal(NullabilityState.Nullable, ids.ElementType.ReadState)

            Dim task = context.Create(api.GetMethod("LoadAsync").ReturnParameter)
            Assert.Equal("Task`1", task.Type.Name)
            Assert.Equal(NullabilityState.Nullable, task.GenericTypeArguments.Single().ReadState)
        End Sub

        <Fact>
        Public Sub OlderFramework_EmbedsNullableAttributeWithoutADuplicateOnModernTargets()
            Dim legacy = CreateCompilation("
Public Class Api
    Public Function Find(text As String?) As String?
        Return text
    End Function
    Public Function Mixed(text As String) As String?
        Return text
    End Function
    Public Function Nested() As System.Collections.Generic.List(Of String?)
        Return Nothing
    End Function
    Public Function MaybeIds() As String?()
        Return Nothing
    End Function
    Public Sub SetValue(text As String?)
    End Sub
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            Dim legacyErrors = legacy.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(legacyErrors.Length = 0, String.Join(Environment.NewLine, legacyErrors.Select(Function(diagnostic) diagnostic.ToString())))
            Dim legacyBytes = legacy.EmitToArray().ToArray()
            Assert.Equal(2, NullableAttributeConstructorCount(legacyBytes))
            Dim legacyImage = MetadataReference.CreateFromImage(legacyBytes)
            Dim legacyConsumer = CreateCompilation("", references:={legacyImage}, parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            Dim legacyApi = legacyConsumer.GetTypeByMetadataName("Api")
            Dim find = DirectCast(legacyApi.GetMembers("Find").Single(), IMethodSymbol)
            Dim mixed = DirectCast(legacyApi.GetMembers("Mixed").Single(), IMethodSymbol)
            Dim nested = DirectCast(DirectCast(legacyApi.GetMembers("Nested").Single(), IMethodSymbol).ReturnType, INamedTypeSymbol)
            Dim ids = DirectCast(DirectCast(legacyApi.GetMembers("MaybeIds").Single(), IMethodSymbol).ReturnType, IArrayTypeSymbol)
            Dim setValue = DirectCast(legacyApi.GetMembers("SetValue").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, find.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, find.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, mixed.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, mixed.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, nested.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, ids.ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, setValue.Parameters(0).NullableAnnotation)
            Assert.DoesNotContain(setValue.GetReturnTypeAttributes(), Function(attribute) attribute.AttributeClass.Name = "NullableAttribute")

            Dim csharp = CreateCSharpCompilation("
#nullable enable
public class Use
{
    public static string Reject(Api api) => api.Mixed(""text"");
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Mscorlib40).Concat({legacyImage}).ToArray())
            Assert.Contains(csharp.GetDiagnostics(), Function(diagnostic) diagnostic.Id = "CS8603")

            Dim modern = CreateCompilation("
Public Class Api
    Public Function Find(text As String?) As String?
        Return text
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext, targetFramework:=TargetFramework.Net100)
            Assert.False(DefinesNullableAttribute(modern.EmitToArray().ToArray()))
            Dim modernConsumer = CreateCompilation("", references:={modern.EmitToImageReference()}, parseOptions:=VbNext, targetFramework:=TargetFramework.Net100)
            Dim modernFind = DirectCast(modernConsumer.GetTypeByMetadataName("Api").GetMembers("Find").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, modernFind.ReturnNullableAnnotation)
        End Sub

        Private Function EmitApi(Optional nullable As NullableContextOptions = NullableContextOptions.Enable) As VisualBasicCompilation
            Dim compilation = CreateCompilation(ApiSource, options:=TestOptions.ReleaseDll.WithNullableContext(nullable), parseOptions:=VbNext)
            Return EmitAndImport(compilation)
        End Function

        Private Function EmitAndImport(compilation As VisualBasicCompilation) As VisualBasicCompilation
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
            Dim image = compilation.EmitToImageReference()
            Return CreateCompilation("", references:={image}, parseOptions:=VbNext)
        End Function

        Private Shared Function Method(compilation As VisualBasicCompilation, name As String) As IMethodSymbol
            Return DirectCast(compilation.GetTypeByMetadataName("Api").GetMembers(name).Single(Function(member) member.Kind = SymbolKind.Method AndAlso DirectCast(member, IMethodSymbol).MethodKind = MethodKind.Ordinary), IMethodSymbol)
        End Function

        Private Shared Sub AssertNoCombinedAttribute(method As IMethodSymbol)
            Assert.DoesNotContain(method.GetAttributes(), Function(attribute) attribute.AttributeClass IsNot Nothing AndAlso attribute.AttributeClass.Name.StartsWith("Nullable", StringComparison.Ordinal))
        End Sub

        Private Shared Sub AssertNoNullableAttributes(method As IMethodSymbol)
            AssertNoCombinedAttribute(method)
            Assert.DoesNotContain(method.GetReturnTypeAttributes(), Function(attribute) attribute.AttributeClass.Name.StartsWith("Nullable", StringComparison.Ordinal))
            Assert.All(method.Parameters, Sub(parameter) Assert.DoesNotContain(parameter.GetAttributes(), Function(attribute) attribute.AttributeClass.Name.StartsWith("Nullable", StringComparison.Ordinal)))
        End Sub

        Private Shared Sub AssertFlags(attributes As ImmutableArray(Of AttributeData), ParamArray expected As Byte())
            Dim attribute = attributes.Single(Function(candidate) candidate.AttributeClass.Name = "NullableAttribute")
            Dim argument = attribute.ConstructorArguments.Single()
            Dim actual As Byte()
            If argument.Kind = TypedConstantKind.Array Then
                actual = argument.Values.Select(Function(value) CByte(value.Value)).ToArray()
            Else
                actual = {CByte(argument.Value)}
            End If

            Assert.Equal(expected, actual)
        End Sub

        Private Function CompileCSharp(source As String, target As TargetFramework) As MetadataReference
            Dim compilation = CreateCSharpCompilation(source, parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(target).ToArray())
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
            Return compilation.EmitToImageReference()
        End Function

        Private Function UseWarnings(reference As MetadataReference) As String()
            Dim compilation = CreateCSharpCompilation("
#nullable enable
public static class Use
{
    public static void M(Api api)
    {
        string plain = api.Find(null);
        string? marked = api.Find(null);
        api.SetValue(null);
        api.Echo(plain);
        string recovered = api.NonNullFromMaybe(null);
        string? mixed = api.Mixed(""text"");
        string rejected = api.Mixed(""text"");
    }
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net100).Concat({reference}).ToArray())
            Return compilation.GetDiagnostics().
                Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Warning).
                Select(Function(diagnostic) diagnostic.Id).
                OrderBy(Function(id) id).
                ToArray()
        End Function

        Private Shared Function DefinesNullableAttribute(image As Byte()) As Boolean
            Using metadata = ModuleMetadata.CreateFromImage(image)
                Dim reader = metadata.Module.MetadataReader
                For Each handle In reader.TypeDefinitions
                    Dim definition = reader.GetTypeDefinition(handle)
                    If reader.StringComparer.Equals(definition.Namespace, "System.Runtime.CompilerServices") AndAlso
                       reader.StringComparer.Equals(definition.Name, "NullableAttribute") Then
                        Return True
                    End If
                Next
            End Using

            Return False
        End Function

        Private Shared Function NullableAttributeConstructorCount(image As Byte()) As Integer
            Using metadata = ModuleMetadata.CreateFromImage(image)
                Dim reader = metadata.Module.MetadataReader
                For Each handle In reader.TypeDefinitions
                    Dim definition = reader.GetTypeDefinition(handle)
                    If reader.StringComparer.Equals(definition.Namespace, "System.Runtime.CompilerServices") AndAlso
                       reader.StringComparer.Equals(definition.Name, "NullableAttribute") Then
                        Dim count = 0
                        For Each methodHandle In definition.GetMethods()
                            Dim method = reader.GetMethodDefinition(methodHandle)
                            If reader.StringComparer.Equals(method.Name, ".ctor") Then
                                count += 1
                            End If
                        Next

                        Return count
                    End If
                Next
            End Using

            Return 0
        End Function
    End Class
End Namespace
