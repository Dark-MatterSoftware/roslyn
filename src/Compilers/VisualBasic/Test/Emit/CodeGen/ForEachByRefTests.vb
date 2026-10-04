' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ForEachByRefTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ForEach_ByRefWritesEachArrayElement()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim values = {1, 2, 3}
        For Each ByRef item In values
            item += 1
        Next
        System.Console.Write(values(0))
        System.Console.Write(values(1))
        System.Console.Write(values(2))
        Dim typed As Integer() = {10, 20}
        For Each ByRef item As Integer In typed
            item += 1
        Next
        System.Console.Write(typed(0))
        System.Console.Write(typed(1))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2341121").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEach_ByRefWritesAStructureField()
            Dim compilation = CreateCompilation("
Structure Point
    Public X As Integer
End Structure
Class Program
    Shared Sub Main()
        Dim values(0) As Point
        values(0).X = 1
        For Each ByRef item In values
            item.X = 7
        Next
        System.Console.Write(values(0).X)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="7").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEach_ByRefWithIndex()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim values = {10, 20}
        For Each ByRef item With index In values
            item += index
        Next
        System.Console.Write(values(0))
        System.Console.Write(values(1))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1021").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEach_ContinueForStillMovesToTheNextElement()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim values = {1, 2, 3}
        For Each ByRef item In values
            If item = 2 Then Continue For
            item += 1
        Next
        System.Console.Write(values(0))
        System.Console.Write(values(1))
        System.Console.Write(values(2))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="224").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEach_WithoutByRefStillCopies()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim values = {1, 2, 3}
        For Each item In values
            item += 1
        Next
        System.Console.Write(values(0))
        System.Console.Write(values(1))
        System.Console.Write(values(2))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="123").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEach_ByRefRequiresAOneDimensionalArray()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim values As New System.Collections.Generic.List(Of Integer) From {1}
        For Each ByRef item In values
        Next
        Dim text = ""ab""
        For Each ByRef letter In text
        Next
        Dim numbers = {1, 2}
        For Each ByRef item As Object In numbers
        Next
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ForEachByRefRequiresArray, "values").WithLocation(5, 32),
                Diagnostic(ERRID.ERR_ForEachByRefRequiresArray, "text").WithLocation(8, 34),
                Diagnostic(ERRID.ERR_ForEachByRefRequiresArray, "numbers").WithLocation(11, 42))
        End Sub

        <Fact>
        Public Sub ForEach_ByRefDeclaresANewVariable()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim item = 1
        Dim values = {1, 2}
        For Each ByRef item In values
        Next
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ForEachByRefDeclaresVariable, "ByRef").WithLocation(6, 18))
        End Sub

        <Fact>
        Public Sub ForEach_ByRefCannotBeCaptured()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim values = {1}
        For Each ByRef item In values
            Dim f = Function() item
        Next
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_CannotLiftByRefLocalLambda1, "item").WithArguments("item").WithLocation(6, 32))
        End Sub

        <Fact>
        Public Sub ForEach_ByRefRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim values = {1, 2, 3}
        For Each ByRef item In values
            item += 1
        Next
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(5, 18),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "item").WithArguments("item").WithLocation(6, 13))
        End Sub

    End Class

End Namespace
