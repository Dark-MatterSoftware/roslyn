' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullConditionalAssignmentTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub NullConditionalAssignment_AssignsOnlyWhenReceiverIsNotNothing()
            Dim compilation = CreateCompilation("
Class Box
    Public Property Value As Integer
    Public Field As Integer
    Public Shared Count As Integer

    Public Shared Function Bump(n As Integer) As Integer
        Count += 1
        Return n
    End Function
End Class

Class Program
    Shared Sub Main()
        Dim box As Box = Nothing
        box?.Value = Box.Bump(1)
        box?.Field = Box.Bump(2)
        System.Console.Write(Box.Count)
        box = New Box()
        box?.Value = Box.Bump(3)
        box?.Field = Box.Bump(4)
        System.Console.Write(Box.Count)
        System.Console.Write(box.Value)
        System.Console.Write(box.Field)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0234").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NullConditionalAssignment_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Box
    Public Value As Integer
End Class

Class Program
    Shared Sub Main()
        Dim box As Box = Nothing
        box?.Value = 1
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_LValueRequired, "box?.Value").WithLocation(9, 9))
        End Sub
    End Class
End Namespace
