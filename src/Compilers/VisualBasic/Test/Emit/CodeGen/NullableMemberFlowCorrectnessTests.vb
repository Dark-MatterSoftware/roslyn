' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableMemberFlowCorrectnessTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const Attributes As String = "
Namespace System.Diagnostics.CodeAnalysis
    <System.AttributeUsage(System.AttributeTargets.ReturnValue Or System.AttributeTargets.Method Or System.AttributeTargets.Property Or System.AttributeTargets.Field)>
    Public NotInheritable Class MaybeNullAttribute
        Inherits System.Attribute
    End Class
    <System.AttributeUsage(System.AttributeTargets.ReturnValue Or System.AttributeTargets.Method Or System.AttributeTargets.Property Or System.AttributeTargets.Field)>
    Public NotInheritable Class NotNullAttribute
        Inherits System.Attribute
    End Class
End Namespace
"

        <Fact>
        Public Sub Parameters_MayAlias()
            Compile("
Public Class Holder
    Public Value As String?
End Class
Public Module Program
    Public Sub Test(first As Holder, second As Holder)
        If first.Value IsNot Nothing Then
            second.Value = Nothing
            Dim length = first.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(9, 26))
        End Sub

        <Fact>
        Public Sub MeAndParameter_MayAlias()
            Compile("
Public Class Holder
    Public Value As String?
    Public Sub Test(other As Holder)
        If Value IsNot Nothing Then
            other.Value = Nothing
            Dim length = Value.Length
        End If
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "Value").WithArguments("Value").WithLocation(7, 26))
        End Sub

        <Fact>
        Public Sub LocalsFromSameParameter_ShareAlias()
            Compile("
Public Class Holder
    Public Value As String?
End Class
Public Module Program
    Public Sub Test(source As Holder)
        Dim left = source
        Dim right = source
        If left.Value IsNot Nothing Then
            right.Value = Nothing
            Dim length = left.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "left.Value").WithArguments("Value").WithLocation(11, 26))
        End Sub

        <Fact>
        Public Sub MethodResults_MayAlias()
            Compile("
Public Class Holder
    Public Value As String?
End Class
Public Module Program
    Public Function Load() As Holder
        Return New Holder()
    End Function
    Public Sub Test()
        Dim first = Load()
        Dim second = Load()
        If first.Value IsNot Nothing Then
            second.Value = Nothing
            Dim length = first.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(14, 26))
        End Sub

        <Fact>
        Public Sub TransitiveAliasChain()
            Compile("
Public Class Holder
    Public Value As String?
End Class
Public Module Program
    Public Sub Test(source As Holder)
        Dim middle = source
        Dim last = middle
        If source.Value IsNot Nothing Then
            last.Value = Nothing
            Dim length = source.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "source.Value").WithArguments("Value").WithLocation(11, 26))
        End Sub

        <Fact>
        Public Sub ConditionalAlias_JoinMayAlias()
            Compile("
Public Class Holder
    Public Value As String?
End Class
Public Module Program
    Public Sub Test(first As Holder, second As Holder, flag As Boolean)
        Dim chosen = first
        If flag Then
            chosen = second
        End If
        If first.Value IsNot Nothing Then
            chosen.Value = Nothing
            Dim length = first.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(13, 26))
        End Sub

        <Fact>
        Public Sub NestedReferenceMembers_MayAlias()
            Compile("
Public Class Node
    Public Child As Node
    Public Name As String?
End Class
Public Module Program
    Public Sub Test(left As Node, right As Node)
        If left.Child.Name IsNot Nothing Then
            right.Child.Name = Nothing
            Dim length = left.Child.Name.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UninitializedNonNullableMember, "Child").WithArguments("Child").WithLocation(3, 12),
                Diagnostic(ERRID.WRN_PossibleNullReference, "left.Child.Name").WithArguments("Name").WithLocation(10, 26))
        End Sub

        <Fact>
        Public Sub PropertySetter_MayMutateAlias()
            Compile("
Public Class Holder
    Public Property Value As String?
End Class
Public Module Program
    Public Sub Test(first As Holder, second As Holder)
        If first.Value IsNot Nothing Then
            second.Value = Nothing
            Dim length = first.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(9, 26))
        End Sub

        <Fact>
        Public Sub ByRefReplacement_DropsMemberFact()
            Compile("
Public Class Holder
    Public Value As String?
End Class
Public Module Program
    Public Sub Replace(ByRef target As Holder)
        target = New Holder()
    End Sub
    Public Sub Test(first As Holder)
        If first.Value IsNot Nothing Then
            Replace(first)
            Dim length = first.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(12, 26))
        End Sub

        <Fact>
        Public Sub CallOnAliasedReceiver_DropsFact()
            Compile("
Public Class Holder
    Public Value As String?
    Public Sub Clear()
        Value = Nothing
    End Sub
End Class
Public Module Program
    Public Sub Test(first As Holder, second As Holder)
        If first.Value IsNot Nothing Then
            second.Clear()
            Dim length = first.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(12, 26))
        End Sub

        <Fact>
        Public Sub FreshObjects_StayDistinct()
            Compile("
Public Class Holder
    Public Value As String?
End Class
Public Module Program
    Public Sub Test()
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
        Public Sub StructureCopies_DoNotAlias()
            Compile("
Public Structure Box
    Public Value As String?
End Structure
Public Module Program
    Public Sub Test()
        Dim first As Box
        Dim second As Box
        first.Value = ""ready""
        second = first
        second.Value = Nothing
        Dim kept = first.Value.Length
    End Sub
End Module").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SharedField_OtherTypeCall_DropsFact()
            Compile("
Public Class Box
    Public Shared Slot As String?
End Class
Public Class Other
    Public Shared Sub Touch()
    End Sub
End Class
Public Module Program
    Public Sub Test()
        If Box.Slot IsNot Nothing Then
            Other.Touch()
            Dim length = Box.Slot.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "Box.Slot").WithArguments("Slot").WithLocation(13, 26))
        End Sub

        <Fact>
        Public Sub StoredReceiver_LaterCallDropsFact()
            Compile("
Public Class Holder
    Public Value As String?
End Class
Public Module Program
    Private saved As Holder
    Public Sub Keep(value As Holder)
        saved = value
    End Sub
    Public Sub Other()
    End Sub
    Public Sub Test(first As Holder)
        Keep(first)
        If first.Value IsNot Nothing Then
            Other()
            Dim length = first.Value.Length
        End If
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "first.Value").WithArguments("Value").WithLocation(16, 26))
        End Sub

        <Fact>
        Public Sub UnrelatedLocal_SurvivesCall()
            Compile("
Public Module Program
    Public Sub Other()
    End Sub
    Public Sub Test(value As String?)
        If value IsNot Nothing Then
            Other()
            Dim length = value.Length
        End If
    End Sub
End Module").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExplicitGetter_DoesNotKeepFact()
            Compile("
Public Class Holder
    Private _name As String?
    Public ReadOnly Property Name As String?
        Get
            Return _name
        End Get
    End Property
    Public Sub Test()
        If Name IsNot Nothing Then
            Dim length = Name.Length
        End If
    End Sub
End Class").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "Name").WithArguments("Name").WithLocation(11, 26))
        End Sub

        <Fact>
        Public Sub TaskResults_FollowGetResult()
            Compile("
Imports System.Threading.Tasks
Public Module Program
    Public Async Function Test(ready As Task(Of String), loose As Task(Of String?)) As Task
        Dim stable = Await ready
        Dim kept = stable.Length
        Dim text = Await loose
        Dim length = text.Length
    End Function
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "text").WithArguments("text").WithLocation(8, 22))
        End Sub

        <Fact>
        Public Sub ValueTaskResults_FollowGetResult()
            CreateCompilation("
Imports System.Threading.Tasks
Public Module Program
    Public Async Function Test(ready As ValueTask(Of String), loose As ValueTask(Of String?)) As Task
        Dim stable = Await ready
        Dim kept = stable.Length
        Dim text = Await loose
        Dim length = text.Length
    End Function
End Module", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext, targetFramework:=TargetFramework.Net90).VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "text").WithArguments("text").WithLocation(8, 22))
        End Sub

        <Fact>
        Public Sub CustomAwaitable_MaybeNullGetResult()
            Compile(Attributes & "
Public Structure LooseAwaiter
    Implements System.Runtime.CompilerServices.INotifyCompletion
    Public ReadOnly Property IsCompleted As Boolean
        Get
            Return True
        End Get
    End Property
    Public Sub OnCompleted(continuation As System.Action) Implements System.Runtime.CompilerServices.INotifyCompletion.OnCompleted
    End Sub
    Public Function GetResult() As <System.Diagnostics.CodeAnalysis.MaybeNull> String
        Return Nothing
    End Function
End Structure
Public Class Loose
    Public Function GetAwaiter() As LooseAwaiter
        Return New LooseAwaiter()
    End Function
End Class
Public Module Program
    Public Async Function Test(value As Loose) As System.Threading.Tasks.Task
        Dim text = Await value
        Dim length = text.Length
    End Function
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "text").WithArguments("text").WithLocation(34, 22))
        End Sub

        <Fact>
        Public Sub CustomAwaitable_UsesGetResultNotUnrelatedArgument()
            Compile(Attributes & "
Public Class Odd(Of T, U)
    Public Function GetAwaiter() As OddAwaiter(Of T, U)
        Return New OddAwaiter(Of T, U)()
    End Function
End Class
Public Structure OddAwaiter(Of T, U)
    Implements System.Runtime.CompilerServices.INotifyCompletion
    Public ReadOnly Property IsCompleted As Boolean
        Get
            Return True
        End Get
    End Property
    Public Sub OnCompleted(continuation As System.Action) Implements System.Runtime.CompilerServices.INotifyCompletion.OnCompleted
    End Sub
    Public Function GetResult() As U
        Throw New System.Exception()
    End Function
End Structure
Public Module Program
    Public Async Function Test(unrelated As Odd(Of String?, String), loose As Odd(Of String, String?)) As System.Threading.Tasks.Task
        Dim stable = Await unrelated
        Dim kept = stable.Length
        Dim text = Await loose
        Dim length = text.Length
    End Function
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "text").WithArguments("text").WithLocation(36, 22))
        End Sub

        <Fact>
        Public Sub CustomAwaitable_NotNullAndObliviousGetResult()
            Compile(Attributes & "
Public Structure MarkedAwaiter
    Implements System.Runtime.CompilerServices.INotifyCompletion
    Public ReadOnly Property IsCompleted As Boolean
        Get
            Return True
        End Get
    End Property
    Public Sub OnCompleted(continuation As System.Action) Implements System.Runtime.CompilerServices.INotifyCompletion.OnCompleted
    End Sub
    Public Function GetResult() As <System.Diagnostics.CodeAnalysis.NotNull> String?
        Return ""ready""
    End Function
End Structure
Public Class Marked
    Public Function GetAwaiter() As MarkedAwaiter
        Return New MarkedAwaiter()
    End Function
End Class
Public Structure PlainAwaiter
    Implements System.Runtime.CompilerServices.INotifyCompletion
    Public ReadOnly Property IsCompleted As Boolean
        Get
            Return True
        End Get
    End Property
    Public Sub OnCompleted(continuation As System.Action) Implements System.Runtime.CompilerServices.INotifyCompletion.OnCompleted
    End Sub
    Public Function GetResult() As String
        Return ""ready""
    End Function
End Structure
Public Class Plain
    Public Function GetAwaiter() As PlainAwaiter
        Return New PlainAwaiter()
    End Function
End Class
Public Module Program
    Public Async Function Test(marked As Marked, plain As Plain) As System.Threading.Tasks.Task
        Dim known = Await marked
        Dim kept = known.Length
        Dim ordinary = Await plain
        Dim length = ordinary.Length
    End Function
End Module").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AwaitEach_NullableElement()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Public Module Program
    Public Async Function Test(stable As IAsyncEnumerable(Of String), loose As IAsyncEnumerable(Of String?), missing As IAsyncEnumerable(Of String?)) As System.Threading.Tasks.Task
        Await Each item In stable
            Dim kept = item.Length
        Next
        Await Each item In loose
            Dim length = item.Length
            If item IsNot Nothing Then
                Dim guarded = item.Length
            End If
        Next
        Await Each item As String In loose
            Dim forced = item.Length
        Next
        If missing IsNot Nothing Then
            Await Each item In missing
                Dim length = item.Length
            Next
        End If
    End Function
End Module", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext, targetFramework:=TargetFramework.Net90)
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "item").WithArguments("item").WithLocation(9, 26),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "loose").WithArguments("item").WithLocation(14, 38),
                Diagnostic(ERRID.WRN_PossibleNullReference, "item").WithArguments("item").WithLocation(15, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "item").WithArguments("item").WithLocation(19, 30))
        End Sub

        <Fact>
        Public Sub GenericSubstitution_TypeArgumentDoesNotCrossMembers()
            Compile("
Public Class Pair(Of T, U)
    Public First As T
    Public Second As U
End Class
Public Module Program
    Public Sub Test(pair As Pair(Of String, String?))
        Dim kept = pair.First.Length
        Dim length = pair.Second.Length
    End Sub
End Module").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "pair.Second").WithArguments("Second").WithLocation(9, 22))
        End Sub

        <Fact>
        Public Sub LegacyCompilation_DoesNotWarn()
            Dim compilation = CreateCompilation("
Public Class Holder
    Public Value As String
End Class
Public Module Program
    Public Sub Test(first As Holder, second As Holder)
        second.Value = Nothing
        Dim length = first.Value.Length
    End Sub
End Module", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17)
            compilation.VerifyDiagnostics()
        End Sub

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function
    End Class

End Namespace
