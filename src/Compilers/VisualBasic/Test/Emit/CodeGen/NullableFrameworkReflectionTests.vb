' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Reflection
Imports System.Reflection.Metadata
Imports System.Reflection.PortableExecutable
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    ''' <summary>
    ''' Exact framework reference verification. A skip means the reference pack is absent.
    ''' A skip is not framework acceptance.
    ''' </summary>
    Public Class NullableFrameworkReflectionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        Private Const LibrarySource As String = "
Imports System.Runtime.CompilerServices
Public Delegate Sub MessageHandler(Of T)(message As T)
Public Class Api
    Public Plain As String
    Public Marked As String?
    Public Items As System.Collections.Generic.List(Of String?)
    Public Function Echo(text As String) As String
        Return text
    End Function
    Public Function Find(text As String?) As String?
        Return text
    End Function
    Public Event Raised As MessageHandler(Of String?)
End Class
Public Module TextExtensions
    <Extension>
    Public Function Mark(text As String?) As String?
        Return text
    End Function
End Module
"

        <ConditionalFact(GetType(ExactNet47Evidence))>
        Public Sub ExactNet47_EmbedsAttributeAndPreservesAnnotations()
            Dim references = ReferenceDirectory(ExactNet47Evidence.Directory())
            Dim compilation = Compile(LibrarySource, references)
            Assert.Empty(compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error))
            Dim image = compilation.EmitToArray().ToArray()
            Assert.True(DefinesType(image, "System.Runtime.CompilerServices.NullableAttribute"))
            Assert.Equal({CByte(2)}, ReadFlags(image, "Api", "Marked"))
            Assert.Equal({CByte(1), CByte(2)}, ReadFlags(image, "Api", "Items"))
            Assert.Equal({CByte(0), CByte(2)}, ReadFlags(image, "Api", "Raised"))
            Assert.Equal({CByte(2)}, ReadFlags(image, "Api", "Find"))
            Dim referenceNames = AssemblyReferenceNames(image)
            Assert.Contains("mscorlib", referenceNames)
            Assert.DoesNotContain("System.Runtime", referenceNames)

            Dim plain = Compile(LibrarySource.Replace("As String?", "As String").Replace("(text As String?) As String?", "(text As String) As String").Replace("As MessageHandler(Of String?)", "As MessageHandler(Of String)"), references)
            Assert.Equal(MethodBody(plain.EmitToArray().ToArray(), "Echo"), MethodBody(image, "Echo"))

            Dim imported = Import(image, references)
            Dim api = imported.GetTypeByMetadataName("Api")
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(api.GetMembers("Plain").Single(), FieldSymbol).GetNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(api.GetMembers("Marked").Single(), FieldSymbol).GetNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(api.GetMembers("Items").Single(), FieldSymbol).Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations()(0))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(api.GetMembers("Find").Single(), MethodSymbol).Parameters(0), IParameterSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(api.GetMembers("Raised").Single(), EventSymbol).Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())

            Dim consumer = Compile("
Public Class Use
    Public Function Go(api As Api) As String
        Return api.Find(Nothing)
    End Function
End Class
", references.Concat({MetadataReference.CreateFromImage(image)}))
            Assert.Contains(consumer.GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullReturn)

            Dim csharp = CreateCSharpCompilation("
#nullable enable
public class Use
{
    public static string? Read(Api api) => api.Find(null);
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=references.Concat({MetadataReference.CreateFromImage(image)}).ToArray())
            Assert.DoesNotContain(csharp.GetDiagnostics(), Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(csharp.GetTypeByMetadataName("Api").GetMembers("Find").Single(), IMethodSymbol).ReturnNullableAnnotation)

            Dim second = Compile(LibrarySource, references, "SecondApi")
            Assert.True(DefinesType(second.EmitToArray().ToArray(), "System.Runtime.CompilerServices.NullableAttribute"))
        End Sub

        <ConditionalFact(GetType(ExactNet47Evidence))>
        Public Sub ExactNet47_IncompatiblePredefinedAttributeReportsMissingHelper()
            Dim source = "
Namespace System.Runtime.CompilerServices
    Public Class NullableAttribute
        Inherits System.Attribute
        Public Sub New(text As String)
        End Sub
    End Class
End Namespace
Public Class Api
    Public Marked As String?
End Class
"
            Dim compilation = Compile(source, ReferenceDirectory(ExactNet47Evidence.Directory()))
            Assert.Contains(compilation.GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.ERR_MissingRuntimeHelper)
        End Sub

        <ConditionalFact(GetType(ExactNet47Evidence))>
        Public Sub ExactNet47_FixtureSuppliedNotNullIsRecognized()
            Dim source = "
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits System.Attribute
    End Class
End Namespace
Public Class Api
    Public Function Ready() As <System.Diagnostics.CodeAnalysis.NotNull> String?
        Return ""ready""
    End Function
End Class
Public Module Use
    Public Function Go(api As Api) As String
        Return api.Ready()
    End Function
End Module
"
            Dim compilation = Compile(source, ReferenceDirectory(ExactNet47Evidence.Directory()))
            Assert.DoesNotContain(compilation.GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullReturn)
        End Sub

        <ConditionalFact(GetType(ExactNet47Evidence))>
        Public Sub ExactNet47_LegacyLanguageDoesNotActivateNullableSyntax()
            Dim references = ReferenceDirectory(ExactNet47Evidence.Directory())
            For Each version In {LanguageVersion.VisualBasic17_13, LanguageVersion.Default, LanguageVersion.Latest}
                Dim compilation = Compile("Public Class Api
    Public Marked As String?
End Class
", references, languageVersion:=version, nullable:=False)
                Assert.Contains(compilation.GetDiagnostics(), Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error)
            Next
        End Sub

        <ConditionalFact(GetType(HistoricalNet472Evidence))>
        Public Sub HistoricalNet472_EmbedsAttributeAndIsDistinctFromNet47()
            Dim net472 = HistoricalNet472Evidence.Directory()
            Dim net47 = ExactNet47Evidence.Directory()
            Assert.NotEqual(New IO.FileInfo(IO.Path.Combine(net47, "mscorlib.dll")).Length, New IO.FileInfo(IO.Path.Combine(net472, "mscorlib.dll")).Length)
            Dim compilation = Compile(LibrarySource, ReferenceDirectory(net472))
            Assert.Empty(compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error))
            Dim image = compilation.EmitToArray().ToArray()
            Assert.True(DefinesType(image, "System.Runtime.CompilerServices.NullableAttribute"))
            Assert.Equal({CByte(2)}, ReadFlags(image, "Api", "Find"))
            Assert.Contains("mscorlib", AssemblyReferenceNames(image))
        End Sub

        <ConditionalFact(GetType(ExactNet47Evidence))>
        Public Sub Net47Image_ImportsAgainstNet11References()
            Dim image = Compile(LibrarySource, ReferenceDirectory(ExactNet47Evidence.Directory())).EmitToArray().ToArray()
            Dim modern = CreateEmptyCompilation("", references:=ReferenceDirectory(ExactNet11Evidence.Directory()).Concat({MetadataReference.CreateFromImage(image)}), parseOptions:=VbNext)
            Assert.Empty(modern.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(modern.GetTypeByMetadataName("Api").GetMembers("Find").Single(), MethodSymbol).GetReturnNullableAnnotation())
            Assert.Equal({CByte(2)}, ReadFlags(image, "Api", "Find"))
        End Sub

        <ConditionalFact(GetType(ExactNet11Evidence))>
        Public Sub ExactNet11_UsesPredefinedAttributeAndRunsOnNet11()
            Dim references = ReferenceDirectory(ExactNet11Evidence.Directory())
            Dim runtime = DirectCast(references.Single(Function(reference) IO.Path.GetFileName(DirectCast(reference, PortableExecutableReference).FilePath).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase)), PortableExecutableReference)
            Assert.Equal(New Version(11, 0, 0, 0), AssemblyNameOf(runtime.FilePath).Version)
            Dim source = LibrarySource & "
Public Class Pair
    Public Value As (String?, String)
End Class
"
            Dim compilation = Compile(source, references)
            Assert.Empty(compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error))
            Dim image = compilation.EmitToArray().ToArray()
            Assert.False(DefinesType(image, "System.Runtime.CompilerServices.NullableAttribute"))
            Assert.Equal({CByte(2)}, ReadFlags(image, "Api", "Find"))
            Assert.Equal({CByte(0), CByte(2), CByte(1)}, ReadFlags(image, "Pair", "Value"))
            Assert.Contains(AssemblyReferenceNames(image), Function(name) name = "System.Runtime")

            Dim plainSource = source.Replace("As String?", "As String").Replace("(text As String?) As String?", "(text As String) As String").Replace("As MessageHandler(Of String?)", "As MessageHandler(Of String)").Replace("As (String?, String)", "As (String, String)")
            Assert.Equal(MethodBody(Compile(plainSource, references).EmitToArray().ToArray(), "Echo"), MethodBody(image, "Echo"))

            Dim imported = Import(image, references)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(imported.GetTypeByMetadataName("Api").GetMembers("Find").Single(), MethodSymbol).GetReturnNullableAnnotation())

            Dim directory = IO.Path.Combine(IO.Path.GetTempPath(), "lc1g3-net11-" & Guid.NewGuid().ToString("N"))
            IO.Directory.CreateDirectory(directory)
            Try
                Dim libraryPath = IO.Path.Combine(directory, "Library.dll")
                IO.File.WriteAllBytes(libraryPath, image)
                Dim inspector = Compile("
Public Module Program
    Public Sub Main()
        Dim assembly = System.Reflection.Assembly.LoadFrom(""Library.dll"")
        Dim context = New System.Reflection.NullabilityInfoContext()
        Dim api = assembly.GetType(""Api"")
        Dim marked = context.Create(api.GetField(""Marked""))
        Dim find = context.Create(api.GetMethod(""Find"").ReturnParameter)
        Dim items = context.Create(api.GetField(""Items""))
        System.Console.Write(marked.ReadState.ToString() & ""|"" & find.ReadState.ToString() & ""|"" & items.ReadState.ToString() & ""|"" & items.GenericTypeArguments(0).ReadState.ToString())
    End Sub
End Module
", references, "Inspector", kind:=OutputKind.ConsoleApplication)
                Assert.Empty(inspector.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error))
                Dim inspectorPath = IO.Path.Combine(directory, "Inspector.dll")
                IO.File.WriteAllBytes(inspectorPath, inspector.EmitToArray().ToArray())
                IO.File.WriteAllText(IO.Path.Combine(directory, "Inspector.runtimeconfig.json"), "{""runtimeOptions"":{""tfm"":""net11.0"",""framework"":{""name"":""Microsoft.NETCore.App"",""version"":""11.0.0""}}}")
                Dim output = Run(inspectorPath, directory)
                Assert.Equal("Nullable|Nullable|NotNull|Nullable", output.Trim())
            Finally
                IO.Directory.Delete(directory, True)
            End Try
        End Sub

        Private Function Compile(source As String, references As IEnumerable(Of MetadataReference), Optional assemblyName As String = Nothing, Optional languageVersion As LanguageVersion = LanguageVersion.VbNext, Optional nullable As Boolean = True, Optional kind As OutputKind = OutputKind.DynamicallyLinkedLibrary) As VisualBasicCompilation
            Dim options = TestOptions.ReleaseDll.WithOutputKind(kind)
            If nullable Then
                options = options.WithNullableContext(NullableContextOptions.Enable)
            End If

            Return CreateEmptyCompilation(source, references:=references, options:=options, parseOptions:=TestOptions.Regular.WithLanguageVersion(languageVersion), assemblyName:=assemblyName)
        End Function

        Private Function Import(image As Byte(), references As IEnumerable(Of MetadataReference)) As VisualBasicCompilation
            Return CreateEmptyCompilation("", references:=references.Concat({MetadataReference.CreateFromImage(image)}), parseOptions:=VbNext)
        End Function

        Private Shared Function ReferenceDirectory(directory As String) As MetadataReference()
            Dim references = New List(Of MetadataReference)()
            For Each path In IO.Directory.GetFiles(directory, "*.dll")
                Try
                    Using stream = IO.File.OpenRead(path)
                        Using pe As New PEReader(stream)
                            If Not pe.HasMetadata OrElse Not pe.GetMetadataReader().IsAssembly Then
                                Continue For
                            End If
                        End Using
                    End Using

                    references.Add(MetadataReference.CreateFromFile(path))
                Catch ex As BadImageFormatException
                End Try
            Next

            Return references.ToArray()
        End Function

        Private Shared Function AssemblyNameOf(path As String) As AssemblyName
            Using stream = IO.File.OpenRead(path)
                Using pe As New PEReader(stream)
                    Dim reader = pe.GetMetadataReader()
                    Dim definition = reader.GetAssemblyDefinition()
                    Return definition.GetAssemblyName()
                End Using
            End Using
        End Function

        Private Shared Function AssemblyReferenceNames(image As Byte()) As String()
            Using pe As New PEReader(New IO.MemoryStream(image))
                Dim reader = pe.GetMetadataReader()
                Dim names = New List(Of String)()
                For Each handle In reader.AssemblyReferences
                    names.Add(reader.GetString(reader.GetAssemblyReference(handle).Name))
                Next

                Return names.ToArray()
            End Using
        End Function

        Private Shared Function DefinesType(image As Byte(), fullName As String) As Boolean
            Dim split = fullName.LastIndexOf("."c)
            Dim [namespace] = fullName.Substring(0, split)
            Dim name = fullName.Substring(split + 1)
            Using pe As New PEReader(New IO.MemoryStream(image))
                Dim reader = pe.GetMetadataReader()
                For Each handle In reader.TypeDefinitions
                    Dim type = reader.GetTypeDefinition(handle)
                    If reader.GetString(type.Name) = name AndAlso reader.GetString(type.Namespace) = [namespace] Then
                        Return True
                    End If
                Next
            End Using

            Return False
        End Function

        Private Shared Function ReadFlags(image As Byte(), typeName As String, memberName As String) As Byte()
            Using pe As New PEReader(New IO.MemoryStream(image))
                Dim reader = pe.GetMetadataReader()
                For Each handle In reader.TypeDefinitions
                    Dim type = reader.GetTypeDefinition(handle)
                    If reader.GetString(type.Name) <> typeName Then
                        Continue For
                    End If

                    For Each fieldHandle In type.GetFields()
                        Dim field = reader.GetFieldDefinition(fieldHandle)
                        If reader.GetString(field.Name) = memberName Then
                            Return ReadAttributeFlags(reader, field.GetCustomAttributes())
                        End If
                    Next

                    For Each eventHandle In type.GetEvents()
                        Dim [event] = reader.GetEventDefinition(eventHandle)
                        If reader.GetString([event].Name) = memberName Then
                            Return ReadAttributeFlags(reader, [event].GetCustomAttributes())
                        End If
                    Next

                    For Each methodHandle In type.GetMethods()
                        Dim method = reader.GetMethodDefinition(methodHandle)
                        If reader.GetString(method.Name) <> memberName Then
                            Continue For
                        End If

                        For Each parameterHandle In method.GetParameters()
                            Dim parameter = reader.GetParameter(parameterHandle)
                            If parameter.SequenceNumber = 0 Then
                                Return ReadAttributeFlags(reader, parameter.GetCustomAttributes())
                            End If
                        Next
                    Next
                Next
            End Using

            Return Array.Empty(Of Byte)()
        End Function

        Private Shared Function ReadAttributeFlags(reader As MetadataReader, attributes As CustomAttributeHandleCollection) As Byte()
            For Each attributeHandle In attributes
                Dim attribute = reader.GetCustomAttribute(attributeHandle)
                If ConstructorTypeName(reader, attribute.Constructor) <> "NullableAttribute" Then
                    Continue For
                End If

                Dim blob = reader.GetBlobReader(attribute.Value)
                If blob.RemainingBytes < 3 OrElse blob.ReadByte() <> 1 OrElse blob.ReadByte() <> 0 Then
                    Continue For
                End If

                If ConstructorTakesArray(reader, attribute.Constructor) Then
                    Dim length = blob.ReadInt32()
                    Dim flags(length - 1) As Byte
                    For i = 0 To length - 1
                        flags(i) = blob.ReadByte()
                    Next

                    Return flags
                End If

                Return {blob.ReadByte()}
            Next

            Return Array.Empty(Of Byte)()
        End Function

        Private Shared Function ConstructorTakesArray(reader As MetadataReader, constructor As EntityHandle) As Boolean
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
                Dim type = reader.GetTypeDefinition(CType(parent, TypeDefinitionHandle))
                Return reader.GetString(type.Name)
            End If

            Dim member = reader.GetMemberReference(CType(constructor, MemberReferenceHandle))
            parent = member.Parent
            If parent.Kind = HandleKind.TypeReference Then
                Return reader.GetString(reader.GetTypeReference(CType(parent, TypeReferenceHandle)).Name)
            End If

            Return ""
        End Function

        Private Shared Function MethodBody(image As Byte(), name As String) As Byte()
            Using pe As New PEReader(New IO.MemoryStream(image))
                Dim reader = pe.GetMetadataReader()
                For Each handle In reader.MethodDefinitions
                    Dim method = reader.GetMethodDefinition(handle)
                    If reader.GetString(method.Name) = name AndAlso method.RelativeVirtualAddress <> 0 Then
                        Return pe.GetMethodBody(method.RelativeVirtualAddress).GetILContent().ToArray()
                    End If
                Next
            End Using

            Throw New InvalidOperationException(name)
        End Function

        Private Shared Function Run(assemblyPath As String, workingDirectory As String) As String
            Dim start As New ProcessStartInfo("dotnet", "exec """ & assemblyPath & """") With {
                .WorkingDirectory = workingDirectory,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .UseShellExecute = False
            }
            Dim process As Process = Process.Start(start)
            Dim stdout = process.StandardOutput.ReadToEnd()
            Dim stderr = process.StandardError.ReadToEnd()
            process.WaitForExit()
            Assert.True(process.ExitCode = 0, stderr)
            process.Dispose()
            Return stdout
        End Function

    End Class

    Public Class ExactNet47Evidence
        Inherits ExecutionCondition

        Public Overrides ReadOnly Property ShouldSkip As Boolean
            Get
                Return Not IO.Directory.Exists(Directory())
            End Get
        End Property

        Public Overrides ReadOnly Property SkipReason As String
            Get
                Return "Exact .NET Framework 4.7 reference assemblies are not configured. A skip is not framework acceptance."
            End Get
        End Property

        Friend Shared Function Directory() As String
            Dim configured = Environment.GetEnvironmentVariable("VBNEXT_NET47_REFS")
            If Not String.IsNullOrWhiteSpace(configured) Then
                Return configured
            End If

            Return "D:\Development\VBNext\Caches\NuGet\packages\microsoft.netframework.referenceassemblies.net47\1.0.3\build\.NETFramework\v4.7"
        End Function

    End Class

    Public Class HistoricalNet472Evidence
        Inherits ExecutionCondition

        Public Overrides ReadOnly Property ShouldSkip As Boolean
            Get
                Return Not IO.Directory.Exists(Directory()) OrElse Not IO.Directory.Exists(ExactNet47Evidence.Directory())
            End Get
        End Property

        Public Overrides ReadOnly Property SkipReason As String
            Get
                Return "The historical .NET Framework 4.7.2 reference pack is not configured. A skip is not .NET Framework 4.7 acceptance."
            End Get
        End Property

        Friend Shared Function Directory() As String
            Dim configured = Environment.GetEnvironmentVariable("VBNEXT_NET472_REFS")
            If Not String.IsNullOrWhiteSpace(configured) Then
                Return configured
            End If

            Return "D:\Development\VBNext\Caches\NuGet\packages\microsoft.netframework.referenceassemblies.net472\1.0.3\build\.NETFramework\v4.7.2"
        End Function

    End Class

    Public Class ExactNet11Evidence
        Inherits ExecutionCondition

        Public Overrides ReadOnly Property ShouldSkip As Boolean
            Get
                Return Not IO.Directory.Exists(Directory())
            End Get
        End Property

        Public Overrides ReadOnly Property SkipReason As String
            Get
                Return "Exact .NET 11 reference assemblies are not installed. A skip is not .NET 11 acceptance."
            End Get
        End Property

        Friend Shared Function Directory() As String
            Dim configured = Environment.GetEnvironmentVariable("VBNEXT_NET11_REFS")
            If Not String.IsNullOrWhiteSpace(configured) Then
                Return configured
            End If

            Dim sharedDirectory = IO.Path.GetDirectoryName(GetType(Object).Assembly.Location)
            Dim dotnetRoot = IO.Path.GetFullPath(IO.Path.Combine(sharedDirectory, "..", "..", ".."))
            Return IO.Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref", "11.0.0", "ref", "net11.0")
        End Function

    End Class

End Namespace
