' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Nullable flow state for locals, parameters, and stable member storage.
    ''' Declared annotations stay on the symbols.
    ''' This walker does not change definite assignment, reachability diagnostics, or generated IL.
    '''
    ''' Member slots are (receiver, path), not the field or property symbol alone.
    ''' Me, MyClass, and MyBase share the current instance. Shared members use one slot.
    ''' A local or parameter receiver is stable storage for that variable.
    ''' Nested paths hang off that receiver and are dropped when a parent is replaced.
    ''' Reference assignments between locals link aliases; a write through one alias
    ''' drops non-null facts on the others. Calls, awaits, and lambdas drop facts only
    ''' for receivers they can reach. Auto-properties are field-like. Other getters are
    ''' calls and do not keep a fact across reads. Array indices and parameterized
    ''' properties are not stable slots; their element contracts come from the
    ''' substituted annotation or accessor.
    ''' </summary>
    Friend NotInheritable Class NullableFlowAnalysis
        Inherits BoundTreeWalkerWithStackGuard

        Private Enum FlowNullability
            Unknown
            NotNull
            MaybeNull
        End Enum

        Private NotInheritable Class MemberSlot
            Implements IEquatable(Of MemberSlot)

            Friend ReadOnly Receiver As Symbol
            Friend ReadOnly IsShared As Boolean
            Private ReadOnly _path As Symbol()
            Private ReadOnly _hash As Integer

            Friend Sub New(receiver As Symbol, isShared As Boolean, path As Symbol())
                Me.Receiver = receiver
                Me.IsShared = isShared
                _path = path
                Dim hash = If(isShared, 17, 23)
                If receiver IsNot Nothing Then
                    hash = (hash * 31) Xor receiver.GetHashCode()
                End If

                For Each item In path
                    hash = (hash * 31) Xor If(item Is Nothing, 0, item.GetHashCode())
                Next

                _hash = hash
            End Sub

            Friend ReadOnly Property LeafSymbol As Symbol
                Get
                    Return _path(_path.Length - 1)
                End Get
            End Property

            Friend ReadOnly Property Depth As Integer
                Get
                    Return _path.Length
                End Get
            End Property

            Friend Function WithReceiver(receiver As Symbol) As MemberSlot
                Return New MemberSlot(receiver, IsShared, _path)
            End Function

            Friend Function StartsWith(prefix As MemberSlot) As Boolean
                If IsShared <> prefix.IsShared OrElse Not SameSymbol(Receiver, prefix.Receiver) OrElse prefix._path.Length > _path.Length Then
                    Return False
                End If

                For index = 0 To prefix._path.Length - 1
                    If Not SameSymbol(_path(index), prefix._path(index)) Then
                        Return False
                    End If
                Next

                Return True
            End Function

            Public Overloads Function Equals(other As MemberSlot) As Boolean Implements IEquatable(Of MemberSlot).Equals
                If other Is Nothing OrElse IsShared <> other.IsShared OrElse _path.Length <> other._path.Length OrElse Not SameSymbol(Receiver, other.Receiver) Then
                    Return False
                End If

                For index = 0 To _path.Length - 1
                    If Not SameSymbol(_path(index), other._path(index)) Then
                        Return False
                    End If
                Next

                Return True
            End Function

            Public Overrides Function Equals(obj As Object) As Boolean
                Return Equals(TryCast(obj, MemberSlot))
            End Function

            Public Overrides Function GetHashCode() As Integer
                Return _hash
            End Function

            Private Shared Function SameSymbol(left As Symbol, right As Symbol) As Boolean
                Return left Is right
            End Function
        End Class

        Private NotInheritable Class FlowSnapshot
            Friend ReadOnly Reachable As Boolean
            Friend ReadOnly Slots As Dictionary(Of Symbol, FlowNullability)
            Friend ReadOnly Members As Dictionary(Of MemberSlot, FlowNullability)

            Friend Sub New(reachable As Boolean, slots As Dictionary(Of Symbol, FlowNullability), members As Dictionary(Of MemberSlot, FlowNullability))
                Me.Reachable = reachable
                Me.Slots = slots
                Me.Members = members
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
        Private _members As Dictionary(Of MemberSlot, FlowNullability)
        Private ReadOnly _aliases As Dictionary(Of Symbol, Symbol)
        Private ReadOnly _memberWrites As Dictionary(Of MemberSlot, Integer)
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
            _members = New Dictionary(Of MemberSlot, FlowNullability)()
            _aliases = New Dictionary(Of Symbol, Symbol)()
            _memberWrites = New Dictionary(Of MemberSlot, Integer)()
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
            _members = New Dictionary(Of MemberSlot, FlowNullability)()
            _aliases.Clear()
            _memberWrites.Clear()
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
                elementState = ElementFlow(node, control)
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
                NoteReceiverIdentity(node.LocalSymbol, node.DeclarationInitializerOpt)
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
                NoteReceiverIdentity(target, node.Right)
                Assign(target, value, node.Right)
            ElseIf Not AssignStorage(node.Left, value, node.Right) Then
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
            InvalidateCapturedReceivers()
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
            InvalidateCapturedReceivers()
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
                    Dim memberTest As MemberSlot = Nothing
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

                    If TryGetMemberNullTest(binary, memberTest, trueWhenNull) Then
                        Dim baseState = Capture()
                        SetSnapshot(CloneSnapshot(baseState))
                        LearnMember(memberTest, trueWhenNull)
                        split.WhenTrue = Capture()
                        SetSnapshot(CloneSnapshot(baseState))
                        LearnMember(memberTest, Not trueWhenNull)
                        split.WhenFalse = Capture()
                        Return split
                    End If

                    Dim here = Capture()
                    split.WhenTrue = here
                    split.WhenFalse = CloneSnapshot(here)
                    Return split
                End If

                If ordinary AndAlso (op = BinaryOperatorKind.And OrElse op = BinaryOperatorKind.Or) Then
                    ' Both operands run. A fact from the left is kept only when the right does not write that slot.
                    Dim left = SplitCondition(binary.Left)
                    SetSnapshot(MergeSnapshots(left.WhenTrue, left.WhenFalse))
                    Dim leftWrites = New Dictionary(Of Symbol, Integer)(_writes)
                    Dim right = SplitCondition(binary.Right)
                    Dim after = MergeSnapshots(right.WhenTrue, right.WhenFalse)
                    Dim strengthenLeft = If(op = BinaryOperatorKind.And, left.WhenTrue, left.WhenFalse)
                    Dim strengthenRight = If(op = BinaryOperatorKind.And, right.WhenTrue, right.WhenFalse)
                    Dim otherRight = If(op = BinaryOperatorKind.And, right.WhenFalse, right.WhenTrue)
                    Dim strengthened = OverlayStable(after, strengthenLeft, leftWrites)
                    strengthened = ApplyBranchDelta(strengthened, strengthenRight, otherRight)
                    If op = BinaryOperatorKind.And Then
                        split.WhenTrue = strengthened
                        split.WhenFalse = CloneSnapshot(after)
                    Else
                        split.WhenTrue = CloneSnapshot(after)
                        split.WhenFalse = strengthened
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

            Dim callNode = TryCast(expr, BoundCall)
            If callNode IsNot Nothing Then
                Return SplitCall(callNode)
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
                Case BoundKind.ArrayAccess
                    Return EvaluateArray(DirectCast(expr, BoundArrayAccess))
                Case BoundKind.AwaitOperator
                    Return EvaluateAwait(DirectCast(expr, BoundAwaitOperator))
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
            Dim effect = AnalyzeInvocation(node.Method, node.Arguments, node.DefaultArguments, node.ReceiverOpt)
            SetSnapshot(MergeSnapshots(effect.WhenTrue, effect.WhenFalse))
            Return effect.Result
        End Function

        Private Function EvaluateProperty(node As BoundPropertyAccess) As FlowNullability
            EvaluateReceiver(node.ReceiverOpt)
            If Not node.Arguments.IsDefault Then
                For Each argument In node.Arguments
                    Evaluate(argument)
                Next
            End If

            Dim slot = TryBuildSlot(node)
            Dim result = If(slot Is Nothing, PropertyResult(node.PropertySymbol), StateOfMember(slot))
            If slot Is Nothing Then
                InvalidatePropertyEffects(node.PropertySymbol, node.ReceiverOpt)
            End If

            Return result
        End Function

        Private Function EvaluateField(node As BoundFieldAccess) As FlowNullability
            EvaluateReceiver(node.ReceiverOpt)
            Dim slot = TryBuildSlot(node)
            If slot Is Nothing Then
                Return FromAnnotation(node.FieldSymbol.GetNullableAnnotation())
            End If

            Return StateOfMember(slot)
        End Function

        Private Function EvaluateObjectCreation(node As BoundObjectCreationExpression) As FlowNullability
            Dim effect = AnalyzeInvocation(node.ConstructorOpt, node.Arguments, node.DefaultArguments, receiver:=Nothing)
            SetSnapshot(MergeSnapshots(effect.WhenTrue, effect.WhenFalse))
            If _reachable AndAlso node.InitializerOpt IsNot Nothing Then
                Visit(node.InitializerOpt)
            End If

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
            Warn(ERRID.WRN_PossibleNullReference, receiver.Syntax, DisplayName(receiver))
        End Sub

        Private Structure InvocationEffect
            Friend Result As FlowNullability
            Friend WhenTrue As FlowSnapshot
            Friend WhenFalse As FlowSnapshot
        End Structure

        Private Structure ArgumentRecord
            Friend Parameter As ParameterSymbol
            Friend Slot As Symbol
            Friend Value As FlowNullability
            Friend Stamp As Integer
            Friend HasSlot As Boolean
            Friend Syntax As SyntaxNode
        End Structure

        Private Function SplitCall(node As BoundCall) As ConditionSplit
            EvaluateReceiver(node.ReceiverOpt)
            Dim effect = AnalyzeInvocation(node.Method, node.Arguments, node.DefaultArguments, node.ReceiverOpt)
            Dim split As New ConditionSplit()
            split.WhenTrue = effect.WhenTrue
            split.WhenFalse = effect.WhenFalse
            Return split
        End Function

        ''' <summary>
        ''' Evaluates arguments in bound order, which is the order the rewriter emits.
        ''' Successful-return postconditions are applied only when a later argument has not
        ''' written the slot. They are not copied into exceptional snapshots.
        ''' </summary>
        Private Function AnalyzeInvocation(method As MethodSymbol, arguments As ImmutableArray(Of BoundExpression), defaultArguments As BitVector, receiver As BoundExpression) As InvocationEffect
            Dim effect As InvocationEffect
            If Not _reachable Then
                effect.Result = FlowNullability.NotNull
                effect.WhenTrue = Unreachable()
                effect.WhenFalse = Unreachable()
                Return effect
            End If

            Dim records As New List(Of ArgumentRecord)()
            Dim survivors As New List(Of FlowSnapshot)()
            Dim survivorWrites As New List(Of Dictionary(Of Symbol, Integer))()
            Dim dependentNotNull = False
            Dim booleanReturn = method IsNot Nothing AndAlso method.ReturnType IsNot Nothing AndAlso method.ReturnType.SpecialType = SpecialType.System_Boolean
            Dim dependencyNames = If(method Is Nothing, Nothing, method.ReturnNotNullIfParameterNotNull)
            Dim argumentCount = If(arguments.IsDefault, 0, arguments.Length)

            For i = 0 To argumentCount - 1
                Dim parameter As ParameterSymbol = Nothing
                If method IsNot Nothing AndAlso i < method.ParameterCount Then
                    parameter = method.Parameters(i)
                End If

                Dim argument = arguments(i)
                Dim annotations = If(parameter Is Nothing, FlowAnalysisAnnotations.None, EffectiveAnnotations(parameter, booleanReturn))
                Dim value As FlowNullability
                If parameter IsNot Nothing AndAlso Not parameter.IsByRef AndAlso HasConditionalNonReturn(annotations) Then
                    ' Later arguments still run. The filtered snapshot is the successful return.
                    Dim split = SplitCondition(ArgumentValue(argument))
                    Dim survivor As FlowSnapshot
                    If (annotations And FlowAnalysisAnnotations.DoesNotReturn) = FlowAnalysisAnnotations.DoesNotReturn Then
                        survivor = Unreachable()
                    ElseIf HasContract(annotations, FlowAnalysisAnnotations.DoesNotReturnIfTrue) Then
                        survivor = split.WhenFalse
                    Else
                        survivor = split.WhenTrue
                    End If

                    survivors.Add(survivor)
                    survivorWrites.Add(New Dictionary(Of Symbol, Integer)(_writes))
                    SetSnapshot(MergeSnapshots(split.WhenTrue, split.WhenFalse))
                    value = FlowNullability.NotNull
                Else
                    value = Evaluate(ArgumentValue(argument))
                End If

                If Not _reachable Then
                    effect.Result = FlowNullability.NotNull
                    effect.WhenTrue = Unreachable()
                    effect.WhenFalse = Unreachable()
                    Return effect
                End If

                Dim isDefault = Not defaultArguments.IsNull AndAlso defaultArguments(i)
                If parameter IsNot Nothing AndAlso Not isDefault AndAlso value = FlowNullability.MaybeNull AndAlso ArgumentDisallowsNull(parameter) Then
                    Warn(ERRID.WRN_PossibleNullArgument, argument.Syntax, parameter.Name)
                End If

                If dependencyNames IsNot Nothing AndAlso parameter IsNot Nothing AndAlso dependencyNames.Contains(parameter.Name) AndAlso value = FlowNullability.NotNull Then
                    dependentNotNull = True
                End If

                Dim record As ArgumentRecord
                record.Parameter = parameter
                record.Value = value
                record.Syntax = argument.Syntax
                record.Slot = DirectArgumentSlot(argument)
                record.HasSlot = record.Slot IsNot Nothing
                record.Stamp = 0
                If record.HasSlot Then
                    _writes.TryGetValue(record.Slot, record.Stamp)
                End If

                records.Add(record)
            Next

            For index = 0 To survivors.Count - 1
                Dim survivor = survivors(index)
                If survivor Is Nothing OrElse Not survivor.Reachable Then
                    effect.Result = FlowNullability.NotNull
                    effect.WhenTrue = Unreachable()
                    effect.WhenFalse = Unreachable()
                    SetSnapshot(effect.WhenTrue)
                    Return effect
                End If

                Dim overlaid = OverlayStable(Capture(), survivor, survivorWrites(index))
                SetSnapshot(overlaid)
            Next

            If method IsNot Nothing AndAlso HasContract(method.FlowAnalysisAnnotations, FlowAnalysisAnnotations.DoesNotReturn) Then
                effect.Result = FlowNullability.NotNull
                effect.WhenTrue = Unreachable()
                effect.WhenFalse = Unreachable()
                SetSnapshot(effect.WhenTrue)
                Return effect
            End If

            For Each record In records
                If record.HasSlot AndAlso record.Parameter IsNot Nothing AndAlso record.Parameter.IsByRef Then
                    WeakenExceptionSlot(record.Slot)
                End If
            Next

            Dim whenTrue = Capture()
            Dim whenFalse = CloneSnapshot(whenTrue)
            Dim appliedTrue As New Dictionary(Of Symbol, FlowNullability)()
            Dim appliedFalse As New Dictionary(Of Symbol, FlowNullability)()
            Dim stamped As New HashSet(Of Symbol)()
            For Each record In records
                If record.Parameter Is Nothing OrElse Not record.HasSlot Then
                    Continue For
                End If

                Dim now = 0
                If _writes.TryGetValue(record.Slot, now) AndAlso now <> record.Stamp Then
                    Continue For
                End If

                Dim annotations = EffectiveAnnotations(record.Parameter, booleanReturn)
                ApplyOutput(whenTrue, appliedTrue, record, annotations, sense:=True)
                ApplyOutput(whenFalse, appliedFalse, record, annotations, sense:=False)
                If appliedTrue.ContainsKey(record.Slot) OrElse appliedFalse.ContainsKey(record.Slot) Then
                    stamped.Add(record.Slot)
                End If
            Next

            For Each symbol In stamped
                ' Successful-return writes invalidate earlier Boolean facts without entering exceptional states.
                _writeClock += 1
                _writes(symbol) = _writeClock
            Next

            InvalidateCaptured(whenTrue, appliedTrue)
            InvalidateCaptured(whenFalse, appliedFalse)
            InvalidateCallSnapshots(method, receiver, records, whenTrue, whenFalse)
            effect.WhenTrue = whenTrue
            effect.WhenFalse = whenFalse
            effect.Result = CallResult(method, dependentNotNull)
            SetSnapshot(MergeSnapshots(whenTrue, whenFalse))
            Return effect
        End Function

        Private Sub ApplyOutput(snapshot As FlowSnapshot, applied As Dictionary(Of Symbol, FlowNullability), record As ArgumentRecord, annotations As FlowAnalysisAnnotations, sense As Boolean)
            Dim state As FlowNullability
            If Not TryOutputState(record.Parameter, annotations, sense, state) Then
                Return
            End If

            Dim previous As FlowNullability
            If applied.TryGetValue(record.Slot, previous) Then
                state = MergeNull(previous, state)
            End If

            applied(record.Slot) = state
            snapshot.Slots(record.Slot) = state
            If sense AndAlso state = FlowNullability.MaybeNull AndAlso AnnotationOf(record.Slot) = NullableAnnotation.NotAnnotated AndAlso record.Parameter.IsByRef Then
                Warn(ERRID.WRN_PossibleNullAssignment, record.Syntax, record.Slot.Name)
            End If
        End Sub

        Private Shared Function TryOutputState(parameter As ParameterSymbol, annotations As FlowAnalysisAnnotations, sense As Boolean, ByRef state As FlowNullability) As Boolean
            Dim notNullWhen = If(sense, HasContract(annotations, FlowAnalysisAnnotations.NotNullWhenTrue), HasContract(annotations, FlowAnalysisAnnotations.NotNullWhenFalse))
            Dim maybeWhen = If(sense, HasContract(annotations, FlowAnalysisAnnotations.MaybeNullWhenTrue), HasContract(annotations, FlowAnalysisAnnotations.MaybeNullWhenFalse))
            Dim maybeOther = If(sense, HasContract(annotations, FlowAnalysisAnnotations.MaybeNullWhenFalse), HasContract(annotations, FlowAnalysisAnnotations.MaybeNullWhenTrue))
            If parameter.IsByRef Then
                If maybeWhen AndAlso Not (maybeOther AndAlso notNullWhen) Then
                    state = FlowNullability.MaybeNull
                    Return True
                End If

                If notNullWhen Then
                    state = FlowNullability.NotNull
                    Return True
                End If

                If AnnotationOf(parameter) = NullableAnnotation.NotAnnotated Then
                    state = FlowNullability.NotNull
                Else
                    state = FlowNullability.MaybeNull
                End If

                Return True
            End If

            If maybeWhen Then
                state = FlowNullability.MaybeNull
                Return True
            End If

            If notNullWhen Then
                state = FlowNullability.NotNull
                Return True
            End If

            Return False
        End Function

        Private Shared Function EffectiveAnnotations(parameter As ParameterSymbol, booleanReturn As Boolean) As FlowAnalysisAnnotations
            Dim annotations = parameter.FlowAnalysisAnnotations
            If booleanReturn Then
                Return annotations
            End If

            Dim notNull = annotations And FlowAnalysisAnnotations.NotNull
            If notNull <> FlowAnalysisAnnotations.None AndAlso notNull <> FlowAnalysisAnnotations.NotNull Then
                annotations = annotations And Not FlowAnalysisAnnotations.NotNull
            End If

            Dim maybeNull = annotations And FlowAnalysisAnnotations.MaybeNull
            If maybeNull <> FlowAnalysisAnnotations.None AndAlso maybeNull <> FlowAnalysisAnnotations.MaybeNull Then
                annotations = annotations And Not FlowAnalysisAnnotations.MaybeNull
            End If

            Return annotations
        End Function

        Private Shared Function HasConditionalNonReturn(annotations As FlowAnalysisAnnotations) As Boolean
            Return (annotations And FlowAnalysisAnnotations.DoesNotReturn) <> FlowAnalysisAnnotations.None
        End Function

        Private Sub InvalidateCaptured(snapshot As FlowSnapshot, applied As Dictionary(Of Symbol, FlowNullability))
            If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                Return
            End If

            For Each symbol In _captured
                If IsTrackedReference(symbol) AndAlso Not applied.ContainsKey(symbol) Then
                    snapshot.Slots(symbol) = FlowNullability.MaybeNull
                End If
            Next
        End Sub

        Private Sub WeakenExceptionSlot(symbol As Symbol)
            For index = 0 To _exceptionStates.Count - 1
                Dim snapshot = _exceptionStates(index)
                If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                    Continue For
                End If

                Dim copy = CloneSnapshot(snapshot)
                copy.Slots(symbol) = FlowNullability.MaybeNull
                _exceptionStates(index) = copy
            Next
        End Sub

        Private Function OverlayStable(base As FlowSnapshot, overlay As FlowSnapshot, baseline As Dictionary(Of Symbol, Integer)) As FlowSnapshot
            Dim result = CloneSnapshot(base)
            If overlay Is Nothing OrElse Not overlay.Reachable OrElse Not result.Reachable Then
                Return result
            End If

            For Each pair In overlay.Slots
                Dim thenStamp = 0
                baseline.TryGetValue(pair.Key, thenStamp)
                Dim nowStamp = 0
                _writes.TryGetValue(pair.Key, nowStamp)
                If nowStamp = thenStamp Then
                    result.Slots(pair.Key) = pair.Value
                End If
            Next

            If overlay.Members IsNot Nothing Then
                For Each pair In overlay.Members
                    result.Members(pair.Key) = pair.Value
                Next
            End If

            Return result
        End Function

        Private Shared Function ApplyBranchDelta(base As FlowSnapshot, branch As FlowSnapshot, other As FlowSnapshot) As FlowSnapshot
            Dim result = CloneSnapshot(base)
            If branch Is Nothing OrElse Not branch.Reachable OrElse Not result.Reachable Then
                Return result
            End If

            For Each pair In branch.Slots
                Dim otherState As FlowNullability
                Dim differs = other Is Nothing OrElse Not other.Reachable OrElse Not other.Slots.TryGetValue(pair.Key, otherState) OrElse otherState <> pair.Value
                If differs Then
                    result.Slots(pair.Key) = pair.Value
                End If
            Next

            If branch.Members IsNot Nothing Then
                For Each pair In branch.Members
                    Dim otherState As FlowNullability
                    Dim differs = other Is Nothing OrElse Not other.Reachable OrElse other.Members Is Nothing OrElse Not other.Members.TryGetValue(pair.Key, otherState) OrElse otherState <> pair.Value
                    If differs Then
                        result.Members(pair.Key) = pair.Value
                    End If
                Next
            End If

            Return result
        End Function

        Private Shared Function DirectArgumentSlot(argument As BoundExpression) As Symbol
            Dim current = argument
            If current IsNot Nothing AndAlso current.Kind = BoundKind.ByRefArgumentWithCopyBack Then
                current = DirectCast(current, BoundByRefArgumentWithCopyBack).OriginalArgument
            End If

            Return TrackedSymbol(current)
        End Function

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
                Dim memberTest As MemberSlot = Nothing
                If TryGetNullTest(binary, tested, trueWhenNull) Then
                    LearnIfStable(tested, If(sense, trueWhenNull, Not trueWhenNull), baseline)
                ElseIf TryGetMemberNullTest(binary, memberTest, trueWhenNull) Then
                    LearnMemberIfStable(memberTest, If(sense, trueWhenNull, Not trueWhenNull), baseline)
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
            Dim parameter = TryCast(symbol, ParameterSymbol)
            If parameter IsNot Nothing Then
                Dim annotations = parameter.FlowAnalysisAnnotations
                ' AllowNull is the entry state when both input attributes are present.
                If HasContract(annotations, FlowAnalysisAnnotations.AllowNull) Then
                    Return FlowNullability.MaybeNull
                End If

                If HasContract(annotations, FlowAnalysisAnnotations.DisallowNull) Then
                    Return FlowNullability.NotNull
                End If
            End If

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

        Private Shared Function CallResult(method As MethodSymbol, dependentNotNull As Boolean) As FlowNullability
            If method Is Nothing Then
                Return FlowNullability.Unknown
            End If

            If method.ReturnType IsNot Nothing AndAlso Not method.ReturnType.IsReferenceType Then
                Return FlowNullability.NotNull
            End If

            If dependentNotNull Then
                Return FlowNullability.NotNull
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
            Return DisallowsNull(AnnotationOf(_method) = NullableAnnotation.NotAnnotated, _method.ReturnTypeFlowAnalysisAnnotations, outputContract:=True)
        End Function

        Private Function ArgumentDisallowsNull(parameter As ParameterSymbol) As Boolean
            Return DisallowsNull(AnnotationOf(parameter) = NullableAnnotation.NotAnnotated, parameter.FlowAnalysisAnnotations, outputContract:=False)
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

        Private Shared Function StripFlow(expr As BoundExpression) As BoundExpression
            Dim current = Unwrap(expr)
            While current IsNot Nothing
                Select Case current.Kind
                    Case BoundKind.Conversion, BoundKind.DirectCast, BoundKind.TryCast
                        current = Unwrap(DirectCast(current, BoundConversionOrCast).Operand)
                    Case Else
                        Return current
                End Select
            End While

            Return current
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
            Return New FlowSnapshot(_reachable, New Dictionary(Of Symbol, FlowNullability)(_state), New Dictionary(Of MemberSlot, FlowNullability)(_members))
        End Function

        Private Shared Function CloneSnapshot(snapshot As FlowSnapshot) As FlowSnapshot
            If snapshot Is Nothing Then
                Return Unreachable()
            End If

            Return New FlowSnapshot(snapshot.Reachable, New Dictionary(Of Symbol, FlowNullability)(snapshot.Slots), New Dictionary(Of MemberSlot, FlowNullability)(snapshot.Members))
        End Function

        Private Shared Function Unreachable() As FlowSnapshot
            Return New FlowSnapshot(False, New Dictionary(Of Symbol, FlowNullability)(), New Dictionary(Of MemberSlot, FlowNullability)())
        End Function

        Private Sub SetSnapshot(snapshot As FlowSnapshot)
            If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                _reachable = False
                Return
            End If

            _reachable = True
            _state = New Dictionary(Of Symbol, FlowNullability)(snapshot.Slots)
            _members = New Dictionary(Of MemberSlot, FlowNullability)(snapshot.Members)
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

            Dim members = MergeMembers(left.Members, right.Members)
            Return New FlowSnapshot(True, slots, members)
        End Function

        Private Shared Function MergeMembers(left As Dictionary(Of MemberSlot, FlowNullability), right As Dictionary(Of MemberSlot, FlowNullability)) As Dictionary(Of MemberSlot, FlowNullability)
            Dim members As New Dictionary(Of MemberSlot, FlowNullability)()
            If left IsNot Nothing Then
                For Each pair In left
                    Dim other As FlowNullability
                    If right IsNot Nothing AndAlso right.TryGetValue(pair.Key, other) Then
                        members(pair.Key) = MergeNull(pair.Value, other)
                    ElseIf pair.Value = FlowNullability.MaybeNull Then
                        members(pair.Key) = FlowNullability.MaybeNull
                    End If
                Next
            End If

            If right IsNot Nothing Then
                For Each pair In right
                    If pair.Value = FlowNullability.MaybeNull AndAlso Not members.ContainsKey(pair.Key) Then
                        members(pair.Key) = FlowNullability.MaybeNull
                    End If
                Next
            End If

            Return members
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
            Dim result = Evaluate(node.UnderlyingExpression)
            Dim method = ConversionOperator(node.UnderlyingExpression)
            If method Is Nothing Then
                Return FlowNullability.Unknown
            End If

            If method.ReturnType IsNot Nothing AndAlso Not method.ReturnType.IsReferenceType Then
                Return FlowNullability.NotNull
            End If

            ' The operator call already applied its return contract, including NotNullIfNotNull.
            If node.UnderlyingExpression IsNot Nothing AndAlso node.UnderlyingExpression.Kind = BoundKind.Call Then
                Return result
            End If

            Return CallResult(method, dependentNotNull:=False)
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

            For Each pair In _memberWrites
                Dim written As FlowNullability
                If pair.Value > clock AndAlso after.Members.TryGetValue(pair.Key, written) Then
                    result.Members(pair.Key) = written
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

            Dim members = New Dictionary(Of MemberSlot, FlowNullability)(snapshot.Members)
            Dim memberKeys = members.Keys.ToArray()
            For Each key In memberKeys
                members(key) = FlowNullability.MaybeNull
            Next

            Return New FlowSnapshot(snapshot.Reachable, slots, members)
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

            If left.Slots.Count <> right.Slots.Count OrElse left.Members.Count <> right.Members.Count Then
                Return False
            End If

            For Each pair In left.Slots
                Dim other As FlowNullability
                If Not right.Slots.TryGetValue(pair.Key, other) OrElse other <> pair.Value Then
                    Return False
                End If
            Next

            For Each pair In left.Members
                Dim other As FlowNullability
                If Not right.Members.TryGetValue(pair.Key, other) OrElse other <> pair.Value Then
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

        Private Function ElementFlow(node As BoundForEachStatement, control As Symbol) As FlowNullability
            Dim produced = CollectionElementState(node)
            Dim declared = InitialNullability(control)
            Dim local = TryCast(control, LocalSymbol)
            Dim inferred = local IsNot Nothing AndAlso local.HasInferredType
            If inferred Then
                If produced = FlowNullability.Unknown Then
                    produced = declared
                End If

                If produced = FlowNullability.Unknown Then
                    Return FlowNullability.MaybeNull
                End If

                Return produced
            End If

            If produced = FlowNullability.MaybeNull AndAlso AnnotationOf(control) = NullableAnnotation.NotAnnotated Then
                Warn(ERRID.WRN_PossibleNullAssignment, node.Collection.Syntax, control.Name)
                Return FlowNullability.MaybeNull
            End If

            If declared = FlowNullability.Unknown Then
                Return If(produced = FlowNullability.Unknown, FlowNullability.MaybeNull, produced)
            End If

            Return declared
        End Function

        Private Function CollectionElementState(node As BoundForEachStatement) As FlowNullability
            Dim collectionType = If(node.Collection Is Nothing, Nothing, node.Collection.Type)
            Dim arrayType = TryCast(collectionType, ArrayTypeSymbol)
            If arrayType IsNot Nothing Then
                Return AnnotatedReferenceState(arrayType.ElementType, arrayType.ElementNullableAnnotation)
            End If

            Dim info = node.EnumeratorInfo
            Dim currentState = FlowNullability.Unknown
            If info IsNot Nothing AndAlso info.Current IsNot Nothing Then
                Dim current = info.Current
                If current.Kind = BoundKind.PropertyAccess Then
                    currentState = PropertyResult(DirectCast(current, BoundPropertyAccess).PropertySymbol)
                ElseIf current.Kind = BoundKind.Call Then
                    currentState = CallResult(DirectCast(current, BoundCall).Method, dependentNotNull:=False)
                End If
            End If

            Dim elementType = If(info Is Nothing, Nothing, info.ElementType)
            Dim argumentState = TypeArgumentState(collectionType, elementType)
            If currentState = FlowNullability.Unknown Then
                Return argumentState
            End If

            If argumentState = FlowNullability.Unknown Then
                Return currentState
            End If

            Return MergeNull(currentState, argumentState)
        End Function

        Private Function EvaluateArray(node As BoundArrayAccess) As FlowNullability
            Dim receiver = Evaluate(node.Expression)
            If receiver = FlowNullability.MaybeNull Then
                WarnReceiverSyntax(node.Expression)
            End If

            If Not node.Indices.IsDefault Then
                For Each index In node.Indices
                    Evaluate(index)
                Next
            End If

            Dim arrayType = TryCast(If(node.Expression Is Nothing, Nothing, node.Expression.Type), ArrayTypeSymbol)
            If arrayType Is Nothing Then
                Return FlowNullability.Unknown
            End If

            Return AnnotatedReferenceState(arrayType.ElementType, arrayType.ElementNullableAnnotation)
        End Function

        Private Function EvaluateAwait(node As BoundAwaitOperator) As FlowNullability
            Dim operand = Evaluate(node.Operand)
            InvalidateOurMembers()
            Dim result = AwaitResult(node)
            If result = FlowNullability.Unknown Then
                result = TypeArgumentState(If(node.Operand Is Nothing, Nothing, node.Operand.Type), node.Type)
            End If

            If operand = FlowNullability.MaybeNull AndAlso result = FlowNullability.NotNull Then
                Return FlowNullability.MaybeNull
            End If

            Return result
        End Function

        Private Shared Function TypeArgumentState(container As TypeSymbol, resultType As TypeSymbol) As FlowNullability
            Dim named = TryCast(container, NamedTypeSymbol)
            If named Is Nothing OrElse resultType Is Nothing Then
                Return FlowNullability.Unknown
            End If

            Dim arguments = named.TypeArgumentsNoUseSiteDiagnostics
            Dim annotations = named.GetTypeArgumentNullableAnnotations()
            If arguments.IsDefault OrElse annotations.IsDefault Then
                Return FlowNullability.Unknown
            End If

            For index = 0 To Math.Min(arguments.Length, annotations.Length) - 1
                If annotations(index) <> NullableAnnotation.None AndAlso resultType.IsSameTypeIgnoringAll(arguments(index)) Then
                    Return FromAnnotation(annotations(index))
                End If
            Next

            Return FlowNullability.Unknown
        End Function

        Private Shared Function AwaitResult(node As BoundAwaitOperator) As FlowNullability
            Dim current = node.GetResult
            While current IsNot Nothing
                Select Case current.Kind
                    Case BoundKind.Call
                        Return CallResult(DirectCast(current, BoundCall).Method, dependentNotNull:=False)
                    Case BoundKind.Parenthesized
                        current = DirectCast(current, BoundParenthesized).Expression
                    Case BoundKind.Conversion, BoundKind.DirectCast
                        current = DirectCast(current, BoundConversionOrCast).Operand
                    Case Else
                        Exit While
                End Select
            End While

            If node.Type IsNot Nothing AndAlso Not node.Type.IsReferenceType Then
                Return FlowNullability.NotNull
            End If

            Return FlowNullability.Unknown
        End Function

        Private Function AssignStorage(target As BoundExpression, value As FlowNullability, syntaxExpr As BoundExpression) As Boolean
            target = Unwrap(target)
            Dim arrayAccess = TryCast(target, BoundArrayAccess)
            If arrayAccess IsNot Nothing Then
                AssignArrayElement(arrayAccess, value, syntaxExpr)
                Return True
            End If

            Dim propertyAccess = TryCast(target, BoundPropertyAccess)
            If propertyAccess IsNot Nothing Then
                AssignProperty(propertyAccess, value, syntaxExpr)
                Return True
            End If

            Dim slot = TryBuildSlot(target)
            If slot Is Nothing Then
                Return False
            End If

            AssignMember(slot, value, syntaxExpr, report:=True)
            Return True
        End Function

        Private Sub AssignArrayElement(access As BoundArrayAccess, value As FlowNullability, syntaxExpr As BoundExpression)
            Dim arrayType = TryCast(If(access.Expression Is Nothing, Nothing, access.Expression.Type), ArrayTypeSymbol)
            If arrayType Is Nothing Then
                Return
            End If

            If value = FlowNullability.MaybeNull AndAlso DisallowsAnnotatedElement(arrayType.ElementType, arrayType.ElementNullableAnnotation) AndAlso IsSourceExpression(syntaxExpr) Then
                Warn(ERRID.WRN_PossibleNullAssignment, syntaxExpr.Syntax, DisplayName(access))
            End If
        End Sub

        Private Sub AssignProperty(access As BoundPropertyAccess, value As FlowNullability, syntaxExpr As BoundExpression)
            Dim [property] = access.PropertySymbol
            Dim setter = If([property] Is Nothing, Nothing, [property].SetMethod)
            If setter IsNot Nothing AndAlso setter.ParameterCount > 0 Then
                Dim parameter = setter.Parameters(setter.ParameterCount - 1)
                If value = FlowNullability.MaybeNull AndAlso ArgumentDisallowsNull(parameter) AndAlso IsSourceExpression(syntaxExpr) Then
                    Warn(ERRID.WRN_PossibleNullAssignment, syntaxExpr.Syntax, [property].Name)
                End If
            End If

            Dim slot = TryBuildSlot(access)
            If slot IsNot Nothing Then
                AssignMember(slot, value, syntaxExpr, report:=False)
            Else
                InvalidatePropertyEffects([property], access.ReceiverOpt)
            End If
        End Sub

        Private Sub AssignMember(slot As MemberSlot, value As FlowNullability, syntaxExpr As BoundExpression, report As Boolean)
            Dim annotation = MemberAnnotation(slot)
            If value = FlowNullability.Unknown AndAlso annotation = NullableAnnotation.Annotated Then
                value = FlowNullability.MaybeNull
            End If

            If report AndAlso value = FlowNullability.MaybeNull AndAlso annotation = NullableAnnotation.NotAnnotated AndAlso IsSourceExpression(syntaxExpr) Then
                Warn(ERRID.WRN_PossibleNullAssignment, syntaxExpr.Syntax, slot.LeafSymbol.Name)
            End If

            RemoveDescendants(slot)
            _members(slot) = value
            NoteMemberWrite(slot)
            If value = FlowNullability.NotNull Then
                InvalidateAliasMembers(slot)
            Else
                CopyAliasMembers(slot, value)
            End If
        End Sub

        Private Sub NoteReceiverIdentity(target As Symbol, value As BoundExpression)
            If target Is Nothing OrElse Not IsReceiverSymbol(target) Then
                Return
            End If

            ClearReceiverMembers(target)
            Unlink(target)
            Dim source = TrackedSymbol(value)
            If source IsNot Nothing AndAlso source IsNot target AndAlso IsReceiverSymbol(source) AndAlso IsReferenceSymbol(source) AndAlso IsReferenceSymbol(target) Then
                Link(target, source)
            End If
        End Sub

        Private Sub LearnMember(slot As MemberSlot, isNull As Boolean)
            If slot Is Nothing Then
                Return
            End If

            _members(slot) = If(isNull, FlowNullability.MaybeNull, FlowNullability.NotNull)
            If isNull Then
                CopyAliasMembers(slot, FlowNullability.MaybeNull)
            End If
        End Sub

        Private Sub LearnMemberIfStable(slot As MemberSlot, isNull As Boolean, baseline As Dictionary(Of Symbol, Integer))
            If slot Is Nothing Then
                Return
            End If

            If slot.Receiver IsNot Nothing AndAlso baseline IsNot Nothing Then
                Dim thenStamp = 0
                baseline.TryGetValue(slot.Receiver, thenStamp)
                Dim nowStamp = 0
                _writes.TryGetValue(slot.Receiver, nowStamp)
                If nowStamp <> thenStamp Then
                    Return
                End If
            End If

            LearnMember(slot, isNull)
        End Sub

        Private Function StateOfMember(slot As MemberSlot) As FlowNullability
            Dim state As FlowNullability
            If slot IsNot Nothing AndAlso _members.TryGetValue(slot, state) AndAlso state <> FlowNullability.Unknown Then
                Return state
            End If

            Return FromAnnotation(MemberAnnotation(slot))
        End Function

        Private Shared Function MemberAnnotation(slot As MemberSlot) As NullableAnnotation
            If slot Is Nothing OrElse slot.LeafSymbol Is Nothing Then
                Return NullableAnnotation.None
            End If

            Dim field = TryCast(slot.LeafSymbol, FieldSymbol)
            If field IsNot Nothing Then
                Return field.GetNullableAnnotation()
            End If

            Dim [property] = TryCast(slot.LeafSymbol, PropertySymbol)
            If [property] IsNot Nothing Then
                Return [property].GetNullableAnnotation()
            End If

            Return NullableAnnotation.None
        End Function

        Private Shared Function PropertyResult([property] As PropertySymbol) As FlowNullability
            If [property] Is Nothing Then
                Return FlowNullability.Unknown
            End If

            Dim getter = [property].GetMethod
            If getter IsNot Nothing Then
                Return CallResult(getter, dependentNotNull:=False)
            End If

            Return FromAnnotation([property].GetNullableAnnotation())
        End Function

        Private Shared Function AnnotatedReferenceState(elementType As TypeSymbol, annotation As NullableAnnotation) As FlowNullability
            If elementType IsNot Nothing AndAlso Not elementType.IsReferenceType Then
                Return FlowNullability.NotNull
            End If

            Dim state = FromAnnotation(annotation)
            If state = FlowNullability.Unknown Then
                Return FlowNullability.NotNull
            End If

            Return state
        End Function

        Private Shared Function DisallowsAnnotatedElement(elementType As TypeSymbol, annotation As NullableAnnotation) As Boolean
            Return elementType IsNot Nothing AndAlso elementType.IsReferenceType AndAlso annotation = NullableAnnotation.NotAnnotated
        End Function

        Private Function TryBuildSlot(expr As BoundExpression) As MemberSlot
            Dim path As New List(Of Symbol)()
            Dim current = StripFlow(expr)
            Dim isShared = False
            While current IsNot Nothing
                If current.Kind = BoundKind.FieldAccess Then
                    Dim field = DirectCast(current, BoundFieldAccess)
                    If field.FieldSymbol Is Nothing Then
                        Return Nothing
                    End If

                    path.Insert(0, field.FieldSymbol)
                    isShared = field.FieldSymbol.IsShared
                    current = If(isShared, Nothing, StripFlow(field.ReceiverOpt))
                ElseIf current.Kind = BoundKind.PropertyAccess Then
                    Dim access = DirectCast(current, BoundPropertyAccess)
                    If Not IsStableProperty(access.PropertySymbol) Then
                        Return Nothing
                    End If

                    path.Insert(0, access.PropertySymbol)
                    isShared = access.PropertySymbol.IsShared
                    current = If(isShared, Nothing, StripFlow(access.ReceiverOpt))
                Else
                    Exit While
                End If
            End While

            If path.Count = 0 Then
                Return Nothing
            End If

            Dim receiver As Symbol = Nothing
            If Not isShared Then
                If current Is Nothing Then
                    Return Nothing
                End If

                Select Case current.Kind
                    Case BoundKind.MeReference, BoundKind.MyClassReference, BoundKind.MyBaseReference
                        receiver = Nothing
                    Case Else
                        receiver = TrackedSymbol(current)
                        If Not IsReceiverSymbol(receiver) Then
                            Return Nothing
                        End If
                End Select
            End If

            Return New MemberSlot(receiver, isShared, path.ToArray())
        End Function

        Private Shared Function IsStableProperty([property] As PropertySymbol) As Boolean
            If [property] Is Nothing OrElse [property].ParameterCount <> 0 OrElse [property].IsOverridable OrElse [property].IsMustOverride Then
                Return False
            End If

            Dim source = TryCast([property].OriginalDefinition, SourcePropertySymbol)
            Return source IsNot Nothing AndAlso source.IsAutoProperty
        End Function

        Private Shared Function IsReceiverSymbol(symbol As Symbol) As Boolean
            Dim local = TryCast(symbol, LocalSymbol)
            If local IsNot Nothing Then
                Return Not local.IsConst
            End If

            Return TypeOf symbol Is ParameterSymbol
        End Function

        Private Shared Function IsReferenceSymbol(symbol As Symbol) As Boolean
            Dim local = TryCast(symbol, LocalSymbol)
            If local IsNot Nothing Then
                Return local.Type IsNot Nothing AndAlso local.Type.IsReferenceType
            End If

            Dim parameter = TryCast(symbol, ParameterSymbol)
            Return parameter IsNot Nothing AndAlso parameter.Type IsNot Nothing AndAlso parameter.Type.IsReferenceType
        End Function

        Private Sub Link(left As Symbol, right As Symbol)
            Dim leftRoot = AliasRoot(left)
            Dim rightRoot = AliasRoot(right)
            If leftRoot IsNot rightRoot Then
                _aliases(leftRoot) = rightRoot
            End If
        End Sub

        Private Sub Unlink(symbol As Symbol)
            If symbol Is Nothing OrElse Not _aliases.ContainsKey(symbol) Then
                Dim stale = _aliases.Where(Function(pair) pair.Value Is symbol).Select(Function(pair) pair.Key).ToArray()
                For Each key In stale
                    _aliases.Remove(key)
                Next

                Return
            End If

            Dim parent = _aliases(symbol)
            _aliases.Remove(symbol)
            Dim children = _aliases.Where(Function(pair) pair.Value Is symbol).Select(Function(pair) pair.Key).ToArray()
            For Each child In children
                _aliases(child) = parent
            Next
        End Sub

        Private Function AliasRoot(symbol As Symbol) As Symbol
            Dim current = symbol
            While current IsNot Nothing AndAlso _aliases.ContainsKey(current)
                current = _aliases(current)
            End While

            Return current
        End Function

        Private Function AliasGroup(symbol As Symbol) As List(Of Symbol)
            Dim root = AliasRoot(symbol)
            Dim group As New List(Of Symbol)()
            group.Add(root)
            For Each pair In _aliases
                If AliasRoot(pair.Key) Is root Then
                    group.Add(pair.Key)
                End If
            Next

            Return group
        End Function

        Private Sub InvalidateAliasMembers(slot As MemberSlot)
            If slot Is Nothing OrElse slot.IsShared OrElse slot.Receiver Is Nothing Then
                Return
            End If

            For Each other In AliasGroup(slot.Receiver)
                If other Is slot.Receiver Then
                    Continue For
                End If

                Dim aliased = slot.WithReceiver(other)
                RemoveDescendants(aliased)
                _members(aliased) = FlowNullability.MaybeNull
            Next
        End Sub

        Private Sub CopyAliasMembers(slot As MemberSlot, value As FlowNullability)
            If slot Is Nothing OrElse slot.IsShared OrElse slot.Receiver Is Nothing Then
                Return
            End If

            For Each other In AliasGroup(slot.Receiver)
                If other Is slot.Receiver Then
                    Continue For
                End If

                Dim aliased = slot.WithReceiver(other)
                RemoveDescendants(aliased)
                _members(aliased) = value
            Next
        End Sub

        Private Sub ClearReceiverMembers(receiver As Symbol)
            Dim keys = _members.Keys.ToArray()
            For Each key In keys
                If key.Receiver Is receiver Then
                    _members.Remove(key)
                End If
            Next
        End Sub

        Private Sub RemoveDescendants(slot As MemberSlot)
            Dim keys = _members.Keys.ToArray()
            For Each key In keys
                If key.StartsWith(slot) AndAlso key.Depth > slot.Depth Then
                    _members.Remove(key)
                End If
            Next
        End Sub

        Private Sub InvalidateReceiverMembers(receiver As Symbol)
            If receiver Is Nothing Then
                Return
            End If

            For Each symbol In AliasGroup(receiver)
                Dim keys = _members.Keys.ToArray()
                For Each key In keys
                    If key.Receiver Is symbol Then
                        _members.Remove(key)
                    End If
                Next
            Next
        End Sub

        Private Sub InvalidateOurMembers()
            Dim keys = _members.Keys.ToArray()
            For Each key In keys
                If key.IsShared OrElse key.Receiver Is Nothing Then
                    _members.Remove(key)
                End If
            Next
        End Sub

        Private Sub InvalidateCapturedReceivers()
            InvalidateOurMembers()
            For Each symbol In _captured
                InvalidateReceiverMembers(symbol)
            Next
        End Sub

        Private Sub InvalidateCallSnapshots(method As MethodSymbol, receiver As BoundExpression, records As List(Of ArgumentRecord), whenTrue As FlowSnapshot, whenFalse As FlowSnapshot)
            Dim touched As New List(Of Symbol)()
            Dim touchOurs = ReceiverIsInstance(receiver) OrElse MethodCanTouchOurState(method, receiver) OrElse (method IsNot Nothing AndAlso method.MethodKind = MethodKind.DelegateInvoke)
            Dim receiverSymbol = TrackedSymbol(receiver)
            If receiverSymbol IsNot Nothing Then
                touched.AddRange(AliasGroup(receiverSymbol))
            End If

            If records IsNot Nothing Then
                For Each record In records
                    If record.HasSlot AndAlso IsReferenceSymbol(record.Slot) Then
                        touched.AddRange(AliasGroup(record.Slot))
                    End If
                Next
            End If

            If method IsNot Nothing AndAlso method.MethodKind = MethodKind.DelegateInvoke Then
                touched.AddRange(_captured)
            End If

            StripMembers(whenTrue, touchOurs, touched)
            StripMembers(whenFalse, touchOurs, touched)
        End Sub

        Private Shared Sub StripMembers(snapshot As FlowSnapshot, touchOurs As Boolean, touched As List(Of Symbol))
            If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                Return
            End If

            Dim keys = snapshot.Members.Keys.ToArray()
            For Each key In keys
                Dim drop = (touchOurs AndAlso (key.IsShared OrElse key.Receiver Is Nothing)) OrElse Touches(key.Receiver, touched)
                If drop Then
                    snapshot.Members.Remove(key)
                End If
            Next
        End Sub

        Private Shared Function Touches(receiver As Symbol, touched As List(Of Symbol)) As Boolean
            If receiver Is Nothing Then
                Return False
            End If

            For Each symbol In touched
                If symbol Is receiver Then
                    Return True
                End If
            Next

            Return False
        End Function

        Private Sub InvalidatePropertyEffects([property] As PropertySymbol, receiver As BoundExpression)
            If [property] IsNot Nothing AndAlso [property].IsShared AndAlso [property].ContainingType Is _method.ContainingType Then
                InvalidateOurMembers()
            End If

            If ReceiverIsInstance(receiver) Then
                InvalidateOurMembers()
                Return
            End If

            Dim receiverSymbol = TrackedSymbol(receiver)
            If receiverSymbol IsNot Nothing Then
                InvalidateReceiverMembers(receiverSymbol)
            End If
        End Sub

        Private Function MethodCanTouchOurState(method As MethodSymbol, receiver As BoundExpression) As Boolean
            If method Is Nothing Then
                Return ReceiverIsInstance(receiver)
            End If

            If method.MethodKind = MethodKind.DelegateInvoke Then
                Return True
            End If

            If Not method.IsShared Then
                Return False
            End If

            Dim container = method.ContainingType
            Return container IsNot Nothing AndAlso (container Is _method.ContainingType OrElse container.Equals(_method.ContainingType))
        End Function

        Private Shared Function ReceiverIsInstance(receiver As BoundExpression) As Boolean
            receiver = Unwrap(receiver)
            If receiver Is Nothing Then
                Return False
            End If

            Select Case receiver.Kind
                Case BoundKind.MeReference, BoundKind.MyClassReference, BoundKind.MyBaseReference
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Sub NoteMemberWrite(slot As MemberSlot)
            _writeClock += 1
            _memberWrites(slot) = _writeClock
            ObserveException()
        End Sub

        Private Function TryGetMemberNullTest(condition As BoundExpression, ByRef slot As MemberSlot, ByRef trueWhenNull As Boolean) As Boolean
            slot = Nothing
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

            Dim tested As BoundExpression = Nothing
            If IsNothing(binary.Right) Then
                tested = binary.Left
            ElseIf IsNothing(binary.Left) Then
                tested = binary.Right
            End If

            slot = TryBuildSlot(tested)
            If slot Is Nothing Then
                Return False
            End If

            trueWhenNull = op = BinaryOperatorKind.Is
            Return True
        End Function

        Private Shared Function IsSourceExpression(expr As BoundExpression) As Boolean
            Return expr IsNot Nothing AndAlso Not expr.WasCompilerGenerated AndAlso expr.Syntax IsNot Nothing AndAlso expr.Syntax.Span.Length > 0
        End Function

        Private Function DisplayName(expr As BoundExpression) As String
            Dim symbol = TrackedSymbol(expr)
            If symbol IsNot Nothing Then
                Return symbol.Name
            End If

            expr = Unwrap(expr)
            Dim field = TryCast(expr, BoundFieldAccess)
            If field IsNot Nothing Then
                Return field.FieldSymbol.Name
            End If

            Dim [property] = TryCast(expr, BoundPropertyAccess)
            If [property] IsNot Nothing Then
                Return [property].PropertySymbol.Name
            End If

            Dim arrayAccess = TryCast(expr, BoundArrayAccess)
            If arrayAccess IsNot Nothing AndAlso arrayAccess.Syntax IsNot Nothing Then
                Return arrayAccess.Syntax.ToString()
            End If

            Return "expression"
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
