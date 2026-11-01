Option Strict On
Option Explicit On
Option Infer On

Class Shelf
    Public Label As String
    Public Property Count As Integer
End Class

Module WaveThirty
    Function StockLevels() As ((String, Integer), Integer)()
        Return {(("bolts", 40), 2), (("nuts", 15), 5)}
    End Function

    Sub Run()
        Dim ((firstName, firstCount), firstAisle) = StockLevels()(0)

        Dim total = 0
        For Each ((name, count As Long), ) In StockLevels()
            total += CInt(count)
        Next

        Dim shelf As New Shelf()
        Dim aisles(1) As Integer
        ((shelf.Label, shelf.Count), aisles(1)) = StockLevels()(1)
        (shelf.Count, ) = (shelf.Count + 1, "ignored")

        Console.WriteLine($"{firstName} {firstCount} {firstAisle} {total} {shelf.Label} {shelf.Count} {aisles(1)}")
    End Sub
End Module
