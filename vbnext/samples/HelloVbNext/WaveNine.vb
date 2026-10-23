Option Strict On
Option Explicit On
Option Infer On

Imports System.Globalization
Imports System.Numerics
Imports System.Runtime.CompilerServices

Structure Meters
    Implements IAdditionOperators(Of Meters, Meters, Meters), IAdditiveIdentity(Of Meters, Meters)

    Public Value As Double

    Public Shared ReadOnly Property AdditiveIdentity As Meters Implements IAdditiveIdentity(Of Meters, Meters).AdditiveIdentity
        Get
            Return New Meters()
        End Get
    End Property

    Public Shared Operator +(left As Meters, right As Meters) As Meters
        Return New Meters With {.Value = left.Value + right.Value}
    End Operator
End Structure

ByRef Structure Counter
    Private ByRef _count As Integer

    Sub New(ByRef count As Integer)
        _count = ByRef count
    End Sub

    Sub Increment()
        _count += 1
    End Sub
End Structure

<InlineArray(3)>
Structure Triple
    Private _element As Integer
End Structure

Module WaveNine
    Function Total(Of T As {IAdditionOperators(Of T, T, T), IAdditiveIdentity(Of T, T)})(values As T()) As T
        Dim result = T.AdditiveIdentity
        For Each value In values
            result += value
        Next
        Return result
    End Function

    Sub Run()
        Dim distances = {New Meters With {.Value = 1.5}, New Meters With {.Value = 2.25}}
        Console.WriteLine(Total(distances).Value.ToString(CultureInfo.InvariantCulture))

        Dim clicks = 0
        Dim counter As New Counter(clicks)
        counter.Increment()
        counter.Increment()
        Console.WriteLine(clicks)

        Dim scores As Triple
        scores(0) = 7
        scores(1) = 8
        scores(2) = 9
        Dim sum = 0
        For Each score In scores
            sum += score
        Next
        Dim view As ReadOnlySpan(Of Integer) = scores
        Console.WriteLine($"{sum} {view.Length}")
    End Sub
End Module
