' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableMetadataImportTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        Private Const LibrarySource As String = "
#nullable enable
using System;
using System.Collections.Generic;

namespace Nrt
{
    public static class Api
    {
        public static string Echo(string text) => text;
        public static string? Find(string? text) => text;
        public static string? Mixed(string text) => text;
        public static void Take(string text) { }
        public static void TakeMaybe(string? text) { }
        public static void Write(ref string text) { }
        public static void Produce(out string? text) { text = null; }
    }

    public class Contracts
    {
        public List<string?> NullableItems;
        public List<string> PlainItems;
        public List<string>? NullableList;
        public List<string?>? Both;
        public Dictionary<string, string?> Map;
        public Dictionary<string, List<string?>?> Nested;
        public IEnumerable<string?> Sequence() => null;
        public string?[] NullableElements;
        public string[]? NullableArray;
        public string?[]? BothArrays;
        public string?[][] Jagged;
        public string?[,] Multi;
        public string? Field;
        public string PlainField;
        public string? Name { get; set; }
        public string PlainName { get; set; }
        public string? this[string? key] => key;
        public T? ClassGet<T>(T value) where T : class => value;
        public T? StructGet<T>(T value) where T : struct => value;
        public T? OpenGet<T>(T value) => value;
        public void ClassBound<T>() where T : class? { }
        public void ClassNonNull<T>() where T : class { }
        public void Compared<T>() where T : IComparable<string?> { }
        public virtual string? VirtualFind() => null;
    }

    public class Derived : Contracts
    {
        public override string? VirtualFind() => null;
    }

    public interface IBox<T>
    {
        T? Value { get; }
    }

    public class StringBox : IBox<string?>
    {
        public string? Value => null;
    }

    public delegate string? Handler(string? text);

    public class Outer
    {
        public string Echo(string text) => text;
        public class Inner
        {
#nullable disable
            public string Echo(string text) => text;
#nullable enable
        }
    }

    public class DisabledMethod
    {
#nullable disable
        public string Echo(string text) => text;
#nullable enable
        public string? Find(string text) => text;
    }

    public class Wrap<T>
    {
        public List<T?> Items;
        public T? Get(T value) => value;
    }

    public class StringList : List<string?> { }
}

#nullable disable
namespace Nrt
{
    public static class Oblivious
    {
        public static string Echo(string text) => text;
        public static void Take(string text) { }
    }
}
"

        <Fact>
        Public Sub ImportedSymbols_MatchStandardMetadata()
            Dim compilation = ImportLibrary()
            compilation.VerifyDiagnostics()

            Dim api = compilation.GetTypeByMetadataName("Nrt.Api")
            Dim echo = DirectCast(api.GetMembers("Echo").Single(), IMethodSymbol)
            Dim find = DirectCast(api.GetMembers("Find").Single(), IMethodSymbol)
            Dim mixed = DirectCast(api.GetMembers("Mixed").Single(), IMethodSymbol)
            Dim take = DirectCast(api.GetMembers("Take").Single(), IMethodSymbol)
            Dim takeMaybe = DirectCast(api.GetMembers("TakeMaybe").Single(), IMethodSymbol)
            Dim write = DirectCast(api.GetMembers("Write").Single(), IMethodSymbol)
            Dim produce = DirectCast(api.GetMembers("Produce").Single(), IMethodSymbol)

            Assert.Equal(NullableAnnotation.NotAnnotated, echo.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, echo.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, find.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, find.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, mixed.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, mixed.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, take.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, take.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, takeMaybe.Parameters(0).NullableAnnotation)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.Ref, write.Parameters(0).RefKind)
            Assert.Equal(NullableAnnotation.NotAnnotated, write.Parameters(0).NullableAnnotation)
            Dim produced = DirectCast(produce.Parameters(0), ParameterSymbol)
            Assert.True(produced.IsByRef)
            Assert.True(produced.IsMetadataOut)
            Assert.Equal(NullableAnnotation.Annotated, produce.Parameters(0).NullableAnnotation)
            Assert.Equal(SpecialType.System_String, echo.ReturnType.SpecialType)
            Assert.True(echo.ReturnType.Equals(find.ReturnType))
            Assert.NotEqual(echo.ReturnNullableAnnotation, find.ReturnNullableAnnotation)

            Dim contracts = compilation.GetTypeByMetadataName("Nrt.Contracts")
            AssertField(contracts, "NullableItems", NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
            AssertField(contracts, "PlainItems", NullableAnnotation.NotAnnotated, NullableAnnotation.NotAnnotated)
            AssertField(contracts, "NullableList", NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertField(contracts, "Both", NullableAnnotation.Annotated, NullableAnnotation.Annotated)
            Assert.True(FieldType(contracts, "NullableItems").Equals(FieldType(contracts, "PlainItems")))

            Dim map = DirectCast(FieldType(contracts, "Map"), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, map.TypeArgumentNullableAnnotations(0))
            Assert.Equal(NullableAnnotation.Annotated, map.TypeArgumentNullableAnnotations(1))

            Dim nested = DirectCast(FieldType(contracts, "Nested"), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, nested.TypeArgumentNullableAnnotations(0))
            Assert.Equal(NullableAnnotation.Annotated, nested.TypeArgumentNullableAnnotations(1))
            Dim nestedList = DirectCast(nested.TypeArguments(1), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, nestedList.TypeArgumentNullableAnnotations(0))

            Dim sequence = DirectCast(contracts.GetMembers("Sequence").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, sequence.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(sequence.ReturnType, INamedTypeSymbol).TypeArgumentNullableAnnotations(0))

            AssertArray(contracts, "NullableElements", NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, rank:=1)
            AssertArray(contracts, "NullableArray", NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, rank:=1)
            AssertArray(contracts, "BothArrays", NullableAnnotation.Annotated, NullableAnnotation.Annotated, rank:=1)
            AssertArray(contracts, "Multi", NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, rank:=2)

            Dim jagged = DirectCast(FieldType(contracts, "Jagged"), IArrayTypeSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(contracts.GetMembers("Jagged").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, jagged.ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(jagged.ElementType, IArrayTypeSymbol).ElementNullableAnnotation)

            Assert.Equal(NullableAnnotation.Annotated, DirectCast(contracts.GetMembers("Field").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(contracts.GetMembers("PlainField").Single(), IFieldSymbol).NullableAnnotation)

            Dim name = DirectCast(contracts.GetMembers("Name").Single(), IPropertySymbol)
            Assert.Equal(NullableAnnotation.Annotated, name.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, name.GetMethod.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, name.SetMethod.Parameters.Last().NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(contracts.GetMembers("PlainName").Single(), IPropertySymbol).NullableAnnotation)

            Dim indexer = contracts.GetMembers().OfType(Of IPropertySymbol)().Single(Function(candidate) candidate.IsIndexer)
            Assert.Equal(NullableAnnotation.Annotated, indexer.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, indexer.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, indexer.GetMethod.ReturnNullableAnnotation)

            Dim classGet = DirectCast(contracts.GetMembers("ClassGet").Single(), IMethodSymbol)
            Assert.Equal(TypeKind.TypeParameter, classGet.ReturnType.TypeKind)
            Assert.Equal(NullableAnnotation.Annotated, classGet.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, classGet.Parameters(0).NullableAnnotation)

            Dim structGet = DirectCast(contracts.GetMembers("StructGet").Single(), IMethodSymbol)
            Assert.Equal(SpecialType.System_Nullable_T, structGet.ReturnType.OriginalDefinition.SpecialType)
            Assert.Equal(NullableAnnotation.None, structGet.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, structGet.Parameters(0).NullableAnnotation)

            Dim openGet = DirectCast(contracts.GetMembers("OpenGet").Single(), IMethodSymbol)
            Assert.Equal(TypeKind.TypeParameter, openGet.ReturnType.TypeKind)
            Assert.Equal(NullableAnnotation.Annotated, openGet.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, openGet.Parameters(0).NullableAnnotation)
            Assert.NotEqual(SpecialType.System_Nullable_T, openGet.ReturnType.OriginalDefinition.SpecialType)

            Assert.Equal(NullableAnnotation.Annotated, DirectCast(contracts.GetMembers("ClassBound").Single(), IMethodSymbol).TypeParameters(0).ReferenceTypeConstraintNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(contracts.GetMembers("ClassNonNull").Single(), IMethodSymbol).TypeParameters(0).ReferenceTypeConstraintNullableAnnotation)

            Dim compared = DirectCast(contracts.GetMembers("Compared").Single(), IMethodSymbol).TypeParameters(0)
            Dim constraint = DirectCast(compared.ConstraintTypes(0), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, compared.ConstraintNullableAnnotations(0))
            Assert.Equal(NullableAnnotation.Annotated, constraint.TypeArgumentNullableAnnotations(0))

            Assert.Equal(NullableAnnotation.Annotated, DirectCast(contracts.GetMembers("VirtualFind").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Dim derived = compilation.GetTypeByMetadataName("Nrt.Derived")
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(derived.GetMembers("VirtualFind").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Dim box = compilation.GetTypeByMetadataName("Nrt.StringBox")
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(box.GetMembers("Value").Single(), IPropertySymbol).NullableAnnotation)
            Dim implemented = DirectCast(box.Interfaces(0), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, implemented.TypeArgumentNullableAnnotations(0))

            Dim invoke = DirectCast(compilation.GetTypeByMetadataName("Nrt.Handler").DelegateInvokeMethod, IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, invoke.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, invoke.Parameters(0).NullableAnnotation)

            Dim outer = compilation.GetTypeByMetadataName("Nrt.Outer")
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(outer.GetMembers("Echo").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Dim inner = compilation.GetTypeByMetadataName("Nrt.Outer+Inner")
            Assert.Equal(NullableAnnotation.None, DirectCast(inner.GetMembers("Echo").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(inner.GetMembers("Echo").Single(), IMethodSymbol).Parameters(0).NullableAnnotation)

            Dim disabled = compilation.GetTypeByMetadataName("Nrt.DisabledMethod")
            Assert.Equal(NullableAnnotation.None, DirectCast(disabled.GetMembers("Echo").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(disabled.GetMembers("Find").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(disabled.GetMembers("Find").Single(), IMethodSymbol).Parameters(0).NullableAnnotation)

            Dim oblivious = compilation.GetTypeByMetadataName("Nrt.Oblivious")
            Assert.Equal(NullableAnnotation.None, DirectCast(oblivious.GetMembers("Echo").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(oblivious.GetMembers("Echo").Single(), IMethodSymbol).Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(oblivious.GetMembers("Take").Single(), IMethodSymbol).Parameters(0).NullableAnnotation)

            Dim stringList = compilation.GetTypeByMetadataName("Nrt.StringList")
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(stringList.BaseType, INamedTypeSymbol).TypeArgumentNullableAnnotations(0))

            Dim wrap = compilation.GetTypeByMetadataName("Nrt.Wrap`1")
            Dim ofString = wrap.Construct(compilation.GetSpecialType(SpecialType.System_String))
            Dim items = DirectCast(ofString.GetMembers("Items").Single(), IFieldSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, items.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(items.Type, INamedTypeSymbol).TypeArgumentNullableAnnotations(0))
            Dim substituted = DirectCast(ofString.GetMembers("Get").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, substituted.ReturnNullableAnnotation)
            Assert.Equal(SpecialType.System_String, substituted.ReturnType.SpecialType)
            Assert.Equal(NullableAnnotation.NotAnnotated, substituted.Parameters(0).NullableAnnotation)
            Assert.Equal(SpecialType.System_String, substituted.Parameters(0).Type.SpecialType)
        End Sub

        <Fact>
        Public Sub ImportedContracts_DriveExistingWarnings_WhenEnabled()
            Dim reference = CompileCSharp(LibrarySource, TargetFramework.Mscorlib40)
            Dim compilation = CreateCompilation(
<compilation>
    <file name="a.vb"><![CDATA[
Class Program
    Shared Sub Run()
        Dim plain As String = Nothing
        Dim marked As String? = Nothing
        Dim known As String = "A"
        Dim fromFind As String = Nrt.Api.Find(Nothing)
        Dim fromEcho As String = Nrt.Api.Echo(known)
        Nrt.Api.Take(Nothing)
        Nrt.Api.TakeMaybe(Nothing)
        Dim fromOblivious As String = Nrt.Oblivious.Echo(known)
        Nrt.Oblivious.Take(Nothing)
    End Sub
End Class
]]></file>
</compilation>, references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)

            Dim warnings = NullableWarnings(compilation)
            Assert.True(warnings.Length = 3, String.Join(Environment.NewLine, compilation.GetDiagnostics().Select(Function(diagnostic) diagnostic.ToString())))
            Assert.Contains(warnings, Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullAssignment AndAlso diagnostic.GetMessage().Contains("plain"))
            Assert.Contains(warnings, Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullAssignment AndAlso diagnostic.GetMessage().Contains("fromFind"))
            Assert.Contains(warnings, Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullArgument AndAlso diagnostic.GetMessage().Contains("text"))
        End Sub

        <Fact>
        Public Sub LanguageVersionAndNullableMode_DoNotInventWarnings()
            Dim reference = CompileCSharp(LibrarySource, TargetFramework.Mscorlib40)
            Dim source =
<compilation>
    <file name="a.vb"><![CDATA[
Class Program
    Shared Sub Run()
        Dim fromFind As String = Nrt.Api.Find(Nothing)
        Nrt.Api.Take(Nothing)
    End Sub
End Class
]]></file>
</compilation>

            AssertNoNullableWarnings(reference, source, TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), Vb17)
            AssertNoNullableWarnings(reference, source, TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), TestOptions.Regular.WithLanguageVersion(LanguageVersion.Default))
            AssertNoNullableWarnings(reference, source, TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), TestOptions.Regular.WithLanguageVersion(LanguageVersion.Latest))
            AssertNoNullableWarnings(reference, source, TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable), VbNext)
            AssertNoNullableWarnings(reference, source, TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Annotations), VbNext)

            Dim warningsOnly = CreateCompilation(source, references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Warnings), parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            Dim warnings = NullableWarnings(warningsOnly)
            Assert.DoesNotContain(warnings, Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullAssignment)
            Assert.Contains(warnings, Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullArgument)

            Dim legacySyntax = CreateCompilation("
Module Program
    Function Find(text As String?) As String?
        Return text
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17)
            Assert.Contains(legacySyntax.GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.ERR_BadTypeArgForStructConstraintNull)

            Dim imported = ImportLibrary(NullableContextOptions.Disable, LanguageVersion.VisualBasic17_13)
            Dim find = DirectCast(imported.GetTypeByMetadataName("Nrt.Api").GetMembers("Find").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, find.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, find.Parameters(0).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub FrameworkReferences_DecodeEmbeddedAndDefinedAttributes()
            Const source = "
#nullable enable
public class N
{
    public string? Find(string text) => text;
    public System.Collections.Generic.List<string?> Items;
}
"
            AssertSimpleImport(source, TargetFramework.NetFramework)
            AssertSimpleImport(source, TargetFramework.Net100)
        End Sub

        <Fact>
        Public Sub NullablePublicOnly_LimitsImportedMembers()
            Const source = "
#nullable enable
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo(""Friend"")]
public class Pub
{
    public string? PublicField;
    internal string? InternalField;
    private string? PrivateField;
}
"
            Dim parse = CSharpPreview.WithFeatures({New KeyValuePair(Of String, String)("nullablePublicOnly", "true")})
            Dim reference = CompileCSharp(source, TargetFramework.Mscorlib40, parse)
            Dim compilation = CreateCompilation("", references:={reference}, options:=TestOptions.ReleaseDll.WithMetadataImportOptions(MetadataImportOptions.All), parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40, assemblyName:="Friend")
            Dim pub = compilation.GetTypeByMetadataName("Pub")
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(pub.GetMembers("PublicField").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(pub.GetMembers("InternalField").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(pub.GetMembers("PrivateField").Single(), IFieldSymbol).NullableAnnotation)

            Dim publicOnly = CompileCSharp(source.Replace("[assembly: InternalsVisibleTo(""Friend"")]", ""), TargetFramework.Mscorlib40, parse)
            Dim withoutInternals = CreateCompilation("", references:={publicOnly}, options:=TestOptions.ReleaseDll.WithMetadataImportOptions(MetadataImportOptions.All), parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            Dim hidden = withoutInternals.GetTypeByMetadataName("Pub")
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(hidden.GetMembers("PublicField").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(hidden.GetMembers("InternalField").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(hidden.GetMembers("PrivateField").Single(), IFieldSymbol).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub MalformedMetadata_StaysObliviousWithoutChangingTypeIdentity()
            Dim il = "
.class public auto ansi beforefieldinit System.Runtime.CompilerServices.NullableAttribute extends [mscorlib]System.Attribute
{
  .method public hidebysig specialname rtspecialname instance void .ctor(uint8) cil managed { ret }
  .method public hidebysig specialname rtspecialname instance void .ctor(uint8[]) cil managed { ret }
}
.class public auto ansi beforefieldinit System.Runtime.CompilerServices.NullableContextAttribute extends [mscorlib]System.Attribute
{
  .method public hidebysig specialname rtspecialname instance void .ctor(uint8) cil managed { ret }
}
.module NullableModule
.custom instance void System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = ( 01 00 01 00 00 )
.class public auto ansi beforefieldinit BadSignatures
{
  .method public hidebysig instance string BadByte() cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8[]) = ( 01 00 01 00 00 00 09 00 00 )
    ldnull
    ret
  }
  .method public hidebysig instance string ExtraByte() cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8[]) = ( 01 00 02 00 00 00 01 01 00 00 )
    ldnull
    ret
  }
  .method public hidebysig instance class [mscorlib]System.Collections.Generic.List`1<string> Short() cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8[]) = ( 01 00 01 00 00 00 01 00 00 )
    ldnull
    ret
  }
  .method public hidebysig instance string SingleByte() cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = ( 01 00 02 00 00 )
    ldnull
    ret
  }
  .method public hidebysig instance string ExplicitOblivious(string x) cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = ( 01 00 02 00 00 )
    .param [1]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = ( 01 00 00 00 00 )
    .custom instance void System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = ( 01 00 01 00 00 )
    ldnull
    ret
  }
  .method public hidebysig instance string ModuleContextIgnored(string x) cil managed
  {
    ldnull
    ret
  }
}
"
            Dim reference = CompileIL(il)
            Dim compilation = CreateCompilation("", references:={reference}, parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            compilation.VerifyDiagnostics()
            Dim type = compilation.GetTypeByMetadataName("BadSignatures")

            Dim bad = DirectCast(type.GetMembers("BadByte").Single(), IMethodSymbol)
            Assert.Equal(SpecialType.System_String, bad.ReturnType.SpecialType)
            Assert.Equal(NullableAnnotation.None, bad.ReturnNullableAnnotation)

            Dim extra = DirectCast(type.GetMembers("ExtraByte").Single(), IMethodSymbol)
            Assert.Equal(SpecialType.System_String, extra.ReturnType.SpecialType)
            Assert.Equal(NullableAnnotation.None, extra.ReturnNullableAnnotation)

            Dim shortMethod = DirectCast(type.GetMembers("Short").Single(), IMethodSymbol)
            Assert.Equal("System.Collections.Generic.List(Of String)", shortMethod.ReturnType.ToDisplayString())
            Assert.Equal(NullableAnnotation.None, shortMethod.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(shortMethod.ReturnType, INamedTypeSymbol).TypeArgumentNullableAnnotations(0))

            Dim singleByte = DirectCast(type.GetMembers("SingleByte").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, singleByte.ReturnNullableAnnotation)

            Dim oblivious = DirectCast(type.GetMembers("ExplicitOblivious").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, oblivious.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, oblivious.Parameters(0).NullableAnnotation)

            Dim ignored = DirectCast(type.GetMembers("ModuleContextIgnored").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.None, ignored.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, ignored.Parameters(0).NullableAnnotation)
        End Sub

        <ConditionalFact(GetType(ExternalLc1AContractEvidence))>
        Public Sub PrebuiltContracts_DecodeAcrossFrameworks_WhenEvidenceIsPresent()
            Dim root = ResolvePrebuiltContractDirectory()
            If Not IO.Directory.Exists(root) Then
                Assert.Fail("VBNEXT_LC1A_CONTRACTS does not identify a contract image directory. This is not a successful framework acceptance.")
            End If

            AssertPrebuiltContract(IO.Path.Combine(root, "net47-CsContract.dll"), FrameworkPack("microsoft.netframework.referenceassemblies.net47", "v4.7"))
            AssertPrebuiltContract(IO.Path.Combine(root, "net472-CsContract.dll"), FrameworkPack("microsoft.netframework.referenceassemblies.net472", "v4.7.2"))
            AssertPrebuiltContract(IO.Path.Combine(root, "net48-CsContract.dll"), FrameworkPack("microsoft.netframework.referenceassemblies.net48", "v4.8"))
            AssertPrebuiltContract(IO.Path.Combine(root, "net10-CsContract.dll"), TargetFrameworkUtil.GetReferences(TargetFramework.Net100))

            ' Net110 reference assemblies are not part of this test project. The net11 contract uses the same
            ' BCL surface as net10, so it is read against the net10 references already present.
            AssertPrebuiltContract(IO.Path.Combine(root, "net11-CsContract.dll"), TargetFrameworkUtil.GetReferences(TargetFramework.Net100))
        End Sub

        <Fact>
        Public Sub LegacyEmitter_UniformContextIsRead_FlatMethodArrayIsNot()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class NullableAttribute
        Inherits Attribute
        Public Sub New(flag As Byte)
        End Sub
        Public Sub New(flags As Byte())
        End Sub
    End Class
    Public Class NullableContextAttribute
        Inherits Attribute
        Public Sub New(flag As Byte)
        End Sub
    End Class
End Namespace
Module Program
    Function Find(text As String?) As String?
        Return text
    End Function
    Function Mix(text As String) As String?
        Return text
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim image = compilation.EmitToImageReference()
            Dim consumer = CreateCompilation("", references:={image}, parseOptions:=VbNext)
            Dim find = DirectCast(consumer.GetTypeByMetadataName("Program").GetMembers("Find").Single(), IMethodSymbol)
            Dim mix = DirectCast(consumer.GetTypeByMetadataName("Program").GetMembers("Mix").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, find.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, find.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, mix.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, mix.Parameters(0).NullableAnnotation)
            Assert.DoesNotContain(mix.GetAttributes(), Function(attribute) attribute.AttributeClass.Name = "NullableAttribute")
        End Sub

        Private Shared Function FrameworkPack(packageName As String, versionFolder As String) As IEnumerable(Of MetadataReference)
            Dim directory = IO.Path.Combine("D:\Development\VBNext\Caches\NuGet\packages", packageName, "1.0.3", "build", ".NETFramework", versionFolder)
            Return {
                MetadataReference.CreateFromFile(IO.Path.Combine(directory, "mscorlib.dll")),
                MetadataReference.CreateFromFile(IO.Path.Combine(directory, "System.dll")),
                MetadataReference.CreateFromFile(IO.Path.Combine(directory, "System.Core.dll"))
            }
        End Function

        Private Sub AssertPrebuiltContract(contract As String, references As IEnumerable(Of MetadataReference))
            Assert.True(IO.File.Exists(contract), contract)
            Dim compilation = CreateEmptyCompilation("", references.Concat({MetadataReference.CreateFromFile(contract)}))
            Dim type = compilation.GetTypeByMetadataName("Contract")
            Assert.NotNull(type)
            Dim echo = DirectCast(type.GetMembers("Echo").Single(), IMethodSymbol)
            Dim find = DirectCast(type.GetMembers("Find").Single(), IMethodSymbol)
            Dim mixed = DirectCast(type.GetMembers("Mixed").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, echo.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, echo.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, find.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, find.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, mixed.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, mixed.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(type.GetMembers("Field").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(type.GetMembers("FieldN").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(type.GetMembers("PropN").Single(), IPropertySymbol).NullableAnnotation)
            Dim nested = DirectCast(type.GetMembers("Nested").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(nested.ReturnType, INamedTypeSymbol).TypeArgumentNullableAnnotations(0))
            Dim maybe = DirectCast(type.GetMembers("Maybe").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, maybe.ReturnNullableAnnotation)
            Assert.Equal(TypeKind.TypeParameter, maybe.ReturnType.TypeKind)
        End Sub

        Private Shared Sub AssertField(container As INamedTypeSymbol, name As String, fieldAnnotation As NullableAnnotation, argumentAnnotation As NullableAnnotation)
            Dim field = DirectCast(container.GetMembers(name).Single(), IFieldSymbol)
            Assert.Equal(fieldAnnotation, field.NullableAnnotation)
            Assert.Equal(argumentAnnotation, DirectCast(field.Type, INamedTypeSymbol).TypeArgumentNullableAnnotations(0))
        End Sub

        Private Shared Sub AssertArray(container As INamedTypeSymbol, name As String, fieldAnnotation As NullableAnnotation, elementAnnotation As NullableAnnotation, rank As Integer)
            Dim field = DirectCast(container.GetMembers(name).Single(), IFieldSymbol)
            Dim array = DirectCast(field.Type, IArrayTypeSymbol)
            Assert.Equal(fieldAnnotation, field.NullableAnnotation)
            Assert.Equal(elementAnnotation, array.ElementNullableAnnotation)
            Assert.Equal(rank, array.Rank)
        End Sub

        Private Shared Function FieldType(container As INamedTypeSymbol, name As String) As ITypeSymbol
            Return DirectCast(container.GetMembers(name).Single(), IFieldSymbol).Type
        End Function

        Private Function ImportLibrary(Optional nullable As NullableContextOptions = NullableContextOptions.Disable, Optional language As LanguageVersion = LanguageVersion.VbNext) As VisualBasicCompilation
            Dim reference = CompileCSharp(LibrarySource, TargetFramework.Mscorlib40)
            Return CreateCompilation("", references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(nullable), parseOptions:=TestOptions.Regular.WithLanguageVersion(language), targetFramework:=TargetFramework.Mscorlib40)
        End Function

        Private Function CompileCSharp(source As String, target As TargetFramework, Optional parseOptions As CSharpParseOptions = Nothing) As MetadataReference
            If parseOptions Is Nothing Then
                parseOptions = CSharpPreview
            End If

            Dim compilation = CreateCSharpCompilation(
                source,
                parseOptions:=parseOptions,
                compilationOptions:=CSharpNullable,
                referencedAssemblies:=TargetFrameworkUtil.GetReferences(target))
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
            Return compilation.EmitToImageReference()
        End Function

        Private Sub AssertSimpleImport(source As String, target As TargetFramework)
            Dim reference = CompileCSharp(source, target)
            Dim compilation = CreateCompilation("", references:={reference}, parseOptions:=VbNext, targetFramework:=target)
            compilation.VerifyDiagnostics()
            Dim type = compilation.GetTypeByMetadataName("N")
            Dim find = DirectCast(type.GetMembers("Find").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, find.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, find.Parameters(0).NullableAnnotation)
            AssertField(type, "Items", NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
        End Sub

        Private Sub AssertNoNullableWarnings(reference As MetadataReference, source As BasicTestSource, options As VisualBasicCompilationOptions, parseOptions As VisualBasicParseOptions)
            Dim compilation = CreateCompilation(source, references:={reference}, options:=options, parseOptions:=parseOptions, targetFramework:=TargetFramework.Mscorlib40)
            Assert.Empty(NullableWarnings(compilation))
        End Sub

        Private Shared Function NullableWarnings(compilation As VisualBasicCompilation) As Diagnostic()
            Return compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullAssignment OrElse diagnostic.Code = ERRID.WRN_PossibleNullArgument OrElse diagnostic.Code = ERRID.WRN_PossibleNullReturn OrElse diagnostic.Code = ERRID.WRN_PossibleNullReference).ToArray()
        End Function

        Friend Shared Function ResolvePrebuiltContractDirectory() As String
            Dim configured = Environment.GetEnvironmentVariable("VBNEXT_LC1A_CONTRACTS")
            If Not String.IsNullOrWhiteSpace(configured) Then
                Return configured
            End If

            Return "D:\Development\VBNext\Evidence\LanguageCompletion\LC-1A\out"
        End Function

        Friend Shared Function ShouldRunPrebuiltContracts() As Boolean
            If Not String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VBNEXT_LC1A_CONTRACTS")) Then
                Return True
            End If

            Return IO.Directory.Exists("D:\Development\VBNext\Evidence\LanguageCompletion\LC-1A\out")
        End Function

    End Class

    ''' <summary>
    ''' The prebuilt framework images are external acceptance evidence. Their absence skips the
    ''' ordinary compiler suite. A skip is not framework acceptance, and an explicitly configured
    ''' missing directory still fails inside the test.
    ''' </summary>
    Public Class ExternalLc1AContractEvidence
        Inherits ExecutionCondition

        Public Overrides ReadOnly Property ShouldSkip As Boolean
            Get
                Return Not NullableMetadataImportTests.ShouldRunPrebuiltContracts()
            End Get
        End Property

        Public Overrides ReadOnly Property SkipReason As String
            Get
                Return "External LC-1A framework contract images are not configured. A skip is not framework acceptance."
            End Get
        End Property
    End Class

End Namespace
