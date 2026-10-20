' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class LocalRewriter

        ''' <summary>
        ''' An Async Using resource is released with an awaited DisposeAsync. Visual Basic cannot await in Finally,
        ''' so the block is rewritten as
        ''' 
        '''      pending = 0 : pendingException = Nothing
        '''      resource = initialization
        '''      Try
        '''          body, where each jump out of the body is "pending = n : GoTo afterTry"
        '''      Catch ex As Exception
        '''          pendingException = ex
        '''      End Try
        '''      afterTry:
        '''      If resource IsNot Nothing Then Await CType(resource, IAsyncDisposable).DisposeAsync()
        '''      If pendingException IsNot Nothing Then ExceptionDispatchInfo.Capture(pendingException).Throw()
        '''      If pending = n Then the original jump
        ''' 
        ''' A Return with a value stores the value first and returns it after the resource is released.
        ''' </summary>
        Private Function RewriteSingleUsingToAsyncDispose(
            resourceSyntax As SyntaxNode,
            localSymbol As LocalSymbol,
            initializationExpression As BoundExpression,
            ByRef placeholderInfo As ValueTuple(Of BoundRValuePlaceholder, BoundExpression, BoundExpression),
            disposeAwait As BoundExpression,
            currentBody As BoundBlock
        ) As BoundBlock

            Dim factory As New SyntheticBoundNodeFactory(_topMethod, _currentMethodOrLambda, resourceSyntax, _compilationState, _diagnostics)
            Dim resourceType = localSymbol.Type
            Dim boundResourceLocal = New BoundLocal(resourceSyntax, localSymbol, isLValue:=True, type:=resourceType)

            Dim resourcePlaceholder As BoundRValuePlaceholder = placeholderInfo.Item1
            Dim disposeCondition As BoundExpression = placeholderInfo.Item3
            AddPlaceholderReplacement(resourcePlaceholder, boundResourceLocal.MakeRValue())

            Dim int32Type = GetSpecialType(SpecialType.System_Int32)
            Dim exceptionType = Compilation.GetWellKnownType(WellKnownType.System_Exception)
            ' These live across the awaited DisposeAsync, so they are long-lived kinds that a state machine hoists.
            Dim pendingBranch = factory.SynthesizedLocal(int32Type, SynthesizedLocalKind.TryAwaitPendingBranch, resourceSyntax)
            Dim pendingException = factory.SynthesizedLocal(exceptionType, SynthesizedLocalKind.TryAwaitPendingException, resourceSyntax)
            ' The catch block declares its own variable.
            Dim caught = factory.SynthesizedLocal(exceptionType)
            Dim afterTry = factory.GenerateLabel("asyncUsing")
            Dim locals = ArrayBuilder(Of LocalSymbol).GetInstance()
            locals.Add(pendingBranch)
            locals.Add(pendingException)

            Dim exits = New AsyncUsingExitRewriter(factory, afterTry, pendingBranch, locals)
            Dim body = DirectCast(exits.Visit(currentBody), BoundBlock)
            body = exits.AppendStubs(body)

            Dim statements = ArrayBuilder(Of BoundStatement).GetInstance()
            statements.Add(factory.Assignment(factory.Local(pendingBranch, isLValue:=True), factory.Literal(0)))
            statements.Add(factory.Assignment(factory.Local(pendingException, isLValue:=True), factory.Null(exceptionType)))
            statements.Add(New BoundAssignmentOperator(resourceSyntax,
                                                       boundResourceLocal,
                                                       VisitAndGenerateObjectCloneIfNeeded(initializationExpression, suppressObjectClone:=True),
                                                       suppressObjectClone:=True,
                                                       type:=resourceType).ToStatement())

            Dim catchBody = factory.Block(factory.Assignment(factory.Local(pendingException, isLValue:=True), factory.Local(caught, isLValue:=False)))
            Dim catchBlock = New BoundCatchBlock(resourceSyntax, caught, factory.Local(caught, isLValue:=False), Nothing, Nothing, catchBody, isSynthesizedAsyncCatchAll:=False)
            statements.Add(factory.Try(body, ImmutableArray.Create(catchBlock)))
            statements.Add(factory.Label(afterTry))

            Dim dispose As BoundStatement = factory.ExpressionStatement(VisitExpression(disposeAwait))
            If disposeCondition IsNot Nothing Then
                dispose = factory.If(VisitExpression(disposeCondition), dispose)
            End If

            statements.Add(dispose)
            statements.Add(factory.If(factory.ReferenceIsNotNothing(factory.Local(pendingException, isLValue:=False)), RethrowPendingException(factory, pendingException)))
            exits.AddDispatch(statements)

            RemovePlaceholderReplacement(resourcePlaceholder)

            Return New BoundBlock(resourceSyntax, Nothing, locals.ToImmutableAndFree(), statements.ToImmutableAndFree())
        End Function

        ''' <summary>
        ''' ExceptionDispatchInfo.Capture(pendingException).Throw(), which keeps the original stack trace, or
        ''' Throw pendingException when that type is not available.
        ''' </summary>
        Private Function RethrowPendingException(factory As SyntheticBoundNodeFactory, pendingException As LocalSymbol) As BoundStatement
            Dim capture = DirectCast(Compilation.GetWellKnownTypeMember(WellKnownMember.System_Runtime_ExceptionServices_ExceptionDispatchInfo__Capture), MethodSymbol)
            Dim [throw] = DirectCast(Compilation.GetWellKnownTypeMember(WellKnownMember.System_Runtime_ExceptionServices_ExceptionDispatchInfo__Throw), MethodSymbol)
            If capture Is Nothing OrElse [throw] Is Nothing Then
                Return factory.Throw(factory.Local(pendingException, isLValue:=False))
            End If

            Return factory.ExpressionStatement(factory.Call(factory.Call(Nothing, capture, factory.Local(pendingException, isLValue:=False)), [throw]))
        End Function

        ''' <summary>
        ''' Rewrites the jumps that leave a lowered Async Using body so they reach the release of the resource first,
        ''' and builds the statements that continue each jump afterwards.
        ''' </summary>
        Private NotInheritable Class AsyncUsingExitRewriter
            Inherits BoundTreeRewriterWithStackGuard

            Private ReadOnly _factory As SyntheticBoundNodeFactory
            Private ReadOnly _afterTry As LabelSymbol
            Private ReadOnly _pendingBranch As LocalSymbol
            Private ReadOnly _locals As ArrayBuilder(Of LocalSymbol)
            Private ReadOnly _labelsInBody As New HashSet(Of LabelSymbol)()
            Private ReadOnly _continuations As ArrayBuilder(Of BoundStatement) = ArrayBuilder(Of BoundStatement).GetInstance()
            Private ReadOnly _stubs As ArrayBuilder(Of BoundStatement) = ArrayBuilder(Of BoundStatement).GetInstance()
            Private _collected As Boolean

            Public Sub New(factory As SyntheticBoundNodeFactory, afterTry As LabelSymbol, pendingBranch As LocalSymbol, locals As ArrayBuilder(Of LocalSymbol))
                _factory = factory
                _afterTry = afterTry
                _pendingBranch = pendingBranch
                _locals = locals
            End Sub

            Public Overrides Function Visit(node As BoundNode) As BoundNode
                If Not _collected Then
                    _collected = True
                    LabelCollector.Collect(node, _labelsInBody)
                End If

                Return MyBase.Visit(node)
            End Function

            Public Overrides Function VisitLambda(node As BoundLambda) As BoundNode
                Return node
            End Function

            Public Overrides Function VisitGotoStatement(node As BoundGotoStatement) As BoundNode
                If _labelsInBody.Contains(node.Label) Then
                    Return node
                End If

                Return LeaveBody(node.Syntax, New BoundGotoStatement(node.Syntax, node.Label, Nothing))
            End Function

            Public Overrides Function VisitConditionalGoto(node As BoundConditionalGoto) As BoundNode
                If _labelsInBody.Contains(node.Label) Then
                    Return MyBase.VisitConditionalGoto(node)
                End If

                ' The jump is taken through a label inside the body, which then leaves it.
                Dim stub = _factory.GenerateLabel("asyncUsingExit")
                _stubs.Add(_factory.Label(stub))
                _stubs.Add(LeaveBody(node.Syntax, New BoundGotoStatement(node.Syntax, node.Label, Nothing)))
                Return node.Update(DirectCast(Visit(node.Condition), BoundExpression), node.JumpIfTrue, stub)
            End Function

            Public Overrides Function VisitReturnStatement(node As BoundReturnStatement) As BoundNode
                If node.ExpressionOpt Is Nothing Then
                    Return LeaveBody(node.Syntax, node)
                End If

                Dim value = DirectCast(Visit(node.ExpressionOpt), BoundExpression)
                Dim saved = _factory.SynthesizedLocal(value.Type, SynthesizedLocalKind.AsyncMethodReturnValue, node.Syntax)
                _locals.Add(saved)
                Dim continuation = node.Update(New BoundLocal(node.Syntax, saved, isLValue:=False, type:=saved.Type), node.FunctionLocalOpt, node.ExitLabelOpt)
                Return New BoundStatementList(node.Syntax, ImmutableArray.Create(Of BoundStatement)(
                    _factory.Assignment(_factory.Local(saved, isLValue:=True), value),
                    LeaveBody(node.Syntax, continuation)))
            End Function

            Private Function LeaveBody(syntax As SyntaxNode, continuation As BoundStatement) As BoundStatement
                Dim branch = _continuations.Count + 1
                _continuations.Add(_factory.If(_factory.IntEqual(_factory.Local(_pendingBranch, isLValue:=False), _factory.Literal(branch)), continuation))
                Return New BoundStatementList(syntax, ImmutableArray.Create(Of BoundStatement)(
                    _factory.Assignment(_factory.Local(_pendingBranch, isLValue:=True), _factory.Literal(branch)),
                    _factory.Goto(_afterTry)))
            End Function

            ''' <summary>
            ''' Adds the targets of conditional jumps that leave the body after its last statement.
            ''' </summary>
            Public Function AppendStubs(body As BoundBlock) As BoundBlock
                If _stubs.Count = 0 Then
                    _stubs.Free()
                    Return body
                End If

                Dim statements = ArrayBuilder(Of BoundStatement).GetInstance()
                statements.AddRange(body.Statements)
                statements.Add(_factory.Goto(_afterTry))
                statements.AddRange(_stubs)
                _stubs.Free()
                Return body.Update(body.StatementListSyntax, body.Locals, statements.ToImmutableAndFree())
            End Function

            Public Sub AddDispatch(statements As ArrayBuilder(Of BoundStatement))
                statements.AddRange(_continuations)
                _continuations.Free()
            End Sub

            Private NotInheritable Class LabelCollector
                Inherits BoundTreeWalkerWithStackGuard

                Private ReadOnly _labels As HashSet(Of LabelSymbol)

                Private Sub New(labels As HashSet(Of LabelSymbol))
                    _labels = labels
                End Sub

                Public Shared Sub Collect(node As BoundNode, labels As HashSet(Of LabelSymbol))
                    Dim collector = New LabelCollector(labels)
                    collector.Visit(node)
                End Sub

                Public Overrides Function VisitLabelStatement(node As BoundLabelStatement) As BoundNode
                    _labels.Add(node.Label)
                    Return Nothing
                End Function

                Public Overrides Function VisitLambda(node As BoundLambda) As BoundNode
                    Return Nothing
                End Function
            End Class
        End Class
    End Class
End Namespace
