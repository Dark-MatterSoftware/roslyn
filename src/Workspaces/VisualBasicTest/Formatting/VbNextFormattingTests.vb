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
