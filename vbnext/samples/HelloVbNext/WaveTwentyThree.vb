Option Strict On
Option Explicit On
Option Infer On

Module WaveTwentyThree
    Function Chain(Of T As [Delegate])(first As T, second As T) As T
        Return DirectCast([Delegate].Combine(first, second), T)
    End Function

    Function Describe(Of T As MulticastDelegate)(handler As T) As String
        Return $"{handler.Method.Name}x{handler.GetInvocationList().Length}"
    End Function

    Sub Tick()
        Console.Write("tick ")
    End Sub

    Sub Run()
        Dim both = Chain(New Action(AddressOf Tick), New Action(AddressOf Tick))
        both()
        Console.WriteLine(Describe(both))
    End Sub
End Module
