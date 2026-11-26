' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableMethodContractFlowTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const Attributes As String = "
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

        <Fact>
        Public Sub AllowNullAndDisallowNull_ArgumentAndEntry()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Sub Accept(<AllowNull> value As String)
        Dim length = value.Length
    End Sub

    Public Sub Require(<DisallowNull> value As String?)
        Dim length = value.Length
    End Sub

    Public Sub Both(<AllowNull, DisallowNull> value As String)
        Dim length = value.Length
    End Sub

    Public Sub Use(maybe As String?, plain As String)
        Accept(Nothing)
        Accept(maybe)
        Require(maybe)
        Require(plain)
        Both(Nothing)
        Both(plain)
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(52, 22),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(60, 22),
                Diagnostic(ERRID.WRN_PossibleNullArgument, "maybe").WithArguments("value").WithLocation(66, 17),
                Diagnostic(ERRID.WRN_PossibleNullArgument, "Nothing").WithArguments("value").WithLocation(68, 14))
        End Sub

        <Fact>
        Public Sub MaybeNullAndNotNull_ReturnAndParameter()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Function Maybe() As <MaybeNull> String
        Return ""ok""
    End Function

    Public Function Forced() As <NotNull> String?
        Return Nothing
    End Function

    Public Sub Ensure(<NotNull> value As String?)
        If value Is Nothing Then
            Throw New System.ArgumentNullException(NameOf(value))
        End If
    End Sub

    Public Sub Clear(<MaybeNull> ByRef value As String)
    End Sub

    Public Sub Use(value As String?)
        Dim loose As String = Maybe()
        Dim known As String? = Forced()
        Dim length = known.Length
        Ensure(value)
        Dim after = value.Length
        Dim text As String = ""x""
        Clear(text)
        Dim cleared = text.Length
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReturn, "Nothing").WithLocation(56, 16),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Maybe()").WithArguments("loose").WithLocation(69, 31),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "text").WithArguments("text").WithLocation(75, 15),
                Diagnostic(ERRID.WRN_PossibleNullReference, "text").WithArguments("text").WithLocation(76, 23))
        End Sub

        <Fact>
        Public Sub ConditionalPostconditions_TrueAndFalse()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Function TryTrue(<NotNullWhen(True)> ByRef value As String?) As Boolean
        value = ""ok""
        Return True
    End Function

    Public Function TryFalse(<NotNullWhen(False)> value As String?) As Boolean
        Return value IsNot Nothing
    End Function

    Public Function LostWhenTrue(<MaybeNullWhen(True)> value As String) As Boolean
        Return False
    End Function

    Public Function LostWhenFalse(<MaybeNullWhen(False)> ByRef value As String) As Boolean
        Return True
    End Function

    Public Sub Use(value As String?, plain As String)
        If TryTrue(value) Then
            Dim ok = value.Length
        Else
            Dim missing = value.Length
        End If

        If TryFalse(value) Then
            Dim still = value.Length
        Else
            Dim present = value.Length
        End If

        If Not TryTrue(value) Then
            Dim negated = value.Length
        Else
            Dim kept = value.Length
        End If

        If LostWhenTrue(plain) Then
            Dim dropped = plain.Length
        Else
            Dim safe = plain.Length
        End If

        If LostWhenFalse(plain) Then
            Dim keptText = plain.Length
        Else
            Dim droppedText = plain.Length
        End If

        If TryTrue(value) Then
            Dim first = value.Length
        ElseIf TryFalse(value) Then
            Dim second = value.Length
        End If
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(72, 27),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(76, 25),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(82, 27),
                Diagnostic(ERRID.WRN_PossibleNullReference, "plain").WithArguments("plain").WithLocation(88, 27),
                Diagnostic(ERRID.WRN_PossibleNullArgument, "plain").WithArguments("value").WithLocation(93, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "plain").WithArguments("plain").WithLocation(96, 31),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(102, 26))
        End Sub

        <Fact>
        Public Sub ConditionalPostconditions_ShortCircuitAndStoredResult()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Function TryGet(<NotNullWhen(True)> ByRef value As String?) As Boolean
        value = ""ok""
        Return True
    End Function

    Public Sub Use(value As String?, other As String?)
        If TryGet(value) AndAlso value.Length > 0 Then
            Dim length = value.Length
        End If

        If TryGet(value) And TryGet(other) Then
            Dim left = value.Length
            Dim right = other.Length
        End If

        If TryGet(value) OrElse other Is Nothing Then
            Dim unknown = value.Length
        Else
            Dim absent = value.Length
        End If

        Dim stored = TryGet(value)
        If stored Then
            Dim unrelated = value.Length
        End If

        TryGet(value)
        Dim ignored = value.Length
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(67, 27),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(69, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(74, 29),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(78, 23))
        End Sub

        <Fact>
        Public Sub NotNullIfNotNull_Dependencies()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        Return value
    End Function

    Public Function Either(left As String?, right As String?) As <NotNullIfNotNull(""left""), NotNullIfNotNull(""right"")> String?
        Return If(left, right)
    End Function

    Public Function Unknown(<NotNullIfNotNull(""missing"")> value As String?) As String?
        Return value
    End Function

    Public Sub Use(value As String?, plain As String, oblivious As Object)
        Dim fromPlain = Normalize(plain)
        Dim length = fromPlain.Length
        Dim fromMaybe = Normalize(value)
        Dim missing = fromMaybe.Length
        Dim fromEither = Either(value, plain)
        Dim eitherLength = fromEither.Length
        Dim fromUnknown = Unknown(plain)
        Dim unknownLength = fromUnknown.Length
        Dim choice = If(value Is Nothing, """", Normalize(value))
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "fromMaybe").WithArguments("fromMaybe").WithLocation(67, 23),
                Diagnostic(ERRID.WRN_PossibleNullReference, "fromUnknown").WithArguments("fromUnknown").WithLocation(71, 29))
        End Sub

        <Fact>
        Public Sub DoesNotReturn_MakesContinuationUnreachable()
            Dim compilation = Compile(Attributes & "
Public Class Api
    <DoesNotReturn>
    Public Sub Fail()
    End Sub

    Public Sub Use(value As String?)
        If value Is Nothing Then
            Fail()
        End If
        Dim length = value.Length

        While value Is Nothing
            Fail()
        End While
        Dim afterLoop = value.Length
    End Sub
End Class
")

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub DoesNotReturnIf_SurvivingFacts()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Sub FailIf(<DoesNotReturnIf(True)> condition As Boolean)
    End Sub

    Public Sub FailUnless(<DoesNotReturnIf(False)> condition As Boolean)
    End Sub

    Public Sub Use(value As String?)
        FailIf(value Is Nothing)
        Dim length = value.Length
        FailUnless(value IsNot Nothing)
        Dim still = value.Length
        FailIf(True)
        Dim dead = value.Length
    End Sub

    Public Sub Mutated(value As String?)
        FailIf(value Is Nothing, value)
        Dim length = value.Length
    End Sub

    Public Sub FailIf(<DoesNotReturnIf(True)> condition As Boolean, <MaybeNull> ByRef value As String?)
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(68, 22))
        End Sub

        <Fact>
        Public Sub ExceptionalPaths_DoNotKeepSuccessfulPostconditions()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Sub Ensure(<NotNull> value As String?)
    End Sub

    <DoesNotReturn>
    Public Sub Fail()
    End Sub

    Public Sub Use(value As String?)
        Try
            Ensure(value)
            Dim safe = value.Length
            Fail()
        Catch
            Dim unsafe = value.Length
        Finally
            Dim uncertain = value.Length
        End Try
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(64, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(66, 29))
        End Sub

        <Fact>
        Public Sub ByRefAlias_LaterArgumentWins()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Sub Touch(<NotNull> ByRef first As String?, <MaybeNull> ByRef second As String?)
    End Sub

    Public Sub Use(value As String?)
        Touch(value, value)
        Dim length = value.Length
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(56, 22))
        End Sub

        <Fact>
        Public Sub NamedArguments_UseFormalParameterIdentity()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Sub Take(<DisallowNull> first As String?, second As String?)
    End Sub

    Public Function Pick(first As String?, second As String?) As <NotNullIfNotNull(""second"")> String?
        Return second
    End Function

    Public Sub Use(value As String?, plain As String)
        Take(second:=value, first:=plain)
        Dim chosen = Pick(second:=plain, first:=value)
        Dim length = chosen.Length
        Dim reversed = Pick(second:=value, first:=plain)
        Dim missing = reversed.Length
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "reversed").WithArguments("reversed").WithLocation(63, 23))
        End Sub

        <Fact>
        Public Sub ObliviousDeclarations_KeepExplicitContracts()
            Dim library = CreateCompilation(Attributes & "
Public Class Api
    Public Shared Sub Accept(<AllowNull> value As String)
    End Sub
    Public Shared Sub Require(<DisallowNull> value As String)
    End Sub
    Public Shared Function Forced() As <NotNull> String
        Return """"
    End Function
    Public Shared Function Maybe() As <MaybeNull> String
        Return """"
    End Function
    <DoesNotReturn>
    Public Shared Sub Fail()
    End Sub
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable), parseOptions:=VbNext)
            library.VerifyDiagnostics()

            Dim consumer = CreateCompilation("
Module Program
    Sub Use()
        Api.Accept(Nothing)
        Api.Require(Nothing)
        Dim known = Api.Forced()
        Dim length = known.Length
        Dim loose = Api.Maybe()
        Dim looseLength = loose.Length
    End Sub
End Module
", references:={library.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Warnings), parseOptions:=VbNext)

            consumer.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullArgument, "Nothing").WithArguments("value").WithLocation(5, 21),
                Diagnostic(ERRID.WRN_PossibleNullReference, "loose").WithArguments("loose").WithLocation(9, 27))
        End Sub

        <Fact>
        Public Sub SourceAndImportedContracts_Agree()
            Dim library = CreateCompilation(Attributes & "
Public Class Api
    Public Shared Function TryGet(<NotNullWhen(True)> ByRef value As String?) As Boolean
        value = ""ok""
        Return True
    End Function
    Public Shared Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        Return value
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            library.VerifyDiagnostics()

            Dim source = "
Module Program
    Sub Use(value As String?, plain As String)
        If Api.TryGet(value) Then
            Dim length = value.Length
        Else
            Dim missing = value.Length
        End If
        Dim known = Api.Normalize(plain)
        Dim knownLength = known.Length
    End Sub
End Module
"
            Dim imported = CreateCompilation(source, references:={library.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            imported.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(7, 27))
        End Sub

        <Fact>
        Public Sub CSharpMetadata_IsConsumed()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
namespace System.Diagnostics.CodeAnalysis
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter)]
    public sealed class DoesNotReturnIfAttribute : global::System.Attribute
    {
        public DoesNotReturnIfAttribute(bool parameterValue) {}
    }
    [global::System.AttributeUsage(global::System.AttributeTargets.Parameter | global::System.AttributeTargets.Property | global::System.AttributeTargets.ReturnValue, AllowMultiple = true)]
    public sealed class NotNullIfNotNullAttribute : global::System.Attribute
    {
        public NotNullIfNotNullAttribute(string parameterName) {}
    }
}
public static class Api
{
    public static void FailIf([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(true)] bool condition) {}
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(value))]
    public static string? Normalize(string? value) => value;
}
", parseOptions:=New CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview))
            csharp.VerifyDiagnostics()

            Dim consumer = CreateCompilation("
Module Program
    Sub Use(value As String?, plain As String)
        Api.FailIf(value Is Nothing)
        Dim length = value.Length
        Dim known = Api.Normalize(plain)
        Dim knownLength = known.Length
        Dim loose = Api.Normalize(plain)
        Dim another As String? = Nothing
        loose = Api.Normalize(another)
        Dim missing = loose.Length
    End Sub
End Module
", references:={csharp.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            consumer.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "loose").WithArguments("loose").WithLocation(11, 23))
        End Sub

        <Fact>
        Public Sub GenericAndDelegateContracts()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Shared Function Id(Of T)(<DisallowNull> value As T) As T
        Return value
    End Function

    Public Shared Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        Return value
    End Function
End Class

Public Delegate Function Map(value As String?) As <NotNullIfNotNull(""value"")> String?

Public Class UseSite
    Public Sub Use(value As String?, plain As String, map As Map)
        Api.Id(value)
        Api.Id(plain)
        map = AddressOf Api.Normalize
        Dim through = map(plain)
        Dim length = through.Length
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullArgument, "value").WithArguments("value").WithLocation(64, 16))
        End Sub

        <Fact>
        Public Sub LegacyAndDisabledModes_DoNotWarn()
            Dim source = Attributes & "
Module Program
    Sub Accept(<AllowNull> value As String)
        Dim length = value.Length
    End Sub
    Sub Use()
        Accept(Nothing)
    End Sub
End Module
"
            For Each parseOptions In {Vb17, TestOptions.Regular, TestOptions.Regular.WithLanguageVersion(LanguageVersion.Latest)}
                Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=parseOptions)
                compilation.VerifyDiagnostics()
            Next

            Dim disabled = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Disable), parseOptions:=VbNext)
            disabled.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NonBooleanReturn_IgnoresConditionalParameterAttribute()
            Dim compilation = Compile(Attributes & "
Public Class Api
    Public Function Describe(<NotNullWhen(True)> value As String?) As String
        Return ""ok""
    End Function

    Public Sub Use(value As String?)
        Dim text = Describe(value)
        Dim length = value.Length
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(57, 22))
        End Sub

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function
    End Class

End Namespace
