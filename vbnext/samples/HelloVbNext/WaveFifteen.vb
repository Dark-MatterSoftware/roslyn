Option Strict On
Option Explicit On
Option Infer On

Imports Span2 = (Start As Integer, Length As Integer)

Interface IGreeter
    Const Greeting As String = "hello"
    Shared Calls As Integer

    Shared Sub New()
        Calls = 0
    End Sub

    Private Function Decorate(name As String) As String
        Return $"{Greeting} {name}"
    End Function

    Function Greet(name As String) As String
        Calls += 1
        Return Decorate(name)
    End Function

    Custom Event Greeted As EventHandler
        AddHandler(value As EventHandler)
            Console.Write("subscribed ")
        End AddHandler
        RemoveHandler(value As EventHandler)
            Console.Write("unsubscribed ")
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent
    End Event
End Interface

Class Greeter
    Implements IGreeter
End Class

Module WaveFifteen
    Function Shift(s As Span2, by As Integer) As Span2
        Return (s.Start + by, s.Length)
    End Function

    Sub OnGreeted(sender As Object, e As EventArgs)
    End Sub

    Sub Run()
        Dim g As IGreeter = New Greeter()
        Console.Write($"{g.Greet("world")} {IGreeter.Calls} ")
        AddHandler g.Greeted, AddressOf OnGreeted
        RemoveHandler g.Greeted, AddressOf OnGreeted

        Dim scale = Function(value As Integer, Optional factor As Integer = 2) value * factor
        Dim total = Function(ParamArray values() As Integer) values.Sum()
        Console.Write($"{scale(5)} {scale(5, 3)} {total(1, 2, 3)} ")

        Dim moved = Shift((2, 4), 3)
        Console.WriteLine($"{moved.Start}+{moved.Length}")
    End Sub
End Module
