' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.EditAndContinue.UnitTests

Namespace Microsoft.CodeAnalysis.VisualBasic.EditAndContinue.UnitTests

    ''' <summary>
    ''' Edit and Continue analyzes edits to methods that use the Vb.Next forms.
    ''' </summary>
    <UseExportProvider>
    Public Class VbNextEditAndContinueTests
        Inherits EditingTestBase

        Private Shared ReadOnly s_vbNext As VisualBasicParseOptions = VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)

        Private Shared Function Method(body As String) As String
            Return "
Class C
    Function F(v As Integer) As Object
" & body & "
    End Function
End Class
"
        End Function

        Private Const SelectCaseExpression As String = "
        Dim w = Select Case v
                    Case 1
                        ""one""
                    Case Else
                        ""other""
                End Select"

        <Fact>
        Public Sub EditBesideSelectCaseExpression()
            Dim src1 = Method(SelectCaseExpression & vbCrLf & "        Return 1")
            Dim src2 = Method(SelectCaseExpression & vbCrLf & "        Return 2")
            GetTopEdits(src1, src2, parseOptions:=s_vbNext).VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub EditInsideSelectCaseExpression()
            Dim src1 = Method(SelectCaseExpression & vbCrLf & "        Return w")
            Dim src2 = Method(SelectCaseExpression.Replace("""one""", """uno""") & vbCrLf & "        Return w")
            GetTopEdits(src1, src2, parseOptions:=s_vbNext).VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub InsertArmInSelectCaseExpression()
            Dim src1 = Method(SelectCaseExpression & vbCrLf & "        Return w")
            Dim src2 = Method(SelectCaseExpression.Replace("                    Case Else", "                    Case 2" & vbCrLf & "                        ""two""" & vbCrLf & "                    Case Else") & vbCrLf & "        Return w")
            GetTopEdits(src1, src2, parseOptions:=s_vbNext).VerifySemanticDiagnostics()
        End Sub

        <Theory>
        <InlineData("Dim ((x, y), label) = ((1, 2), ""a"")")>
        <InlineData("Dim (n As Long, s As String) = (1, ""a"")")>
        <InlineData("Select Case (1, ""a"") : Case Is (> 0, ""a"") : End Select")>
        <InlineData("For Each (k, s) In {(1, ""a"")} : Next")>
        <InlineData("Dim items = {1, 2, 3} : Dim last = items(^1) : Dim mid = items(1..2)")>
        <InlineData("Dim text As String = Nothing : text ?= ""x""")>
        <InlineData("Dim spot = New With {.X = 1} : Dim moved = spot With {.X = 2}")>
        Public Sub EditBesideVbNextForm(form As String)
            Dim src1 = Method("        " & form & vbCrLf & "        Return 1")
            Dim src2 = Method("        " & form & vbCrLf & "        Return 2")
            GetTopEdits(src1, src2, parseOptions:=s_vbNext).VerifySemanticDiagnostics()
        End Sub
    End Class
End Namespace
