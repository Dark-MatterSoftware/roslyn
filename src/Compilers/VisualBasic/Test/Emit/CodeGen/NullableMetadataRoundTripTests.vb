' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Reflection.Metadata
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols.Retargeting
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableMetadataRoundTripTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        <Fact>
        Public Sub LongTuple_PreservesAlternatingElementAnnotations()
            Dim source = "
Public Class Api
    Public Two As (String?, String)
    Public Seven As (String?, String, String?, String, String?, String, String?)
    Public Eight As (String?, String, String?, String, String?, String, String?, String)
    Public Nine As (String, String?, String, String?, String, String?, String, String?, String)
    Public Fifteen As (String?, String, String?, String, String?, String, String?, String, String?, String, String?, String, String?, String, String?)
    Public Values As (Integer, Integer?, String?)
    Public Nested As (Item As (String?, Integer), Tail As String?)
    Public Function GetEight() As (String?, String, String?, String, String?, String, String?, String)
        Return Nothing
    End Function
    Public Sub TakeNine(value As (String, String?, String, String?, String, String?, String, String?, String))
    End Sub
    Public Property Item As (String?, String, String?, String, String?, String, String?, String)
End Class
"
            Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
            Dim sourceApi = compilation.GetTypeByMetadataName("Api")
            AssertLogical(Field(sourceApi, "Eight"), NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            Dim imported = CreateCompilation("", references:={compilation.EmitToImageReference()}, parseOptions:=VbNext)
            Dim api = imported.GetTypeByMetadataName("Api")
            AssertLogical(Field(api, "Two"), NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertLogical(Field(api, "Seven"), NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
            AssertLogical(Field(api, "Eight"), NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertLogical(DirectCast(api.GetMembers("GetEight").Single(), MethodSymbol).ReturnType, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertLogical(DirectCast(api.GetMembers("TakeNine").Single(), MethodSymbol).Parameters.Single().Type, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertLogical(DirectCast(api.GetMembers("Item").Single(), PropertySymbol).Type, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertLogical(Field(api, "Nine"), NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertLogical(Field(api, "Fifteen"), NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
            AssertLogical(Field(api, "Values").Type, NullableAnnotation.None, NullableAnnotation.None, NullableAnnotation.Annotated)
            Dim nested = DirectCast(Field(api, "Nested").Type, NamedTypeSymbol)
            Dim inner = DirectCast(nested.TupleElements(0).Type, NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, LogicalAnnotations(inner).First())
            Assert.Equal(NullableAnnotation.Annotated, LogicalAnnotations(nested).Last())
        End Sub

        <Fact>
        Public Sub NestedGenericsAndArrays_RoundTrip()
            Dim source = "
Imports System.Collections.Generic
Public Class Pair(Of T, U)
End Class
Public Class Api
    Public Items As List(Of String?)
    Public MaybeList As List(Of String)?
    Public Map As Dictionary(Of String, List(Of String?))
    Public Mixed As Pair(Of String?, String)
    Public Deep As Pair(Of Pair(Of String?, String), String())
    Public SingleArray As String?()
    Public Multi As String?(,)
    Public Jagged As String?()()
End Class
"
            Dim imported = ImportVb(source)
            Dim api = imported.GetTypeByMetadataName("Api")
            Assert.Equal(NullableAnnotation.Annotated, Args(Field(api, "Items").Type).Single())
            Assert.Equal(NullableAnnotation.Annotated, Field(api, "MaybeList").GetNullableAnnotation())
            Assert.Equal(NullableAnnotation.NotAnnotated, Args(Field(api, "MaybeList").Type).Single())
            Assert.Equal(NullableAnnotation.Annotated, Args(Field(api, "Mixed").Type)(0))
            Assert.Equal(NullableAnnotation.NotAnnotated, Args(Field(api, "Mixed").Type)(1))
            Dim map = DirectCast(Field(api, "Map").Type, NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, Args(map.TypeArgumentsNoUseSiteDiagnostics(1)).Single())
            Dim deep = DirectCast(Field(api, "Deep").Type, NamedTypeSymbol)
            Dim innerPair = DirectCast(deep.TypeArgumentsNoUseSiteDiagnostics(0), NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, Args(innerPair)(0))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(Field(api, "SingleArray").Type, ArrayTypeSymbol).ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(Field(api, "Multi").Type, ArrayTypeSymbol).ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(Field(api, "Jagged").Type, ArrayTypeSymbol).ElementType, ArrayTypeSymbol).ElementNullableAnnotation)
        End Sub

        <Fact>
        Public Sub EventType_RoundTripsTheDelegateArgument()
            Dim source = "
Public Delegate Sub MessageHandler(Of T)(message As T)
Public Class Box
    Public Event MessageReceived As MessageHandler(Of String?)
    Public Event Ready As MessageHandler(Of String)
    Public Custom Event CustomMessage As MessageHandler(Of String?)
        AddHandler(value As MessageHandler(Of String?))
        End AddHandler
        RemoveHandler(value As MessageHandler(Of String?))
        End RemoveHandler
        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim imported = ImportVb(source)
            Dim box = imported.GetTypeByMetadataName("Box")
            Assert.Equal(NullableAnnotation.Annotated, Args(EventType(box, "MessageReceived")).Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, Args(EventType(box, "Ready")).Single())
            Assert.Equal(NullableAnnotation.Annotated, Args(EventType(box, "CustomMessage")).Single())
            Assert.Equal(NullableAnnotation.None, DirectCast(box.GetMembers("MessageReceived").Single(), IEventSymbol).NullableAnnotation)
            Dim invoke = DirectCast(EventType(box, "MessageReceived"), NamedTypeSymbol).DelegateInvokeMethod
            Assert.Equal(NullableAnnotation.Annotated, invoke.Parameters.Single().GetDeclaredNullableAnnotation())
        End Sub

        <Fact>
        Public Sub ImportedClassConstraint_RetargetsANullableReferenceConstraint()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
public class Box
{
    public void NullableRef<T>() where T : class? {}
    public void NonNullRef<T>() where T : class {}
    public void Open<T>() {}
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable)
            Dim image = csharp.EmitToImageReference()
            Dim consumer = CreateCompilation("
Public Class Use
    Public Sub Go(value As Box)
    End Sub
End Class
", references:={image}, parseOptions:=VbNext)
            Dim box = consumer.GetTypeByMetadataName("Box")
            Assert.Equal(NullableAnnotation.Annotated, Constraint(box, "NullableRef"))
            Assert.Equal(NullableAnnotation.NotAnnotated, Constraint(box, "NonNullRef"))
            Assert.Equal(NullableAnnotation.None, Constraint(box, "Open"))

            Dim sourceAssembly = DirectCast(DirectCast(consumer.SourceModule, SourceModuleSymbol).ContainingAssembly, SourceAssemblySymbol)
            Dim retargeting = New RetargetingAssemblySymbol(sourceAssembly, isLinked:=False)
            retargeting.SetCorLibrary(sourceAssembly.CorLibrary)
            Dim useType = retargeting.GlobalNamespace.GetTypeMembers("Use").Single()
            Assert.IsType(Of RetargetingNamedTypeSymbol)(useType)
            Dim go = useType.GetMember(Of MethodSymbol)("Go")
            Assert.IsType(Of RetargetingMethodSymbol)(go)
            Dim parameterType = go.Parameters.Single().Type
            ' The referenced assembly symbol is unchanged, so the translator reuses the imported PE type.
            Assert.IsType(Of PENamedTypeSymbol)(parameterType)
            Assert.Equal(NullableAnnotation.Annotated, Constraint(parameterType, "NullableRef"))
            Assert.Equal(NullableAnnotation.NotAnnotated, Constraint(parameterType, "NonNullRef"))

            Dim bytes = csharp.EmitToArray()
            Dim first = MetadataReference.CreateFromImage(bytes)
            Dim second = MetadataReference.CreateFromImage(bytes)
            Dim library = CreateCompilation("
Public Class Use
    Public Sub Go(value As Box)
    End Sub
End Class
", references:={first}, parseOptions:=VbNext)
            Dim substituted = CreateCompilation("", references:={New VisualBasicCompilationReference(library), second}, parseOptions:=VbNext)
            Dim substitutedUse = substituted.GetTypeByMetadataName("Use")
            Assert.IsType(Of RetargetingNamedTypeSymbol)(substitutedUse)
            Dim substitutedBox = substitutedUse.GetMember(Of MethodSymbol)("Go").Parameters.Single().Type
            Assert.NotSame(library.GetTypeByMetadataName("Box").ContainingAssembly, substitutedBox.ContainingAssembly)
            Assert.Equal(NullableAnnotation.Annotated, Constraint(substitutedBox, "NullableRef"))
            Assert.Equal(NullableAnnotation.NotAnnotated, Constraint(substitutedBox, "NonNullRef"))
        End Sub

        <Fact>
        Public Sub NotNullContract_StaysIndependentOfTheSubstitutedAnnotation()
            Dim source = "
Imports System
Imports System.Diagnostics.CodeAnalysis
Public Class Api
    Public Function RequireValue(Of T As Class)(value As T) As <NotNull> T
        Return value
    End Function
End Class
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
End Namespace
"
            Dim imported = ImportVb(source)
            Dim method = DirectCast(imported.GetTypeByMetadataName("Api").GetMembers("RequireValue").Single(), MethodSymbol)
            Assert.Equal(FlowAnalysisAnnotations.NotNull, method.ReturnTypeFlowAnalysisAnnotations And FlowAnalysisAnnotations.NotNull)
            Dim constructed = method.Construct(ImmutableArray.Create(Of TypeSymbol)(imported.GetSpecialType(SpecialType.System_String)))
            constructed.SetTypeArgumentNullableAnnotations(ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.Equal(NullableAnnotation.Annotated, constructed.GetReturnNullableAnnotation())
            Assert.Equal(FlowAnalysisAnnotations.NotNull, constructed.ReturnTypeFlowAnalysisAnnotations And FlowAnalysisAnnotations.NotNull)
        End Sub

        <Fact>
        Public Sub IncompatibleNullableAttribute_DoesNotCrashOrEmbedASecondDefinition()
            Dim fake = "
Namespace System.Runtime.CompilerServices
    Public Class NullableAttribute
        Inherits System.Attribute
        Public Sub New(text As String)
        End Sub
    End Class
End Namespace
"
            Dim reference = CreateCompilation(fake, parseOptions:=VbNext).EmitToImageReference()
            Dim consumer = CreateCompilation("
Public Class Api
    Public Function Find(text As String?) As String?
        Return text
    End Function
End Class
", references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim image = consumer.EmitToArray()
            Assert.False(DefinesType(image, "System.Runtime.CompilerServices.NullableAttribute"))
        End Sub

        <Fact>
        Public Sub CrossLanguage_PreservesNestedGenericTupleAndEvent()
            Dim vbSource = "
Imports System.Collections.Generic
Public Delegate Sub MessageHandler(Of T)(message As T)
Public Class Api
    Public Items As List(Of String?)
    Public Eight As (String?, String, String?, String, String?, String, String?, String)
    Public Event MessageReceived As MessageHandler(Of String?)
End Class
"
            Dim vbImage = CreateCompilation(vbSource, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext).EmitToImageReference()
            Dim csharpConsumer = CreateCSharpCompilation("", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net100).Concat({vbImage}).ToArray())
            Dim csharpApi = csharpConsumer.GetTypeByMetadataName("Api")
            Dim csharpItems = DirectCast(csharpApi.GetMembers("Items").Single(), IFieldSymbol).Type
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(csharpItems, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Dim csharpEight = DirectCast(DirectCast(csharpApi.GetMembers("Eight").Single(), IFieldSymbol).Type, INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, csharpEight.TupleElements(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, csharpEight.TupleElements(1).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, csharpEight.TupleElements(7).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(csharpApi.GetMembers("MessageReceived").Single(), IEventSymbol).Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())

            Dim csharp = CreateCSharpCompilation("
#nullable enable
using System.Collections.Generic;
public delegate void MessageHandler<T>(T message);
public class Api
{
    public List<string?> Items;
    public (string?, string, string?, string, string?, string, string?, string) Eight;
    public event MessageHandler<string?> MessageReceived;
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net100).ToArray())
            Dim csharpImage = csharp.EmitToImageReference()
            Dim vbConsumer = CreateCompilation("", references:={csharpImage}, parseOptions:=VbNext, targetFramework:=TargetFramework.Net100)
            Dim api = vbConsumer.GetTypeByMetadataName("Api")
            Assert.Equal(NullableAnnotation.Annotated, Args(Field(api, "Items").Type).Single())
            AssertLogical(Field(api, "Eight"), NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            Assert.Equal(NullableAnnotation.Annotated, Args(EventType(api, "MessageReceived")).Single())
        End Sub

        <Fact>
        Public Sub LegacyLanguageVersions_DoNotEmitNullableMetadata()
            Dim source = "
Public Class Api
    Public Function Echo(text As String) As String
        Return text
    End Function
End Class
"
            For Each version In {LanguageVersion.Default, LanguageVersion.Latest, LanguageVersion.VisualBasic17_13}
                Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=TestOptions.Regular.WithLanguageVersion(version))
                compilation.VerifyDiagnostics()
                Assert.False(DefinesType(compilation.EmitToArray(), "System.Runtime.CompilerServices.NullableAttribute"))
            Next
        End Sub

        <Fact>
        Public Sub NullableAnnotations_DoNotChangeRepresentativeMethodBodies()
            Dim plain = "
Public Module Api
    Public Function Id(Of T)(value As T) As T
        Return value
    End Function
    <System.Runtime.CompilerServices.Extension>
    Public Function Echo(value As String) As String
        Return value
    End Function
    Public Function First(value As (String, String)) As String
        Return value.Item1
    End Function
    Public Event Ready As System.Action
    Public Sub RaiseReady()
        RaiseEvent Ready()
    End Sub
End Module
"
            Dim annotated = "
Public Module Api
    Public Function Id(Of T)(value As T) As T
        Return value
    End Function
    <System.Runtime.CompilerServices.Extension>
    Public Function Echo(value As String?) As String?
        Return value
    End Function
    Public Function First(value As (String?, String)) As String?
        Return value.Item1
    End Function
    Public Event Ready As System.Action
    Public Sub RaiseReady()
        RaiseEvent Ready()
    End Sub
End Module
"
            Dim options = TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable)
            Dim plainCompilation = CreateCompilation(plain, options:=options, parseOptions:=VbNext)
            Dim annotatedCompilation = CreateCompilation(annotated, options:=options, parseOptions:=VbNext)
            plainCompilation.VerifyDiagnostics()
            annotatedCompilation.VerifyDiagnostics()
            Assert.Equal(MethodBody(plainCompilation, "Api.Id"), MethodBody(annotatedCompilation, "Api.Id"))
            Assert.Equal(MethodBody(plainCompilation, "Api.Echo"), MethodBody(annotatedCompilation, "Api.Echo"))
            Assert.Equal(MethodBody(plainCompilation, "Api.First"), MethodBody(annotatedCompilation, "Api.First"))
            Assert.Equal(MethodBody(plainCompilation, "Api.RaiseReady"), MethodBody(annotatedCompilation, "Api.RaiseReady"))
        End Sub

        <Fact>
        Public Sub NetModule_PreservesANestedAnnotation()
            Dim source = "
Imports System.Collections.Generic
Public Class Api
    Public Items As List(Of String?)
    Public Eight As (String?, String, String?, String, String?, String, String?, String)
End Class
"
            Dim netModule = CreateCompilation(source, options:=TestOptions.ReleaseModule.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim moduleReference = netModule.EmitToImageReference()
            Dim assembly = CreateCompilation("
Public Class Host
End Class
", references:={moduleReference}, parseOptions:=VbNext)
            Dim added = assembly.Assembly.Modules(1)
            Dim api = added.GlobalNamespace.GetTypeMembers("Api").Single()
            Assert.Equal(NullableAnnotation.Annotated, Args(Field(api, "Items").Type).Single())
            AssertLogical(Field(api, "Eight"), NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
        End Sub

        <Fact>
        Public Sub EmbeddedNullableAttributes_AreRecognizedAcrossAssemblies()
            Dim source = "
Public Class Api
    Public Function Find(text As String?) As String?
        Return text
    End Function
End Class
"
            Dim options = TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable)
            Dim first = CreateCompilation(source, options:=options, parseOptions:=VbNext, assemblyName:="First", targetFramework:=TargetFramework.Mscorlib40).EmitToImageReference()
            Dim second = CreateCompilation(source, options:=options, parseOptions:=VbNext, assemblyName:="Second", targetFramework:=TargetFramework.Mscorlib40).EmitToImageReference()
            Dim consumer = CreateCompilation("", references:={first, second}, parseOptions:=VbNext, targetFramework:=TargetFramework.Mscorlib40)
            Dim apis = consumer.GetTypesByMetadataName("Api")
            Assert.Equal(2, apis.Length)
            For Each api In apis
                Dim method = DirectCast(api.GetMembers("Find").Single(), MethodSymbol)
                Assert.Equal(NullableAnnotation.Annotated, method.GetReturnNullableAnnotation())
                Assert.Equal(NullableAnnotation.Annotated, method.Parameters.Single().GetDeclaredNullableAnnotation())
            Next
        End Sub

        Private Function MethodBody(compilation As VisualBasicCompilation, methodName As String) As String
            Dim verified = CompileAndVerify(compilation)
            Dim il = verified.VisualizeIL(methodName)
            Dim bodyStart = il.IndexOf("{", StringComparison.Ordinal)
            Assert.True(bodyStart >= 0, il)
            Return il.Substring(bodyStart)
        End Function

        Private Function ImportVb(source As String) As VisualBasicCompilation
            Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
            Return CreateCompilation("", references:={compilation.EmitToImageReference()}, parseOptions:=VbNext)
        End Function

        Private Shared Function Field(container As INamedTypeSymbol, name As String) As FieldSymbol
            Return DirectCast(container.GetMembers(name).Single(), FieldSymbol)
        End Function

        Private Shared Function EventType(container As INamedTypeSymbol, name As String) As TypeSymbol
            Return DirectCast(container.GetMembers(name).Single(), EventSymbol).Type
        End Function

        Private Shared Function Args(type As TypeSymbol) As ImmutableArray(Of NullableAnnotation)
            Return DirectCast(type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations()
        End Function

        Private Shared Function Constraint(container As INamespaceOrTypeSymbol, name As String) As NullableAnnotation
            Dim method = DirectCast(DirectCast(container, INamedTypeSymbol).GetMembers(name).Single(), MethodSymbol)
            Return method.TypeParameters.Single().GetReferenceTypeConstraintNullableAnnotation()
        End Function

        Private Shared Sub AssertLogical(field As FieldSymbol, ParamArray expected() As NullableAnnotation)
            AssertLogical(field.Type, expected)
        End Sub

        Private Shared Sub AssertLogical(type As TypeSymbol, ParamArray expected() As NullableAnnotation)
            Dim actual = LogicalAnnotations(DirectCast(type, NamedTypeSymbol))
            Assert.Equal(expected.Length, actual.Count)
            For i = 0 To expected.Length - 1
                Assert.Equal(expected(i), actual(i))
            Next
        End Sub

        Private Shared Function LogicalAnnotations(tuple As NamedTypeSymbol) As IReadOnlyList(Of NullableAnnotation)
            Dim builder As New List(Of NullableAnnotation)()
            ReadChain(tuple.TupleUnderlyingType, builder)
            Return builder
        End Function

        Private Shared Sub ReadChain(underlying As NamedTypeSymbol, builder As List(Of NullableAnnotation))
            Dim shape = If(underlying.IsTupleType, underlying.TupleUnderlyingType, underlying)
            Dim annotations = shape.GetTypeArgumentNullableAnnotations()
            Dim arguments = shape.TypeArgumentsNoUseSiteDiagnostics
            Dim prefix = Math.Min(arguments.Length, 7)
            For i = 0 To prefix - 1
                builder.Add(If(annotations.IsDefault OrElse i >= annotations.Length, NullableAnnotation.None, annotations(i)))
            Next

            If shape.Arity = 8 Then
                ReadChain(DirectCast(arguments(7), NamedTypeSymbol), builder)
            End If
        End Sub

        Private Shared Function DefinesType(image As ImmutableArray(Of Byte), fullName As String) As Boolean
            Using reader = ModuleMetadata.CreateFromImage(image)
                Dim metadata = reader.Module.GetMetadataReader()
                For Each handle In metadata.TypeDefinitions
                    Dim type = metadata.GetTypeDefinition(handle)
                    Dim name = metadata.GetString(type.Namespace) & "." & metadata.GetString(type.Name)
                    If name = fullName Then
                        Return True
                    End If
                Next
            End Using

            Return False
        End Function

    End Class

End Namespace
