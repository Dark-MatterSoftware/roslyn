' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Contracts.EditAndContinue
Imports Microsoft.CodeAnalysis.EditAndContinue
Imports Microsoft.CodeAnalysis.EditAndContinue.UnitTests
Imports Microsoft.CodeAnalysis.Emit

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

        <Fact>
        Public Sub VbNextStatementsHaveStatementLabels()
            Assert.True(SyntaxComparer.Statement.HasLabel(SyntaxKind.TupleDeconstructionStatement, ignoreVariableDeclarations:=False))
            Assert.True(SyntaxComparer.Statement.HasLabel(SyntaxKind.DeconstructionDeclarationStatement, ignoreVariableDeclarations:=False))
            Assert.True(SyntaxComparer.Statement.HasLabel(SyntaxKind.NothingCoalescingAssignmentStatement, ignoreVariableDeclarations:=False))
        End Sub

        <Theory>
        <InlineData(
            "        Dim (x, y) = (1, 2)" & vbCrLf & "        Return x + y",
            "        Dim (x, y) = (2, 3)" & vbCrLf & "        Return x + y")>
        <InlineData(
            "        Dim ((x, y), label) = ((1, 2), ""a"")" & vbCrLf & "        Return label",
            "        Dim ((x, y), label) = ((2, 3), ""b"")" & vbCrLf & "        Return label")>
        <InlineData(
            "        Dim text As String = Nothing" & vbCrLf & "        text ?= ""a""" & vbCrLf & "        Return text",
            "        Dim text As String = Nothing" & vbCrLf & "        text ?= ""b""" & vbCrLf & "        Return text")>
        <InlineData(
            "        Dim items = {1, 2, 3}" & vbCrLf & "        Return items(1..^0).Length",
            "        Dim items = {1, 2, 3}" & vbCrLf & "        Return items(0..^1).Length")>
        <InlineData(
            "        If TypeOf New Object() IsNot String Into text Then Return 1" & vbCrLf & "        Return text.Length",
            "        If TypeOf ""a"" IsNot String Into text Then Return 1" & vbCrLf & "        Return text.Length")>
        <InlineData(
            "        For Each (x, y) In {(1, 2)}" & vbCrLf & "            Return x + y" & vbCrLf & "        Next" & vbCrLf & "        Return 0",
            "        For Each (x, y) In {(2, 3)}" & vbCrLf & "            Return x + y" & vbCrLf & "        Next" & vbCrLf & "        Return 0")>
        Public Sub EditInsideVbNextForm(oldBody As String, newBody As String)
            GetTopEdits(Method(oldBody), Method(newBody), parseOptions:=s_vbNext).VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub PreserveDeconstructionLocals()
            Dim src1 = Method(
                "        Dim (<N:0>x</N:0>, <N:1>y</N:1>) = (1, 2)" & vbCrLf &
                "        Dim ((<N:2>a</N:2>, <N:3>b</N:3>), <N:4>label</N:4>) = ((3, 4), ""old"")" & vbCrLf &
                "        Dim <N:5>pairs</N:5> = {(5, 6)}" & vbCrLf &
                "        For Each (<N:6>k</N:6>, <N:7>value</N:7>) In pairs" & vbCrLf &
                "            <AS:0>Return x + y + a + b + k + value + label.Length</AS:0>" & vbCrLf &
                "        Next" & vbCrLf &
                "        Return 0")
            Dim src2 = Method(
                "        Dim (<N:0>x</N:0>, <N:1>y</N:1>) = (1, 2)" & vbCrLf &
                "        Dim ((<N:2>a</N:2>, <N:3>b</N:3>), <N:4>label</N:4>) = ((3, 4), ""new"")" & vbCrLf &
                "        Dim <N:5>pairs</N:5> = {(5, 6)}" & vbCrLf &
                "        For Each (<N:6>k</N:6>, <N:7>value</N:7>) In pairs" & vbCrLf &
                "            <AS:0>Return x + y + a + b + k + value + label.Length</AS:0>" & vbCrLf &
                "        Next" & vbCrLf &
                "        Return 0")

            Dim edits = GetTopEdits(src1, src2, parseOptions:=s_vbNext)
            edits.VerifySemantics(
                activeStatements:=GetActiveStatements(src1, src2, parseOptions:=s_vbNext),
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), edits.GetSyntaxMap())})
        End Sub

        <Fact>
        Public Sub ActiveStatementInForEachDeconstruction()
            Dim src1 = Method(
                "        Dim pairs = {(1, 2)}" & vbCrLf &
                "        For Each (x, y) In pairs" & vbCrLf &
                "            <AS:0>Return x + y</AS:0>" & vbCrLf &
                "        Next" & vbCrLf &
                "        Return 0")
            Dim src2 = Method(
                "        Dim pairs = {(1, 2)}" & vbCrLf &
                "        For Each (x, y) In pairs" & vbCrLf &
                "            <AS:0>Return x + y</AS:0>" & vbCrLf &
                "        Next" & vbCrLf &
                "        Return 1")

            Dim edits = GetTopEdits(src1, src2, parseOptions:=s_vbNext)
            Dim active = GetActiveStatements(src1, src2, parseOptions:=s_vbNext)

            edits.VerifySemanticDiagnostics(active)
        End Sub
        Private Shared Function AsyncMethod(body As String) As String
            Return "
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Class C
    Async Function F(items As IAsyncEnumerable(Of (Integer, Integer))) As Task(Of Integer)
" & body & "
    End Function
End Class
"
        End Function

        <Fact>
        Public Sub ActiveStatementInAwaitEachDeconstruction()
            Dim src1 = AsyncMethod(
                "        Await Each (x, y) In items" & vbCrLf &
                "            <AS:0>Return x + y</AS:0>" & vbCrLf &
                "        Next" & vbCrLf &
                "        Return 0")
            Dim src2 = AsyncMethod(
                "        Await Each (x, y) In items" & vbCrLf &
                "            <AS:0>Return x + y</AS:0>" & vbCrLf &
                "        Next" & vbCrLf &
                "        Return 1")

            Dim edits = GetTopEdits(src1, src2, parseOptions:=s_vbNext)
            Dim active = GetActiveStatements(src1, src2, parseOptions:=s_vbNext)
            edits.VerifySemanticDiagnostics(active, capabilities:=EditAndContinueCapabilities.AddInstanceFieldToExistingType)
        End Sub

        <Fact>
        Public Sub ActiveStatementInAsyncUsing()
            Dim src1 = "
Imports System
Imports System.IO
Imports System.Threading.Tasks
Class C
    Async Function F() As Task
        Async Using stream As New MemoryStream()
            <AS:0>Await stream.WriteAsync(New Byte() {1}, 0, 1)</AS:0>
        End Using
    End Function
End Class
"
            Dim src2 = "
Imports System
Imports System.IO
Imports System.Threading.Tasks
Class C
    Async Function F() As Task
        Async Using stream As New MemoryStream()
            <AS:0>Await stream.WriteAsync(New Byte() {1, 2}, 0, 2)</AS:0>
        End Using
    End Function
End Class
"
            Dim edits = GetTopEdits(src1, src2, parseOptions:=s_vbNext)
            Dim active = GetActiveStatements(src1, src2, parseOptions:=s_vbNext)
            edits.VerifySemanticDiagnostics(active, capabilities:=EditAndContinueCapabilities.AddInstanceFieldToExistingType)
        End Sub

        <Fact>
        Public Sub InsertAwaitEachAroundActiveStatementIsRude()
            Dim src1 = AsyncMethod(
                "        <AS:0>System.Console.WriteLine(1)</AS:0>" & vbCrLf &
                "        Return 0")
            Dim src2 = AsyncMethod(
                "        Await Each (x, y) In items" & vbCrLf &
                "            <AS:0>System.Console.WriteLine(1)</AS:0>" & vbCrLf &
                "        Next" & vbCrLf &
                "        Return 0")

            Dim edits = GetTopEdits(src1, src2, parseOptions:=s_vbNext)
            Dim active = GetActiveStatements(src1, src2, parseOptions:=s_vbNext)
            edits.VerifySemanticDiagnostics(active,
                Diagnostic(RudeEditKind.InsertAroundActiveStatement, "Await Each (x, y) In items", VBFeaturesResources.For_Each_block))
        End Sub

        <Fact>
        Public Sub InsertAsyncUsingAroundActiveStatementIsRude()
            Dim src1 = "
Imports System
Imports System.IO
Imports System.Threading.Tasks
Class C
    Async Function F() As Task
        <AS:0>System.Console.WriteLine(1)</AS:0>
    End Function
End Class
"
            Dim src2 = "
Imports System
Imports System.IO
Imports System.Threading.Tasks
Class C
    Async Function F() As Task
        Async Using stream As New MemoryStream()
            <AS:0>System.Console.WriteLine(1)</AS:0>
        End Using
    End Function
End Class
"
            Dim edits = GetTopEdits(src1, src2, parseOptions:=s_vbNext)
            Dim active = GetActiveStatements(src1, src2, parseOptions:=s_vbNext)
            edits.VerifySemanticDiagnostics(active,
                Diagnostic(RudeEditKind.InsertAroundActiveStatement, "Async Using stream As New MemoryStream()", VBFeaturesResources.Using_block))
        End Sub

        <Fact>
        Public Sub PreservePatternAndCoalescingLocals()
            Dim src1 = Method(
                "        Dim <N:0>text</N:0> As String = Nothing" & vbCrLf &
                "        text ?= ""old""" & vbCrLf &
                "        If TypeOf text Is String Into matched Then" & vbCrLf &
                "            <AS:0>Return matched.Length + text.Length</AS:0>" & vbCrLf &
                "        End If" & vbCrLf &
                "        Return 0")
            Dim src2 = Method(
                "        Dim <N:0>text</N:0> As String = Nothing" & vbCrLf &
                "        text ?= ""new""" & vbCrLf &
                "        If TypeOf text Is String Into matched Then" & vbCrLf &
                "            <AS:0>Return matched.Length + text.Length</AS:0>" & vbCrLf &
                "        End If" & vbCrLf &
                "        Return 0")

            Dim edits = GetTopEdits(src1, src2, parseOptions:=s_vbNext)
            edits.VerifySemantics(
                activeStatements:=GetActiveStatements(src1, src2, parseOptions:=s_vbNext),
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), edits.GetSyntaxMap())})
        End Sub
    End Class
End Namespace