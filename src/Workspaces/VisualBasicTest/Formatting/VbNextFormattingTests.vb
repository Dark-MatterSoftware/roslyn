' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.Text
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Formatting
    <Trait(Traits.Feature, Traits.Features.Formatting)>
    Public Class VbNextFormattingTests
        Inherits VisualBasicFormattingTestBase

        Private Shared ReadOnly s_vbNext As VisualBasicParseOptions = VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)

        Private Function AssertVbNextFormatAsync(code As String, expected As String) As Task
            Return AssertFormatAsync(expected, code, {New TextSpan(0, code.Length)}, LanguageNames.VisualBasic, parseOptions:=s_vbNext)
        End Function

        <Fact>
        Public Async Function IndexesFromTheEndAndRangesHaveNoSpaces() As Task
            Await AssertVbNextFormatAsync("
Class C
Sub F(items As Integer(), n As Integer)
Dim last = items( ^ 1)
Dim fromEnd = items(^ n)
Dim mid = items(1 .. 2)
Dim head = items( .. 2)
Dim tail = items(3 .. )
Dim inner = items(1 .. ^ 1)
Dim value As System.Range = (1 .. n)
Dim power = n ^ 2
Dim all As Integer() = {.. items, 3}
Dim tight As Integer() = {..items, 3}
Dim spread As Integer() = (1, .. items)
End Sub
End Class
", "
Class C
    Sub F(items As Integer(), n As Integer)
        Dim last = items(^1)
        Dim fromEnd = items(^n)
        Dim mid = items(1..2)
        Dim head = items(..2)
        Dim tail = items(3..)
        Dim inner = items(1..^1)
        Dim value As System.Range = (1..n)
        Dim power = n ^ 2
        Dim all As Integer() = {.. items, 3}
        Dim tight As Integer() = {..items, 3}
        Dim spread As Integer() = (1, .. items)
    End Sub
End Class
")
        End Function

        <Fact>
        Public Async Function AsynchronousStreamsAndPatternVariablesIndentTheirBodies() As Task
            Await AssertVbNextFormatAsync("
Class C
Async Iterator Function Items() As System.Collections.Generic.IAsyncEnumerable(Of Integer)
Async Using   r = Open()
Await Each   n In Source()
If TypeOf n Is Integer   Into i Then
Yield i
End If
Next
End Using
End Function
Property Name As String
Get
Return   Field
End Get
Set
Select Case Value
Case Is TypeOf String   Into s
Field = s
End Select
End Set
End Property
End Class
", "
Class C
    Async Iterator Function Items() As System.Collections.Generic.IAsyncEnumerable(Of Integer)
        Async Using r = Open()
            Await Each n In Source()
                If TypeOf n Is Integer Into i Then
                    Yield i
                End If
            Next
        End Using
    End Function
    Property Name As String
        Get
            Return Field
        End Get
        Set
            Select Case Value
                Case Is TypeOf String Into s
                    Field = s
            End Select
        End Set
    End Property
End Class
")
        End Function

        <Fact>
        Public Async Function ExtensionBlockIndentsItsMembers() As Task
            Await AssertVbNextFormatAsync("
Module M
Extension    (s As String)
Public ReadOnly Property WordCount As Integer
Get
Return s.Length
End Get
End Property

Public Shared Function Twice(value As String) As String
Return value & value
End Function
End Extension
End Module
", "
Module M
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property

        Public Shared Function Twice(value As String) As String
            Return value & value
        End Function
    End Extension
End Module
")
        End Function

        <Fact>
        Public Async Function EmptyExtensionBlock() As Task
            Await AssertVbNextFormatAsync("
Module M
        Extension(s As String)
            End Extension
End Module
", "
Module M
    Extension(s As String)
    End Extension
End Module
")
        End Function

        <Fact>
        Public Async Function SelectCaseExpressionIndentsItsArms() As Task
            Await AssertVbNextFormatAsync("
Module M
    Sub Main(order As Integer)
        Dim label = Select Case order
Case 1
""one""
Case Else
""other""
End Select
    End Sub
End Module
", "
Module M
    Sub Main(order As Integer)
        Dim label = Select Case order
                        Case 1
                            ""one""
                        Case Else
                            ""other""
                    End Select
    End Sub
End Module
")
        End Function

        <Fact>
        Public Async Function SelectExpressionWithoutCaseKeyword() As Task
            Await AssertVbNextFormatAsync("
Module M
    Sub Main(order As Integer)
        Dim label = Select order
        Case Else
        ""other""
        End Select
    End Sub
End Module
", "
Module M
    Sub Main(order As Integer)
        Dim label = Select order
                        Case Else
                            ""other""
                    End Select
    End Sub
End Module
")
        End Function

        <Fact>
        Public Async Function SelectCaseExpressionInsideStatementSelectCase() As Task
            Await AssertVbNextFormatAsync("
Module M
    Sub Main(order As Integer)
        Select Case order
        Case 1
        Dim label = Select Case order
        Case 1
        ""one""
        Case Else
        ""other""
        End Select
        End Select
    End Sub
End Module
", "
Module M
    Sub Main(order As Integer)
        Select Case order
            Case 1
                Dim label = Select Case order
                                Case 1
                                    ""one""
                                Case Else
                                    ""other""
                            End Select
        End Select
    End Sub
End Module
")
        End Function

        <Fact>
        Public Async Function StatementsAfterUsingDeclarationStayAtItsLevel() As Task
            Await AssertVbNextFormatAsync("
Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
            stream.WriteByte(1)
                stream.WriteByte(2)
    End Sub
End Class
", "
Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
        stream.WriteByte(1)
        stream.WriteByte(2)
    End Sub
End Class
")
        End Function

        <Fact>
        Public Async Function NamespaceToEndOfFileDoesNotIndentMembers() As Task
            Await AssertVbNextFormatAsync("
Namespace N
    Class C
        Sub M()
        End Sub
    End Class
", "
Namespace N
Class C
    Sub M()
    End Sub
End Class
")
        End Function

        <Fact>
        Public Async Function XmlStatementBlockIndentsFromItsFirstStatement() As Task
            Await AssertVbNextFormatAsync("
Module M
    Function Items(names As String()) As System.Xml.Linq.XElement
        Return <ul>
                   <%= For Each name In names
                   Yield <li><%= name %></li>
                   Next %>
               </ul>
    End Function
End Module
", "
Module M
    Function Items(names As String()) As System.Xml.Linq.XElement
        Return <ul>
                   <%= For Each name In names
                           Yield <li><%= name %></li>
                       Next %>
               </ul>
    End Function
End Module
")
        End Function

        <Fact>
        Public Async Function RecordClassIndentsLikeClass() As Task
            Await AssertVbNextFormatAsync("
Record Class Point(X As Integer, Y As Integer)
Function Total() As Integer
Return X + Y
End Function
End Class
", "
Record Class Point(X As Integer, Y As Integer)
    Function Total() As Integer
        Return X + Y
    End Function
End Class
")
        End Function
    End Class
End Namespace
