' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class UnmanagedCallersOnlyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub UnmanagedCallersOnly_AcceptsSharedMethodAndCallingConvention()
            Dim compilation = CreateCompilation("
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Class Program
    <UnmanagedCallersOnly>
    Public Shared Sub Plain()
    End Sub

    <UnmanagedCallersOnly(CallConvs:=New System.Type() { GetType(CallConvCdecl) })>
    Public Shared Function WithConvention(value As Integer) As Integer
        Return value
    End Function
End Class
", targetFramework:=TargetFramework.Net50, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub UnmanagedCallersOnly_RejectsInvalidCallingConventionAndSignature()
            Dim compilation = CreateCompilation("
Imports System.Runtime.InteropServices
Class Program
    <UnmanagedCallersOnly(CallConvs:=New System.Type() { GetType(String) })>
    Public Shared Sub BadConvention()
    End Sub

    <UnmanagedCallersOnly>
    Public Sub InstanceMethod()
    End Sub

    <UnmanagedCallersOnly>
    Public Shared Sub TakesString(value As String)
    End Sub

    <UnmanagedCallersOnly>
    Public Shared Sub TakesByRef(ByRef value As Integer)
    End Sub
End Class

Class Holder(Of T)
    <UnmanagedCallersOnly>
    Public Shared Sub InGeneric()
    End Sub
End Class
", targetFramework:=TargetFramework.Net50, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_InvalidUnmanagedCallersOnlyCallConv, "UnmanagedCallersOnly(CallConvs:=New System.Type() { GetType(String) })").WithArguments("String").WithLocation(4, 6),
                Diagnostic(ERRID.ERR_UnmanagedCallersOnlyRequiresShared, "UnmanagedCallersOnly").WithLocation(8, 6),
                Diagnostic(ERRID.ERR_CannotUseManagedTypeInUnmanagedCallersOnly, "value").WithArguments("String", "parameter").WithLocation(13, 35),
                Diagnostic(ERRID.ERR_CannotUseByRefInUnmanagedCallersOnly, "value").WithLocation(17, 40),
                Diagnostic(ERRID.ERR_UnmanagedCallersOnlyMethodOrTypeCannotBeGeneric, "UnmanagedCallersOnly").WithLocation(22, 6))
        End Sub

        <Fact>
        Public Sub UnmanagedCallersOnly_CannotBeCalledOrUsedWithAddressOf()
            Dim compilation = CreateCompilation("
Imports System.Runtime.InteropServices
Class Program
    <UnmanagedCallersOnly>
    Public Shared Sub Exported()
    End Sub

    Public Shared Sub CallSite()
        Exported()
        Dim callback As System.Action = AddressOf Exported
    End Sub
End Class
", targetFramework:=TargetFramework.Net50, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_UnmanagedCallersOnlyMethodsCannotBeCalledDirectly, "Exported()").WithArguments("Exported").WithLocation(9, 9),
                Diagnostic(ERRID.ERR_UnmanagedCallersOnlyMethodsCannotBeCalledDirectly, "Exported").WithArguments("Exported").WithLocation(10, 51))
        End Sub

        <Fact>
        Public Sub UnmanagedCallersOnly_CannotBeEntryPointOrModuleInitializer()
            Dim compilation = CreateCompilation("
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Class Program
    <UnmanagedCallersOnly>
    <ModuleInitializer>
    Public Shared Sub Init()
    End Sub

    <UnmanagedCallersOnly>
    Public Shared Sub Main()
    End Sub
End Class
", options:=TestOptions.ReleaseExe, targetFramework:=TargetFramework.Net50, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ModuleInitializerCannotBeUnmanagedCallersOnly, "ModuleInitializer").WithLocation(6, 6),
                Diagnostic(ERRID.ERR_EntryPointCannotBeUnmanagedCallersOnly, "Main").WithLocation(11, 23))
        End Sub

    End Class

End Namespace
