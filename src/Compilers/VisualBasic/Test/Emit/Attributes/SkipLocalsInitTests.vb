' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class SkipLocalsInitTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const AttributePreamble As String = "
Namespace System.Runtime.CompilerServices
    Public Class SkipLocalsInitAttribute
        Inherits System.Attribute
    End Class
End Namespace
"

        <Fact>
        Public Sub SkipLocalsInit_OnMethodOmitsLocalsInit()
            Dim compilation = CreateCompilationWithMscorlib40(AttributePreamble & "
Class Program
    <System.Runtime.CompilerServices.SkipLocalsInit>
    Public Shared Sub M()
        Dim value As Integer = 1
        System.Console.Write(value)
    End Sub

    Public Shared Sub N()
        Dim value As Integer = 1
        System.Console.Write(value)
    End Sub
End Class
", options:=TestOptions.DebugDll, parseOptions:=VbNext)

            Dim verifier = CompileAndVerify(compilation, verify:=Verification.Skipped)
            verifier.VerifyDiagnostics()
            AssertSkipsLocalsInit(verifier.VisualizeIL("Program.M"))
            AssertKeepsLocalsInit(verifier.VisualizeIL("Program.N"))
        End Sub

        <Fact>
        Public Sub SkipLocalsInit_OnTypeAndModule()
            Dim compilation = CreateCompilationWithMscorlib40(AttributePreamble & "
<System.Runtime.CompilerServices.SkipLocalsInit>
Class Outer
    Public Shared Sub M()
        Dim value As Integer = 1
        System.Console.Write(value)
    End Sub

    Class Inner
        Public Shared Sub N()
            Dim value As Integer = 1
            System.Console.Write(value)
        End Sub
    End Class
End Class

Class Other
    Public Shared Sub P()
        Dim value As Integer = 1
        System.Console.Write(value)
    End Sub
End Class
", options:=TestOptions.DebugDll, parseOptions:=VbNext)

            Dim verifier = CompileAndVerify(compilation, verify:=Verification.Skipped)
            verifier.VerifyDiagnostics()
            AssertSkipsLocalsInit(verifier.VisualizeIL("Outer.M"))
            AssertSkipsLocalsInit(verifier.VisualizeIL("Outer.Inner.N"))
            AssertKeepsLocalsInit(verifier.VisualizeIL("Other.P"))
        End Sub

        <Fact>
        Public Sub SkipLocalsInit_OnNetModule()
            Dim compilation = CreateCompilationWithMscorlib40("
<Module: System.Runtime.CompilerServices.SkipLocalsInit>

Namespace System.Runtime.CompilerServices
    Public Class SkipLocalsInitAttribute
        Inherits System.Attribute
    End Class
End Namespace

Class Program
    Public Shared Sub M()
        Dim value As Integer = 1
        System.Console.Write(value)
    End Sub
End Class
", options:=TestOptions.DebugDll, parseOptions:=VbNext)

            Dim verifier = CompileAndVerify(compilation, verify:=Verification.Skipped)
            verifier.VerifyDiagnostics()
            AssertSkipsLocalsInit(verifier.VisualizeIL("Program.M"))
        End Sub

        <Fact>
        Public Sub SkipLocalsInit_OnPropertyAppliesToAccessors()
            Dim compilation = CreateCompilationWithMscorlib40(AttributePreamble & "
Class Program
    <System.Runtime.CompilerServices.SkipLocalsInit>
    Public Shared Property P As Integer
        Get
            Dim value As Integer = 1
            Return value
        End Get
        Set
            Dim copy As Integer = Value
            System.Console.Write(copy)
        End Set
    End Property
End Class
", options:=TestOptions.DebugDll, parseOptions:=VbNext)

            Dim verifier = CompileAndVerify(compilation, verify:=Verification.Skipped)
            verifier.VerifyDiagnostics()
            AssertSkipsLocalsInit(verifier.VisualizeIL("Program.get_P"))
            AssertSkipsLocalsInit(verifier.VisualizeIL("Program.set_P"))
        End Sub

        <Fact>
        Public Sub SkipLocalsInit_RejectedOn17_13AndKeepsLocalsInit()
            Dim compilation = CreateCompilationWithMscorlib40(AttributePreamble & "
Class Program
    <System.Runtime.CompilerServices.SkipLocalsInit>
    Public Shared Sub M()
        Dim value As Integer = 1
        System.Console.Write(value)
    End Sub
End Class
", options:=TestOptions.DebugDll, parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_AttributeNotSupportedInVB, "System.Runtime.CompilerServices.SkipLocalsInit").WithArguments("System.Runtime.CompilerServices.SkipLocalsInitAttribute").WithLocation(9, 6))
            AssertKeepsLocalsInit(CompileAndVerify(compilation).VisualizeIL("Program.M"))
        End Sub

        Private Shared Sub AssertSkipsLocalsInit(il As String)
            Assert.Contains(".locals (", il)
            Assert.DoesNotContain(".locals init", il)
        End Sub

        Private Shared Sub AssertKeepsLocalsInit(il As String)
            Assert.Contains(".locals init", il)
        End Sub

    End Class

End Namespace
