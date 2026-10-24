Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic

Interface INamed
    Shared Overridable Function DefaultName() As String
        Return "unnamed"
    End Function
End Interface

Class Anonymous
    Implements INamed
End Class

Class Named
    Implements INamed

    Shared Function DefaultName() As String Implements INamed.DefaultName
        Return "named"
    End Function
End Class

Class Totals
    Private ReadOnly _values As Integer() = {3, 4, 5}
    Private ReadOnly _first As Integer = 3

    ByRef ReadOnly Function First() As Integer
        Return _first
    End Function

    ByRef ReadOnly Function Item(index As Integer) As Integer
        Return _values(index)
    End Function
End Class

Module WaveEleven
    Function DefaultOf(Of T As INamed)() As String
        Return T.DefaultName()
    End Function

    Function SumOfSquares(ByRef ReadOnly a As Integer, ByRef ReadOnly b As Integer) As Integer
        Return a * a + b * b
    End Function

    Sub Run()
        Console.WriteLine($"{NameOf(List(Of))} {NameOf(Dictionary(Of ,))}")
        Console.WriteLine($"{DefaultOf(Of Anonymous)()} {DefaultOf(Of Named)()}")

        Dim totals As New Totals()
        Dim ByRef ReadOnly first = totals.First()
        Console.WriteLine(SumOfSquares(first, totals.Item(1)))
    End Sub
End Module
