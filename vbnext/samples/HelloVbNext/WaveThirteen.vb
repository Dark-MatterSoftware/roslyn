Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic

Interface IQuantity(Of T As IQuantity(Of T))
    Shared MustOverride Widening Operator CType(value As Double) As T

    Shared Overridable Narrowing Operator CType(value As T) As Double
        Return 0
    End Operator

    ReadOnly Property Unit As String
        Get
            Return "units"
        End Get
    End Property

    Shared Function Describe(value As Double) As String
        Return $"quantity of {value}"
    End Function
End Interface

Structure Mass
    Implements IQuantity(Of Mass)

    Public Kilograms As Double

    Public Shared Widening Operator CType(value As Double) As Mass
        Return New Mass With {.Kilograms = value}
    End Operator

    Public Shared Narrowing Operator CType(value As Mass) As Double
        Return value.Kilograms
    End Operator

    Public Shared Operator +(ByRef ReadOnly left As Mass, ByRef ReadOnly right As Mass) As Mass
        Return New Mass With {.Kilograms = left.Kilograms + right.Kilograms}
    End Operator
End Structure

Module WaveThirteen
    Function Make(Of T As IQuantity(Of T))(amount As Double) As T
        Dim result As T = amount
        Return result
    End Function

    Function Amount(Of T As IQuantity(Of T))(value As T) As Double
        Return CType(value, Double)
    End Function

    Sub Run()
        Dim m = Make(Of Mass)(2.5)
        Dim maybe As Mass? = m
        Dim total = maybe + maybe
        Console.WriteLine($"{Amount(m)} {total.Value.Kilograms} {DirectCast(m, IQuantity(Of Mass)).Unit}")
        Console.WriteLine(IQuantity(Of Mass).Describe(Amount(m)))
        Console.WriteLine($"{NameOf(List(Of).Count)} {String.Join(", ", "params", "collections")}")
    End Sub
End Module
