Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic

Module WaveTwentyNine
    Private Builds As Integer

    Function BuildQueue() As List(Of String)
        Builds += 1
        Return New List(Of String) From {"first", "second", "third"}
    End Function

    Sub Run()
        Dim letters As ReadOnlySpan(Of Char) = "operations".AsSpan()
        Dim tail As Index = ^4
        Dim middle As Range = 2..^2
        Dim queue = BuildQueue()
        queue(^1) = "last"
        queue(^2) &= "!"
        Console.WriteLine($"{letters(tail)} {letters(middle).ToString()} {String.Join(",", queue)} {BuildQueue()(^1)} {Builds}")
    End Sub
End Module
