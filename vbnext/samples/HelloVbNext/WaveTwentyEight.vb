Option Strict On
Option Explicit On
Option Infer On

Module WaveTwentyEight
    Function DescribeWindow(window As Range?) As String
        Return If(window.HasValue, window.Value.ToString(), "none")
    End Function

    Sub Run()
        Dim depth As Integer? = 2
        Dim missing As Integer? = Nothing
        Dim fromEnd = ^depth
        Dim words = {"alpha", "beta", "gamma", "delta"}
        Dim pick = If(fromEnd.HasValue, words(fromEnd.Value), "none")
        Console.WriteLine($"{fromEnd} {pick} {DescribeWindow(..depth)} {DescribeWindow(missing..)} {(^missing).HasValue}")
    End Sub
End Module
