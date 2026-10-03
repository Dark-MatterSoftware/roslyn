' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class PartialEventTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub PartialEvent_DefinitionAndImplementationMatch()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Event Click As System.Action
End Class
Partial Class Program
    Private _click As System.Action
    Partial Custom Event Click As System.Action
        AddHandler(value As System.Action)
            _click = DirectCast(System.Delegate.Combine(_click, value), System.Action)
        End AddHandler
        RemoveHandler(value As System.Action)
            _click = DirectCast(System.Delegate.Remove(_click, value), System.Action)
        End RemoveHandler
        RaiseEvent()
            If _click IsNot Nothing Then
                _click()
            End If
        End RaiseEvent
    End Event
    Sub Fire()
        RaiseEvent Click()
    End Sub
    Shared Sub Main()
        Dim p As New Program()
        Dim ran = False
        AddHandler p.Click, Sub() ran = True
        p.Fire()
        System.Console.Write(If(ran, ""ok"", ""no""))
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()

            Dim [event] = DirectCast(compilation.GetTypeByMetadataName("Program").GetMembers("Click").Single(), IEventSymbol)
            Assert.False([event].IsPartialDefinition)
            Dim definition = [event].PartialDefinitionPart
            Assert.NotNull(definition)
            Assert.True(definition.IsPartialDefinition)
            Assert.Same([event], definition.PartialImplementationPart)
        End Sub

        <Fact>
        Public Sub PartialEvent_TypeMismatch()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Event Click As System.Action
    Partial Custom Event Click As System.Action(Of Integer)
        AddHandler(value As System.Action(Of Integer))
        End AddHandler
        RemoveHandler(value As System.Action(Of Integer))
        End RemoveHandler
        RaiseEvent(value As Integer)
        End RaiseEvent
    End Event
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PartialEventTypeMismatch, "Click").WithArguments("Click").WithLocation(4, 26))
        End Sub

        <Fact>
        Public Sub PartialEvent_ModifierMismatch()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Event Click As System.Action
    Partial Shared Custom Event Click As System.Action
        AddHandler(value As System.Action)
        End AddHandler
        RemoveHandler(value As System.Action)
        End RemoveHandler
        RaiseEvent()
        End RaiseEvent
    End Event
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PartialEventModifierMismatch, "Click").WithArguments("Click").WithLocation(4, 33))
        End Sub

        <Fact>
        Public Sub PartialEvent_MissingImplementation()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Event Click As System.Action
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PartialEventDeclarationMismatch, "Click").WithArguments("Click").WithLocation(3, 19))
        End Sub

        <Fact>
        Public Sub PartialEvent_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Partial Class Program
    Partial Event Click As System.Action
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadEventFlags1, "Partial").WithArguments("Partial").WithLocation(3, 5))
        End Sub
    End Class
End Namespace
