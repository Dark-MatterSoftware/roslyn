Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Reflection

Delegate Function Formatter(<Caption(NameOf(text))> text As String) As String

Class Ledger
    Private ReadOnly _entries As New Dictionary(Of String, Integer) From {{"rent", 900}}

    Default ReadOnly Property Item(<Caption(NameOf(account))> account As String) As Integer
        Get
            Return _entries(account)
        End Get
    End Property
End Class

Module WaveTwentySeven
    Sub Run()
        Dim last As Index = ^1
        Dim middle As Range = 1..^1
        Dim scores = {3, 5, 8, 13}
        Dim word = "wavelength"
        Dim buffer As Memory(Of Integer) = scores
        Dim books As New Ledger()

        Dim account = GetType(Ledger).GetProperty("Item").GetIndexParameters()(0).GetCustomAttribute(Of CaptionAttribute)()
        Dim text = GetType(Formatter).GetMethod("Invoke").GetParameters()(0).GetCustomAttribute(Of CaptionAttribute)()

        Console.WriteLine($"{scores(last)} {String.Join(",", scores(middle))} {word(middle)} {buffer(middle).Length} {last} {middle} {account.Text} {text.Text} {books("rent")}")
    End Sub
End Module
