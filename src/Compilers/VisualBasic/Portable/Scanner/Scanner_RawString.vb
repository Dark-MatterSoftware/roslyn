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
        Private _rawInterpolatedQuoteCount As Integer
        Private _rawInterpolatedMultiLine As Boolean
        Private _rawInterpolatedIndent As String
        Private _rawInterpolatedLineStart As Boolean
        Private _hasSavedRawInterpolated As Boolean
        Private _savedRawQuoteCount As Integer
        Private _savedRawMultiLine As Boolean
        Private _savedRawIndent As String
        Private _savedRawLineStart As Boolean
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

        ' $r"""text {expr}""" is an interpolated raw string. The r keeps it distinct from $"",
        ' and a single { starts a hole. {{ is a literal brace, matching ordinary interpolation.
        Private Function IsRawInterpolatedStart(offset As Integer) As Boolean
            If Not CheckFeatureAvailability(Feature.RawStringLiteral) OrElse _isScanningDirective Then
                Return False
            End If

            If Not CanGet(offset + 2) OrElse Peek(offset) <> "$"c Then
                Return False
            End If

            Dim marker = Peek(offset + 1)
            Return (marker = "r"c OrElse marker = "R"c) AndAlso CountAsciiQuotes(offset + 2) >= 3
        End Function

        Private Function MeasureRawInterpolatedStart(offset As Integer, commit As Boolean) As Integer
            Dim quoteCount = CountAsciiQuotes(offset + 2)
            Dim length = 2 + quoteCount
            Dim look = offset + length
            While CanGet(look) AndAlso IsWhitespace(Peek(look))
                look += 1
            End While

            Dim multiLine = CanGet(look) AndAlso IsNewLine(Peek(look))
            If multiLine Then
                length = (look - offset) + LengthOfLineBreak(Peek(look), look)
            End If

            If commit Then
                If _rawInterpolatedQuoteCount >= 3 AndAlso Not _hasSavedRawInterpolated Then
                    _savedRawQuoteCount = _rawInterpolatedQuoteCount
                    _savedRawMultiLine = _rawInterpolatedMultiLine
                    _savedRawIndent = _rawInterpolatedIndent
                    _savedRawLineStart = _rawInterpolatedLineStart
                    _hasSavedRawInterpolated = True
                End If

                _rawInterpolatedQuoteCount = quoteCount
                _rawInterpolatedMultiLine = multiLine
                _rawInterpolatedLineStart = multiLine
                _rawInterpolatedIndent = If(multiLine, LookAheadRawInterpolatedIndent(offset + length), "")
            End If

            Return length
        End Function

        Private Function LookAheadRawInterpolatedIndent(contentStart As Integer) As String
            Dim index = contentStart
            While CanGet(index)
                If IsNewLine(Peek(index)) Then
                    Dim afterNewLine = index + LengthOfLineBreak(Peek(index), index)
                    Dim indentEnd = afterNewLine
                    While CanGet(indentEnd) AndAlso IsWhitespace(Peek(indentEnd))
                        indentEnd += 1
                    End While

                    If CountAsciiQuotes(indentEnd) >= _rawInterpolatedQuoteCount Then
                        Return RawText(afterNewLine, indentEnd - afterNewLine)
                    End If
                End If

                index += 1
            End While

            Return ""
        End Function

        Private Function IsRawInterpolatedCloser(offset As Integer) As Boolean
            If _rawInterpolatedQuoteCount < 3 OrElse Not CanGet(offset) Then
                Return False
            End If

            If _rawInterpolatedMultiLine AndAlso IsNewLine(Peek(offset)) Then
                Dim afterNewLine = offset + LengthOfLineBreak(Peek(offset), offset)
                Dim indentEnd = afterNewLine
                While CanGet(indentEnd) AndAlso IsWhitespace(Peek(indentEnd))
                    indentEnd += 1
                End While

                Return CountAsciiQuotes(indentEnd) >= _rawInterpolatedQuoteCount
            End If

            Return Peek(offset) = """"c AndAlso CountAsciiQuotes(offset) >= _rawInterpolatedQuoteCount
        End Function

        Private Function ScanRawInterpolatedCloser(leadingTriviaLength As Integer) As SyntaxToken
            Dim quoteCount = _rawInterpolatedQuoteCount
            Dim start = leadingTriviaLength
            Dim errorId = ERRID.ERR_None
            If _rawInterpolatedMultiLine AndAlso CanGet(start) AndAlso IsNewLine(Peek(start)) Then
                start += LengthOfLineBreak(Peek(start), start)
                While CanGet(start) AndAlso IsWhitespace(Peek(start))
                    start += 1
                End While
            ElseIf _rawInterpolatedMultiLine Then
                errorId = ERRID.ERR_RawStringDelimiterOnOwnLine
            End If

            Dim quotes = CountAsciiQuotes(start)
            Dim length = (start - leadingTriviaLength) + quotes
            If quotes > quoteCount AndAlso errorId = ERRID.ERR_None Then
                errorId = ERRID.ERR_TooManyQuotesForRawString
            End If

            Dim leadingTrivia = ScanWhitespace(leadingTriviaLength)
            Dim text = GetText(length)
            ClearOrRestoreRawInterpolatedState()
            Dim followingTrivia = ScanSingleLineTrivia()
            Dim token As SyntaxToken = MakePunctuationToken(SyntaxKind.DoubleQuoteToken, text, leadingTrivia, followingTrivia)
            If errorId <> ERRID.ERR_None Then
                token = DirectCast(token.SetDiagnostics({ErrorFactory.ErrorInfo(errorId)}), SyntaxToken)
            End If

            Return token
        End Function

        Private Function ScanRawInterpolatedStringText() As SyntaxToken
            Dim offset = 0
            Dim valueBuilder = GetScratch()
            While CanGet(offset)
                If IsRawInterpolatedCloser(offset) Then
                    Exit While
                End If

                Dim ch = Peek(offset)
                If Not _rawInterpolatedMultiLine AndAlso IsNewLine(ch) Then
                    Exit While
                End If

                If IsLeftCurlyBracket(ch) OrElse IsRightCurlyBracket(ch) Then
                    If CanGet(offset + 1) AndAlso Peek(offset + 1) = ch Then
                        valueBuilder.Append(ch)
                        valueBuilder.Append(ch)
                        offset += 2
                        Continue While
                    End If

                    Exit While
                End If

                valueBuilder.Append(ch)
                offset += 1
            End While

            If offset = 0 Then
                valueBuilder.Clear()
                Return MakeEndOfInterpolatedStringToken()
            End If

            Dim text = GetTextNotInterned(offset)
            Dim rawValue = GetScratchText(valueBuilder, text)
            Dim failed = False
            Dim value = ApplyRawInterpolatedIndent(rawValue, failed)
            _rawInterpolatedLineStart = False
            Dim token As SyntaxToken = SyntaxFactory.InterpolatedStringTextToken(text, value, Nothing, Nothing)
            If failed Then
                token = DirectCast(token.SetDiagnostics({ErrorFactory.ErrorInfo(ERRID.ERR_RawStringLineIndent)}), SyntaxToken)
            End If

            Return token
        End Function

        Private Function ApplyRawInterpolatedIndent(value As String, ByRef failed As Boolean) As String
            If Not _rawInterpolatedMultiLine OrElse String.IsNullOrEmpty(_rawInterpolatedIndent) OrElse value.Length = 0 Then
                Return value
            End If

            If _rawInterpolatedLineStart Then
                Return DedentRawString(value, _rawInterpolatedIndent, failed)
            End If

            Dim split = 0
            While split < value.Length AndAlso Not IsNewLine(value(split))
                split += 1
            End While

            If split >= value.Length Then
                Return value
            End If

            split += If(value(split) = CARRIAGE_RETURN AndAlso split + 1 < value.Length AndAlso value(split + 1) = LINE_FEED, 2, 1)
            Dim tail = DedentRawString(value.Substring(split), _rawInterpolatedIndent, failed)
            Return value.Substring(0, split) & tail
        End Function

        Private Sub ClearOrRestoreRawInterpolatedState()
            If _hasSavedRawInterpolated Then
                _rawInterpolatedQuoteCount = _savedRawQuoteCount
                _rawInterpolatedMultiLine = _savedRawMultiLine
                _rawInterpolatedIndent = _savedRawIndent
                _rawInterpolatedLineStart = _savedRawLineStart
                _hasSavedRawInterpolated = False
            Else
                _rawInterpolatedQuoteCount = 0
                _rawInterpolatedMultiLine = False
                _rawInterpolatedIndent = ""
                _rawInterpolatedLineStart = False
            End If
        End Sub
    End Class
End Namespace
