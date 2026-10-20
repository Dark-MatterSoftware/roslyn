' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class AwaitEachTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        ' The test runtime is .NET Framework, which has none of these types, so the tests declare them.
        Private Const AsyncStreamTypes As String = "
Namespace System
    Public Interface IAsyncDisposable
        Function DisposeAsync() As System.Threading.Tasks.ValueTask
    End Interface
End Namespace

Namespace System.Threading.Tasks
    Public Structure ValueTask
        Private ReadOnly _task As Task

        Public Sub New(task As Task)
            _task = task
        End Sub

        Public Function GetAwaiter() As System.Runtime.CompilerServices.TaskAwaiter
            Return If(_task, Task.FromResult(0)).GetAwaiter()
        End Function
    End Structure

    Public Structure ValueTask(Of TResult)
        Private ReadOnly _task As Task(Of TResult)

        Public Sub New(task As Task(Of TResult))
            _task = task
        End Sub

        Public Function GetAwaiter() As System.Runtime.CompilerServices.TaskAwaiter(Of TResult)
            Return _task.GetAwaiter()
        End Function
    End Structure
End Namespace

Namespace System.Collections.Generic
    Public Interface IAsyncEnumerable(Of Out T)
        Function GetAsyncEnumerator(Optional cancellationToken As System.Threading.CancellationToken = Nothing) As IAsyncEnumerator(Of T)
    End Interface

    Public Interface IAsyncEnumerator(Of Out T)
        Inherits System.IAsyncDisposable

        ReadOnly Property Current As T
        Function MoveNextAsync() As System.Threading.Tasks.ValueTask(Of Boolean)
    End Interface
End Namespace

Public Class Numbers
    Implements System.Collections.Generic.IAsyncEnumerable(Of Integer)

    Private ReadOnly _count As Integer

    Public Sub New(count As Integer)
        _count = count
    End Sub

    Public Function GetAsyncEnumerator(Optional cancellationToken As System.Threading.CancellationToken = Nothing) As System.Collections.Generic.IAsyncEnumerator(Of Integer) Implements System.Collections.Generic.IAsyncEnumerable(Of Integer).GetAsyncEnumerator
        Return New Enumerator(_count)
    End Function

    Private Class Enumerator
        Implements System.Collections.Generic.IAsyncEnumerator(Of Integer)

        Private ReadOnly _count As Integer
        Private _current As Integer

        Public Sub New(count As Integer)
            _count = count
        End Sub

        Public ReadOnly Property Current As Integer Implements System.Collections.Generic.IAsyncEnumerator(Of Integer).Current
            Get
                Return _current
            End Get
        End Property

        Public Function MoveNextAsync() As System.Threading.Tasks.ValueTask(Of Boolean) Implements System.Collections.Generic.IAsyncEnumerator(Of Integer).MoveNextAsync
            Return New System.Threading.Tasks.ValueTask(Of Boolean)(Advance())
        End Function

        Private Async Function Advance() As System.Threading.Tasks.Task(Of Boolean)
            Await System.Threading.Tasks.Task.Yield()
            _current += 1
            Return _current <= _count
        End Function

        Public Function DisposeAsync() As System.Threading.Tasks.ValueTask Implements System.IAsyncDisposable.DisposeAsync
            Return New System.Threading.Tasks.ValueTask(Release())
        End Function

        Private Async Function Release() As System.Threading.Tasks.Task
            Await System.Threading.Tasks.Task.Yield()
            System.Console.Write(""released "")
        End Function
    End Class
End Class
"

        Private Function CompileAndRun(source As String, expectedOutput As String, Optional options As VisualBasicCompilationOptions = Nothing) As CompilationVerifier
            Dim compilation = CreateCompilation(AsyncStreamTypes & source, options:=If(options, TestOptions.ReleaseExe), parseOptions:=VbNext)
            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput).VerifyDiagnostics()
        End Function

        <Fact>
        Public Sub AwaitEach_IteratesAndReleasesTheEnumerator()
            CompileAndRun("
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Await Each n In New Numbers(3)
            System.Console.Write(n & "" "")
        Next
        System.Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "1 2 3 released after")
        End Sub

        <Fact>
        Public Sub AwaitEach_ExitForAndContinueFor()
            CompileAndRun("
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Await Each n In New Numbers(5)
            If n = 2 Then Continue For
            If n = 4 Then Exit For
            System.Console.Write(n & "" "")
        Next
        System.Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "1 3 released after")
        End Sub

        <Fact>
        Public Sub AwaitEach_ReleasesWhenTheBodyThrowsOrReturns()
            CompileAndRun("
Module Program
    Async Function Fail() As System.Threading.Tasks.Task
        Await Each n In New Numbers(3)
            If n = 2 Then Throw New System.InvalidOperationException(""boom"")
        Next
    End Function

    Async Function Find() As System.Threading.Tasks.Task(Of Integer)
        Await Each n In New Numbers(5)
            If n * n > 5 Then Return n
        Next
        Return -1
    End Function

    Sub Main()
        Try
            Fail().GetAwaiter().GetResult()
        Catch ex As System.InvalidOperationException
            System.Console.Write(""caught "" & ex.Message & "" "")
        End Try
        System.Console.Write(Find().GetAwaiter().GetResult())
    End Sub
End Module
", "released caught boom released 3")
        End Sub

        <Fact>
        Public Sub AwaitEach_WithIndexUnderOptionStrict()
            CompileAndRun("
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Await Each n As Integer With i In New Numbers(3)
            System.Console.Write(i & "":"" & n & "" "")
        Next
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "0:1 1:2 2:3 released ", TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(False).WithOptionExplicit(True))
        End Sub

        <Fact>
        Public Sub AwaitEach_Errors()
            Dim compilation = CreateCompilation(AsyncStreamTypes & "
Module Program
    Sub NotAsync()
        Await Each n In New Numbers(1)
        Next
    End Sub

    Async Function NotAsyncEnumerable() As System.Threading.Tasks.Task
        Await Each n In New Integer() {1, 2}
        Next
    End Function

    Async Function InFinally() As System.Threading.Tasks.Task
        Try
        Finally
            Await Each n In New Numbers(1)
            Next
        End Try
    End Function
End Module
", parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37391: 'Await Each' can only be used within an Async method. Consider marking this method with the 'Async' modifier and changing its return type to 'Task'.
        Await Each n In New Numbers(1)
        ~~~~~
BC37392: 'Await Each' requires a collection that implements 'System.Collections.Generic.IAsyncEnumerable(Of T)', but 'Integer()' does not.
        Await Each n In New Integer() {1, 2}
                        ~~~~~~~~~~~~~~~~~~~~
BC36943: 'Await' cannot be used inside a 'Catch' statement, a 'Finally' statement, or a 'SyncLock' statement.
            Await Each n In New Numbers(1)
            ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AwaitEach_RejectedOn17_13()
            Dim compilation = CreateCompilation(AsyncStreamTypes & "
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Await Each n In New Numbers(1)
        Next
    End Function
End Module
", parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36716: Visual Basic 17.13 does not support 'Await Each'.
        Await Each n In New Numbers(1)
        ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AwaitEach_IsAnAsynchronousForEachLoop()
            Dim compilation = CreateCompilation(AsyncStreamTypes & "
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Await Each n In New Numbers(1)
        Next
        For Each m In New Integer() {1}
        Next
    End Function
End Module
", parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim loops = tree.GetRoot().DescendantNodes().OfType(Of Syntax.ForEachBlockSyntax)().ToArray()
            Assert.Equal(SyntaxKind.AwaitKeyword, loops(0).ForEachStatement.ForKeyword.Kind())
            Assert.True(DirectCast(model.GetOperation(loops(0)), Operations.IForEachLoopOperation).IsAsynchronous)
            Assert.False(DirectCast(model.GetOperation(loops(1)), Operations.IForEachLoopOperation).IsAsynchronous)
        End Sub

        <Fact>
        Public Sub AwaitEach_IteratesAnAsyncEnumerableFromDotNet9()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Imports System.Threading
Imports System.Threading.Tasks

Class Empty
    Implements IAsyncEnumerable(Of String), IAsyncEnumerator(Of String)

    Public Function GetAsyncEnumerator(Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerator(Of String) Implements IAsyncEnumerable(Of String).GetAsyncEnumerator
        Return Me
    End Function

    Public ReadOnly Property Current As String Implements IAsyncEnumerator(Of String).Current
        Get
            Return Nothing
        End Get
    End Property

    Public Function MoveNextAsync() As ValueTask(Of Boolean) Implements IAsyncEnumerator(Of String).MoveNextAsync
        Return New ValueTask(Of Boolean)(False)
    End Function

    Public Function DisposeAsync() As ValueTask Implements System.IAsyncDisposable.DisposeAsync
        Return Nothing
    End Function
End Class

Module Program
    Async Function Run() As Task
        Await Each s In New Empty()
            System.Console.WriteLine(s)
        Next
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.FailsPEVerify).VerifyDiagnostics()
        End Sub
    End Class
End Namespace
