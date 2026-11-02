' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.ObjectModel
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.ExpressionEvaluator
Imports Microsoft.CodeAnalysis.ExpressionEvaluator.UnitTests
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator.UnitTests
    ''' <summary>
    ''' The expression evaluator parses with the language version that a module's portable PDB records, so the Watch and
    ''' Immediate windows take vbnext forms in a module built with vbnext.
    ''' </summary>
    Public Class VbNextExpressionTests
        Inherits ExpressionCompilerTestBase

        Private Const Source = "
Structure Spot
    Public X As Integer
    Public Y As Integer
End Structure
Class C
    Shared Sub M(a As Integer(), s As Spot, n As Integer)
    End Sub
End Class
"

        Private Shared ReadOnly s_vbNextExpressions As String() = {
            "a(^1)",
            "a(1..2)",
            "s With {.X = 5}",
            "New With {.A = 1} With {.A = 2}",
            "Select Case n" & vbCrLf & "Case Is > 0" & vbCrLf & """positive""" & vbCrLf & "Case Else" & vbCrLf & """other""" & vbCrLf & "End Select"}

        Private Function Compile(parseOptions As VisualBasicParseOptions, debugFormat As DebugInformationFormat, expr As String, ByRef formatSpecifiers As ReadOnlyCollection(Of String)) As String
            Dim comp = CreateCompilationWithMscorlib40({Source}, options:=TestOptions.DebugDll, parseOptions:=parseOptions)
            Dim runtime = CreateRuntimeInstance(comp, debugFormat:=debugFormat)
            Dim context = CreateMethodContext(runtime, "C.M")
            Dim errorMessage As String = Nothing
            Dim result = context.CompileExpression(expr, errorMessage, formatter:=DebuggerDiagnosticFormatter.Instance)
            formatSpecifiers = result?.FormatSpecifiers
            Return If(errorMessage, If(result Is Nothing, "no result", "OK"))
        End Function

        <Fact>
        Public Sub VbNextModule_EvaluatesVbNextForms()
            Dim vbNext = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
            For Each expr In s_vbNextExpressions
                Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
                Assert.Equal(expr & ": OK", expr & ": " & Compile(vbNext, DebugInformationFormat.PortablePdb, expr, formatSpecifiers))
            Next
        End Sub

        <Fact>
        Public Sub VbNextModule_KeepsFormatSpecifiers()
            Dim vbNext = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
            Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
            Assert.Equal("OK", Compile(vbNext, DebugInformationFormat.PortablePdb, "a(^1), h", formatSpecifiers))
            Assert.Equal({"h"}, formatSpecifiers)
        End Sub

        <Fact>
        Public Sub Vb17_13Module_KeepsCurrentSyntax()
            Dim vb17_13 = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
            Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
            Assert.Equal("error BC30201: Expression expected.", Compile(vb17_13, DebugInformationFormat.PortablePdb, "a(^1)", formatSpecifiers))
        End Sub

        <Fact>
        Public Sub WindowsPdb_KeepsCurrentSyntax()
            Dim vbNext = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
            Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
            Assert.Equal("error BC30201: Expression expected.", Compile(vbNext, DebugInformationFormat.Pdb, "a(^1)", formatSpecifiers))
        End Sub
    End Class
End Namespace
