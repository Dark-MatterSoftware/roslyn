Option Strict On
Option Explicit On
Option Infer On

Record Structure Seat(Row As Integer, Number As Integer)
End Structure

Record Class Ticket(Holder As String, Price As Decimal)
    Public Property Holder As String = Holder.Trim()
End Class

Module WaveTwentyOne
    Function CountDistinct(Of T As IEquatable(Of T))(items As T()) As Integer
        Dim seen As New List(Of T)
        For Each item In items
            If Not seen.Exists(Function(other) other.Equals(item)) Then
                seen.Add(item)
            End If
        Next
        Return seen.Count
    End Function

    Sub Greet()
        Console.Write("greet ")
    End Sub

    Sub Run()
        Dim seats = {New Seat(1, 1), New Seat(1, 2), New Seat(1, 1)}
        Dim ticket As New Ticket("  Ann  ", 12.5D)
        Dim makeGreeter = Function() AddressOf Greet
        makeGreeter()()
        Console.WriteLine($"{CountDistinct(seats)} [{ticket.Holder}] {ticket}")
    End Sub
End Module
