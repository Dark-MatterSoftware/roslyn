' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableCoreFlowTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub NullTests_NarrowTrueFalseAndEarlyExits()
            Dim compilation = Compile("
Module Program
    Sub Taken(value As String?)
        If value IsNot Nothing Then
            Dim length = value.Length
        End If
    End Sub

    Sub Rejected(value As String?)
        If value IsNot Nothing Then
            Return
        End If
        Dim length = value.Length
    End Sub

    Sub EarlyReturn(value As String?)
        If value Is Nothing Then
            Return
        End If
        Dim length = value.Length
    End Sub

    Sub EarlyExit(value As String?)
        If value Is Nothing Then
            Exit Sub
        End If
        Dim length = value.Length
    End Sub

    Sub EarlyThrow(value As String?)
        If value Is Nothing Then
            Throw New System.InvalidOperationException()
        End If
        Dim length = value.Length
    End Sub

    Sub Reversed(value As String?)
        If Nothing Is value Then
            Return
        End If
        Dim length = value.Length
    End Sub

    Sub Negated(value As String?)
        If Not (value Is Nothing) Then
            Dim length = value.Length
        End If
    End Sub

    Sub ElseBranch(value As String?)
        If value Is Nothing Then
            Dim length = value.Length
        Else
            Dim safe = value.Length
        End If
    End Sub

    Sub ElseIfBranch(value As String?, other As String?)
        If value IsNot Nothing Then
            Dim first = value.Length
        ElseIf other IsNot Nothing Then
            Dim second = other.Length
            Dim still = value.Length
        Else
            Dim third = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(13, 22),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(52, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(63, 25),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(65, 25))
        End Sub

        <Fact>
        Public Sub ShortCircuit_PropagatesStateThroughAndAlsoAndOrElse()
            Dim compilation = Compile("
Module Program
    Sub AndAlsoGuard(value As String?)
        If value IsNot Nothing AndAlso value.Length > 0 Then
            Dim length = value.Length
        End If
    End Sub

    Sub OrElseGuard(value As String?)
        If value Is Nothing OrElse value.Length = 0 Then
            Return
        End If
        Dim length = value.Length
    End Sub

    Sub Nested(first As String?, second As String?)
        If first IsNot Nothing AndAlso second IsNot Nothing AndAlso first.Length > second.Length Then
            Dim left = first.Length
            Dim right = second.Length
        End If
    End Sub

    Sub ParenthesizedNot(value As String?)
        If Not (value Is Nothing OrElse value.Length = 0) Then
            Dim length = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NonShortCircuit_DoesNotProtectTheRightOperand()
            Dim compilation = Compile("
Module Program
    Sub AndOperator(value As String?)
        If value IsNot Nothing And value.Length > 0 Then
            Dim length = value.Length
        End If
    End Sub

    Sub OrOperator(value As String?)
        If value Is Nothing Or value.Length = 0 Then
            Return
        End If
        Dim length = value.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(4, 36),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(10, 32))
        End Sub

        <Fact>
        Public Sub Assignment_ReplacesAndInvalidatesState()
            Dim compilation = Compile("
Module Program
    Sub Reassign(value As String?)
        If value IsNot Nothing Then
            value = Nothing
            Dim length = value.Length
        End If
    End Sub

    Sub Sources(marked As String?, plain As String, oblivious As Object)
        Dim fromMarked As String? = marked
        Dim fromPlain As String = plain
        Dim fromOblivious As String? = TryCast(oblivious, String)
        Dim known As String? = ""known""
        Dim cleared As String = Nothing
        Dim markedLength = fromMarked.Length
        Dim knownLength = known.Length
        fromMarked = plain
        Dim afterPlain = fromMarked.Length
    End Sub

    Sub Concatenate(value As String?)
        If value IsNot Nothing Then
            value &= ""!""
            Dim length = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(6, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "fromMarked").WithArguments("fromMarked").WithLocation(16, 28),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("cleared").WithLocation(15, 33))
        End Sub

        <Fact>
        Public Sub BranchJoins_KeepNullWhenAnyReachablePathDoes()
            Dim compilation = Compile("
Module Program
    Sub Mixed(condition As Boolean, value As String?)
        If condition Then
            value = ""ready""
        Else
            value = Nothing
        End If
        Dim length = value.Length
    End Sub

    Sub AllKnown(condition As Boolean, value As String?)
        If condition Then
            value = ""left""
        Else
            value = ""right""
        End If
        Dim length = value.Length
    End Sub

    Sub NestedIf(condition As Boolean, value As String?)
        If condition Then
            If value IsNot Nothing Then
                value = ""ready""
            End If
        Else
            value = ""other""
        End If
        Dim length = value.Length
    End Sub

    Sub Selected(condition As Boolean, value As String?)
        value = ""start""
        Select Case condition
            Case True
                value = ""ready""
            Case Else
                value = Nothing
        End Select
        Dim length = value.Length
    End Sub

    Sub SelectedKnown(condition As Boolean, value As String?)
        Select Case condition
            Case True
                value = ""left""
            Case Else
                value = ""right""
        End Select
        Dim length = value.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(9, 22),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(29, 22),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(40, 22))
        End Sub

        <Fact>
        Public Sub Loops_MergeEntryBackEdgeAndExit()
            Dim compilation = Compile("
Module Program
    Sub WhileExit(value As String?)
        While value Is Nothing
            value = ""ready""
        End While
        Dim length = value.Length
    End Sub

    Sub DoUntil(value As String?)
        Do
            If value IsNot Nothing Then
                Exit Do
            End If
            value = ""ready""
        Loop
        Dim length = value.Length
    End Sub

    Sub DoLoopWhile(value As String?)
        Do
            value = Nothing
        Loop While value IsNot Nothing
        Dim length = value.Length
    End Sub

    Sub ForMaySkip(value As String?)
        value = ""ready""
        For index = 1 To 2
            value = Nothing
        Next
        Dim length = value.Length
    End Sub

    Sub ForEachElement(items As String?(), value As String?)
        For Each value In items
            Dim length = value.Length
        Next
        Dim after = value.Length
    End Sub

    Sub ContinueKeepsBackEdge(value As String?)
        Do While value IsNot Nothing
            If value.Length = 0 Then
                value = Nothing
                Continue Do
            End If
            Exit Do
        Loop
        Dim length = value.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(24, 22),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(32, 22),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(37, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(39, 21),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(50, 22))
        End Sub

        <Fact>
        Public Sub TryCatch_DoesNotInventNonNullState()
            Dim compilation = Compile("
Module Program
    Sub AssignedInTry(value As String?)
        Try
            value = ""ready""
        Catch ex As System.Exception
            Dim caught = value.Length
        End Try
        Dim after = value.Length
    End Sub

    Sub EstablishedBeforeTry(value As String?)
        If value Is Nothing Then
            Return
        End If
        Try
            Dim inside = value.Length
        Catch ex As System.Exception
            Dim caught = value.Length
        Finally
            Dim finalLength = value.Length
        End Try
        Dim after = value.Length
    End Sub

    Sub ExitTry(value As String?)
        Try
            If value Is Nothing Then
                Exit Try
            End If
            Dim inside = value.Length
        Finally
        End Try
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(7, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(9, 21))
        End Sub

        <Fact>
        Public Sub ConditionalForms_DistinguishNonNullFromSafeNavigation()
            Dim compilation = Compile("
Module Program
    Sub Choices(condition As Boolean, value As String?, other As String?)
        Dim mixed As String? = If(condition, value, other)
        Dim known As String = If(condition, ""left"", ""right"")
        Dim fallback As String = If(value, ""fallback"")
        Dim length = known.Length
        Dim fallbackLength = fallback.Length
        Dim mixedLength = mixed.Length
        Dim conditionalLength = value?.Length
        Dim text As String = value?.ToString()
    End Sub

    Sub Casts(value As Object)
        Dim known = DirectCast(value, String)
        Dim maybe = TryCast(value, String)
        Dim directLength = known.Length
        Dim tryLength = maybe.Length
        Dim converted As String = CType(value, String)
    End Sub

    Sub TypeTest(value As String?)
        If TypeOf value Is String Then
            Dim length = value.Length
        End If
        If TypeOf value IsNot String Then
            Dim length = value.Length
        Else
            Dim safe = value.Length
        End If
        If TypeOf value Is String Into text Then
            Dim intoLength = text.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "mixed").WithArguments("mixed").WithLocation(9, 27),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "value?.ToString()").WithArguments("text").WithLocation(11, 30),
                Diagnostic(ERRID.WRN_PossibleNullReference, "maybe").WithArguments("maybe").WithLocation(18, 25),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(27, 26))
        End Sub

        <Fact>
        Public Sub ByRefAndCapturedVariables_AreInvalidatedConservatively()
            Dim compilation = Compile("
Delegate Sub Callback()

Module Program
    Sub Mutate(ByRef value As String?)
        value = Nothing
    End Sub

    Sub Keep(ByRef value As String)
    End Sub

    Sub Run(callback As Callback)
    End Sub

    Sub ThroughByRef(value As String?)
        If value IsNot Nothing Then
            Mutate(value)
            Dim length = value.Length
        End If
    End Sub

    Sub ThroughNonNullByRef(value As String?)
        If value IsNot Nothing Then
            Keep(value)
            Dim length = value.Length
        End If
    End Sub

    Sub Captured(value As String?)
        If value IsNot Nothing Then
            Dim before = value.Length
            Dim callback As Callback = Sub() value = Nothing
            Dim still = value.Length
            Run(callback)
            Dim later = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(18, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(35, 25))
        End Sub

        <Fact>
        Public Sub NullableContext_KeepsStateWhenWarningsChange()
            Dim compilation = Compile("
#Nullable Annotations
Module Quiet
    Sub M(value As String?)
        Dim length = value.Length
    End Sub
End Module

#Nullable Enable
Module Loud
    Sub M(value As String?)
        If value IsNot Nothing AndAlso value.Length > 0 Then
            Dim length = value.Length
        End If
    End Sub

    Sub AcrossDirective(value As String?)
#Nullable Annotations
        If value IsNot Nothing Then
#Nullable Enable
            Dim length = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub WarningsMode_AnalyzesImportedAnnotatedContracts()
            Dim library = CreateCompilation("
Public Class Api
    Public Shared Function Find() As String?
        Return Nothing
    End Function

    Public Shared Sub Take(value As String)
    End Sub
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            library.VerifyDiagnostics()
            Dim consumer = CreateCompilation("
Module Program
    Sub Use(plain As String)
        Dim found = Api.Find()
        Dim length = found.Length
        Api.Take(found)
        Api.Take(Nothing)
        Dim localLength = plain.Length
    End Sub
End Module
", references:={library.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Warnings), parseOptions:=VbNext)

            consumer.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "found").WithArguments("found").WithLocation(5, 22),
                Diagnostic(ERRID.WRN_PossibleNullArgument, "found").WithArguments("value").WithLocation(6, 18),
                Diagnostic(ERRID.WRN_PossibleNullArgument, "Nothing").WithArguments("value").WithLocation(7, 18))
        End Sub

        <Fact>
        Public Sub LegacyLanguageVersions_DoNotReportNullableFlowWarnings()
            Dim source = "
Module Program
    Sub M(value As String)
        If value IsNot Nothing AndAlso value.Length > 0 Then
            Dim length = value.Length
        End If
        value = Nothing
    End Sub
End Module
"
            For Each parseOptions In {Vb17, TestOptions.Regular, TestOptions.Regular.WithLanguageVersion(LanguageVersion.Latest)}
                Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=parseOptions)
                Assert.Equal(LanguageVersion.VisualBasic17_13, compilation.LanguageVersion)
                compilation.VerifyDiagnostics()
            Next
        End Sub

        <Fact>
        Public Sub OrdinaryMethodIL_IsUnchangedWhenNullableWarningsAreEnabled()
            Dim source = "
Module Program
    Sub M(value As String)
        If value Is Nothing Then
            Return
        End If
        System.Console.WriteLine(value.Length)
    End Sub
End Module
"
            Dim disabled = MethodIl(source, LanguageVersion.VisualBasic17_13, NullableContextOptions.Disable)
            Dim enabledOn17 = MethodIl(source, LanguageVersion.VisualBasic17_13, NullableContextOptions.Enable)
            Dim enabledOnNext = MethodIl(source, LanguageVersion.VbNext, NullableContextOptions.Enable)
            Assert.Equal(disabled, enabledOn17)
            Assert.Equal(disabled, enabledOnNext)
        End Sub

        <Fact>
        Public Sub UnconditionalContracts_StayOnTheExistingWarningPaths()
            Dim compilation = Compile("
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Field Or AttributeTargets.Parameter Or AttributeTargets.Property)>
    Public NotInheritable Class AllowNullAttribute
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
Public Class Box
    Sub Take(<AllowNull> value As String)
    End Sub
    Sub TakeRequired(value As String)
    End Sub
    Function Maybe() As <MaybeNull> String
        Return Nothing
    End Function
    Function Forced() As <NotNull> String?
        Return Nothing
    End Function
    Sub Use()
        Take(Nothing)
        TakeRequired(Nothing)
        Dim text As String = Maybe()
        Dim known As String = Forced()
        Dim length = known.Length
    End Sub
End Class
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReturn, "Nothing").WithLocation(26, 16),
                Diagnostic(ERRID.WRN_PossibleNullArgument, "Nothing").WithArguments("value").WithLocation(30, 22),
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Maybe()").WithArguments("text").WithLocation(31, 30))
        End Sub

        <Fact>
        Public Sub GoTo_MergesForwardLabelState()
            Dim compilation = Compile("
Module Program
    Sub Forward(value As String?)
        If value Is Nothing Then
            GoTo Done
        End If
        Dim safe = value.Length
        Return
Done:
        Dim length = value.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(10, 22))
        End Sub

        Private Function Compile(source As String, Optional nullable As NullableContextOptions = NullableContextOptions.Enable) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(nullable), parseOptions:=VbNext)
        End Function

        Private Function MethodIl(source As String, language As LanguageVersion, nullable As NullableContextOptions) As String
            Dim compilation = CreateCompilation(source,
                                                options:=TestOptions.ReleaseDll.WithNullableContext(nullable),
                                                parseOptions:=TestOptions.Regular.WithLanguageVersion(language))
            compilation.VerifyDiagnostics()
            Return CompileAndVerify(compilation).VisualizeIL("Program.M")
        End Function
    End Class

End Namespace
