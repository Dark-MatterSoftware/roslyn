' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Reflection
Imports System.Reflection.Metadata
Imports System.Reflection.PortableExecutable
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ExtensionPropertyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ExtensionProperty_ReceiverIsPassed()
            Dim compilation = CreateCompilation("
Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property
    End Extension
End Module

Module Program
    Sub Main()
        System.Console.Write(""hi"".WordCount)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionIndexer_ReceiverAndIndexArePassed()
            Dim compilation = CreateCompilation("
Class Box
    Public Value As String
End Class

Module BoxExtensions
    Extension(box As Box)
        Public ReadOnly Property Item(index As Integer) As Char
            Get
                Return box.Value(index)
            End Get
        End Property
    End Extension
End Module

Module Program
    Sub Main()
        Dim box As New Box()
        box.Value = ""hi""
        System.Console.Write(box(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="h").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionSharedMember_CalledOnExtendedType()
            Dim compilation = CreateCompilation("
Module StringExtensions
    Extension(s As String)
        Public Shared Function Twice(value As String) As String
            Return value & value
        End Function

        Public Shared ReadOnly Property Marker As String
            Get
                Return ""!""
            End Get
        End Property
    End Extension
End Module

Module Program
    Sub Main()
        System.Console.Write(String.Twice(""hi""))
        System.Console.Write(String.Marker)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hihi!").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionProperty_RejectedOutsideModule()
            Dim compilation = CreateCompilation("
Class C
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property
    End Extension
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExtensionMethodNotInModule, "Extension(s As String)").WithLocation(3, 5))
        End Sub

        <Fact>
        Public Sub ExtensionProperty_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property
    End Extension
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedDeclaration, "Extension").WithLocation(3, 5),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "s").WithArguments("s").WithLocation(6, 24),
                Diagnostic(ERRID.ERR_UnrecognizedEnd, "End").WithLocation(9, 5))
        End Sub

        <Fact>
        Public Sub ExtensionProperty_DeclaredSymbols()
            Dim compilation = CreateCompilation("
Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property
    End Extension
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim propertyBlock = tree.GetRoot().DescendantNodes().OfType(Of Microsoft.CodeAnalysis.VisualBasic.Syntax.PropertyBlockSyntax)().Single()
            Dim wordCount = model.GetDeclaredSymbol(propertyBlock.PropertyStatement)
            Assert.Equal("ReadOnly Property StringExtensions.WordCount(s As System.String) As System.Int32", wordCount.ToTestDisplayString())
            Dim getter = model.GetDeclaredSymbol(propertyBlock.Accessors.Single().AccessorStatement)
            Assert.Same(DirectCast(wordCount, IPropertySymbol).GetMethod, getter)
        End Sub

        <Fact>
        Public Sub ExtensionProperty_MetadataReference_NamedAccessAndExtensionMethod()
            Dim producer = ExtensionProducer()
            Dim consumer = CreateCompilation(ExtensionConsumer(includeDefaultIndex:=False), references:={producer.EmitToImageReference()}, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)
            CompileAndVerify(consumer, expectedOutput:="!h2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionProperty_ReferenceAssembly_NamedAccess()
            Dim producer = ExtensionProducer()
            Dim image = producer.EmitToArray(options:=New EmitOptions(metadataOnly:=True))
            Dim sawGrouping = False
            Dim sawMarker = False
            Dim sawGroupingProperty = False
            Dim sawImplementation = False
            Dim sawModuleProperty = False
            Using stream = New IO.MemoryStream(image.ToArray())
                Using pe = New PEReader(stream)
                    Dim reader = pe.GetMetadataReader()
                    For Each handle In reader.TypeDefinitions
                        Dim type = reader.GetTypeDefinition(handle)
                        Dim name = reader.GetString(type.Name)
                        If name = "BoxExtensions" Then
                            For Each propertyHandle In type.GetProperties()
                                If reader.GetString(reader.GetPropertyDefinition(propertyHandle).Name) = "Marker" Then
                                    sawModuleProperty = True
                                End If
                            Next

                            For Each methodHandle In type.GetMethods()
                                If reader.GetString(reader.GetMethodDefinition(methodHandle).Name) = "get_Marker" Then
                                    sawImplementation = True
                                End If
                            Next
                        ElseIf name.StartsWith("<G>$", StringComparison.Ordinal) Then
                            sawGrouping = True
                            For Each propertyHandle In type.GetProperties()
                                If reader.GetString(reader.GetPropertyDefinition(propertyHandle).Name) = "Marker" Then
                                    sawGroupingProperty = True
                                End If
                            Next

                            For Each nestedHandle In type.GetNestedTypes()
                                If reader.GetString(reader.GetTypeDefinition(nestedHandle).Name).StartsWith("<M>$", StringComparison.Ordinal) Then
                                    sawMarker = True
                                End If
                            Next
                        End If
                    Next
                End Using
            End Using

            Assert.True(sawGrouping)
            Assert.True(sawMarker)
            Assert.True(sawGroupingProperty)
            Assert.True(sawImplementation)
            Assert.False(sawModuleProperty)

            Dim reference = MetadataReference.CreateFromImage(image)
            Dim consumer = CreateCompilation(ExtensionConsumer(includeDefaultIndex:=False), references:={reference}, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)
            consumer.VerifyDiagnostics()
            Dim tree = consumer.SyntaxTrees.Single()
            Dim model = consumer.GetSemanticModel(tree)
            Dim markerAccess = tree.GetRoot().DescendantNodes().OfType(Of Syntax.MemberAccessExpressionSyntax)().Single(Function(access) access.Name.Identifier.ValueText = "Marker")
            Assert.Equal("Marker", model.GetSymbolInfo(markerAccess).Symbol.Name)
        End Sub

        <Fact>
        Public Sub ExtensionProperty_CSharpConsumesNamedPropertyAndIndexer()
            Dim producer = ExtensionProducer()
            Dim csharp = CreateCSharpCompilation("
class Program
{
    static void Main()
    {
        var box = new Box();
        box.Value = ""hi"";
        System.Console.Write(box.Marker);
        System.Console.Write(box[0]);
    }
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net100).Concat({producer.EmitToImageReference()}),
               compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.ConsoleApplication))
            CompileAndVerify(csharp, expectedOutput:="!h").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionProperty_PeReference_DefaultIndexRemainsUnresolved()
            Dim producer = ExtensionProducer()
            Dim consumer = CreateCompilation("
Module Program
    Sub Main()
        Dim box As New Box()
        Dim value = box(0)
    End Sub
End Module
", references:={producer.EmitToImageReference()}, parseOptions:=VbNext)

            consumer.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NoDefaultNotExtend1, "box").WithArguments("Box").WithLocation(5, 21))
        End Sub

        <Fact>
        Public Sub ExtensionProperty_PeReference_NotBoundOn17_13()
            Dim producer = ExtensionProducer()
            Dim consumer = CreateCompilation("
Module Program
    Sub Main()
        Dim box As New Box()
        Dim marker = box.Marker
        Dim count = ""ab"".WordCount()
    End Sub
End Module
", references:={producer.EmitToImageReference()}, parseOptions:=Vb17_13)

            consumer.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotMember2, "box.Marker").WithArguments("Marker", "Box").WithLocation(5, 22))
        End Sub

        <Fact>
        Public Sub ExtensionProperty_CompilationReference_BindsWithoutVbNextGate()
            ' Preserved discrepancy. A compilation reference binds the extension property when the
            ' language version is 17.13. A PE reference does not. This is not accepted as permanent policy.
            Dim producer = ExtensionProducer()
            Dim consumer = CreateCompilation("
Module Program
    Sub Main()
        Dim box As New Box()
        Dim marker = box.Marker
    End Sub
End Module
", references:={producer.ToMetadataReference()}, parseOptions:=Vb17_13)

            consumer.AssertNoDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionProperty_FriendIsNotVisibleToAnotherAssembly()
            Dim producer = CreateCompilation("
Public Class Box
End Class
Public Module BoxExtensions
    Extension(box As Box)
        Friend ReadOnly Property Hidden As Integer
            Get
                Return 1
            End Get
        End Property
    End Extension
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            producer.AssertNoDiagnostics()
            Dim consumer = CreateCompilation("
Module Program
    Sub Main()
        Dim box As New Box()
        Dim hidden = box.Hidden
    End Sub
End Module
", references:={producer.EmitToImageReference()}, parseOptions:=VbNext)

            consumer.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotMember2, "box.Hidden").WithArguments("Hidden", "Box").WithLocation(5, 22))
        End Sub

        <Fact>
        Public Sub ExtensionProperty_EmitsGroupingInsteadOfModuleProperty()
            Dim producer = ExtensionProducer()
            Dim image = producer.EmitToArray()
            Using stream = New IO.MemoryStream(image.ToArray())
                Using pe = New PEReader(stream)
                    Dim reader = pe.GetMetadataReader()
                    Dim sawModuleProperty = False
                    Dim sawGrouping = False
                    Dim moduleIsAbstract = False
                    For Each handle In reader.TypeDefinitions
                        Dim type = reader.GetTypeDefinition(handle)
                        Dim name = reader.GetString(type.Name)
                        If name = "BoxExtensions" Then
                            moduleIsAbstract = (type.Attributes And TypeAttributes.Abstract) <> 0
                            For Each propertyHandle In type.GetProperties()
                                If reader.GetString(reader.GetPropertyDefinition(propertyHandle).Name) = "Marker" Then
                                    sawModuleProperty = True
                                End If
                            Next
                        ElseIf name.StartsWith("<G>$", StringComparison.Ordinal) Then
                            sawGrouping = True
                        End If
                    Next

                    Assert.True(moduleIsAbstract)
                    Assert.True(sawGrouping)
                    Assert.False(sawModuleProperty)
                End Using
            End Using
        End Sub

        <Fact>
        Public Sub OrdinaryModule_IsNotMarkedAbstract()
            Dim compilation = CreateCompilation("
Public Module Ordinary
    Public Function Value() As Integer
        Return 1
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()
            Dim image = compilation.EmitToArray()
            Using stream = New IO.MemoryStream(image.ToArray())
                Using pe = New PEReader(stream)
                    Dim reader = pe.GetMetadataReader()
                    For Each handle In reader.TypeDefinitions
                        Dim type = reader.GetTypeDefinition(handle)
                        If reader.GetString(type.Name) = "Ordinary" Then
                            Assert.Equal(TypeAttributes.NotPublic, type.Attributes And TypeAttributes.Abstract)
                            Return
                        End If
                    Next
                End Using
            End Using

            Assert.Fail("Ordinary module was not emitted.")
        End Sub

        <Fact>
        Public Sub ExtensionMarker_ParameterlessConstructor_ReportsMissingHelper()
            AssertUnusableMarker("Public Sub New()" & vbCrLf & "End Sub")
        End Sub

        <Fact>
        Public Sub ExtensionMarker_WrongParameterType_ReportsMissingHelper()
            AssertUnusableMarker("Public Sub New(value As Integer)" & vbCrLf & "End Sub")
        End Sub

        <Fact>
        Public Sub ExtensionMarker_InaccessibleConstructor_ReportsMissingHelper()
            AssertUnusableMarker("Private Sub New(name As String)" & vbCrLf & "End Sub")
        End Sub

        <Fact>
        Public Sub ExtensionMarker_ReferencedStringConstructor_EmitsAndConsumes()
            Dim attribute = MarkerAttributeReference("Public Sub New(name As String)" & vbCrLf & "End Sub")
            Dim producer = MarkerProducer(attribute)
            producer.VerifyDiagnostics()
            Dim image = producer.EmitToArray()
            Assert.False(ImageDefinesMarkerAttribute(image))
            AssertMarkerConsumed(image, attribute)
        End Sub

        <Fact>
        Public Sub ExtensionMarker_MissingAttribute_EmbedsAndConsumes()
            Dim producer = MarkerProducer(Nothing)
            producer.VerifyDiagnostics()
            Dim image = producer.EmitToArray()
            Assert.True(ImageDefinesMarkerAttribute(image))
            AssertMarkerConsumed(image, Nothing)
        End Sub

        <Fact>
        Public Sub ExtensionMarker_AdditionalConstructor_UsesRequiredConstructor()
            Dim attribute = MarkerAttributeReference("Public Sub New()" & vbCrLf & "End Sub" & vbCrLf & "Public Sub New(name As String)" & vbCrLf & "End Sub")
            Dim producer = MarkerProducer(attribute)
            producer.VerifyDiagnostics()
            Assert.True(producer.Emit(New IO.MemoryStream()).Success)
        End Sub

        <Fact>
        Public Sub ExtensionMarker_UnrelatedCompilation_IgnoresUnusableAttribute()
            Dim compilation = CreateCompilation("
Public Module Ordinary
    Public Function Value() As Integer
        Return 1
    End Function
End Module
", references:={MarkerAttributeReference("Public Sub New()" & vbCrLf & "End Sub")}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext, targetFramework:=TargetFramework.NetFramework)
            compilation.VerifyDiagnostics()
            Assert.True(compilation.Emit(New IO.MemoryStream()).Success)
        End Sub

        <Fact>
        Public Sub ExtensionMarker_Legacy17_13_IgnoresUnusableAttribute()
            Dim compilation = CreateCompilation("
Public Module Ordinary
    Public Function Value() As Integer
        Return 1
    End Function
End Module
", references:={MarkerAttributeReference("Public Sub New()" & vbCrLf & "End Sub")}, options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13, targetFramework:=TargetFramework.NetFramework)
            compilation.VerifyDiagnostics()
            Assert.False(compilation.GetDiagnostics().Any(Function(diagnostic) diagnostic.Code = ERRID.ERR_MissingRuntimeHelper))
        End Sub

        <Fact>
        Public Sub ExtensionMarker_ExtensionSyntaxRemainsRejectedOn17_13()
            Dim compilation = CreateCompilation("
Public Class Box
End Class
Public Module BoxExtensions
    Extension(box As Box)
        Public ReadOnly Property Marker As String
            Get
                Return ""!""
            End Get
        End Property
    End Extension
End Module
", references:={MarkerAttributeReference("Public Sub New()" & vbCrLf & "End Sub")}, options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13, targetFramework:=TargetFramework.NetFramework)
            Assert.False(compilation.GetDiagnostics().Any(Function(diagnostic) diagnostic.Code = ERRID.ERR_MissingRuntimeHelper))
            Assert.Contains(compilation.GetDiagnostics(), Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error)
        End Sub

        <Fact>
        Public Sub ExtensionMarker_CSharpWrongParameterType_IsNotEmitted()
            AssertCSharpMarkerRejected("public ExtensionMarkerAttribute(int value) {}")
        End Sub

        <Fact>
        Public Sub ExtensionMarker_CSharpInaccessibleConstructor_IsNotEmitted()
            AssertCSharpMarkerRejected("private ExtensionMarkerAttribute(string name) {}")
        End Sub

        Private Sub AssertUnusableMarker(constructor As String)
            Dim producer = MarkerProducer(MarkerAttributeReference(constructor))
            producer.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_MissingRuntimeHelper, "Marker").WithArguments("System.Runtime.CompilerServices.ExtensionMarkerAttribute..ctor"))
            Assert.False(producer.Emit(New IO.MemoryStream()).Success)
            Assert.False(producer.Emit(New IO.MemoryStream(), options:=New EmitOptions(metadataOnly:=True)).Success)
        End Sub

        Private Sub AssertCSharpMarkerRejected(constructor As String)
            Dim attribute = CreateCSharpCompilation("
namespace System.Runtime.CompilerServices
{
    public class ExtensionMarkerAttribute : System.Attribute
    {
        " & constructor & "
    }
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.NetFramework),
               compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            attribute.VerifyDiagnostics()
            Dim producer = CreateCSharpCompilation("
public class Box {}
public static class BoxExtensions
{
    extension(Box box)
    {
        public string Marker => ""!"";
    }
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.NetFramework).Concat({attribute.EmitToImageReference()}),
               compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            Assert.Contains(producer.GetDiagnostics(), Function(diagnostic) diagnostic.Id = "CS0656")
            Assert.False(producer.Emit(New IO.MemoryStream()).Success)
        End Sub

        Private Function MarkerAttributeReference(constructor As String) As MetadataReference
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class ExtensionMarkerAttribute
        Inherits Global.System.Attribute
        " & constructor & "
    End Class
End Namespace
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext, targetFramework:=TargetFramework.NetFramework)
            compilation.VerifyDiagnostics()
            Return compilation.EmitToImageReference()
        End Function

        Private Function MarkerProducer(attribute As MetadataReference) As VisualBasicCompilation
            Dim references = If(attribute Is Nothing, Array.Empty(Of MetadataReference)(), {attribute})
            Return CreateCompilation("
Public Class Box
End Class
Public Module BoxExtensions
    Extension(box As Box)
        Public ReadOnly Property Marker As String
            Get
                Return ""!""
            End Get
        End Property
    End Extension
End Module
", references:=references, options:=TestOptions.ReleaseDll, parseOptions:=VbNext, targetFramework:=TargetFramework.NetFramework)
        End Function

        Private Sub AssertMarkerConsumed(image As ImmutableArray(Of Byte), attribute As MetadataReference)
            Dim references As MetadataReference() = {MetadataReference.CreateFromImage(image)}
            If attribute IsNot Nothing Then
                references = {references(0), attribute}
            End If

            Dim consumer = CreateCompilation("
Module Program
    Sub Main()
        Dim box As New Box()
        Dim marker = box.Marker
    End Sub
End Module
", references:=references, options:=TestOptions.ReleaseDll, parseOptions:=VbNext, targetFramework:=TargetFramework.NetFramework)
            consumer.VerifyDiagnostics()
        End Sub

        Private Shared Function ImageDefinesMarkerAttribute(image As ImmutableArray(Of Byte)) As Boolean
            Using stream = New IO.MemoryStream(image.ToArray())
                Using pe = New PEReader(stream)
                    Dim reader = pe.GetMetadataReader()
                    For Each handle In reader.TypeDefinitions
                        If reader.GetString(reader.GetTypeDefinition(handle).Name) = "ExtensionMarkerAttribute" Then
                            Return True
                        End If
                    Next
                End Using
            End Using

            Return False
        End Function

        Private Shared Function ExtensionProducer() As VisualBasicCompilation
            Return CreateCompilation("
Public Class Box
    Public Value As String
End Class
Public Module BoxExtensions
    Extension(box As Box)
        Public ReadOnly Property Marker As String
            Get
                Return ""!""
            End Get
        End Property
        Public ReadOnly Property Item(index As Integer) As Char
            Get
                Return box.Value(index)
            End Get
        End Property
    End Extension
    <System.Runtime.CompilerServices.Extension>
    Public Function WordCount(s As String) As Integer
        Return s.Length
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
        End Function

        Private Shared Function ExtensionConsumer(includeDefaultIndex As Boolean) As String
            Return "
Module Program
    Sub Main()
        Dim box As New Box()
        box.Value = ""hi""
        System.Console.Write(box.Marker)
        System.Console.Write(box.Item(0))
        System.Console.Write(""ab"".WordCount())
    End Sub
End Module
"
        End Function
    End Class

End Namespace
