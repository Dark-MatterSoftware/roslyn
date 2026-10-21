' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class LocalRewriter

        ''' <summary>
        ''' A Catch body that awaits cannot stay in its handler, because the state machine cannot resume inside one.
        ''' The statement is rewritten as
        ''' 
        '''      pendingJump = 0
        '''      retry:
        '''      pendingCatch = 0
        '''      Try
        '''          If pendingJump = k Then pendingJump = 0 : GoTo label_k   ' GoTo from a Catch back into the Try
        '''          body
        '''      Catch ex As E When filter
        '''          pendingException = ex : pendingCatch = i
        '''      End Try
        '''      If pendingCatch = i Then
        '''          SetProjectError(pendingException) : catch body : ClearProjectError()
        '''      End If
        '''      exitLabel:
        ''' 
        ''' A Finally stays in a Try around all of it. In the catch bodies, Throw rethrows pendingException with its
        ''' stack trace, and GoTo label_k becomes pendingJump = k : GoTo retry.
        ''' </summary>
        Private Function RewriteTryWithAwaitInCatch(node As BoundTryStatement) As BoundStatement
            Dim syntax = node.Syntax
            Dim factory As New SyntheticBoundNodeFactory(_topMethod, _currentMethodOrLambda, syntax, _compilationState, _diagnostics)
            Dim int32Type = GetSpecialType(SpecialType.System_Int32)
            Dim exceptionType = Compilation.GetWellKnownType(WellKnownType.System_Exception)

            Dim pendingCatch = factory.SynthesizedLocal(int32Type, SynthesizedLocalKind.TryAwaitPendingCatch, syntax)
            Dim pendingException = factory.SynthesizedLocal(exceptionType, SynthesizedLocalKind.TryAwaitPendingException, syntax)
            Dim pendingJump = factory.SynthesizedLocal(int32Type, SynthesizedLocalKind.TryAwaitPendingBranch, syntax)
            Dim retry = factory.GenerateLabel("awaitInCatchRetry")

            Dim locals = ArrayBuilder(Of LocalSymbol).GetInstance()
            locals.Add(pendingCatch)
            locals.Add(pendingException)
            locals.Add(pendingJump)

            Dim tryLabels As New HashSet(Of LabelSymbol)()
            TryBlockLabelCollector.Collect(node.TryBlock, tryLabels)
            Dim jumpTargets = ArrayBuilder(Of LabelSymbol).GetInstance()

            Dim handlers = ArrayBuilder(Of BoundCatchBlock).GetInstance()
            Dim bodies = ArrayBuilder(Of BoundStatement).GetInstance()
            For i = 0 To node.CatchBlocks.Length - 1
                Dim catchBlock = node.CatchBlocks(i)
                Dim handlerNumber = i + 1

                ' The catch variable outlives the handler, so the enclosing block declares it.
                Dim source = catchBlock.ExceptionSourceOpt
                If catchBlock.LocalOpt IsNot Nothing Then
                    locals.Add(catchBlock.LocalOpt)
                ElseIf source Is Nothing Then
                    Dim caught = factory.SynthesizedLocal(exceptionType, SynthesizedLocalKind.TryAwaitPendingCaughtException, syntax)
                    locals.Add(caught)
                    source = factory.Local(caught, isLValue:=True)
                End If

                Dim caughtValue = source.MakeRValue()
                If Not TypeSymbol.Equals(caughtValue.Type, exceptionType, TypeCompareKind.ConsiderEverything) Then
                    caughtValue = factory.Convert(exceptionType, caughtValue, ConversionKind.WideningReference)
                End If

                Dim record = factory.Block(
                    factory.Assignment(factory.Local(pendingException, isLValue:=True), caughtValue),
                    factory.Assignment(factory.Local(pendingCatch, isLValue:=True), factory.Literal(handlerNumber)))

                handlers.Add(catchBlock.Update(localOpt:=Nothing,
                                               exceptionSourceOpt:=source,
                                               errorLineNumberOpt:=catchBlock.ErrorLineNumberOpt,
                                               exceptionFilterOpt:=catchBlock.ExceptionFilterOpt,
                                               body:=record,
                                               isSynthesizedAsyncCatchAll:=False))

                Dim body = DirectCast(New CatchBodyRewriter(Me, factory, pendingException, pendingJump, retry, tryLabels, jumpTargets).Visit(catchBlock.Body), BoundBlock)
                bodies.Add(factory.If(factory.IntEqual(factory.Local(pendingCatch, isLValue:=False), factory.Literal(handlerNumber)),
                                      WithProjectError(factory, pendingException, body)))
            Next

            ' GoTo from a catch body back into the Try re-enters at the top of the Try and jumps from there.
            Dim tryBlock = node.TryBlock
            If jumpTargets.Count > 0 Then
                Dim dispatch = ArrayBuilder(Of BoundStatement).GetInstance()
                For k = 0 To jumpTargets.Count - 1
                    dispatch.Add(factory.If(factory.IntEqual(factory.Local(pendingJump, isLValue:=False), factory.Literal(k + 1)),
                                            factory.Block(factory.Assignment(factory.Local(pendingJump, isLValue:=True), factory.Literal(0)),
                                                          factory.Goto(jumpTargets(k), setWasCompilerGenerated:=False))))
                Next

                dispatch.Add(tryBlock)
                tryBlock = factory.Block(dispatch.ToImmutableAndFree())
            End If

            jumpTargets.Free()

            Dim statements = ArrayBuilder(Of BoundStatement).GetInstance()
            statements.Add(factory.Assignment(factory.Local(pendingJump, isLValue:=True), factory.Literal(0)))
            statements.Add(factory.Label(retry))
            statements.Add(factory.Assignment(factory.Local(pendingCatch, isLValue:=True), factory.Literal(0)))
            statements.Add(New BoundTryStatement(syntax, tryBlock, handlers.ToImmutableAndFree(), finallyBlockOpt:=Nothing, exitLabelOpt:=Nothing))
            statements.AddRange(bodies)
            bodies.Free()

            Dim result As BoundStatement = factory.Block(statements.ToImmutableAndFree())
            If node.FinallyBlockOpt IsNot Nothing Then
                result = New BoundTryStatement(syntax, factory.Block(result), ImmutableArray(Of BoundCatchBlock).Empty, node.FinallyBlockOpt, exitLabelOpt:=Nothing)
            End If

            If node.ExitLabelOpt IsNot Nothing Then
                result = factory.Block(result, factory.Label(node.ExitLabelOpt))
            End If

            Return New BoundBlock(syntax, Nothing, locals.ToImmutableAndFree(), ImmutableArray.Create(result))
        End Function

        ''' <summary>
        ''' The Err object holds the exception while the catch body runs, as it does in a handler.
        ''' </summary>
        Private Function WithProjectError(factory As SyntheticBoundNodeFactory, pendingException As LocalSymbol, body As BoundBlock) As BoundStatement
            Dim setProjectError = TryCast(Compilation.GetWellKnownTypeMember(WellKnownMember.Microsoft_VisualBasic_CompilerServices_ProjectData__SetProjectError), MethodSymbol)
            Dim clearProjectError = TryCast(Compilation.GetWellKnownTypeMember(WellKnownMember.Microsoft_VisualBasic_CompilerServices_ProjectData__ClearProjectError), MethodSymbol)
            If setProjectError Is Nothing OrElse clearProjectError Is Nothing Then
                Return body
            End If

            Return factory.Block(
                factory.ExpressionStatement(factory.Call(Nothing, setProjectError, factory.Local(pendingException, isLValue:=False))),
                body,
                factory.ExpressionStatement(factory.Call(Nothing, clearProjectError)))
        End Function

        ''' <summary>
        ''' Whether a Catch body awaits, outside lambdas.
        ''' </summary>
        Private Shared Function CatchBlocksContainAwait(node As BoundTryStatement) As Boolean
            For Each catchBlock In node.CatchBlocks
                If AwaitFinder.ContainsAwait(catchBlock.Body) Then
                    Return True
                End If
            Next

            Return False
        End Function

        Private NotInheritable Class AwaitFinder
            Inherits BoundTreeWalkerWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator

            Private _found As Boolean

            Public Shared Function ContainsAwait(node As BoundNode) As Boolean
                Dim finder As New AwaitFinder()
                finder.Visit(node)
                Return finder._found
            End Function

            Public Overrides Function Visit(node As BoundNode) As BoundNode
                If _found Then
                    Return Nothing
                End If

                Return MyBase.Visit(node)
            End Function

            Public Overrides Function VisitAwaitOperator(node As BoundAwaitOperator) As BoundNode
                _found = True
                Return Nothing
            End Function

            Public Overrides Function VisitUsingStatement(node As BoundUsingStatement) As BoundNode
                If node.UsingInfo.IsAsynchronous Then
                    _found = True
                    Return Nothing
                End If

                Return MyBase.VisitUsingStatement(node)
            End Function

            Public Overrides Function VisitForEachStatement(node As BoundForEachStatement) As BoundNode
                If node.EnumeratorInfo.IsAsynchronous Then
                    _found = True
                    Return Nothing
                End If

                Return MyBase.VisitForEachStatement(node)
            End Function

            Public Overrides Function VisitLambda(node As BoundLambda) As BoundNode
                Return Nothing
            End Function
        End Class

        Private NotInheritable Class TryBlockLabelCollector
            Inherits BoundTreeWalkerWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator

            Private ReadOnly _labels As HashSet(Of LabelSymbol)

            Private Sub New(labels As HashSet(Of LabelSymbol))
                _labels = labels
            End Sub

            Public Shared Sub Collect(node As BoundNode, labels As HashSet(Of LabelSymbol))
                Dim collector As New TryBlockLabelCollector(labels)
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

        ''' <summary>
        ''' Rewrites a catch body that runs after its Try: Throw rethrows the pending exception, and GoTo a label in the
        ''' Try goes back through the retry label. Nested Catch bodies keep their own Throw.
        ''' </summary>
        Private NotInheritable Class CatchBodyRewriter
            Inherits BoundTreeRewriterWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator

            Private ReadOnly _owner As LocalRewriter
            Private ReadOnly _factory As SyntheticBoundNodeFactory
            Private ReadOnly _pendingException As LocalSymbol
            Private ReadOnly _pendingJump As LocalSymbol
            Private ReadOnly _retry As LabelSymbol
            Private ReadOnly _tryLabels As HashSet(Of LabelSymbol)
            Private ReadOnly _jumpTargets As ArrayBuilder(Of LabelSymbol)
            Private _nestedCatchDepth As Integer

            Public Sub New(owner As LocalRewriter, factory As SyntheticBoundNodeFactory, pendingException As LocalSymbol, pendingJump As LocalSymbol,
                           retry As LabelSymbol, tryLabels As HashSet(Of LabelSymbol), jumpTargets As ArrayBuilder(Of LabelSymbol))
                _owner = owner
                _factory = factory
                _pendingException = pendingException
                _pendingJump = pendingJump
                _retry = retry
                _tryLabels = tryLabels
                _jumpTargets = jumpTargets
            End Sub

            Public Overrides Function VisitLambda(node As BoundLambda) As BoundNode
                Return node
            End Function

            Public Overrides Function VisitCatchBlock(node As BoundCatchBlock) As BoundNode
                _nestedCatchDepth += 1
                Dim result = MyBase.VisitCatchBlock(node)
                _nestedCatchDepth -= 1
                Return result
            End Function

            Public Overrides Function VisitThrowStatement(node As BoundThrowStatement) As BoundNode
                If node.ExpressionOpt Is Nothing AndAlso _nestedCatchDepth = 0 Then
                    Return _owner.RethrowPendingException(_factory, _pendingException)
                End If

                Return MyBase.VisitThrowStatement(node)
            End Function

            Public Overrides Function VisitGotoStatement(node As BoundGotoStatement) As BoundNode
                If Not _tryLabels.Contains(node.Label) Then
                    Return node
                End If

                Dim index = _jumpTargets.IndexOf(node.Label)
                If index < 0 Then
                    _jumpTargets.Add(node.Label)
                    index = _jumpTargets.Count - 1
                End If

                Return New BoundStatementList(node.Syntax, ImmutableArray.Create(Of BoundStatement)(
                    _factory.Assignment(_factory.Local(_pendingJump, isLValue:=True), _factory.Literal(index + 1)),
                    _factory.Goto(_retry)))
            End Function
        End Class
    End Class
End Namespace
