' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class InitOnlyPropertyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub InitOnlyProperty_ConstructorCanAssign()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Class Box
    Public InitOnly Property Value As Integer

    Public Sub New(value As Integer)
        Me.Value = value
    End Sub
End Class

Class Program
    Shared Sub Main()
        Dim box = New Box(4)
        System.Console.Write(box.Value)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4").VerifyDiagnostics()

            Dim [property] = compilation.GetMember(Of PropertySymbol)("Box.Value")
            Assert.True([property].SetMethod.IsInitOnly)
            Assert.Equal("System.Runtime.CompilerServices.IsExternalInit", [property].SetMethod.ReturnTypeCustomModifiers.Single().Modifier.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub InitOnlyProperty_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Box
    Public InitOnly Property Value As Integer
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_LanguageVersion, "InitOnly").WithArguments("17.13", "init-only properties", "vbnext").WithLocation(3, 12))
        End Sub
    End Class
End Namespace
