Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic

Module WaveThirtyFour
    Function Label(order As (Integer, String)) As String
        Dim text = Select Case order
                       Case Is (> 100, "open")
                           "large open"
                       Case Is (1 To 9, "draft")
                           "small draft"
                       Case Else
                           "other"
                   End Select
        Return text
    End Function

    Sub Run()
        Dim orders As List(Of (Integer, String)) = {(250, "open"), (4, "draft"), (40, "open")}
        Dim labels As New List(Of String)
        For Each (size, state) In orders
            labels.Add($"{size}:{Label((size, state))}")
        Next

        Dim extra As String() = {"closed"}
        Dim all As List(Of String) = {..labels, ..extra}
        Console.WriteLine($"{all(^1)} {String.Join(",", all(1..^1))} {all.Count}")
    End Sub
End Module
