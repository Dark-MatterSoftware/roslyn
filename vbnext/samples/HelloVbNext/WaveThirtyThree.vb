Option Strict On
Option Explicit On
Option Infer On

Imports System.Threading.Tasks

Module WaveThirtyThree
    Async Function NextTicket() As Task(Of Integer)
        Static issued As Integer = 100
        Await Task.Yield()
        issued += 1
        Return issued
    End Function

    Function Triage(order As (Integer, String)) As String
        Select Case order
            Case Is (> 100, "open")
                Return "large open"
            Case Is (1 To 9, "draft")
                Return "small draft"
            Case Is (<> 0, "open")
                Return "open"
        End Select
        Return "other"
    End Function

    Sub Run()
        Dim first = NextTicket().Result
        Dim second = NextTicket().Result
        Console.WriteLine($"{first} {second} {Triage((250, "open"))}, {Triage((4, "draft"))}, {Triage((40, "open"))}")
    End Sub
End Module
