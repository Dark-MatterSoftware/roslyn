Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports System.Threading.Tasks

Module WaveSeven
    Private ReadOnly s_gate As New Lock()

    Function CountVowels(text As String) As Integer
        Dim letters As ReadOnlySpan(Of Char) = text.AsSpan()
        Dim count = 0
        For i = 0 To letters.Length - 1
            If "aeiou".Contains(letters(i)) Then count += 1
        Next
        Return count
    End Function

    Async Function RunAsync() As Task
        Dim prices As New Dictionary(Of String, Decimal) From {{"tea", 2.5D}, {"cake", 4D}}
        For Each (item, price) In prices
            Console.WriteLine($"{item} costs {price}")
        Next

        Dim title As ReadOnlySpan(Of Char) = "wave seven".AsSpan()
        Console.WriteLine($"starts with {title(0)}, {CountVowels("wave seven")} vowels")
        Await Task.Yield()

        Using s_gate.EnterScope()
            Console.WriteLine("inside the lock")
        End Using

        Dim rounds As Func(Of CancellationToken, IAsyncEnumerable(Of (String, Integer))) =
            Async Iterator Function(<EnumeratorCancellation> token As CancellationToken)
                For i = 1 To 2
                    Await Task.Delay(5, token)
                    Yield ($"round {i}", i * 10)
                Next
            End Function

        Await Each (round, score) In rounds(CancellationToken.None)
            Console.WriteLine($"{round}: {score}")
        Next
    End Function
End Module
