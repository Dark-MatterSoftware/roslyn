' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Nullable flow state for locals and parameters. Declared annotations stay on the symbols.
    ''' This walker does not change definite assignment, reachability diagnostics, or generated IL.
    ''' </summary>
    Friend NotInheritable Class NullableFlowAnalysis
        Inherits BoundTreeWalkerWithStackGuard

        Private Enum FlowNullability
            Unknown
            NotNull
            MaybeNull
        End Enum

        Private NotInheritable Class FlowSnapshot
            Friend ReadOnly Reachable As Boolean
            Friend ReadOnly Slots As Dictionary(Of Symbol, FlowNullability)

            Friend Sub New(reachable As Boolean, slots As Dictionary(Of Symbol, FlowNullability))
                Me.Reachable = reachable
                Me.Slots = slots
            End Sub
        End Class

        Private NotInheritable Class ConditionSplit
            Friend WhenTrue As FlowSnapshot
            Friend WhenFalse As FlowSnapshot
        End Class

        Private Const MaxLoopIterations As Integer = 8
        Private Const MaxGotoPasses As Integer = 4

        Private ReadOnly _method As MethodSymbol
        Private ReadOnly _diagnostics As DiagnosticBag
        Private ReadOnly _project As NullableContextOptions
        Private _reachable As Boolean
        Private _state As Dictionary(Of Symbol, FlowNullability)
        Private ReadOnly _jumps As Dictionary(Of LabelSymbol, FlowSnapshot)
        Private ReadOnly _captured As HashSet(Of Symbol)
        Private ReadOnly _visitedLabels As HashSet(Of LabelSymbol)
        Private ReadOnly _reported As HashSet(Of String)
        Private ReadOnly _writes As Dictionary(Of Symbol, Integer)
        Private ReadOnly _exceptionStates As List(Of FlowSnapshot)
        Private _writeClock As Integer
        Private _isolation As Integer
        Private _needsRewalk As Boolean

        Private Sub New(method As MethodSymbol, diagnostics As DiagnosticBag, project As NullableContextOptions)
            _method = method
            _diagnostics = diagnostics
            _project = project
            _state = New Dictionary(Of Symbol, FlowNullability)()
            _jumps = New Dictionary(Of LabelSymbol, FlowSnapshot)()
            _captured = New HashSet(Of Symbol)()
            _visitedLabels = New HashSet(Of LabelSymbol)()
            _reported = New HashSet(Of String)()
            _writes = New Dictionary(Of Symbol, Integer)()
            _exceptionStates = New List(Of FlowSnapshot)()
        End Sub

        Public Shared Sub Analyze(method As MethodSymbol, body As BoundBlock, diagnostics As DiagnosticBag)
            Dim compilation = method.DeclaringCompilation
            If compilation Is Nothing OrElse compilation.LanguageVersion < LanguageVersion.VbNext Then
                Return
            End If

            If body Is Nothing OrElse body.Syntax Is Nothing OrElse body.Syntax.SyntaxTree Is Nothing Then
                Return
            End If

            Dim project = DirectCast(compilation.Options, VisualBasicCompilationOptions).NullableContext
            Dim map = NullableContextFacts.ForTree(body.Syntax.SyntaxTree, project)
            If (project And NullableContextOptions.Warnings) = 0 AndAlso Not map.HasDirective Then
                Return
            End If

            Dim walker = New NullableFlowAnalysis(method, diagnostics, project)
            walker.BeginPass()
            walker.Visit(body)
            Dim pass = 1
            While walker._needsRewalk AndAlso pass < MaxGotoPasses
                walker._needsRewalk = False
                walker.BeginPass()
                walker.Visit(body)
                pass += 1
            End While

            If walker._needsRewalk Then
                ' The backward edges did not converge. Drop non-null facts carried by those
                ' jumps and visit once more so the widened state is actually diagnosed.
                walker.WidenJumps()
                walker._needsRewalk = False
                walker.BeginPass()
                walker.Visit(body)
            End If
        End Sub

        Private Sub BeginPass()
            _reachable = True
            _state = New Dictionary(Of Symbol, FlowNullability)()
            _captured.Clear()
            _visitedLabels.Clear()
            _writes.Clear()
            _exceptionStates.Clear()
            _isolation = 0
            For Each parameter In _method.Parameters
                If Not parameter.IsMe AndAlso IsTrackedReference(parameter) Then
                    _state(parameter) = InitialNullability(parameter)
                End If
            Next
        End Sub

        Public Overrides Function VisitIfStatement(node As BoundIfStatement) As BoundNode
            If Not _reachable Then
                Visit(node.Consequence)
                Visit(node.AlternativeOpt)
                Return Nothing
            End If

            Dim split = SplitCondition(node.Condition)
            SetSnapshot(split.WhenTrue)
            Visit(node.Consequence)
            Dim afterThen = Capture()
            SetSnapshot(split.WhenFalse)
            If node.AlternativeOpt IsNot Nothing Then
                Visit(node.AlternativeOpt)
            End If

            Dim afterElse = Capture()
            SetSnapshot(MergeSnapshots(afterThen, afterElse))
            Return Nothing
        End Function

        Public Overrides Function VisitSelectStatement(node As BoundSelectStatement) As BoundNode
            If Not _reachable Then
                For Each block In node.CaseBlocks
                    Visit(block.Body)
                Next
                Return Nothing
            End If

            Visit(node.ExpressionStatement)
            Dim afterSelector = Capture()
            Dim join = Unreachable()
            Dim sawElse = False
            For Each block In node.CaseBlocks
                If block.CaseStatement.Syntax IsNot Nothing AndAlso block.CaseStatement.Syntax.Kind = SyntaxKind.CaseElseStatement Then
                    sawElse = True
                End If

                SetSnapshot(CloneSnapshot(afterSelector))
                If block.CaseStatement.ConditionOpt IsNot Nothing Then
                    Evaluate(block.CaseStatement.ConditionOpt)
                End If

                Visit(block.Body)
                join = MergeSnapshots(join, Capture())
            Next

            If Not sawElse Then
                join = MergeSnapshots(join, afterSelector)
            End If

            join = MergeSnapshots(join, JumpSnapshot(node.ExitLabel))
            SetSnapshot(join)
            Return Nothing
        End Function

        Public Overrides Function VisitWhileStatement(node As BoundWhileStatement) As BoundNode
            If Not _reachable Then
                Visit(node.Body)
                Return Nothing
            End If

            VisitPretestedLoop(node.Condition, node.Body, node.ContinueLabel, node.ExitLabel, untilCondition:=False)
            Return Nothing
        End Function

        Public Overrides Function VisitDoLoopStatement(node As BoundDoLoopStatement) As BoundNode
            If Not _reachable Then
                Visit(node.Body)
                Return Nothing
            End If

            If node.TopConditionOpt Is Nothing AndAlso node.BottomConditionOpt Is Nothing Then
                VisitOpenLoop(node.Body, node.ContinueLabel, node.ExitLabel)
            ElseIf node.ConditionIsTop Then
                VisitPretestedLoop(node.ConditionOpt, node.Body, node.ContinueLabel, node.ExitLabel, node.ConditionIsUntil)
            Else
                VisitPosttestedLoop(node.ConditionOpt, node.Body, node.ContinueLabel, node.ExitLabel, node.ConditionIsUntil)
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitForToStatement(node As BoundForToStatement) As BoundNode
            If Not _reachable Then
                Visit(node.Body)
                Return Nothing
            End If

            Dim initial = Evaluate(node.InitialValue)
            Evaluate(node.LimitValue)
            Evaluate(node.StepValue)
            Dim control = TrackedSymbol(node.ControlVariable)
            If control IsNot Nothing Then
                Assign(control, initial, node.InitialValue)
            ElseIf node.DeclaredOrInferredLocalOpt IsNot Nothing AndAlso IsTrackedReference(node.DeclaredOrInferredLocalOpt) Then
                Assign(node.DeclaredOrInferredLocalOpt, initial, node.InitialValue)
            End If

            VisitRepeatableBody(node.Body, node.ContinueLabel, node.ExitLabel, Nothing, FlowNullability.NotNull)
            Return Nothing
        End Function

        Public Overrides Function VisitForEachStatement(node As BoundForEachStatement) As BoundNode
            If Not _reachable Then
                Visit(node.Body)
                Return Nothing
            End If

            Dim collectionState = Evaluate(node.Collection)
            If collectionState = FlowNullability.MaybeNull Then
                WarnReceiverSyntax(node.Collection)
            End If

            Dim control As Symbol = node.DeclaredOrInferredLocalOpt
            If control Is Nothing Then
                control = TrackedSymbol(node.ControlVariable)
            End If

            Dim elementState = FlowNullability.NotNull
            If control IsNot Nothing AndAlso IsTrackedReference(control) Then
                elementState = InitialNullability(control)
                If elementState = FlowNullability.Unknown Then
                    elementState = FlowNullability.MaybeNull
                End If
            Else
                control = Nothing
            End If

            VisitRepeatableBody(node.Body, node.ContinueLabel, node.ExitLabel, control, elementState)
            Return Nothing
        End Function

        Public Overrides Function VisitBlock(node As BoundBlock) As BoundNode
            For Each statement In node.Statements
                If _reachable Then
                    ObserveException()
                End If

                Visit(statement)
            Next

            Return Nothing
        End Function

        Public Overrides Function VisitTryStatement(node As BoundTryStatement) As BoundNode
            If Not _reachable Then
                Visit(node.TryBlock)
                For Each catchBlock In node.CatchBlocks
                    Visit(catchBlock.Body)
                Next
                Visit(node.FinallyBlockOpt)
                Return Nothing
            End If

            Dim entry = Capture()
            _exceptionStates.Add(Unreachable())
            ObserveException()
            Visit(node.TryBlock)
            If _reachable Then
                ObserveException()
            End If

            Dim exceptional = _exceptionStates(_exceptionStates.Count - 1)
            _exceptionStates.RemoveAt(_exceptionStates.Count - 1)
            Dim tryEnd = Capture()
            Dim tryExits = If(node.ExitLabelOpt Is Nothing, Unreachable(), JumpSnapshot(node.ExitLabelOpt))
            Dim catchJoin = Unreachable()
            Dim handlerState = If(exceptional.Reachable, exceptional, entry)
            For Each catchBlock In node.CatchBlocks
                ' Every reachable state in the try can reach the handler, including a null
                ' assignment that a later statement overwrites on the normal path.
                SetSnapshot(CloneSnapshot(handlerState))
                If catchBlock.LocalOpt IsNot Nothing AndAlso IsTrackedReference(catchBlock.LocalOpt) Then
                    _state(catchBlock.LocalOpt) = FlowNullability.NotNull
                End If

                If catchBlock.ExceptionFilterOpt IsNot Nothing Then
                    Evaluate(catchBlock.ExceptionFilterOpt)
                End If

                Visit(catchBlock.Body)
                catchJoin = MergeSnapshots(catchJoin, Capture())
            Next

            Dim leaving = MergeSnapshots(tryEnd, tryExits)
            If node.CatchBlocks.Length > 0 Then
                leaving = MergeSnapshots(leaving, catchJoin)
            End If

            If node.FinallyBlockOpt IsNot Nothing Then
                Dim clock = _writeClock
                SetSnapshot(MergeSnapshots(handlerState, leaving))
                Visit(node.FinallyBlockOpt)
                If leaving.Reachable AndAlso _reachable Then
                    ApplyFinallyWrites(leaving, clock)
                Else
                    _reachable = False
                End If
            Else
                SetSnapshot(leaving)
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitReturnStatement(node As BoundReturnStatement) As BoundNode
            If Not _reachable Then
                Return Nothing
            End If

            If node.ExpressionOpt IsNot Nothing Then
                Dim result = Evaluate(node.ExpressionOpt)
                If result = FlowNullability.MaybeNull AndAlso ReturnDisallowsNull() Then
                    Warn(ERRID.WRN_PossibleNullReturn, node.ExpressionOpt.Syntax)
                End If
            End If

            _reachable = False
            Return Nothing
        End Function

        Public Overrides Function VisitThrowStatement(node As BoundThrowStatement) As BoundNode
            If Not _reachable Then
                Return Nothing
            End If

            Evaluate(node.ExpressionOpt)
            _reachable = False
            Return Nothing
        End Function

        Public Overrides Function VisitExitStatement(node As BoundExitStatement) As BoundNode
            If _reachable Then
                AddJump(node.Label, Capture())
                _reachable = False
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitContinueStatement(node As BoundContinueStatement) As BoundNode
            If _reachable Then
                AddJump(node.Label, Capture())
                _reachable = False
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitGotoStatement(node As BoundGotoStatement) As BoundNode
            If _reachable Then
                AddJump(node.Label, Capture())
                _reachable = False
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitLabelStatement(node As BoundLabelStatement) As BoundNode
            _visitedLabels.Add(node.Label)
            Dim incoming As FlowSnapshot = Nothing
            If _jumps.TryGetValue(node.Label, incoming) AndAlso incoming.Reachable Then
                If _reachable Then
                    SetSnapshot(MergeSnapshots(Capture(), incoming))
                Else
                    SetSnapshot(CloneSnapshot(incoming))
                End If
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitLocalDeclaration(node As BoundLocalDeclaration) As BoundNode
            If Not _reachable Then
                Return Nothing
            End If

            If node.LocalSymbol.IsStatic AndAlso IsTrackedReference(node.LocalSymbol) Then
                _captured.Add(node.LocalSymbol)
            End If

            If node.DeclarationInitializerOpt IsNot Nothing Then
                Dim value = Evaluate(node.DeclarationInitializerOpt)
                Assign(node.LocalSymbol, value, node.DeclarationInitializerOpt)
            ElseIf node.IdentifierInitializerOpt IsNot Nothing Then
                Evaluate(node.IdentifierInitializerOpt)
                If IsTrackedReference(node.LocalSymbol) Then
                    _state(node.LocalSymbol) = FlowNullability.NotNull
                End If
            ElseIf node.InitializedByAsNew Then
                If IsTrackedReference(node.LocalSymbol) Then
                    _state(node.LocalSymbol) = FlowNullability.NotNull
                End If
            ElseIf IsTrackedReference(node.LocalSymbol) Then
                Dim initial = InitialNullability(node.LocalSymbol)
                If AnnotationOf(node.LocalSymbol) = NullableAnnotation.Annotated Then
                    initial = FlowNullability.MaybeNull
                End If

                _state(node.LocalSymbol) = initial
            End If

            If node.LocalSymbol.IsStatic AndAlso IsTrackedReference(node.LocalSymbol) Then
                _state(node.LocalSymbol) = FlowNullability.MaybeNull
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitAssignmentOperator(node As BoundAssignmentOperator) As BoundNode
            If Not _reachable Then
                Return Nothing
            End If

            Dim target = TrackedSymbol(node.Left)
            Dim compound = node.LeftOnTheRightOpt IsNot Nothing
            If compound OrElse target Is Nothing Then
                Evaluate(node.Left)
            End If

            Dim value = Evaluate(node.Right)
            If compound AndAlso node.Type IsNot Nothing AndAlso node.Type.SpecialType = SpecialType.System_String Then
                value = FlowNullability.NotNull
            End If

            If target IsNot Nothing Then
                Assign(target, value, node.Right)
            Else
                AfterInvocation()
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitBinaryOperator(node As BoundBinaryOperator) As BoundNode
            Evaluate(node)
            Return Nothing
        End Function

        Public Overrides Function VisitUnaryOperator(node As BoundUnaryOperator) As BoundNode
            Evaluate(node)
            Return Nothing
        End Function

        Public Overrides Function VisitCall(node As BoundCall) As BoundNode
            Evaluate(node)
            Return Nothing
        End Function

        Public Overrides Function VisitPropertyAccess(node As BoundPropertyAccess) As BoundNode
            Evaluate(node)
            Return Nothing
        End Function

        Public Overrides Function VisitLambda(node As BoundLambda) As BoundNode
            VisitIsolated(node.Body, node.LambdaSymbol.Parameters)
            Return Nothing
        End Function

        Public Overrides Function VisitQueryLambda(node As BoundQueryLambda) As BoundNode
            If Not _reachable Then
                Return Nothing
            End If

            Dim saved = Capture()
            NoteCaptured(node.Expression)
            AssumeCapturedMayBeNull()
            Evaluate(node.Expression)
            SetSnapshot(saved)
            Return Nothing
        End Function

        Private Sub VisitPretestedLoop(condition As BoundExpression, body As BoundStatement, continueLabel As LabelSymbol, exitLabel As LabelSymbol, untilCondition As Boolean)
            Dim entry = Capture()
            Dim head = CloneSnapshot(entry)
            Dim exitState = Unreachable()
            Dim stable = False
            For iteration = 1 To MaxLoopIterations
                Dim newHead = StepPretested(entry, head, condition, body, continueLabel, untilCondition, exitState)
                If Same(newHead, head) Then
                    stable = True
                    Exit For
                End If

                head = newHead
            Next

            If Not stable Then
                StepPretested(entry, Widen(head), condition, body, continueLabel, untilCondition, exitState)
            End If

            SetSnapshot(MergeSnapshots(exitState, JumpSnapshot(exitLabel)))
        End Sub

        Private Function StepPretested(entry As FlowSnapshot, head As FlowSnapshot, condition As BoundExpression, body As BoundStatement, continueLabel As LabelSymbol, untilCondition As Boolean, ByRef exitState As FlowSnapshot) As FlowSnapshot
            SetSnapshot(CloneSnapshot(head))
            Dim split = SplitCondition(condition)
            Dim enter = If(untilCondition, split.WhenFalse, split.WhenTrue)
            Dim leave = If(untilCondition, split.WhenTrue, split.WhenFalse)
            exitState = MergeSnapshots(exitState, leave)
            SetSnapshot(enter)
            Visit(body)
            Dim back = MergeSnapshots(Capture(), JumpSnapshot(continueLabel))
            Return MergeSnapshots(entry, back)
        End Function

        Private Sub VisitPosttestedLoop(condition As BoundExpression, body As BoundStatement, continueLabel As LabelSymbol, exitLabel As LabelSymbol, untilCondition As Boolean)
            Dim entry = Capture()
            Dim head = CloneSnapshot(entry)
            Dim exitState = Unreachable()
            Dim stable = False
            For iteration = 1 To MaxLoopIterations
                Dim newHead = StepPosttested(entry, head, condition, body, continueLabel, untilCondition, exitState)
                If Same(newHead, head) Then
                    stable = True
                    Exit For
                End If

                head = newHead
            Next

            If Not stable Then
                StepPosttested(entry, Widen(head), condition, body, continueLabel, untilCondition, exitState)
            End If

            SetSnapshot(MergeSnapshots(exitState, JumpSnapshot(exitLabel)))
        End Sub

        Private Function StepPosttested(entry As FlowSnapshot, head As FlowSnapshot, condition As BoundExpression, body As BoundStatement, continueLabel As LabelSymbol, untilCondition As Boolean, ByRef exitState As FlowSnapshot) As FlowSnapshot
            SetSnapshot(CloneSnapshot(head))
            Visit(body)
            Dim afterBody = MergeSnapshots(Capture(), JumpSnapshot(continueLabel))
            SetSnapshot(afterBody)
            Dim split = SplitCondition(condition)
            Dim back = If(untilCondition, split.WhenFalse, split.WhenTrue)
            Dim leave = If(untilCondition, split.WhenTrue, split.WhenFalse)
            exitState = MergeSnapshots(exitState, leave)
            Return MergeSnapshots(entry, back)
        End Function

        Private Sub VisitOpenLoop(body As BoundStatement, continueLabel As LabelSymbol, exitLabel As LabelSymbol)
            Dim entry = Capture()
            Dim head = CloneSnapshot(entry)
            Dim stable = False
            For iteration = 1 To MaxLoopIterations
                Dim newHead = StepOpen(entry, head, body, continueLabel)
                If Same(newHead, head) Then
                    stable = True
                    Exit For
                End If

                head = newHead
            Next

            If Not stable Then
                StepOpen(entry, Widen(head), body, continueLabel)
            End If

            SetSnapshot(JumpSnapshot(exitLabel))
        End Sub

        Private Function StepOpen(entry As FlowSnapshot, head As FlowSnapshot, body As BoundStatement, continueLabel As LabelSymbol) As FlowSnapshot
            SetSnapshot(CloneSnapshot(head))
            Visit(body)
            Dim back = MergeSnapshots(Capture(), JumpSnapshot(continueLabel))
            Return MergeSnapshots(entry, back)
        End Function

        ''' <summary>
        ''' A For or For Each may execute zero times. The body state is merged with the entry state,
        ''' and a reference iteration variable is maybe-null after the loop because an empty collection
        ''' leaves it unset.
        ''' </summary>
        Private Sub VisitRepeatableBody(body As BoundStatement, continueLabel As LabelSymbol, exitLabel As LabelSymbol, control As Symbol, elementState As FlowNullability)
            Dim entry = Capture()
            Dim head = CloneSnapshot(entry)
            Dim exitState = Unreachable()
            Dim stable = False
            For iteration = 1 To MaxLoopIterations
                Dim newHead = StepRepeatable(entry, head, body, continueLabel, control, elementState, exitState)
                If Same(newHead, head) Then
                    stable = True
                    Exit For
                End If

                head = newHead
            Next

            If Not stable Then
                StepRepeatable(entry, Widen(head), body, continueLabel, control, elementState, exitState)
            End If

            exitState = MergeSnapshots(exitState, entry)
            exitState = MergeSnapshots(exitState, JumpSnapshot(exitLabel))
            If control IsNot Nothing AndAlso exitState.Reachable Then
                exitState = CloneSnapshot(exitState)
                exitState.Slots(control) = FlowNullability.MaybeNull
            End If

            SetSnapshot(exitState)
        End Sub

        Private Function StepRepeatable(entry As FlowSnapshot, head As FlowSnapshot, body As BoundStatement, continueLabel As LabelSymbol, control As Symbol, elementState As FlowNullability, ByRef exitState As FlowSnapshot) As FlowSnapshot
            SetSnapshot(CloneSnapshot(head))
            If control IsNot Nothing Then
                _state(control) = elementState
            End If

            Visit(body)
            Dim afterBody = MergeSnapshots(Capture(), JumpSnapshot(continueLabel))
            exitState = MergeSnapshots(exitState, afterBody)
            Return MergeSnapshots(entry, afterBody)
        End Function

        Private Sub VisitIsolated(body As BoundNode, parameters As ImmutableArray(Of ParameterSymbol))
            If Not _reachable OrElse body Is Nothing Then
                Return
            End If

            Dim saved = Capture()
            NoteCaptured(body)
            _isolation += 1
            For Each parameter In parameters
                If IsTrackedReference(parameter) Then
                    _state(parameter) = InitialNullability(parameter)
                End If
            Next

            AssumeCapturedMayBeNull()
            Visit(body)
            _isolation -= 1
            SetSnapshot(saved)
        End Sub

        Private Function SplitCondition(expr As BoundExpression) As ConditionSplit
            Dim split As New ConditionSplit()
            If expr Is Nothing OrElse Not _reachable Then
                split.WhenTrue = Unreachable()
                split.WhenFalse = Unreachable()
                Return split
            End If

            expr = Unwrap(expr)
            If expr.Kind = BoundKind.NullableIsTrueOperator Then
                Dim operand = DirectCast(expr, BoundNullableIsTrueOperator).Operand
                If operand IsNot Nothing AndAlso operand.Type IsNot Nothing AndAlso operand.Type.SpecialType = SpecialType.System_Boolean Then
                    Return SplitCondition(operand)
                End If

                Evaluate(operand)
                Dim snapped = Capture()
                split.WhenTrue = snapped
                split.WhenFalse = CloneSnapshot(snapped)
                Return split
            End If

            If expr.ConstantValueOpt IsNot Nothing AndAlso expr.ConstantValueOpt.IsBoolean Then
                Dim here = Capture()
                Dim dead = Unreachable()
                If expr.ConstantValueOpt.BooleanValue Then
                    split.WhenTrue = here
                    split.WhenFalse = dead
                Else
                    split.WhenTrue = dead
                    split.WhenFalse = here
                End If

                Return split
            End If

            Dim unary = TryCast(expr, BoundUnaryOperator)
            If unary IsNot Nothing AndAlso IsBuiltInNot(unary) Then
                Dim inner = SplitCondition(unary.Operand)
                split.WhenTrue = inner.WhenFalse
                split.WhenFalse = inner.WhenTrue
                Return split
            End If

            Dim shortCircuit = TryCast(expr, BoundUserDefinedShortCircuitingOperator)
            If shortCircuit IsNot Nothing Then
                Evaluate(shortCircuit.LeftOperand)
                Evaluate(shortCircuit.LeftTest)
                Evaluate(shortCircuit.BitwiseOperator)
                Dim here = Capture()
                split.WhenTrue = here
                split.WhenFalse = CloneSnapshot(here)
                Return split
            End If

            Dim binary = TryCast(expr, BoundBinaryOperator)
            If binary IsNot Nothing Then
                Dim kind = binary.OperatorKind
                Dim op = kind And BinaryOperatorKind.OpMask
                Dim ordinary = (kind And (BinaryOperatorKind.Lifted Or BinaryOperatorKind.UserDefined)) = 0
                If ordinary AndAlso (op = BinaryOperatorKind.AndAlso OrElse op = BinaryOperatorKind.OrElse) Then
                    Dim left = SplitCondition(binary.Left)
                    If op = BinaryOperatorKind.AndAlso Then
                        SetSnapshot(left.WhenTrue)
                        Dim right = SplitCondition(binary.Right)
                        split.WhenTrue = right.WhenTrue
                        split.WhenFalse = MergeSnapshots(left.WhenFalse, right.WhenFalse)
                    Else
                        SetSnapshot(left.WhenFalse)
                        Dim right = SplitCondition(binary.Right)
                        split.WhenTrue = MergeSnapshots(left.WhenTrue, right.WhenTrue)
                        split.WhenFalse = right.WhenFalse
                    End If

                    Return split
                End If

                If ordinary AndAlso (op = BinaryOperatorKind.Is OrElse op = BinaryOperatorKind.IsNot) Then
                    Dim tested As Symbol = Nothing
                    Dim trueWhenNull = False
                    Evaluate(binary.Left)
                    Evaluate(binary.Right)
                    If TryGetNullTest(binary, tested, trueWhenNull) Then
                        Dim baseState = Capture()
                        SetSnapshot(CloneSnapshot(baseState))
                        Learn(tested, trueWhenNull)
                        split.WhenTrue = Capture()
                        SetSnapshot(CloneSnapshot(baseState))
                        Learn(tested, Not trueWhenNull)
                        split.WhenFalse = Capture()
                        Return split
                    End If

                    Dim here = Capture()
                    split.WhenTrue = here
                    split.WhenFalse = CloneSnapshot(here)
                    Return split
                End If

                If ordinary AndAlso (op = BinaryOperatorKind.And OrElse op = BinaryOperatorKind.Or) Then
                    ' Both operands run. A fact from the left is not reapplied when the right writes that slot.
                    Evaluate(binary.Left)
                    Dim leftWrites = New Dictionary(Of Symbol, Integer)(_writes)
                    Evaluate(binary.Right)
                    Dim after = Capture()
                    split.WhenTrue = CloneSnapshot(after)
                    split.WhenFalse = CloneSnapshot(after)
                    If op = BinaryOperatorKind.And Then
                        SetSnapshot(split.WhenTrue)
                        ApplyFacts(binary.Left, True, leftWrites)
                        ApplyFacts(binary.Right, True, Nothing)
                        split.WhenTrue = Capture()
                    Else
                        SetSnapshot(split.WhenFalse)
                        ApplyFacts(binary.Left, False, leftWrites)
                        ApplyFacts(binary.Right, False, Nothing)
                        split.WhenFalse = Capture()
                    End If

                    SetSnapshot(after)
                    Return split
                End If
            End If

            Dim typeOfNode = TryCast(expr, BoundTypeOf)
            If typeOfNode IsNot Nothing Then
                Return SplitTypeOf(typeOfNode.Operand, typeOfNode.IsTypeOfIsNotExpression, Nothing)
            End If

            Dim typeOfDeclaration = TryCast(expr, BoundTypeOfDeclaration)
            If typeOfDeclaration IsNot Nothing Then
                Return SplitTypeOf(typeOfDeclaration.Operand, isNotExpression:=False, typeOfDeclaration.Local)
            End If

            Evaluate(expr)
            Dim current = Capture()
            split.WhenTrue = current
            split.WhenFalse = CloneSnapshot(current)
            Return split
        End Function

        Private Function SplitTypeOf(operand As BoundExpression, isNotExpression As Boolean, declared As LocalSymbol) As ConditionSplit
            Evaluate(operand)
            Dim unchanged = Capture()
            Dim symbol = TrackedSymbol(operand)
            If symbol IsNot Nothing Then
                Learn(symbol, isNull:=False)
            End If

            If declared IsNot Nothing AndAlso IsTrackedReference(declared) Then
                _state(declared) = FlowNullability.NotNull
            End If

            Dim whenMatched = Capture()
            Dim split As New ConditionSplit()
            If isNotExpression Then
                split.WhenTrue = unchanged
                split.WhenFalse = whenMatched
            Else
                split.WhenTrue = whenMatched
                split.WhenFalse = unchanged
            End If

            Return split
        End Function

        Private Function Evaluate(expr As BoundExpression) As FlowNullability
            If expr Is Nothing OrElse Not _reachable Then
                Return FlowNullability.NotNull
            End If

            Select Case expr.Kind
                Case BoundKind.Parenthesized
                    Return Evaluate(DirectCast(expr, BoundParenthesized).Expression)
                Case BoundKind.Literal
                    If expr.ConstantValueOpt IsNot Nothing AndAlso expr.ConstantValueOpt.IsNothing Then
                        Return FlowNullability.MaybeNull
                    End If

                    Return FlowNullability.NotNull
                Case BoundKind.Local, BoundKind.Parameter
                    Return StateOf(TrackedSymbol(expr))
                Case BoundKind.Conversion, BoundKind.DirectCast
                    Dim inner = Evaluate(DirectCast(expr, BoundConversionOrCast).Operand)
                    If expr.Type Is Nothing OrElse Not expr.Type.IsReferenceType Then
                        Return FlowNullability.NotNull
                    End If

                    Return inner
                Case BoundKind.UserDefinedConversion
                    Return EvaluateUserDefinedConversion(DirectCast(expr, BoundUserDefinedConversion))
                Case BoundKind.TryCast
                    Evaluate(DirectCast(expr, BoundTryCast).Operand)
                    If expr.Type IsNot Nothing AndAlso expr.Type.IsReferenceType Then
                        Return FlowNullability.MaybeNull
                    End If

                    Return FlowNullability.NotNull
                Case BoundKind.Call
                    Return EvaluateCall(DirectCast(expr, BoundCall))
                Case BoundKind.PropertyAccess
                    Return EvaluateProperty(DirectCast(expr, BoundPropertyAccess))
                Case BoundKind.FieldAccess
                    Return EvaluateField(DirectCast(expr, BoundFieldAccess))
                Case BoundKind.ObjectCreationExpression
                    Return EvaluateObjectCreation(DirectCast(expr, BoundObjectCreationExpression))
                Case BoundKind.BinaryOperator
                    Return EvaluateBinary(DirectCast(expr, BoundBinaryOperator))
                Case BoundKind.UnaryOperator
                    Dim unary = DirectCast(expr, BoundUnaryOperator)
                    If IsBuiltInNot(unary) Then
                        Dim split = SplitCondition(expr)
                        SetSnapshot(MergeSnapshots(split.WhenTrue, split.WhenFalse))
                        Return FlowNullability.NotNull
                    End If

                    Dim operand = Evaluate(unary.Operand)
                    If expr.Type IsNot Nothing AndAlso expr.Type.IsReferenceType Then
                        Return operand
                    End If

                    Return FlowNullability.NotNull
                Case BoundKind.UserDefinedShortCircuitingOperator, BoundKind.NullableIsTrueOperator
                    Dim conditional = SplitCondition(expr)
                    SetSnapshot(MergeSnapshots(conditional.WhenTrue, conditional.WhenFalse))
                    Return FlowNullability.NotNull
                Case BoundKind.UserDefinedBinaryOperator
                    Evaluate(DirectCast(expr, BoundUserDefinedBinaryOperator).UnderlyingExpression)
                    Return If(expr.Type IsNot Nothing AndAlso expr.Type.IsReferenceType, FlowNullability.Unknown, FlowNullability.NotNull)
                Case BoundKind.UserDefinedUnaryOperator
                    Evaluate(DirectCast(expr, BoundUserDefinedUnaryOperator).UnderlyingExpression)
                    Return If(expr.Type IsNot Nothing AndAlso expr.Type.IsReferenceType, FlowNullability.Unknown, FlowNullability.NotNull)
                Case BoundKind.TernaryConditionalExpression
                    Return EvaluateTernary(DirectCast(expr, BoundTernaryConditionalExpression))
                Case BoundKind.BinaryConditionalExpression
                    Return EvaluateIfOperator(DirectCast(expr, BoundBinaryConditionalExpression))
                Case BoundKind.ConditionalAccess
                    Return EvaluateConditionalAccess(DirectCast(expr, BoundConditionalAccess))
                Case BoundKind.TypeOf, BoundKind.TypeOfDeclaration
                    Dim split = SplitCondition(expr)
                    SetSnapshot(MergeSnapshots(split.WhenTrue, split.WhenFalse))
                    Return FlowNullability.NotNull
                Case BoundKind.Lambda
                    VisitLambda(DirectCast(expr, BoundLambda))
                    Return FlowNullability.NotNull
                Case BoundKind.QueryLambda
                    VisitQueryLambda(DirectCast(expr, BoundQueryLambda))
                    Return FlowNullability.NotNull
                Case Else
                    Visit(expr)
                    If expr.Type IsNot Nothing AndAlso expr.Type.IsReferenceType Then
                        Return FlowNullability.Unknown
                    End If

                    Return FlowNullability.NotNull
            End Select
        End Function

        Private Function EvaluateBinary(node As BoundBinaryOperator) As FlowNullability
            Dim op = node.OperatorKind And BinaryOperatorKind.OpMask
            If op = BinaryOperatorKind.AndAlso OrElse op = BinaryOperatorKind.OrElse OrElse
               op = BinaryOperatorKind.And OrElse op = BinaryOperatorKind.Or OrElse
               op = BinaryOperatorKind.Is OrElse op = BinaryOperatorKind.IsNot Then
                Dim split = SplitCondition(node)
                SetSnapshot(MergeSnapshots(split.WhenTrue, split.WhenFalse))
                Return FlowNullability.NotNull
            End If

            Dim left = Evaluate(node.Left)
            Dim right = Evaluate(node.Right)
            If op = BinaryOperatorKind.Concatenate Then
                Return FlowNullability.NotNull
            End If

            If node.Type IsNot Nothing AndAlso node.Type.IsReferenceType Then
                Return MergeNull(left, right)
            End If

            Return FlowNullability.NotNull
        End Function

        Private Function EvaluateCall(node As BoundCall) As FlowNullability
            EvaluateReceiver(node.ReceiverOpt)
            CheckArguments(node.Method, node.Arguments)
            AfterInvocation()
            Return CallResult(node.Method)
        End Function

        Private Function EvaluateProperty(node As BoundPropertyAccess) As FlowNullability
            EvaluateReceiver(node.ReceiverOpt)
            If Not node.Arguments.IsDefault Then
                For Each argument In node.Arguments
                    Evaluate(argument)
                Next
            End If

            AfterInvocation()
            Return FromAnnotation(node.PropertySymbol.GetNullableAnnotation())
        End Function

        Private Function EvaluateField(node As BoundFieldAccess) As FlowNullability
            EvaluateReceiver(node.ReceiverOpt)
            ' Field assignment state is LC-1F.3. A read uses the declared annotation only.
            Return FromAnnotation(node.FieldSymbol.GetNullableAnnotation())
        End Function

        Private Function EvaluateObjectCreation(node As BoundObjectCreationExpression) As FlowNullability
            CheckArguments(node.ConstructorOpt, node.Arguments)
            If node.InitializerOpt IsNot Nothing Then
                Visit(node.InitializerOpt)
            End If

            AfterInvocation()
            Return FlowNullability.NotNull
        End Function

        Private Function EvaluateTernary(node As BoundTernaryConditionalExpression) As FlowNullability
            Dim split = SplitCondition(node.Condition)
            SetSnapshot(split.WhenTrue)
            Dim whenTrue = Evaluate(node.WhenTrue)
            Dim afterTrue = Capture()
            SetSnapshot(split.WhenFalse)
            Dim whenFalse = Evaluate(node.WhenFalse)
            Dim afterFalse = Capture()
            SetSnapshot(MergeSnapshots(afterTrue, afterFalse))
            If Not afterTrue.Reachable Then
                Return whenFalse
            End If

            If Not afterFalse.Reachable Then
                Return whenTrue
            End If

            Return MergeNull(whenTrue, whenFalse)
        End Function

        Private Function EvaluateIfOperator(node As BoundBinaryConditionalExpression) As FlowNullability
            Dim test = Evaluate(node.TestExpression)
            If node.TestExpression IsNot Nothing AndAlso node.TestExpression.ConstantValueOpt IsNot Nothing AndAlso node.TestExpression.ConstantValueOpt.IsNothing Then
                Return Evaluate(node.ElseExpression)
            End If

            If test = FlowNullability.NotNull Then
                Return FlowNullability.NotNull
            End If

            Dim whenPresent = Capture()
            Dim symbol = TrackedSymbol(node.TestExpression)
            If symbol IsNot Nothing Then
                Learn(symbol, isNull:=False)
            End If

            Dim present = Capture()
            SetSnapshot(whenPresent)
            If symbol IsNot Nothing Then
                Learn(symbol, isNull:=True)
            End If

            Dim alternative = Evaluate(node.ElseExpression)
            Dim absent = Capture()
            SetSnapshot(MergeSnapshots(present, absent))
            Return MergeNull(FlowNullability.NotNull, alternative)
        End Function

        Private Function EvaluateConditionalAccess(node As BoundConditionalAccess) As FlowNullability
            ' Conditional access does not dereference a null receiver, so the receiver itself is not a warning.
            Dim receiver = Evaluate(node.Receiver)
            Dim whenAbsent = Capture()
            Dim symbol = TrackedSymbol(node.Receiver)
            If symbol IsNot Nothing Then
                Learn(symbol, isNull:=False)
            End If

            Dim access = Evaluate(node.AccessExpression)
            Dim whenPresent = Capture()
            If receiver = FlowNullability.NotNull Then
                SetSnapshot(whenPresent)
                Return access
            End If

            SetSnapshot(MergeSnapshots(whenAbsent, whenPresent))
            Return FlowNullability.MaybeNull
        End Function

        Private Function EvaluateReceiver(receiver As BoundExpression) As FlowNullability
            If receiver Is Nothing OrElse Not _reachable Then
                Return FlowNullability.NotNull
            End If

            Select Case receiver.Kind
                Case BoundKind.TypeExpression, BoundKind.MeReference, BoundKind.MyBaseReference, BoundKind.MyClassReference
                    Return FlowNullability.NotNull
            End Select

            Dim state = Evaluate(receiver)
            If state = FlowNullability.MaybeNull AndAlso receiver.Type IsNot Nothing AndAlso receiver.Type.IsReferenceType Then
                WarnReceiverSyntax(receiver)
            End If

            Return state
        End Function

        Private Sub WarnReceiverSyntax(receiver As BoundExpression)
            Dim symbol = TrackedSymbol(receiver)
            Dim name = If(symbol Is Nothing, "expression", symbol.Name)
            Warn(ERRID.WRN_PossibleNullReference, receiver.Syntax, name)
        End Sub

        Private Sub CheckArguments(method As MethodSymbol, arguments As ImmutableArray(Of BoundExpression))
            If arguments.IsDefault Then
                Return
            End If

            Dim count = 0
            If method IsNot Nothing Then
                count = Math.Min(arguments.Length, method.ParameterCount)
            End If

            For i = 0 To arguments.Length - 1
                Dim parameter As ParameterSymbol = Nothing
                If method IsNot Nothing AndAlso i < count AndAlso Not method.Parameters(i).IsParamArray Then
                    parameter = method.Parameters(i)
                End If

                Dim value = Evaluate(ArgumentValue(arguments(i)))
                If parameter IsNot Nothing AndAlso value = FlowNullability.MaybeNull AndAlso ArgumentDisallowsNull(parameter) Then
                    Warn(ERRID.WRN_PossibleNullArgument, arguments(i).Syntax, parameter.Name)
                End If
            Next

            If method Is Nothing Then
                Return
            End If

            For i = 0 To count - 1
                Dim parameter = method.Parameters(i)
                If parameter.IsParamArray OrElse Not parameter.IsByRef Then
                    Continue For
                End If

                Dim target = TrackedSymbol(ArgumentValue(arguments(i)))
                If target Is Nothing OrElse Not IsTrackedReference(target) Then
                    Continue For
                End If

                ' NotNull ByRef keeps a non-null result. Annotated or oblivious ByRef does not.
                If AnnotationOf(parameter) = NullableAnnotation.NotAnnotated Then
                    _state(target) = FlowNullability.NotNull
                Else
                    _state(target) = FlowNullability.MaybeNull
                End If

                NoteWrite(target)
            Next
        End Sub

        Private Shared Function ArgumentValue(argument As BoundExpression) As BoundExpression
            If argument IsNot Nothing AndAlso argument.Kind = BoundKind.ByRefArgumentWithCopyBack Then
                Return DirectCast(argument, BoundByRefArgumentWithCopyBack).OriginalArgument
            End If

            Return argument
        End Function

        Private Sub AfterInvocation()
            If Not _reachable OrElse _captured.Count = 0 Then
                Return
            End If

            For Each symbol In _captured
                If IsTrackedReference(symbol) Then
                    _state(symbol) = FlowNullability.MaybeNull
                    NoteWrite(symbol)
                End If
            Next
        End Sub

        Private Sub AssumeCapturedMayBeNull()
            For Each symbol In _captured
                If IsTrackedReference(symbol) AndAlso symbol.ContainingSymbol Is _method Then
                    _state(symbol) = FlowNullability.MaybeNull
                End If
            Next
        End Sub

        Private Sub NoteCaptured(node As BoundNode)
            If node Is Nothing Then
                Return
            End If

            Dim walker As New CaptureWalker(_method, _captured)
            walker.Visit(node)
        End Sub

        Private Sub ApplyFacts(expr As BoundExpression, sense As Boolean, baseline As Dictionary(Of Symbol, Integer))
            expr = Unwrap(expr)
            If expr Is Nothing Then
                Return
            End If

            Dim unary = TryCast(expr, BoundUnaryOperator)
            If unary IsNot Nothing AndAlso IsBuiltInNot(unary) Then
                ApplyFacts(unary.Operand, Not sense, baseline)
                Return
            End If

            Dim binary = TryCast(expr, BoundBinaryOperator)
            If binary IsNot Nothing Then
                Dim kind = binary.OperatorKind
                If (kind And (BinaryOperatorKind.Lifted Or BinaryOperatorKind.UserDefined)) <> 0 Then
                    Return
                End If

                Dim op = kind And BinaryOperatorKind.OpMask
                If op = BinaryOperatorKind.And OrElse op = BinaryOperatorKind.AndAlso Then
                    If sense Then
                        ApplyFacts(binary.Left, True, baseline)
                        ApplyFacts(binary.Right, True, baseline)
                    End If

                    Return
                End If

                If op = BinaryOperatorKind.Or OrElse op = BinaryOperatorKind.OrElse Then
                    If Not sense Then
                        ApplyFacts(binary.Left, False, baseline)
                        ApplyFacts(binary.Right, False, baseline)
                    End If

                    Return
                End If

                Dim tested As Symbol = Nothing
                Dim trueWhenNull = False
                If TryGetNullTest(binary, tested, trueWhenNull) Then
                    LearnIfStable(tested, If(sense, trueWhenNull, Not trueWhenNull), baseline)
                End If

                Return
            End If

            Dim typeOfNode = TryCast(expr, BoundTypeOf)
            If typeOfNode IsNot Nothing AndAlso sense <> typeOfNode.IsTypeOfIsNotExpression Then
                LearnIfStable(TrackedSymbol(typeOfNode.Operand), isNull:=False, baseline)
            End If
        End Sub

        Private Sub LearnIfStable(symbol As Symbol, isNull As Boolean, baseline As Dictionary(Of Symbol, Integer))
            If symbol IsNot Nothing AndAlso baseline IsNot Nothing Then
                Dim thenStamp = 0
                baseline.TryGetValue(symbol, thenStamp)
                Dim nowStamp = 0
                _writes.TryGetValue(symbol, nowStamp)
                If nowStamp <> thenStamp Then
                    Return
                End If
            End If

            Learn(symbol, isNull)
        End Sub

        Private Sub Learn(symbol As Symbol, isNull As Boolean)
            If symbol Is Nothing OrElse Not IsTrackedReference(symbol) Then
                Return
            End If

            _state(symbol) = If(isNull, FlowNullability.MaybeNull, FlowNullability.NotNull)
        End Sub

        Private Sub Assign(target As Symbol, value As FlowNullability, syntaxExpr As BoundExpression)
            If target Is Nothing OrElse Not IsTrackedReference(target) Then
                Return
            End If

            Dim effective = value
            If effective = FlowNullability.Unknown AndAlso AnnotationOf(target) = NullableAnnotation.Annotated Then
                effective = FlowNullability.MaybeNull
            End If

            If effective = FlowNullability.MaybeNull AndAlso AnnotationOf(target) = NullableAnnotation.NotAnnotated Then
                Warn(ERRID.WRN_PossibleNullAssignment, syntaxExpr.Syntax, target.Name)
            End If

            _state(target) = effective
            NoteWrite(target)
        End Sub

        Private Function StateOf(symbol As Symbol) As FlowNullability
            If symbol Is Nothing OrElse Not IsTrackedReference(symbol) Then
                Return FlowNullability.NotNull
            End If

            Dim state As FlowNullability
            If _state.TryGetValue(symbol, state) Then
                Return state
            End If

            Return InitialNullability(symbol)
        End Function

        Private Function InitialNullability(symbol As Symbol) As FlowNullability
            Return FromAnnotation(AnnotationOf(symbol))
        End Function

        Private Shared Function FromAnnotation(annotation As NullableAnnotation) As FlowNullability
            Select Case annotation
                Case NullableAnnotation.Annotated
                    Return FlowNullability.MaybeNull
                Case NullableAnnotation.NotAnnotated
                    Return FlowNullability.NotNull
                Case Else
                    Return FlowNullability.Unknown
            End Select
        End Function

        Private Shared Function CallResult(method As MethodSymbol) As FlowNullability
            If method Is Nothing OrElse AnnotationOf(method) = NullableAnnotation.None Then
                ' Unconditional contracts on an oblivious declaration stay deferred to LC-1F.2.
                Return FlowNullability.Unknown
            End If

            Dim annotations = method.ReturnTypeFlowAnalysisAnnotations
            If HasContract(annotations, FlowAnalysisAnnotations.NotNull) Then
                Return FlowNullability.NotNull
            End If

            If HasContract(annotations, FlowAnalysisAnnotations.MaybeNull) Then
                Return FlowNullability.MaybeNull
            End If

            Return FromAnnotation(AnnotationOf(method))
        End Function

        Private Function ReturnDisallowsNull() As Boolean
            If AnnotationOf(_method) = NullableAnnotation.None Then
                Return False
            End If

            Return DisallowsNull(AnnotationOf(_method) = NullableAnnotation.NotAnnotated, _method.ReturnTypeFlowAnalysisAnnotations, outputContract:=True)
        End Function

        Private Function ArgumentDisallowsNull(parameter As ParameterSymbol) As Boolean
            Dim declared = AnnotationOf(parameter)
            If declared = NullableAnnotation.None Then
                Return False
            End If

            Return DisallowsNull(declared = NullableAnnotation.NotAnnotated, parameter.FlowAnalysisAnnotations, outputContract:=False)
        End Function

        Private Shared Function DisallowsNull(declaredNotNull As Boolean, annotations As FlowAnalysisAnnotations, outputContract As Boolean) As Boolean
            Dim allow = If(outputContract, FlowAnalysisAnnotations.MaybeNull, FlowAnalysisAnnotations.AllowNull)
            Dim disallow = If(outputContract, FlowAnalysisAnnotations.NotNull, FlowAnalysisAnnotations.DisallowNull)
            If HasContract(annotations, allow) AndAlso Not HasContract(annotations, disallow) Then
                Return False
            End If

            If HasContract(annotations, disallow) Then
                Return True
            End If

            Return declaredNotNull
        End Function

        Private Shared Function HasContract(annotations As FlowAnalysisAnnotations, contract As FlowAnalysisAnnotations) As Boolean
            Return (annotations And contract) = contract
        End Function

        Private Shared Function IsTrackedReference(symbol As Symbol) As Boolean
            Dim local = TryCast(symbol, LocalSymbol)
            If local IsNot Nothing Then
                Return Not local.IsConst AndAlso local.Type IsNot Nothing AndAlso local.Type.IsReferenceType
            End If

            Dim parameter = TryCast(symbol, ParameterSymbol)
            If parameter IsNot Nothing Then
                Return parameter.Type IsNot Nothing AndAlso parameter.Type.IsReferenceType
            End If

            Return False
        End Function

        Private Shared Function TrackedSymbol(expr As BoundExpression) As Symbol
            Dim current = expr
            While current IsNot Nothing
                Select Case current.Kind
                    Case BoundKind.Local
                        Return DirectCast(current, BoundLocal).LocalSymbol
                    Case BoundKind.Parameter
                        Return DirectCast(current, BoundParameter).ParameterSymbol
                    Case BoundKind.Parenthesized
                        current = DirectCast(current, BoundParenthesized).Expression
                    Case BoundKind.Conversion, BoundKind.DirectCast
                        current = DirectCast(current, BoundConversionOrCast).Operand
                    Case Else
                        Return Nothing
                End Select
            End While

            Return Nothing
        End Function

        Private Shared Function AnnotationOf(symbol As Symbol) As NullableAnnotation
            Dim local = TryCast(symbol, ILocalSymbol)
            If local IsNot Nothing Then
                Return local.NullableAnnotation
            End If

            Dim parameter = TryCast(symbol, IParameterSymbol)
            If parameter IsNot Nothing Then
                Return parameter.NullableAnnotation
            End If

            Dim method = TryCast(symbol, IMethodSymbol)
            If method IsNot Nothing Then
                Return method.ReturnNullableAnnotation
            End If

            Return NullableAnnotation.None
        End Function

        Private Shared Function TryGetNullTest(condition As BoundExpression, ByRef symbol As Symbol, ByRef trueWhenNull As Boolean) As Boolean
            Dim binary = TryCast(Unwrap(condition), BoundBinaryOperator)
            If binary Is Nothing Then
                Return False
            End If

            Dim kind = binary.OperatorKind
            If (kind And (BinaryOperatorKind.Lifted Or BinaryOperatorKind.UserDefined)) <> 0 Then
                Return False
            End If

            Dim op = kind And BinaryOperatorKind.OpMask
            If op <> BinaryOperatorKind.Is AndAlso op <> BinaryOperatorKind.IsNot Then
                Return False
            End If

            Dim tested As Symbol = Nothing
            If IsNothing(binary.Right) Then
                tested = TrackedSymbol(binary.Left)
            ElseIf IsNothing(binary.Left) Then
                tested = TrackedSymbol(binary.Right)
            End If

            If tested Is Nothing OrElse Not IsTrackedReference(tested) Then
                Return False
            End If

            symbol = tested
            trueWhenNull = op = BinaryOperatorKind.Is
            Return True
        End Function

        Private Shared Function Unwrap(expr As BoundExpression) As BoundExpression
            Dim current = expr
            While current IsNot Nothing AndAlso current.Kind = BoundKind.Parenthesized
                current = DirectCast(current, BoundParenthesized).Expression
            End While

            Return current
        End Function

        Private Shared Function IsNothing(expr As BoundExpression) As Boolean
            Return expr IsNot Nothing AndAlso expr.ConstantValueOpt IsNot Nothing AndAlso expr.ConstantValueOpt.IsNothing
        End Function

        Private Shared Function IsBuiltInNot(node As BoundUnaryOperator) As Boolean
            Dim kind = node.OperatorKind
            If (kind And (UnaryOperatorKind.UserDefined Or UnaryOperatorKind.Lifted)) <> 0 Then
                Return False
            End If

            Return (kind And UnaryOperatorKind.IntrinsicOpMask) = UnaryOperatorKind.Not
        End Function

        Private Sub Warn(code As ERRID, syntax As SyntaxNode, ParamArray args As Object())
            If Not _reachable OrElse syntax Is Nothing OrElse Not WarningsEnabled(syntax) Then
                Return
            End If

            Dim location = syntax.GetLocation()
            Dim key = CInt(code).ToString() & ":" & location.SourceSpan.Start.ToString() & ":" & location.SourceSpan.Length.ToString()
            If Not _reported.Add(key) Then
                Return
            End If

            _diagnostics.Add(New VBDiagnostic(ErrorFactory.ErrorInfo(code, args), location))
        End Sub

        Private Function WarningsEnabled(node As SyntaxNode) As Boolean
            If node.SyntaxTree Is Nothing Then
                Return False
            End If

            Dim context = NullableContextFacts.ForTree(node.SyntaxTree, _project).GetContext(node.SpanStart)
            Return (context And NullableContextOptions.Warnings) <> 0
        End Function

        Private Function Capture() As FlowSnapshot
            Return New FlowSnapshot(_reachable, New Dictionary(Of Symbol, FlowNullability)(_state))
        End Function

        Private Shared Function CloneSnapshot(snapshot As FlowSnapshot) As FlowSnapshot
            If snapshot Is Nothing Then
                Return Unreachable()
            End If

            Return New FlowSnapshot(snapshot.Reachable, New Dictionary(Of Symbol, FlowNullability)(snapshot.Slots))
        End Function

        Private Shared Function Unreachable() As FlowSnapshot
            Return New FlowSnapshot(False, New Dictionary(Of Symbol, FlowNullability)())
        End Function

        Private Sub SetSnapshot(snapshot As FlowSnapshot)
            If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                _reachable = False
                Return
            End If

            _reachable = True
            _state = New Dictionary(Of Symbol, FlowNullability)(snapshot.Slots)
        End Sub

        Private Shared Function MergeSnapshots(left As FlowSnapshot, right As FlowSnapshot) As FlowSnapshot
            If left Is Nothing OrElse Not left.Reachable Then
                Return If(right Is Nothing, Unreachable(), right)
            End If

            If right Is Nothing OrElse Not right.Reachable Then
                Return left
            End If

            Dim slots As New Dictionary(Of Symbol, FlowNullability)()
            For Each key In left.Slots.Keys
                slots(key) = left.Slots(key)
            Next

            For Each key In right.Slots.Keys
                Dim other = right.Slots(key)
                Dim current As FlowNullability
                If Not slots.TryGetValue(key, current) Then
                    current = FlowNullability.Unknown
                End If

                slots(key) = MergeNull(current, other)
            Next

            For Each key In left.Slots.Keys
                If Not right.Slots.ContainsKey(key) Then
                    slots(key) = MergeNull(left.Slots(key), FlowNullability.Unknown)
                End If
            Next

            Return New FlowSnapshot(True, slots)
        End Function

        Private Shared Function MergeNull(left As FlowNullability, right As FlowNullability) As FlowNullability
            If left = FlowNullability.MaybeNull OrElse right = FlowNullability.MaybeNull Then
                Return FlowNullability.MaybeNull
            End If

            If left = FlowNullability.Unknown OrElse right = FlowNullability.Unknown Then
                Return FlowNullability.Unknown
            End If

            Return FlowNullability.NotNull
        End Function

        Private Function EvaluateUserDefinedConversion(node As BoundUserDefinedConversion) As FlowNullability
            Evaluate(node.UnderlyingExpression)
            Dim method = ConversionOperator(node.UnderlyingExpression)
            If method Is Nothing Then
                Return FlowNullability.Unknown
            End If

            If method.ReturnType IsNot Nothing AndAlso Not method.ReturnType.IsReferenceType Then
                Return FlowNullability.NotNull
            End If

            ' The operator's declared return contract, not the null state of its input.
            Return CallResult(method)
        End Function

        Private Shared Function ConversionOperator(expr As BoundExpression) As MethodSymbol
            Dim current = expr
            While current IsNot Nothing
                Select Case current.Kind
                    Case BoundKind.Call
                        Return DirectCast(current, BoundCall).Method
                    Case BoundKind.Parenthesized
                        current = DirectCast(current, BoundParenthesized).Expression
                    Case BoundKind.Conversion, BoundKind.DirectCast, BoundKind.TryCast
                        current = DirectCast(current, BoundConversionOrCast).Operand
                    Case BoundKind.UserDefinedConversion
                        current = DirectCast(current, BoundUserDefinedConversion).UnderlyingExpression
                    Case Else
                        Return Nothing
                End Select
            End While

            Return Nothing
        End Function

        Private Sub NoteWrite(symbol As Symbol)
            If symbol Is Nothing OrElse Not IsTrackedReference(symbol) Then
                Return
            End If

            _writeClock += 1
            _writes(symbol) = _writeClock
            ObserveException()
        End Sub

        Private Sub ObserveException()
            If _isolation > 0 OrElse Not _reachable OrElse _exceptionStates.Count = 0 Then
                Return
            End If

            Dim current = Capture()
            For index = 0 To _exceptionStates.Count - 1
                _exceptionStates(index) = MergeSnapshots(_exceptionStates(index), current)
            Next
        End Sub

        Private Sub ApplyFinallyWrites(leaving As FlowSnapshot, clock As Integer)
            Dim after = Capture()
            Dim result = CloneSnapshot(leaving)
            For Each pair In _writes
                Dim written As FlowNullability
                If pair.Value > clock AndAlso after.Slots.TryGetValue(pair.Key, written) Then
                    result.Slots(pair.Key) = written
                End If
            Next

            SetSnapshot(result)
        End Sub

        Private Sub WidenJumps()
            Dim labels = _jumps.Keys.ToArray()
            For Each label In labels
                _jumps(label) = Widen(_jumps(label))
            Next
        End Sub

        Private Shared Function Widen(snapshot As FlowSnapshot) As FlowSnapshot
            Dim slots = New Dictionary(Of Symbol, FlowNullability)(snapshot.Slots)
            Dim keys = slots.Keys.ToArray()
            For Each key In keys
                slots(key) = FlowNullability.MaybeNull
            Next

            Return New FlowSnapshot(snapshot.Reachable, slots)
        End Function

        Private Shared Function Same(left As FlowSnapshot, right As FlowSnapshot) As Boolean
            If left Is Nothing OrElse right Is Nothing Then
                Return left Is right
            End If

            If left.Reachable <> right.Reachable Then
                Return False
            End If

            If Not left.Reachable Then
                Return True
            End If

            If left.Slots.Count <> right.Slots.Count Then
                Return False
            End If

            For Each pair In left.Slots
                Dim other As FlowNullability
                If Not right.Slots.TryGetValue(pair.Key, other) OrElse other <> pair.Value Then
                    Return False
                End If
            Next

            Return True
        End Function

        Private Sub AddJump(label As LabelSymbol, snapshot As FlowSnapshot)
            If label Is Nothing OrElse snapshot Is Nothing OrElse Not snapshot.Reachable Then
                Return
            End If

            Dim existing As FlowSnapshot = Nothing
            If _jumps.TryGetValue(label, existing) Then
                Dim merged = MergeSnapshots(existing, snapshot)
                If Not Same(existing, merged) Then
                    _jumps(label) = merged
                    If _visitedLabels.Contains(label) Then
                        _needsRewalk = True
                    End If
                End If
            Else
                _jumps(label) = CloneSnapshot(snapshot)
                If _visitedLabels.Contains(label) Then
                    _needsRewalk = True
                End If
            End If
        End Sub

        Private Function JumpSnapshot(label As LabelSymbol) As FlowSnapshot
            Dim existing As FlowSnapshot = Nothing
            If label IsNot Nothing AndAlso _jumps.TryGetValue(label, existing) Then
                Return CloneSnapshot(existing)
            End If

            Return Unreachable()
        End Function

        Private NotInheritable Class CaptureWalker
            Inherits BoundTreeWalkerWithStackGuard

            Private ReadOnly _owner As MethodSymbol
            Private ReadOnly _captured As HashSet(Of Symbol)

            Friend Sub New(owner As MethodSymbol, captured As HashSet(Of Symbol))
                _owner = owner
                _captured = captured
            End Sub

            Public Overrides Function VisitLocal(node As BoundLocal) As BoundNode
                If node.LocalSymbol IsNot Nothing AndAlso node.LocalSymbol.ContainingSymbol Is _owner Then
                    _captured.Add(node.LocalSymbol)
                End If

                Return Nothing
            End Function

            Public Overrides Function VisitParameter(node As BoundParameter) As BoundNode
                If node.ParameterSymbol IsNot Nothing AndAlso node.ParameterSymbol.ContainingSymbol Is _owner Then
                    _captured.Add(node.ParameterSymbol)
                End If

                Return Nothing
            End Function
        End Class
    End Class

End Namespace
