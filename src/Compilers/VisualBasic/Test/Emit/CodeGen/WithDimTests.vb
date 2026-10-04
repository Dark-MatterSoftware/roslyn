' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class WithDimTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub WithDim_DeclaresTheWithObject()
            Dim compilation = CreateCompilation("
Class Customer
    Public Name As String
    Public Id As Integer
End Class
Class Program
    Shared Sub Main()
        With Dim customer = New Customer()
            .Name = ""Ada""
            customer.Id = 7
            Dim describe = Function() customer.Name & .Id
            System.Console.Write(describe())
        End With
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Ada7").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub WithDim_AsClauseConvertsTheValue()
            Dim compilation = CreateCompilation("
Option Strict On
Class Program
    Shared Sub Main()
        With Dim total As Long = 41
            System.Console.Write(.GetType().Name)
            System.Console.Write(total + 1)
        End With
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Int6442").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub WithDim_StructureMembersWriteTheLocal()
            Dim compilation = CreateCompilation("
Structure Point
    Public X As Integer
End Structure
Class Program
    Shared Sub Main()
        With Dim p = New Point()
            .X = 5
            System.Console.Write(p.X)
        End With
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub WithDim_ValueBindsAgainstTheEnclosingWith()
            Dim compilation = CreateCompilation("
Class Node
    Public Name As String
    Public Child As Node
End Class
Class Program
    Shared Sub Main()
        Dim root = New Node() With {.Name = ""root"", .Child = New Node() With {.Name = ""leaf""}}
        With root
            With Dim leaf = .Child
                System.Console.Write(.Name)
                System.Console.Write(leaf Is root.Child)
            End With
            System.Console.Write(.Name)
        End With
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="leafTrueroot").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub WithDim_LocalIsScopedToTheBlock()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        With Dim text = ""abc""
            System.Console.Write(.Length)
        End With
        System.Console.Write(text)
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotDeclared1, "text").WithArguments("text").WithLocation(7, 30))
        End Sub

        <Fact>
        Public Sub WithDim_EqualityTestKeepsItsMeaning()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim x = ""Test""
        With x = ""Test""
            System.Console.Write(.ToString())
        End With
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="True").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub WithDim_RequiresAnInitialValue()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        With Dim count As Integer
        End With
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedEQ, "").WithLocation(4, 34))
        End Sub

        <Fact>
        Public Sub WithDim_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        With Dim text = ""abc""
        End With
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(4, 14))
        End Sub

    End Class

End Namespace
