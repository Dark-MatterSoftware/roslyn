' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    ''' <summary>
    ''' Cross-language facts the earlier slice filters did not state directly.
    ''' Long-tuple round trip stays in NullableMetadataRoundTripTests.
    ''' </summary>
    Public Class NullableConsolidatedCrossLanguageTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Enabled As VisualBasicCompilationOptions = TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        <Fact>
        Public Sub CSharpImplementsVbNullableInterface()
            Dim visualBasic = CreateCompilation("
Public Interface IReader
    Function Read(value As String?) As String?
End Interface
", options:=Enabled, parseOptions:=VbNext)
            Assert.Empty(Errors(visualBasic))
            Dim interfaceReference = visualBasic.EmitToImageReference()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
public class Reader : IReader
{
    public string? Read(string? value) => value;
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=Net100(interfaceReference))
            Assert.Empty(Errors(csharp))
            Dim implemented = DirectCast(csharp.GetTypeByMetadataName("Reader").GetMembers("Read").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, implemented.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, implemented.Parameters.Single().NullableAnnotation)

            Dim consumer = CreateCompilation("
Public Module Use
    Public Function Go(reader As Reader) As String
        Return reader.Read(Nothing)
    End Function
End Module
", options:=Enabled, parseOptions:=VbNext, references:={interfaceReference, csharp.EmitToImageReference()})
            Assert.Empty(Errors(consumer))
            Assert.Contains(consumer.GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullReturn)
            Dim read = DirectCast(consumer.GetTypeByMetadataName("Reader").GetMembers("Read").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, read.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, read.Parameters.Single().NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ImportedGenericMethod_ExposesConstructedTypeArgumentAnnotation()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
public class Api
{
    public T Echo<T>(T value) => value;
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=Net100())
            Assert.Empty(Errors(csharp))
            Dim visualBasic = CreateCompilation("", options:=Enabled, parseOptions:=VbNext, references:={csharp.EmitToImageReference()})
            Dim echo = DirectCast(visualBasic.GetTypeByMetadataName("Api").GetMembers("Echo").Single(), IMethodSymbol)
            Dim text = DirectCast(visualBasic.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim constructed = echo.Construct(ImmutableArray.Create(Of ITypeSymbol)(text.WithNullableAnnotation(NullableAnnotation.Annotated)), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.Equal(NullableAnnotation.Annotated, constructed.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, constructed.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, echo.TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub ReducedExtension_CrossLanguageReceiverAnnotation()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
public static class TextExtensions
{
    public static string? Mark(this string? text) => text;
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=Net100())
            Assert.Empty(Errors(csharp))
            Dim visualBasic = CreateCompilation("", options:=Enabled, parseOptions:=VbNext, references:={csharp.EmitToImageReference()})
            Dim definition = DirectCast(visualBasic.GetTypeByMetadataName("TextExtensions").GetMembers("Mark").Single(), IMethodSymbol)
            Dim text = DirectCast(visualBasic.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim reduced = definition.ReduceExtensionMethod(text.WithNullableAnnotation(NullableAnnotation.Annotated))
            Assert.NotNull(reduced)
            Assert.Equal(NullableAnnotation.Annotated, reduced.ReceiverNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, reduced.ReturnNullableAnnotation)

            Dim visualBasicLibrary = CreateCompilation("
Imports System.Runtime.CompilerServices
Public Module VbExtensions
    <Extension>
    Public Function Mark(text As String?) As String?
        Return text
    End Function
End Module
", options:=Enabled, parseOptions:=VbNext)
            Assert.Empty(Errors(visualBasicLibrary))
            Dim csharpConsumer = CreateCSharpCompilation("", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=Net100(visualBasicLibrary.EmitToImageReference()))
            Dim vbDefinition = DirectCast(csharpConsumer.GetTypeByMetadataName("VbExtensions").GetMembers("Mark").Single(), IMethodSymbol)
            Dim csharpText = DirectCast(csharpConsumer.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim csharpReduced = vbDefinition.ReduceExtensionMethod(csharpText.WithNullableAnnotation(NullableAnnotation.Annotated))
            Assert.NotNull(csharpReduced)
            Assert.Equal(NullableAnnotation.Annotated, csharpReduced.ReceiverNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, csharpReduced.ReturnNullableAnnotation)
        End Sub

        Private Shared Function Net100(ParamArray extra() As MetadataReference) As MetadataReference()
            Return TargetFrameworkUtil.GetReferences(TargetFramework.Net100).Concat(extra).ToArray()
        End Function

        Private Shared Function Errors(compilation As Compilation) As Diagnostic()
            Return compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
        End Function
    End Class
End Namespace
