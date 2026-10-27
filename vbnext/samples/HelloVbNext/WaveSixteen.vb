Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports Score = Integer
Imports Scores = Integer()

Structure Pixel
    Public Left As Integer
    Public Top As Integer
End Structure

Class Clicker
    Public Count As Integer
End Class

Module WaveSixteen
    Function Total(values As Scores) As Score
        Dim sum As Score = 0
        For Each value In values
            sum += value
        Next
        Return sum
    End Function

    Sub Run()
        Dim origin As New Pixel With {.Left = 1, .Top = 2}
        Dim moved = origin With {.Left = 5}
        Dim label = New With {.Name = "a", .Size = 1}
        Dim bigger = label With {.Size = 3}
        Console.Write($"{Total({1, 2, 3})} {(origin.Left, origin.Top) = (1, 2)} {moved.Left}{moved.Top} {bigger.Name}{bigger.Size} ")

        Dim counter As New Clicker()
        Dim missing As Clicker = Nothing
        counter?.Count += 2
        missing?.Count += 2
        Dim names As List(Of String) = {"x", "y"}
        Dim unique As HashSet(Of Integer) = {1, 1, 2}
        Console.WriteLine($"{counter.Count} {names.Count} {unique.Count}")
    End Sub
End Module
