' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Reflection.Metadata
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableMetadataCorrectnessCloseoutTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        <Fact>
        Public Sub IncompatibleNullableAttribute_FollowsOptionalCompilerContract()
            AssertOmitted("Public Sub New(text As String)")
            AssertOmitted("Public Sub New(flag As Byte)")
            AssertOmitted("Public Sub New(flags As Byte())")
            AssertOmitted("Private Sub New(flag As Byte)
        End Sub
        Public Sub New(text As String)")

            Dim wrongBase = EmitConsumer("
Namespace System.Runtime.CompilerServices
    Public Class NullableAttribute
        Public Sub New(flag As Byte)
        End Sub
        Public Sub New(flags As Byte())
        End Sub
    End Class
End Namespace
", ConsumerSource())
            Assert.False(wrongBase.HasErrors)
            Assert.False(DefinesType(wrongBase.Image, "System.Runtime.CompilerServices.NullableAttribute"))

            Dim badContext = EmitConsumer("
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
        Public Sub New(text As String)
        End Sub
    End Class
End Namespace
", ConsumerSource())
            Assert.False(badContext.HasErrors)
            Assert.Equal({CByte(2)}, ReadNullableFlags(badContext.Image, "Api", "Find"))
            Assert.Equal(NullableAnnotation.Annotated, ImportedReturn(badContext.Image))

            Dim compatible = EmitConsumer("
Namespace System.Runtime.CompilerServices
    Public Class NullableAttribute
        Inherits Attribute
        Public Sub New(flag As Byte)
        End Sub
        Public Sub New(flags As Byte())
        End Sub
    End Class
End Namespace
", ConsumerSource())
            Assert.False(compatible.HasErrors)
            Assert.Equal({CByte(2)}, ReadNullableFlags(compatible.Image, "Api", "Find"))
            Assert.Equal({CByte(1), CByte(2)}, ReadNullableFlags(compatible.Image, "Api", "Nested"))
            Assert.Equal(NullableAnnotation.Annotated, ImportedReturn(compatible.Image))
            Assert.False(DefinesType(compatible.Image, "System.Runtime.CompilerServices.NullableAttribute"))

            Dim missing = CreateCompilation(ConsumerSource(), options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            Assert.Empty(missing.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error))
            Dim missingImage = missing.EmitToArray()
            Assert.True(DefinesType(missingImage, "System.Runtime.CompilerServices.NullableAttribute"))
            Assert.Equal(NullableAnnotation.Annotated, ImportedReturn(missingImage, TargetFramework.Mscorlib40))

            Dim csharp = CreateCSharpCompilation("
namespace System.Runtime.CompilerServices
{
    public class NullableAttribute : System.Attribute
    {
        public NullableAttribute(string text) { }
    }
}
public class Api
{
    public string? Find(string? text) => text;
    public System.Collections.Generic.List<string?> Nested() => null;
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net100).ToArray())
            Assert.Contains(csharp.GetDiagnostics(), Function(diagnostic) diagnostic.Id = "CS0656")
        End Sub

        <Fact>
        Public Sub RawMetadata_MatchesTheStandardNullableWalk()
            Dim source = "
Imports System.Collections.Generic
Public Delegate Sub MessageHandler(Of T)(message As T)
Public Class Outer(Of T)
    Public Class Inner(Of U)
    End Class
End Class
Public Class Api
    Public Eight As (String?, String, String?, String, String?, String, String?, String)
    Public Fifteen As (String?, String, String?, String, String?, String, String?, String, String?, String, String?, String, String?, String, String?)
    Public Values As (Integer, Integer?, String?)
    Public Nested As Outer(Of String?).Inner(Of String)
    Public Items As List(Of String?)()
    Public Event MessageReceived As MessageHandler(Of String?)
End Class
"
            Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.Empty(compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error))
            Dim image = compilation.EmitToArray()
            Assert.Equal({CByte(0), CByte(2), CByte(1), CByte(2), CByte(1), CByte(2), CByte(1), CByte(2), CByte(0), CByte(1)}, ReadNullableFlags(image, "Api", "Eight"))
            Assert.Equal({CByte(0), CByte(2), CByte(1), CByte(2), CByte(1), CByte(2), CByte(1), CByte(2), CByte(0), CByte(1), CByte(2), CByte(1), CByte(2), CByte(1), CByte(2), CByte(1), CByte(0), CByte(2)}, ReadNullableFlags(image, "Api", "Fifteen"))
            Assert.Equal({CByte(0), CByte(2)}, ReadNullableFlags(image, "Api", "Values"))
            Assert.Equal({CByte(1), CByte(2), CByte(1)}, ReadNullableFlags(image, "Api", "Nested"))
            Assert.Equal({CByte(1), CByte(1), CByte(2)}, ReadNullableFlags(image, "Api", "Items"))
            Assert.Equal({CByte(0), CByte(2)}, ReadNullableFlags(image, "Api", "MessageReceived"))
            Assert.Equal(0, ReadContext(image, "Api"))

            Dim imported = CreateCompilation("", references:={compilation.EmitToImageReference()}, parseOptions:=VbNext)
            Dim api = imported.GetTypeByMetadataName("Api")
            Dim eight = DirectCast(Field(api, "Eight").Type, NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, eight.TupleUnderlyingType.GetTypeArgumentNullableAnnotations()(0))
            Dim rest = DirectCast(eight.TupleUnderlyingType.TypeArgumentsNoUseSiteDiagnostics(7), NamedTypeSymbol)
            Dim restShape = If(rest.IsTupleType, rest.TupleUnderlyingType, rest)
            Assert.Equal(NullableAnnotation.NotAnnotated, restShape.GetTypeArgumentNullableAnnotations()(0))
            Dim nested = DirectCast(Field(api, "Nested").Type, NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, nested.ContainingType.GetTypeArgumentNullableAnnotations()(0))
            Assert.Equal(NullableAnnotation.NotAnnotated, nested.GetTypeArgumentNullableAnnotations()(0))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(Field(api, "Items").Type, ArrayTypeSymbol).ElementType, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.Annotated, Args(EventType(api, "MessageReceived")).Single())

            Dim csharp = CreateCSharpCompilation("
#nullable enable
using System.Collections.Generic;
public class Api
{
    public (string?, string, string?, string, string?, string, string?, string) Eight;
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net100).ToArray())
            Dim csharpImage = csharp.EmitToArray()
            Dim csharpImported = CreateCompilation("", references:={MetadataReference.CreateFromImage(csharpImage)}, parseOptions:=VbNext, targetFramework:=TargetFramework.Net100)
            Dim csharpEight = DirectCast(Field(csharpImported.GetTypeByMetadataName("Api"), "Eight").Type, NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, csharpEight.TupleUnderlyingType.GetTypeArgumentNullableAnnotations()(0))
            Dim csharpRest = DirectCast(csharpEight.TupleUnderlyingType.TypeArgumentsNoUseSiteDiagnostics(7), NamedTypeSymbol)
            Dim csharpRestShape = If(csharpRest.IsTupleType, csharpRest.TupleUnderlyingType, csharpRest)
            Assert.Equal(NullableAnnotation.NotAnnotated, csharpRestShape.GetTypeArgumentNullableAnnotations()(0))
        End Sub

        <Fact>
        Public Sub MalformedNullableMetadata_RecoversWithoutANonNullGuarantee()
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
.class public auto ansi beforefieldinit BadNullable
{
  .custom instance void System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = ( 01 00 01 00 00 )
  .method public hidebysig instance string EmptyArray() cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8[]) = ( 01 00 00 00 00 00 00 00 )
    ldnull
    ret
  }
  .method public hidebysig instance string InvalidFlag() cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8) = ( 01 00 09 00 00 )
    ldnull
    ret
  }
  .method public hidebysig instance string ShortBlob() cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8[]) = ( 01 00 )
    ldnull
    ret
  }
  .method public hidebysig instance valuetype [mscorlib]System.ValueTuple`2<string, string> BadTuple() cil managed
  {
    .param [0]
    .custom instance void System.Runtime.CompilerServices.NullableAttribute::.ctor(uint8[]) = ( 01 00 01 00 00 00 09 00 00 )
    ldnull
    ret
  }
}
.class public auto ansi beforefieldinit InvalidContext
{
  .custom instance void System.Runtime.CompilerServices.NullableContextAttribute::.ctor(uint8) = ( 01 00 09 00 00 )
  .method public hidebysig instance string Echo(string x) cil managed
  {
    ldnull
    ret
  }
}
"
            Dim reference = CompileIL(il)
            Dim compilation = CreateCompilation("", references:={reference}, parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            compilation.VerifyDiagnostics()
            Dim bad = compilation.GetTypeByMetadataName("BadNullable")
            AssertRecovered(bad, "EmptyArray", NullableAnnotation.None)
            AssertRecovered(bad, "InvalidFlag", NullableAnnotation.None)
            AssertRecovered(bad, "ShortBlob", NullableAnnotation.NotAnnotated)
            Dim tuple = DirectCast(bad.GetMembers("BadTuple").Single(), MethodSymbol)
            Assert.Equal("System.ValueTuple(Of String, String)", tuple.ReturnType.ToDisplayString())
            Assert.Equal(NullableAnnotation.None, tuple.GetReturnNullableAnnotation())
            Dim invalid = DirectCast(compilation.GetTypeByMetadataName("InvalidContext").GetMembers("Echo").Single(), MethodSymbol)
            Assert.Equal(SpecialType.System_String, invalid.ReturnType.SpecialType)
            Assert.Equal(NullableAnnotation.None, invalid.GetReturnNullableAnnotation())
        End Sub

        Private Sub AssertOmitted(constructor As String)
            Dim attribute = "
Namespace System.Runtime.CompilerServices
    Public Class NullableAttribute
        Inherits Attribute
        " & constructor & "
        End Sub
    End Class
End Namespace
"
            Dim reference = CreateCompilation(attribute, parseOptions:=VbNext).EmitToImageReference()
            Dim consumer = CreateCompilation(ConsumerSource(), references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim diagnostics = consumer.GetDiagnostics()
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.Code = ERRID.ERR_MissingRuntimeHelper)
        End Sub

        Private Shared Function ConsumerSource() As String
            Return "
Public Class Api
    Public Function Find(text As String?) As String?
        Return text
    End Function
    Public Function Nested() As System.Collections.Generic.List(Of String?)
        Return Nothing
    End Function
End Class
"
        End Function

        Private Function EmitConsumer(attributeSource As String, consumerSource As String) As (Image As ImmutableArray(Of Byte), HasErrors As Boolean)
            Dim reference = CreateCompilation(attributeSource, parseOptions:=VbNext).EmitToImageReference()
            Dim consumer = CreateCompilation(consumerSource, references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim errors = consumer.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Return (consumer.EmitToArray(), errors.Length > 0)
        End Function

        Private Function ImportedReturn(image As ImmutableArray(Of Byte), Optional target As TargetFramework = TargetFramework.Net100) As NullableAnnotation
            Dim imported = CreateCompilation("", references:={MetadataReference.CreateFromImage(image)}, parseOptions:=VbNext, targetFramework:=target)
            Dim method = DirectCast(imported.GetTypeByMetadataName("Api").GetMembers("Find").Single(), MethodSymbol)
            Return method.GetReturnNullableAnnotation()
        End Function

        Private Shared Sub AssertRecovered(type As INamedTypeSymbol, name As String, annotation As NullableAnnotation)
            Dim method = DirectCast(type.GetMembers(name).Single(), MethodSymbol)
            Assert.Equal(SpecialType.System_String, method.ReturnType.SpecialType)
            Assert.Equal(annotation, method.GetReturnNullableAnnotation())
        End Sub

        Private Shared Function Field(container As INamedTypeSymbol, name As String) As FieldSymbol
            Return DirectCast(container.GetMembers(name).Single(), FieldSymbol)
        End Function

        Private Shared Function EventType(container As INamedTypeSymbol, name As String) As TypeSymbol
            Return DirectCast(container.GetMembers(name).Single(), EventSymbol).Type
        End Function

        Private Shared Function Args(type As TypeSymbol) As ImmutableArray(Of NullableAnnotation)
            Return DirectCast(type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations()
        End Function

        Private Shared Function ReadNullableFlags(image As ImmutableArray(Of Byte), typeName As String, memberName As String) As Byte()
            Using metadata = ModuleMetadata.CreateFromImage(image)
                Dim reader = metadata.Module.GetMetadataReader()
                For Each typeHandle In reader.TypeDefinitions
                    Dim type = reader.GetTypeDefinition(typeHandle)
                    If reader.GetString(type.Name) <> typeName Then
                        Continue For
                    End If

                    Dim owner = FindMember(reader, type, memberName)
                    Return ReadFlags(reader, owner)
                Next
            End Using

            Return Array.Empty(Of Byte)()
        End Function

        Private Shared Function ReadContext(image As ImmutableArray(Of Byte), typeName As String) As Integer
            Using metadata = ModuleMetadata.CreateFromImage(image)
                Dim reader = metadata.Module.GetMetadataReader()
                For Each typeHandle In reader.TypeDefinitions
                    Dim type = reader.GetTypeDefinition(typeHandle)
                    If reader.GetString(type.Name) = typeName Then
                        Return ReadContextValue(reader, type.GetCustomAttributes())
                    End If
                Next
            End Using

            Return -1
        End Function

        Private Shared Function FindMember(reader As MetadataReader, type As TypeDefinition, memberName As String) As EntityHandle
            For Each handle In type.GetFields()
                If reader.GetString(reader.GetFieldDefinition(handle).Name) = memberName Then
                    Return handle
                End If
            Next

            For Each handle In type.GetEvents()
                If reader.GetString(reader.GetEventDefinition(handle).Name) = memberName Then
                    Return handle
                End If
            Next

            For Each handle In type.GetMethods()
                Dim method = reader.GetMethodDefinition(handle)
                If reader.GetString(method.Name) <> memberName Then
                    Continue For
                End If

                For Each parameterHandle In method.GetParameters()
                    Dim parameter = reader.GetParameter(parameterHandle)
                    If parameter.SequenceNumber = 0 Then
                        Return parameterHandle
                    End If
                Next
            Next

            Return Nothing
        End Function

        Private Shared Function ReadFlags(reader As MetadataReader, owner As EntityHandle) As Byte()
            If owner.IsNil Then
                Return Array.Empty(Of Byte)()
            End If

            Dim attributes As CustomAttributeHandleCollection
            If owner.Kind = HandleKind.FieldDefinition Then
                attributes = reader.GetFieldDefinition(CType(owner, FieldDefinitionHandle)).GetCustomAttributes()
            ElseIf owner.Kind = HandleKind.EventDefinition Then
                attributes = reader.GetEventDefinition(CType(owner, EventDefinitionHandle)).GetCustomAttributes()
            Else
                attributes = reader.GetParameter(CType(owner, ParameterHandle)).GetCustomAttributes()
            End If

            For Each attributeHandle In attributes
                Dim attribute = reader.GetCustomAttribute(attributeHandle)
                If Not IsNullableAttribute(reader, attribute.Constructor) Then
                    Continue For
                End If

                Dim readerBlob = reader.GetBlobReader(attribute.Value)
                If readerBlob.RemainingBytes < 3 OrElse readerBlob.ReadByte() <> 1 OrElse readerBlob.ReadByte() <> 0 Then
                    Continue For
                End If

                If AttributeConstructorTakesArray(reader, attribute.Constructor) Then
                    Dim length = readerBlob.ReadInt32()
                    Dim flags(length - 1) As Byte
                    For i = 0 To length - 1
                        flags(i) = readerBlob.ReadByte()
                    Next

                    Return flags
                End If

                Return {readerBlob.ReadByte()}
            Next

            Return Array.Empty(Of Byte)()
        End Function

        Private Shared Function ReadContextValue(reader As MetadataReader, attributes As CustomAttributeHandleCollection) As Integer
            For Each attributeHandle In attributes
                Dim attribute = reader.GetCustomAttribute(attributeHandle)
                Dim name = ConstructorTypeName(reader, attribute.Constructor)
                If name <> "NullableContextAttribute" Then
                    Continue For
                End If

                Dim blob = reader.GetBlobReader(attribute.Value)
                blob.ReadUInt16()
                Return blob.ReadByte()
            Next

            Return 0
        End Function

        Private Shared Function IsNullableAttribute(reader As MetadataReader, constructor As EntityHandle) As Boolean
            Return ConstructorTypeName(reader, constructor) = "NullableAttribute"
        End Function

        Private Shared Function AttributeConstructorTakesArray(reader As MetadataReader, constructor As EntityHandle) As Boolean
            Dim signature = reader.GetBlobReader(ConstructorSignature(reader, constructor))
            Dim convention = signature.ReadByte()
            If (convention And &H10) <> 0 Then
                signature.ReadCompressedInteger()
            End If

            If signature.ReadCompressedInteger() <> 1 Then
                Return False
            End If

            SkipSignatureType(signature)
            Return signature.ReadByte() = &H1D
        End Function

        Private Shared Sub SkipSignatureType(ByRef signature As BlobReader)
            Dim code = signature.ReadCompressedInteger()
            If code = &H1D OrElse code = &H1E Then
                SkipSignatureType(signature)
            End If
        End Sub

        Private Shared Function ConstructorSignature(reader As MetadataReader, constructor As EntityHandle) As BlobHandle
            If constructor.Kind = HandleKind.MethodDefinition Then
                Return reader.GetMethodDefinition(CType(constructor, MethodDefinitionHandle)).Signature
            End If

            Return reader.GetMemberReference(CType(constructor, MemberReferenceHandle)).Signature
        End Function

        Private Shared Function ConstructorTypeName(reader As MetadataReader, constructor As EntityHandle) As String
            Dim parent As EntityHandle
            If constructor.Kind = HandleKind.MethodDefinition Then
                parent = reader.GetMethodDefinition(CType(constructor, MethodDefinitionHandle)).GetDeclaringType()
                Return reader.GetString(reader.GetTypeDefinition(CType(parent, TypeDefinitionHandle)).Name)
            End If

            parent = reader.GetMemberReference(CType(constructor, MemberReferenceHandle)).Parent
            If parent.Kind = HandleKind.TypeReference Then
                Return reader.GetString(reader.GetTypeReference(CType(parent, TypeReferenceHandle)).Name)
            End If

            Return reader.GetString(reader.GetTypeDefinition(CType(parent, TypeDefinitionHandle)).Name)
        End Function

        Private Shared Function DefinesType(image As ImmutableArray(Of Byte), fullName As String) As Boolean
            Using metadata = ModuleMetadata.CreateFromImage(image)
                Dim reader = metadata.Module.GetMetadataReader()
                For Each handle In reader.TypeDefinitions
                    Dim type = reader.GetTypeDefinition(handle)
                    If reader.GetString(type.Namespace) & "." & reader.GetString(type.Name) = fullName Then
                        Return True
                    End If
                Next
            End Using

            Return False
        End Function

    End Class

End Namespace
