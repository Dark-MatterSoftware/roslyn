Option Strict On
Option Explicit On
Option Infer On

Record Class Badge(Name As String)
    Public NotOverridable Overrides Function ToString() As String
        Return $"[{Name}]"
    End Function
End Class

Record Class VipBadge(Name As String, Level As Integer)
    Inherits Badge(Name)
End Class

Record Class Sku(Code As String)
    Public Overloads Function Equals(other As Sku) As Boolean
        Return other IsNot Nothing AndAlso String.Equals(Code, other.Code, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return StringComparer.OrdinalIgnoreCase.GetHashCode(Code)
    End Function
End Class

Module WaveTwentyTwo
    Function Matches(Of T As IEquatable(Of T))(a As T, b As T) As Boolean
        Return a.Equals(b)
    End Function

    Sub Run()
        Dim vip As New VipBadge("Ann", 3)
        Console.WriteLine($"{vip} {vip.Level} {Matches(New Sku("ab-1"), New Sku("AB-1"))} {New Sku("ab-1") = New Sku("AB-1")}")
    End Sub
End Module
