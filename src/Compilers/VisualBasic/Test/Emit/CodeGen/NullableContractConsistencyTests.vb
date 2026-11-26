' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableContractConsistencyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        <Fact>
        Public Sub Override_ReturnAndParameterAnnotations()
            Dim compilation = Compile("
Public MustInherit Class Base
    Public Overridable Function GetName(id As String) As String
        Return id
    End Function
    Public Overridable Function Find(id As String?) As String?
        Return id
    End Function
    Public Overridable Sub Take(id As String, extra As String?)
    End Sub
    Public MustOverride Function AbstractName() As String
End Class
Public Class Derived
    Inherits Base
    Public Overrides Function GetName(id As String?) As String?
        Return id
    End Function
    Public Overrides Function Find(id As String) As String
        Return id
    End Function
    Public Overrides Sub Take(id As String?, extra As String)
    End Sub
    Public Overrides Function AbstractName() As String?
        Return Nothing
    End Function
End Class
Public Class Further
    Inherits Derived
    Public Overrides Function Find(id As String) As String
        Return id
    End Function
End Class
")
            AssertCodes(compilation, ERRID.WRN_NullabilityMismatchInReturnTypeOnOverride, ERRID.WRN_NullabilityMismatchInParameterTypeOnOverride)
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnOverride AndAlso d.GetMessage().Contains("id"))
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnOverride AndAlso d.GetMessage().Contains("extra"))
        End Sub

        <Fact>
        Public Sub Override_MatchingAndOblivious()
            Dim compilation = Compile("
Public Class Base
    Public Overridable Function Same(id As String?) As String?
        Return id
    End Function
End Class
Public Class Derived
    Inherits Base
    Public Overrides Function Same(id As String?) As String?
        Return id
    End Function
End Class
", nullable:=NullableContextOptions.Disable)
            Assert.False(compilation.GetDiagnostics().Any(Function(d) IsContract(d.Code)))

            Dim mixed = CreateCompilation("
Public Class Annotated
    Public Overridable Function Name(id As String) As String
        Return id
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable), parseOptions:=VbNext)
            Dim image = mixed.EmitToImageReference()
            Dim consumer = CreateCompilation("
Public Class Derived
    Inherits Annotated
    Public Overrides Function Name(id As String?) As String?
        Return id
    End Function
End Class
", references:={image}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.False(consumer.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnOverride))
        End Sub

        <Fact>
        Public Sub Override_GenericAndConstructed()
            Dim compilation = Compile("
Public Class Base(Of T)
    Public Overridable Function Box(value As T) As T
        Return value
    End Function
End Class
Public Class Derived(Of T As Class)
    Inherits Base(Of T)
    Public Overrides Function Box(value As T?) As T?
        Return value
    End Function
End Class
Public Class Strings
    Inherits Base(Of String)
    Public Overrides Function Box(value As String?) As String?
        Return value
    End Function
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnOverride)
        End Sub

        <Fact>
        Public Sub Interface_ImplementsByDifferentName()
            Dim compilation = Compile("
Public Interface IRepository
    Function GetName() As String
End Interface
Public Class Repository
    Implements IRepository
    Public Function FetchName() As String? Implements IRepository.GetName
        Return Nothing
    End Function
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnImplementation)
        End Sub

        <Fact>
        Public Sub Interface_OneMethodTwoContracts()
            Dim compilation = Compile("
Public Interface ILeft
    Function GetName() As String
End Interface
Public Interface IRight
    Inherits ILeft
    Function GetName() As String?
End Interface
Public Class Store
    Implements ILeft, IRight
    Public Function FetchName() As String? Implements ILeft.GetName, IRight.GetName
        Return Nothing
    End Function
End Class
")
            Dim mismatches = compilation.GetDiagnostics().Where(Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnImplementation).ToArray()
            Assert.Single(mismatches)
        End Sub

        <Fact>
        Public Sub Interface_Properties()
            Dim compilation = Compile("
Public Interface IData
    ReadOnly Property Name As String
    WriteOnly Property Title As String
    Property Item(index As String?) As String
End Interface
Public Class Data
    Implements IData
    Public ReadOnly Property Fetch As String? Implements IData.Name
        Get
            Return Nothing
        End Get
    End Property
    Public WriteOnly Property Heading As String? Implements IData.Title
        Set(value As String?)
        End Set
    End Property
    Public Property Slot(index As String) As String? Implements IData.Item
        Get
            Return Nothing
        End Get
        Set(value As String?)
        End Set
    End Property
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnImplementation)
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnImplementation)
        End Sub

        <Fact>
        Public Sub ByRef_AndAttributes()
            Dim compilation = Compile(Attributes() & "
Public Class Base
    Public Overridable Sub Update(ByRef value As String)
    End Sub
    Public Overridable Sub Relax(<AllowNull> ByRef value As String)
    End Sub
    Public Overridable Sub Keep(ByRef value As String?)
    End Sub
End Class
Public Class Derived
    Inherits Base
    Public Overrides Sub Update(ByRef value As String?)
    End Sub
    Public Overrides Sub Relax(ByRef value As String?)
    End Sub
    Public Overrides Sub Keep(<NotNull> ByRef value As String?)
    End Sub
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnOverride AndAlso d.GetMessage().Contains("value"))
            Dim relax = compilation.GetDiagnostics().Where(Function(d) d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnOverride AndAlso d.Location.SourceSpan.Start > 0).ToArray()
            Assert.True(relax.Length >= 1)
        End Sub

        <Fact>
        Public Sub Nested_Variance()
            Dim compilation = Compile("
Imports System.Collections.Generic
Public Interface IOut(Of Out T)
    Function GetItem() As T
End Interface
Public Interface IIn(Of In T)
    Sub Take(value As T)
End Interface
Public Class Holder(Of T)
    Public Overridable Function Wrap(value As T) As T
        Return value
    End Function
End Class
Public Class Derived
    Inherits Holder(Of IEnumerable(Of String))
    Public Overrides Function Wrap(value As IEnumerable(Of String?)) As IEnumerable(Of String?)
        Return value
    End Function
    Public Overridable Function Arrays(values As String?()) As String()
        Return Nothing
    End Function
End Class
Public Class More
    Inherits Derived
    Public Overrides Function Arrays(values As String()) As String()?
        Return values
    End Function
End Class
Public Class Contra
    Implements IIn(Of String)
    Public Sub Take(value As String?) Implements IIn(Of String).Take
    End Sub
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnOverride)
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnOverride)
        End Sub

        <Fact>
        Public Sub PropertySetter_ExplicitAnnotation()
            Dim compilation = Compile(Attributes() & "
Public Class Data
    Public Property Name As String
        Get
            Return ""x""
        End Get
        Set(value As String?)
        End Set
    End Property
    Public Property Title As String?
        Get
            Return Nothing
        End Get
        Set(<DisallowNull> value As String)
        End Set
    End Property
    Public Property Note As String = ""
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullableSetterAnnotationMismatch AndAlso d.GetMessage().Contains("Name"))
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_NullableSetterAnnotationMismatch AndAlso d.GetMessage().Contains("Title")))
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_NullableSetterAnnotationMismatch AndAlso d.GetMessage().Contains("Note")))
        End Sub

        <Fact>
        Public Sub Legacy_DoesNotWarn()
            Dim compilation = CreateCompilation("
Public Class Base
    Public Overridable Function Name() As String
        Return ""x""
    End Function
End Class
Public Class Derived
    Inherits Base
    Public Overrides Function Name() As String
        Return Nothing
    End Function
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=Vb17)
            Assert.False(compilation.GetDiagnostics().Any(Function(d) IsContract(d.Code)))
        End Sub

        <Fact>
        Public Sub CrossLanguage_CSharpBase()
            Dim csharpLibrary = CreateCSharpCompilation("
#nullable enable
public class Base
{
    public virtual string GetName(string id) => id;
    public virtual void Update(ref string value) { }
}
public interface IBox<out T> { T Value { get; } }
", parseOptions:=New CSharpParseOptions(CSharp.LanguageVersion.Preview), compilationOptions:=CSharpNullable)
            Dim image = csharpLibrary.EmitToImageReference()
            Dim vb = CreateCompilation("
Public Class Derived
    Inherits Base
    Public Overrides Function GetName(id As String?) As String?
        Return id
    End Function
    Public Overrides Sub Update(ByRef value As String?)
    End Sub
End Class
Public Class Box
    Implements IBox(Of String)
    Public ReadOnly Property Value As String? Implements IBox(Of String).Value
        Get
            Return Nothing
        End Get
    End Property
End Class
", references:={image}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.Contains(vb.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnOverride)
            Assert.Contains(vb.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnOverride)
            Assert.Contains(vb.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnImplementation)
        End Sub

        <Fact>
        Public Sub CrossLanguage_VbMetadataConsumedByCSharpAndVb()
            Dim library = Compile("
Public Class Base
    Public Overridable Function GetName(id As String) As String
        Return id
    End Function
End Class
")
            Dim image = library.EmitToImageReference()
            Dim csharpLibrary = CreateCSharpCompilation("
#nullable enable
public class Derived : Base
{
    public override string? GetName(string? id) => id;
}
public class Match : Base
{
    public override string GetName(string id) => id;
}
", parseOptions:=New CSharpParseOptions(CSharp.LanguageVersion.Preview), compilationOptions:=CSharpNullable, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Standard).Concat({image}).ToArray())
            Dim csharpDiagnostics = csharpLibrary.GetDiagnostics()
            Assert.Single(csharpDiagnostics, Function(d) d.Id = "CS8764")

            Dim reimport = CreateCompilation("
Public Class Derived
    Inherits Base
    Public Overrides Function GetName(id As String?) As String?
        Return id
    End Function
End Class
", references:={image}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.Contains(reimport.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnOverride)
        End Sub

        Private Shared Function IsContract(code As Integer) As Boolean
            Return code >= 42521 AndAlso code <= 42526
        End Function

        Private Function Compile(source As String, Optional nullable As NullableContextOptions = NullableContextOptions.Enable) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(nullable), parseOptions:=VbNext)
        End Function

        Private Shared Sub AssertCodes(compilation As VisualBasicCompilation, ParamArray codes() As ERRID)
            Dim actual = compilation.GetDiagnostics()
            For Each code In codes
                Assert.Contains(actual, Function(d) d.Code = code)
            Next
        End Sub

        Private Shared Function Attributes() As String
            Return "
Imports System.Diagnostics.CodeAnalysis
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
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Method)>
    Public NotInheritable Class DoesNotReturnAttribute
        Inherits Attribute
    End Class
End Namespace
"
        End Function
    End Class
End Namespace
