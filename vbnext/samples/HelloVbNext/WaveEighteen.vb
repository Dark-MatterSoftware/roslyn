Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic

Module WaveEighteen
    Function Describe(Of T)(values As List(Of T)) As String
        Return $"{GetType(T).Name}x{values.Count}"
    End Function

    Function Joined(separator As String, values As List(Of String)) As String
        Return String.Join(separator, values)
    End Function

    Sub Run()
        Dim seen As Integer() = {3, 1}
        Dim unique As HashSet(Of Integer) = {..seen, ..seen, 2}
        Dim picked As List(Of Integer) = If(seen.Length > 1, {7, 8}, {9})
        Dim none As List(Of Integer) = Nothing
        Dim fallback = If(none, {4, 5, 6})
        Console.WriteLine($"{unique.Count} {Joined(values:={"a", "b"}, separator:="-")} {picked.Count} {fallback.Count} {Describe({1, 2})} {Describe((1.5, 2))}")
    End Sub
End Module
