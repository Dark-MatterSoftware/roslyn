' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.CSharp.Syntax
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableAnalysisAttributeTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        Private Const AttributeDefinitions As String = "
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property)>
    Public NotInheritable Class AllowNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property)>
    Public NotInheritable Class DisallowNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class MaybeNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Parameter)>
    Public NotInheritable Class MaybeNullWhenAttribute
        Inherits Attribute
        Public Sub New(returnWhen As Boolean)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Parameter)>
    Public NotInheritable Class NotNullWhenAttribute
        Inherits Attribute
        Public Sub New(returnWhen As Boolean)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue, AllowMultiple:=True)>
    Public NotInheritable Class NotNullIfNotNullAttribute
        Inherits Attribute
        Public Sub New(parameterName As String)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Method Or AttributeTargets.Property, AllowMultiple:=True)>
    Public NotInheritable Class MemberNotNullAttribute
        Inherits Attribute
        Public Sub New(member As String)
        End Sub
        Public Sub New(ParamArray members As String())
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Method Or AttributeTargets.Property, AllowMultiple:=True)>
    Public NotInheritable Class MemberNotNullWhenAttribute
        Inherits Attribute
        Public Sub New(returnWhen As Boolean, member As String)
        End Sub
        Public Sub New(returnWhen As Boolean, ParamArray members As String())
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Method)>
    Public NotInheritable Class DoesNotReturnAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Parameter)>
    Public NotInheritable Class DoesNotReturnIfAttribute
        Inherits Attribute
        Public Sub New(condition As Boolean)
        End Sub
    End Class
End Namespace
"

        Private Const CSharpAttributeDefinitions As String = "
namespace System.Diagnostics.CodeAnalysis
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter | global::System.AttributeTargets.Property)]
    public sealed class AllowNullAttribute : global::System.Attribute { }
    [global::System.AttributeUsage(global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter | global::System.AttributeTargets.Property)]
    public sealed class DisallowNullAttribute : global::System.Attribute { }
    [global::System.AttributeUsage(global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter | global::System.AttributeTargets.Property | global::System.AttributeTargets.ReturnValue)]
    public sealed class MaybeNullAttribute : global::System.Attribute { }
    [global::System.AttributeUsage(global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter | global::System.AttributeTargets.Property | global::System.AttributeTargets.ReturnValue)]
    public sealed class NotNullAttribute : global::System.Attribute { }
    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter)]
    public sealed class MaybeNullWhenAttribute : global::System.Attribute { public MaybeNullWhenAttribute(bool when) { } }
    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter)]
    public sealed class NotNullWhenAttribute : global::System.Attribute { public NotNullWhenAttribute(bool when) { } }
    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter | global::System.AttributeTargets.Property | global::System.AttributeTargets.ReturnValue, AllowMultiple = true)]
    public sealed class NotNullIfNotNullAttribute : global::System.Attribute { public NotNullIfNotNullAttribute(string parameterName) { } }
    [global::System.AttributeUsage(global::System.AttributeTargets.Method | global::System.AttributeTargets.Property, AllowMultiple = true)]
    public sealed class MemberNotNullAttribute : global::System.Attribute
    {
        public MemberNotNullAttribute(params string[] members) { }
        public MemberNotNullAttribute(string member) { }
    }
    [global::System.AttributeUsage(global::System.AttributeTargets.Method | global::System.AttributeTargets.Property, AllowMultiple = true)]
    public sealed class MemberNotNullWhenAttribute : global::System.Attribute
    {
        public MemberNotNullWhenAttribute(bool when, params string[] members) { }
        public MemberNotNullWhenAttribute(bool when, string member) { }
    }
    [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
    public sealed class DoesNotReturnAttribute : global::System.Attribute { public DoesNotReturnAttribute() { } }
    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter)]
    public sealed class DoesNotReturnIfAttribute : global::System.Attribute { public DoesNotReturnIfAttribute(bool condition) { } }
}
"

        <Fact>
        Public Sub SourceRecognition_AllElevenAttributes()
            Dim compilation = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    <AllowNull>
    Public Item As String = """"
    <DisallowNull>
    Public Required As String = """"
    <MaybeNull>
    Public Cached As String = """"
    <NotNull>
    Public Ready As String = """"
    <AllowNull>
    Public Property Name As String = """"
    <MemberNotNull(""Item"")>
    Public Sub Initialize()
        Item = """"
    End Sub
    <MemberNotNullWhen(True, ""Ready"")>
    Public Function TryInitialize() As Boolean
        Return False
    End Function
    <DoesNotReturn>
    Public Sub Fail()
    End Sub
    Public Function Pass(<AllowNull> text As String, <DisallowNull> required As String, <MaybeNullWhen(False)> found As String, <NotNullWhen(True)> ByRef matched As String, <DoesNotReturnIf(False)> condition As Boolean) As <MaybeNull, NotNullIfNotNull(""text"")> String
        matched = text
        Return text
    End Function
End Class
Public Delegate Function Parser(<NotNullIfNotNull(""text"")> text As String) As <NotNull> String
", NullableContextOptions.Enable)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "text").WithArguments("matched").WithLocation(26, 19))
            Dim box = compilation.GetMember(Of NamedTypeSymbol)("Box")
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, box.GetMember(Of FieldSymbol)("Item").FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.DisallowNull, box.GetMember(Of FieldSymbol)("Required").FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.MaybeNull, box.GetMember(Of FieldSymbol)("Cached").FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.NotNull, box.GetMember(Of FieldSymbol)("Ready").FlowAnalysisAnnotations)
            Assert.Equal(SpecialType.System_String, box.GetMember(Of FieldSymbol)("Item").Type.SpecialType)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(box.GetMember(Of FieldSymbol)("Item"), IFieldSymbol).NullableAnnotation)

            Dim name = box.GetMember(Of PropertySymbol)("Name")
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, name.FlowAnalysisAnnotations)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(name, IPropertySymbol).NullableAnnotation)
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, name.SetMethod.Parameters.Last().FlowAnalysisAnnotations)

            Dim initialize = box.GetMember(Of MethodSymbol)("Initialize")
            Assert.Equal({"Item"}, initialize.NotNullMembers)
            Dim tryInitialize = box.GetMember(Of MethodSymbol)("TryInitialize")
            Assert.Equal({"Ready"}, tryInitialize.NotNullWhenTrueMembers)
            Assert.Empty(tryInitialize.NotNullWhenFalseMembers)
            Assert.Equal(FlowAnalysisAnnotations.DoesNotReturn, box.GetMember(Of MethodSymbol)("Fail").FlowAnalysisAnnotations)

            Dim pass = box.GetMember(Of MethodSymbol)("Pass")
            Assert.Equal(FlowAnalysisAnnotations.MaybeNull, pass.ReturnTypeFlowAnalysisAnnotations)
            Assert.Contains("text", pass.ReturnNotNullIfParameterNotNull)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(pass, IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, pass.Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.DisallowNull, pass.Parameters(1).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.MaybeNullWhenFalse, pass.Parameters(2).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.NotNullWhenTrue, pass.Parameters(3).FlowAnalysisAnnotations)
            Assert.True(pass.Parameters(3).IsByRef)
            Assert.Equal(FlowAnalysisAnnotations.DoesNotReturnIfFalse, pass.Parameters(4).FlowAnalysisAnnotations)

            Dim parser = compilation.GetMember(Of NamedTypeSymbol)("Parser")
            Dim invoke = parser.DelegateInvokeMethod
            Assert.Equal(FlowAnalysisAnnotations.NotNull, invoke.ReturnTypeFlowAnalysisAnnotations)
            Assert.Contains("text", invoke.Parameters(0).NotNullIfParameterNotNull)
            Assert.Contains("text", parser.GetMember(Of MethodSymbol)("BeginInvoke").Parameters(0).NotNullIfParameterNotNull)
        End Sub

        <Fact>
        Public Sub SourcePayloads_ConditionsNamesAndInvalidAttributes()
            Dim compilation = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    Public Sub Suppressed(<MaybeNull, MaybeNullWhen(False)> value As String)
    End Sub
    <MemberNotNull(""First"")>
    <MemberNotNull(""Second"", ""Third"")>
    Public Sub Initialize()
    End Sub
    <MemberNotNull(""Missing"")>
    Public Sub InitializeMissing()
    End Sub
    <MemberNotNullWhen(True, ""Left"")>
    <MemberNotNullWhen(False, ""Right"", ""Tail"")>
    Public Function Choose() As Boolean
        Return False
    End Function
    Public Function Both(left As String, right As String) As <NotNullIfNotNull(""left""), NotNullIfNotNull(""right""), NotNullIfNotNull(Nothing)> String
        Return left
    End Function
    Public Sub Other(<Other.AllowNull> value As String)
    End Sub
    Public Sub Wrong(<System.Diagnostics.CodeAnalysis.CountAttribute(1)> value As String)
    End Sub
End Class
Namespace Other
    <System.AttributeUsage(System.AttributeTargets.Parameter)>
    Public Class AllowNullAttribute
        Inherits System.Attribute
    End Class
End Namespace
Namespace System.Diagnostics.CodeAnalysis
    <System.AttributeUsage(System.AttributeTargets.Parameter)>
    Public Class CountAttribute
        Inherits System.Attribute
        Public Sub New(count As Integer)
        End Sub
    End Class
End Namespace
", NullableContextOptions.Disable)

            compilation.VerifyDiagnostics()
            Dim box = compilation.GetMember(Of NamedTypeSymbol)("Box")
            Assert.Equal(FlowAnalysisAnnotations.MaybeNull, box.GetMember(Of MethodSymbol)("Suppressed").Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal({"First", "Second", "Third"}, box.GetMember(Of MethodSymbol)("Initialize").NotNullMembers)
            Assert.Equal({"Missing"}, box.GetMember(Of MethodSymbol)("InitializeMissing").NotNullMembers)
            Dim choose = box.GetMember(Of MethodSymbol)("Choose")
            Assert.Equal({"Left"}, choose.NotNullWhenTrueMembers)
            Assert.Equal({"Right", "Tail"}, choose.NotNullWhenFalseMembers)
            Dim both = box.GetMember(Of MethodSymbol)("Both")
            Assert.Contains("left", both.ReturnNotNullIfParameterNotNull)
            Assert.Contains("right", both.ReturnNotNullIfParameterNotNull)
            Assert.Equal(2, both.ReturnNotNullIfParameterNotNull.Count)
            Assert.Equal(FlowAnalysisAnnotations.None, box.GetMember(Of MethodSymbol)("Other").Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.None, box.GetMember(Of MethodSymbol)("Wrong").Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal("AllowNullAttribute", box.GetMember(Of MethodSymbol)("Other").Parameters(0).GetAttributes().Single().AttributeClass.Name)
        End Sub

        <Fact>
        Public Sub DoesNotReturn_OnParameter_IsRejected()
            Dim compilation = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    Public Sub M(<DoesNotReturn> value As String)
    End Sub
End Class
", NullableContextOptions.Disable)

            compilation.VerifyDiagnostics(Diagnostic(ERRID.ERR_InvalidAttributeUsage2, "DoesNotReturn").WithArguments("DoesNotReturnAttribute", "value"))
            Assert.Equal(FlowAnalysisAnnotations.None, compilation.GetMember(Of MethodSymbol)("Box.M").FlowAnalysisAnnotations)
        End Sub

        <Fact>
        Public Sub Contracts_DoNotInheritAndSurviveSubstitution()
            Dim compilation = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Base
    Public Overridable Sub M(<AllowNull> value As String)
    End Sub
End Class
Public Class Derived
    Inherits Base
    Public Overrides Sub M(value As String)
    End Sub
End Class
Public Interface IContract
    Sub M(<AllowNull> value As String)
End Interface
Public Class Implementation
    Implements IContract
    Public Sub M(value As String) Implements IContract.M
    End Sub
End Class
Public Class Box(Of T)
    Public Sub Take(<DisallowNull> value As T)
    End Sub
End Class
Public Class GenericMethods
    Public Function Pass(Of T)(<NotNullWhen(True)> value As T) As <NotNullIfNotNull(""value"")> T
        Return value
    End Function
End Class
", NullableContextOptions.Disable)

            compilation.VerifyDiagnostics()
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, compilation.GetMember(Of MethodSymbol)("Base.M").Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.None, compilation.GetMember(Of MethodSymbol)("Derived.M").Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, compilation.GetMember(Of MethodSymbol)("IContract.M").Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.None, compilation.GetMember(Of MethodSymbol)("Implementation.M").Parameters(0).FlowAnalysisAnnotations)

            Dim constructed = compilation.GetMember(Of NamedTypeSymbol)("Box").Construct(compilation.GetSpecialType(SpecialType.System_String))
            Dim take = constructed.GetMember(Of MethodSymbol)("Take")
            Assert.Equal(SpecialType.System_String, take.Parameters(0).Type.SpecialType)
            Assert.Equal(FlowAnalysisAnnotations.DisallowNull, take.Parameters(0).FlowAnalysisAnnotations)

            Dim pass = compilation.GetMember(Of MethodSymbol)("GenericMethods.Pass").Construct(compilation.GetSpecialType(SpecialType.System_String))
            Assert.Equal(FlowAnalysisAnnotations.NotNullWhenTrue, pass.Parameters(0).FlowAnalysisAnnotations)
            Assert.Contains("value", pass.ReturnNotNullIfParameterNotNull)
        End Sub

        <Fact>
        Public Sub ImportedMetadata_AllElevenAttributes()
            AssertImportedContracts(TargetFramework.Mscorlib40)
        End Sub

        <Fact>
        Public Sub RoundTrip_ReimportsContractsAndCopiesPropertyAttributesOnVbNext()
            Dim compilation = Compile(LibrarySource, NullableContextOptions.Enable)
            compilation.VerifyDiagnostics()
            Dim imported = Import(compilation, TargetFramework.Mscorlib40)
            imported.VerifyDiagnostics()
            AssertLibraryContracts(imported.GetMember(Of NamedTypeSymbol)("Api"), imported.GetMember(Of NamedTypeSymbol)("Box"))

            Dim legacy = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Api
    Private Shared _name As String = """"
    <AllowNull>
    Public Shared Property Name As String
        Get
            Return _name
        End Get
        Set(value As String)
            _name = value
        End Set
    End Property
    <MaybeNull>
    Public Shared ReadOnly Property Cached As String
        Get
            Return _name
        End Get
    End Property
End Class
", NullableContextOptions.Enable, Vb17)
            legacy.VerifyDiagnostics()
            Dim legacyImage = Import(legacy, TargetFramework.Mscorlib40)
            Dim legacyApi = legacyImage.GetMember(Of NamedTypeSymbol)("Api")
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, legacyApi.GetMember(Of PropertySymbol)("Name").FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.None, legacyApi.GetMember(Of PropertySymbol)("Name").SetMethod.Parameters.Last().FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.None, legacyApi.GetMember(Of PropertySymbol)("Name").GetMethod.ReturnTypeFlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.MaybeNull, legacyApi.GetMember(Of PropertySymbol)("Cached").FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.None, legacyApi.GetMember(Of PropertySymbol)("Cached").GetMethod.ReturnTypeFlowAnalysisAnnotations)
        End Sub

        <Fact>
        Public Sub CSharpConsumer_RecognizesRepresentativeVbContracts()
            Dim reference = Compile(LibrarySource, NullableContextOptions.Enable).EmitToImageReference()
            Dim compilation = CreateCSharpCompilation("
#nullable enable
public static class Use
{
    public static void MaybeNullReturn()
    {
        string value = Api.Maybe();
        Api.Maybe().ToString();
    }
    public static void AllowNullProperty()
    {
        Api.Name = null;
        string value = Api.Name;
    }
    public static void DisallowNullProperty()
    {
        Api.Optional = null;
    }
    public static void NotNullWhen()
    {
        string? found = null;
        if (Api.TryFind(ref found))
        {
            found.ToString();
        }
        else
        {
            found.ToString();
        }
    }
    public static void NotNullIfNotNull()
    {
        string? input = null;
        string fromNull = Api.Pass(input);
        string fromValue = Api.Pass(""text"");
    }
    public static void MemberNotNull(Box box)
    {
        box.Initialize();
        box.Name.ToString();
    }
    public static void MemberNotNullMissing(Box box)
    {
        box.Name.ToString();
    }
    public static void MemberNotNullWhen(Box box)
    {
        if (box.TryInitialize())
        {
            box.Name.ToString();
        }
    }
    public static void DoesNotReturn()
    {
        Api.Fail();
        string value = null;
    }
    public static void DoesNotReturnIf()
    {
        Api.FailIf(false);
        string value = null;
    }
    public static void ReturnsWhenTrue()
    {
        Api.FailIf(true);
        string value = null;
    }
    public static void PlainNull()
    {
        string value = Api.Find();
    }
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Mscorlib40).Concat({reference}).ToArray())

            AssertMethodWarnings(compilation, "MaybeNullReturn", "CS8600", "CS8602")
            AssertMethodWarnings(compilation, "AllowNullProperty")
            AssertMethodWarnings(compilation, "DisallowNullProperty", "CS8625")
            AssertMethodWarnings(compilation, "NotNullWhen", "CS8602")
            AssertMethodWarnings(compilation, "NotNullIfNotNull", "CS8600")
            AssertMethodWarnings(compilation, "MemberNotNull")
            AssertMethodWarnings(compilation, "MemberNotNullMissing")
            AssertMethodWarnings(compilation, "MemberNotNullWhen")
            Dim importedBox = compilation.GetTypeByMetadataName("Box")
            Assert.Contains("MemberNotNullAttribute", importedBox.GetMembers("Initialize").Single().GetAttributes().Select(Function(attribute) attribute.AttributeClass.Name))
            Assert.Contains("MemberNotNullWhenAttribute", importedBox.GetMembers("TryInitialize").Single().GetAttributes().Select(Function(attribute) attribute.AttributeClass.Name))
            AssertMethodWarnings(compilation, "DoesNotReturn", "CS0219")
            AssertMethodWarnings(compilation, "DoesNotReturnIf", "CS0219")
            AssertMethodWarnings(compilation, "ReturnsWhenTrue", "CS0219", "CS8600")
            AssertMethodWarnings(compilation, "PlainNull", "CS8600")
        End Sub

        <Fact>
        Public Sub FlowWarnings_UseUnconditionalContractsOnly()
            Dim enabled = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    Sub Take(<AllowNull> value As String)
    End Sub
    Sub TakeRequired(value As String)
    End Sub
    Sub TakeMaybe(<DisallowNull> value As String?)
    End Sub
    Function Maybe() As <MaybeNull> String
        Return Nothing
    End Function
    Function Plain() As String
        Return Nothing
    End Function
    Function Forced() As <NotNull> String?
        Return Nothing
    End Function
    Sub Use()
        Take(Nothing)
        TakeRequired(Nothing)
        TakeMaybe(Nothing)
        Dim text As String = Maybe()
        Dim ordinary As String = Plain()
    End Sub
End Class
", NullableContextOptions.Enable)

            AssertCodes(enabled,
                ERRID.WRN_PossibleNullArgument,
                ERRID.WRN_PossibleNullArgument,
                ERRID.WRN_PossibleNullAssignment,
                ERRID.WRN_PossibleNullReturn,
                ERRID.WRN_PossibleNullReturn)

            Dim disabled = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    Sub Take(<AllowNull> value As String)
    End Sub
    Sub Use()
        Take(Nothing)
    End Sub
End Class
", NullableContextOptions.Disable)
            disabled.VerifyDiagnostics()

            Dim annotations = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    Sub Take(<AllowNull> value As String)
    End Sub
    Sub Use()
        Take(Nothing)
    End Sub
End Class
", NullableContextOptions.Annotations)
            annotations.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ConditionalContracts_ApplyOnTheCaller()
            Dim compilation = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    Function TryFind(<NotNullWhen(True)> ByRef found As String?) As Boolean
        found = Nothing
        Return False
    End Function
    <DoesNotReturn>
    Sub Fail()
    End Sub
    Sub Use()
        Dim found As String? = Nothing
        Dim before = found.Length
        If TryFind(found) Then
            Dim length = found.Length
        End If
        Fail()
        Dim value As String = Nothing
    End Sub
End Class
", NullableContextOptions.Enable)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "found").WithArguments("found").WithLocation(13, 22))
        End Sub

        <Fact>
        Public Sub LegacyLanguageVersions_KeepContractsWithoutNullableWarnings()
            For Each parseOptions In {Vb17, TestOptions.Regular, TestOptions.Regular.WithLanguageVersion(LanguageVersion.Latest)}
                Dim compilation = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    Sub Take(<AllowNull> value As String)
    End Sub
    Function Maybe() As <MaybeNull> String
        Return Nothing
    End Function
    Sub Use()
        Take(Nothing)
        Dim length = Maybe().Length
    End Sub
End Class
", NullableContextOptions.Enable, parseOptions)

                Assert.Equal(LanguageVersion.VisualBasic17_13, compilation.LanguageVersion)
                compilation.VerifyDiagnostics()
                Assert.Equal(FlowAnalysisAnnotations.AllowNull, compilation.GetMember(Of MethodSymbol)("Box.Take").Parameters(0).FlowAnalysisAnnotations)
                Assert.Equal(FlowAnalysisAnnotations.MaybeNull, compilation.GetMember(Of MethodSymbol)("Box.Maybe").ReturnTypeFlowAnalysisAnnotations)
            Next
        End Sub

        <Theory>
        <InlineData(TargetFramework.Mscorlib40)>
        <InlineData(TargetFramework.NetFramework)>
        <InlineData(TargetFramework.Net100)>
        Public Sub FrameworkTargets_RecognizeSourceAndImportedContracts(target As TargetFramework)
            Dim compilation = Compile("
Imports System.Diagnostics.CodeAnalysis
Public Class Box
    Public Sub Take(<AllowNull> value As String)
    End Sub
    Public Function Maybe() As <MaybeNull> String
        Return Nothing
    End Function
End Class
", NullableContextOptions.Disable, target:=target)
            compilation.VerifyDiagnostics()
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, compilation.GetMember(Of MethodSymbol)("Box.Take").Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.MaybeNull, compilation.GetMember(Of MethodSymbol)("Box.Maybe").ReturnTypeFlowAnalysisAnnotations)

            AssertImportedContracts(target)
        End Sub

        Private Sub AssertImportedContracts(target As TargetFramework)
            Dim reference = CompileCSharp(ImportedLibrarySource, target)
            Dim compilation = CreateCompilation("", references:={reference}, parseOptions:=VbNext, targetFramework:=target)
            compilation.VerifyDiagnostics()
            AssertLibraryContracts(compilation.GetMember(Of NamedTypeSymbol)("Api"), compilation.GetMember(Of NamedTypeSymbol)("Box"))
            Assert.Equal(FlowAnalysisAnnotations.None, compilation.GetMember(Of MethodSymbol)("Derived.M").Parameters(0).FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, compilation.GetMember(Of MethodSymbol)("IContract.M").Parameters(0).FlowAnalysisAnnotations)
        End Sub

        Private Shared Sub AssertLibraryContracts(api As NamedTypeSymbol, box As NamedTypeSymbol)
            Dim maybe = api.GetMember(Of MethodSymbol)("Maybe")
            Assert.Equal(FlowAnalysisAnnotations.MaybeNull, maybe.ReturnTypeFlowAnalysisAnnotations)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(maybe, IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(SpecialType.System_String, maybe.ReturnType.SpecialType)

            Dim name = api.GetMember(Of PropertySymbol)("Name")
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, name.FlowAnalysisAnnotations)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(name, IPropertySymbol).NullableAnnotation)
            Assert.Equal(FlowAnalysisAnnotations.AllowNull, name.SetMethod.Parameters.Last().FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.None, name.GetMethod.FlowAnalysisAnnotations)

            Dim optionalValue = api.GetMember(Of PropertySymbol)("Optional")
            Assert.Equal(FlowAnalysisAnnotations.DisallowNull, optionalValue.FlowAnalysisAnnotations)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(optionalValue, IPropertySymbol).NullableAnnotation)
            Assert.Equal(FlowAnalysisAnnotations.DisallowNull, optionalValue.SetMethod.Parameters.Last().FlowAnalysisAnnotations)

            Dim tryFind = api.GetMember(Of MethodSymbol)("TryFind")
            Assert.Equal(FlowAnalysisAnnotations.NotNullWhenTrue, tryFind.Parameters(0).FlowAnalysisAnnotations)
            Assert.True(tryFind.Parameters(0).IsByRef)
            Dim pass = api.GetMember(Of MethodSymbol)("Pass")
            Assert.Contains("input", pass.ReturnNotNullIfParameterNotNull)
            Assert.Equal(FlowAnalysisAnnotations.DoesNotReturn, api.GetMember(Of MethodSymbol)("Fail").FlowAnalysisAnnotations)
            Assert.Equal(FlowAnalysisAnnotations.DoesNotReturnIfFalse, api.GetMember(Of MethodSymbol)("FailIf").Parameters(0).FlowAnalysisAnnotations)

            Assert.Equal({"Name"}, box.GetMember(Of MethodSymbol)("Initialize").NotNullMembers)
            Assert.Equal({"Name"}, box.GetMember(Of MethodSymbol)("TryInitialize").NotNullWhenTrueMembers)
            Assert.Equal(FlowAnalysisAnnotations.MaybeNullWhenFalse, api.GetMember(Of MethodSymbol)("Read").Parameters(0).FlowAnalysisAnnotations)
        End Sub

        Private Function Compile(source As String, nullable As NullableContextOptions, Optional parseOptions As VisualBasicParseOptions = Nothing, Optional target As TargetFramework = TargetFramework.Mscorlib40) As VisualBasicCompilation
            If parseOptions Is Nothing Then
                parseOptions = VbNext
            End If

            Dim sources = If(target = TargetFramework.Net100, {source}, {AttributeDefinitions, source})
            Return CreateCompilation(sources, options:=TestOptions.ReleaseDll.WithNullableContext(nullable), parseOptions:=parseOptions, targetFramework:=target)
        End Function

        Private Function Import(compilation As VisualBasicCompilation, target As TargetFramework) As VisualBasicCompilation
            Return CreateCompilation("", references:={compilation.EmitToImageReference()}, parseOptions:=VbNext, targetFramework:=target)
        End Function

        Private Function CompileCSharp(source As String, target As TargetFramework) As MetadataReference
            Dim text = If(target = TargetFramework.Net100, source, source.Replace("using System.Diagnostics.CodeAnalysis;", "using System.Diagnostics.CodeAnalysis;" & CSharpAttributeDefinitions))
            Dim compilation = CreateCSharpCompilation(text, parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(target).ToArray())
            Dim errors = compilation.GetDiagnostics().Where(Function(diagnostic) diagnostic.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 0, String.Join(Environment.NewLine, errors.Select(Function(diagnostic) diagnostic.ToString())))
            Return compilation.EmitToImageReference()
        End Function

        Private Shared Sub AssertCodes(compilation As VisualBasicCompilation, ParamArray codes As ERRID())
            Dim actual = compilation.GetDiagnostics().Select(Function(diagnostic) CType(diagnostic.Code, ERRID)).OrderBy(Function(code) code).ToArray()
            Assert.Equal(codes.OrderBy(Function(code) code).ToArray(), actual)
        End Sub

        Private Shared Sub AssertMethodWarnings(compilation As CSharpCompilation, methodName As String, ParamArray expected As String())
            Dim method = compilation.SyntaxTrees.Single().GetRoot().DescendantNodes().OfType(Of MethodDeclarationSyntax)().Single(Function(candidate) candidate.Identifier.ValueText = methodName)
            Dim actual = compilation.GetDiagnostics().
                Where(Function(diagnostic) diagnostic.Location.IsInSource AndAlso method.Span.Contains(diagnostic.Location.SourceSpan) AndAlso diagnostic.Severity >= DiagnosticSeverity.Warning).
                Select(Function(diagnostic) diagnostic.Id).
                OrderBy(Function(id) id).
                ToArray()
            Assert.Equal(expected.OrderBy(Function(id) id).ToArray(), actual)
        End Sub

        Private Const LibrarySource As String = "
Imports System
Imports System.Diagnostics.CodeAnalysis
Public Class Api
    Public Shared Function Maybe() As <MaybeNull> String
        Return Nothing
    End Function
    Public Shared Function Find() As String?
        Return Nothing
    End Function
    Private Shared _name As String = """"
    <AllowNull>
    Public Shared Property Name As String
        Get
            Return _name
        End Get
        Set(value As String)
            _name = If(value, """")
        End Set
    End Property
    Private Shared _optional As String? = Nothing
    <DisallowNull>
    Public Shared Property [Optional] As String?
        Get
            Return _optional
        End Get
        Set(value As String?)
            _optional = value
        End Set
    End Property
    Public Shared Function TryFind(<NotNullWhen(True)> ByRef found As String?) As Boolean
        found = Nothing
        Return False
    End Function
    Public Shared Function Pass(input As String?) As <NotNullIfNotNull(""input"")> String?
        Return input
    End Function
    <DoesNotReturn>
    Public Shared Sub Fail()
        Throw New Exception()
    End Sub
    Public Shared Sub FailIf(<DoesNotReturnIf(False)> condition As Boolean)
        If Not condition Then
            Throw New Exception()
        End If
    End Sub
    Public Shared Sub Read(<MaybeNullWhen(False)> value As String)
    End Sub
End Class
Public Class Box
    Public Name As String = """"
    <MemberNotNull(""Name"")>
    Public Sub Initialize()
        Name = """"
    End Sub
    <MemberNotNullWhen(True, ""Name"")>
    Public Function TryInitialize() As Boolean
        Name = """"
        Return True
    End Function
End Class
"

        Private Const ImportedLibrarySource As String = "
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
public class Api
{
    [return: MaybeNull]
    public static string Maybe() => null;
    public static string? Find() => null;
    [AllowNull]
    public static string Name { get; set; }
    [DisallowNull]
    public static string? Optional { get; set; }
    public static bool TryFind([NotNullWhen(true)] ref string? found) { found = null; return false; }
    [return: NotNullIfNotNull(""input"")]
    public static string? Pass(string? input) => input;
    [DoesNotReturn]
    public static void Fail() => throw new Exception();
    public static void FailIf([DoesNotReturnIf(false)] bool condition) { if (!condition) throw new Exception(); }
    public static void Read([MaybeNullWhen(false)] string value) { }
}
public class Box
{
    public string Name;
    [MemberNotNull(""Name"")]
    public void Initialize() { Name = """"; }
    [MemberNotNullWhen(true, ""Name"")]
    public bool TryInitialize() { Name = """"; return true; }
}
public class Base
{
    public virtual void M([AllowNull] string value) { }
}
public class Derived : Base
{
    public override void M(string value) { }
}
public interface IContract
{
    void M([AllowNull] string value);
}
"
    End Class

End Namespace
