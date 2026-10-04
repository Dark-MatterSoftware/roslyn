' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ExtensionPropertyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ExtensionProperty_ReceiverIsPassed()
            Dim compilation = CreateCompilation("
Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property
    End Extension
End Module

Module Program
    Sub Main()
        System.Console.Write(""hi"".WordCount)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionIndexer_ReceiverAndIndexArePassed()
            Dim compilation = CreateCompilation("
Class Box
    Public Value As String
End Class

Module BoxExtensions
    Extension(box As Box)
        Public ReadOnly Property Item(index As Integer) As Char
            Get
                Return box.Value(index)
            End Get
        End Property
    End Extension
End Module

Module Program
    Sub Main()
        Dim box As New Box()
        box.Value = ""hi""
        System.Console.Write(box(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="h").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionSharedMember_CalledOnExtendedType()
            Dim compilation = CreateCompilation("
Module StringExtensions
    Extension(s As String)
        Public Shared Function Twice(value As String) As String
            Return value & value
        End Function

        Public Shared ReadOnly Property Marker As String
            Get
                Return ""!""
            End Get
        End Property
    End Extension
End Module

Module Program
    Sub Main()
        System.Console.Write(String.Twice(""hi""))
        System.Console.Write(String.Marker)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="hihi!").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionProperty_RejectedOutsideModule()
            Dim compilation = CreateCompilation("
Class C
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property
    End Extension
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExtensionMethodNotInModule, "Extension(s As String)").WithLocation(3, 5))
        End Sub

        <Fact>
        Public Sub ExtensionProperty_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property
    End Extension
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedDeclaration, "Extension").WithLocation(3, 5),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "s").WithArguments("s").WithLocation(6, 24),
                Diagnostic(ERRID.ERR_UnrecognizedEnd, "End").WithLocation(9, 5))
        End Sub

    End Class

End Namespace
