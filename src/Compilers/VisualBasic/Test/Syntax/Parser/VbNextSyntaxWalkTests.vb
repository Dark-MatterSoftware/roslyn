' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

''' <summary>
''' Each child of a Vb.Next form can be reached by index, as syntax walkers reach it.
''' </summary>
Public Class VbNextSyntaxWalkTests

    Private Shared ReadOnly s_vbNext As VisualBasicParseOptions = VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)

    <Theory>
    <InlineData("For Each ((name, count As Long), ) In items : Next")>
    <InlineData("For Each ((name, count), ) In items : Next")>
    <InlineData("For Each ((name, count As Long), other) In items : Next")>
    <InlineData("For Each (name, count As Long) In items : Next")>
    <InlineData("For Each (count As Long, ) In items : Next")>
    <InlineData("For Each (item, price) In prices : Next")>
    <InlineData("For Each (, price) In prices : Next")>
    <InlineData("Dim ((a, b), c) = value")>
    <InlineData("Dim (best, ) = value")>
    <InlineData("((x.A, x.B), y(1)) = value")>
    Public Sub ChildrenOfDeconstructions(statement As String)
        Dim tree = VisualBasicSyntaxTree.ParseText("
Module M
    Sub F()
        " & statement & "
    End Sub
End Module", s_vbNext)

        Dim walker As New CountingWalker()
        walker.Visit(tree.GetRoot())
        Assert.True(walker.Nodes > 0)

        For Each node In tree.GetRoot().DescendantNodesAndSelf()
            Dim children = node.ChildNodesAndTokens()
            Dim enumerated = children.ToList()
            Assert.Equal(enumerated.Count, children.Count)
            For i = 0 To children.Count - 1
                Assert.Equal(enumerated(i), children(i))
            Next
        Next
    End Sub

    Private NotInheritable Class CountingWalker
        Inherits VisualBasicSyntaxWalker

        Public Nodes As Integer

        Public Sub New()
            MyBase.New(SyntaxWalkerDepth.Token)
        End Sub

        Public Overrides Sub Visit(node As SyntaxNode)
            Nodes += 1
            MyBase.Visit(node)
        End Sub
    End Class
End Class
