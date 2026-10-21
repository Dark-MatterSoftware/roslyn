' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class AsyncUsingTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        ' The test runtime is .NET Framework, which has neither type, so the tests declare them.
        Private Const AsyncDisposableTypes As String = "
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
End Namespace

Public Class Resource
    Implements System.IAsyncDisposable

    Private ReadOnly _name As String

    Public Sub New(name As String)
        _name = name
        System.Console.Write(""open "" & name & "" "")
    End Sub

    Public Function DisposeAsync() As System.Threading.Tasks.ValueTask Implements System.IAsyncDisposable.DisposeAsync
        Return New System.Threading.Tasks.ValueTask(Release())
    End Function

    Private Async Function Release() As System.Threading.Tasks.Task
        Await System.Threading.Tasks.Task.Yield()
        System.Console.Write(""release "" & _name & "" "")
    End Function
End Class
"

        Private Function CompileAndRun(source As String, expectedOutput As String) As CompilationVerifier
            Dim compilation = CreateCompilation(AsyncDisposableTypes & source, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)
            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput).VerifyDiagnostics()
        End Function

        <Fact>
        Public Sub AsyncUsing_ReleasesAtTheEndOfTheBlock()
            CompileAndRun("
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Async Using r As New Resource(""a"")
            System.Console.Write(""body "")
            Await System.Threading.Tasks.Task.Yield()
            System.Console.Write(""more "")
        End Using
        System.Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "open a body more release a after")
        End Sub

        <Fact>
        Public Sub AsyncUsing_DeclarationReleasesAtTheEndOfTheEnclosingBlock()
            CompileAndRun("
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        If True Then
            Async Using r As New Resource(""a"")
            System.Console.Write(""body "")
        End If
        System.Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "open a body release a after")
        End Sub

        <Fact>
        Public Sub AsyncUsing_ReleasesWhenTheBodyThrows()
            CompileAndRun("
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Async Using r As New Resource(""a"")
            System.Console.Write(""body "")
            Throw New System.InvalidOperationException(""boom"")
        End Using
        System.Console.Write(""not reached"")
    End Function

    Sub Main()
        Try
            Run().GetAwaiter().GetResult()
        Catch ex As System.InvalidOperationException
            System.Console.Write(""caught "" & ex.Message & "" "" & ex.StackTrace.Contains(""Run""))
        End Try
    End Sub
End Module
", "open a body release a caught boom True")
        End Sub

        <Fact>
        Public Sub AsyncUsing_ReleasesOnReturnAndExit()
            CompileAndRun("
Module Program
    Async Function GetValue() As System.Threading.Tasks.Task(Of Integer)
        Async Using r As New Resource(""f"")
            Return 42
        End Using
    End Function

    Async Function Leave() As System.Threading.Tasks.Task
        Async Using r As New Resource(""s"")
            Exit Function
        End Using
        System.Console.Write(""not reached "")
    End Function

    Async Function Repeat() As System.Threading.Tasks.Task
        For i = 1 To 3
            Async Using r As New Resource(i.ToString())
                If i = 1 Then Continue For
                If i = 2 Then Exit For
            End Using
        Next
    End Function

    Sub Main()
        System.Console.Write(GetValue().GetAwaiter().GetResult() & "" "")
        Leave().GetAwaiter().GetResult()
        Repeat().GetAwaiter().GetResult()
        System.Console.Write(""done"")
    End Sub
End Module
", "open f release f 42 open s release s open 1 release 1 open 2 release 2 done")
        End Sub

        <Fact>
        Public Sub AsyncUsing_ReleasesResourcesInReverseOrderAndSkipsNothing()
            CompileAndRun("
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Async Using a As New Resource(""a""), b As Resource = Nothing, c As New Resource(""c"")
            System.Console.Write(""body "")
        End Using
        System.Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "open a open c body release c release a after")
        End Sub

        <Fact>
        Public Sub AsyncUsing_ReleasesAnExpressionResource()
            CompileAndRun("
Module Program
    Function Open() As Resource
        Return New Resource(""e"")
    End Function

    Async Function Run() As System.Threading.Tasks.Task
        Async Using Open()
            System.Console.Write(""body "")
        End Using
        System.Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", "open e body release e after")
        End Sub

        <Fact>
        Public Sub AsyncUsing_NestedReturnReleasesBothInnermostFirst()
            CompileAndRun("
Module Program
    Async Function GetValue() As System.Threading.Tasks.Task(Of String)
        Async Using outer As New Resource(""outer"")
            Async Using inner As New Resource(""inner"")
                Await System.Threading.Tasks.Task.Yield()
                Return ""value""
            End Using
        End Using
    End Function

    Sub Main()
        System.Console.Write(GetValue().GetAwaiter().GetResult())
    End Sub
End Module
", "open outer open inner release inner release outer value")
        End Sub

        <Fact>
        Public Sub AsyncUsing_InAnAsyncLambda()
            CompileAndRun("
Module Program
    Sub Main()
        Dim run As System.Func(Of System.Threading.Tasks.Task) =
            Async Function()
                Async Using r As New Resource(""l"")
                    System.Console.Write(""body "")
                End Using
            End Function
        run().GetAwaiter().GetResult()
        System.Console.Write(""done"")
    End Sub
End Module
", "open l body release l done")
        End Sub

        <Fact>
        Public Sub AsyncUsing_ReleasesAStreamFromDotNet9()
            Dim compilation = CreateCompilation("
Imports System.IO
Imports System.Threading.Tasks

Module Program
    Async Function Run() As Task
        Async Using stream As New MemoryStream()
            Await stream.WriteAsync(New Byte() {1, 2, 3}, 0, 3)
        End Using
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.FailsPEVerify).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AsyncUsing_Errors()
            Dim compilation = CreateCompilation(AsyncDisposableTypes & "
Class Plain
    Implements System.IDisposable

    Public Sub Dispose() Implements System.IDisposable.Dispose
    End Sub
End Class

Module Program
    Sub NotAsync()
        Async Using r As New Resource(""a"")
        End Using
    End Sub

    Async Function NotAsyncDisposable() As System.Threading.Tasks.Task
        Async Using p As New Plain()
        End Using
    End Function

    Async Function InFinally() As System.Threading.Tasks.Task
        Try
        Finally
            Async Using r As New Resource(""a"")
            End Using
        End Try
    End Function
End Module
", parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37389: 'Async Using' can only be used within an Async method. Consider marking this method with the 'Async' modifier and changing its return type to 'Task'.
        Async Using r As New Resource("a")
        ~~~~~
BC37390: 'Async Using' operand of type 'Plain' must implement 'System.IAsyncDisposable'.
        Async Using p As New Plain()
                    ~~~~~~~~~~~~~~~~
BC36943: 'Await' cannot be used inside a 'Catch' statement, a 'Finally' statement, or a 'SyncLock' statement.
            Async Using r As New Resource("a")
            ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AsyncUsing_RejectedOn17_13()
            Dim compilation = CreateCompilation(AsyncDisposableTypes & "
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Async Using r As New Resource(""a"")
        End Using
    End Function
End Module
", parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36716: Visual Basic 17.13 does not support 'Async Using'.
        Async Using r As New Resource("a")
        ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub AsyncUsing_IsAnAsynchronousUsingOperation()
            Dim compilation = CreateCompilation(AsyncDisposableTypes & "
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Async Using r As New Resource(""a"")
        End Using
        Using s As System.IDisposable = Nothing
        End Using
    End Function
End Module
", parseOptions:=VbNext)

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim blocks = tree.GetRoot().DescendantNodes().OfType(Of Syntax.UsingBlockSyntax)().ToArray()
            Assert.True(DirectCast(model.GetOperation(blocks(0)), Operations.IUsingOperation).IsAsynchronous)
            Assert.False(DirectCast(model.GetOperation(blocks(1)), Operations.IUsingOperation).IsAsynchronous)
            compilation.AssertNoDiagnostics()
        End Sub

        <Fact>
        Public Sub AsyncUsing_BindsDisposeAsyncByPattern()
            Dim compilation = CreateCompilation(AsyncDisposableTypes & "
Class Handle
    Private ReadOnly _name As String
    Sub New(name As String)
        _name = name
    End Sub
    Function DisposeAsync() As System.Threading.Tasks.Task
        System.Console.Write(""released "" & _name & "" "")
        Return System.Threading.Tasks.Task.CompletedTask
    End Function
End Class

Structure Token
    Function DisposeAsync() As System.Threading.Tasks.Task
        System.Console.Write(""token "")
        Return System.Threading.Tasks.Task.CompletedTask
    End Function
End Structure

Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Async Using h As New Handle(""a""), t As New Token()
            System.Console.Write(""body "")
        End Using
        Async Using New Handle(""b"")
        End Using
        Dim missing As Handle = Nothing
        Async Using missing
        End Using
        System.Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="body token released a released b after").VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub AsyncUsing_ConfigureAwait()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Threading.Tasks

Class Resource
    Implements IAsyncDisposable

    Public Async Function Release() As Task
        Await Task.Yield()
        Console.Write(""released "")
    End Function

    Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        Return New ValueTask(Release())
    End Function
End Class

Module Program
    Async Function Run() As Task
        Dim r As New Resource()
        Async Using r.ConfigureAwait(False)
            Console.Write(""body "")
        End Using
        Console.Write(""after"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="body released after", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AsyncUsing_ConfigureAwaitEmits()
            Dim compilation = CreateCompilation("
Imports System.IO
Imports System.Threading.Tasks

Module Program
    Async Function Copy(source As Stream, target As Stream) As Task
        Async Using source.ConfigureAwait(False)
            Await source.CopyToAsync(target).ConfigureAwait(False)
        End Using
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AsyncUsing_ParsesAsAUsingStatement()
            Dim tree = VisualBasicSyntaxTree.ParseText("
Module Program
    Async Function Run() As System.Threading.Tasks.Task
        Async Using r As New Resource(""a"")
        End Using
        Using s As New Resource(""b"")
        End Using
    End Function
End Module
", VbNext)

            Dim usings = tree.GetRoot().DescendantNodes().OfType(Of Syntax.UsingStatementSyntax)().ToArray()
            Assert.Equal(2, usings.Length)
            Assert.Equal(SyntaxKind.AsyncKeyword, usings(0).AsyncKeyword.Kind())
            Assert.Equal(SyntaxKind.None, usings(1).AsyncKeyword.Kind())
            Assert.Empty(tree.GetDiagnostics())
        End Sub
    End Class
End Namespace
