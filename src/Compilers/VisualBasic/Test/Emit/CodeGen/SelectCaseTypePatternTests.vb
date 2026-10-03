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
    End Class
End Namespace
