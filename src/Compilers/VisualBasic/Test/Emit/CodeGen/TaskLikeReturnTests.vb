' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class TaskLikeReturnTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const AttributeSource As String = "
Namespace System.Runtime.CompilerServices
    <AttributeUsage(AttributeTargets.All, AllowMultiple:=False, Inherited:=False)>
    Public NotInheritable Class AsyncMethodBuilderAttribute
        Inherits Attribute

        Public Sub New(builderType As Type)
            Me.BuilderType = builderType
        End Sub

        Public ReadOnly Property BuilderType As Type
    End Class
End Namespace
"

        Private Const TaskLikeSource As String = "
Imports System
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks

<AsyncMethodBuilder(GetType(MyTaskBuilder))>
Public Class MyTask
    Private ReadOnly _inner As Task

    Public Sub New(inner As Task)
        _inner = inner
    End Sub

    Public Function GetAwaiter() As TaskAwaiter
        Return _inner.GetAwaiter()
    End Function
End Class

Public Class MyTaskBuilder
    Private _builder As AsyncTaskMethodBuilder

    Public Shared Function Create() As MyTaskBuilder
        Console.Write(""create "")
        Dim result As New MyTaskBuilder()
        result._builder = AsyncTaskMethodBuilder.Create()
        Return result
    End Function

    Public Sub Start(Of TStateMachine As IAsyncStateMachine)(ByRef stateMachine As TStateMachine)
        _builder.Start(stateMachine)
    End Sub

    Public Sub SetStateMachine(stateMachine As IAsyncStateMachine)
    End Sub

    Public Sub SetResult()
        _builder.SetResult()
    End Sub

    Public Sub SetException(exception As Exception)
        _builder.SetException(exception)
    End Sub

    Public Sub AwaitOnCompleted(Of TAwaiter As INotifyCompletion, TStateMachine As IAsyncStateMachine)(ByRef awaiter As TAwaiter, ByRef stateMachine As TStateMachine)
        _builder.AwaitOnCompleted(awaiter, stateMachine)
    End Sub

    Public Sub AwaitUnsafeOnCompleted(Of TAwaiter As ICriticalNotifyCompletion, TStateMachine As IAsyncStateMachine)(ByRef awaiter As TAwaiter, ByRef stateMachine As TStateMachine)
        _builder.AwaitUnsafeOnCompleted(awaiter, stateMachine)
    End Sub

    Public ReadOnly Property Task As MyTask
        Get
            Return New MyTask(_builder.Task)
        End Get
    End Property
End Class

<AsyncMethodBuilder(GetType(MyTaskBuilder(Of )))>
Public Class MyTask(Of T)
    Private ReadOnly _inner As Task(Of T)

    Public Sub New(inner As Task(Of T))
        _inner = inner
    End Sub

    Public Function GetAwaiter() As TaskAwaiter(Of T)
        Return _inner.GetAwaiter()
    End Function
End Class

Public Class MyTaskBuilder(Of T)
    Private _builder As AsyncTaskMethodBuilder(Of T)

    Public Shared Function Create() As MyTaskBuilder(Of T)
        Console.Write(""create(Of T) "")
        Dim result As New MyTaskBuilder(Of T)()
        result._builder = AsyncTaskMethodBuilder(Of T).Create()
        Return result
    End Function

    Public Sub Start(Of TStateMachine As IAsyncStateMachine)(ByRef stateMachine As TStateMachine)
        _builder.Start(stateMachine)
    End Sub

    Public Sub SetStateMachine(stateMachine As IAsyncStateMachine)
    End Sub

    Public Sub SetResult(result As T)
        _builder.SetResult(result)
    End Sub

    Public Sub SetException(exception As Exception)
        _builder.SetException(exception)
    End Sub

    Public Sub AwaitOnCompleted(Of TAwaiter As INotifyCompletion, TStateMachine As IAsyncStateMachine)(ByRef awaiter As TAwaiter, ByRef stateMachine As TStateMachine)
        _builder.AwaitOnCompleted(awaiter, stateMachine)
    End Sub

    Public Sub AwaitUnsafeOnCompleted(Of TAwaiter As ICriticalNotifyCompletion, TStateMachine As IAsyncStateMachine)(ByRef awaiter As TAwaiter, ByRef stateMachine As TStateMachine)
        _builder.AwaitUnsafeOnCompleted(awaiter, stateMachine)
    End Sub

    Public ReadOnly Property Task As MyTask(Of T)
        Get
            Return New MyTask(Of T)(_builder.Task)
        End Get
    End Property
End Class
"

        Private Function CreateTaskLikeCompilation(source As String, Optional options As VisualBasicCompilationOptions = Nothing, Optional parseOptions As VisualBasicParseOptions = Nothing) As VisualBasicCompilation
            Return CreateCompilation({source, TaskLikeSource, AttributeSource},
                                     options:=If(options, TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On)),
                                     parseOptions:=If(parseOptions, VbNext))
        End Function

        <Fact>
        Public Sub TaskLike_NonGenericAndGeneric()
            Dim compilation = CreateTaskLikeCompilation("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Log(message As String) As MyTask
        Await Task.Yield()
        Console.Write(message & "" "")
    End Function

    Async Function Twice(x As Integer) As MyTask(Of Integer)
        Await Task.Yield()
        Return x * 2
    End Function

    Async Function Fail() As MyTask(Of String)
        Await Task.Yield()
        Throw New InvalidOperationException(""boom"")
    End Function

    Async Function Run() As Task
        Await Log(""log"")
        Console.Write((Await Twice(21)) & "" "")
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
")
            CompileAndVerify(compilation, expectedOutput:="create log create(Of T) 42 create(Of T) boom").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TaskLike_LambdasAndDebugBuild()
            Dim compilation = CreateTaskLikeCompilation("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Run() As Task
        Dim compute As Func(Of MyTask(Of Integer)) =
            Async Function()
                Await Task.Yield()
                Return 3
            End Function
        Console.Write((Await compute()) & "" "")

        Dim act As Func(Of MyTask) =
            Async Function()
                Await Task.Yield()
                Console.Write(""act"")
            End Function
        Await act()
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", TestOptions.DebugExe)
            CompileAndVerify(compilation, expectedOutput:="create(Of T) 3 create act").VerifyDiagnostics()
        End Sub

        Private Const MethodBuilderSource As String = "
Imports System
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks

Public Class LoggingBuilder
    Private _builder As AsyncTaskMethodBuilder

    Public Shared Function Create() As LoggingBuilder
        Console.Write(""logging "")
        Dim result As New LoggingBuilder()
        result._builder = AsyncTaskMethodBuilder.Create()
        Return result
    End Function

    Public Sub Start(Of TStateMachine As IAsyncStateMachine)(ByRef stateMachine As TStateMachine)
        _builder.Start(stateMachine)
    End Sub

    Public Sub SetStateMachine(stateMachine As IAsyncStateMachine)
    End Sub

    Public Sub SetResult()
        _builder.SetResult()
    End Sub

    Public Sub SetException(exception As Exception)
        _builder.SetException(exception)
    End Sub

    Public Sub AwaitOnCompleted(Of TAwaiter As INotifyCompletion, TStateMachine As IAsyncStateMachine)(ByRef awaiter As TAwaiter, ByRef stateMachine As TStateMachine)
        _builder.AwaitOnCompleted(awaiter, stateMachine)
    End Sub

    Public Sub AwaitUnsafeOnCompleted(Of TAwaiter As ICriticalNotifyCompletion, TStateMachine As IAsyncStateMachine)(ByRef awaiter As TAwaiter, ByRef stateMachine As TStateMachine)
        _builder.AwaitUnsafeOnCompleted(awaiter, stateMachine)
    End Sub

    Public ReadOnly Property Task As Task
        Get
            Return _builder.Task
        End Get
    End Property
End Class

Module Program
    <AsyncMethodBuilder(GetType(LoggingBuilder))>
    Async Function Run() As Task
        Await Task.Yield()
        Console.Write(""run"")
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
"

        <Fact>
        Public Sub TaskLike_BuilderOnTheMethod()
            Dim compilation = CreateCompilation({MethodBuilderSource, AttributeSource}, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="logging run").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TaskLike_BuilderOnTheMethodIgnoredOn17_13()
            Dim compilation = CreateCompilation({MethodBuilderSource, AttributeSource}, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=Vb17_13)
            CompileAndVerify(compilation, expectedOutput:="run").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TaskLike_ReturnErrorsNameTheReturnType()
            Dim compilation = CreateTaskLikeCompilation("
Imports System.Threading.Tasks

Module Program
    Async Function NoValue() As MyTask
        Await Task.Yield()
        Return 1
    End Function

    Async Function Wrapped() As MyTask(Of Integer)
        Await Task.Yield()
        Return Wrapped()
    End Function

    Async Function NotTaskLike() As String
        Await Task.Yield()
        Return """"
    End Function

    Sub Main()
    End Sub
End Module
")
            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37400: 'Return' statements in this Async method cannot return a value since the return type of the function is 'MyTask'.
        Return 1
        ~~~~~~~~
BC37401: Since this is an async method, the return expression must be of type 'Integer' rather than 'MyTask(Of Integer)'.
        Return Wrapped()
               ~~~~~~~~~
BC37399: The 'Async' modifier can only be used on Subs, or on Functions that return Task, Task(Of T), or a type that names its builder with 'AsyncMethodBuilder'.
    Async Function NotTaskLike() As String
                                    ~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub TaskLike_BadBuilders()
            Dim compilation = CreateCompilation({"
Imports System
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks

<AsyncMethodBuilder(GetType(NoCreate))>
Public Class NoCreateTask
End Class

Public Class NoCreate
    Public ReadOnly Property Task As NoCreateTask
        Get
            Return Nothing
        End Get
    End Property
End Class

<AsyncMethodBuilder(GetType(NotGeneric))>
Public Class GenericTask(Of T)
End Class

Public Class NotGeneric
End Class

<AsyncMethodBuilder(GetType(WrongTask))>
Public Class WrongTaskTask
End Class

Public Class WrongTask
    Public Shared Function Create() As WrongTask
        Return Nothing
    End Function

    Public ReadOnly Property Task As Task
        Get
            Return Nothing
        End Get
    End Property
End Class

Module Program
    Async Function A() As NoCreateTask
        Await Task.Yield()
    End Function

    Async Function B() As GenericTask(Of Integer)
        Await Task.Yield()
        Return 1
    End Function

    Async Function C() As WrongTaskTask
        Await Task.Yield()
    End Function

    Sub Main()
    End Sub
End Module
", AttributeSource}, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            compilation.AssertTheseEmitDiagnostics(<expected><![CDATA[
BC37402: 'NoCreate' cannot build the result of an Async function that returns 'NoCreateTask'. A builder needs a 'Public Shared Function Create()' that returns the builder and a 'Public' instance property 'Task' of the return type, and has one type parameter when the return type is generic.
    Async Function A() As NoCreateTask
                   ~
BC37402: 'NotGeneric' cannot build the result of an Async function that returns 'GenericTask(Of Integer)'. A builder needs a 'Public Shared Function Create()' that returns the builder and a 'Public' instance property 'Task' of the return type, and has one type parameter when the return type is generic.
    Async Function B() As GenericTask(Of Integer)
                   ~
BC37402: 'WrongTask' cannot build the result of an Async function that returns 'WrongTaskTask'. A builder needs a 'Public Shared Function Create()' that returns the builder and a 'Public' instance property 'Task' of the return type, and has one type parameter when the return type is generic.
    Async Function C() As WrongTaskTask
                   ~
]]></expected>)
        End Sub

        <Fact>
        Public Sub TaskLike_RejectedOn17_13()
            Dim compilation = CreateTaskLikeCompilation("
Imports System.Threading.Tasks

Module Program
    Async Function Log() As MyTask
        Await Task.Yield()
    End Function

    Sub Main()
    End Sub
End Module
", parseOptions:=Vb17_13)
            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36945: The 'Async' modifier can only be used on Subs, or on Functions that return Task or Task(Of T).
    Async Function Log() As MyTask
                            ~~~~~~
BC42105: Function 'Log' doesn't return a value on all code paths. A null reference exception could occur at run time when the result is used.
    End Function
    ~~~~~~~~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
