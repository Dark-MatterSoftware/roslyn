' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class SelectCaseTypePatternTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub SelectCase_TypeAndRelation()
            Dim compilation = CreateCompilation("
Class Program
    Shared n As Integer
    Shared Function NextValue() As Object
        n += 1
        Return n
    End Function
    Shared Sub Main()
        Select Case NextValue()
            Case Is TypeOf Integer AndAlso Is > 0
                System.Console.Write(""positive"")
            Case Else
                System.Console.Write(""other"")
        End Select
        Select Case DirectCast(""hi"", Object)
            Case Is TypeOf Integer AndAlso Is > 0
                System.Console.Write(""number"")
            Case Is TypeOf String
                System.Console.Write(""text"")
            Case Else
                System.Console.Write(""other"")
        End Select
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="positivetext1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SelectCase_TypePatternRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim value As Object = 1
        Select Case value
            Case Is TypeOf Integer
        End Select
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedRelational, "").WithLocation(6, 21))
        End Sub

        <Fact>
        Public Sub SelectCase_PropertyPattern()
            Dim compilation = CreateCompilation("
Class Customer
    Public Balance As Integer
    Public Name As String
End Class
Class Program
    Shared n As Integer
    Shared Function NextCustomer() As Object
        n += 1
        Dim customer As New Customer()
        customer.Balance = 20
        customer.Name = ""A""
        Return customer
    End Function
    Shared Sub Main()
        Select Case NextCustomer()
            Case Is TypeOf Customer AndAlso .Balance Is > 0 AndAlso .Name = ""A""
                System.Console.Write(""match"")
            Case Else
                System.Console.Write(""other"")
        End Select
        Select Case DirectCast(""hi"", Object)
            Case Is TypeOf Customer AndAlso .Name = ""A""
                System.Console.Write(""customer"")
            Case Else
                System.Console.Write(""text"")
        End Select
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="matchtext1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SelectCase_PositionalPattern()
            Dim compilation = CreateCompilation("
Class Point
    Public X As Integer
    Public Y As Integer
    Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub
    Sub Deconstruct(ByRef a As Integer, ByRef b As Integer)
        a = X
        b = Y
    End Sub
End Class
Class Program
    Shared n As Integer
    Shared Function NextPoint() As Object
        n += 1
        Return New Point(1, 2)
    End Function
    Shared Function NextPair() As (Integer, Integer)
        n += 1
        Return (3, 4)
    End Function
    Shared Sub Main()
        Select Case NextPoint()
            Case Is TypeOf Point(1, 2)
                System.Console.Write(""point"")
            Case Else
                System.Console.Write(""no"")
        End Select
        Select Case NextPair()
            Case Is (3, 4)
                System.Console.Write(""pair"")
            Case Else
                System.Console.Write(""no"")
        End Select
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="pointpair2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SelectCase_ExpressionProducesAValue()
            Dim compilation = CreateCompilation("
Class Program
    Shared n As Integer
    Shared Function NextOrder() As Integer
        n += 1
        Return 1
    End Function
    Shared Sub Main()
        Dim label = Select Case NextOrder()
                        Case Is > 0
                            ""positive""
                        Case Else
                            ""other""
                    End Select
        System.Console.Write(label)
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="positive1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SelectCase_ExpressionRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim label = Select Case 1
                        Case Is > 0
                            ""positive""
                        Case Else
                            ""other""
                    End Select
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(4, 21),
                Diagnostic(ERRID.ERR_CaseNoSelect, "Case Is > 0").WithLocation(5, 25),
                Diagnostic(ERRID.ERR_Syntax, """positive""").WithLocation(6, 29),
                Diagnostic(ERRID.ERR_CaseElseNoSelect, "Case Else").WithLocation(7, 25),
                Diagnostic(ERRID.ERR_Syntax, """other""").WithLocation(8, 29),
                Diagnostic(ERRID.ERR_EndSelectNoSelect, "End Select").WithLocation(9, 21))
        End Sub

        <Fact>
        Public Sub SelectCase_ListPatternMatchesAnArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared n As Integer
    Shared Function NextValues() As Integer()
        n += 1
        Return New Integer() {1, 2}
    End Function
    Shared Sub Main()
        Select Case NextValues()
            Case Is {1, 2}
                System.Console.Write(""pair"")
            Case Else
                System.Console.Write(""other"")
        End Select
        Select Case New Integer() {}
            Case Is {}
                System.Console.Write(""empty"")
            Case Else
                System.Console.Write(""full"")
        End Select
        Select Case New Integer() {9}
            Case Is {1, 2}
                System.Console.Write(""pair"")
            Case Else
                System.Console.Write(""miss"")
        End Select
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="pairemptymiss1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SelectCase_ListPatternRequiresAnArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Select Case 1
            Case Is {1}
        End Select
        Dim grid = New Integer(1, 1) {}
        Select Case grid
            Case Is {1}
        End Select
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ListPatternRequiresArray, "Is {1}").WithLocation(5, 18),
                Diagnostic(ERRID.ERR_ListPatternRequiresArray, "Is {1}").WithLocation(9, 18))
        End Sub

        <Fact>
        Public Sub SelectCase_ListPatternRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim values = New Integer() {1, 2}
        Select Case values
            Case Is {1, 2}
        End Select
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedRelational, "").WithLocation(6, 21))
        End Sub
    End Class
End Namespace
