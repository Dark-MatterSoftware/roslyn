' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.EditAndContinue
Imports Microsoft.CodeAnalysis.VisualBasic.EditAndContinue
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.EditAndContinue.UnitTests

    Public Class VbNextStateMachineInfoTests
        Private Shared ReadOnly s_vbNext As VisualBasicParseOptions = VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)

        Private Shared Function StateMachineInfoOf(source As String) As StateMachineInfo
            Dim tree = SyntaxFactory.ParseSyntaxTree(source, s_vbNext)
            tree.GetDiagnostics().Verify()
            Dim method = tree.GetRoot().DescendantNodes().OfType(Of MethodBlockSyntax)().Single()
            Return VisualBasicEditAndContinueAnalyzer.GetStateMachineInfo(method)
        End Function

        <Fact>
        Public Sub AwaitEachIsASuspensionPoint()
            Assert.Equal(New StateMachineInfo(IsAsync:=True, IsIterator:=False, HasSuspensionPoints:=True), StateMachineInfoOf("
Class C
    Async Function F() As Task
        Await Each item In Items()
        Next
    End Function
End Class"))
        End Sub

        <Fact>
        Public Sub AsyncUsingIsASuspensionPoint()
            Assert.Equal(New StateMachineInfo(IsAsync:=True, IsIterator:=False, HasSuspensionPoints:=True), StateMachineInfoOf("
Class C
    Async Function F() As Task
        Async Using r = Open()
        End Using
    End Function
End Class"))

            Assert.Equal(New StateMachineInfo(IsAsync:=True, IsIterator:=False, HasSuspensionPoints:=True), StateMachineInfoOf("
Class C
    Async Function F() As Task
        Async Using r = Open()
        Work(r)
    End Function
End Class"))
        End Sub

        <Fact>
        Public Sub AsyncIteratorIsAsyncAndAnIterator()
            Assert.Equal(New StateMachineInfo(IsAsync:=True, IsIterator:=True, HasSuspensionPoints:=True), StateMachineInfoOf("
Class C
    Async Iterator Function F() As IAsyncEnumerable(Of Integer)
        Yield 1
    End Function
End Class"))
        End Sub

        <Fact>
        Public Sub AsyncMethodWithoutSuspensionPoints()
            Assert.Equal(New StateMachineInfo(IsAsync:=True, IsIterator:=False, HasSuspensionPoints:=False), StateMachineInfoOf("
Class C
    Async Function F() As Task
        Work()
    End Function
End Class"))
        End Sub
    End Class
End Namespace
