' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Option Compare Binary
Option Strict On

Imports System.Text
Imports Microsoft.CodeAnalysis.VisualBasic.SyntaxFacts
Imports CoreInternalSyntax = Microsoft.CodeAnalysis.Syntax.InternalSyntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Syntax.InternalSyntax
    Partial Friend Class Scanner
        ' r"""text""" is a raw string. The r prefix keeps """text""" meaning a normal string
        ' whose value contains a quote. Quotes inside the raw string are characters, and a
        ' multi-line form drops the whitespace that precedes the closing quotes.
        Private Function IsRawStringStart() As Boolean
            If _isScanningDirective OrElse Not CheckFeatureAvailability(Feature.RawStringLiteral) Then
                Return False
            End If

            Dim ch = Peek()
            If ch <> "r"c AndAlso ch <> "R"c Then
                Return False
            End If

            Return CountAsciiQuotes(1) >= 3
        End Function

        Private Function ScanRawStringLiteral(precedingTrivia As CoreInternalSyntax.SyntaxList(Of VisualBasicSyntaxNode)) As SyntaxToken
            Dim quoteCount = CountAsciiQuotes(1)
            Debug.Assert(quoteCount >= 3)

            Dim openQuoteEnd = 1 + quoteCount
            Dim look = openQuoteEnd
            While CanGet(look) AndAlso IsWhitespace(Peek(look))
                look += 1
            End While

            Dim length As Integer = 0
            Dim value As String = Nothing
            Dim errorId As ERRID = ERRID.ERR_None
            If CanGet(look) AndAlso IsNewLine(Peek(look)) Then
                ScanMultiLineRawString(quoteCount, look + LengthOfLineBreak(Peek(look), look), length, value, errorId)
            Else
                ScanSingleLineRawString(quoteCount, openQuoteEnd, length, value, errorId)
            End If

            Dim spelling = GetTextNotInterned(length)
            Dim followingTrivia = ScanSingleLineTrivia()
            Dim token As SyntaxToken = SyntaxFactory.StringLiteralToken(spelling, value, precedingTrivia.Node, followingTrivia.Node)
            If errorId <> ERRID.ERR_None Then
                token = DirectCast(token.SetDiagnostics({ErrorFactory.ErrorInfo(errorId)}), SyntaxToken)
            End If

            Return token
        End Function

        Private Sub ScanSingleLineRawString(quoteCount As Integer, contentStart As Integer, ByRef length As Integer, ByRef value As String, ByRef errorId As ERRID)
            Dim index = contentStart
            While CanGet(index)
                Dim ch = Peek(index)
                If IsNewLine(ch) Then
                    length = index
                    value = RawText(contentStart, index - contentStart)
                    errorId = ERRID.ERR_UnterminatedStringLiteral
                    Return
                End If

                If ch = """"c Then
                    Dim quotes = CountAsciiQuotes(index)
                    If quotes >= quoteCount Then
                        value = RawText(contentStart, index - contentStart)
                        length = index + quotes
                        If quotes > quoteCount Then
                            errorId = ERRID.ERR_TooManyQuotesForRawString
                        End If
                        Return
                    End If

                    index += quotes
                Else
                    index += 1
                End If
            End While

            length = index
            value = RawText(contentStart, index - contentStart)
            errorId = ERRID.ERR_UnterminatedStringLiteral
        End Sub

        Private Sub ScanMultiLineRawString(quoteCount As Integer, contentStart As Integer, ByRef length As Integer, ByRef value As String, ByRef errorId As ERRID)
            Dim closing = contentStart
            While CanGet(closing) AndAlso IsWhitespace(Peek(closing))
                closing += 1
            End While

            Dim openingQuotes = CountAsciiQuotes(closing)
            If openingQuotes >= quoteCount Then
                value = ""
                length = closing + openingQuotes
                If openingQuotes > quoteCount Then
                    errorId = ERRID.ERR_TooManyQuotesForRawString
                End If
                Return
            End If

            Dim index = contentStart
            While CanGet(index)
                Dim ch = Peek(index)
                If IsNewLine(ch) Then
                    Dim afterNewLine = index + LengthOfLineBreak(ch, index)
                    Dim indentEnd = afterNewLine
                    While CanGet(indentEnd) AndAlso IsWhitespace(Peek(indentEnd))
                        indentEnd += 1
                    End While

                    Dim quotes = CountAsciiQuotes(indentEnd)
                    If quotes >= quoteCount Then
                        Dim content = RawText(contentStart, index - contentStart)
                        Dim indent = RawText(afterNewLine, indentEnd - afterNewLine)
                        Dim failed = False
                        value = DedentRawString(content, indent, failed)
                        length = indentEnd + quotes
                        If failed Then
                            errorId = ERRID.ERR_RawStringLineIndent
                        ElseIf quotes > quoteCount Then
                            errorId = ERRID.ERR_TooManyQuotesForRawString
                        End If
                        Return
                    End If

                    index += 1
                ElseIf ch = """"c Then
                    Dim quotes = CountAsciiQuotes(index)
                    If quotes >= quoteCount Then
                        value = RawText(contentStart, index - contentStart)
                        length = index + quotes
                        errorId = ERRID.ERR_RawStringDelimiterOnOwnLine
                        Return
                    End If

                    index += quotes
                Else
                    index += 1
                End If
            End While

            value = RawText(contentStart, index - contentStart)
            length = index
            errorId = ERRID.ERR_UnterminatedStringLiteral
        End Sub

        Private Function CountAsciiQuotes(start As Integer) As Integer
            Dim count = 0
            While CanGet(start + count) AndAlso Peek(start + count) = """"c
                count += 1
            End While
            Return count
        End Function

        Private Function RawText(start As Integer, length As Integer) As String
            If length = 0 Then
                Return ""
            End If

            Return GetTextNotInterned(_lineBufferOffset + start, length)
        End Function

        Private Function DedentRawString(content As String, indent As String, ByRef failed As Boolean) As String
            If indent.Length = 0 OrElse content.Length = 0 Then
                Return content
            End If

            Dim result = GetScratch()
            Dim index = 0
            While index < content.Length
                Dim lineStart = index
                While index < content.Length AndAlso IsWhitespace(content(index)) AndAlso Not IsNewLine(content(index))
                    index += 1
                End While

                Dim lineWhitespace = content.Substring(lineStart, index - lineStart)
                Dim blankLine = index >= content.Length OrElse IsNewLine(content(index))
                If lineWhitespace.StartsWith(indent, StringComparison.Ordinal) Then
                    index = lineStart + indent.Length
                ElseIf blankLine AndAlso indent.StartsWith(lineWhitespace, StringComparison.Ordinal) Then
                    index = index
                Else
                    failed = True
                    result.Clear()
                    Return content
                End If

                While index < content.Length
                    Dim ch = content(index)
                    result.Append(ch)
                    index += 1
                    If ch = CARRIAGE_RETURN Then
                        If index < content.Length AndAlso content(index) = LINE_FEED Then
                            result.Append(content(index))
                            index += 1
                        End If
                        Exit While
                    ElseIf IsNewLine(ch) Then
                        Exit While
                    End If
                End While
            End While

            Return GetScratchText(result)
        End Function
    End Class
End Namespace
