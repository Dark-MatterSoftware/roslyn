Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Reflection

<AttributeUsage(AttributeTargets.All)>
Class CaptionAttribute
    Inherits Attribute

    Public ReadOnly Property Text As String

    Sub New(text As String)
        Me.Text = text
    End Sub
End Class

Module WaveTwentySix
    <Caption(NameOf(count))>
    Function Repeat(word As String, count As Integer) As String
        Return String.Concat(Enumerable.Repeat(word, count))
    End Function

    Sub Run()
        Dim caption = GetType(WaveTwentySix).GetMethod(NameOf(Repeat), BindingFlags.Static Or BindingFlags.Public Or BindingFlags.NonPublic).GetCustomAttribute(Of CaptionAttribute)()
        Console.Write($"{caption.Text} ")

        Dim scores As New List(Of Integer) From {3, 5, 8, 13}
        Dim word = "rangefinder"
        scores(^1) = 21
        Console.WriteLine($"{scores(^1)} {String.Join(",", scores(1..^1))} {word(^1)} {word(..5)} {word(5..)}")
    End Sub
End Module
