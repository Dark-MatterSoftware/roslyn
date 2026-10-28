Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic

Class Basket
    Public Label As String
End Class

Module WaveSeventeen
    Function Total(values As List(Of Integer)) As Integer
        Dim sum = 0
        For Each value In values
            sum += value
        Next
        Return sum
    End Function

    Function Show(value As Boolean?) As String
        Return If(value.HasValue, value.Value.ToString(), "Nothing")
    End Function

    Sub Run()
        Dim head As Integer() = {1, 2}
        Dim all As List(Of Integer) = {..head, 3}
        Dim pair As (Integer, Integer)? = (1, 2)
        Dim none As (Integer, Integer)? = Nothing
        Console.Write($"{all.Count} {Total({4, 5})} {Total((..head, 6))} {Show(pair = (1, 2))} {Show(none = (1, 2))} ")

        Dim basket As New Basket()
        Dim missing As Basket = Nothing
        basket?.Label ?= "fruit"
        basket?.Label ?= "ignored"
        missing?.Label ?= "never"

        Dim zeros As Integer() = {0, 0, 7}
        Console.WriteLine($"{basket.Label} {zeros.IndexOfAnyExcept(0)} {"hello world".ContainsAny("xw")}")
    End Sub
End Module
