' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class LocalRewriter

        ''' <summary>
        ''' A Finally that awaits cannot stay in its handler, because the state machine cannot resume inside one.
        ''' The Try and its Catch blocks become the body that Async Using releases after, and the Finally becomes
        ''' the release:
        ''' 
        '''      Try
        '''          Try ... Catch ... End Try, where each jump out is "pending = n : GoTo afterTry"
        '''      Catch ex As Exception
        '''          pendingException = ex
        '''      End Try
        '''      afterTry:
        '''      finally body
        '''      If pendingException IsNot Nothing Then ExceptionDispatchInfo.Capture(pendingException).Throw()
        '''      If pending = n Then the original jump
        '''      exitLabel:
        ''' </summary>
        Private Function RewriteTryWithAwaitInFinally(node As BoundTryStatement) As BoundStatement
            Dim syntax = node.Syntax

            Dim protectedRegion = RewriteTryBlock(node)
            Dim rewrittenCatchBlocks = VisitList(node.CatchBlocks)
            If Not rewrittenCatchBlocks.IsEmpty Then
                ' Exit Try leaves through the Finally, so the inner Try does not place the exit label.
                Dim inner = RewriteTryStatement(syntax, protectedRegion, rewrittenCatchBlocks, finallyBlockOpt:=Nothing, exitLabelOpt:=Nothing)
                protectedRegion = New BoundBlock(syntax, Nothing, ImmutableArray(Of LocalSymbol).Empty, ImmutableArray.Create(inner))
            End If

            Dim rewrittenFinally = RewriteFinallyBlock(node)
            Dim prologue = New BoundStatementList(syntax, ImmutableArray(Of BoundStatement).Empty)
            Dim rewritten As BoundStatement = RewriteWithAsyncDispose(syntax, prologue, protectedRegion, rewrittenFinally)

            If node.ExitLabelOpt IsNot Nothing Then
                rewritten = New BoundStatementList(syntax, ImmutableArray.Create(Of BoundStatement)(rewritten, New BoundLabelStatement(syntax, node.ExitLabelOpt)))
            End If

            If Me.Instrument(node) AndAlso TypeOf node.Syntax Is Syntax.TryBlockSyntax Then
                rewritten = _instrumenterOpt.InstrumentTryStatement(node, rewritten)
            End If

            Return rewritten
        End Function
    End Class
End Namespace
