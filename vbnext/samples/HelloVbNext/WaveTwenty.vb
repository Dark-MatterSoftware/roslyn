Option Strict On
Option Explicit On
Option Infer On

Record Class Parcel(Of T)(Contents As T, Note As String)
End Class

Module WaveTwenty
    Function Inc(value As Integer) As Integer
        Return value + 1
    End Function

    Function Dbl(value As Integer) As Integer
        Return value * 2
    End Function

    Sub Run()
        Dim steps = {AddressOf Inc, AddressOf Dbl}
        Dim pick = If(steps.Length > 1, AddressOf Dbl, AddressOf Inc)
        Dim boxed As Object = AddressOf Inc
        Dim total = 3
        For Each [step] In steps
            total = [step](total)
        Next
        Dim parcel As New Parcel(Of Integer)(total, Nothing)
        Dim relabeled = parcel With {.Note = "done"}
        Console.WriteLine($"{parcel} {relabeled} {pick(5)} {DirectCast(boxed, [Delegate]).DynamicInvoke(1)}")
    End Sub
End Module
