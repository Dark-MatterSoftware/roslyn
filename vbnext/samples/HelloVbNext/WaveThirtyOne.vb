Option Strict On
Option Explicit On
Option Infer Off

Module WaveThirtyOne
    Function Reading() As (Integer, String)
        Return (7000, "meters")
    End Function

    Function Placement() As ((Integer, Integer), String)
        Return ((3, 4), "corner")
    End Function

    Sub Run()
        Dim (distance As Long, unit As String) = Reading()
        Dim ((column As Long, row As Long), spot As Object) = Placement()

        Console.WriteLine($"{distance * 1000L} {unit} {column + row} {spot}")
    End Sub
End Module
