' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Reflection
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableDeclarationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly Enabled As VisualBasicCompilationOptions = TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        <Fact>
        Public Sub Fields_StoreReferenceAnnotations()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Public Class Box
    Private _name As String?
    Public Name As String
    Public Shared Cache As Dictionary(Of String, String?)
    Private ReadOnly _items As String?()
    Public A, B As String?
    Public Numbers() As Integer?
    Public Marked? As String
    Public MarkedArray?() As String
End Class
", options:=Enabled, parseOptions:=VbNext)
            AssertNoErrors(compilation)
            Dim box = compilation.GetTypeByMetadataName("Box")
            AssertField(box, "_name", NullableAnnotation.Annotated, SpecialType.System_String)
            AssertField(box, "Name", NullableAnnotation.NotAnnotated, SpecialType.System_String)
            Dim cache = DirectCast(box.GetMembers("Cache").Single(), IFieldSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, cache.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(cache.Type, INamedTypeSymbol).TypeArgumentNullableAnnotations(1))
            Dim items = DirectCast(box.GetMembers("_items").Single(), IFieldSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, items.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(items.Type, IArrayTypeSymbol).ElementNullableAnnotation)
            AssertField(box, "A", NullableAnnotation.Annotated, SpecialType.System_String)
            AssertField(box, "B", NullableAnnotation.Annotated, SpecialType.System_String)
            Dim numbers = DirectCast(DirectCast(box.GetMembers("Numbers").Single(), IFieldSymbol).Type, IArrayTypeSymbol)
            Assert.Equal(SpecialType.System_Nullable_T, numbers.ElementType.OriginalDefinition.SpecialType)
            AssertField(box, "Marked", NullableAnnotation.Annotated, SpecialType.System_String)
            AssertArray(box, "MarkedArray", NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
        End Sub

        <Fact>
        Public Sub Fields_StayObliviousWhenAnnotationsAreDisabled()
            Dim compilation = CreateCompilation("
Public Class Box
    Public Name As String
    Public Count As Integer?
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            AssertNoErrors(compilation)
            Dim box = compilation.GetTypeByMetadataName("Box")
            Assert.Equal(NullableAnnotation.None, DirectCast(box.GetMembers("Name").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(SpecialType.System_Nullable_T, DirectCast(box.GetMembers("Count").Single(), IFieldSymbol).Type.OriginalDefinition.SpecialType)

            Dim rejected = CreateCompilation("
Public Class Box
    Public Name As String?
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            rejected.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraintNull, "Name").WithArguments("String").WithLocation(3, 12))
        End Sub

        <Fact>
        Public Sub Properties_StoreAccessorAndBackingContracts()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Public Class Customer
End Class
Public Interface IBox
    Property Name As String?
End Interface
Public MustInherit Class Base
    Public MustOverride Property Name As String?
    Public MustOverride ReadOnly Property Title As String?
End Class
Public Class Box
    Inherits Base
    Implements IBox
    Public Overrides Property Name As String? Implements IBox.Name
    Public Overrides ReadOnly Property Title As String?
        Get
            Return Name
        End Get
    End Property
    Public ReadOnly Property Customer As Customer?
    Public Property Items As List(Of String?)
    Public Shared Property Cache As String?
    Public WriteOnly Property Note As String?
        Set(value As String?)
        End Set
    End Property
    Public Property Item(index As Integer) As String?
        Get
            Return Nothing
        End Get
        Set(value As String?)
        End Set
    End Property
End Class
", options:=Enabled, parseOptions:=VbNext)
            AssertNoErrors(compilation)
            Dim box = compilation.GetTypeByMetadataName("Box")
            AssertProperty(box, "Name", NullableAnnotation.Annotated)
            AssertProperty(box, "Title", NullableAnnotation.Annotated)
            AssertProperty(box, "Customer", NullableAnnotation.Annotated)
            Dim items = DirectCast(box.GetMembers("Items").Single(), IPropertySymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, items.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(items.Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            AssertProperty(box, "Cache", NullableAnnotation.Annotated)
            AssertProperty(box, "Note", NullableAnnotation.Annotated)
            Dim indexed = DirectCast(box.GetMembers("Item").Single(), IPropertySymbol)
            Assert.Equal(NullableAnnotation.Annotated, indexed.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, indexed.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, indexed.SetMethod.Parameters.Last().NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, indexed.GetMethod.ReturnNullableAnnotation)

            Dim reference = compilation.EmitToImageReference()
            Dim consumer = CreateCompilation("", references:={reference}, options:=TestOptions.ReleaseDll.WithMetadataImportOptions(MetadataImportOptions.All), parseOptions:=VbNext)
            Dim imported = consumer.GetTypeByMetadataName("Box")
            AssertProperty(imported, "Name", NullableAnnotation.Annotated)
            Dim importedName = DirectCast(imported.GetMembers("Name").Single(), IPropertySymbol)
            Assert.Equal(NullableAnnotation.Annotated, importedName.GetMethod.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, importedName.SetMethod.Parameters.Last().NullableAnnotation)
            Dim backing = imported.GetMembers().OfType(Of IFieldSymbol)().Single(Function(field) field.Name.Contains("Name"))
            Assert.Equal(NullableAnnotation.Annotated, backing.NullableAnnotation)
            AssertFlags(importedName.GetAttributes(), 2)
            AssertFlags(importedName.GetMethod.GetReturnTypeAttributes(), 2)
            AssertFlags(importedName.SetMethod.Parameters.Last().GetAttributes(), 2)
            AssertFlags(backing.GetAttributes(), 2)
        End Sub

        <Fact>
        Public Sub Delegates_StoreReturnAndParameterAnnotations()
            Dim compilation = CreateCompilation("
Public Delegate Function Find(id As Integer) As String?
Public Delegate Sub Notify(value As String?)
Public Delegate Function Factory(Of T As Class)() As T?
", options:=Enabled, parseOptions:=VbNext)
            AssertNoErrors(compilation)
            Dim find = DirectCast(compilation.GetTypeByMetadataName("Find"), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, find.DelegateInvokeMethod.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, find.DelegateInvokeMethod.Parameters(0).NullableAnnotation)
            Dim notify = DirectCast(compilation.GetTypeByMetadataName("Notify"), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, notify.DelegateInvokeMethod.Parameters(0).NullableAnnotation)
            Dim factory = DirectCast(compilation.GetTypeByMetadataName("Factory`1"), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, factory.DelegateInvokeMethod.ReturnNullableAnnotation)

            Dim endInvoke = DirectCast(find.GetMembers("EndInvoke").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, endInvoke.ReturnNullableAnnotation)
            Dim beginInvoke = DirectCast(notify.GetMembers("BeginInvoke").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, beginInvoke.Parameters(0).NullableAnnotation)

            Dim reference = compilation.EmitToImageReference()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
public class Use
{
    public static string? Read(Find find) => find(1);
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net100).Concat({reference}).ToArray())
            AssertNoErrors(csharp)
            Dim read = DirectCast(csharp.GetTypeByMetadataName("Use").GetMembers("Read").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, read.ReturnNullableAnnotation)
        End Sub

        <Fact>
        Public Sub UnconstrainedTypeParameter_UsesNullableAnnotationSemantics()
            Dim compilation = CreateCompilation("
Public Class Animal
End Class
Public Structure Point
End Structure
Public Class Open(Of T)
    Public F As T?
    Public Property P As T?
    Public Function Id(value As T?) As T?
        Return value
    End Function
    Public Delegate Function Make() As T?
End Class
Public Class Closed
    Public Function ClassId(Of T As Class)(value As T?) As T?
        Return value
    End Function
    Public Function StructId(Of T As Structure)(value As T?) As T?
        Return Nothing
    End Function
    Public Function Pass(Of U)(value As Open(Of U)) As U?
        Return value.F
    End Function
End Class
", options:=Enabled, parseOptions:=VbNext)
            AssertNoErrors(compilation)
            Dim open = DirectCast(compilation.GetTypeByMetadataName("Open`1"), INamedTypeSymbol)
            Dim stringOpen = open.Construct(compilation.GetSpecialType(SpecialType.System_String))
            Dim animal = compilation.GetTypeByMetadataName("Animal")
            Dim animalOpen = open.Construct(animal)
            Dim integerOpen = open.Construct(compilation.GetSpecialType(SpecialType.System_Int32))
            Dim point = compilation.GetTypeByMetadataName("Point")
            Dim pointOpen = open.Construct(point)
            Dim nullableInteger = compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(compilation.GetSpecialType(SpecialType.System_Int32))
            Dim nullableOpen = open.Construct(nullableInteger)

            AssertSubstitutedReference(stringOpen, SpecialType.System_String)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(animalOpen.GetMembers("F").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(TypeKind.Class, DirectCast(animalOpen.GetMembers("F").Single(), IFieldSymbol).Type.TypeKind)
            AssertSubstitutedValue(integerOpen, SpecialType.System_Int32)
            AssertSubstitutedValue(pointOpen, SpecialType.None)
            Assert.Equal(SpecialType.System_Nullable_T, DirectCast(nullableOpen.GetMembers("F").Single(), IFieldSymbol).Type.OriginalDefinition.SpecialType)
            Assert.Equal(NullableAnnotation.None, DirectCast(nullableOpen.GetMembers("F").Single(), IFieldSymbol).NullableAnnotation)

            Dim closed = compilation.GetTypeByMetadataName("Closed")
            Dim classId = DirectCast(closed.GetMembers("ClassId").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, classId.ReturnNullableAnnotation)
            Assert.Equal(TypeKind.TypeParameter, classId.ReturnType.TypeKind)
            Dim structId = DirectCast(closed.GetMembers("StructId").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.None, structId.ReturnNullableAnnotation)
            Assert.Equal(SpecialType.System_Nullable_T, structId.ReturnType.OriginalDefinition.SpecialType)
            Dim pass = DirectCast(closed.GetMembers("Pass").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, pass.ReturnNullableAnnotation)
            Dim passedField = DirectCast(DirectCast(pass.Parameters(0).Type, INamedTypeSymbol).GetMembers("F").Single(), IFieldSymbol)
            Assert.Equal(NullableAnnotation.Annotated, passedField.NullableAnnotation)
            Assert.Equal(TypeKind.TypeParameter, passedField.Type.TypeKind)

            Dim disabled = CreateCompilation("
Public Class Closed
    Public Function StructId(Of T As Structure)(value As T?) As T?
        Return Nothing
    End Function
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            AssertNoErrors(disabled)
            Dim disabledStruct = DirectCast(disabled.GetTypeByMetadataName("Closed").GetMembers("StructId").Single(), IMethodSymbol)
            Assert.Equal(SpecialType.System_Nullable_T, disabledStruct.ReturnType.OriginalDefinition.SpecialType)
        End Sub

        <Fact>
        Public Sub Arrays_DistinguishElementAndReferenceAnnotations()
            Dim compilation = CreateCompilation("
Public Class Box
    Public Plain As String()
    Public Elements As String?()
    Public Reference As String()?
    Public Both As String?()?
    Public Grid As String(,)?
    Public NullableGrid As String?(,)?
    Public Outer As String()()?
    Public Inner As String()?()
    Public Values As Integer?()
    Public ValueReference As Integer()?
    Public NullableValues As Integer?()?
    Public Identified() As String?
End Class
", options:=Enabled, parseOptions:=VbNext)
            AssertNoErrors(compilation)
            Dim box = compilation.GetTypeByMetadataName("Box")
            AssertArray(box, "Plain", NullableAnnotation.NotAnnotated, NullableAnnotation.NotAnnotated)
            AssertArray(box, "Elements", NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
            AssertArray(box, "Reference", NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertArray(box, "Both", NullableAnnotation.Annotated, NullableAnnotation.Annotated)
            AssertArray(box, "Grid", NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            Assert.Equal(2, DirectCast(DirectCast(box.GetMembers("Grid").Single(), IFieldSymbol).Type, IArrayTypeSymbol).Rank)
            AssertArray(box, "NullableGrid", NullableAnnotation.Annotated, NullableAnnotation.Annotated)
            Dim outer = DirectCast(DirectCast(box.GetMembers("Outer").Single(), IFieldSymbol).Type, IArrayTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(box.GetMembers("Outer").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, outer.ElementNullableAnnotation)
            Dim inner = DirectCast(DirectCast(box.GetMembers("Inner").Single(), IFieldSymbol).Type, IArrayTypeSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(box.GetMembers("Inner").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, inner.ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(inner.ElementType, IArrayTypeSymbol).ElementNullableAnnotation)
            Dim values = DirectCast(box.GetMembers("Values").Single(), IFieldSymbol)
            Assert.Equal(SpecialType.System_Nullable_T, DirectCast(values.Type, IArrayTypeSymbol).ElementType.OriginalDefinition.SpecialType)
            AssertArray(box, "ValueReference", NullableAnnotation.Annotated, NullableAnnotation.None)
            Assert.Equal(SpecialType.System_Int32, DirectCast(DirectCast(box.GetMembers("ValueReference").Single(), IFieldSymbol).Type, IArrayTypeSymbol).ElementType.SpecialType)
            Dim nullableValues = DirectCast(box.GetMembers("NullableValues").Single(), IFieldSymbol)
            Assert.Equal(NullableAnnotation.Annotated, nullableValues.NullableAnnotation)
            Assert.Equal(SpecialType.System_Nullable_T, DirectCast(nullableValues.Type, IArrayTypeSymbol).ElementType.OriginalDefinition.SpecialType)
            AssertArray(box, "Identified", NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
        End Sub

        <Fact>
        Public Sub Parser_AcceptsArraySuffixesOnVbNextOnly()
            Dim source = "Class C" & vbLf & "Public A As String()?" & vbLf & "Public B As String?()?" & vbLf & "Public C As String()?()" & vbLf & "End Class"
            Dim tree = VisualBasicSyntaxTree.ParseText(source, VbNext)
            Assert.Empty(tree.GetDiagnostics())
            Assert.Equal(source, tree.GetRoot().ToFullString())
            Dim types = tree.GetRoot().DescendantNodes().OfType(Of FieldDeclarationSyntax)().Select(Function(field) field.Declarators(0).AsClause.Type).ToArray()
            Assert.Equal(SyntaxKind.NullableType, types(0).Kind())
            Assert.Equal(SyntaxKind.ArrayType, DirectCast(types(0), NullableTypeSyntax).ElementType.Kind())
            Assert.Equal(SyntaxKind.NullableType, types(1).Kind())
            Assert.Equal(SyntaxKind.ArrayType, types(2).Kind())
            Assert.Equal(SyntaxKind.NullableType, DirectCast(types(2), ArrayTypeSyntax).ElementType.Kind())

            Dim continued = "Class C" & vbLf & "Public A As String() _" & vbLf & "?" & vbLf & "End Class"
            Assert.Empty(VisualBasicSyntaxTree.ParseText(continued, VbNext).GetDiagnostics())
            Dim spaced = "Class C" & vbLf & "Public A As String() ?" & vbLf & "End Class"
            Assert.Empty(VisualBasicSyntaxTree.ParseText(spaced, VbNext).GetDiagnostics())

            Dim legacy = VisualBasicSyntaxTree.ParseText(source, Vb17)
            Assert.NotEmpty(legacy.GetDiagnostics())
            Dim values = "Class C" & vbLf & "Public A As Integer?()" & vbLf & "End Class"
            Assert.Empty(VisualBasicSyntaxTree.ParseText(values, Vb17).GetDiagnostics())

            Dim expressions = "Class C" & vbLf &
                "Function M(a As String) As Integer" & vbLf &
                "Return If(a?.Length, 0)" & vbLf &
                "End Function" & vbLf &
                "Sub N(ByRef a As Integer)" & vbLf &
                "a ?= 1" & vbLf &
                "End Sub" & vbLf &
                "End Class"
            Assert.Empty(VisualBasicSyntaxTree.ParseText(expressions, VbNext).GetDiagnostics())

            Dim malformed = "Class C" & vbLf & "Public A As String()??" & vbLf & "End Class"
            Assert.NotEmpty(VisualBasicSyntaxTree.ParseText(malformed, VbNext).GetDiagnostics())
            Dim identifierSuffix = "Class C" & vbLf & "Public A()? As String" & vbLf & "End Class"
            Assert.NotEmpty(VisualBasicSyntaxTree.ParseText(identifierSuffix, VbNext).GetDiagnostics())
        End Sub

        <Fact>
        Public Sub Metadata_RoundTripsDeclarationSites()
            Dim compilation = CreateCompilation("
Public Delegate Function Find(id As Integer) As String?
Public Class Box(Of T)
    Public Name As String?
    Public Items As String?()
    Public Reference As String()?
    Public Both As String?()?
    Public Function Id(value As T?) As T?
        Return value
    End Function
End Class
", options:=Enabled, parseOptions:=VbNext)
            AssertNoErrors(compilation)
            Dim reference = compilation.EmitToImageReference()
            Dim consumer = CreateCompilation("", references:={reference}, parseOptions:=VbNext)
            Dim box = consumer.GetTypeByMetadataName("Box`1")
            AssertFlags(DirectCast(box.GetMembers("Name").Single(), IFieldSymbol).GetAttributes(), 2)
            AssertFlags(DirectCast(box.GetMembers("Items").Single(), IFieldSymbol).GetAttributes(), 1, 2)
            AssertFlags(DirectCast(box.GetMembers("Reference").Single(), IFieldSymbol).GetAttributes(), 2, 1)
            AssertFlags(DirectCast(box.GetMembers("Both").Single(), IFieldSymbol).GetAttributes(), 2)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(box.GetMembers("Both").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(box.GetMembers("Both").Single(), IFieldSymbol).Type, IArrayTypeSymbol).ElementNullableAnnotation)
            Dim id = DirectCast(box.GetMembers("Id").Single(), IMethodSymbol)
            AssertFlags(id.GetReturnTypeAttributes(), 2)
            AssertFlags(id.Parameters(0).GetAttributes(), 2)
            Dim find = DirectCast(consumer.GetTypeByMetadataName("Find").GetMembers("Invoke").Single(), IMethodSymbol)
            AssertFlags(find.GetReturnTypeAttributes(), 2)
            Assert.Equal(NullableAnnotation.Annotated, find.ReturnNullableAnnotation)

            Dim csharp = CreateCSharpCompilation("
#nullable enable
public class Use
{
    public static string? Read(Box<string> box) => box.Name;
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net100).Concat({reference}).ToArray())
            AssertNoErrors(csharp)
            Dim read = DirectCast(csharp.GetTypeByMetadataName("Use").GetMembers("Read").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(read.Parameters(0).Type, INamedTypeSymbol).GetMembers("Name").OfType(Of IFieldSymbol)().Single().NullableAnnotation)
        End Sub

        <Fact>
        Public Sub Reflection_ReadsFieldPropertyAndArrayContracts()
            Dim compilation = CreateCompilation("
Public Delegate Function Find(id As Integer) As String?
Public Class Box
    Public Name As String?
    Public Property Title As String?
    Public Elements As String?()
    Public Reference As String()?
    Public Both As String?()?
    Public Function Id(Of T)(value As T) As T?
        Return Nothing
    End Function
End Class
", options:=Enabled, parseOptions:=VbNext)
            AssertNoErrors(compilation)
            Dim loaded = System.Reflection.Assembly.Load(compilation.EmitToArray().ToArray())
            Dim box = loaded.GetType("Box")
            Dim context As New NullabilityInfoContext()
            Assert.Equal(NullabilityState.Nullable, context.Create(box.GetField("Name")).ReadState)
            Assert.Equal(NullabilityState.Nullable, context.Create(box.GetProperty("Title")).ReadState)
            Dim elements = context.Create(box.GetField("Elements"))
            Assert.Equal(NullabilityState.NotNull, elements.ReadState)
            Assert.Equal(NullabilityState.Nullable, elements.ElementType.ReadState)
            Dim reference = context.Create(box.GetField("Reference"))
            Assert.Equal(NullabilityState.Nullable, reference.ReadState)
            Assert.Equal(NullabilityState.NotNull, reference.ElementType.ReadState)
            Dim both = context.Create(box.GetField("Both"))
            Assert.Equal(NullabilityState.Nullable, both.ReadState)
            Assert.Equal(NullabilityState.Nullable, both.ElementType.ReadState)
            Dim id = context.Create(box.GetMethod("Id").ReturnParameter)
            Assert.Equal(NullabilityState.Nullable, id.ReadState)
            Dim invoke = loaded.GetType("Find").GetMethod("Invoke")
            Assert.Equal(NullabilityState.Nullable, context.Create(invoke.ReturnParameter).ReadState)
        End Sub

        <Fact>
        Public Sub OlderFramework_EmbedsNullableAttributeForAField()
            Dim compilation = CreateCompilation("
Public Class Box
    Public Name As String?
    Public Reference As String()?
End Class
", options:=Enabled, parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            AssertNoErrors(compilation)
            Dim bytes = compilation.EmitToArray().ToArray()
            Assert.Equal(2, NullableAttributeConstructorCount(bytes))
            Dim image = MetadataReference.CreateFromImage(bytes)
            Dim consumer = CreateCompilation("", references:={image}, parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            Dim box = consumer.GetTypeByMetadataName("Box")
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(box.GetMembers("Name").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(box.GetMembers("Reference").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(DirectCast(box.GetMembers("Reference").Single(), IFieldSymbol).Type, IArrayTypeSymbol).ElementNullableAnnotation)
        End Sub

        Private Shared Sub AssertField(container As INamedTypeSymbol, name As String, annotation As NullableAnnotation, specialType As SpecialType)
            Dim field = DirectCast(container.GetMembers(name).Single(), IFieldSymbol)
            Assert.Equal(annotation, field.NullableAnnotation)
            Assert.Equal(specialType, field.Type.SpecialType)
        End Sub

        Private Shared Sub AssertProperty(container As INamedTypeSymbol, name As String, annotation As NullableAnnotation)
            Dim [property] = DirectCast(container.GetMembers(name).Single(), IPropertySymbol)
            Assert.Equal(annotation, [property].NullableAnnotation)
            If [property].GetMethod IsNot Nothing Then
                Assert.Equal(annotation, [property].GetMethod.ReturnNullableAnnotation)
            End If
        End Sub

        Private Shared Sub AssertArray(container As INamedTypeSymbol, name As String, annotation As NullableAnnotation, elementAnnotation As NullableAnnotation)
            Dim field = DirectCast(container.GetMembers(name).Single(), IFieldSymbol)
            Assert.Equal(annotation, field.NullableAnnotation)
            Assert.Equal(elementAnnotation, DirectCast(field.Type, IArrayTypeSymbol).ElementNullableAnnotation)
        End Sub

        Private Shared Sub AssertSubstitutedReference(open As INamedTypeSymbol, specialType As SpecialType)
            Dim field = DirectCast(open.GetMembers("F").Single(), IFieldSymbol)
            Dim [property] = DirectCast(open.GetMembers("P").Single(), IPropertySymbol)
            Dim method = DirectCast(open.GetMembers("Id").Single(), IMethodSymbol)
            Assert.Equal(specialType, field.Type.SpecialType)
            Assert.Equal(NullableAnnotation.Annotated, field.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, [property].NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, method.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, method.Parameters(0).NullableAnnotation)
        End Sub

        Private Shared Sub AssertSubstitutedValue(open As INamedTypeSymbol, specialType As SpecialType)
            Dim field = DirectCast(open.GetMembers("F").Single(), IFieldSymbol)
            Assert.NotEqual(SpecialType.System_Nullable_T, field.Type.OriginalDefinition.SpecialType)
            If specialType <> SpecialType.None Then
                Assert.Equal(specialType, field.Type.SpecialType)
            End If
            Assert.Equal(NullableAnnotation.None, field.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(open.GetMembers("Id").Single(), IMethodSymbol).ReturnNullableAnnotation)
        End Sub

        Private Shared Sub AssertFlags(attributes As ImmutableArray(Of AttributeData), ParamArray expected As Byte())
            Dim attribute = attributes.Single(Function(item) item.AttributeClass.Name = "NullableAttribute")
            Dim value = attribute.ConstructorArguments(0)
            Dim actual As Byte()
            If value.Kind = TypedConstantKind.Primitive Then
                actual = {CByte(value.Value)}
            Else
                actual = value.Values.Select(Function(item) CByte(item.Value)).ToArray()
            End If
            Assert.Equal(expected, actual)
        End Sub

        Private Shared Sub AssertNoErrors(compilation As Compilation)
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
        End Sub

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
