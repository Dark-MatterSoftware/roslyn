Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Numerics
Imports System.Runtime.InteropServices

Structure Money
    Implements IParsable(Of Money)

    Public Amount As Decimal

    Public Shared Function Parse(s As String, provider As IFormatProvider) As Money Implements IParsable(Of Money).Parse
        Return New Money With {.Amount = Decimal.Parse(s, CultureInfo.InvariantCulture)}
    End Function

    Public Shared Function TryParse(s As String, provider As IFormatProvider, ByRef result As Money) As Boolean Implements IParsable(Of Money).TryParse
        Dim amount As Decimal
        Dim parsed = Decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, amount)
        result = New Money With {.Amount = amount}
        Return parsed
    End Function

    Public Overrides Function ToString() As String
        Return Amount.ToString("0.00", CultureInfo.InvariantCulture)
    End Function
End Structure

Module WaveEight
    Function Sum(Of T As INumber(Of T))(values As IEnumerable(Of T)) As T
        Dim total = T.Zero
        For Each value In values
            total += value
        Next
        Return total
    End Function

    Function Average(Of T As INumber(Of T))(values As T()) As T
        Return Sum(values) / T.CreateChecked(values.Length)
    End Function

    Function ParseAll(Of T As IParsable(Of T))(texts As String()) As List(Of T)
        Dim result As New List(Of T)()
        For Each entry In texts
            result.Add(T.Parse(entry, CultureInfo.InvariantCulture))
        Next
        Return result
    End Function

    Function Describe(Of T As INumber(Of T))(value As T) As String
        Return value.ToString() & If(T.IsNegative(value), " (negative)", "")
    End Function

    Sub Run()
        Console.WriteLine($"{Sum({1, 2, 3})} {Average({2.5, 3.5}).ToString(CultureInfo.InvariantCulture)} {Describe(-4)}")
        Console.WriteLine(String.Join(", ", ParseAll(Of Money)({"1.5", "2.25"})))

        Dim counts As New Dictionary(Of String, Integer)()
        For Each word In "to be or not to be".Split(" "c)
            Dim exists As Boolean
            Dim ByRef count = CollectionsMarshal.GetValueRefOrAddDefault(counts, word, exists)
            count += 1
        Next
        Console.WriteLine($"to={counts("to")} be={counts("be")} or={counts("or")}")

        Dim scores As New List(Of Integer) From {10, 20, 30}
        CollectionsMarshal.AsSpan(scores)(1) += 5
        Dim ByRef ReadOnly first = CollectionsMarshal.AsSpan(scores)(0)
        Console.WriteLine($"{first} {String.Join(",", scores)}")
    End Sub
End Module
