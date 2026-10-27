' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class TupleEqualityTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub CompareTuples()
            Dim compilation = CreateCompilation("
Imports System

Class Holder
    Public Shared Same As Boolean = (1, 2) = (1, 2)
End Class

Module Program
    Function Show(b As Boolean) As String
        Return If(b, ""T"", ""F"")
    End Function

    Function Trace(name As String, value As Integer) As Integer
        Console.Write(name)
        Return value
    End Function

    Sub Main()
        Dim point As (X As Integer, Y As Integer) = (1, 2)
        Dim other = (1L, 2L)
        Dim nested = ((1, ""a""), 3)
        Dim big = (1, 2, 3, 4, 5, 6, 7, 8, 9)
        Console.Write(Show((1, 2) = (1, 2)) & Show((1, 2) = (1, 3)) & Show((1, 2) <> (1, 3)) & Show((1, 2) <> (1, 2)) & "" "")
        Console.Write(Show(point = (1, 2)) & Show(point = other) & Show(nested = ((1, ""a""), 3)) & Show(nested <> ((1, ""b""), 3)) & "" "")
        Console.Write(Show(big = (1, 2, 3, 4, 5, 6, 7, 8, 9)) & Show(big = (1, 2, 3, 4, 5, 6, 7, 8, 0)) & Show(Holder.Same) & "" "")
        Console.Write(Show((Trace(""a"", 1), Trace(""b"", 2)) = (Trace(""c"", 0), Trace(""d"", 2))) & "" "")
        If point = (1, 2) AndAlso point <> (2, 1) Then
            Console.Write(""if "")
        End If
        Select Case point
            Case (2, 1)
                Console.Write(""wrong"")
            Case (1, 2)
                Console.Write(""case"")
        End Select
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="TFTF TTTT TFT abcdF if case").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CompareNullableElements()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim n As Integer? = Nothing
        Dim m As Integer? = 1
        Dim unknown = (n, 1) = (1, 1)
        Dim known = (m, 1) = (1, 1)
        Console.Write(unknown.HasValue & "" "" & known.Value & "" "" & If((n, 2) = (1, 1), ""T"", ""F""))
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="False True F").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleEqualityErrors()
            Dim compilation = CreateCompilation("
Class Item
End Class

Module Program
    Sub Main()
        Dim a = (1, 2) = (1, 2, 3)
        Dim b = (New Item(), 1) = (New Item(), 1)
        Dim c = (1, 2) < (1, 2)
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30452: Operator '=' is not defined for types '(Integer, Integer)' and '(Integer, Integer, Integer)'.
        Dim a = (1, 2) = (1, 2, 3)
                ~~~~~~~~~~~~~~~~~~
BC30452: Operator '=' is not defined for types 'Item' and 'Item'.
        Dim b = (New Item(), 1) = (New Item(), 1)
                ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC30452: Operator '&lt;' is not defined for types '(Integer, Integer)' and '(Integer, Integer)'.
        Dim c = (1, 2) &lt; (1, 2)
                ~~~~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub TupleEqualityInExpressionTree()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Linq.Expressions

Module Program
    Sub Main()
        Dim e As Expression(Of Func(Of (Integer, Integer), Boolean)) = Function(t) t = (1, 2)
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC36534: Expression cannot be converted into an expression tree.
        Dim e As Expression(Of Func(Of (Integer, Integer), Boolean)) = Function(t) t = (1, 2)
                                                                                   ~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub TupleEqualityNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a = (1, 2) = (1, 2)
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30452: Operator '=' is not defined for types '(Integer, Integer)' and '(Integer, Integer)'.
        Dim a = (1, 2) = (1, 2)
                ~~~~~~~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
