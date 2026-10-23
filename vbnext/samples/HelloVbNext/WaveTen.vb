Option Strict On
Option Explicit On
Option Infer On

Imports System.Globalization
Imports System.Runtime.CompilerServices

<InlineArray(4)>
Structure Quad
    Private _element As Integer
End Structure

ByRef Structure Tally
    Private ByRef _total As Integer

    Sub New(ByRef total As Integer)
        _total = ByRef total
    End Sub

    Sub Add(amount As Integer)
        _total += amount
    End Sub
End Structure

Module WaveTen
    Function Parser(Of T As IParsable(Of T))() As Func(Of String, IFormatProvider, T)
        Return AddressOf T.Parse
    End Function

    Function Squares() As Quad
        Dim q As Quad
        For i = 0 To 3
            q(i) = i * i
        Next
        Return q
    End Function

    Function Track(ByRef total As Integer) As Tally
        Return New Tally(total)
    End Function

    Sub Run()
        Dim parse = Parser(Of Integer)()
        Console.WriteLine(parse("42", CultureInfo.InvariantCulture) + 1)

        Dim sum = 0
        For Each square In Squares()
            sum += square
        Next
        Console.WriteLine($"{Squares()(3)} {sum}")

        Dim total = 0
        Dim tally = Track(total)
        tally.Add(5)
        tally.Add(2)
        Console.WriteLine(total)
    End Sub
End Module
