' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class AwaitInFinallyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Function CompileAndRun(source As String, expectedOutput As String, Optional options As VisualBasicCompilationOptions = Nothing) As CompilationVerifier
            Dim compilation = CreateCompilation(source, options:=If(options, TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On)), parseOptions:=VbNext)
            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput).VerifyDiagnostics()
        End Function

        Private Const CompletesAndThrows As String = "
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Log(message As String) As Task
        Await Task.Yield()
        Console.Write(message & "" "")
    End Function

    Async Function Run(fail As Boolean) As Task
        Try
            Console.Write(""try "")
            If fail Then Throw New InvalidOperationException(""boom"")
        Finally
            Await Log(""finally"")
        End Try
        Console.Write(""after "")
    End Function

    Sub Main()
        Run(False).GetAwaiter().GetResult()
        Try
            Run(True).GetAwaiter().GetResult()
        Catch ex As InvalidOperationException
            Console.Write(ex.Message)
        End Try
    End Sub
End Module
"

        <Fact>
        Public Sub AwaitInFinally_RunsWhenTheTryCompletesOrThrows()
            CompileAndRun(CompletesAndThrows, "try finally after try finally boom")
        End Sub

        <Fact>
        Public Sub AwaitInFinally_InDebugBuild()
            CompileAndRun(CompletesAndThrows, "try finally after try finally boom", TestOptions.DebugExe)
        End Sub

        <Fact>
        Public Sub AwaitInFinally_RethrowKeepsTheStackTrace()
            CompileAndRun("
Imports System
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks

Module Program
    <MethodImpl(MethodImplOptions.NoInlining)>
    Sub Fail()
        Throw New InvalidOperationException(""boom"")
    End Sub

    Async Function Run() As Task
        Try
            Fail()
        Finally
            Await Task.Yield()
        End Try
    End Function

    Sub Main()
        Try
            Run().GetAwaiter().GetResult()
        Catch ex As InvalidOperationException
            Console.Write(ex.StackTrace.Contains(""Fail""))
        End Try
    End Sub
End Module
", "True")
        End Sub

        <Fact>
        Public Sub AwaitInFinally_ReturnExitContinueAndGoTo()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Log(message As String) As Task
        Await Task.Yield()
        Console.Write(message & "" "")
    End Function

    Async Function Value(x As Integer) As Task(Of Integer)
        Try
            If x > 0 Then Return x * 10
            Console.Write(""no return "")
        Finally
            Await Log(""finally"")
        End Try
        Return -1
    End Function

    Async Function Loops() As Task
        For i = 1 To 3
            Try
                If i = 2 Then Continue For
                If i = 3 Then Exit For
                Console.Write(""body"" & i & "" "")
            Finally
                Await Log(""f"" & i)
            End Try
        Next

        Try
            Exit Try
        Finally
            Await Log(""exitTry"")
        End Try

        Try
            GoTo done
        Finally
            Await Log(""goto"")
        End Try
done:
        Console.Write(""done"")
    End Function

    Sub Main()
        Console.Write(Value(4).GetAwaiter().GetResult() & "" "")
        Console.Write(Value(0).GetAwaiter().GetResult() & "" "")
        Loops().GetAwaiter().GetResult()
    End Sub
End Module
", "finally 40 no return finally -1 body1 f1 f2 f3 exitTry goto done")
        End Sub

        <Fact>
        Public Sub AwaitInFinally_WithAwaitInCatch()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Log(message As String) As Task
        Await Task.Yield()
        Console.Write(message & "" "")
    End Function

    Async Function Recovers() As Task
        Try
            Throw New InvalidOperationException(""boom"")
        Catch ex As InvalidOperationException
            Await Log(""catch "" & ex.Message)
        Finally
            Await Log(""finally"")
        End Try
        Console.Write(""after "")
    End Function

    Async Function Rethrows() As Task
        Try
            Throw New InvalidOperationException(""again"")
        Catch ex As InvalidOperationException
            Await Log(""catch"")
            Throw
        Finally
            Await Log(""finally"")
        End Try
    End Function

    Sub Main()
        Recovers().GetAwaiter().GetResult()
        Try
            Rethrows().GetAwaiter().GetResult()
        Catch ex As InvalidOperationException
            Console.Write(ex.Message)
        End Try
    End Sub
End Module
", "catch boom finally after catch finally again")
        End Sub

        <Fact>
        Public Sub AwaitInFinally_NestedTryBlocks()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Log(message As String) As Task
        Await Task.Yield()
        Console.Write(message & "" "")
    End Function

    Async Function Run() As Task
        Try
            Try
                Throw New InvalidOperationException(""boom"")
            Finally
                Await Log(""inner"")
            End Try
        Finally
            Await Log(""outer"")
        End Try
    End Function

    Sub Main()
        Try
            Run().GetAwaiter().GetResult()
        Catch ex As InvalidOperationException
            Console.Write(ex.Message)
        End Try
    End Sub
End Module
", "inner outer boom")
        End Sub

        <Fact>
        Public Sub AwaitInFinally_InAnAsyncLambda()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Dim f As Func(Of Task(Of Integer)) =
            Async Function()
                Dim x = 1
                Try
                    x = 2
                    Return x
                Finally
                    Await Task.Yield()
                    x = 3
                    Console.Write(""lambda "")
                End Try
            End Function

        Console.Write(f().GetAwaiter().GetResult())
    End Sub
End Module
", "lambda 2")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AwaitInFinally_InAnAsyncIterator()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Async Iterator Function Numbers() As IAsyncEnumerable(Of Integer)
        Try
            Yield 1
            Yield 2
        Finally
            Await Task.Yield()
            Console.Write(""cleanup "")
        End Try
    End Function

    Async Function Run() As Task
        Await Each n In Numbers()
            Console.Write(n & "" "")
        Next
        Await Each n In Numbers()
            Console.Write(n & "" "")
            Exit For
        Next
        Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1 2 cleanup 1 cleanup after", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AwaitInFinally_SyncLockStillReportsAnError()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Module Program
    Async Function Run() As Task
        SyncLock New Object()
            Await Task.Yield()
        End SyncLock
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36943: 'Await' cannot be used inside a 'Catch' statement, a 'Finally' statement, or a 'SyncLock' statement.
            Await Task.Yield()
            ~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AwaitInFinally_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Module Program
    Async Function Run() As Task
        Try
        Finally
            Await Task.Yield()
        End Try
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36943: 'Await' cannot be used inside a 'Catch' statement, a 'Finally' statement, or a 'SyncLock' statement.
            Await Task.Yield()
            ~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
