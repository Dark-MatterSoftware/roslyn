' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class WriteThroughReferenceTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const Writes As String = "
Imports System
Imports System.Collections.Generic
Imports System.Runtime.InteropServices

Module Program
    Sub Increment(ByRef value As Integer)
        value += 1
    End Sub

    Sub Main()
        Dim list As New List(Of Integer) From {1, 2, 3}
        CollectionsMarshal.AsSpan(list)(0) = 5
        CollectionsMarshal.AsSpan(list)(1) += 10
        Increment(CollectionsMarshal.AsSpan(list)(2))

        Dim values As Integer() = {1, 2, 3}
        Dim whole As Span(Of Integer) = values
        whole.Slice(1)(0) = 7
        whole.Slice(1)(1) *= 3

        Console.Write($""{String.Join("","", list)} {String.Join("","", values)}"")
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub WritesThroughAByRefIndexerOfAValue()
            Dim compilation = CreateCompilation(Writes, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5,12,4 1,7,9", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub WritesThroughAByRefIndexerOfAValueInDebugBuild()
            Dim compilation = CreateCompilation(Writes, targetFramework:=TargetFramework.Net90, options:=TestOptions.DebugExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5,12,4 1,7,9", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ReadOnlyReferenceOfAValueIsStillAnError()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        MemoryExtensions.AsSpan(""ab"")(0) = ""c""c
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        MemoryExtensions.AsSpan("ab")(0) = "c"c
        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub WritingThroughAByRefIndexerOfAValueIsAnErrorOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Imports System.Runtime.InteropServices

Module Program
    Sub Main()
        Dim list As New List(Of Integer) From {1, 2, 3}
        CollectionsMarshal.AsSpan(list)(0) = 5
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        CollectionsMarshal.AsSpan(list)(0) = 5
        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
