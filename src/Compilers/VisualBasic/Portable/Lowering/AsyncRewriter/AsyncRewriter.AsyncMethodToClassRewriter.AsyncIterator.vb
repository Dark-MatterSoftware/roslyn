' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend NotInheritable Class AsyncRewriter
        Inherits StateMachineRewriter(Of CapturedSymbolOrExpression)

        Partial Friend Class AsyncMethodToClassRewriter
            Inherits StateMachineMethodToClassRewriter

            ''' <summary>
            ''' Yield states count down from <see cref="StateMachineState.FirstResumableAsyncIteratorState"/> while
            ''' Await states count up from zero.
            ''' </summary>
            Private _yieldStateAllocator As ResumableStateMachineStateAllocator

            ''' <summary>
            ''' Label for a Yield: completes the promise with True and returns from MoveNext.
            ''' </summary>
            Private _exprReturnLabelTrue As LabelSymbol

            ''' <summary>
            ''' Where to go when the enumerator is being disposed: the label after the innermost enclosing Finally,
            ''' or the end of the method. Nothing inside a Finally block, which runs to completion on its own.
            ''' </summary>
            Private _currentDisposalLabel As LabelSymbol

            Private ReadOnly Property IsAsyncIterator As Boolean
                Get
                    Return Me._asyncMethodKind = AsyncMethodKind.AsyncIterator
                End Get
            End Property

            Private Sub InitializeAsyncIterator(slotAllocatorOpt As VariableSlotAllocator)
                Me._yieldStateAllocator = New ResumableStateMachineStateAllocator(
                    slotAllocatorOpt, firstState:=StateMachineState.FirstResumableAsyncIteratorState, increasing:=False)
                Me._exprReturnLabelTrue = Me.F.GenerateLabel("yieldReturn")
                Me._currentDisposalLabel = Me._exprReturnLabel
            End Sub

            ''' <summary>
            ''' Wraps the rewritten body so the initial state resumes at its start:
            ''' <code>
            ''' initialStateResumeLabel:
            '''     If Me.$DisposeMode Then GoTo exprReturn
            '''     Me.$State = cachedState = NotStartedOrRunningState
            '''     [body]
            ''' </code>
            ''' </summary>
            Private Function WrapAsyncIteratorBody(initialStateResumeLabel As LabelSymbol, rewrittenBody As BoundStatement) As BoundStatement
                Return Me.F.Block(
                    Me.F.Label(initialStateResumeLabel),
                    GenerateJumpToCurrentDisposalLabel(),
                    GenerateSetBothStates(StateMachineState.NotStartedOrRunningState),
                    rewrittenBody)
            End Function

            Private Function GenerateSetBothStates(state As StateMachineState) As BoundStatement
                Return Me.F.Assignment(
                    Me.F.Field(Me.F.Me(), Me.StateField, True),
                    Me.F.AssignmentExpression(Me.F.Local(Me.CachedState, True), Me.F.Literal(state)))
            End Function

            Private Function GenerateJumpToCurrentDisposalLabel() As BoundStatement
                If Me._currentDisposalLabel Is Nothing Then
                    Return Me.F.StatementList()
                End If

                Return Me.F.If(Me.F.Field(Me.F.Me(), Me._owner._disposeModeField, False), Me.F.Goto(Me._currentDisposalLabel))
            End Function

            ''' <summary>
            ''' The end of an async iterator: clears the current value and completes the builder before the
            ''' promise is completed with False or with the exception.
            ''' </summary>
            Private Function GenerateAsyncIteratorCompletion(promiseCompletion As BoundExpression) As BoundStatement
                Return Me.F.Block(
                    Me.F.Assignment(Me.F.Field(Me.F.Me(), Me._owner._currentField, True), Me.F.Null(Me._owner._currentField.Type)),
                    Me.F.ExpressionStatement(
                        Me._owner.GenerateMethodCall(Me.F.Field(Me.F.Me(), Me._builder, False), Me._owner._builderType, "Complete")),
                    Me.F.ExpressionStatement(promiseCompletion))
            End Function

            Private Function GeneratePromiseCompletion(member As WellKnownMember, argument As BoundExpression) As BoundExpression
                Return Me.F.Call(Me.F.Field(Me.F.Me(), Me._owner._promiseOfValueOrEndField, False), Me._owner.GetPromiseMethod(member), argument)
            End Function

            Public Overrides Function VisitYieldStatement(node As BoundYieldStatement) As BoundNode
                Debug.Assert(IsAsyncIterator)

                '     Yield expression
                ' is translated to -
                '     Me.$Current = expression
                '     Me.$State = cachedState = <next_state>
                '     GoTo yieldReturn   ' Me.$PromiseOfValueOrEnd.SetResult(True): Return
                ' <next_state_label>:
                '     Me.$State = cachedState = NotStartedOrRunningState
                '     If Me.$DisposeMode Then GoTo <current_disposal_label>

                Dim stateNumber As StateMachineState = 0
                Dim resumeLabel As GeneratedLabelSymbol = Nothing
                AddResumableState(Me._yieldStateAllocator, node.Syntax, stateNumber, resumeLabel)

                Dim expression = DirectCast(Visit(node.Expression), BoundExpression)
                Dim currentField = Me.F.Field(Me.F.Me(), Me._owner._currentField, True)

                Dim assignCurrent As BoundStatement
                If expression.Kind = BoundKind.SpillSequence Then
                    Dim spill = DirectCast(expression, BoundSpillSequence)
                    assignCurrent = RewriteSpillSequenceIntoBlock(spill, False, Me.F.Assignment(currentField, spill.ValueOpt))
                Else
                    assignCurrent = Me.F.Assignment(currentField, expression)
                End If

                Return Me.F.SequencePoint(
                    node.Syntax,
                    Me.F.Block(
                        assignCurrent,
                        GenerateSetBothStates(stateNumber),
                        Me.F.Goto(Me._exprReturnLabelTrue),
                        Me.F.Label(resumeLabel),
                        GenerateSetBothStates(StateMachineState.NotStartedOrRunningState),
                        GenerateJumpToCurrentDisposalLabel()))
            End Function

            Public Overrides Function VisitTryStatement(node As BoundTryStatement) As BoundNode
                If Not IsAsyncIterator OrElse node.FinallyBlockOpt Is Nothing Then
                    Return MyBase.VisitTryStatement(node)
                End If

                ' Disposal leaves the Try body for the label after the Finally, which runs the Finally on the way
                ' out and then keeps going to the next enclosing disposal label:
                '     Try
                '         ...
                '     Finally
                '         ...
                '     End Try
                ' afterFinally:
                '     If Me.$DisposeMode Then GoTo <enclosing_disposal_label>
                Dim savedDisposalLabel = Me._currentDisposalLabel
                Dim afterFinally = Me.F.GenerateLabel("afterFinally")
                Me._currentDisposalLabel = afterFinally

                Dim result = DirectCast(MyBase.VisitTryStatement(node), BoundStatement)

                Me._currentDisposalLabel = savedDisposalLabel
                Return Me.F.Block(result, Me.F.Label(afterFinally), GenerateJumpToCurrentDisposalLabel())
            End Function

            Public Overrides Function VisitAsyncDisposalScope(node As BoundAsyncDisposalScope) As BoundNode
                ' Disposal inside the scope goes to its release, and the release then continues the disposal:
                '     [Try ... afterTry: release]
                '     If Me.$DisposeMode Then GoTo <enclosing_disposal_label>
                Dim savedDisposalLabel = Me._currentDisposalLabel
                Me._currentDisposalLabel = node.DisposalLabel

                Dim statement = DirectCast(Visit(node.Statement), BoundStatement)

                Me._currentDisposalLabel = savedDisposalLabel
                Return Me.F.Block(statement, GenerateJumpToCurrentDisposalLabel())
            End Function

            Protected Overrides Function VisitFinallyBlock(finallyBlock As BoundBlock) As BoundBlock
                If Not IsAsyncIterator Then
                    Return MyBase.VisitFinallyBlock(finallyBlock)
                End If

                Dim savedDisposalLabel = Me._currentDisposalLabel
                Me._currentDisposalLabel = Nothing
                Dim result = MyBase.VisitFinallyBlock(finallyBlock)
                Me._currentDisposalLabel = savedDisposalLabel
                Return result
            End Function

            Protected Overrides Function ShouldEnterFinallyBlock() As BoundExpression
                If Not IsAsyncIterator Then
                    Return MyBase.ShouldEnterFinallyBlock()
                End If

                ' A Yield suspends on a negative state too, so only a running state machine runs Finally blocks.
                Return Me.F.IntEqual(Me.F.Local(Me.CachedState, False), Me.F.Literal(StateMachineState.NotStartedOrRunningState))
            End Function

            Protected Overrides ReadOnly Property UseFinalizerBookkeeping As Boolean
                Get
                    Return Not IsAsyncIterator
                End Get
            End Property

            Protected Overrides Function GenerateMissingStateDispatch() As BoundStatement
                Dim result = MyBase.GenerateMissingStateDispatch()
                If Not IsAsyncIterator Then
                    Return result
                End If

                Dim yieldDispatch = Me._yieldStateAllocator.GenerateThrowMissingStateDispatch(Me.F, Me.F.Local(Me.CachedState, False), EncMissingStateErrorCode)
                If result Is Nothing Then
                    Return yieldDispatch
                End If

                Return If(yieldDispatch Is Nothing, result, Me.F.Block(result, yieldDispatch))
            End Function

            Private Function RewriteAsyncIteratorReturn() As BoundStatement
                ' Return in an async iterator ends the iteration: it is a disposal that starts at the Return.
                ' STAT:   Me.$DisposeMode = True
                ' STAT:   GoTo <current_disposal_label>
                Return Me.F.Block(
                    Me.F.Assignment(Me.F.Field(Me.F.Me(), Me._owner._disposeModeField, True), Me.F.Literal(True)),
                    Me.F.Goto(If(Me._currentDisposalLabel, Me._exprReturnLabel)))
            End Function
        End Class
    End Class
End Namespace
