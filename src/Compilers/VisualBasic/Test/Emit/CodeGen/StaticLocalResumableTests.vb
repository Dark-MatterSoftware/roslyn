' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class StaticLocalResumableTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub AsyncFunction_KeepsValueAcrossCallsAndAwaits()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Module Program
    Function Seed() As Integer
        System.Console.Write(""seed "")
        Return 10
    End Function

    Async Function NextId() As Task(Of Integer)
        Static last As Integer = Seed()
        Await Task.Yield()
        last += 1
        Await Task.Yield()
        Return last
    End Function

    Sub Main()
        System.Console.Write(NextId().Result & "" "")
        System.Console.Write(NextId().Result & "" "")
        System.Console.Write(NextId().Result)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="seed 11 12 13").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub IteratorFunction_KeepsValueAcrossEnumerations()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Iterator Function Numbers() As IEnumerable(Of Integer)
        Static runs As Integer
        runs += 1
        Yield runs
        Yield runs * 10
    End Function

    Sub Main()
        For Each n In Numbers()
            System.Console.Write(n & "" "")
        Next
        For Each n In Numbers()
            System.Console.Write(n & "" "")
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1 10 2 20 ").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AsyncInstanceMethod_KeepsOneValuePerInstance()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Class Counter
    Async Function Increment() As Task(Of Integer)
        Static count As Integer
        Await Task.Yield()
        count += 1
        Return count
    End Function
End Class

Module Program
    Sub Main()
        Dim a As New Counter()
        Dim b As New Counter()
        a.Increment().Wait()
        System.Console.Write(a.Increment().Result & "" "" & b.Increment().Result)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2 1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AsyncIteratorFunction_DeclaresStaticLocal()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Async Iterator Function Ticks() As IAsyncEnumerable(Of Integer)
        Static total As Integer
        Await Task.Yield()
        total += 1
        Yield total
    End Function
End Module
", targetFramework:=TargetFramework.Net90, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AwaitInInitializer_IsAnError()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Threading.Tasks

Class Box
    Public Sub New(value As Integer)
    End Sub
End Class

Module Program
    Async Function Load() As Task(Of Integer)
        Await Task.Yield()
        Return 1
    End Function

    Async Function Uses() As Task
        Static first As Integer = Await Load()
        Static second As New Box(Await Load())
        Static later As Func(Of Task(Of Integer)) = Async Function() Await Load()
        Await Task.Yield()
    End Function
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AwaitInStaticLocalInitializer, "Await Load()").WithLocation(17, 35),
                Diagnostic(ERRID.ERR_AwaitInStaticLocalInitializer, "Await Load()").WithLocation(18, 34))
        End Sub

        <Fact>
        Public Sub LambdaAndGenericMethod_StillReportTheirErrors()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function Generic(Of T)() As Task
        Static count As Integer
        Await Task.Yield()
    End Function

    Sub Uses()
        Dim f As Func(Of Task) = Async Function()
                                     Static count As Integer
                                     Await Task.Yield()
                                 End Function
    End Sub
End Module
", parseOptions:=VbNext)

            ' The lambda's body binds twice, so its error is reported twice, as on Visual Basic 17.13.
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadStaticLocalInGenericMethod, "Static").WithLocation(7, 9),
                Diagnostic(ERRID.WRN_UnusedLocal, "count").WithArguments("count").WithLocation(7, 16),
                Diagnostic(ERRID.ERR_StaticInLambda, "Static").WithLocation(13, 38),
                Diagnostic(ERRID.ERR_StaticInLambda, "Static").WithLocation(13, 38),
                Diagnostic(ERRID.WRN_UnusedLocal, "count").WithArguments("count").WithLocation(13, 45),
                Diagnostic(ERRID.WRN_DefAsgNoRetValFuncRef1, "End Function").WithArguments("<anonymous method>").WithLocation(15, 34))
        End Sub

        <Fact>
        Public Sub AsyncFunction_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Module Program
    Async Function NextId() As Task(Of Integer)
        Static last As Integer
        Await Task.Yield()
        last += 1
        Return last
    End Function
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadStaticInitializerInResumable, "last").WithLocation(6, 16))
        End Sub
    End Class
End Namespace
