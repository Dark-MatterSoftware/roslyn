Option Strict On
Option Explicit On
Option Infer On

Record Class Pet(Name As String)
End Class

Record Class Hound(Name As String, Breed As String)
    Inherits Pet(Name)
End Class

Module WaveNineteen
    Function Twice(value As Integer) As Integer
        Return value * 2
    End Function

    Sub Run()
        Dim rex As New Hound("Rex", "Lab")
        Dim asPet As Pet = rex
        Dim renamed = asPet With {.Name = "Max"}
        Dim (name, breed) = rex
        Dim doubled = AddressOf Twice
        Console.WriteLine($"{renamed} {name}/{breed} {rex = New Hound("Rex", "Lab")} {New Pet("Rex") = asPet} {doubled(21)}")
    End Sub
End Module
