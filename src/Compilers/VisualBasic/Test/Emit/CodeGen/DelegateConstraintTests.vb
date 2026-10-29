' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class DelegateConstraintTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub DelegateAndMulticastDelegateConstrainTypeParameters()
            Dim compilation = CreateCompilation("
Module Program
    Function Combine(Of T As System.Delegate)(a As T, b As T) As T
        Return DirectCast(System.Delegate.Combine(a, b), T)
    End Function

    Function MethodName(Of T As System.MulticastDelegate)(d As T) As String
        Return d.Method.Name
    End Function

    Sub Hello()
    End Sub

    Sub Main()
        Dim both = Combine(Of System.Action)(Sub() System.Console.Write(""a""), Sub() System.Console.Write(""b""))
        both()
        Dim twice = Combine(New System.Action(AddressOf Hello), New System.Action(AddressOf Hello))
        System.Console.Write("" "" & MethodName(Of System.Action)(AddressOf Hello) & "" "" & twice.GetInvocationList().Length)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ab Hello 2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NonDelegateTypeArgumentsAndConflictingConstraints()
            Dim compilation = CreateCompilation("
Module Program
    Function Combine(Of T As System.Delegate)(a As T, b As T) As T
        Return a
    End Function

    Function Impossible(Of T As {Structure, System.Delegate})() As T
        Return Nothing
    End Function

    Sub Main()
        Combine(Of Integer)(1, 2)
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ValueAndClassTypeConstrCombined, "System.Delegate").WithLocation(7, 45),
                Diagnostic(ERRID.ERR_GenericConstraintNotSatisfied2, "Combine(Of Integer)").WithArguments("Integer", "System.Delegate").WithLocation(12, 9))
        End Sub

        <Fact>
        Public Sub DelegateConstraintRejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Function Combine(Of T As System.Delegate)(a As T, b As T) As T
        Return a
    End Function
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ConstraintIsRestrictedType1, "System.Delegate").WithArguments("System.Delegate").WithLocation(3, 30))
        End Sub
    End Class

End Namespace
