Option Strict On
Option Explicit On
Option Infer Off

Imports System.Xml.Linq

Record Class Point(X As Integer, Y As Integer)
End Class

Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Split(" "c).Length
            End Get
        End Property
    End Extension
End Module

Class Resource
    Implements IDisposable

    Private ReadOnly _name As String

    Sub New(name As String)
        _name = name
    End Sub

    Sub Dispose() Implements IDisposable.Dispose
        Console.WriteLine($"disposed {_name}")
    End Sub
End Class

Module Program
    Function Area(width As Integer, height As Integer) As Integer = Return width * height

    Function Describe(count As Integer) As String
        Dim label As String = Select Case count
                                  Case 0
                                      "none"
                                  Case 1
                                      "one"
                                  Case Else
                                      "many"
                              End Select
        Return label
    End Function

    Function Items(names As String()) As XElement
        Return <ul>
                   <%= For Each name As String In names
                           Yield <li><%= name %></li>
                       Next %>
               </ul>
    End Function

    Sub Main()
        Using resource As Resource = New Resource("sample")

        Dim a As New Point(1, 2)
        Dim b As New Point(1, 2)
        Dim c As Point = a With {.Y = 5}
        Console.WriteLine(a)
        Console.WriteLine(a.Equals(b))
        Console.WriteLine(c)
        Console.WriteLine(Area(3, 4))
        Console.WriteLine(Describe(5))
        Console.WriteLine("modern visual basic".WordCount)
        Console.WriteLine(Items({"records", "extensions"}))
        WaveFive.RunAsync().GetAwaiter().GetResult()
        WaveSix.RunAsync().GetAwaiter().GetResult()
        WaveSeven.RunAsync().GetAwaiter().GetResult()
        WaveEight.Run()
        WaveNine.Run()
        WaveTen.Run()
        WaveEleven.Run()
        WaveTwelve.Run()
        WaveThirteen.Run()
        WaveFourteen.Run()
        WaveFifteen.Run()
        WaveSixteen.Run()
        WaveSeventeen.Run()
        WaveEighteen.Run()
        WaveNineteen.Run()
        WaveTwenty.Run()
    End Sub
End Module
