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

        <Theory>
        <InlineData(False)>
        <InlineData(True)>
        Public Sub AsyncIterator_Emits(debug As Boolean)
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Class C
    Private ReadOnly _offset As Integer = 10

    Async Iterator Function Numbers(count As Integer) As IAsyncEnumerable(Of Integer)
        Try
            For i As Integer = 1 To count
                Await Task.Yield()
                Yield i + _offset
            Next
        Finally
            Console.WriteLine(""finally"")
        End Try
    End Function

    Shared Async Iterator Function Letters(Of T)(value As T) As IAsyncEnumerator(Of T)
        Yield value
        Yield Await Task.FromResult(value)
    End Function
End Class
", targetFramework:=TargetFramework.Net90, options:=If(debug, TestOptions.DebugDll, TestOptions.ReleaseDll), parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        Private Function RunAsyncIterator(body As String, expectedOutput As String) As CompilationVerifier
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
" & body & "
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput, verify:=Verification.Skipped).VerifyDiagnostics()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIterator_Enumerable_YieldsAcrossAwaits()
            RunAsyncIterator("
    Async Iterator Function Numbers(count As Integer) As IAsyncEnumerable(Of Integer)
        For i As Integer = 1 To count
            Await Task.Yield()
            Yield i
        Next
        Console.Write(""end "")
    End Function

    Async Function Run() As Task
        Await Each n In Numbers(3)
            Console.Write(n & "" "")
        Next
        Console.Write(""after"")
    End Function
", "1 2 3 end after")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIterator_Enumerator_MovesByHand()
            RunAsyncIterator("
    Async Iterator Function Letters() As IAsyncEnumerator(Of String)
        Yield ""a""
        Await Task.Delay(1)
        Yield ""b""
    End Function

    Async Function Run() As Task
        Dim e As IAsyncEnumerator(Of String) = Letters()
        While Await e.MoveNextAsync()
            Console.Write(e.Current & "" "")
        End While
        Await e.DisposeAsync()
        Console.Write(""done"")
    End Function
", "a b done")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIterator_FinallyRunsOnExitFor()
            RunAsyncIterator("
    Async Iterator Function Numbers() As IAsyncEnumerable(Of Integer)
        Try
            Yield 1
            Await Task.Yield()
            Yield 2
            Console.Write(""unreached "")
            Yield 3
        Finally
            Console.Write(""finally "")
        End Try
    End Function

    Async Function Run() As Task
        Await Each n In Numbers()
            Console.Write(n & "" "")
            If n = 2 Then Exit For
        Next
        Console.Write(""after"")
    End Function
", "1 2 finally after")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIterator_NestedFinallyRunsInOrder()
            RunAsyncIterator("
    Async Iterator Function Numbers() As IAsyncEnumerable(Of Integer)
        Try
            Try
                Yield 1
                Yield 2
            Finally
                Console.Write(""inner "")
            End Try
            Console.Write(""between "")
        Finally
            Console.Write(""outer "")
        End Try
    End Function

    Async Function Run() As Task
        Await Each n In Numbers()
            Console.Write(n & "" "")
            Exit For
        Next
        Console.Write(""| "")
        Await Each n In Numbers()
            Console.Write(n & "" "")
        Next
    End Function
", "1 inner outer | 1 2 inner between outer ")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIterator_ReturnEndsTheIteration()
            RunAsyncIterator("
    Async Iterator Function Numbers(stopAt As Integer) As IAsyncEnumerable(Of Integer)
        Try
            For i As Integer = 1 To 10
                If i = stopAt Then Return
                Yield i
            Next
        Finally
            Console.Write(""finally "")
        End Try
        Console.Write(""unreached "")
    End Function

    Async Function Run() As Task
        Await Each n In Numbers(3)
            Console.Write(n & "" "")
        Next
        Console.Write(""after"")
    End Function
", "1 2 finally after")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIterator_ExceptionReachesTheConsumer()
            RunAsyncIterator("
    Async Iterator Function Numbers() As IAsyncEnumerable(Of Integer)
        Try
            Yield 1
            Await Task.Yield()
            Throw New InvalidOperationException(""boom"")
        Finally
            Console.Write(""finally "")
        End Try
    End Function

    Async Function Run() As Task
        Try
            Await Each n In Numbers()
                Console.Write(n & "" "")
            Next
        Catch ex As InvalidOperationException
            Console.Write(ex.Message)
        End Try
    End Function
", "1 finally boom")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIterator_EnumeratesTwiceWithFreshParameters()
            RunAsyncIterator("
    Async Iterator Function Countdown(start As Integer) As IAsyncEnumerable(Of Integer)
        While start > 0
            Yield start
            start -= 1
            Await Task.Yield()
        End While
    End Function

    Async Function Run() As Task
        Dim numbers = Countdown(3)
        Await Each n In numbers
            Console.Write(n & "" "")
        Next
        Await Each n In numbers
            Console.Write(n & "" "")
        Next
        Dim first = numbers.GetAsyncEnumerator()
        Dim second = numbers.GetAsyncEnumerator()
        Console.Write(Object.ReferenceEquals(first, second))
    End Function
", "3 2 1 3 2 1 False")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIterator_GenericInstanceAndYieldAwait()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Class Repeater(Of T)
    Private ReadOnly _value As T

    Sub New(value As T)
        _value = value
    End Sub

    Async Iterator Function Repeat(count As Integer) As IAsyncEnumerable(Of T)
        For i As Integer = 1 To count
            Yield Await Task.FromResult(_value)
        Next
    End Function

    Shared Async Iterator Function Pair(Of U)(first As T, second As U) As IAsyncEnumerable(Of String)
        Await Task.Yield()
        Yield first.ToString()
        Yield second.ToString()
    End Function
End Class

Module Program
    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub

    Async Function Run() As Task
        Await Each s In New Repeater(Of String)(""x"").Repeat(3)
            Console.Write(s)
        Next
        Console.Write("" "")
        Await Each s In Repeater(Of Integer).Pair(Of Double)(1, 2.5)
            Console.Write(s & "" "")
        Next
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="xxx 1 2.5 ", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub
    End Class
End Namespace
