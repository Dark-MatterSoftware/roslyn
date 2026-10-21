' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class AwaitInCatchTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Function CompileAndRun(source As String, expectedOutput As String, Optional options As VisualBasicCompilationOptions = Nothing) As CompilationVerifier
            Dim compilation = CreateCompilation(source, options:=If(options, TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On)), parseOptions:=VbNext)
            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput).VerifyDiagnostics()
        End Function

        <Fact>
        Public Sub AwaitInCatch_KeepsTheExceptionAndErr()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks
Imports Microsoft.VisualBasic

Module Program
    Async Function Log(message As String) As Task
        Await Task.Yield()
        Console.Write(message & "" "")
    End Function

    Async Function Run() As Task
        Try
            Throw New InvalidOperationException(""boom"")
        Catch ex As InvalidOperationException
            Dim description = Err.Description
            Await Log(""caught"")
            Console.Write(ex.Message & "" "" & description & "" "")
        End Try
        Console.Write(""after "" & (Err.Number = 0).ToString())
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "caught boom boom after True")
        End Sub

        <Fact>
        Public Sub AwaitInCatch_FiltersAndSeveralHandlers()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Handle(e As Exception) As Task(Of String)
        Try
            Throw e
        Catch ex As ArgumentException When ex.ParamName = ""a""
            Await Task.Yield()
            Return ""a""
        Catch ex As ArgumentException
            Await Task.Yield()
            Return ""argument "" & ex.ParamName
        Catch
            Await Task.Delay(1)
            Return ""other""
        End Try
    End Function

    Sub Main()
        Console.Write(Handle(New ArgumentException(""x"", ""a"")).Result & "", "")
        Console.Write(Handle(New ArgumentException(""x"", ""b"")).Result & "", "")
        Console.Write(Handle(New InvalidOperationException()).Result)
    End Sub
End Module
", "a, argument b, other")
        End Sub

        <Fact>
        Public Sub AwaitInCatch_RethrowKeepsTheStackTrace()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Thrower()
        Throw New InvalidOperationException(""deep"")
    End Sub

    Async Function Run() As Task
        Try
            Thrower()
        Catch
            Await Task.Yield()
            Throw
        End Try
    End Function

    Sub Main()
        Try
            Run().GetAwaiter().GetResult()
        Catch ex As InvalidOperationException
            Console.Write(ex.Message & "" "" & ex.StackTrace.Contains(""Thrower"").ToString())
        End Try
    End Sub
End Module
", "deep True")
        End Sub

        <Fact>
        Public Sub AwaitInCatch_FinallyRunsAfterTheCatchBody()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Run(fail As Boolean) As Task
        Try
            Console.Write(""try "")
            If fail Then Throw New Exception()
        Catch
            Await Task.Yield()
            Console.Write(""catch "")
        Finally
            Console.Write(""finally "")
        End Try
        Console.Write(""| "")
    End Function

    Sub Main()
        Run(True).GetAwaiter().GetResult()
        Run(False).GetAwaiter().GetResult()
    End Sub
End Module
", "try catch finally | try finally | ")
        End Sub

        <Fact>
        Public Sub AwaitInCatch_GoToBackIntoTheTry()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Private _attempts As Integer

    Function Work() As Integer
        _attempts += 1
        If _attempts < 3 Then Throw New TimeoutException()
        Return _attempts
    End Function

    Async Function Run() As Task(Of Integer)
        Dim retryCount As Integer = 0
        Try
retry:
            Return Work()
        Catch ex As TimeoutException When retryCount < 5
            retryCount += 1
            Await Task.Delay(1)
            GoTo retry
        End Try
    End Function

    Sub Main()
        Console.Write(Run().Result.ToString() & "" "" & _attempts.ToString())
    End Sub
End Module
", "3 3")
        End Sub

        <Fact>
        Public Sub AwaitInCatch_ExitTryReturnAndNestedTry()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Run(mode As Integer) As Task(Of String)
        Try
            Throw New Exception(""outer"")
        Catch ex As Exception
            Await Task.Yield()
            If mode = 1 Then Exit Try
            If mode = 2 Then Return ""returned""
            Try
                Throw New Exception(""inner"")
            Catch inner As Exception
                Return ex.Message & ""+"" & inner.Message
            End Try
        End Try
        Return ""exited""
    End Function

    Sub Main()
        Console.Write(Run(1).Result & "" "" & Run(2).Result & "" "" & Run(3).Result)
    End Sub
End Module
", "exited returned outer+inner")
        End Sub

        <Fact>
        Public Sub AwaitInCatch_InAnAsyncLambda()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Dim f As Func(Of Task(Of String)) =
            Async Function()
                Try
                    Throw New Exception(""lambda"")
                Catch ex As Exception
                    Await Task.Yield()
                    Return ex.Message
                End Try
            End Function
        Console.Write(f().Result)
    End Sub
End Module
", "lambda")
        End Sub

        <Fact>
        Public Sub AwaitInCatch_InDebugBuild()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Run() As Task
        Try
            Throw New Exception(""debug"")
        Catch ex As Exception
            Await Task.Yield()
            Console.Write(ex.Message)
        End Try
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "debug", TestOptions.DebugExe)
        End Sub

        <Fact>
        Public Sub AwaitInCatch_FilterAndFinallyStillReportAnError()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Module Program
    Async Function Check() As Task(Of Boolean)
        Await Task.Yield()
        Return True
    End Function

    Async Function Run() As Task
        Try
        Catch ex As System.Exception When Await Check()
        Finally
            Await Task.Yield()
        End Try
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36943: 'Await' cannot be used inside a 'Catch' statement, a 'Finally' statement, or a 'SyncLock' statement.
        Catch ex As System.Exception When Await Check()
                                          ~~~~~~~~~~~~~
BC36943: 'Await' cannot be used inside a 'Catch' statement, a 'Finally' statement, or a 'SyncLock' statement.
            Await Task.Yield()
            ~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AwaitInCatch_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Module Program
    Async Function Run() As Task
        Try
        Catch
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
