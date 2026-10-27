' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class StructureWithTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub CopyStructures()
            Dim compilation = CreateCompilation("
Imports System

Structure Point
    Public X As Integer
    Public Y As Integer
    Public Property Label As String
    Private _secret As Integer

    Sub New(secret As Integer)
        _secret = secret
    End Sub

    ReadOnly Property Secret As Integer
        Get
            Return _secret
        End Get
    End Property
End Structure

Class Holder
    Public Shared Moved As Point = (New Point(7) With {.X = 1}) With {.Y = 2}
End Class

Module Program
    Sub Main()
        Dim p As New Point(42) With {.X = 1, .Y = 2, .Label = ""p""}
        Dim q = p With {.Y = 5, .Label = ""q""}
        Dim r = p With {.Y = .X + 10}
        Console.Write($""{p.X}{p.Y}{p.Label} {q.X}{q.Y}{q.Label}{q.Secret} {r.Y} "")
        Dim t As (Name As String, Count As Integer) = (""a"", 1)
        Dim u = t With {.Count = 3}
        Dim v = (1, 2) With {.Item1 = 9}
        Console.Write($""{t.Count}{u.Name}{u.Count} {v.Item1}{v.Item2} {Holder.Moved.X}{Holder.Moved.Y}{Holder.Moved.Secret}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="12p 15q42 11 1a3 92 127").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CopySetsInitOnlyProperty()
            Dim compilation = CreateCompilation("
Imports System

Namespace System.Runtime.CompilerServices
    Friend Class IsExternalInit
    End Class
End Namespace

Structure Size
    Public InitOnly Property Width As Integer
    Public InitOnly Property Height As Integer
End Structure

Module Program
    Sub Main()
        Dim s As New Size With {.Width = 2, .Height = 3}
        Dim wide = s With {.Width = 8}
        Console.Write($""{s.Width}{s.Height} {wide.Width}{wide.Height}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="23 83").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub StructureWithErrors()
            Dim compilation = CreateCompilation("
Structure Point
    Public X As Integer
    Public ReadOnly Fixed As Integer
    ReadOnly Property Total As Integer
        Get
            Return X
        End Get
    End Property
    Sub Reset()
    End Sub
End Structure

Class Shape
    Public X As Integer
End Class

Module Program
    Sub Main()
        Dim p As New Point()
        Dim a = p With {.Missing = 1}
        Dim b = p With {.X = 1, .X = 2}
        Dim c = p With {.Fixed = 1}
        Dim d = p With {.Total = 1}
        Dim e = p With {.Reset = 1}
        Dim f = (New Shape() With {.X = 1}) With {.X = 2}
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30456: 'Missing' is not a member of 'Point'.
        Dim a = p With {.Missing = 1}
                         ~~~~~~~
BC30989: Multiple initializations of 'X'.  Fields and properties can be initialized only once in an object initializer expression.
        Dim b = p With {.X = 1, .X = 2}
                                 ~
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        Dim c = p With {.Fixed = 1}
                         ~~~~~
BC30526: Property 'Total' is 'ReadOnly'.
        Dim d = p With {.Total = 1}
                        ~~~~~~~~~~
BC30990: Member 'Reset' cannot be initialized in an object initializer expression because it is not a field or property.
        Dim e = p With {.Reset = 1}
                         ~~~~~
BC37377: 'Shape' is not a record, so it cannot be copied with 'With'.
        Dim f = (New Shape() With {.X = 1}) With {.X = 2}
                                            ~~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub StructureWithNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Structure Point
    Public X As Integer
End Structure

Module Program
    Sub Main()
        Dim p As New Point()
        Dim q = p With {.X = 1}
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30205: End of statement expected.
        Dim q = p With {.X = 1}
                  ~~~~
</expected>)
        End Sub
    End Class
End Namespace
