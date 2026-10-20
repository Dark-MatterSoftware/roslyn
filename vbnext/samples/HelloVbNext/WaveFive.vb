Option Strict On
Option Explicit On
Option Infer Off

Imports System.Collections.Generic
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports System.Threading.Tasks

Class Connection
    Implements IAsyncDisposable

    Public Property Name As String
        Get
            Return Field
        End Get
        Set
            If String.IsNullOrWhiteSpace(Value) Then Throw New ArgumentException("A name is required.")
            Field = Value.Trim()
        End Set
    End Property

    Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        Console.WriteLine($"closed {Name}")
        Return ValueTask.CompletedTask
    End Function
End Class

Module WaveFive
    Async Iterator Function Countdown(start As Integer, <EnumeratorCancellation> Optional token As CancellationToken = Nothing) As IAsyncEnumerable(Of Integer)
        For i As Integer = start To 1 Step -1
            Await Task.Delay(10, token)
            Yield i
        Next
    End Function

    Function Describe(value As Object) As String
        Select Case value
            Case Is TypeOf Integer Into n
                Return $"number {n + 1}"
            Case Is TypeOf String Into s
                Return $"text {s.ToUpperInvariant()}"
        End Select

        If TypeOf value Is Connection Into c Then Return $"connection {c.Name}"
        Return "something else"
    End Function

    Async Function RunAsync() As Task
        Async Using connection As Connection = New Connection() With {.Name = " db "}
            Await Each n As Integer In Countdown(3)
                Console.WriteLine(n)
            Next
            Console.WriteLine(Describe(connection))
        End Using

        Console.WriteLine(Describe(41))
        Console.WriteLine(Describe("hi"))
    End Function
End Module
