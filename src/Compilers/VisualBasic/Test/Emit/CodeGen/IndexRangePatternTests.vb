' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class IndexRangePatternTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub IndexesAndSlicesListsStringsAndCustomTypes()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Class Window
    Private ReadOnly _items As Integer()

    Public Sub New(ParamArray items As Integer())
        _items = items
    End Sub

    Public ReadOnly Property Length As Integer
        Get
            Return _items.Length
        End Get
    End Property

    Default Public ReadOnly Property Item(index As Integer) As Integer
        Get
            Return _items(index)
        End Get
    End Property

    Public Function Slice(start As Integer, length As Integer) As Window
        Dim part(length - 1) As Integer
        System.Array.Copy(_items, start, part, 0, length)
        Return New Window(part)
    End Function
End Class

Module Program
    Private Calls As Integer

    Function Make() As List(Of Integer)
        Calls += 1
        Return New List(Of Integer) From {10, 20, 30, 40}
    End Function

    Sub Main()
        Dim list As New List(Of Integer) From {1, 2, 3, 4}
        Dim s = ""hello""
        System.Console.Write(list(^1) & s(^1) & s(1..3) & s(..2) & s(^2..) & "" "")
        list(^1) = 40
        System.Console.Write(list(3) & "" "" & Make()(^2) & "" "" & Make()(^1) & "" "" & Calls & "" "")
        Dim w As New Window(5, 6, 7, 8)
        System.Console.Write(w(^1) & "" "" & w(1..^1).Length & "" "" & w(1..^1)(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4oelhelo 40 30 40 2 8 2 6").VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SlicesListsThroughSlice()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Private Calls As Integer

    Function Make() As List(Of Integer)
        Calls += 1
        Return New List(Of Integer) From {10, 20, 30, 40}
    End Function

    Sub Main()
        Dim list As New List(Of Integer) From {1, 2, 3, 4}
        System.Console.Write(String.Join("","", list(1..3)) & "" "" & Make()(1..^1).Count & "" "" & Calls)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2,3 2 1", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TypesWithoutTheMembersStillReportTheErrors()
            Dim compilation = CreateCompilation("
Class Indexed
    Default Public ReadOnly Property Item(index As Integer) As Integer
        Get
            Return index
        End Get
    End Property
End Class

Class Counted
    Public ReadOnly Property Count As Integer
        Get
            Return 3
        End Get
    End Property

    Default Public ReadOnly Property Item(index As Integer) As Integer
        Get
            Return index
        End Get
    End Property
End Class

Module Program
    Sub Main()
        Dim i As New Indexed()
        Dim c As New Counted()
        Dim a = i(^1)
        Dim b = c(^1)
        Dim d = c(1..2)
    End Sub
End Module
", parseOptions:=VbNext)

            ' Indexed has no Count or Length, and Counted has no Slice.
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_IndexFromEndRequiresArray, "^1").WithLocation(28, 19),
                Diagnostic(ERRID.ERR_RangeRequiresArray, "1..2").WithLocation(30, 19))
        End Sub

        <Fact>
        Public Sub RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Sub Main()
        Dim list As New List(Of Integer) From {1, 2, 3}
        Dim s = ""hello""
        Dim a = list(^1)
        Dim b = s(1..3)
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(8, 22),
                Diagnostic(ERRID.ERR_ExpectedIdentifier, "").WithLocation(9, 21))
        End Sub
    End Class

End Namespace
