Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Collections.Immutable

ByRef Structure Scope
    Implements IDisposable

    Public Name As String

    Public Sub Dispose() Implements IDisposable.Dispose
        Console.Write($"closed {Name} ")
    End Sub
End Structure

Module WaveFourteen
    Function Distinct(ParamArray values As HashSet(Of Integer)) As Integer
        Return values.Count
    End Function

    Function Grow(ParamArray values As ICollection(Of String)) As Integer
        values.Add("added")
        Return values.Count
    End Function

    Function Frozen(ParamArray values As ImmutableArray(Of Integer)) As Integer
        Return values.Length
    End Function

    Function Measure(Of T As ByRef)(value As T) As T
        Return value
    End Function

    Sub Close(Of T As {IDisposable, ByRef})(value As T)
        value.Dispose()
    End Sub

    Sub Run()
        Console.WriteLine($"{Distinct(1, 1, 2, 3)} {Grow("a", "b")} {Frozen(4, 5, 6, 7)}")
        Dim span As Span(Of Integer) = New Integer() {1, 2, 3}
        Console.Write($"{Measure(span).Length} ")
        Close(New Scope With {.Name = "scope"})
        Console.WriteLine()
    End Sub
End Module
