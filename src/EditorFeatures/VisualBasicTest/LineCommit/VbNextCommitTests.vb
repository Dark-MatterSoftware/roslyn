' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Editor.UnitTests.Utilities

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.LineCommit
    <[UseExportProvider]>
    <Trait(Traits.Feature, Traits.Features.LineCommit)>
    Public Class VbNextCommitTests
        <WpfFact>
        Public Sub CompactFunctionIsPrettyListed()
            AssertCommit(
"Class C
    function Area(width as integer, height as integer) as integer = return width*heigh$$
    Function Other() As Integer
        Return 1
    End Function
End Class",
                insertion:="t",
"Class C
    Function Area(width As Integer, height As Integer) As Integer = Return width * height
    Function Other() As Integer
        Return 1
    End Function
End Class")
        End Sub

        <WpfFact>
        Public Sub CompactPropertyAndSubArePrettyListed()
            AssertCommit(
"Class C
    readonly property Size as integer = get 4
    sub Log(message as string) = call System.Console.WriteLine(messag$$)
End Class",
                insertion:="e",
"Class C
    readonly property Size as integer = get 4
    Sub Log(message As String) = Call System.Console.WriteLine(message)
End Class")
        End Sub

        <WpfFact>
        Public Sub MemberAfterCompactMemberKeepsItsIndentation()
            AssertCommit(
"Class C
    Function Area() As Integer = Return 1
    private coun$$ as integer
End Class",
                insertion:="t",
"Class C
    Function Area() As Integer = Return 1
    Private count As Integer
End Class")
        End Sub

        <WpfFact>
        Public Sub UsingDeclarationIsPrettyListed()
            AssertCommit(
"Class C
    Sub M()
        using stream = new System.IO.MemoryStrea$$
        stream.WriteByte(1)
    End Sub
End Class",
                insertion:="m()",
"Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
        stream.WriteByte(1)
    End Sub
End Class")
        End Sub

        <WpfFact>
        Public Sub StatementAfterUsingDeclarationKeepsItsIndentation()
            AssertCommit(
"Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
        stream.writebyte(1$$
        stream.WriteByte(2)
    End Sub
End Class",
                insertion:=")",
"Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
        stream.WriteByte(1)
        stream.WriteByte(2)
    End Sub
End Class")
        End Sub

        <WpfFact>
        Public Sub UsingBlockStillIndents()
            AssertCommit(
"Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
        stream.writebyte(1$$
        End Using
    End Sub
End Class",
                insertion:=")",
"Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
            stream.WriteByte(1)
        End Using
    End Sub
End Class")
        End Sub

        <WpfFact>
        Public Sub UsingWithoutEndStillIndentsOn17_13()
            AssertCommit(
"Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
        stream.writebyte(1$$
    End Sub
End Class",
                insertion:=")",
"Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
            stream.WriteByte(1)
    End Sub
End Class",
                languageVersion:="17.13")
        End Sub

        <WpfFact>
        Public Sub MemberOfNamespaceToEndOfFileKeepsItsIndentation()
            AssertCommit(
"Namespace N

clas$$ C
End Class",
                insertion:="s",
"Namespace N

Class C
End Class")
        End Sub

        <WpfFact>
        Public Sub XmlStatementBlockIsLeftAlone()
            AssertCommit(
"Module M
    Function Items(names As String()) As System.Xml.Linq.XElement
        Return <ul>
                   <%= For Each name In names
                           Yield <li><%= nam$$ %></li>
                       Next %>
               </ul>
    End Function
End Module",
                insertion:="e",
"Module M
    Function Items(names As String()) As System.Xml.Linq.XElement
        Return <ul>
                   <%= For Each name In names
                           Yield <li><%= name %></li>
                       Next %>
               </ul>
    End Function
End Module")
        End Sub

        <WpfFact>
        Public Sub XmlStatementBlockHeaderIsLeftAlone()
            AssertCommit(
"Module M
    Function Item(count As Integer) As System.Xml.Linq.XElement
        Return <ul>
                   <%= Select Case coun$$
                           Case 1
                               Return <li>One Item</li>
                           Case Else
                               Return <li>Many Items</li>
                       End Select %>
               </ul>
    End Function
End Module",
                insertion:="t",
"Module M
    Function Item(count As Integer) As System.Xml.Linq.XElement
        Return <ul>
                   <%= Select Case count
                           Case 1
                               Return <li>One Item</li>
                           Case Else
                               Return <li>Many Items</li>
                       End Select %>
               </ul>
    End Function
End Module")
        End Sub

        Private Shared Sub AssertCommit(markup As String, insertion As String, expected As String, Optional languageVersion As String = "vbnext")
            Using testData = CommitTestData.Create(
                <Workspace>
                    <Project Language="Visual Basic" CommonReferences="true" LanguageVersion=<%= languageVersion %>>
                        <Document><%= markup.Replace(vbCrLf, vbLf) %></Document>
                    </Project>
                </Workspace>)

                testData.EditorOperations.InsertText(insertion)
                testData.EditorOperations.MoveLineDown(extendSelection:=False)

                Assert.Equal(expected.Replace(vbCrLf, vbLf), testData.Buffer.CurrentSnapshot.GetText().Replace(vbCrLf, vbLf))
            End Using
        End Sub
    End Class
End Namespace
