' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RecordTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub RecordClass_EmitsEqualityHashCodeAndPrint()
            Dim compilation = CreateCompilation("
Record Class Point
    Public Property X As Integer
    Public Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1, .Y = 2}
        Dim b As New Point With {.X = 1, .Y = 2}
        Dim c As New Point With {.X = 1, .Y = 3}
        System.Console.Write(a.Equals(b))
        System.Console.Write(a.Equals(c))
        System.Console.Write(a.GetHashCode() = b.GetHashCode())
        System.Console.Write(a.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="TrueFalseTruePoint { X = 1, Y = 2 }").VerifyDiagnostics()
            Dim point = DirectCast(compilation.GlobalNamespace.GetMembers("Point").Single(), INamedTypeSymbol)
            Assert.True(point.IsRecord)
        End Sub

        <Fact>
        Public Sub RecordClass_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Record Class Point
    Public Property X As Integer
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedDeclaration, "Record").WithLocation(2, 1))
        End Sub

        <Fact>
        Public Sub RecordStructure_IsRejected()
            Dim compilation = CreateCompilation("
Record Structure Point
    Public Property X As Integer
End Structure
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadRecordFlags1, "Record").WithArguments("Record").WithLocation(2, 1))
        End Sub
    End Class

End Namespace
