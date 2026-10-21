' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ValueTaskReturnTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Function CompileAndRun(source As String, expectedOutput As String, Optional options As VisualBasicCompilationOptions = Nothing) As CompilationVerifier
            Dim compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, options:=If(options, TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On)), parseOptions:=VbNext)
            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput, verify:=Verification.Skipped).VerifyDiagnostics()
        End Function

        Private Const Basics As String = "
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Log(message As String) As ValueTask
        Await Task.Yield()
        Console.Write(message & "" "")
    End Function

    Async Function Twice(x As Integer) As ValueTask(Of Integer)
        Await Task.Yield()
        Return x * 2
    End Function

    Async Function Quick() As ValueTask(Of String)
        Await Task.CompletedTask
        Return ""quick""
    End Function

    Async Function Fail() As ValueTask(Of Integer)
        Await Task.Yield()
        Throw New InvalidOperationException(""boom"")
    End Function

    Async Function Run() As Task
        Await Log(""log"")
        Console.Write((Await Twice(21)) & "" "")
        Dim pending = Quick()
        Console.Write(pending.IsCompletedSuccessfully & "" "" & Await pending & "" "")
        Try
            Await Fail()
        Catch ex As InvalidOperationException
            Console.Write(ex.Message)
        End Try
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ValueTask_ReturnsWithAndWithoutAValue()
            CompileAndRun(Basics, "log 42 True quick boom")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ValueTask_InDebugBuild()
            CompileAndRun(Basics, "log 42 True quick boom", TestOptions.DebugExe)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ValueTask_GenericMethodAndLambdas()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Echo(Of T)(value As T) As ValueTask(Of T)
        Await Task.Yield()
        Return value
    End Function

    Async Function Run() As Task
        Console.Write((Await Echo(""echo"")) & "" "")

        Dim compute As Func(Of ValueTask(Of Integer)) =
            Async Function()
                Await Task.Yield()
                Return 5
            End Function
        Console.Write((Await compute()) & "" "")

        Dim act As Func(Of ValueTask) =
            Async Function()
                Await Task.Yield()
                Console.Write(""act "")
            End Function
        Await act()

        Dim typed = Async Function() As ValueTask(Of String)
                        Await Task.Yield()
                        Return ""typed""
                    End Function
        Console.Write(Await typed())
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "echo 5 act typed")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ValueTask_ImplementsDisposeAsync()
            CompileAndRun("
Imports System
Imports System.Threading.Tasks

Class Resource
    Implements IAsyncDisposable

    Public Async Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        Await Task.Yield()
        Console.Write(""disposed"")
    End Function
End Class

Module Program
    Async Function Run() As Task
        Async Using r As New Resource()
            Console.Write(""using "")
        End Using
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "using disposed")
        End Sub

        <Fact>
        Public Sub ValueTask_ReturnErrors()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Module Program
    Async Function NoValue() As ValueTask
        Return 1
    End Function

    Async Function Wrapped() As ValueTask(Of Integer)
        Await Task.Yield()
        Return Wrapped()
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36952: 'Return' statements in this Async method cannot return a value since the return type of the function is 'Task'. Consider changing the function's return type to 'Task(Of T)'.
        Return 1
        ~~~~~~~~
BC37055: Since this is an async method, the return expression must be of type 'Integer' rather than 'Task(Of Integer)'.
        Return Wrapped()
               ~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub ValueTask_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Run() As ValueTask
        Await Task.Yield()
    End Function

    Sub Main()
        Dim f As Func(Of ValueTask(Of Integer)) = Async Function()
                                                      Return 1
                                                  End Function
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36945: The 'Async' modifier can only be used on Subs, or on Functions that return Task or Task(Of T).
    Async Function Run() As ValueTask
                            ~~~~~~~~~
BC36945: The 'Async' modifier can only be used on Subs, or on Functions that return Task or Task(Of T).
        Dim f As Func(Of ValueTask(Of Integer)) = Async Function()
                                                  ~~~~~~~~~~~~~~~~
BC30311: Value of type 'Integer' cannot be converted to 'ValueTask(Of Integer)'.
                                                      Return 1
                                                             ~
]]></expected>)
        End Sub
    End Class
End Namespace
