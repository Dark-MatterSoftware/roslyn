' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class PartialPropertyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub PartialProperty_DefinitionAndImplementationMatch()
            Dim compilation = CreateCompilation("
Partial Class Program
    Private _label As String
    Partial Property Label As String
End Class
Partial Class Program
    Partial Property Label As String
        Get
            Return _label
        End Get
        Set(value As String)
            _label = value
        End Set
    End Property
    Shared Sub Main()
        Dim p As New Program()
        p.Label = ""ok""
        System.Console.Write(p.Label)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()

            Dim prop = DirectCast(compilation.GetTypeByMetadataName("Program").GetMembers("Label").Single(), IPropertySymbol)
            Assert.False(prop.IsPartialDefinition)
            Dim definition = prop.PartialDefinitionPart
            Assert.NotNull(definition)
            Assert.True(definition.IsPartialDefinition)
            Assert.Same(prop, definition.PartialImplementationPart)
            Assert.Null(prop.PartialImplementationPart)
            Assert.Null(definition.PartialDefinitionPart)
        End Sub

        <Fact>
        Public Sub PartialProperty_AccessorsSplitAcrossParts()
            Dim compilation = CreateCompilation("
Partial Class Program
    Private _label As String
    Partial Property Label As String
        Get
            Return _label
        End Get
    End Property
End Class
Partial Class Program
    Partial Property Label As String
        Set(value As String)
            _label = value
        End Set
    End Property
    Shared Sub Main()
        Dim p As New Program()
        p.Label = ""ok""
        System.Console.Write(p.Label)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub PartialProperty_TypeMismatch()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial ReadOnly Property Label As String
    Partial ReadOnly Property Label As Integer
        Get
            Return 1
        End Get
    End Property
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PartialPropertyTypeMismatch, "Label").WithArguments("Label").WithLocation(4, 31))
        End Sub

        <Fact>
        Public Sub PartialProperty_ModifierMismatch()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial ReadOnly Property Label As String
    Partial Property Label As String
        Get
            Return ""ok""
        End Get
        Set(value As String)
        End Set
    End Property
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PartialPropertyModifierMismatch, "Label").WithArguments("Label").WithLocation(4, 22))
        End Sub

        <Fact>
        Public Sub PartialProperty_MissingImplementation()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Property Label As String
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PartialPropertyDeclarationMismatch, "Label").WithArguments("Label").WithLocation(3, 22))
        End Sub

        <Fact>
        Public Sub PartialProperty_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Property Label As String
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadPropertyFlags1, "Partial").WithArguments("Partial").WithLocation(3, 5))
        End Sub
    End Class
End Namespace
