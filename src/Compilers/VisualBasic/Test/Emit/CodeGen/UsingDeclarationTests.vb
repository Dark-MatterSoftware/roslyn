' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class UsingDeclarationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const ResourceType As String = "
Class Resource
    Implements System.IDisposable

    Private ReadOnly _name As String

    Public Sub New(name As String)
        _name = name
    End Sub

    Public ReadOnly Property Name As String
        Get
            Return _name
        End Get
    End Property

    Public Sub Dispose() Implements System.IDisposable.Dispose
        System.Console.Write(_name)
    End Sub
End Class
"

        <Fact>
        Public Sub UsingDeclaration_DisposesAtEndOfEnclosingBlock()
            Dim compilation = CreateCompilation(ResourceType & "
Class Program
    Shared Sub Main()
        Using first = New Resource(""a"")
            System.Console.Write(""1"")
            Using second = New Resource(""b"")
                System.Console.Write(""2"")
        System.Console.Write(first.Name)
        System.Console.Write(second.Name)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="12abba").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub UsingDeclaration_EndUsingStillEndsEarly()
            Dim compilation = CreateCompilation(ResourceType & "
Class Program
    Shared Sub Main()
        Using first = New Resource(""a"")
            System.Console.Write(""1"")
        End Using
        System.Console.Write(""2"")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1a2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub UsingDeclaration_RejectedOn17_13()
            Dim compilation = CreateCompilation(ResourceType & "
Class Program
    Shared Sub Main()
        Using text = New Resource(""a"")
            System.Console.Write(""1"")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedEndUsing, "Using text = New Resource(""a"")").WithLocation(24, 9))
        End Sub
    End Class
End Namespace
