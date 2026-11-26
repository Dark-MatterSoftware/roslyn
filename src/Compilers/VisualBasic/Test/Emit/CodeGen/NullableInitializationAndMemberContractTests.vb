' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableInitializationAndMemberContractTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        Private Const Attributes As String = "
Namespace System.Diagnostics.CodeAnalysis
    <System.AttributeUsage(System.AttributeTargets.Method Or System.AttributeTargets.Property, AllowMultiple:=True)>
    Public NotInheritable Class MemberNotNullAttribute
        Inherits System.Attribute
        Public Sub New(member As String)
        End Sub
        Public Sub New(ParamArray members As String())
        End Sub
    End Class
    <System.AttributeUsage(System.AttributeTargets.Method Or System.AttributeTargets.Property, AllowMultiple:=True)>
    Public NotInheritable Class MemberNotNullWhenAttribute
        Inherits System.Attribute
        Public Sub New(returnValue As Boolean, member As String)
        End Sub
        Public Sub New(returnValue As Boolean, ParamArray members As String())
        End Sub
    End Class
End Namespace
"

        <Fact>
        Public Sub InstanceConstructor_WarnsWhenNonNullableIsNotEstablished()
            Dim compilation = Compile("
Public Class Customer
    Public Name As String
    Public Title As String?
    Public Note As String
    Public Sub New()
        Name = ""Customer""
        Name = Nothing
    End Sub
    Public Sub New(value As String)
        Name = value
    End Sub
    Public Sub New(ready As Boolean)
        If ready Then
            Name = ""Customer""
        End If
    End Sub
    Public Sub New(throwAway As Integer)
        Throw New System.Exception()
    End Sub
End Class
")
            Dim diagnostics = compilation.GetDiagnostics()
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember AndAlso diagnostic.GetMessage().Contains("Name"))
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullAssignment)
            Assert.DoesNotContain(diagnostics, Function(diagnostic) diagnostic.GetMessage().Contains("'Title'"))
            Assert.DoesNotContain(diagnostics, Function(diagnostic) diagnostic.GetMessage().Contains("'Note'") AndAlso diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember AndAlso diagnostic.Location.GetLineSpan().StartLinePosition.Line < 4)
        End Sub

        <Fact>
        Public Sub NoteField_IsObliviousAndNotRequired()
            Dim compilation = Compile("
#Nullable Disable
Public Class Customer
    Public Note As String
End Class
", nullable:=NullableContextOptions.Enable)
            Assert.DoesNotContain(compilation.GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember)
        End Sub

        <Fact>
        Public Sub MemberInitializers_AreHonoredOnce()
            Dim compilation = Compile("
Public Class Customer
    Private _name As String = ""Customer""
    Private _missing As String = Nothing
    Private _maybe As String = CType(Nothing, String)
    Public Property Name As String = ""Customer""
    Public Property Title As String?
    Public A, B As String = ""Customer""
    Public Sub New()
    End Sub
    Public Sub New(copy As Boolean)
        Me.New()
    End Sub
End Class
")
            Dim messages = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember).Select(Function(diagnostic) diagnostic.GetMessage()).ToArray()
            Assert.Contains(messages, Function(message) message.Contains("_missing"))
            Assert.Contains(messages, Function(message) message.Contains("_maybe"))
            Assert.DoesNotContain(messages, Function(message) message.Contains("_name"))
            Assert.DoesNotContain(messages, Function(message) message.Contains("Name"))
            Assert.DoesNotContain(messages, Function(message) message.Contains("Title"))
        End Sub

        <Fact>
        Public Sub ImplicitConstructor_ReportsAtTheMember()
            Dim compilation = Compile("
Public Class Customer
    Public Name As String
    Public Property Title As String
    Public Note As String?
End Class
")
            Dim diagnostics = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember).ToArray()
            Assert.Equal(2, diagnostics.Length)
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.GetMessage().Contains("Name"))
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.GetMessage().Contains("Title"))
            Assert.DoesNotContain(diagnostics, Function(diagnostic) diagnostic.GetMessage().Contains("k__BackingField"))
        End Sub

        <Fact>
        Public Sub ChainedConstructor_UsesTheTargetSummary()
            Dim compilation = Compile("
Public Class Customer
    Public Name As String
    Public Sub New(name As String)
        Name = name
    End Sub
    Public Sub New()
        Me.New(""Customer"")
    End Sub
    Public Sub New(missing As Boolean)
        Me.New()
        If missing Then
            Name = Nothing
        End If
    End Sub
End Class
")
            Dim diagnostics = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember OrElse diagnostic.Code = ERRID.WRN_PossibleNullAssignment).ToArray()
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullAssignment)
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember)
        End Sub

        <Fact>
        Public Sub DerivedConstructor_DoesNotAssumeBaseInitializesDerivedMembers()
            Dim compilation = Compile("
Public Class Base
    Public BaseName As String = ""Base""
End Class
Public Class Derived
    Inherits Base
    Public Name As String
    Public Sub New()
        MyBase.New()
    End Sub
End Class
")
            Dim diagnostics = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember).ToArray()
            Assert.Single(diagnostics)
            Assert.Contains("Name", diagnostics(0).GetMessage())
        End Sub

        <Fact>
        Public Sub SharedInitialization_UsesSharedConstructors()
            Dim compilation = Compile("
Public Module Catalog
    Public Name As String
    Public Note As String?
    Sub New()
    End Sub
End Module
Public Class Holder
    Public Shared Ready As String = ""yes""
    Public Shared Missing As String
    Shared Sub New()
    End Sub
End Class
")
            Dim messages = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember).Select(Function(diagnostic) diagnostic.GetMessage()).ToArray()
            Assert.Contains(messages, Function(message) message.Contains("Name"))
            Assert.Contains(messages, Function(message) message.Contains("Missing"))
            Assert.DoesNotContain(messages, Function(message) message.Contains("Ready"))
            Assert.DoesNotContain(messages, Function(message) message.Contains("Note"))
        End Sub

        <Fact>
        Public Sub StructureAndGenericBoundaries()
            Dim compilation = Compile("
Public Structure Point
    Public Name As String
    Public Sub New(name As String)
    End Sub
End Structure
Public Class Box(Of T As Class, U)
    Public Item As T
    Public Other As U
    Public Marked As T?
    Public Sub New()
    End Sub
End Class
")
            Dim messages = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember).Select(Function(diagnostic) diagnostic.GetMessage()).ToArray()
            Assert.Contains(messages, Function(message) message.Contains("Name"))
            Assert.Contains(messages, Function(message) message.Contains("Item"))
            Assert.DoesNotContain(messages, Function(message) message.Contains("Other"))
            Assert.DoesNotContain(messages, Function(message) message.Contains("Marked"))
        End Sub

        <Fact>
        Public Sub MemberNotNull_AppliesToTheReceiver()
            Dim compilation = Compile("
Public Class Customer
    Private _name As String?
    <System.Diagnostics.CodeAnalysis.MemberNotNull(""_name"")>
    Private Sub Initialize()
        _name = ""Customer""
    End Sub
    Public Sub Print(other As Customer)
        Dim before = _name.Length
        Initialize()
        Dim after = _name.Length
        other.Initialize()
        Dim otherLength = other._name.Length
        _name = Nothing
        Dim cleared = _name.Length
    End Sub
End Class
")
            Dim references = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullReference).ToArray()
            Assert.Equal(2, references.Length)
        End Sub

        <Fact>
        Public Sub MemberNotNullWhen_SplitsOnTheCondition()
            Dim compilation = Compile("
Public Class Customer
    Private _name As String?
    <System.Diagnostics.CodeAnalysis.MemberNotNullWhen(True, ""_name"")>
    Private Function TryInitialize() As Boolean
        _name = ""Customer""
        Return True
    End Function
    Public Sub Print()
        If TryInitialize() Then
            Dim length = _name.Length
        Else
            Dim missing = _name.Length
        End If
        If Not TryInitialize() Then
            Dim stillMissing = _name.Length
        Else
            Dim present = _name.Length
        End If
    End Sub
End Class
")
            Dim references = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullReference).ToArray()
            Assert.Equal(2, references.Length)
        End Sub

        <Fact>
        Public Sub MemberContracts_AreCheckedAgainstTheBody()
            Dim compilation = Compile("
Public Class Customer
    Private _name As String?
    Private _title As String?
    <System.Diagnostics.CodeAnalysis.MemberNotNull(""_name"")>
    Public Sub Initialize()
    End Sub
    <System.Diagnostics.CodeAnalysis.MemberNotNull(""_name"")>
    Public Sub InitializeWell()
        _name = ""Customer""
    End Sub
    <System.Diagnostics.CodeAnalysis.MemberNotNullWhen(False, ""_title"")>
    Public Function Fail() As Boolean
        Return False
    End Function
    <System.Diagnostics.CodeAnalysis.MemberNotNull(""Missing"")>
    Public Sub Bad()
    End Sub
End Class
")
            Dim diagnostics = compilation.GetDiagnostics()
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.Code = ERRID.WRN_MemberContractNotSatisfied AndAlso diagnostic.GetMessage().Contains("_name"))
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.Code = ERRID.WRN_MemberContractNotSatisfiedWhen AndAlso diagnostic.GetMessage().Contains("_title"))
            Assert.Contains(diagnostics, Function(diagnostic) diagnostic.Code = ERRID.WRN_BadNullableMemberName AndAlso diagnostic.GetMessage().Contains("Missing"))
        End Sub

        <Fact>
        Public Sub LegacyAndDisabledContexts_DoNotWarn()
            Dim source = "
Public Class Customer
    Public Name As String
    Public Sub New()
    End Sub
End Class
"
            Assert.DoesNotContain(CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17).GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember)
            Assert.DoesNotContain(CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable), parseOptions:=VbNext).GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.WRN_UninitializedNonNullableMember)
        End Sub

        <Fact>
        Public Sub ConstructorIL_StaysTheSame()
            Dim source = "
Public Class Customer
    Public Name As String
    Public Sub New()
        Name = ""Customer""
    End Sub
End Class
"
            Dim disabled = MethodIl(source, LanguageVersion.VisualBasic17_13, NullableContextOptions.Disable)
            Dim enabled = MethodIl(source, LanguageVersion.VbNext, NullableContextOptions.Enable)
            Assert.Equal(disabled, enabled)
        End Sub

        <Fact>
        Public Sub CrossLanguage_MemberContractsChangeWarnings()
            Dim vbLibrary = Compile("
Public Class Box
    Public Name As String?
    <System.Diagnostics.CodeAnalysis.MemberNotNull(""Name"")>
    Public Sub Initialize()
        Name = ""Customer""
    End Sub
End Class
")
            vbLibrary.VerifyDiagnostics()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
public class Program
{
    public static void Missing(Box box) { box.Name.ToString(); }
    public static void Present(Box box) { box.Initialize(); box.Name.ToString(); }
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Mscorlib40).Concat({vbLibrary.EmitToImageReference()}).ToArray())
            Assert.Contains(csharp.GetDiagnostics(), Function(diagnostic) diagnostic.Id = "CS8602" AndAlso diagnostic.Location.SourceTree.ToString().Contains("Missing"))
            Dim present = csharp.SyntaxTrees.Single().GetRoot().DescendantNodes().OfType(Of Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax)().Single(Function(method) method.Identifier.ValueText = "Present")
            Assert.DoesNotContain(csharp.GetDiagnostics(), Function(diagnostic) diagnostic.Id = "CS8602" AndAlso present.Span.Contains(diagnostic.Location.SourceSpan))

            Dim reference = CompileCSharp("
#nullable enable
namespace System.Diagnostics.CodeAnalysis
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class MemberNotNullAttribute : System.Attribute
    {
        public MemberNotNullAttribute(string member) {}
        public MemberNotNullAttribute(params string[] members) {}
    }
}
public class Box
{
    public string? Name;
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Name))]
    public void Initialize() { Name = ""Customer""; }
}
")
            Dim consumer = CreateCompilation("
Public Module Program
    Public Sub Missing(box As Box)
        Dim length = box.Name.Length
    End Sub
    Public Sub Present(box As Box)
        box.Initialize()
        Dim length = box.Name.Length
    End Sub
End Module
", references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim warnings = consumer.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code = ERRID.WRN_PossibleNullReference).ToArray()
            Assert.Single(warnings)
            Assert.Contains("Missing", warnings(0).Location.SourceTree.GetText().GetSubText(New Text.TextSpan(0, warnings(0).Location.SourceSpan.End)).ToString())
        End Sub

        Private Function Compile(source As String, Optional nullable As NullableContextOptions = NullableContextOptions.Enable) As VisualBasicCompilation
            Return CreateCompilation({Attributes, source}, options:=TestOptions.ReleaseDll.WithNullableContext(nullable), parseOptions:=VbNext)
        End Function

        Private Function MethodIl(source As String, language As LanguageVersion, nullable As NullableContextOptions) As String
            Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(nullable), parseOptions:=TestOptions.Regular.WithLanguageVersion(language))
            compilation.VerifyDiagnostics()
            Return CompileAndVerify(compilation).VisualizeIL("Customer..ctor")
        End Function

        Private Function CompileCSharp(source As String) As MetadataReference
            Dim compilation = CreateCSharpCompilation(source, parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Mscorlib40).ToArray())
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
            Return compilation.EmitToImageReference()
        End Function
    End Class

End Namespace
