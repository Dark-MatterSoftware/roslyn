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

    Async Function InSyncLock() As System.Threading.Tasks.Task
        SyncLock New Object()
            Await Each n In New Numbers(1)
            Next
        End SyncLock
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

        <Fact>
        Public Sub AwaitEach_BindsByPatternWithTaskReturningMembers()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Class Countdown
    Private ReadOnly _start As Integer
    Sub New(start As Integer)
        _start = start
    End Sub
    Function GetAsyncEnumerator() As Enumerator
        Return New Enumerator(_start)
    End Function

    Class Enumerator
        Private _value As Integer
        Sub New(start As Integer)
            _value = start + 1
        End Sub
        ReadOnly Property Current As Integer
            Get
                Return _value
            End Get
        End Property
        Async Function MoveNextAsync() As Task(Of Boolean)
            Await Task.Yield()
            _value -= 1
            Return _value > 0
        End Function
        Function DisposeAsync() As Task
            System.Console.Write(""disposed "")
            Return Task.CompletedTask
        End Function
    End Class
End Class

Class NoRelease
    Function GetAsyncEnumerator() As Enumerator
        Return New Enumerator()
    End Function

    Structure Enumerator
        Private _done As Boolean
        ReadOnly Property Current As String
            Get
                Return ""once""
            End Get
        End Property
        Function MoveNextAsync() As Task(Of Boolean)
            Dim result = Not _done
            _done = True
            Return Task.FromResult(result)
        End Function
    End Structure
End Class

Module Program
    Async Function Run() As Task
        Await Each n As Integer In New Countdown(3)
            System.Console.Write(n & "" "")
            If n = 2 Then Exit For
        Next
        Await Each s As String In New NoRelease()
            System.Console.Write(s)
        Next
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3 2 disposed once").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AwaitEach_PatternWithTheWrongMoveNextAsyncIsAnError()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Class Wrong
    Function GetAsyncEnumerator() As Wrong
        Return Me
    End Function
    ReadOnly Property Current As Integer
        Get
            Return 0
        End Get
    End Property
    Function MoveNextAsync() As Task(Of Integer)
        Return Task.FromResult(1)
    End Function
End Class

Module Program
    Async Function Run() As Task
        Await Each n In New Wrong()
        Next
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37392: 'Await Each' requires a collection that implements 'System.Collections.Generic.IAsyncEnumerable(Of T)', but 'Wrong' does not.
        Await Each n In New Wrong()
                        ~~~~~~~~~~~
]]></expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AwaitEach_WithCancellationAndConfigureAwait()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports System.Threading.Tasks

Module Program
    Async Iterator Function Numbers(<EnumeratorCancellation> Optional token As CancellationToken = Nothing) As IAsyncEnumerable(Of Integer)
        Try
            For i As Integer = 1 To 5
                token.ThrowIfCancellationRequested()
                Await Task.Yield()
                Yield i
            Next
        Finally
            Console.Write(""finally "")
        End Try
    End Function

    Async Function Run() As Task
        Dim cts As New CancellationTokenSource()
        Try
            Await Each n As Integer In Numbers().WithCancellation(cts.Token)
                Console.Write(n & "" "")
                If n = 2 Then cts.Cancel()
            Next
        Catch ex As OperationCanceledException
            Console.Write(""cancelled "")
        End Try

        Await Each n As Integer In Numbers().ConfigureAwait(False)
            Console.Write(n & "" "")
            If n = 2 Then Exit For
        Next
        Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1 2 finally cancelled 1 2 finally after", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AwaitEach_WithCancellationEmits()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Imports System.Threading
Imports System.Threading.Tasks

Module Program
    Async Function Sum(source As IAsyncEnumerable(Of Integer), token As CancellationToken) As Task(Of Integer)
        Dim total As Integer = 0
        Await Each n As Integer In source.WithCancellation(token).ConfigureAwait(False)
            total += n
        Next
        Return total
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        Private Const Items As String = "
Public Class Items(Of T)
    Implements System.Collections.Generic.IAsyncEnumerable(Of T)

    Private ReadOnly _values As T()

    Public Sub New(ParamArray values As T())
        _values = values
    End Sub

    Public Function GetAsyncEnumerator(Optional cancellationToken As System.Threading.CancellationToken = Nothing) As System.Collections.Generic.IAsyncEnumerator(Of T) Implements System.Collections.Generic.IAsyncEnumerable(Of T).GetAsyncEnumerator
        Return New Enumerator(_values)
    End Function

    Private Class Enumerator
        Implements System.Collections.Generic.IAsyncEnumerator(Of T)

        Private ReadOnly _values As T()
        Private _index As Integer = -1

        Public Sub New(values As T())
            _values = values
        End Sub

        Public ReadOnly Property Current As T Implements System.Collections.Generic.IAsyncEnumerator(Of T).Current
            Get
                Return _values(_index)
            End Get
        End Property

        Public Function MoveNextAsync() As System.Threading.Tasks.ValueTask(Of Boolean) Implements System.Collections.Generic.IAsyncEnumerator(Of T).MoveNextAsync
            Return New System.Threading.Tasks.ValueTask(Of Boolean)(Advance())
        End Function

        Private Async Function Advance() As System.Threading.Tasks.Task(Of Boolean)
            Await System.Threading.Tasks.Task.Yield()
            _index += 1
            Return _index < _values.Length
        End Function

        Public Function DisposeAsync() As System.Threading.Tasks.ValueTask Implements System.IAsyncDisposable.DisposeAsync
            Return New System.Threading.Tasks.ValueTask(System.Threading.Tasks.Task.FromResult(0))
        End Function
    End Class
End Class

Public Class Point
    Private ReadOnly _x As Integer
    Private ReadOnly _y As Integer

    Public Sub New(x As Integer, y As Integer)
        _x = x
        _y = y
    End Sub

    Public Sub Deconstruct(ByRef x As Integer, ByRef y As Integer)
        x = _x
        y = _y
    End Sub
End Class
"

        Private Const DeconstructionProgram As String = "
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Await Each (name, count) In New Items(Of (String, Integer))((""a"", 1), (""b"", 2))
            Await System.Threading.Tasks.Task.Yield()
            System.Console.Write(name & count.ToString() & "" "")
        Next

        Await Each (x, y As Long) In New Items(Of Point)(New Point(3, 4), New Point(5, 6))
            Await System.Threading.Tasks.Task.Yield()
            System.Console.Write((x + y).ToString() & "" "")
        Next

        Dim last As String = Nothing
        Dim total As Integer = 0
        Await Each (last, total) In New Items(Of (String, Integer))((""c"", 7), (""d"", 8))
        Next
        System.Console.Write(last & total.ToString() & "" "")

        Dim actions As New System.Collections.Generic.List(Of System.Func(Of String))
        Await Each (letter, digit) In New Items(Of (String, Integer))((""e"", 1), (""f"", 2))
            actions.Add(Function() letter & digit.ToString())
        Next
        For Each action In actions
            System.Console.Write(action() & "" "")
        Next
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
"

        <Fact>
        Public Sub AwaitEach_Deconstruction()
            CompileAndRun(Items & DeconstructionProgram, "a1 b2 7 11 d8 e1 f2 ",
                          TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True))
        End Sub

        <Fact>
        Public Sub AwaitEach_DeconstructionInDebugBuild()
            CompileAndRun(Items & DeconstructionProgram, "a1 b2 7 11 d8 e1 f2 ",
                          TestOptions.DebugExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True))
        End Sub

        <Fact>
        Public Sub AwaitEach_DeconstructionErrors()
            Dim compilation = CreateCompilation(AsyncStreamTypes & Items & "
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Await Each (a, b) In New Items(Of Integer)(1, 2)
        Next
    End Function

    Sub NotAsync()
        Await Each (c, d) In New Items(Of (Integer, Integer))((1, 2))
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37363: Deconstruction requires a tuple with 2 elements.
        Await Each (a, b) In New Items(Of Integer)(1, 2)
                   ~~~~~~
BC37391: 'Await Each' can only be used within an Async method. Consider marking this method with the 'Async' modifier and changing its return type to 'Task'.
        Await Each (c, d) In New Items(Of (Integer, Integer))((1, 2))
        ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AwaitEach_DeconstructionSemanticModel()
            Dim compilation = CreateCompilation(AsyncStreamTypes & Items & "
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Await Each (name, count) In New Items(Of (String, Integer))((""a"", 1))
            System.Console.Write(name & count.ToString())
        Next
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim forEachLoop = tree.GetRoot().DescendantNodes().OfType(Of Syntax.ForEachBlockSyntax)().Single()
            Dim usages = forEachLoop.DescendantNodes().OfType(Of Syntax.IdentifierNameSyntax)().Where(Function(n) n.Identifier.ValueText = "name" OrElse n.Identifier.ValueText = "count").ToArray()
            Assert.Equal(2, usages.Length)
            Assert.Equal("System.String", DirectCast(model.GetSymbolInfo(usages(0)).Symbol, ILocalSymbol).Type.ToTestDisplayString())
            Assert.Equal("System.Int32", DirectCast(model.GetSymbolInfo(usages(1)).Symbol, ILocalSymbol).Type.ToTestDisplayString())
            Assert.True(DirectCast(model.GetOperation(forEachLoop), Operations.IForEachLoopOperation).IsAsynchronous)
        End Sub
    End Class
End Namespace
