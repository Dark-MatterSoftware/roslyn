Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic

Module WaveThirtyFive
    Private ReadOnly Store As New List(Of Integer) From {1, 2, 3, 4}

    Function Make() As List(Of Integer)
        Return Store
    End Function

    ReadOnly Property Items As List(Of Integer)
        Get
            Return Store
        End Get
    End Property

    Function Describe(value As Object) As String
        If TypeOf value IsNot List(Of Integer) Into list Then Return "not a list"

        Return $"{list.Count} items ending in {list(^1)}"
    End Function

    Sub Run()
        Make()(^1) = 20
        Make()(^2) += 5
        Items(^3) = 30
        Console.WriteLine($"{String.Join(",", Store)} {Describe(Store)}, {Describe("x")}")
    End Sub
End Module
