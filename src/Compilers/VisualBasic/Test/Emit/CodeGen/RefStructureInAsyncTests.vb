' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RefStructureInAsyncTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const SpansBeforeAwait As String = "
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function SumThenWait(data As Integer()) As Task(Of Integer)
        Dim span As ReadOnlySpan(Of Integer) = data
        Dim total As Integer = 0
        For Each value As Integer In span
            total += value
        Next
        Dim first As Integer = span(0)

        Await Task.Yield()

        Dim again As Span(Of Integer) = data
        again(0) = total
        Return again(0) + first + data.AsSpan()(1) + Await Task.FromResult(0)
    End Function

    Async Function Run() As Task
        Console.Write((Await SumThenWait(New Integer() {1, 2, 3})) & "" "")

        Dim count As Func(Of String, Task(Of Integer)) =
            Async Function(text As String)
                Dim chars As ReadOnlySpan(Of Char) = text.AsSpan()
                Dim length As Integer = chars.Length
                Await Task.Yield()
                Return length
            End Function
        Console.Write(Await count(""hello""))
    End Function

    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefStructure_LocalsBeforeAwait()
            Dim compilation = CreateCompilation(SpansBeforeAwait, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="9 5", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefStructure_LocalsBeforeAwaitInDebugBuild()
            Dim compilation = CreateCompilation(SpansBeforeAwait, targetFramework:=TargetFramework.Net90, options:=TestOptions.DebugExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="9 5", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefStructure_LiveAcrossAwait()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function AfterAwait(data As Integer()) As Task(Of Integer)
        Dim span As Span(Of Integer) = data
        Await Task.Yield()
        Return span(0)
    End Function

    Async Function InLoop(data As Integer()) As Task
        For Each value As Integer In data.AsSpan()
            Await Task.Yield()
        Next
    End Function

    Async Function Spilled(data As Integer()) As Task(Of Integer)
        Return data.AsSpan().Slice(Await Task.FromResult(1)).Length
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseEmitDiagnostics(<expected><![CDATA[
BC37405: A value of type 'Span(Of Integer)' cannot be kept across an 'Await' or 'Yield', because a ref structure cannot be stored while the method is suspended.
        Return span(0)
               ~~~~
BC37405: A value of type 'Span(Of Integer).Enumerator' cannot be kept across an 'Await' or 'Yield', because a ref structure cannot be stored while the method is suspended.
        For Each value As Integer In data.AsSpan()
        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC37405: A value of type 'Span(Of Integer)' cannot be kept across an 'Await' or 'Yield', because a ref structure cannot be stored while the method is suspended.
        Return data.AsSpan().Slice(Await Task.FromResult(1)).Length
               ~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefStructure_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Threading.Tasks

Module Program
    Async Function First(data As Integer()) As Task(Of Integer)
        Dim span As Span(Of Integer) = data
        Dim value As Integer = span(0)
        Await Task.Yield()
        Return value
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30668: 'Span(Of Integer)' is obsolete: 'Types with embedded references are not supported in this version of your compiler.'.
        Dim span As Span(Of Integer) = data
                    ~~~~~~~~~~~~~~~~
BC37052: Variable of restricted type 'Span(Of Integer)' cannot be declared in an Async or Iterator method.
        Dim span As Span(Of Integer) = data
                    ~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
