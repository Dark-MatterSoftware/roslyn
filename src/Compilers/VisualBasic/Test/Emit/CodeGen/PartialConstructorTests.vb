' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class PartialConstructorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub PartialConstructor_DefinitionAndImplementationMatch()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Sub New()
    End Sub
End Class
Partial Class Program
    Sub New()
        System.Console.Write(""ok"")
    End Sub
    Shared Sub Main()
        Dim p = New Program()
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()

            Dim ctor = DirectCast(compilation.GetTypeByMetadataName("Program").Constructors.Single(), IMethodSymbol)
            Assert.True(ctor.IsPartialDefinition)
            Assert.NotNull(ctor.PartialImplementationPart)
            Assert.False(ctor.PartialImplementationPart.IsPartialDefinition)
        End Sub

        <Fact>
        Public Sub PartialConstructor_ModifierMismatch()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Private Sub New()
    End Sub
    Public Sub New()
        System.Console.Write(""ok"")
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PartialConstructorModifierMismatch, "New").WithLocation(5, 16))
        End Sub

        <Fact>
        Public Sub PartialConstructor_MissingImplementation()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Sub New()
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PartialConstructorDeclarationMismatch, "New").WithLocation(3, 17))
        End Sub

        <Fact>
        Public Sub PartialConstructor_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Sub New()
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ConstructorCannotBeDeclaredPartial, "Partial").WithArguments("Partial").WithLocation(3, 5))
        End Sub
    End Class
End Namespace
