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
        Public Sub ExtensionProperty_CompilationReference_RejectedBelowVbNext()
            ' Replaces ExtensionProperty_CompilationReference_BindsWithoutVbNextGate.
            ' That test expected a 17.13 compilation reference to bind Marker. The owner accepted
            ' that result as a defect. Legacy consumers now reject the property through every reference kind.
            Dim producer = ExtensionProducer()
            Dim implementation = producer.EmitToImageReference()
            Dim metadataOnly = MetadataReference.CreateFromImage(producer.EmitToArray(options:=New EmitOptions(metadataOnly:=True)))
            For Each parseOptions In LegacyParseOptions()
                For Each reference In {producer.ToMetadataReference(), implementation, metadataOnly}
                    Dim consumer = CreateMarkerConsumer(reference, parseOptions)
                    Assert.Equal(LanguageVersion.VisualBasic17_13, consumer.LanguageVersion)
                    consumer.VerifyDiagnostics(
                        Diagnostic(ERRID.ERR_NameNotMember2, "box.Marker").WithArguments("Marker", "Box").WithLocation(5, 22))
                    Dim access = consumer.SyntaxTrees.Single().GetRoot().DescendantNodes().OfType(Of Syntax.MemberAccessExpressionSyntax)().Single()
                    Dim info = consumer.GetSemanticModel(consumer.SyntaxTrees.Single()).GetSymbolInfo(access)
                    Assert.Null(info.Symbol)
                    Assert.Empty(info.CandidateSymbols)
                Next
            Next
        End Sub

        <Fact>
        Public Sub ExtensionProperty_VbNext_BindsAcrossReferenceKinds()
            Dim producer = ExtensionProducer()
            Dim implementation = producer.EmitToImageReference()
            Dim metadataOnly = MetadataReference.CreateFromImage(producer.EmitToArray(options:=New EmitOptions(metadataOnly:=True)))
            For Each reference In {producer.ToMetadataReference(), implementation, metadataOnly}
                Dim consumer = CreateMarkerConsumer(reference, VbNext)
                Assert.Equal(LanguageVersion.VbNext, consumer.LanguageVersion)
                consumer.VerifyDiagnostics()
                AssertMarkerSymbol(consumer, producer.AssemblyName)
            Next

            Dim same = CreateCompilation(ExtensionProducerSource() & "
Module Program
    Sub Main()
        Dim box As New Box()
        Dim marker = box.Marker
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            same.VerifyDiagnostics()
            AssertMarkerSymbol(same, same.AssemblyName)
        End Sub

        <Fact>
        Public Sub ExtensionProperty_LegacyCompilationReference_RejectsRepresentativeForms()
            Dim producer = FormsProducer()
            Dim consumer = CreateCompilation(FormsConsumerSource(), references:={producer.ToMetadataReference()}, parseOptions:=Vb17_13)
            Assert.Equal(LanguageVersion.VisualBasic17_13, consumer.LanguageVersion)
            AssertRejectedForms(consumer)
            AssertBoundName(consumer, "InstanceName")
            AssertBoundName(consumer, "WordCount")
        End Sub

        <Fact>
        Public Sub ExtensionProperty_DefaultAndLatestCompilationReference_RejectRepresentativeForms()
            For Each parseOptions In {VisualBasicParseOptions.Default, New VisualBasicParseOptions(LanguageVersion.Latest)}
                Dim producer = FormsProducer()
                Dim consumer = CreateCompilation(FormsConsumerSource(), references:={producer.ToMetadataReference()}, parseOptions:=parseOptions)
                Assert.Equal(LanguageVersion.VisualBasic17_13, consumer.LanguageVersion)
                AssertRejectedForms(consumer)
                AssertBoundName(consumer, "WordCount")
            Next
        End Sub

        <Fact>
        Public Sub ExtensionProperty_VbNextCompilationReference_BindsRepresentativeForms()
            Dim producer = FormsProducer()
            Dim consumer = CreateCompilation(FormsConsumerSource(), references:={producer.ToMetadataReference()}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            consumer.VerifyDiagnostics()
            For Each name In {"Marker", "Sink", "ValueText", "Item", "Initial", "Doubled", "FirstOrZero", "InstanceName", "WordCount"}
                AssertBoundName(consumer, name)
            Next
        End Sub

        <Fact>
        Public Sub ExtensionProperty_Friend_VbNextBindsAcrossReferenceKinds()
            Dim producer = FriendProducer()
            Dim implementation = producer.EmitToImageReference()
            Dim metadataOnly = MetadataReference.CreateFromImage(producer.EmitToArray(options:=New EmitOptions(metadataOnly:=True)))
            For Each reference In {producer.ToMetadataReference(), implementation, metadataOnly}
                Dim consumer = CreateFriendConsumer(reference, VbNext, "FriendConsumer")
                consumer.VerifyDiagnostics()
                Dim hidden = BoundProperty(consumer, "Hidden")
                Assert.Equal(Accessibility.Friend, hidden.DeclaredAccessibility)
                Assert.Equal(Accessibility.Friend, hidden.GetMethod.DeclaredAccessibility)
                Assert.Equal(producer.AssemblyName, hidden.ContainingAssembly.Name)
            Next
        End Sub

        <Fact>
        Public Sub ExtensionProperty_Friend_LegacyCompilationReferenceRejectsExtensionProperty()
            Dim producer = FriendProducer()
            For Each parseOptions In LegacyParseOptions()
                Dim consumer = CreateFriendConsumer(producer.ToMetadataReference(), parseOptions, "FriendConsumer")
                Assert.Equal(LanguageVersion.VisualBasic17_13, consumer.LanguageVersion)
                consumer.VerifyDiagnostics(
                    Diagnostic(ERRID.ERR_NameNotMember2, "box.Shown").WithArguments("Shown", "Box").WithLocation(5, 21),
                    Diagnostic(ERRID.ERR_NameNotMember2, "box.Hidden").WithArguments("Hidden", "Box").WithLocation(6, 22))
                AssertBoundName(consumer, "Secret")
                AssertBoundName(consumer, "FriendWord")
                Dim access = MemberAccess(consumer, "Hidden")
                Dim info = consumer.GetSemanticModel(consumer.SyntaxTrees.Single()).GetSymbolInfo(access)
                Assert.Null(info.Symbol)
                Assert.Empty(info.CandidateSymbols)
            Next
        End Sub

        <Fact>
        Public Sub ExtensionProperty_Friend_UnauthorizedCompilationReferenceIsRejected()
            Dim producer = FriendProducer()
            For Each assemblyName In {"OtherConsumer", "FriendCompilationConsumer"}
                Dim consumer = CreateFriendConsumer(producer.ToMetadataReference(), VbNext, assemblyName)
                Dim errors = consumer.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).Select(Function(diagnostic) diagnostic.Id).ToArray()
                Assert.Contains("BC30389", errors)
                Assert.Contains("BC30390", errors)
                Assert.Null(consumer.GetSemanticModel(consumer.SyntaxTrees.Single()).GetSymbolInfo(MemberAccess(consumer, "Hidden")).Symbol)
                AssertBoundName(consumer, "Shown")
            Next
        End Sub

        <Fact>
        Public Sub ExtensionProperty_RetargetedReference_FollowsConsumerLanguageVersion()
            Dim lib1 = CreateCompilation("
<Assembly: System.Reflection.AssemblyVersion(""1.0.0.0"")>
Public Class Box
    Public Value As String
End Class
", assemblyName:="Lib", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            Dim lib2 = CreateCompilation("
<Assembly: System.Reflection.AssemblyVersion(""1.0.1.0"")>
Public Class Box
    Public Value As String
End Class
", assemblyName:="Lib", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            Dim producer = CreateCompilation("
Public Module BoxExtensions
    Extension(box As Box)
        Public ReadOnly Property Marker As String
            Get
                Return box.Value
            End Get
        End Property
    End Extension
    <System.Runtime.CompilerServices.Extension>
    Public Function WordCount(s As String) As Integer
        Return s.Length
    End Function
End Module
", references:={lib1.EmitToImageReference()}, assemblyName:="RetargetProducer", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            producer.VerifyDiagnostics()

            Dim legacy = CreateRetargetConsumer(producer, lib2, Vb17_13)
            Assert.Equal(LanguageVersion.VisualBasic17_13, legacy.LanguageVersion)
            legacy.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotMember2, "box.Marker").WithArguments("Marker", "Box").WithLocation(5, 22))
            AssertBoundName(legacy, "WordCount")

            Dim current = CreateRetargetConsumer(producer, lib2, VbNext)
            current.VerifyDiagnostics()
            Dim marker = BoundProperty(current, "Marker")
            Dim reducedFrom = marker.GetType().GetProperty("ReducedFrom", BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic).GetValue(marker)
            Assert.Contains("RetargetingPropertySymbol", reducedFrom.GetType().Name)
            Dim receiver = marker.GetType().GetProperty("ReceiverType", BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic).GetValue(marker)
            Dim receiverType = DirectCast(receiver, ITypeSymbol)
            Assert.Equal("Lib", receiverType.ContainingAssembly.Name)
            Assert.Equal(New Version(1, 0, 1, 0), receiverType.ContainingAssembly.Identity.Version)
            Assert.Equal(Accessibility.Public, marker.GetMethod.DeclaredAccessibility)
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

        Private Shared Function LegacyParseOptions() As IEnumerable(Of VisualBasicParseOptions)
            Return {
                New VisualBasicParseOptions(LanguageVersion.VisualBasic17_13),
                VisualBasicParseOptions.Default,
                New VisualBasicParseOptions(LanguageVersion.Latest)
            }
        End Function

        Private Shared Function ExtensionProducerSource() As String
            Return "
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
"
        End Function

        Private Function CreateMarkerConsumer(reference As MetadataReference, parseOptions As VisualBasicParseOptions) As VisualBasicCompilation
            Return CreateCompilation("
Module Program
    Sub Main()
        Dim box As New Box()
        Dim marker = box.Marker
    End Sub
End Module
", references:={reference}, parseOptions:=parseOptions)
        End Function

        Private Sub AssertMarkerSymbol(compilation As VisualBasicCompilation, assemblyName As String)
            Dim marker = BoundProperty(compilation, "Marker")
            Assert.Equal("Marker", marker.Name)
            Assert.Equal(Accessibility.Public, marker.DeclaredAccessibility)
            Assert.Equal(Accessibility.Public, marker.GetMethod.DeclaredAccessibility)
            Assert.Null(marker.SetMethod)
            Assert.Equal(assemblyName, marker.ContainingAssembly.Name)
            Dim info = compilation.GetSemanticModel(compilation.SyntaxTrees.Single()).GetTypeInfo(MemberAccess(compilation, "Marker"))
            Assert.Equal(SpecialType.System_String, info.Type.SpecialType)
        End Sub

        Private Shared Function FormsProducer() As VisualBasicCompilation
            Return CreateCompilation("
Public Class Box
    Public InstanceName As String
    Public Value As String
End Class
Public Interface IText
    Function Text() As String
End Interface
Public Class NamedText
    Implements IText
    Public Function Text() As String Implements IText.Text
        Return ""cat""
    End Function
End Class
Public Structure Pair
    Public N As Integer
End Structure
Public Module BoxExtensions
    Extension(box As Box)
        Public ReadOnly Property Marker As String
            Get
                Return ""!""
            End Get
        End Property
        Public WriteOnly Property Sink As String
            Set(value As String)
                box.Value = value
            End Set
        End Property
        Public Property ValueText As String
            Get
                Return box.Value
            End Get
            Set(value As String)
                box.Value = value
            End Set
        End Property
        Public ReadOnly Property Item(index As Integer) As Char
            Get
                Return box.Value(index)
            End Get
        End Property
    End Extension
    Extension(text As IText)
        Public ReadOnly Property Initial As Char
            Get
                Return text.Text()(0)
            End Get
        End Property
    End Extension
    Extension(pair As Pair)
        Public ReadOnly Property Doubled As Integer
            Get
                Return pair.N * 2
            End Get
        End Property
    End Extension
    Extension(items As System.Collections.Generic.List(Of Integer))
        Public ReadOnly Property FirstOrZero As Integer
            Get
                Return If(items.Count = 0, 0, items(0))
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

        Private Shared Function FormsConsumerSource() As String
            Return "
Module Program
    Sub Main()
        Dim box As New Box()
        box.Value = ""hi""
        Dim name = box.InstanceName
        Dim marker = box.Marker
        box.Sink = ""H""
        Dim value = box.ValueText
        Dim item = box.Item(0)
        Dim text As IText = New NamedText()
        Dim initial = text.Initial
        Dim pair As Pair
        Dim doubled = pair.Doubled
        Dim items As New System.Collections.Generic.List(Of Integer)()
        Dim first = items.FirstOrZero
        Dim count = ""ab"".WordCount()
    End Sub
End Module
"
        End Function

        Private Sub AssertRejectedForms(compilation As VisualBasicCompilation)
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim rejected = {"Marker", "Sink", "ValueText", "Item", "Initial", "Doubled", "FirstOrZero"}
            For Each name In rejected
                Dim info = model.GetSymbolInfo(MemberAccess(compilation, name))
                Assert.Null(info.Symbol)
                Assert.Empty(info.CandidateSymbols)
            Next

            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.Equal(rejected.Length, errors.Length)
            Assert.All(errors, Sub(diagnostic) Assert.Equal("BC30456", diagnostic.Id))
        End Sub

        Private Function FriendProducer() As VisualBasicCompilation
            Return CreateCompilation("
<Assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""FriendConsumer"")>
Public Class Box
End Class
Public Module BoxExtensions
    Extension(box As Box)
        Public ReadOnly Property Shown As Integer
            Get
                Return 1
            End Get
        End Property
        Friend ReadOnly Property Hidden As Integer
            Get
                Return 2
            End Get
        End Property
    End Extension
    Friend Function Secret() As Integer
        Return 4
    End Function
    <System.Runtime.CompilerServices.Extension>
    Friend Function FriendWord(box As Box) As String
        Return ""f""
    End Function
End Module
", assemblyName:="FriendProducer", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
        End Function

        Private Function CreateFriendConsumer(reference As MetadataReference, parseOptions As VisualBasicParseOptions, assemblyName As String) As VisualBasicCompilation
            Return CreateCompilation("
Module Program
    Sub Main()
        Dim box As New Box()
        Dim shown = box.Shown
        Dim hidden = box.Hidden
        Dim secret = BoxExtensions.Secret()
        Dim friendWord = box.FriendWord()
    End Sub
End Module
", references:={reference}, assemblyName:=assemblyName, parseOptions:=parseOptions)
        End Function

        Private Function CreateRetargetConsumer(producer As VisualBasicCompilation, lib2 As VisualBasicCompilation, parseOptions As VisualBasicParseOptions) As VisualBasicCompilation
            Return CreateCompilation("
Module Program
    Sub Main()
        Dim box As New Box()
        Dim marker = box.Marker
        Dim count = ""ab"".WordCount()
    End Sub
End Module
", references:={lib2.EmitToImageReference(), producer.ToMetadataReference()}, assemblyName:="RetargetConsumer", parseOptions:=parseOptions)
        End Function

        Private Shared Function MemberAccess(compilation As VisualBasicCompilation, name As String) As Syntax.MemberAccessExpressionSyntax
            Return compilation.SyntaxTrees.Single().GetRoot().DescendantNodes().OfType(Of Syntax.MemberAccessExpressionSyntax)().Single(Function(access) access.Name.Identifier.ValueText = name)
        End Function

        Private Shared Sub AssertBoundName(compilation As VisualBasicCompilation, name As String)
            Dim info = compilation.GetSemanticModel(compilation.SyntaxTrees.Single()).GetSymbolInfo(MemberAccess(compilation, name))
            Assert.NotNull(info.Symbol)
            Assert.Equal(name, info.Symbol.Name)
        End Sub

        Private Shared Function BoundProperty(compilation As VisualBasicCompilation, name As String) As IPropertySymbol
            Return DirectCast(compilation.GetSemanticModel(compilation.SyntaxTrees.Single()).GetSymbolInfo(MemberAccess(compilation, name)).Symbol, IPropertySymbol)
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
