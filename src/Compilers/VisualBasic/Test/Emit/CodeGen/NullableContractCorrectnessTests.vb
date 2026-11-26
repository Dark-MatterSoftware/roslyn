' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableContractCorrectnessTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        <Fact>
        Public Sub NotNullIfNotNull_ReassignmentDoesNotHideOriginal()
            Dim invalid = Compile(Attributes() & "
Public Class Api
    Public Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        value = Nothing
        Return Nothing
    End Function
End Class
")
            Assert.Contains(invalid.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull)

            Dim valid = Compile(Attributes() & "
Public Class Api
    Public Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        If value Is Nothing Then
            value = ""replacement""
            Return Nothing
        End If
        Return value
    End Function
End Class
")
            Assert.False(valid.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull))
        End Sub

        <Fact>
        Public Sub NotNullIfNotNull_DirectReturnAndMultipleNames()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Function Keep(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        Return value
    End Function
    Public Function Either(left As String?, right As String?) As <NotNullIfNotNull(""left""), NotNullIfNotNull(""right"")> String?
        If left IsNot Nothing Then
            Return Nothing
        End If
        Return right
    End Function
End Class
")
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull AndAlso d.GetMessage().Contains("value")))
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull AndAlso d.GetMessage().Contains("left"))
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull AndAlso d.GetMessage().Contains("right"))
        End Sub

        <Fact>
        Public Sub MutualReturn_DoesNotProveItself()
            Dim compilation = Compile("
Public Class Api
    Public Function First(value As String?) As String
        Return Second(value)
    End Function
    Public Function Second(value As String?) As String
        Return First(value)
    End Function
    Public Function Established() As String
        Return ""ready""
    End Function
    Public Function UseEstablished() As String
        Return Established()
    End Function
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_PossibleNullReturn)
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))
        End Sub

        <Fact>
        Public Sub ThreeMethodCycle_AndOutsideCaller()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Sub One(<NotNull> ByRef value As String?)
        Two(value)
    End Sub
    Public Sub Two(<NotNull> ByRef value As String?)
        Three(value)
    End Sub
    Public Sub Three(<NotNull> ByRef value As String?)
        One(value)
    End Sub
    Public Sub Ready(<NotNull> ByRef value As String?)
        value = ""ready""
    End Sub
    Public Sub Use(value As String?)
        Ready(value)
        Dim length = value.Length
    End Sub
End Class
")
            Assert.Equal(3, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_ParameterDisallowsNull))
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_PossibleNullReference))
        End Sub

        <Fact>
        Public Sub DoesNotReturn_NormalExitWarns_ThrowDoesNot()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    <DoesNotReturn>
    Public Sub Always()
        Throw New System.Exception()
    End Sub
    <DoesNotReturn>
    Public Sub Returns()
        Return
    End Sub
    <DoesNotReturn>
    Public Sub Sometimes(flag As Boolean)
        If flag Then
            Throw New System.Exception()
        End If
    End Sub
End Class
")
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) CInt(d.Code) = 42532))
        End Sub

        <Fact>
        Public Sub CSharp_DoesNotReturn_WarnsOnNormalReturn()
            Dim csharpLibrary = CreateCSharpCompilation("
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DoesNotReturnAttribute : Attribute { }
}
public class Api
{
    [DoesNotReturn]
    public void Returns() { return; }
    [DoesNotReturn]
    public void Throws() { throw new System.Exception(); }
}
", parseOptions:=New CSharpParseOptions(CSharp.LanguageVersion.Preview), compilationOptions:=CSharpNullable)
            Dim diagnostics = csharpLibrary.GetDiagnostics()
            Assert.Contains(diagnostics, Function(d) d.Id = "CS8763")
            Assert.Single(diagnostics, Function(d) d.Id = "CS8763")
        End Sub

        <Fact>
        Public Sub PropertyAttributes_StricterFlagWinsAndSetterMismatchIsSeparate()
            Dim combined = Compile(Attributes() & "
Public Class Api
    <MaybeNull, NotNull>
    Public Property Strict As String?
        Get
            Return Nothing
        End Get
        Set(value As String?)
        End Set
    End Property
    <AllowNull, DisallowNull>
    Public Property Required As String?
        Get
            Return Nothing
        End Get
        Set(value As String?)
        End Set
    End Property
    Public Sub Use()
        Required = Nothing
    End Sub
End Class
")
            Assert.Equal(1, combined.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))
            Assert.Contains(combined.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_PossibleNullAssignment)

            Dim mismatched = Compile(Attributes() & "
Public Class Api
    Public Property Name As String
        Get
            Return """"
        End Get
        Set(value As String?)
        End Set
    End Property
End Class
")
            Assert.Contains(mismatched.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullableSetterAnnotationMismatch)

            Dim asymmetric = Compile(Attributes() & "
Public Class Api
    <AllowNull>
    Public Property Name As String?
        Get
            Return Nothing
        End Get
        Set(value As String)
        End Set
    End Property
    Public Sub Use()
        Name = Nothing
    End Sub
End Class
")
            Assert.False(asymmetric.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_NullableSetterAnnotationMismatch))
            Assert.False(asymmetric.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))
            Assert.False(asymmetric.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_PossibleNullAssignment))
        End Sub

        <Fact>
        Public Sub PropertyAttributes_MetadataReimportAndCSharpGetter()
            Dim library = Compile(Attributes() & "
Public Class Api
    <MaybeNull, NotNull>
    Public Property Strict As String?
        Get
            Return """"
        End Get
        Set(value As String?)
        End Set
    End Property
End Class
")
            Dim image = library.EmitToImageReference()
            Dim reimport = CreateCompilation("
Public Class Use
    Public Function Read(api As Api) As String
        Return api.Strict
    End Function
End Class
", references:={image}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim [property] = reimport.GetMember(Of NamedTypeSymbol)("Api").GetMember(Of PropertySymbol)("Strict")
            Assert.True(([property].FlowAnalysisAnnotations And FlowAnalysisAnnotations.NotNull) = FlowAnalysisAnnotations.NotNull)
            Assert.True(([property].FlowAnalysisAnnotations And FlowAnalysisAnnotations.MaybeNull) = FlowAnalysisAnnotations.MaybeNull)
            Assert.False(reimport.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))

            ' C# getter flow annotations are taken from the property. An attribute written only on the getter does not change them.
            Dim getterNotNull = CreateCSharpCompilation(CSharpProperty("public string? Relaxed { [return: NotNull] get { return null; } set { } }"), parseOptions:=New CSharpParseOptions(CSharp.LanguageVersion.Preview), compilationOptions:=CSharpNullable)
            Assert.False(getterNotNull.GetDiagnostics().Any(Function(d) d.Id = "CS8603"))
            Dim getterMaybe = CreateCSharpCompilation(CSharpProperty("public string Strict { [return: MaybeNull] get { return null; } set { } }"), parseOptions:=New CSharpParseOptions(CSharp.LanguageVersion.Preview), compilationOptions:=CSharpNullable)
            Assert.Single(getterMaybe.GetDiagnostics(), Function(d) d.Id = "CS8603")
            ' C# does not warn when MaybeNull and NotNull are both present. VB keeps the stricter NotNull flag.
            Dim both = CreateCSharpCompilation(CSharpProperty("[MaybeNull, NotNull] public string? Combined { get { return null; } set { } }"), parseOptions:=New CSharpParseOptions(CSharp.LanguageVersion.Preview), compilationOptions:=CSharpNullable)
            Assert.False(both.GetDiagnostics().Any(Function(d) d.Id = "CS8603"))
        End Sub

        Private Shared Function CSharpProperty(declaration As String) As String
            Return "
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.ReturnValue)]
    public sealed class MaybeNullAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.ReturnValue)]
    public sealed class NotNullAttribute : Attribute { }
}
public class Api
{
    " & declaration & "
}
"
        End Function

        <Fact>
        Public Sub GenericMutualReturn_DoesNotProveItself()
            Dim compilation = Compile("
Public Class Api
    Public Function First(Of T As Class)(value As T) As T
        Return Second(value)
    End Function
    Public Function Second(Of T As Class)(value As T) As T
        Return First(value)
    End Function
End Class
")
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))
        End Sub

        <Fact>
        Public Sub NotNullIfNotNull_ExitFunctionByRefAndFinally()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Function Keep(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        Keep = value
        value = Nothing
        Exit Function
    End Function
    Public Sub Mirror(<NotNullIfNotNull(""value"")> ByRef result As String?, value As String?)
        result = value
    End Sub
    Public Function AfterNull(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        If value Is Nothing Then
            Try
                Return Nothing
            Finally
            End Try
        End If
        Return value
    End Function
End Class
")
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull))
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_ParameterNotNullIfNotNull))
        End Sub

        <Fact>
        Public Sub RecursiveContracts_EstablishedPathAndInfiniteLoop()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Name As String
    Public Function LoopValue(value As String?) As String
        Return LoopValue(value)
    End Function
    Public Function Never() As String
        While True
            Never()
        End While
    End Function
    <MemberNotNull(""Name"")>
    Public Sub Ready()
        Name = """"
    End Sub
    Public Sub Use()
        Ready()
        Dim length = Name.Length
    End Sub
    <DoesNotReturn>
    Public Sub Hidden()
        Try
            Return
        Finally
            Throw New System.Exception()
        End Try
    End Sub
    Public Sub FailIf(<DoesNotReturnIf(True)> condition As Boolean)
        If condition Then
            Throw New System.Exception()
        End If
    End Sub
End Class
")
            Assert.Equal(1, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_MemberContractNotSatisfied))
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_PossibleNullReference))
            Assert.Equal(0, compilation.GetDiagnostics().Count(Function(d) CInt(d.Code) = 42532))
        End Sub

        <Fact>
        Public Sub CrossLanguage_InterfaceImplementationMatrix()
            Dim csharpLibrary = CreateCSharpCompilation("
#nullable enable
public interface IWorker<T>
{
    T Get(T id);
    string?[] Labels { get; set; }
}
", parseOptions:=New CSharpParseOptions(CSharp.LanguageVersion.Preview), compilationOptions:=CSharpNullable)
            Dim image = csharpLibrary.EmitToImageReference()
            Dim incompatible = CreateCompilation("
Public Class Worker
    Implements IWorker(Of String)
    Public Function Get(id As String?) As String? Implements IWorker(Of String).Get
        Return id
    End Function
    Public Property Labels As String() Implements IWorker(Of String).Labels
        Get
            Return Nothing
        End Get
        Set(value As String())
        End Set
    End Property
End Class
", references:={image}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.Contains(incompatible.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnImplementation)
            Assert.Contains(incompatible.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnImplementation)

            Dim compatible = CreateCompilation("
Public Class Worker
    Implements IWorker(Of String)
    Public Function Get(id As String) As String Implements IWorker(Of String).Get
        Return id
    End Function
    Public Property Labels As String?() Implements IWorker(Of String).Labels
        Get
            Return Nothing
        End Get
        Set(value As String?())
        End Set
    End Property
End Class
", references:={image}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.False(compatible.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_NullabilityMismatchInReturnTypeOnImplementation OrElse d.Code = ERRID.WRN_NullabilityMismatchInParameterTypeOnImplementation))
        End Sub

        <Fact>
        Public Sub Legacy_DoesNotApplyVbNextContractDiagnostics()
            Dim source = Attributes() & "
Public Class Api
    Public Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        value = Nothing
        Return Nothing
    End Function
    <DoesNotReturn>
    Public Sub Returns()
        Return
    End Sub
End Class
"
            Dim legacy = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            Assert.False(legacy.GetDiagnostics().Any(Function(d) CInt(d.Code) = 42532 OrElse d.Code = ERRID.WRN_ReturnNotNullIfNotNull))
        End Sub

        <Fact>
        Public Sub LegacyAndModernIlMatch()
            Const source = "
Module Math
    Function Plus(value As Integer) As Integer
        Return value + 1
    End Function
End Module
"
            Dim legacy = CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)))
            Dim modern = CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext))
            Assert.Equal(legacy.VisualizeIL("Math.Plus"), modern.VisualizeIL("Math.Plus"))
        End Sub

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function

        Private Shared Function Attributes() As String
            Return "
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue, AllowMultiple:=True)>
    Public NotInheritable Class NotNullIfNotNullAttribute
        Inherits Attribute
        Public Sub New(parameterName As String)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property)>
    Public NotInheritable Class AllowNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property)>
    Public NotInheritable Class DisallowNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class MaybeNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Method, AllowMultiple:=True)>
    Public NotInheritable Class MemberNotNullAttribute
        Inherits Attribute
        Public Sub New(member As String)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Method)>
    Public NotInheritable Class DoesNotReturnAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Parameter)>
    Public NotInheritable Class DoesNotReturnIfAttribute
        Inherits Attribute
        Public Sub New(parameterValue As Boolean)
        End Sub
    End Class
End Namespace
"
        End Function
    End Class
End Namespace
