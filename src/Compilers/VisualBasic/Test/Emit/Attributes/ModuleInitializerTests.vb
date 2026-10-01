' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ModuleInitializerTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ModuleInitializer_RunsBeforeMain()
            Dim compilation = CreateCompilationWithMscorlib40("
Namespace System.Runtime.CompilerServices
    Public Class ModuleInitializerAttribute
        Inherits System.Attribute
    End Class
End Namespace

Class Program
    <System.Runtime.CompilerServices.ModuleInitializer>
    Public Shared Sub Init()
        System.Console.Write(""init"")
    End Sub

    Public Shared Sub Main()
        System.Console.Write(""main"")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="initmain").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ModuleInitializer_RunsInSourceOrder()
            Dim compilation = CreateCompilationWithMscorlib40("
Namespace System.Runtime.CompilerServices
    Public Class ModuleInitializerAttribute
        Inherits System.Attribute
    End Class
End Namespace

Class Program
    <System.Runtime.CompilerServices.ModuleInitializer>
    Public Shared Sub InitB()
        System.Console.Write(""B"")
    End Sub

    <System.Runtime.CompilerServices.ModuleInitializer>
    Public Shared Sub InitA()
        System.Console.Write(""A"")
    End Sub

    Public Shared Sub Main()
        System.Console.Write(""main"")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="BAmain").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ModuleInitializer_RejectedOn17_13AndDoesNotRun()
            Dim compilation = CreateCompilationWithMscorlib40("
Namespace System.Runtime.CompilerServices
    Public Class ModuleInitializerAttribute
        Inherits System.Attribute
    End Class
End Namespace

Class Program
    <System.Runtime.CompilerServices.ModuleInitializer>
    Public Shared Sub Init()
        System.Console.Write(""init"")
    End Sub

    Public Shared Sub Main()
        System.Console.Write(""main"")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=Vb17_13)

            CompileAndVerify(compilation, expectedOutput:="main").VerifyDiagnostics(
                Diagnostic(ERRID.WRN_AttributeNotSupportedInVB, "System.Runtime.CompilerServices.ModuleInitializer").WithArguments("System.Runtime.CompilerServices.ModuleInitializerAttribute").WithLocation(9, 6))
        End Sub

        <Fact>
        Public Sub ModuleInitializer_InvalidShapes()
            Dim compilation = CreateCompilationWithMscorlib40("
Imports System.Runtime.CompilerServices

Namespace System.Runtime.CompilerServices
    Public Class ModuleInitializerAttribute
        Inherits System.Attribute
    End Class
End Namespace

Class Program
    <ModuleInitializer>
    Public Sub InstanceMethod()
    End Sub

    <ModuleInitializer>
    Public Shared Function ReturnsValue() As Integer
        Return 0
    End Function

    <ModuleInitializer>
    Private Shared Sub Hidden()
    End Sub

    <ModuleInitializer>
    Public Shared Sub TakesArgument(value As Integer)
    End Sub

    <ModuleInitializer>
    Public Shared Sub GenericMethod(Of T)()
    End Sub
End Class

Class Holder(Of T)
    <ModuleInitializer>
    Public Shared Sub InGeneric()
    End Sub
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ModuleInitializerMethodMustBeSharedParameterlessSub, "ModuleInitializer").WithArguments("InstanceMethod").WithLocation(11, 6),
                Diagnostic(ERRID.ERR_ModuleInitializerMethodMustBeSharedParameterlessSub, "ModuleInitializer").WithArguments("ReturnsValue").WithLocation(15, 6),
                Diagnostic(ERRID.ERR_ModuleInitializerMethodMustBeAccessible, "ModuleInitializer").WithArguments("Hidden").WithLocation(20, 6),
                Diagnostic(ERRID.ERR_ModuleInitializerMethodMustBeSharedParameterlessSub, "ModuleInitializer").WithArguments("TakesArgument").WithLocation(24, 6),
                Diagnostic(ERRID.ERR_ModuleInitializerMethodMustNotBeGeneric, "ModuleInitializer").WithArguments("GenericMethod").WithLocation(28, 6),
                Diagnostic(ERRID.ERR_ModuleInitializerMethodMustNotBeGeneric, "ModuleInitializer").WithArguments("InGeneric").WithLocation(34, 6))
        End Sub

    End Class

End Namespace
