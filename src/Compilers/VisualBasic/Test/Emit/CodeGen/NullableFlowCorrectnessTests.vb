' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGen

    ''' <summary>
    ''' LC-1F.1a regressions for exceptional paths, iteration limits, conversions, and Boolean side effects.
    ''' </summary>
    Public Class NullableFlowCorrectnessTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub Catch_SeesIntermediateNothing()
            Dim compilation = Compile("
Module Program
    Sub Risky()
        Throw New System.InvalidOperationException()
    End Sub

    Sub M()
        Dim value As String? = ""ready""
        Try
            value = Nothing
            Risky()
            value = ""restored""
        Catch
            Dim length = value.Length
        End Try
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(14, 26))
        End Sub

        <Fact>
        Public Sub Catch_SeesExplicitThrowAfterNothing()
            Dim compilation = Compile("
Module Program
    Sub M()
        Dim value As String? = ""ready""
        Try
            value = Nothing
            Throw New System.InvalidOperationException()
            value = ""restored""
        Catch
            Dim length = value.Length
        End Try
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(10, 26))
        End Sub

        <Fact>
        Public Sub Finally_SeesIntermediateAndReturn()
            Dim compilation = Compile("
Module Program
    Function M() As Integer
        Dim value As String? = ""ready""
        Try
            value = Nothing
            Return 1
            value = ""restored""
        Finally
            Dim length = value.Length
        End Try
        Return 0
    End Function
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(10, 26))
        End Sub

        <Fact>
        Public Sub Finally_SeesExitTry()
            Dim compilation = Compile("
Module Program
    Sub M(stopEarly As Boolean)
        Dim value As String? = ""ready""
        Try
            value = Nothing
            If stopEarly Then Exit Try
            value = ""restored""
        Finally
            Dim length = value.Length
        End Try
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(10, 26))
        End Sub

        <Fact>
        Public Sub ByRefClear_BeforeThrow_IsVisibleInCatch()
            Dim compilation = Compile("
Module Program
    Sub ClearAndThrow(ByRef value As String?)
        value = Nothing
        Throw New System.InvalidOperationException()
    End Sub

    Sub M()
        Dim value As String? = ""ready""
        Try
            ClearAndThrow(value)
            value = ""restored""
        Catch
            Dim length = value.Length
        End Try
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(14, 26))
        End Sub

        <Fact>
        Public Sub NestedTry_PropagatesIntermediateState()
            Dim compilation = Compile("
Module Program
    Sub Risky()
        Throw New System.InvalidOperationException()
    End Sub

    Sub M()
        Dim value As String? = ""ready""
        Try
            Try
                value = Nothing
                Risky()
                value = ""restored""
            Finally
            End Try
        Catch
            Dim length = value.Length
        End Try
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(17, 26))
        End Sub

        <Fact>
        Public Sub EstablishedNonNull_RemainsSafe()
            Dim compilation = Compile("
Module Program
    Sub M(value As String?)
        If value Is Nothing Then Return
        Try
            Dim inside = value.Length
        Catch
            Dim handler = value.Length
        Finally
            Dim cleanup = value.Length
        End Try
        Dim after = value.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub LoopLimit_PropagatesDependentVariables()
            Dim compilation = Compile("
Module Program
    Sub M(condition As Boolean)
        Dim a1 As String? = ""1""
        Dim a2 As String? = ""2""
        Dim a3 As String? = ""3""
        Dim a4 As String? = ""4""
        Dim a5 As String? = ""5""
        Dim a6 As String? = ""6""
        Dim a7 As String? = ""7""
        Dim a8 As String? = ""8""
        Dim a9 As String? = ""9""
        While condition
            Dim inside = a9.Length
            a9 = a8
            a8 = a7
            a7 = a6
            a6 = a5
            a5 = a4
            a4 = a3
            a3 = a2
            a2 = a1
            a1 = Nothing
        End While
        Dim after = a9.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "a9").WithArguments("a9").WithLocation(14, 26),
                Diagnostic(ERRID.WRN_PossibleNullReference, "a9").WithArguments("a9").WithLocation(25, 21))
        End Sub

        <Fact>
        Public Sub StableLoop_KeepsEstablishedNonNull()
            Dim compilation = Compile("
Module Program
    Sub M(condition As Boolean)
        Dim value As String = ""ready""
        While condition
            Dim inside = value.Length
        End While
        Dim after = value.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub BackwardGoToLimit_DoesNotKeepNonNull()
            Dim compilation = Compile("
Module Program
    Sub M()
        Dim a1 As String? = ""1""
        Dim a2 As String? = ""2""
        Dim a3 As String? = ""3""
        Dim a4 As String? = ""4""
        Dim a5 As String? = ""5""
Again:
        Dim length = a5.Length
        a5 = a4
        a4 = a3
        a3 = a2
        a2 = a1
        a1 = Nothing
        GoTo Again
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "a5").WithArguments("a5").WithLocation(10, 22))
        End Sub

        <Fact>
        Public Sub UserDefinedConversion_DoesNotInheritOperandState()
            Dim compilation = Compile("
Class Holder
    Public Shared Widening Operator CType(value As Holder) As String?
        Return Nothing
    End Operator
End Class

Module Program
    Sub M()
        Dim input As New Holder()
        Dim text As String = CType(input, String)
        Dim length = text.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "CType(input, String)").WithArguments("text").WithLocation(11, 30),
                Diagnostic(ERRID.WRN_PossibleNullReference, "text").WithArguments("text").WithLocation(12, 22))
        End Sub

        <Fact>
        Public Sub OrdinaryReferenceConversion_PreservesState()
            Dim compilation = Compile("
Module Program
    Sub M(value As String?)
        If value IsNot Nothing Then
            Dim copy As String = DirectCast(value, String)
            Dim length = copy.Length
        End If

        Dim bad As String = DirectCast(value, String)
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "DirectCast(value, String)").WithArguments("bad").WithLocation(9, 29))
        End Sub

        <Fact>
        Public Sub TryCast_RemainsNullable()
            Dim compilation = Compile("
Module Program
    Sub M(value As String)
        Dim maybe = TryCast(value, String)
        Dim length = maybe.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "maybe").WithArguments("maybe").WithLocation(5, 22))
        End Sub

        <Fact>
        Public Sub ObliviousConversion_DoesNotBecomeNotNull()
            Dim compilation = Compile("
Module Program
    Sub M(value As Object)
        Dim text As String = DirectCast(value, String)
        If text IsNot Nothing Then
            Dim length = text.Length
        End If
    End Sub
End Module
", NullableContextOptions.Warnings)

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub UserDefinedConversion_InvalidatesCapturedFacts()
            Dim compilation = Compile("
Class Holder
    Public Shared Widening Operator CType(value As Holder) As String?
        Return Nothing
    End Operator
End Class

Module Program
    Function Make(ByRef value As String?) As Holder
        value = Nothing
        Return New Holder()
    End Function

    Sub M(value As String?)
        If value IsNot Nothing Then
            Dim text As Object = CType(Make(value), Object)
            Dim length = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(17, 26))
        End Sub

        <Fact>
        Public Sub AndAlso_ByRefClear_InvalidatesFact()
            Dim compilation = Compile("
Module Program
    Function ClearValue(ByRef value As String?) As Boolean
        value = Nothing
        Return True
    End Function

    Sub M(value As String?)
        If value IsNot Nothing AndAlso ClearValue(value) Then
            Dim length = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(10, 26))
        End Sub

        <Fact>
        Public Sub And_ByRefClear_InvalidatesFact()
            Dim compilation = Compile("
Module Program
    Function ClearValue(ByRef value As String?) As Boolean
        value = Nothing
        Return True
    End Function

    Sub M(value As String?)
        If value IsNot Nothing And ClearValue(value) Then
            Dim length = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(10, 26))
        End Sub

        <Fact>
        Public Sub AndAlso_CapturedMutation_InvalidatesFact()
            Dim compilation = Compile("
Delegate Sub Callback()

Module Program
    Function Invoke(callback As Callback) As Boolean
        callback()
        Return True
    End Function

    Sub M(value As String?)
        If value IsNot Nothing AndAlso Invoke(Sub() value = Nothing) Then
            Dim length = value.Length
        End If
    End Sub
End Module
")

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullReference, "value").WithArguments("value").WithLocation(12, 26))
        End Sub

        <Fact>
        Public Sub OrElse_NegativeControl_KeepsNarrowing()
            Dim compilation = Compile("
Module Program
    Sub M(value As String?)
        If value Is Nothing OrElse value.Length = 0 Then
            Return
        End If

        Dim length = value.Length
    End Sub
End Module
")

            compilation.VerifyDiagnostics()
        End Sub

        Private Function Compile(source As String, Optional nullable As NullableContextOptions = NullableContextOptions.Enable) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(nullable), parseOptions:=VbNext)
        End Function
    End Class

End Namespace
