' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class AsyncIteratorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub AsyncIterator_BindsAwaitAndYield()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Async Iterator Function Numbers(count As Integer) As IAsyncEnumerable(Of Integer)
        For i As Integer = 1 To count
            Await Task.Yield()
            Yield i
        Next
        Return
    End Function

    Async Iterator Function Letters() As IAsyncEnumerator(Of String)
        Yield ""a""
        Await Task.Delay(1)
        Yield ""b""
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
        End Sub

        <Fact>
        Public Sub AsyncIterator_ReturnTypeAndBodyErrors()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Async Iterator Function WrongType() As Task(Of Integer)
        Yield 1
    End Function

    Async Iterator Function ReturnsAValue() As IAsyncEnumerable(Of Integer)
        Return 1
    End Function

    Async Iterator Function WrongElement() As IAsyncEnumerable(Of Integer)
        Yield ""text""
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37393: An 'Async Iterator' function must return 'IAsyncEnumerable(Of T)' or 'IAsyncEnumerator(Of T)'.
    Async Iterator Function WrongType() As Task(Of Integer)
                                           ~~~~~~~~~~~~~~~~
BC36942: To return a value from an Iterator function, use 'Yield' rather than 'Return'.
        Return 1
        ~~~~~~~~
BC30512: Option Strict On disallows implicit conversions from 'String' to 'Integer'.
        Yield "text"
              ~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AsyncIterator_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Async Iterator Function Numbers() As IAsyncEnumerable(Of Integer)
        Yield 1
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            Dim errors = compilation.GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error).Select(Function(d) d.Code).Distinct().ToArray()
            Assert.Contains(CInt(ERRID.ERR_InvalidAsyncIteratorModifiers), errors)
        End Sub

        <Fact>
        Public Sub AsyncIterator_ReportsThatEmitIsNotAvailableYet()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Async Iterator Function Numbers() As IAsyncEnumerable(Of Integer)
        Yield 1
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim result = compilation.Emit(New System.IO.MemoryStream())
            Assert.False(result.Success)
            result.Diagnostics.Verify(Diagnostic(ERRID.ERR_AsyncIteratorNotAvailableYet, "Numbers").WithLocation(5, 29))
        End Sub
    End Class
End Namespace
