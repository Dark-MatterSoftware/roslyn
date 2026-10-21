' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class AsyncIteratorLambdaTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Function CompileAndRun(body As String, expectedOutput As String, Optional options As VisualBasicCompilationOptions = Nothing) As CompilationVerifier
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
", targetFramework:=TargetFramework.Net90, options:=If(options, TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On)), parseOptions:=VbNext)

            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput, verify:=Verification.Skipped).VerifyDiagnostics()
        End Function

        Private Const Inferred As String = "
    Async Function Run() As Task
        Dim numbers = Async Iterator Function()
                          For i = 1 To 3
                              Await Task.Yield()
                              Yield i
                          Next
                      End Function

        Dim sequence As IAsyncEnumerable(Of Integer) = numbers()
        Dim total = 0
        Await Each n In sequence
            total += n
            Console.Write(n & "" "")
        Next
        Console.Write(""total "" & total)
    End Function
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIteratorLambda_InfersIAsyncEnumerable()
            CompileAndRun(Inferred, "1 2 3 total 6")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIteratorLambda_InDebugBuild()
            CompileAndRun(Inferred, "1 2 3 total 6", TestOptions.DebugExe)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIteratorLambda_TargetTypeCaptureAndFinally()
            CompileAndRun("
    Async Function Run() As Task
        Dim prefix = ""item""
        Dim items As Func(Of Integer, IAsyncEnumerable(Of String)) =
            Async Iterator Function(count As Integer)
                Try
                    For i = 1 To count
                        Await Task.Yield()
                        Yield prefix & i
                    Next
                Finally
                    Await Task.Yield()
                    Console.Write(""cleanup "")
                End Try
            End Function

        Await Each s In items(5)
            Console.Write(s & "" "")
            If s = ""item2"" Then Exit For
        Next
        Console.Write(""after"")
    End Function
", "item1 item2 cleanup after")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIteratorLambda_AsClauseEnumerator()
            CompileAndRun("
    Async Function Run() As Task
        Dim letters = Async Iterator Function() As IAsyncEnumerator(Of String)
                          Yield ""a""
                          Await Task.Delay(1)
                          Yield ""b""
                      End Function

        Dim e = letters()
        While Await e.MoveNextAsync()
            Console.Write(e.Current & "" "")
        End While
        Await e.DisposeAsync()
        Console.Write(""done"")
    End Function
", "a b done")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncIteratorLambda_TypeInferenceAndArguments()
            CompileAndRun("
    Async Function First(Of T)(make As Func(Of IAsyncEnumerable(Of T))) As Task(Of T)
        Await Each item In make()
            Return item
        Next
        Return Nothing
    End Function

    Async Function Count(source As IAsyncEnumerable(Of Object)) As Task(Of Integer)
        Dim n = 0
        Await Each item In source
            n += 1
        Next
        Return n
    End Function

    Async Function Run() As Task
        Dim value = Await First(Async Iterator Function()
                                    Await Task.Yield()
                                    Yield 42
                                End Function)
        Console.Write(value & "" "")

        Dim empty = Async Iterator Function()
                        Await Task.Yield()
                    End Function
        Console.Write(Await Count(empty()))
    End Function
", "42 0")
        End Sub

        <Fact>
        Public Sub AsyncIteratorLambda_Errors()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Dim toTask As Func(Of Task) = Async Iterator Function()
                                          Yield 1
                                      End Function

        Dim asTask = Async Iterator Function() As Task
                         Yield 1
                     End Function

        Dim asSub = Async Iterator Sub()
                        Await Task.Yield()
                    End Sub

        Dim returnsValue As Func(Of IAsyncEnumerable(Of Integer)) = Async Iterator Function()
                                                                         Return 1
                                                                     End Function
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37393: An 'Async Iterator' function must return 'IAsyncEnumerable(Of T)' or 'IAsyncEnumerator(Of T)'.
        Dim toTask As Func(Of Task) = Async Iterator Function()
                                      ~~~~~~~~~~~~~~~~~~~~~~~~~
BC37393: An 'Async Iterator' function must return 'IAsyncEnumerable(Of T)' or 'IAsyncEnumerator(Of T)'.
        Dim asTask = Async Iterator Function() As Task
                                                  ~~~~
BC37393: An 'Async Iterator' function must return 'IAsyncEnumerable(Of T)' or 'IAsyncEnumerator(Of T)'.
        Dim asSub = Async Iterator Sub()
                                   ~~~
BC36942: To return a value from an Iterator function, use 'Yield' rather than 'Return'.
                                                                         Return 1
                                                                         ~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AsyncIteratorLambda_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Dim numbers = Async Iterator Function()
                          Await Task.Yield()
                          Yield 1
                      End Function
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36936: 'Async' and 'Iterator' modifiers cannot be used together.
        Dim numbers = Async Iterator Function()
                      ~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
