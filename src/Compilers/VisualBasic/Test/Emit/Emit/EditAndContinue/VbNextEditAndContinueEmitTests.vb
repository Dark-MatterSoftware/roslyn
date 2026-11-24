' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.EditAndContinue.UnitTests
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class VbNextEditAndContinueEmitTests
        Inherits EditAndContinueTestBase

        Private Shared ReadOnly s_vbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub UpdateMethodWithVbNextLocals()
            Dim source0 = "
Imports System

Class C
    Shared Sub F()
        Dim (<N:0>x</N:0>, <N:1>y</N:1>) = (1, 2)
        Dim ((<N:2>a</N:2>, <N:3>b</N:3>), <N:4>label</N:4>) = ((3, 4), ""old"")
        Dim <N:5>text</N:5> As String = Nothing
        text ?= ""fallback""
        Dim <N:6>values</N:6> = {0, 1, 2, 3}
        Dim <N:7>middle</N:7> = values(1..^1)
        If TypeOf label Is String Into <N:8>matched</N:8> Then
            Console.WriteLine(matched)
        End If
        For Each (<N:9>key</N:9>, <N:10>value</N:10>) In {(5, 6)}
            Console.WriteLine(x + y + a + b + key + value + middle.Length + text.Length)
        Next
    End Sub
End Class
"
            Dim source1 = "
Imports System

Class C
    Shared Sub F()
        Dim (<N:0>x</N:0>, <N:1>y</N:1>) = (1, 2)
        Dim ((<N:2>a</N:2>, <N:3>b</N:3>), <N:4>label</N:4>) = ((3, 4), ""new"")
        Dim <N:5>text</N:5> As String = Nothing
        text ?= ""updated""
        Dim <N:6>values</N:6> = {0, 1, 2, 3}
        Dim <N:7>middle</N:7> = values(0..^1)
        If TypeOf label Is String Into <N:8>matched</N:8> Then
            Console.WriteLine(matched & ""!"")
        End If
        For Each (<N:9>key</N:9>, <N:10>value</N:10>) In {(5, 6)}
            Console.WriteLine(x + y + a + b + key + value + middle.Length + text.Length + 1)
        Next
    End Sub
End Class
"

            Using New EditAndContinueTest(parseOptions:=s_vbNext, targetFramework:=TargetFramework.Net90, verification:=Verification.Skipped).
                AddBaseline(source0).
                AddGeneration(
                    source1,
                    edits:=
                    {
                        Edit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)
                    },
                    validator:=
                    Sub(g)
                        g.VerifyMethodDefNames("F")
                    End Sub).
                Verify()
            End Using
        End Sub

        <Fact>
        Public Sub UpdateAsyncUsingAndAwaitEach()
            Dim source0 = "
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks

Class C
    Shared Async Function UseAsync() As Task
        Async Using <N:0>stream</N:0> As New MemoryStream()
            Await stream.WriteAsync(New Byte() {1, 2}, 0, 2)
        End Using
    End Function

    Shared Async Function ConsumeAsync(items As IAsyncEnumerable(Of (Integer, Integer))) As Task
        Await Each (<N:1>x</N:1>, <N:2>y</N:2>) In items
            Console.WriteLine(x + y)
        Next
    End Function
End Class
"
            Dim source1 = "
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks

Class C
    Shared Async Function UseAsync() As Task
        Async Using <N:0>stream</N:0> As New MemoryStream()
            Await stream.WriteAsync(New Byte() {1, 2, 3}, 0, 3)
        End Using
    End Function

    Shared Async Function ConsumeAsync(items As IAsyncEnumerable(Of (Integer, Integer))) As Task
        Await Each (<N:1>x</N:1>, <N:2>y</N:2>) In items
            Console.WriteLine(x + y + 1)
        Next
    End Function
End Class
"

            Using New EditAndContinueTest(parseOptions:=s_vbNext, targetFramework:=TargetFramework.Net90, verification:=Verification.Skipped).
                AddBaseline(source0).
                AddGeneration(
                    source1,
                    edits:=
                    {
                        Edit(SemanticEditKind.Update, Function(c) c.GetMember("C.UseAsync"), preserveLocalVariables:=True),
                        Edit(SemanticEditKind.Update, Function(c) c.GetMember("C.ConsumeAsync"), preserveLocalVariables:=True)
                    },
                    validator:=
                    Sub(g)
                        g.VerifyMethodDefNames("UseAsync", "ConsumeAsync", "MoveNext", "MoveNext", ".ctor")
                    End Sub).
                Verify()
            End Using
        End Sub
    End Class
End Namespace
