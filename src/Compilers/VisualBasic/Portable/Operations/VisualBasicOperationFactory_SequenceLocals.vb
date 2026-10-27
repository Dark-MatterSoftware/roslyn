' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.Operations
    Partial Friend NotInheritable Class VisualBasicOperationFactory

        ''' <summary>
        ''' vbnext binds some expressions, such as a Select Case expression, to a sequence with temporaries.
        ''' The operation tree has no expression that declares locals, so the enclosing block declares them.
        ''' Nested blocks and lambdas declare the temporaries of their own sequences.
        ''' </summary>
        Private NotInheritable Class SequenceLocalsCollector
            Inherits BoundTreeWalkerWithStackGuard

            Private ReadOnly _locals As ArrayBuilder(Of LocalSymbol)

            Private Sub New(locals As ArrayBuilder(Of LocalSymbol))
                _locals = locals
            End Sub

            Public Shared Function GetBlockLocals(block As BoundBlock) As ImmutableArray(Of LocalSymbol)
                Dim options = TryCast(block.Syntax.SyntaxTree?.Options, VisualBasicParseOptions)
                If options Is Nothing OrElse options.LanguageVersion < LanguageVersion.VbNext Then
                    Return block.Locals
                End If

                Dim locals = ArrayBuilder(Of LocalSymbol).GetInstance()
                locals.AddRange(block.Locals)
                Dim collector As New SequenceLocalsCollector(locals)
                For Each statement In block.Statements
                    collector.Visit(statement)
                Next

                If locals.Count = block.Locals.Length Then
                    locals.Free()
                    Return block.Locals
                End If

                Return locals.ToImmutableAndFree()
            End Function

            ''' <summary>
            ''' A field or property initializer declares the temporaries of the sequences in its value.
            ''' </summary>
            Public Shared Function GetExpressionLocals(expression As BoundExpression) As ImmutableArray(Of LocalSymbol)
                Dim options = TryCast(expression.Syntax.SyntaxTree?.Options, VisualBasicParseOptions)
                If options Is Nothing OrElse options.LanguageVersion < LanguageVersion.VbNext Then
                    Return ImmutableArray(Of LocalSymbol).Empty
                End If

                Dim locals = ArrayBuilder(Of LocalSymbol).GetInstance()
                Dim collector As New SequenceLocalsCollector(locals)
                collector.Visit(expression)
                Return locals.ToImmutableAndFree()
            End Function

            Public Overrides Function VisitSequence(node As BoundSequence) As BoundNode
                _locals.AddRange(node.Locals)
                Return MyBase.VisitSequence(node)
            End Function

            Public Overrides Function VisitBlock(node As BoundBlock) As BoundNode
                Return Nothing
            End Function

            Public Overrides Function VisitLambda(node As BoundLambda) As BoundNode
                Return Nothing
            End Function

            Public Overrides Function VisitQueryLambda(node As BoundQueryLambda) As BoundNode
                Return Nothing
            End Function

            Public Overrides Function VisitUnboundLambda(node As UnboundLambda) As BoundNode
                Return Nothing
            End Function
        End Class
    End Class
End Namespace
