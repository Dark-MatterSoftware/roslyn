Option Strict On
Option Explicit On
Option Infer Off

Imports System.Collections.Generic
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports System.Threading.Tasks

Class Journal
    Implements IAsyncDisposable

    Private ReadOnly _entries As New List(Of String)()

    Public Async Function WriteAsync(entry As String) As ValueTask
        Await Task.Yield()
        _entries.Add(entry)
    End Function

    Public Async Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        Await Task.Yield()
        Console.WriteLine($"journal: {String.Join(", ", _entries)}")
    End Function
End Class

Module WaveSix
    <AsyncMethodBuilder(GetType(PoolingAsyncValueTaskMethodBuilder(Of )))>
    Async Function SquareAsync(value As Integer) As ValueTask(Of Integer)
        Await Task.Yield()
        Return value * value
    End Function

    Function Label(value As Object) As String
        If TypeOf value IsNot String Into text Then Return "not text"

        Return $"text of length {text.Length}"
    End Function

    Async Function RunAsync() As Task
        Dim evens As Func(Of Integer, IAsyncEnumerable(Of Integer)) =
            Async Iterator Function(count As Integer)
                For i As Integer = 1 To count
                    Await Task.Delay(5)
                    Yield i * 2
                Next
            End Function

        Using cancellation As New CancellationTokenSource()
            Dim journal As New Journal()
            Async Using journal.ConfigureAwait(False)
                Await Each n As Integer In evens(3).WithCancellation(cancellation.Token).ConfigureAwait(False)
                    Await journal.WriteAsync($"even {n}")
                Next

                Try
                    Throw New InvalidOperationException("disk full")
                Catch ex As InvalidOperationException
                    Await journal.WriteAsync($"caught {ex.Message}")
                Finally
                    Await journal.WriteAsync("finally")
                End Try
            End Using
        End Using

        Console.WriteLine(Await SquareAsync(12))
        Console.WriteLine(Label("hello"))
        Console.WriteLine(Label(42))
    End Function
End Module
