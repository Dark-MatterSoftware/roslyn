Option Strict On
Option Explicit On
Option Infer On

Interface IMeasure(Of T As IMeasure(Of T))
    Shared MustOverride ReadOnly Property Zero As T
    Shared MustOverride Operator +(left As T, right As T) As T

    Shared Overridable ReadOnly Property Unit As String
        Get
            Return "units"
        End Get
    End Property
End Interface

Delegate Function Rescale(ByRef ReadOnly value As Length) As Length

Structure Length
    Implements IMeasure(Of Length)

    Public Value As Double

    Public Shared ReadOnly Property Zero As Length Implements IMeasure(Of Length).Zero
        Get
            Return New Length()
        End Get
    End Property

    Public Shared Operator +(left As Length, right As Length) As Length
        Return New Length With {.Value = left.Value + right.Value}
    End Operator

    Public Shared Operator -(ByRef ReadOnly left As Length, ByRef ReadOnly right As Length) As Length
        Return New Length With {.Value = left.Value - right.Value}
    End Operator

    Public ReadOnly Function Describe() As String
        Return $"{Value} m"
    End Function
End Structure

Module WaveTwelve
    Function Total(Of T As IMeasure(Of T))(items As T()) As T
        Dim sum = T.Zero
        For Each item In items
            sum = sum + item
        Next
        Return sum
    End Function

    Function UnitOf(Of T As IMeasure(Of T))() As String
        Return T.Unit
    End Function

    Sub Run()
        Dim sum = Total({New Length With {.Value = 1.5}, New Length With {.Value = 2}})
        Dim doubled As Rescale = Function(l) l + l
        Dim gap = doubled(sum) - New Length With {.Value = 1}
        Console.WriteLine($"{sum.Describe()} {gap.Describe()} {UnitOf(Of Length)()}")
        Console.WriteLine(NameOf(System.Collections.Generic.List(Of)))
    End Sub
End Module
