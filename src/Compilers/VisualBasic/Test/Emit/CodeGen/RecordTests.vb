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

        <Fact>
        Public Sub PositionalRecord_ConstructsPrintsAndDeconstructs()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point(X As Integer, Y As Integer)
End Class

Module Program
    Sub Main()
        Dim a As New Point(1, 2)
        Dim b As New Point(1, 2)
        Dim (x, y) = a
        System.Console.Write(a.X)
        System.Console.Write(a.Y)
        System.Console.Write(x)
        System.Console.Write(y)
        System.Console.Write(a.Equals(b))
        System.Console.Write(a.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1212TruePoint { X = 1, Y = 2 }").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordWith_CopiesAndAssignsInitOnly()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point
    Public Property X As Integer
    Public InitOnly Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1, .Y = 2}
        Dim b = a With {.Y = 3}
        System.Console.Write(a.X)
        System.Console.Write(a.Y)
        System.Console.Write(b.X)
        System.Console.Write(b.Y)
        System.Console.Write(a.Equals(b))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1213False").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordWith_InitOnlyStaysReadOnly()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point
    Public InitOnly Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.Y = 2}
        a.Y = 9
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AssignmentInitOnly, "a.Y = 9").WithArguments("Y").WithLocation(14, 9))
        End Sub

        <Fact>
        Public Sub RecordWith_RejectsNonRecord()
            Dim compilation = CreateCompilation("
Class Point
    Public Property X As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1}
        Dim b = a With {.X = 2}
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_WithCopyNotRecord, "With {.X = 2}").WithArguments("Point").WithLocation(9, 19))
        End Sub

        <Fact>
        Public Sub RecordWith_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Point
    Public Property X As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1}
        Dim b = a With {.X = 2}
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedEOS, "With").WithLocation(9, 19))
        End Sub
    End Class

End Namespace
