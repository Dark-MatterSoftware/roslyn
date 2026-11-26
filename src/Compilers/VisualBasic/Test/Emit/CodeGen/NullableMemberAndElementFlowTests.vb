' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableMemberAndElementFlowTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        Private Const Attributes As String = "
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class AllowNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue)>
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
End Namespace
"

        <Fact>
        Public Sub Field_UnguardedDereference_Warns()
            Dim compilation = Compile("
Class Holder
    Private _value As String?
    Public Sub Print()
        Dim length = _value.Length
    End Sub
End Class")
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "_value").WithArguments("_value").WithLocation(5, 22))
        End Sub

        <Fact>
        Public Sub Field_NarrowedByIsNotNothing_IsWarningFree()
            Compile("
Class Holder
    Private _value As String?
    Public Sub Print()
        If _value IsNot Nothing Then
            Dim length = _value.Length
        End If
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Field_NarrowedByEarlyReturn_IsWarningFree()
            Compile("
Class Holder
    Private _value As String?
    Public Sub Print()
        If _value Is Nothing Then
            Return
        End If
        Dim length = _value.Length
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Field_AssignmentOfNothing_ClearsNarrowing()
            Compile("
Class Holder
    Private _value As String?
    Public Sub Print()
        If _value IsNot Nothing Then
            _value = Nothing
            Dim length = _value.Length
        End If
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "_value").WithArguments("_value").WithLocation(7, 26))
        End Sub

        <Fact>
        Public Sub Field_MaybeNullAssignedToNonNullField_Warns()
            Compile("
Class Holder
    Public Ready As String
    Public Sub SetValue(value As String?)
        Ready = value
        Dim length = Ready.Length
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "Ready").WithArguments("Ready").WithLocation(3, 12),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "value").WithArguments("Ready").WithLocation(5, 17),
                Diagnostic(ERRID.WRN_PossibleNullReference, "Ready").WithArguments("Ready").WithLocation(6, 22))
        End Sub

        <Fact>
        Public Sub Field_NonNullAssignment_AllowsDereference()
            Compile("
Class Holder
    Private _value As String?
    Public Sub Print()
        _value = ""ready""
        Dim length = _value.Length
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Field_Reassignment_InvalidatesNonNullFact()
            Compile("
Class Holder
    Private _value As String?
    Public Sub Print(value As String?)
        _value = ""ready""
        _value = value
        Dim length = _value.Length
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "_value").WithArguments("_value").WithLocation(7, 22))
        End Sub

        <Fact>
        Public Sub Field_BranchJoin_DropsOneSidedFact()
            Compile("
Class Holder
    Private _value As String?
    Public Sub Print(flag As Boolean)
        If flag Then
            _value = ""ready""
        End If
        Dim length = _value.Length
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "_value").WithArguments("_value").WithLocation(8, 22))
        End Sub

        <Fact>
        Public Sub Field_ShortCircuit_KeepsStableFact()
            Compile("
Class Holder
    Private _value As String?
    Public Sub Print()
        If _value IsNot Nothing AndAlso _value.Length > 0 Then
            Dim length = _value.Length
        End If
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Field_MeAndUnqualified_ShareState()
            Compile("
Class Holder
    Private _value As String?
    Public Sub Print()
        If Me._value IsNot Nothing Then
            Dim length = _value.Length
        End If
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Field_DistinctReceivers_DoNotShareState()
            Compile("
Class Holder
    Public Value As String?
End Class
Module Program
    Public Sub Run()
        Dim first As New Holder()
        Dim second As New Holder()
        first.Value = ""ready""
        second.Value = Nothing
        Dim kept = first.Value.Length
        Dim dropped = second.Value.Length
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "second.Value").WithArguments("Value").WithLocation(12, 23))
        End Sub

        <Fact>
        Public Sub Field_ReceiverReassignment_DropsMemberFact()
            Compile("
Class Holder
    Public Value As String?
End Class
Module Program
    Public Sub Run()
        Dim first As New Holder()
        first.Value = ""ready""
        first = New Holder()
        Dim length = first.Value.Length
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(10, 22))
        End Sub

        <Fact>
        Public Sub Field_AliasMutation_DropsGuarantee()
            Compile("
Class Holder
    Public Value As String?
End Class
Module Program
    Public Sub Run()
        Dim first As New Holder()
        Dim aliasValue = first
        If first.Value IsNot Nothing Then
            aliasValue.Value = Nothing
            Dim length = first.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(11, 26))
        End Sub

        <Fact>
        Public Sub Field_NestedReplacement_DropsDescendant()
            Compile("
Class Holder
    Public Value As String?
End Class
Class Box
    Public Inner As Holder
End Class
Module Program
    Public Sub Run()
        Dim box As New Box()
        box.Inner = New Holder()
        box.Inner.Value = ""ready""
        box.Inner = New Holder()
        Dim length = box.Inner.Value.Length
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "Inner").WithArguments("Inner").WithLocation(6, 12),
                Diagnostic(ERRID.WRN_PossibleNullReference, "box.Inner.Value").WithArguments("Value").WithLocation(14, 22))
        End Sub

        <Fact>
        Public Sub Field_CallOnReceiver_DropsMemberFact()
            Compile("
Class Holder
    Public Value As String?
    Public Sub Reset()
        Value = Nothing
    End Sub
End Class
Module Program
    Public Sub Run()
        Dim first As New Holder()
        first.Value = ""ready""
        first.Reset()
        Dim length = first.Value.Length
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(13, 22))
        End Sub

        <Fact>
        Public Sub Field_InstanceCall_DropsThisFieldFact()
            Compile("
Class Holder
    Private _customer As String?
    Public Sub Print()
        If _customer IsNot Nothing Then
            ResetCustomer()
            Dim length = _customer.Length
        End If
    End Sub
    Private Sub ResetCustomer()
        _customer = Nothing
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "_customer").WithArguments("_customer").WithLocation(7, 26))
        End Sub

        <Fact>
        Public Sub Field_SharedAccess_IsOneSlot()
            Compile("
Class Holder
    Public Shared Value As String?
    Public Sub Print()
        Holder.Value = ""ready""
        Dim length = Value.Length
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Field_ReadOnlyAndGeneric_UseDeclaredAndFlowState()
            Compile("
Class Holder(Of T As Class)
    Public ReadOnly Item As T
    Public Slot As T
    Public Sub New(item As T)
        Me.Item = item
    End Sub
    Public Sub Print()
        Dim text = Item.ToString()
    End Sub
    Public Sub PrintMaybe(value As T?)
        Slot = value
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "End Sub").WithArguments("Slot").WithLocation(7, 5),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "value").WithArguments("Slot").WithLocation(12, 16))
        End Sub

        <Fact>
        Public Sub Property_AutoGetter_RetainsNarrowing()
            Compile("
Class Customer
    Public Property Name As String?
    Public Sub Print()
        If Name IsNot Nothing Then
            Dim length = Name.Length
        End If
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Property_ExplicitGetter_DoesNotRetainNarrowing()
            Compile("
Class Customer
    Private _name As String?
    Public ReadOnly Property Name As String?
        Get
            Return _name
        End Get
    End Property
    Public Sub Print()
        If Name IsNot Nothing Then
            Dim length = Name.Length
        End If
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "Name").WithArguments("Name").WithLocation(11, 26))
        End Sub

        <Fact>
        Public Sub Property_MaybeNullGetter_OverridesNonNullDeclaration()
            Compile(Attributes & "
Class Customer
    <MaybeNull>
    Public ReadOnly Property Name As String
        Get
            Return Nothing
        End Get
    End Property
    Public Sub Print()
        Dim length = Name.Length
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "Name").WithArguments("Name").WithLocation(30, 22))
        End Sub

        <Fact>
        Public Sub Property_NotNullGetter_AllowsDereference()
            Compile(Attributes & "
Class Customer
    <NotNull>
    Public ReadOnly Property Name As String?
        Get
            Return ""ready""
        End Get
    End Property
    Public Sub Print()
        Dim length = Name.Length
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Property_SetterContracts_AllowAndDisallowNull()
            Compile(Attributes & "
Class Customer
    Private _name As String
    Public Property Name As String
        Get
            Return _name
        End Get
        Set(<AllowNull> value As String)
            _name = If(value, """")
        End Set
    End Property
    Public Property Title As String?
        Get
            Return Name
        End Get
        Set(<DisallowNull> value As String?)
            Name = value
        End Set
    End Property
    Public Sub Print(value As String?)
        Name = Nothing
        Title = value
        Title = Nothing
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "_name").WithArguments("_name").WithLocation(23, 13),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "value").WithArguments("Title").WithLocation(42, 17),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("Title").WithLocation(43, 17))
        End Sub

        <Fact>
        Public Sub Property_AutoSetter_RejectsNothingForNonNull()
            Compile("
Class Customer
    Public Property Name As String
    Public Sub Print(value As String?)
        Name = Nothing
        Name = value
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "Public Property Name As String").WithArguments("Name").WithLocation(3, 5),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("Name").WithLocation(5, 16),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "value").WithArguments("Name").WithLocation(6, 16))
        End Sub

        <Fact>
        Public Sub Property_CompoundAssignment_UsesSetterContract()
            Compile("
Class Customer
    Public Property Name As String?
    Public Sub Print()
        Name &= ""x""
        Dim length = Name.Length
    End Sub
End Class").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Member_TryCatch_PreservesNullAssignment()
            Compile("
Class Holder
    Private _value As String
    Public Sub Print()
        Try
            _value = Nothing
        Catch ex As System.Exception
            Dim length = _value.Length
        End Try
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "_value").WithArguments("_value").WithLocation(3, 13),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("_value").WithLocation(6, 22),
                Diagnostic(ERRID.WRN_PossibleNullReference, "_value").WithArguments("_value").WithLocation(8, 26))
        End Sub

        <Fact>
        Public Sub Array_ReceiverAndElement_AreDistinct()
            Compile("
Module Program
    Public Sub Run(items As String?(), slot As String(), missing As String?()?)
        Dim nullableElement = items(0)
        Dim elementLength = nullableElement.Length
        Dim stable = slot(0).Length
        Dim receiverLength = missing(0).Length
        If missing IsNot Nothing Then
            Dim inner = missing(0)
            Dim innerLength = inner.Length
        End If
        slot(0) = Nothing
        items(0) = Nothing
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "nullableElement").WithArguments("nullableElement").WithLocation(5, 29),
                Diagnostic(ERRID.WRN_PossibleNullReference, "missing").WithArguments("missing").WithLocation(7, 30),
                Diagnostic(ERRID.WRN_PossibleNullReference, "missing(0)").WithArguments("missing(0)").WithLocation(7, 30),
                Diagnostic(ERRID.WRN_PossibleNullReference, "inner").WithArguments("inner").WithLocation(10, 31),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("slot(0)").WithLocation(12, 19))
        End Sub

        <Fact>
        Public Sub Array_OneAssignment_DoesNotClearEveryElement()
            Compile("
Module Program
    Public Sub Run(items As String())
        items(0) = ""ready""
        Dim length = items(1).Length
    End Sub
End Module").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Array_JaggedAnnotations()
            Compile("
Module Program
    Public Sub Run(rows As String?()())
        Dim row = rows(0)
        Dim length = row(0).Length
        If row IsNot Nothing AndAlso row(0) IsNot Nothing Then
            Dim kept = row(0).Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "row(0)").WithArguments("row(0)").WithLocation(5, 22),
                Diagnostic(ERRID.WRN_PossibleNullReference, "row(0)").WithArguments("row(0)").WithLocation(7, 24))
        End Sub

        <Fact>
        Public Sub DefaultProperty_SetterAndGetter()
            Compile("
Class Box
    Private _item As String?
    Default Public Property Item(index As Integer) As String
        Get
            Return If(_item, """")
        End Get
        Set(value As String)
            _item = value
        End Set
    End Property
    Public Sub Print(box As Box)
        box(0) = Nothing
        Dim length = box(1).Length
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("Item").WithLocation(13, 18))
        End Sub

        <Fact>
        Public Sub GenericList_ElementAnnotation_WarnsOnDereference()
            Compile("
Imports System.Collections.Generic
Module Program
    Public Sub Run(items As List(Of String?))
        Dim item = items(0)
        Dim length = item.Length
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "item").WithArguments("item").WithLocation(6, 22))
        End Sub

        <Fact>
        Public Sub GenericDictionaryAndNestedList_KeepElementAnnotations()
            Compile("
Imports System.Collections.Generic
Class Customer
    Public Name As String
End Class
Module Program
    Public Sub Run(map As Dictionary(Of String, Customer?), rows As List(Of List(Of String?)?))
        Dim customer = map(""a"")
        Dim name = customer.Name
        Dim row = rows(0)
        If row IsNot Nothing Then
            Dim item = row(0)
            Dim length = item.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "Name").WithArguments("Name").WithLocation(4, 12),
                Diagnostic(ERRID.WRN_PossibleNullReference, "customer").WithArguments("customer").WithLocation(9, 20),
                Diagnostic(ERRID.WRN_PossibleNullReference, "item").WithArguments("item").WithLocation(13, 26))
        End Sub

        <Fact>
        Public Sub ForEach_ElementState_FollowsCollectionAnnotation()
            Compile("
Imports System.Collections.Generic
Class Customer
    Public Name As String
End Class
Module Program
    Public Sub Arrays(stable As String(), loose As String?())
        For Each item In stable
            Dim length = item.Length
        Next
        For Each item In loose
            Dim length = item.Length
        Next
    End Sub
    Public Sub Sequence(items As IEnumerable(Of Customer?))
        For Each customer In items
            Dim name = customer.Name
        Next
        For Each customer As Customer In items
            Dim name = customer.Name
        Next
    End Sub
    Public Sub Missing(items As String?())
        If items Is Nothing Then
            Return
        End If
        For Each item In items
            Dim length = item.Length
        Next
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "Name").WithArguments("Name").WithLocation(4, 12),
                Diagnostic(ERRID.WRN_PossibleNullReference, "item").WithArguments("item").WithLocation(12, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "customer").WithArguments("customer").WithLocation(17, 24),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "items").WithArguments("customer").WithLocation(19, 42),
                Diagnostic(ERRID.WRN_PossibleNullReference, "customer").WithArguments("customer").WithLocation(20, 24),
                Diagnostic(ERRID.WRN_PossibleNullReference, "item").WithArguments("item").WithLocation(28, 26))
        End Sub

        <Fact>
        Public Sub AwaitResult_UsesTaskTypeArgument()
            Compile("
Imports System.Threading.Tasks
Module Program
    Public Async Function Run(loose As Task(Of String?), ready As Task(Of String)) As Task
        Dim text = Await loose
        Dim length = text.Length
        Dim stable = Await ready
        Dim kept = stable.Length
    End Function
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "text").WithArguments("text").WithLocation(6, 22))
        End Sub

        <Fact>
        Public Sub LegacyLanguage_DoesNotWarn()
            Dim compilation = CreateCompilation("
Class Holder
    Private _value As String
    Public Sub Print(value As String)
        _value = Nothing
        Dim length = _value.Length
    End Sub
End Class", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17)
            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub DisabledNullableContext_DoesNotWarn()
            Dim compilation = CreateCompilation("
Class Holder
    Private _value As String
    Public Sub Print()
        _value = Nothing
        Dim length = _value.Length
    End Sub
End Class", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable), parseOptions:=VbNext)
            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ImportedCSharpFieldAndProperty_UseMetadataContracts()
            Dim reference = CreateCSharpCompilation("
#nullable enable
public class Holder
{
    public string? Value;
    public string Name { get; set; } = """";
    public string? Cached => Value;
}
", parseOptions:=New CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview), compilationOptions:=CSharpNullable).EmitToImageReference()

            Dim compilation = CreateCompilation("
Module Program
    Public Sub Run(holder As Holder, value As String?)
        holder.Value = Nothing
        Dim length = holder.Value.Length
        holder.Name = value
        Dim cached = holder.Cached.Length
    End Sub
End Module", references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "holder.Value").WithArguments("Value").WithLocation(5, 22),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "value").WithArguments("Name").WithLocation(6, 23),
                Diagnostic(ERRID.WRN_PossibleNullReference, "holder.Cached").WithArguments("Cached").WithLocation(7, 22))
        End Sub

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function
    End Class

End Namespace
