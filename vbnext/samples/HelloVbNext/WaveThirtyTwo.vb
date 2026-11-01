Option Strict On
Option Explicit On
Option Infer On

Module WaveThirtyTwo
    Function Grade(score As (Integer, String)) As String
        Select Case score
            Case Is (100, "math")
                Return "perfect math"
            Case Is (100, "art")
                Return "perfect art"
        End Select
        Return "keep going"
    End Function

    Sub Run()
        Dim (best, ) = (100, "math")
        Dim total = 0
        For Each (points, subject) In {(100, "math"), (90, "art")}
            total += points
        Next

        Console.WriteLine($"{Grade((best, "math"))}, {Grade((90, "art"))}, {total}")
    End Sub
End Module
