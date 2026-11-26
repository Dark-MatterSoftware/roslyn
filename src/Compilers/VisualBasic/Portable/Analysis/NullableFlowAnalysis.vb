' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Runtime.CompilerServices
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
    '''
    ''' Constructor initialization is separate from the declared annotation.
    ''' A non-nullable field or auto-property starts MaybeNull until an initializer
    ''' or assignment proves NotNull. The declaration itself is not that proof.
    ''' Initializers run only for a constructor that does not delegate with Me.New,
    ''' and they run after a base constructor returns. Shared constructors use the
    ''' shared initializer list. A normal exit is a reachable Return, Exit Sub,
    ''' Exit Function, Exit Property, or fall-through. Throw does not establish
    ''' a member postcondition. MemberNotNull is applied to the callee receiver
    ''' after invalidation. A method's own attribute is not evidence that its body
    ''' satisfies that attribute.
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

            Friend Function SamePath(other As MemberSlot) As Boolean
                If other Is Nothing OrElse IsShared <> other.IsShared OrElse _path.Length <> other._path.Length Then
                    Return False
                End If

                For index = 0 To _path.Length - 1
                    If Not SameSymbol(_path(index), other._path(index)) Then
                        Return False
                    End If
                Next

                Return True
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
                If left Is right Then
                    Return True
                End If

                If left Is Nothing OrElse right Is Nothing Then
                    Return False
                End If

                Return left.Equals(right)
            End Function
        End Class

        Private NotInheritable Class FlowSnapshot
            Friend ReadOnly Reachable As Boolean
            Friend ReadOnly Slots As Dictionary(Of Symbol, FlowNullability)
            Friend ReadOnly Members As Dictionary(Of MemberSlot, FlowNullability)
            Friend ReadOnly Escaped As HashSet(Of Symbol)
            Friend ReadOnly Published As HashSet(Of Symbol)
            Friend ReadOnly Original As Dictionary(Of Symbol, FlowNullability)
            Friend ReadOnly Carriers As Dictionary(Of Symbol, Symbol)

            Friend Sub New(reachable As Boolean, slots As Dictionary(Of Symbol, FlowNullability), members As Dictionary(Of MemberSlot, FlowNullability), escaped As HashSet(Of Symbol), published As HashSet(Of Symbol), original As Dictionary(Of Symbol, FlowNullability), carriers As Dictionary(Of Symbol, Symbol))
                Me.Reachable = reachable
                Me.Slots = slots
                Me.Members = members
                Me.Escaped = escaped
                Me.Published = published
                Me.Original = If(original, New Dictionary(Of Symbol, FlowNullability)())
                Me.Carriers = If(carriers, New Dictionary(Of Symbol, Symbol)())
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
        Private _escaped As HashSet(Of Symbol)
        Private _published As HashSet(Of Symbol)
        Private ReadOnly _aliases As Dictionary(Of Symbol, Symbol)
        Private ReadOnly _memberWrites As Dictionary(Of MemberSlot, Integer)
        Private ReadOnly _jumps As Dictionary(Of LabelSymbol, FlowSnapshot)
        Private ReadOnly _captured As HashSet(Of Symbol)
        Private ReadOnly _visitedLabels As HashSet(Of LabelSymbol)
        Private ReadOnly _reported As HashSet(Of String)
        Private ReadOnly _writes As Dictionary(Of Symbol, Integer)
        Private _original As Dictionary(Of Symbol, FlowNullability)
        Private _carriers As Dictionary(Of Symbol, Symbol)
        Private _returnPreserved As HashSet(Of Symbol)
        Private _hasReturnPreserved As Boolean
        Private _sawCycle As Boolean
        Private ReadOnly _exceptionStates As List(Of FlowSnapshot)
        Private _writeClock As Integer
        Private _isolation As Integer
        Private _needsRewalk As Boolean
        Private _summaryOnly As Boolean
        Private _bodyProof As Boolean
        Private _initializerPass As Boolean
        Private _applyInitializersAfterBase As Boolean
        Private _initializerClock As Integer
        Private _initializersApplied As Boolean
        Private _userMayHaveMutated As Boolean
        Private _body As BoundBlock
        Private _pendingContracts As List(Of Symbol)
        Private ReadOnly _normalExits As New List(Of NormalExit)()

        Private Structure NormalExit
            Friend Snapshot As FlowSnapshot
            Friend Syntax As SyntaxNode
            Friend Sense As ExitSense
            Friend HasReturn As Boolean
            Friend ReturnState As FlowNullability
            Friend Preserved As HashSet(Of Symbol)
            Friend Unwritten As HashSet(Of Symbol)
        End Structure

        Private Enum ExitSense
            Any
            WhenTrue
            WhenFalse
            UnknownBoolean
        End Enum

        Private NotInheritable Class ConstructorFacts
            Friend ReadOnly Established As New Dictionary(Of MethodSymbol, HashSet(Of Symbol))()
            Friend ReadOnly Completes As New Dictionary(Of MethodSymbol, Boolean)()
            Friend ReadOnly InProgress As New HashSet(Of MethodSymbol)()
            Friend ReadOnly Initializers As New Dictionary(Of Symbol, Dictionary(Of MemberSlot, FlowNullability))()
            Friend ReadOnly InitializersBound As New Dictionary(Of Symbol, ImmutableArray(Of BoundInitializer))()
            Friend ReadOnly ContractCallees As New Dictionary(Of MethodSymbol, ImmutableArray(Of MethodSymbol))()
            Friend ReadOnly BodyProofs As New Dictionary(Of MethodSymbol, BodyProof)()
            Friend ReadOnly BodyInProgress As New HashSet(Of MethodSymbol)()
        End Class

        Private NotInheritable Class BodyProof
            Friend Shared ReadOnly Cyclic As New BodyProof() With {.ReachesCycle = True}
            Friend NoNormalExit As Boolean
            Friend ReturnNotNull As Boolean
            Friend ReachesCycle As Boolean
            Friend NotNullParameters As HashSet(Of Integer)
        End Class

        Private NotInheritable Class ContractCalleeWalker
            Inherits BoundTreeWalkerWithStackGuard

            Friend ReadOnly Callees As New List(Of MethodSymbol)()

            Public Overrides Function VisitCall(node As BoundCall) As BoundNode
                If node.Method IsNot Nothing AndAlso HasMemberContract(node.Method) Then
                    Callees.Add(node.Method)
                End If

                Return MyBase.VisitCall(node)
            End Function
        End Class

        Private Shared ReadOnly Facts As New ConditionalWeakTable(Of VisualBasicCompilation, ConstructorFacts)()

        Private Sub New(method As MethodSymbol, diagnostics As DiagnosticBag, project As NullableContextOptions)
            _method = method
            _diagnostics = diagnostics
            _project = project
            _state = New Dictionary(Of Symbol, FlowNullability)()
            _members = New Dictionary(Of MemberSlot, FlowNullability)()
            _escaped = New HashSet(Of Symbol)()
            _published = New HashSet(Of Symbol)()
            _aliases = New Dictionary(Of Symbol, Symbol)()
            _memberWrites = New Dictionary(Of MemberSlot, Integer)()
            _jumps = New Dictionary(Of LabelSymbol, FlowSnapshot)()
            _captured = New HashSet(Of Symbol)()
            _visitedLabels = New HashSet(Of LabelSymbol)()
            _reported = New HashSet(Of String)()
            _writes = New Dictionary(Of Symbol, Integer)()
            _original = New Dictionary(Of Symbol, FlowNullability)()
            _carriers = New Dictionary(Of Symbol, Symbol)()
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
            walker.Prepare(body)
            If walker.SkipBecauseInitializersAreCached() Then
                Return
            End If

            Dim proofKey = method.OriginalDefinition
            Dim stored = Facts.GetOrCreateValue(compilation)
            SyncLock stored
                stored.BodyInProgress.Add(proofKey)
            End SyncLock

            Try
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

                walker.FinishPass()
            Finally
                SyncLock stored
                    stored.BodyInProgress.Remove(proofKey)
                End SyncLock
            End Try
        End Sub

        Private Sub BeginPass()
            _reachable = True
            _state = New Dictionary(Of Symbol, FlowNullability)()
            _members = New Dictionary(Of MemberSlot, FlowNullability)()
            _escaped = New HashSet(Of Symbol)()
            _published = New HashSet(Of Symbol)()
            _aliases.Clear()
            _memberWrites.Clear()
            _captured.Clear()
            _visitedLabels.Clear()
            _writes.Clear()
            _original = New Dictionary(Of Symbol, FlowNullability)()
            _carriers = New Dictionary(Of Symbol, Symbol)()
            _sawCycle = False
            _hasReturnPreserved = False
            _exceptionStates.Clear()
            _isolation = 0
            _normalExits.Clear()
            _applyInitializersAfterBase = False
            _initializersApplied = False
            _userMayHaveMutated = False
            If IsConstructor(_method) AndAlso Not _initializerPass Then
                SeedConstructorMembers()
                If _method.IsShared Then
                    ' A shared constructor has no base call. Compiler-generated calls may drop
                    ' shared facts; a later user call is a possible mutation and is not restored.
                    ApplyInitializers(publish:=Not _summaryOnly)
                    _initializerClock = _writeClock
                    _initializersApplied = True
                Else
                    Dim chained = FirstConstructorCallTarget()
                    If chained IsNot Nothing AndAlso IsSameTypeConstructor(chained) Then
                        ' Me.New runs the target constructor, including its initializers.
                    ElseIf chained IsNot Nothing Then
                        ' Initializers run after the base constructor returns.
                        _applyInitializersAfterBase = True
                    Else
                        ApplyInitializers(publish:=Not _summaryOnly)
                        _initializerClock = _writeClock
                        _initializersApplied = True
                    End If
                End If
            End If

            For Each parameter In _method.Parameters
                If parameter.IsMe Then
                    Continue For
                End If

                If IsTrackedReference(parameter) Then
                    Dim initial = InitialNullability(parameter)
                    _state(parameter) = initial
                    ' Unknown means the original argument was proven null. A maybe-null
                    ' parameter still imposes a NotNullIfNotNull obligation until a test proves otherwise.
                    _original(parameter) = If(initial = FlowNullability.NotNull, FlowNullability.NotNull, FlowNullability.MaybeNull)
                End If

                If IsReferenceSymbol(parameter) Then
                    ' A parameter is reachable by the caller, so it may be the same object as another compatible parameter.
                    _escaped.Add(parameter)
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
            Dim exitsAtTry = _normalExits.Count
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
                Dim exitsBeforeFinally = _normalExits.Count
                SetSnapshot(MergeSnapshots(handlerState, leaving))
                Visit(node.FinallyBlockOpt)
                Dim finallyCompleted = _reachable
                If leaving.Reachable AndAlso finallyCompleted Then
                    ApplyFinallyWrites(leaving, clock)
                Else
                    _reachable = False
                End If

                If Not finallyCompleted Then
                    ' A Finally that does not complete cancels exits recorded in this Try.
                    While _normalExits.Count > exitsAtTry
                        _normalExits.RemoveAt(_normalExits.Count - 1)
                    End While

                    ApplyFinallyToExits(_normalExits.Count, clock)
                Else
                    ApplyFinallyToExits(exitsBeforeFinally, clock)
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

            Dim sense = ExitSense.Any
            If node.ExpressionOpt IsNot Nothing Then
                Dim result = Evaluate(node.ExpressionOpt)
                If result = FlowNullability.MaybeNull AndAlso ReturnDisallowsNull() Then
                    Warn(ERRID.WRN_PossibleNullReturn, node.ExpressionOpt.Syntax)
                End If

                sense = ReturnSense(node.ExpressionOpt)
                _hasReturnPreserved = True
                _returnPreserved = PreservedBy(node.ExpressionOpt)
                NoteNormalExit(node.Syntax, sense, hasReturn:=True, returnState:=result)
                _hasReturnPreserved = False
            Else
                NoteNormalExit(node.Syntax, sense)
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
                If IsMethodExit(node) Then
                    Dim hasReturn = _method IsNot Nothing AndAlso _method.MethodKind = MethodKind.Ordinary AndAlso _method.ReturnType IsNot Nothing AndAlso _method.ReturnType.SpecialType <> SpecialType.System_Void
                    NoteNormalExit(node.Syntax, ExitSense.Any, hasReturn, If(hasReturn, FunctionReturnState(Capture()), FlowNullability.NotNull))
                End If

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

        Public Overrides Function VisitFieldInitializer(node As BoundFieldInitializer) As BoundNode
            If Not _reachable Then
                Return Nothing
            End If

            Dim value = Evaluate(node.InitialValue)
            For Each field In node.InitializedFields
                If field Is Nothing OrElse IsBackingField(field) Then
                    Continue For
                End If

                AssignMember(SlotFor(field), value, node.InitialValue, report:=True)
            Next

            Return Nothing
        End Function

        Public Overrides Function VisitPropertyInitializer(node As BoundPropertyInitializer) As BoundNode
            If Not _reachable Then
                Return Nothing
            End If

            Dim value = Evaluate(node.InitialValue)
            For Each [property] In node.InitializedProperties
                If [property] Is Nothing OrElse Not IsAutoLike([property]) Then
                    Continue For
                End If

                AssignMember(SlotFor([property]), value, node.InitialValue, report:=True)
            Next

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

            Dim propertyAccess = TryCast(expr, BoundPropertyAccess)
            If propertyAccess IsNot Nothing Then
                EvaluateProperty(propertyAccess)
                Dim propertySplit As New ConditionSplit()
                Dim afterProperty = Capture()
                propertySplit.WhenTrue = CloneSnapshot(afterProperty)
                propertySplit.WhenFalse = CloneSnapshot(afterProperty)
                Dim getter = If(propertyAccess.PropertySymbol Is Nothing, Nothing, propertyAccess.PropertySymbol.GetMethod)
                If getter IsNot Nothing AndAlso Not SameMethod(getter, _method) Then
                    ApplyConditionalMembers(getter, propertyAccess.ReceiverOpt, propertySplit.WhenTrue, sense:=True)
                    ApplyConditionalMembers(getter, propertyAccess.ReceiverOpt, propertySplit.WhenFalse, sense:=False)
                End If

                Return propertySplit
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
            Dim effect = AnalyzeInvocation(node.Method, node.Arguments, node.DefaultArguments, node.ReceiverOpt, node.WasCompilerGenerated)
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

            Dim getter = If(node.PropertySymbol Is Nothing, Nothing, node.PropertySymbol.GetMethod)
            If getter IsNot Nothing AndAlso Not SameMethod(getter, _method) Then
                ApplyUnconditionalMembers(getter, node.ReceiverOpt)
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
            _pendingContracts = Nothing
            Dim effect = AnalyzeInvocation(node.ConstructorOpt, node.Arguments, node.DefaultArguments, receiver:=Nothing, compilerGenerated:=node.WasCompilerGenerated)
            SetSnapshot(MergeSnapshots(effect.WhenTrue, effect.WhenFalse))
            If node.ConstructorOpt IsNot Nothing Then
                _pendingContracts = ResolvedMembers(node.ConstructorOpt, node.ConstructorOpt.NotNullMembers)
            End If
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
            Dim effect = AnalyzeInvocation(node.Method, node.Arguments, node.DefaultArguments, node.ReceiverOpt, node.WasCompilerGenerated)
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
        Private Function AnalyzeInvocation(method As MethodSymbol, arguments As ImmutableArray(Of BoundExpression), defaultArguments As BitVector, receiver As BoundExpression, compilerGenerated As Boolean) As InvocationEffect
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
                If parameter IsNot Nothing AndAlso parameter.IsByRef AndAlso IsConstructor(_method) Then
                    NoteByRefMember(ArgumentValue(argument))
                End If
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

            Dim observed = If(SameMethod(method, _method), Nothing, ObservedProof(method))
            Dim doesNotReturn = If(observed IsNot Nothing, observed.NoNormalExit, method IsNot Nothing AndAlso Not SameMethod(method, _method) AndAlso HasContract(method.FlowAnalysisAnnotations, FlowAnalysisAnnotations.DoesNotReturn))
            If doesNotReturn Then
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
                If SameMethod(method, _method) AndAlso HasConditionalOutput(annotations) Then
                    Continue For
                End If

                If observed IsNot Nothing Then
                    If observed.NotNullParameters IsNot Nothing AndAlso observed.NotNullParameters.Contains(record.Parameter.Ordinal) Then
                        appliedTrue(record.Slot) = FlowNullability.NotNull
                        appliedFalse(record.Slot) = FlowNullability.NotNull
                        whenTrue.Slots(record.Slot) = FlowNullability.NotNull
                        whenFalse.Slots(record.Slot) = FlowNullability.NotNull
                    End If
                Else
                    ApplyOutput(whenTrue, appliedTrue, record, annotations, sense:=True)
                    ApplyOutput(whenFalse, appliedFalse, record, annotations, sense:=False)
                End If
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
            NoteCalleeEffects(method, receiver, whenTrue, whenFalse, compilerGenerated)
            effect.WhenTrue = whenTrue
            effect.WhenFalse = whenFalse
            effect.Result = CallResult(method, dependentNotNull)
            If SameMethod(method, _method) AndAlso method.ReturnType IsNot Nothing AndAlso method.ReturnType.IsReferenceType Then
                effect.Result = FlowNullability.MaybeNull
            ElseIf observed IsNot Nothing AndAlso method.ReturnType IsNot Nothing AndAlso method.ReturnType.IsReferenceType AndAlso Not dependentNotNull Then
                effect.Result = If(observed.ReturnNotNull, FlowNullability.NotNull, FlowNullability.MaybeNull)
            End If
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
            Dim parameter = TryCast(symbol, ParameterSymbol)
            If parameter IsNot Nothing AndAlso Not _writes.ContainsKey(parameter) Then
                ' A later assignment must not erase whether the original argument was null.
                _original(parameter) = If(isNull, FlowNullability.Unknown, FlowNullability.NotNull)
            End If
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
            UpdateCarrier(target, TrackedSymbol(syntaxExpr))
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

        Private Shared Function CopyStates(source As Dictionary(Of Symbol, FlowNullability)) As Dictionary(Of Symbol, FlowNullability)
            If source Is Nothing Then
                Return New Dictionary(Of Symbol, FlowNullability)()
            End If

            Return New Dictionary(Of Symbol, FlowNullability)(source)
        End Function

        Private Shared Function CopyCarriers(source As Dictionary(Of Symbol, Symbol)) As Dictionary(Of Symbol, Symbol)
            If source Is Nothing Then
                Return New Dictionary(Of Symbol, Symbol)()
            End If

            Return New Dictionary(Of Symbol, Symbol)(source)
        End Function

        Private Shared Function MergeOriginal(left As Dictionary(Of Symbol, FlowNullability), right As Dictionary(Of Symbol, FlowNullability)) As Dictionary(Of Symbol, FlowNullability)
            Dim result As New Dictionary(Of Symbol, FlowNullability)()
            If left IsNot Nothing Then
                For Each pair In left
                    Dim other As FlowNullability
                    If right IsNot Nothing AndAlso right.TryGetValue(pair.Key, other) Then
                        result(pair.Key) = If(pair.Value = other, pair.Value, FlowNullability.MaybeNull)
                    Else
                        result(pair.Key) = FlowNullability.MaybeNull
                    End If
                Next
            End If

            If right IsNot Nothing Then
                For Each pair In right
                    If Not result.ContainsKey(pair.Key) Then
                        result(pair.Key) = FlowNullability.MaybeNull
                    End If
                Next
            End If

            Return result
        End Function

        Private Shared Function MergeCarriers(left As Dictionary(Of Symbol, Symbol), right As Dictionary(Of Symbol, Symbol)) As Dictionary(Of Symbol, Symbol)
            Dim result As New Dictionary(Of Symbol, Symbol)()
            If left Is Nothing OrElse right Is Nothing Then
                Return result
            End If

            For Each pair In left
                Dim other As Symbol = Nothing
                If right.TryGetValue(pair.Key, other) AndAlso other IsNot Nothing AndAlso other.Equals(pair.Value) Then
                    result(pair.Key) = pair.Value
                End If
            Next

            Return result
        End Function

        Private Sub UpdateCarrier(target As Symbol, source As Symbol)
            If target Is Nothing Then
                Return
            End If

            Dim carried As Symbol = Nothing
            Dim parameter = TryCast(source, ParameterSymbol)
            If parameter IsNot Nothing AndAlso Not _writes.ContainsKey(parameter) Then
                _carriers(target) = parameter
            ElseIf source IsNot Nothing AndAlso _carriers.TryGetValue(source, carried) Then
                _carriers(target) = carried
            Else
                _carriers.Remove(target)
            End If
        End Sub

        Private Function PreservedFunctionValue() As HashSet(Of Symbol)
            Dim result As New HashSet(Of Symbol)()
            For Each pair In _carriers
                Dim local = TryCast(pair.Key, LocalSymbol)
                If local IsNot Nothing AndAlso local.IsFunctionValue AndAlso pair.Value IsNot Nothing Then
                    result.Add(pair.Value)
                End If
            Next

            Return result
        End Function

        Private Function UnwrittenParameters() As HashSet(Of Symbol)
            Dim result As New HashSet(Of Symbol)()
            If _method Is Nothing Then
                Return result
            End If

            For Each parameter In _method.Parameters
                If Not parameter.IsMe AndAlso Not _writes.ContainsKey(parameter) Then
                    result.Add(parameter)
                End If
            Next

            Return result
        End Function

        Private Function PreservedBy(expr As BoundExpression) As HashSet(Of Symbol)
            Dim result As New HashSet(Of Symbol)()
            If expr Is Nothing Then
                Return result
            End If

            AddPreservedSymbol(result, TrackedSymbol(expr))
            Dim conditional = TryCast(Unwrap(expr), BoundBinaryConditionalExpression)
            If conditional IsNot Nothing Then
                ' If(left, right) returns the first non-null operand, so each original input remains a valid result.
                AddPreservedSymbol(result, TrackedSymbol(conditional.TestExpression))
                AddPreservedSymbol(result, TrackedSymbol(conditional.ElseExpression))
            End If

            Dim invocation = TryCast(Unwrap(expr), BoundCall)
            If invocation Is Nothing OrElse invocation.Method Is Nothing OrElse SameMethod(invocation.Method, _method) Then
                Return result
            End If

            If ObservedProof(invocation.Method) IsNot Nothing OrElse invocation.Method.ReturnNotNullIfParameterNotNull.IsEmpty Then
                Return result
            End If

            For index = 0 To invocation.Arguments.Length - 1
                If index >= invocation.Method.ParameterCount Then
                    Exit For
                End If

                Dim parameter = invocation.Method.Parameters(index)
                If parameter IsNot Nothing AndAlso NamesMatch(invocation.Method.ReturnNotNullIfParameterNotNull, parameter.Name) Then
                    AddPreservedSymbol(result, TrackedSymbol(invocation.Arguments(index)))
                End If
            Next

            Return result
        End Function

        Private Sub AddPreservedSymbol(result As HashSet(Of Symbol), symbol As Symbol)
            Dim parameter = TryCast(symbol, ParameterSymbol)
            If parameter IsNot Nothing AndAlso Not _writes.ContainsKey(parameter) Then
                result.Add(parameter)
                Return
            End If

            Dim carried As Symbol = Nothing
            If symbol IsNot Nothing AndAlso _carriers.TryGetValue(symbol, carried) AndAlso carried IsNot Nothing Then
                result.Add(carried)
            End If
        End Sub

        Private Shared Function OriginalObligates(wayOut As NormalExit, input As Symbol) As Boolean
            Dim state As FlowNullability
            If wayOut.Snapshot IsNot Nothing AndAlso wayOut.Snapshot.Original IsNot Nothing AndAlso wayOut.Snapshot.Original.TryGetValue(input, state) Then
                Return state <> FlowNullability.Unknown
            End If

            Return True
        End Function

        Private Shared Function OutputPreserves(wayOut As NormalExit, output As Symbol, input As Symbol) As Boolean
            If wayOut.Unwritten IsNot Nothing AndAlso wayOut.Unwritten.Contains(output) AndAlso output.Equals(input) Then
                Return True
            End If

            Dim carried As Symbol = Nothing
            Return wayOut.Snapshot IsNot Nothing AndAlso wayOut.Snapshot.Carriers IsNot Nothing AndAlso wayOut.Snapshot.Carriers.TryGetValue(output, carried) AndAlso carried IsNot Nothing AndAlso carried.Equals(input)
        End Function

        Private Function ObservedProof(method As MethodSymbol) As BodyProof
            If method Is Nothing OrElse SameMethod(method, _method) Then
                Return Nothing
            End If

            Dim proof = ReadBodyProof(method)
            If proof Is Nothing OrElse Not proof.ReachesCycle Then
                Return Nothing
            End If

            _sawCycle = True
            Return proof
        End Function

        Private Function ReadBodyProof(method As MethodSymbol) As BodyProof
            If method Is Nothing OrElse method.DeclaringCompilation Is Nothing OrElse Not TypeOf method.OriginalDefinition Is SourceMethodSymbol Then
                Return Nothing
            End If

            Dim key = method.OriginalDefinition
            Dim stored = Facts.GetOrCreateValue(key.DeclaringCompilation)
            SyncLock stored
                Dim cached As BodyProof = Nothing
                If stored.BodyProofs.TryGetValue(key, cached) Then
                    Return cached
                End If

                If Not stored.BodyInProgress.Add(key) Then
                    _sawCycle = True
                    Return BodyProof.Cyclic
                End If
            End SyncLock

            Dim binding = BindingDiagnosticBag.GetInstance(withDiagnostics:=True, withDependencies:=False)
            Dim diagnostics = DiagnosticBag.GetInstance()
            Try
                Dim sourceMethod = TryCast(key, SourceMethodSymbol)
                If sourceMethod IsNot Nothing AndAlso sourceMethod.BlockSyntax IsNot Nothing Then
                    Dim body = key.GetBoundMethodBody(Nothing, binding)
                    If body IsNot Nothing Then
                        Dim walker = New NullableFlowAnalysis(key, diagnostics, _project)
                        walker._summaryOnly = True
                        walker._bodyProof = True
                        walker.Prepare(body)
                        walker.BeginPass()
                        walker.Visit(body)
                        walker.FinishPass()
                    End If
                End If
            Finally
                SyncLock stored
                    stored.BodyInProgress.Remove(key)
                    If Not stored.BodyProofs.ContainsKey(key) Then
                        stored.BodyProofs(key) = New BodyProof()
                    End If
                End SyncLock

                diagnostics.Free()
                binding.Free()
            End Try

            SyncLock stored
                Dim cached As BodyProof = Nothing
                stored.BodyProofs.TryGetValue(key, cached)
                Return cached
            End SyncLock
        End Function

        Private Sub PublishBodyProof()
            If _method Is Nothing OrElse _method.DeclaringCompilation Is Nothing Then
                Return
            End If

            Dim proof As New BodyProof With {
                .NoNormalExit = _normalExits.Count = 0,
                .ReturnNotNull = ExitsProveReturnNotNull(),
                .ReachesCycle = _sawCycle,
                .NotNullParameters = ParametersProvenNotNull()
            }
            Dim stored = Facts.GetOrCreateValue(_method.DeclaringCompilation)
            SyncLock stored
                stored.BodyProofs(_method.OriginalDefinition) = proof
            End SyncLock
        End Sub

        Private Function ExitsProveReturnNotNull() As Boolean
            Dim sawReturn = False
            For Each wayOut In _normalExits
                If Not wayOut.HasReturn Then
                    Continue For
                End If

                sawReturn = True
                If wayOut.ReturnState <> FlowNullability.NotNull Then
                    Return False
                End If
            Next

            Return sawReturn
        End Function

        Private Function ParametersProvenNotNull() As HashSet(Of Integer)
            Dim result As New HashSet(Of Integer)()
            If _method Is Nothing OrElse _normalExits.Count = 0 Then
                Return result
            End If

            For Each parameter In _method.Parameters
                If parameter.IsMe OrElse Not parameter.IsByRef OrElse Not IsTrackedReference(parameter) Then
                    Continue For
                End If

                Dim proven = True
                For Each wayOut In _normalExits
                    If SnapshotState(wayOut.Snapshot, parameter) <> FlowNullability.NotNull Then
                        proven = False
                        Exit For
                    End If
                Next

                If proven Then
                    result.Add(parameter.Ordinal)
                End If
            Next

            Return result
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
            If Not _reachable OrElse _summaryOnly OrElse syntax Is Nothing OrElse Not WarningsEnabled(syntax) Then
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
            Return New FlowSnapshot(_reachable, New Dictionary(Of Symbol, FlowNullability)(_state), New Dictionary(Of MemberSlot, FlowNullability)(_members), New HashSet(Of Symbol)(_escaped), New HashSet(Of Symbol)(_published), CopyStates(_original), CopyCarriers(_carriers))
        End Function

        Private Shared Function CloneSnapshot(snapshot As FlowSnapshot) As FlowSnapshot
            If snapshot Is Nothing Then
                Return Unreachable()
            End If

            Return New FlowSnapshot(snapshot.Reachable, New Dictionary(Of Symbol, FlowNullability)(snapshot.Slots), New Dictionary(Of MemberSlot, FlowNullability)(snapshot.Members), New HashSet(Of Symbol)(snapshot.Escaped), New HashSet(Of Symbol)(snapshot.Published), CopyStates(snapshot.Original), CopyCarriers(snapshot.Carriers))
        End Function

        Private Shared Function Unreachable() As FlowSnapshot
            Return New FlowSnapshot(False, New Dictionary(Of Symbol, FlowNullability)(), New Dictionary(Of MemberSlot, FlowNullability)(), New HashSet(Of Symbol)(), New HashSet(Of Symbol)(), New Dictionary(Of Symbol, FlowNullability)(), New Dictionary(Of Symbol, Symbol)())
        End Function

        Private Sub SetSnapshot(snapshot As FlowSnapshot)
            If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                _reachable = False
                Return
            End If

            _reachable = True
            _state = New Dictionary(Of Symbol, FlowNullability)(snapshot.Slots)
            _members = New Dictionary(Of MemberSlot, FlowNullability)(snapshot.Members)
            _escaped = New HashSet(Of Symbol)(snapshot.Escaped)
            _published = New HashSet(Of Symbol)(snapshot.Published)
            _original = CopyStates(snapshot.Original)
            _carriers = CopyCarriers(snapshot.Carriers)
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
            Dim escaped As New HashSet(Of Symbol)(left.Escaped)
            escaped.UnionWith(right.Escaped)
            Dim published As New HashSet(Of Symbol)(left.Published)
            published.UnionWith(right.Published)
            Return New FlowSnapshot(True, slots, members, escaped, published, MergeOriginal(left.Original, right.Original), MergeCarriers(left.Carriers, right.Carriers))
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

        Private Sub ApplyFinallyToExits(exitCount As Integer, clock As Integer)
            Dim after = Capture()
            For index = 0 To exitCount - 1
                Dim wayOut = _normalExits(index)
                If wayOut.Snapshot Is Nothing OrElse wayOut.Snapshot.Members Is Nothing Then
                    Continue For
                End If

                For Each pair In _memberWrites
                    Dim written As FlowNullability
                    If pair.Value > clock AndAlso after.Members.TryGetValue(pair.Key, written) Then
                        wayOut.Snapshot.Members(pair.Key) = written
                    End If
                Next

                If wayOut.Snapshot.Slots IsNot Nothing Then
                    For Each pair In _writes
                        Dim written As FlowNullability
                        If pair.Value > clock AndAlso after.Slots.TryGetValue(pair.Key, written) Then
                            wayOut.Snapshot.Slots(pair.Key) = written
                        End If
                    Next
                End If
            Next
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

            Dim original = CopyStates(snapshot.Original)
            Dim originalKeys = original.Keys.ToArray()
            For Each key In originalKeys
                original(key) = FlowNullability.MaybeNull
            Next

            Return New FlowSnapshot(snapshot.Reachable, slots, members, New HashSet(Of Symbol)(snapshot.Escaped), New HashSet(Of Symbol)(snapshot.Published), original, New Dictionary(Of Symbol, Symbol)())
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

            If left.Slots.Count <> right.Slots.Count OrElse left.Members.Count <> right.Members.Count OrElse left.Escaped.Count <> right.Escaped.Count OrElse left.Published.Count <> right.Published.Count OrElse left.Original.Count <> right.Original.Count OrElse left.Carriers.Count <> right.Carriers.Count Then
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

            For Each symbol In left.Escaped
                If Not right.Escaped.Contains(symbol) Then
                    Return False
                End If
            Next

            For Each symbol In left.Published
                If Not right.Published.Contains(symbol) Then
                    Return False
                End If
            Next

            For Each pair In left.Original
                Dim other As FlowNullability
                If Not right.Original.TryGetValue(pair.Key, other) OrElse other <> pair.Value Then
                    Return False
                End If
            Next

            For Each pair In left.Carriers
                Dim other As Symbol = Nothing
                If Not right.Carriers.TryGetValue(pair.Key, other) OrElse Not other.Equals(pair.Value) Then
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
            InvalidateEscapedMembers()

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
            Dim method = AwaitResultMethod(node)
            If method IsNot Nothing Then
                Dim annotations = method.ReturnTypeFlowAnalysisAnnotations
                If HasContract(annotations, FlowAnalysisAnnotations.NotNull) Then
                    Return FlowNullability.NotNull
                End If

                If HasContract(annotations, FlowAnalysisAnnotations.MaybeNull) Then
                    Return FlowNullability.MaybeNull
                End If

                Dim carried = CarriedAwaitAnnotation(node, method)
                If carried <> NullableAnnotation.None Then
                    Return FromAnnotation(carried)
                End If

                Return CallResult(method, dependentNotNull:=False)
            End If

            If node.Type IsNot Nothing AndAlso Not node.Type.IsReferenceType Then
                Return FlowNullability.NotNull
            End If

            Return FlowNullability.Unknown
        End Function

        Private Shared Function AwaitResultMethod(node As BoundAwaitOperator) As MethodSymbol
            Dim current = node.GetResult
            While current IsNot Nothing
                Select Case current.Kind
                    Case BoundKind.Call
                        Return DirectCast(current, BoundCall).Method
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

        ''' <summary>
        ''' GetResult may return a type parameter of the awaiter. That parameter is the same
        ''' position in the type returned by the bound GetAwaiter method, whose type argument
        ''' comes from the awaited value. An unrelated type argument is not consulted.
        ''' </summary>
        Private Shared Function CarriedAwaitAnnotation(node As BoundAwaitOperator, method As MethodSymbol) As NullableAnnotation
            Dim parameter = TryCast(method.OriginalDefinition.ReturnType, TypeParameterSymbol)
            If parameter Is Nothing OrElse parameter.ContainingType Is Nothing Then
                Return NullableAnnotation.None
            End If

            Dim getter = AwaiterMethod(node.GetAwaiter)
            Dim operand = TryCast(If(node.Operand Is Nothing, Nothing, node.Operand.Type), NamedTypeSymbol)
            Dim originalReturn = If(getter Is Nothing, Nothing, TryCast(getter.OriginalDefinition.ReturnType, NamedTypeSymbol))
            If getter Is Nothing OrElse operand Is Nothing OrElse originalReturn Is Nothing OrElse parameter.Ordinal >= originalReturn.Arity Then
                Return NullableAnnotation.None
            End If

            Dim source = TryCast(originalReturn.TypeArgumentsNoUseSiteDiagnostics(parameter.Ordinal), TypeParameterSymbol)
            Dim owner = TryCast(If(source Is Nothing, Nothing, source.ContainingSymbol), NamedTypeSymbol)
            If source Is Nothing OrElse owner Is Nothing OrElse operand.OriginalDefinition IsNot owner Then
                Return NullableAnnotation.None
            End If

            Dim annotations = operand.GetTypeArgumentNullableAnnotations()
            If source.Ordinal < 0 OrElse source.Ordinal >= annotations.Length Then
                Return NullableAnnotation.None
            End If

            Return annotations(source.Ordinal)
        End Function

        Private Shared Function AwaiterMethod(expr As BoundExpression) As MethodSymbol
            Dim current = expr
            While current IsNot Nothing
                Select Case current.Kind
                    Case BoundKind.Call
                        Return DirectCast(current, BoundCall).Method
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
                If IsConstructor(_method) Then
                    ' The setter body is not inlined, so a field initializer is no longer proof.
                    _userMayHaveMutated = True
                End If
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
            MirrorBackingField(slot, value)
            NoteMemberWrite(slot)
            If slot.Depth > 1 Then
                ' A nested reference can be shared even when the root locals are distinct.
                WeakenSamePath(slot)
            ElseIf value = FlowNullability.NotNull Then
                InvalidateAliasMembers(slot)
            Else
                CopyAliasMembers(slot, value)
            End If

            Dim stored = TrackedSymbol(syntaxExpr)
            If stored IsNot Nothing AndAlso IsReferenceSymbol(stored) Then
                _published.Add(stored)
                _escaped.Add(stored)
            End If
        End Sub

        Private Sub NoteReceiverIdentity(target As Symbol, value As BoundExpression)
            If target Is Nothing OrElse Not IsReceiverSymbol(target) Then
                Return
            End If

            ClearReceiverMembers(target)
            Unlink(target)
            If Not IsReferenceSymbol(target) Then
                Return
            End If

            If IsFreshAllocation(value) Then
                _escaped.Remove(target)
                _published.Remove(target)
                ApplyPendingContracts(target)
                Return
            End If

            Dim source = TrackedSymbol(value)
            If source IsNot Nothing AndAlso source IsNot target AndAlso IsReferenceSymbol(source) Then
                Link(target, source)
                If _escaped.Contains(source) Then
                    _escaped.Add(target)
                Else
                    _escaped.Remove(target)
                End If

                If _published.Contains(source) Then
                    _published.Add(target)
                End If

                Return
            End If

            ' A call or other reference result may be an object the caller already holds.
            _escaped.Add(target)
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
            If slot Is Nothing OrElse slot.IsShared Then
                Return
            End If

            For Each other In AliasReceivers(slot.Receiver)
                If other Is slot.Receiver Then
                    Continue For
                End If

                Dim aliased = slot.WithReceiver(other)
                RemoveDescendants(aliased)
                _members(aliased) = FlowNullability.MaybeNull
            Next
        End Sub

        Private Sub CopyAliasMembers(slot As MemberSlot, value As FlowNullability)
            If slot Is Nothing OrElse slot.IsShared Then
                Return
            End If

            For Each other In AliasReceivers(slot.Receiver)
                If other Is slot.Receiver Then
                    Continue For
                End If

                Dim aliased = slot.WithReceiver(other)
                RemoveDescendants(aliased)
                _members(aliased) = value
            Next
        End Sub

        Private Sub WeakenSamePath(slot As MemberSlot)
            Dim keys = _members.Keys.ToArray()
            For Each key In keys
                If key.SamePath(slot) AndAlso Not key.Equals(slot) Then
                    _members(key) = FlowNullability.MaybeNull
                End If
            Next
        End Sub

        Private Function AliasReceivers(receiver As Symbol) As List(Of Symbol)
            Dim result As New List(Of Symbol)()
            If receiver IsNot Nothing Then
                result.AddRange(AliasGroup(receiver))
            End If

            For Each symbol In _escaped
                If receiver Is Nothing Then
                    If MayAliasMe(symbol) Then
                        result.Add(symbol)
                    End If
                ElseIf symbol IsNot receiver AndAlso MayAlias(receiver, symbol) Then
                    result.Add(symbol)
                End If
            Next

            If receiver IsNot Nothing AndAlso MayAliasMe(receiver) Then
                result.Add(Nothing)
            End If

            Return result
        End Function

        Private Function MayAlias(left As Symbol, right As Symbol) As Boolean
            If left Is right Then
                Return True
            End If

            If left IsNot Nothing AndAlso right IsNot Nothing AndAlso AliasRoot(left) Is AliasRoot(right) AndAlso ( _aliases.ContainsKey(left) OrElse _aliases.ContainsKey(right)) Then
                Return True
            End If

            If left Is Nothing Then
                Return MayAliasMe(right)
            End If

            If right Is Nothing Then
                Return MayAliasMe(left)
            End If

            Return _escaped.Contains(left) AndAlso _escaped.Contains(right) AndAlso TypesMayAlias(SymbolType(left), SymbolType(right))
        End Function

        Private Function MayAliasMe(symbol As Symbol) As Boolean
            If symbol Is Nothing OrElse Not _escaped.Contains(symbol) Then
                Return False
            End If

            Dim container = _method.ContainingType
            If container Is Nothing OrElse Not container.IsReferenceType Then
                Return False
            End If

            Return TypesMayAlias(SymbolType(symbol), container)
        End Function

        Private Shared Function SymbolType(symbol As Symbol) As TypeSymbol
            Dim local = TryCast(symbol, LocalSymbol)
            If local IsNot Nothing Then
                Return local.Type
            End If

            Dim parameter = TryCast(symbol, ParameterSymbol)
            If parameter IsNot Nothing Then
                Return parameter.Type
            End If

            Return Nothing
        End Function

        Private Shared Function TypesMayAlias(left As TypeSymbol, right As TypeSymbol) As Boolean
            If left Is Nothing OrElse right Is Nothing OrElse Not left.IsReferenceType OrElse Not right.IsReferenceType Then
                Return False
            End If

            If left.SpecialType = SpecialType.System_Object OrElse right.SpecialType = SpecialType.System_Object OrElse left.IsInterfaceType() OrElse right.IsInterfaceType() Then
                Return True
            End If

            Return IsSameOrBase(left, right) OrElse IsSameOrBase(right, left)
        End Function

        Private Shared Function IsSameOrBase(candidate As TypeSymbol, derived As TypeSymbol) As Boolean
            Dim current = derived
            While current IsNot Nothing
                If current.Equals(candidate) Then
                    Return True
                End If

                current = current.BaseTypeNoUseSiteDiagnostics
            End While

            Return False
        End Function

        Private Shared Function IsFreshAllocation(expr As BoundExpression) As Boolean
            expr = StripFlow(expr)
            Return expr IsNot Nothing AndAlso (expr.Kind = BoundKind.ObjectCreationExpression OrElse expr.Kind = BoundKind.NewT)
        End Function

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

            For Each symbol In AliasReceivers(receiver)
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
            touched.AddRange(_published)
            Dim receiverSymbol = TrackedSymbol(receiver)
            If receiverSymbol IsNot Nothing Then
                touched.AddRange(AliasReceivers(receiverSymbol))
            End If

            If records IsNot Nothing Then
                For Each record In records
                    If record.HasSlot AndAlso IsReferenceSymbol(record.Slot) Then
                        touched.AddRange(AliasReceivers(record.Slot))
                    End If
                Next
            End If

            If method IsNot Nothing AndAlso method.MethodKind = MethodKind.DelegateInvoke Then
                touched.AddRange(_captured)
            End If

            StripMembers(whenTrue, touchOurs, touched)
            StripMembers(whenFalse, touchOurs, touched)
            If records IsNot Nothing Then
                For Each record In records
                    If record.HasSlot AndAlso IsReferenceSymbol(record.Slot) Then
                        _published.Add(record.Slot)
                        _escaped.Add(record.Slot)
                        If whenTrue IsNot Nothing AndAlso whenTrue.Reachable Then
                            whenTrue.Published.Add(record.Slot)
                            whenTrue.Escaped.Add(record.Slot)
                        End If

                        If whenFalse IsNot Nothing AndAlso whenFalse.Reachable Then
                            whenFalse.Published.Add(record.Slot)
                            whenFalse.Escaped.Add(record.Slot)
                        End If
                    End If
                Next
            End If
        End Sub

        Private Sub InvalidateEscapedMembers()
            Dim keys = _members.Keys.ToArray()
            For Each key In keys
                If key.IsShared OrElse key.Receiver Is Nothing OrElse (key.Receiver IsNot Nothing AndAlso _escaped.Contains(key.Receiver)) Then
                    _members.Remove(key)
                End If
            Next
        End Sub

        Private Shared Sub StripMembers(snapshot As FlowSnapshot, touchOurs As Boolean, touched As List(Of Symbol))
            If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                Return
            End If

            Dim keys = snapshot.Members.Keys.ToArray()
            For Each key In keys
                Dim drop = key.IsShared OrElse (touchOurs AndAlso key.Receiver Is Nothing) OrElse Touches(key.Receiver, touched)
                If drop Then
                    snapshot.Members.Remove(key)
                End If
            Next
        End Sub

        Private Shared Function Touches(receiver As Symbol, touched As List(Of Symbol)) As Boolean
            If receiver Is Nothing Then
                Return touched.Contains(Nothing)
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

        Private Sub Prepare(body As BoundBlock)
            _body = body
            _initializerPass = IsInitializerOnly(body)
            If IsConstructor(_method) AndAlso Not _initializerPass AndAlso Not _bodyProof AndAlso _method.DeclaringCompilation IsNot Nothing Then
                Dim stored = Facts.GetOrCreateValue(_method.DeclaringCompilation)
                SyncLock stored
                    stored.InProgress.Add(_method)
                End SyncLock
            End If
        End Sub

        Private Function SkipBecauseInitializersAreCached() As Boolean
            Return _initializerPass AndAlso LookupInitializerCache() IsNot Nothing
        End Function

        Private Sub FinishPass()
            If _initializerPass Then
                If Not _summaryOnly Then
                    PublishInitializerCache()
                End If

                Return
            End If

            If _reachable Then
                Dim hasReturn = _method IsNot Nothing AndAlso _method.ReturnType IsNot Nothing AndAlso _method.ReturnType.IsReferenceType AndAlso _method.ReturnType.SpecialType <> SpecialType.System_Void
                NoteNormalExit(If(_body Is Nothing, Nothing, _body.Syntax), ExitSense.Any, hasReturn, If(hasReturn, FunctionReturnState(Capture()), FlowNullability.NotNull))
            End If

            If _summaryOnly Then
                PublishBodyProof()
            End If

            If IsConstructor(_method) AndAlso Not _bodyProof Then
                CheckConstructorExits()
                PublishSummary()
            End If

            CheckMemberContracts()
            CheckValuePostconditions()
            ValidateMemberNames()
            ValidateParameterContractNames()
        End Sub

        Private Sub NoteNormalExit(syntax As SyntaxNode, sense As ExitSense, Optional hasReturn As Boolean = False, Optional returnState As FlowNullability = FlowNullability.Unknown)
            If Not _reachable Then
                Return
            End If

            Dim preserved = If(_hasReturnPreserved, If(_returnPreserved, New HashSet(Of Symbol)()), PreservedFunctionValue())
            _normalExits.Add(New NormalExit With {
                .Snapshot = Capture(),
                .Syntax = syntax,
                .Sense = sense,
                .HasReturn = hasReturn,
                .ReturnState = returnState,
                .Preserved = preserved,
                .Unwritten = UnwrittenParameters()
            })
        End Sub

        Private Function ReturnSense(expr As BoundExpression) As ExitSense
            If _method.ReturnType Is Nothing OrElse _method.ReturnType.SpecialType <> SpecialType.System_Boolean Then
                Return ExitSense.Any
            End If

            If expr IsNot Nothing AndAlso expr.ConstantValueOpt IsNot Nothing AndAlso expr.ConstantValueOpt.IsBoolean Then
                Return If(expr.ConstantValueOpt.BooleanValue, ExitSense.WhenTrue, ExitSense.WhenFalse)
            End If

            Return ExitSense.UnknownBoolean
        End Function

        Private Shared Function IsMethodExit(node As BoundExitStatement) As Boolean
            If node.Syntax Is Nothing Then
                Return False
            End If

            Select Case node.Syntax.Kind
                Case SyntaxKind.ExitSubStatement, SyntaxKind.ExitFunctionStatement, SyntaxKind.ExitPropertyStatement
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Shared Function IsConstructor(method As MethodSymbol) As Boolean
            Return method IsNot Nothing AndAlso (method.MethodKind = MethodKind.Constructor OrElse method.MethodKind = MethodKind.SharedConstructor)
        End Function

        Private Shared Function IsInitializerOnly(body As BoundBlock) As Boolean
            If body Is Nothing OrElse body.Statements.IsDefaultOrEmpty Then
                Return False
            End If

            For Each statement In body.Statements
                If statement.Kind <> BoundKind.FieldInitializer AndAlso statement.Kind <> BoundKind.PropertyInitializer AndAlso statement.Kind <> BoundKind.GlobalStatementInitializer Then
                    Return False
                End If
            Next

            Return True
        End Function

        Private Function FirstConstructorCallTarget() As MethodSymbol
            If _body Is Nothing OrElse _body.Statements.IsDefaultOrEmpty Then
                Return Nothing
            End If

            Dim statement = TryCast(_body.Statements(0), BoundExpressionStatement)
            If statement Is Nothing Then
                Return Nothing
            End If

            Dim called = TryCast(statement.Expression, BoundCall)
            If called Is Nothing OrElse called.Method Is Nothing OrElse called.Method.MethodKind <> MethodKind.Constructor Then
                Return Nothing
            End If

            Return called.Method
        End Function

        Private Function IsSameTypeConstructor(method As MethodSymbol) As Boolean
            Return method IsNot Nothing AndAlso
                method.MethodKind = MethodKind.Constructor AndAlso
                Not method.IsShared AndAlso
                Not _method.IsShared AndAlso
                method.ContainingType IsNot Nothing AndAlso
                _method.ContainingType IsNot Nothing AndAlso
                method.ContainingType.OriginalDefinition.Equals(_method.ContainingType.OriginalDefinition)
        End Function

        Private Sub SeedConstructorMembers()
            For Each member In _method.ContainingType.GetMembers()
                If IsObligation(member) Then
                    _members(SlotFor(member)) = FlowNullability.MaybeNull
                End If
            Next
        End Sub

        Private Function IsObligation(member As Symbol) As Boolean
            Dim field = TryCast(member, FieldSymbol)
            Dim [property] = TryCast(member, PropertySymbol)
            Dim memberType As TypeSymbol
            Dim annotation As NullableAnnotation
            Dim isShared As Boolean
            If field IsNot Nothing Then
                If field.IsConst OrElse IsBackingField(field) Then
                    Return False
                End If

                memberType = field.Type
                annotation = field.GetNullableAnnotation()
                isShared = field.IsShared
            ElseIf [property] IsNot Nothing AndAlso IsAutoLike([property]) Then
                memberType = [property].Type
                annotation = [property].GetNullableAnnotation()
                isShared = [property].IsShared
            Else
                Return False
            End If

            Return isShared = _method.IsShared AndAlso annotation = NullableAnnotation.NotAnnotated AndAlso memberType IsNot Nothing AndAlso memberType.IsReferenceType
        End Function

        Private Shared Function IsBackingField(field As FieldSymbol) As Boolean
            Return TypeOf field.AssociatedSymbol Is PropertySymbol
        End Function

        Private Shared Function IsAutoLike([property] As PropertySymbol) As Boolean
            If [property] Is Nothing OrElse [property].ParameterCount <> 0 Then
                Return False
            End If

            Dim source = TryCast([property].OriginalDefinition, SourcePropertySymbol)
            If source IsNot Nothing Then
                Return source.IsAutoProperty
            End If

            Return TypeOf [property].OriginalDefinition Is SynthesizedRecordPositionalPropertySymbol
        End Function

        Private Function SlotFor(member As Symbol) As MemberSlot
            member = Canonical(member)
            Dim isShared = member.IsShared
            Return New MemberSlot(Nothing, isShared, {member})
        End Function

        Private Function Canonical(member As Symbol) As Symbol
            If member Is Nothing OrElse _method Is Nothing OrElse _method.ContainingType Is Nothing Then
                Return member
            End If

            If member.ContainingType IsNot Nothing AndAlso member.ContainingType.OriginalDefinition.Equals(_method.ContainingType.OriginalDefinition) Then
                For Each candidate In _method.ContainingType.GetMembers(member.Name)
                    If candidate.Kind = member.Kind Then
                        Return candidate
                    End If
                Next
            End If

            Return member
        End Function

        Private Function SlotForReceiver(member As Symbol, receiver As Symbol) As MemberSlot
            If member.IsShared Then
                Return New MemberSlot(Nothing, True, {member})
            End If

            Return New MemberSlot(receiver, False, {member})
        End Function

        Private Sub MirrorBackingField(slot As MemberSlot, value As FlowNullability)
            If slot Is Nothing OrElse slot.Depth <> 1 Then
                Return
            End If

            Dim field = TryCast(slot.LeafSymbol, FieldSymbol)
            Dim [property] = TryCast(If(field Is Nothing, Nothing, field.AssociatedSymbol), PropertySymbol)
            If [property] Is Nothing Then
                Return
            End If

            _members(New MemberSlot(slot.Receiver, slot.IsShared, {[property]})) = value
        End Sub

        Private Sub RestoreProvenInitializers(Optional snapshot As FlowSnapshot = Nothing)
            Dim cached = LookupInitializerCache()
            If cached Is Nothing Then
                Return
            End If

            For Each pair In cached
                If pair.Value <> FlowNullability.NotNull OrElse WasWrittenAfterInitializer(pair.Key) Then
                    Continue For
                End If

                If snapshot Is Nothing Then
                    _members(pair.Key) = FlowNullability.NotNull
                Else
                    snapshot.Members(pair.Key) = FlowNullability.NotNull
                End If
            Next
        End Sub

        Private Shared Function SyntaxInitializerIsNotNull(member As Symbol) As Boolean
            Dim field = TryCast(member, SourceFieldSymbol)
            Dim syntax As VisualBasicSyntaxNode = If(field Is Nothing, Nothing, field.EqualsValueOrAsNewInitOpt)
            If syntax Is Nothing Then
                Return False
            End If

            If syntax.Kind = SyntaxKind.AsNewClause Then
                Return True
            End If

            Dim equalsValue = TryCast(syntax, EqualsValueSyntax)
            Dim value = If(equalsValue Is Nothing, Nothing, equalsValue.Value)
            If value Is Nothing Then
                Return False
            End If

            If value.Kind = SyntaxKind.NothingLiteralExpression Then
                Return False
            End If

            Return value.Kind = SyntaxKind.StringLiteralExpression OrElse
                value.Kind = SyntaxKind.CharacterLiteralExpression OrElse
                value.Kind = SyntaxKind.NumericLiteralExpression OrElse
                value.Kind = SyntaxKind.TrueLiteralExpression OrElse
                value.Kind = SyntaxKind.FalseLiteralExpression
        End Function

        Private Function WasWrittenAfterInitializer(slot As MemberSlot) As Boolean
            Dim stamp = 0
            Return _memberWrites.TryGetValue(slot, stamp) AndAlso stamp > _initializerClock
        End Function

        Private Sub ApplyInitializers(publish As Boolean)
            Dim cached = LookupInitializerCache()
            If cached IsNot Nothing Then
                For Each pair In cached
                    _members(pair.Key) = pair.Value
                Next

                Return
            End If

            For Each initializer In BindInitializers()
                Visit(initializer)
            Next

            If publish Then
                PublishInitializerCache()
            End If
        End Sub

        Private Function BindInitializers() As ImmutableArray(Of BoundInitializer)
            Dim source = TryCast(_method.ContainingType, SourceMemberContainerTypeSymbol)
            If source Is Nothing OrElse source.IsScriptClass Then
                Return ImmutableArray(Of BoundInitializer).Empty
            End If

            Dim stored = Facts.GetOrCreateValue(_method.DeclaringCompilation)
            Dim key = InitializerKey()
            Dim cached As ImmutableArray(Of BoundInitializer) = Nothing
            SyncLock stored
                If stored.InitializersBound.TryGetValue(key, cached) Then
                    Return cached
                End If
            End SyncLock

            Dim syntax = If(_method.IsShared, source.StaticInitializers, source.InstanceInitializers)
            If syntax.IsDefaultOrEmpty Then
                cached = ImmutableArray(Of BoundInitializer).Empty
            Else
                Dim bag = BindingDiagnosticBag.GetInstance(withDiagnostics:=True, withDependencies:=False)
                Try
                    cached = Binder.BindFieldAndPropertyInitializers(source, syntax, Nothing, bag)
                Finally
                    bag.Free()
                End Try
            End If

            SyncLock stored
                stored.InitializersBound(key) = cached
            End SyncLock

            Return cached
        End Function

        Private Function InitializerKey() As Symbol
            Return _method.ContainingType
        End Function

        Private Function LookupInitializerCache() As Dictionary(Of MemberSlot, FlowNullability)
            If _method.DeclaringCompilation Is Nothing OrElse _method.ContainingType Is Nothing Then
                Return Nothing
            End If

            Dim stored = Facts.GetOrCreateValue(_method.DeclaringCompilation)
            Dim found As Dictionary(Of MemberSlot, FlowNullability) = Nothing
            Dim key = If(_method.IsShared, DirectCast(_method, Symbol), _method.ContainingType)
            SyncLock stored
                stored.Initializers.TryGetValue(key, found)
            End SyncLock

            Return found
        End Function

        Private Sub PublishInitializerCache()
            If _method.DeclaringCompilation Is Nothing Then
                Return
            End If

            Dim copy As New Dictionary(Of MemberSlot, FlowNullability)()
            For Each pair In _members
                If pair.Key.Receiver Is Nothing Then
                    copy(pair.Key) = pair.Value
                End If
            Next

            Dim stored = Facts.GetOrCreateValue(_method.DeclaringCompilation)
            SyncLock stored
                stored.Initializers(If(_method.IsShared, DirectCast(_method, Symbol), _method.ContainingType)) = copy
            End SyncLock
        End Sub

        Private Sub PublishSummary()
            If _method.DeclaringCompilation Is Nothing Then
                Return
            End If

            Dim stored = Facts.GetOrCreateValue(_method.DeclaringCompilation)
            Dim established As HashSet(Of Symbol) = Nothing
            Dim completes = _normalExits.Count > 0
            If completes Then
                Dim first = True
                For Each wayOut In _normalExits
                    Dim here As New HashSet(Of Symbol)()
                    For Each pair In wayOut.Snapshot.Members
                        If pair.Value = FlowNullability.NotNull AndAlso pair.Key.Receiver Is Nothing AndAlso pair.Key.Depth = 1 Then
                            here.Add(pair.Key.LeafSymbol)
                        End If
                    Next

                    If first Then
                        established = here
                        first = False
                    Else
                        established.IntersectWith(here)
                    End If
                Next
            End If

            If established Is Nothing Then
                established = New HashSet(Of Symbol)()
            End If

            SyncLock stored
                stored.Established(_method) = established
                stored.Completes(_method) = completes
                stored.InProgress.Remove(_method)
            End SyncLock
        End Sub

        Private Function ReadSummary(method As MethodSymbol) As HashSet(Of Symbol)
            Dim completes As Boolean
            Return ReadSummary(method, completes)
        End Function

        Private Function ReadSummary(method As MethodSymbol, ByRef completes As Boolean) As HashSet(Of Symbol)
            completes = True
            If method Is Nothing OrElse method.DeclaringCompilation Is Nothing Then
                Return New HashSet(Of Symbol)()
            End If

            Dim stored = Facts.GetOrCreateValue(method.DeclaringCompilation)
            Dim established As HashSet(Of Symbol) = Nothing
            SyncLock stored
                If stored.Established.TryGetValue(method, established) Then
                    stored.Completes.TryGetValue(method, completes)
                    Return established
                End If

                If Not stored.InProgress.Add(method) Then
                    Return New HashSet(Of Symbol)()
                End If
            End SyncLock

            Dim binding = BindingDiagnosticBag.GetInstance(withDiagnostics:=True, withDependencies:=False)
            Dim diagnostics = DiagnosticBag.GetInstance()
            Try
                Dim body = method.GetBoundMethodBody(Nothing, binding)
                If body IsNot Nothing Then
                    Dim walker = New NullableFlowAnalysis(method, diagnostics, _project)
                    walker._summaryOnly = True
                    walker.Prepare(body)
                    walker.BeginPass()
                    walker.Visit(body)
                    walker.FinishPass()
                End If

                SyncLock stored
                    If stored.Established.TryGetValue(method, established) Then
                        stored.Completes.TryGetValue(method, completes)
                        Return established
                    End If
                End SyncLock
            Finally
                SyncLock stored
                    If Not stored.Established.ContainsKey(method) Then
                        stored.InProgress.Remove(method)
                        stored.Established(method) = New HashSet(Of Symbol)()
                        stored.Completes(method) = True
                    End If
                End SyncLock

                diagnostics.Free()
                binding.Free()
            End Try

            Return New HashSet(Of Symbol)()
        End Function

        Private Shared Function HasMemberContract(method As MethodSymbol) As Boolean
            If method Is Nothing Then
                Return False
            End If

            Return Not method.NotNullMembers.IsDefaultOrEmpty OrElse
                Not method.NotNullWhenTrueMembers.IsDefaultOrEmpty OrElse
                Not method.NotNullWhenFalseMembers.IsDefaultOrEmpty
        End Function

        Private Function ContractCycle(caller As MethodSymbol, callee As MethodSymbol) As Boolean
            If Not HasMemberContract(caller) OrElse Not HasMemberContract(callee) Then
                Return False
            End If

            Dim seen As New HashSet(Of MethodSymbol)()
            Dim pending As New Stack(Of MethodSymbol)()
            pending.Push(callee)
            While pending.Count > 0
                Dim current = pending.Pop()
                If Not seen.Add(current) Then
                    Continue While
                End If

                If current.Equals(caller) Then
                    Return True
                End If

                For Each nextMethod In MemberContractCallees(current)
                    pending.Push(nextMethod)
                Next
            End While

            Return False
        End Function

        Private Function MemberContractCallees(method As MethodSymbol) As ImmutableArray(Of MethodSymbol)
            If method Is Nothing OrElse method.DeclaringCompilation Is Nothing OrElse Not TypeOf method Is SourceMethodSymbol Then
                Return ImmutableArray(Of MethodSymbol).Empty
            End If

            Dim stored = Facts.GetOrCreateValue(method.DeclaringCompilation)
            Dim cached As ImmutableArray(Of MethodSymbol) = Nothing
            SyncLock stored
                If stored.ContractCallees.TryGetValue(method, cached) Then
                    Return cached
                End If
            End SyncLock

            cached = ImmutableArray(Of MethodSymbol).Empty
            Dim binding = BindingDiagnosticBag.GetInstance(withDiagnostics:=True, withDependencies:=False)
            Try
                Dim body = method.GetBoundMethodBody(Nothing, binding)
                If body IsNot Nothing Then
                    Dim walker As New ContractCalleeWalker()
                    walker.Visit(body)
                    cached = walker.Callees.Distinct().ToImmutableArray()
                End If
            Finally
                binding.Free()
            End Try

            SyncLock stored
                stored.ContractCallees(method) = cached
            End SyncLock

            Return cached
        End Function

        Private Sub NoteByRefMember(argument As BoundExpression)
            Dim slot = TryBuildSlot(argument)
            If slot Is Nothing Then
                Return
            End If

            ' A ByRef argument can clear the member without an assignment in this method.
            AssignMember(slot, FlowNullability.MaybeNull, argument, report:=False)
            _userMayHaveMutated = True
        End Sub

        Private Sub NoteCalleeEffects(method As MethodSymbol, receiver As BoundExpression, ByRef whenTrue As FlowSnapshot, ByRef whenFalse As FlowSnapshot, compilerGenerated As Boolean)
            If method Is Nothing OrElse whenTrue Is Nothing OrElse whenFalse Is Nothing Then
                Return
            End If

            If IsConstructor(_method) AndAlso Not compilerGenerated AndAlso Not IsConstructor(method) Then
                ' A user call can mutate members indirectly. The base or Me constructor call
                ' is the prologue and does not by itself undo an initializer.
                _userMayHaveMutated = True
            End If

            If _method.IsShared AndAlso IsConstructor(_method) Then
                SetSnapshot(MergeSnapshots(whenTrue, whenFalse))
                If Not _userMayHaveMutated Then
                    RestoreProvenInitializers()
                End If

                OverlayOurMembers(whenTrue)
                OverlayOurMembers(whenFalse)
            End If

            If _applyInitializersAfterBase AndAlso IsConstructor(method) AndAlso Not IsSameTypeConstructor(method) Then
                SetSnapshot(MergeSnapshots(whenTrue, whenFalse))
                ApplyInitializers(publish:=Not _summaryOnly)
                OverlayOurMembers(whenTrue)
                OverlayOurMembers(whenFalse)
                _initializerClock = _writeClock
                _initializersApplied = True
                _applyInitializersAfterBase = False
            End If

            If IsConstructor(_method) AndAlso IsSameTypeConstructor(method) AndAlso ReceiverIsInstance(receiver) Then
                Dim completes As Boolean
                Dim established = ReadSummary(method, completes)
                If Not completes Then
                    whenTrue = Unreachable()
                    whenFalse = Unreachable()
                    Return
                End If

                For Each member In established
                    SetMember(whenTrue, SlotFor(member), FlowNullability.NotNull)
                    SetMember(whenFalse, SlotFor(member), FlowNullability.NotNull)
                Next
            End If

            If SameMethod(method, _method) OrElse Not whenTrue.Reachable OrElse ContractCycle(_method, method) Then
                Return
            End If

            ApplyUnconditionalMembers(method, receiver, whenTrue)
            ApplyUnconditionalMembers(method, receiver, whenFalse)
            ApplyConditionalMembers(method, receiver, whenTrue, sense:=True)
            ApplyConditionalMembers(method, receiver, whenFalse, sense:=False)
        End Sub

        Private Sub ApplyUnconditionalMembers(method As MethodSymbol, receiver As BoundExpression)
            ApplyUnconditionalMembers(method, receiver, snapshot:=Nothing)
        End Sub

        Private Sub ApplyUnconditionalMembers(method As MethodSymbol, receiver As BoundExpression, snapshot As FlowSnapshot)
            For Each member In ResolvedMembers(method, method.NotNullMembers)
                Dim slot = ContractSlot(member, receiver)
                If slot Is Nothing Then
                    Continue For
                End If

                If snapshot Is Nothing Then
                    _members(slot) = FlowNullability.NotNull
                Else
                    SetMember(snapshot, slot, FlowNullability.NotNull)
                End If
            Next
        End Sub

        Private Sub ApplyConditionalMembers(method As MethodSymbol, receiver As BoundExpression, snapshot As FlowSnapshot, sense As Boolean)
            If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                Return
            End If

            Dim names = If(sense, method.NotNullWhenTrueMembers, method.NotNullWhenFalseMembers)
            For Each member In ResolvedMembers(method, names)
                Dim slot = ContractSlot(member, receiver)
                If slot IsNot Nothing Then
                    SetMember(snapshot, slot, FlowNullability.NotNull)
                End If
            Next
        End Sub

        Private Sub ApplyPendingContracts(target As Symbol)
            If _pendingContracts Is Nothing OrElse target Is Nothing Then
                Return
            End If

            For Each member In _pendingContracts
                If Not member.IsShared Then
                    _members(SlotForReceiver(member, target)) = FlowNullability.NotNull
                End If
            Next

            _pendingContracts = Nothing
        End Sub

        Private Function ContractSlot(member As Symbol, receiver As BoundExpression) As MemberSlot
            If member Is Nothing Then
                Return Nothing
            End If

            If member.IsShared Then
                Return SlotFor(member)
            End If

            If ReceiverIsInstance(receiver) Then
                Return SlotFor(member)
            End If

            Dim symbol = TrackedSymbol(receiver)
            If symbol Is Nothing OrElse Not IsReceiverSymbol(symbol) Then
                Return Nothing
            End If

            Return SlotForReceiver(member, symbol)
        End Function

        Private Shared Sub SetMember(snapshot As FlowSnapshot, slot As MemberSlot, value As FlowNullability)
            If snapshot IsNot Nothing AndAlso snapshot.Reachable AndAlso slot IsNot Nothing Then
                snapshot.Members(slot) = value
            End If
        End Sub

        Private Sub OverlayOurMembers(snapshot As FlowSnapshot)
            If snapshot Is Nothing OrElse Not snapshot.Reachable Then
                Return
            End If

            For Each pair In _members
                If pair.Key.Receiver Is Nothing Then
                    snapshot.Members(pair.Key) = pair.Value
                End If
            Next
        End Sub

        Private Sub CheckConstructorExits()
            If _summaryOnly Then
                Return
            End If

            For Each wayOut In _normalExits
                If _method.IsShared AndAlso Not _userMayHaveMutated Then
                    RestoreProvenInitializers(wayOut.Snapshot)
                End If

                For Each member In _method.ContainingType.GetMembers()
                    If Not IsObligation(member) Then
                        Continue For
                    End If

                    If MemberState(wayOut.Snapshot, SlotFor(member)) = FlowNullability.NotNull Then
                        Continue For
                    End If

                    If Not _userMayHaveMutated AndAlso Not WasWrittenAfterInitializer(SlotFor(member)) AndAlso SyntaxInitializerIsNotNull(member) Then
                        Continue For
                    End If

                    Dim syntax = If(_method.IsImplicitlyDeclared, MemberSyntax(member), wayOut.Syntax)
                    Report(ERRID.WRN_UninitializedNonNullableMember, syntax, member.Name)
                Next
            Next
        End Sub

        Private Sub CheckValuePostconditions()
            If _summaryOnly OrElse _initializerPass OrElse _method Is Nothing Then
                Return
            End If

            If HasContract(_method.FlowAnalysisAnnotations, FlowAnalysisAnnotations.DoesNotReturn) Then
                For Each wayOut In _normalExits
                    Report(ERRID.WRN_ShouldNotReturn, If(wayOut.Syntax, If(_body Is Nothing, Nothing, _body.Syntax)))
                Next
            End If

            For Each wayOut In _normalExits
                For Each parameter In _method.Parameters
                    If parameter.IsMe OrElse Not IsTrackedReference(parameter) Then
                        Continue For
                    End If

                    Dim state = SnapshotState(wayOut.Snapshot, parameter)
                    If HasContract(parameter.FlowAnalysisAnnotations, FlowAnalysisAnnotations.NotNull) AndAlso state <> FlowNullability.NotNull Then
                        Report(ERRID.WRN_ParameterDisallowsNull, If(wayOut.Syntax, _body.Syntax), parameter.Name)
                    End If

                    If parameter.IsByRef Then
                        ReportConditionalParameter(parameter, wayOut, state)
                    End If

                    ReportParameterRelation(parameter, wayOut, state)
                Next

                ReportReturnRelation(wayOut)
            Next
        End Sub

        Private Sub ReportConditionalParameter(parameter As ParameterSymbol, wayOut As NormalExit, state As FlowNullability)
            Dim annotations = parameter.FlowAnalysisAnnotations
            Dim trueRequired = wayOut.Sense = ExitSense.WhenTrue OrElse wayOut.Sense = ExitSense.UnknownBoolean
            Dim falseRequired = wayOut.Sense = ExitSense.WhenFalse OrElse wayOut.Sense = ExitSense.UnknownBoolean
            Dim declaredNotNull = AnnotationOf(parameter) = NullableAnnotation.NotAnnotated
            If trueRequired AndAlso HasContract(annotations, FlowAnalysisAnnotations.NotNullWhenTrue) AndAlso state <> FlowNullability.NotNull Then
                Report(ERRID.WRN_ParameterConditionallyDisallowsNull, If(wayOut.Syntax, _body.Syntax), parameter.Name, "True")
            End If

            If falseRequired AndAlso HasContract(annotations, FlowAnalysisAnnotations.NotNullWhenFalse) AndAlso state <> FlowNullability.NotNull Then
                Report(ERRID.WRN_ParameterConditionallyDisallowsNull, If(wayOut.Syntax, _body.Syntax), parameter.Name, "False")
            End If

            If trueRequired AndAlso HasContract(annotations, FlowAnalysisAnnotations.MaybeNullWhenFalse) AndAlso declaredNotNull AndAlso state <> FlowNullability.NotNull Then
                Report(ERRID.WRN_ParameterConditionallyDisallowsNull, If(wayOut.Syntax, _body.Syntax), parameter.Name, "True")
            End If

            If falseRequired AndAlso HasContract(annotations, FlowAnalysisAnnotations.MaybeNullWhenTrue) AndAlso declaredNotNull AndAlso state <> FlowNullability.NotNull Then
                Report(ERRID.WRN_ParameterConditionallyDisallowsNull, If(wayOut.Syntax, _body.Syntax), parameter.Name, "False")
            End If
        End Sub

        Private Sub ReportParameterRelation(parameter As ParameterSymbol, wayOut As NormalExit, state As FlowNullability)
            If parameter.NotNullIfParameterNotNull.IsEmpty OrElse state = FlowNullability.NotNull Then
                Return
            End If

            For Each input In _method.Parameters
                If input.IsMe OrElse Not NamesMatch(parameter.NotNullIfParameterNotNull, input.Name) Then
                    Continue For
                End If

                If Not OriginalObligates(wayOut, input) OrElse OutputPreserves(wayOut, parameter, input) Then
                    Continue For
                End If

                Report(ERRID.WRN_ParameterNotNullIfNotNull, If(wayOut.Syntax, _body.Syntax), parameter.Name, input.Name)
            Next
        End Sub

        Private Sub ReportReturnRelation(wayOut As NormalExit)
            If Not wayOut.HasReturn OrElse wayOut.ReturnState = FlowNullability.NotNull OrElse _method.ReturnNotNullIfParameterNotNull.IsEmpty Then
                Return
            End If

            For Each input In _method.Parameters
                If input.IsMe OrElse Not NamesMatch(_method.ReturnNotNullIfParameterNotNull, input.Name) Then
                    Continue For
                End If

                If Not OriginalObligates(wayOut, input) OrElse (wayOut.Preserved IsNot Nothing AndAlso wayOut.Preserved.Contains(input)) Then
                    Continue For
                End If

                Report(ERRID.WRN_ReturnNotNullIfNotNull, If(wayOut.Syntax, _body.Syntax), input.Name)
            Next
        End Sub

        Private Sub ValidateParameterContractNames()
            If _summaryOnly OrElse _initializerPass OrElse _method Is Nothing OrElse _method.IsImplicitlyDeclared Then
                Return
            End If

            Dim names = _method.ReturnNotNullIfParameterNotNull
            If Not names.IsEmpty Then
                ReportUnknownParameterNames(names, If(_body Is Nothing, Nothing, _body.Syntax))
            End If

            For Each parameter In _method.Parameters
                If Not parameter.NotNullIfParameterNotNull.IsEmpty Then
                    ReportUnknownParameterNames(parameter.NotNullIfParameterNotNull, If(_body Is Nothing, Nothing, _body.Syntax))
                End If
            Next
        End Sub

        Private Sub ReportUnknownParameterNames(names As ImmutableHashSet(Of String), syntax As SyntaxNode)
            For Each name In names
                Dim found = False
                For Each parameter In _method.Parameters
                    If Not parameter.IsMe AndAlso String.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase) Then
                        found = True
                        Exit For
                    End If
                Next

                If Not found Then
                    Report(ERRID.WRN_BadNullableParameterName, syntax, name)
                End If
            Next
        End Sub

        Private Shared Function NamesMatch(names As ImmutableHashSet(Of String), parameterName As String) As Boolean
            If names.IsEmpty OrElse parameterName Is Nothing Then
                Return False
            End If

            For Each name In names
                If String.Equals(name, parameterName, StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
            Next

            Return False
        End Function

        Private Shared Function SnapshotState(snapshot As FlowSnapshot, symbol As Symbol) As FlowNullability
            Dim state As FlowNullability
            If snapshot IsNot Nothing AndAlso snapshot.Slots IsNot Nothing AndAlso snapshot.Slots.TryGetValue(symbol, state) Then
                Return state
            End If

            Return FlowNullability.MaybeNull
        End Function

        Private Function FunctionReturnState(snapshot As FlowSnapshot) As FlowNullability
            If snapshot IsNot Nothing AndAlso snapshot.Slots IsNot Nothing Then
                For Each pair In snapshot.Slots
                    Dim local = TryCast(pair.Key, LocalSymbol)
                    If local IsNot Nothing AndAlso local.IsFunctionValue Then
                        Return pair.Value
                    End If
                Next
            End If

            Return FlowNullability.MaybeNull
        End Function

        Private Shared Function HasConditionalOutput(annotations As FlowAnalysisAnnotations) As Boolean
            Return HasContract(annotations, FlowAnalysisAnnotations.NotNullWhenTrue) OrElse
                HasContract(annotations, FlowAnalysisAnnotations.NotNullWhenFalse) OrElse
                HasContract(annotations, FlowAnalysisAnnotations.MaybeNullWhenTrue) OrElse
                HasContract(annotations, FlowAnalysisAnnotations.MaybeNullWhenFalse)
        End Function

        Private Sub CheckMemberContracts()
            If _summaryOnly OrElse _initializerPass Then
                Return
            End If

            For Each wayOut In _normalExits
                For Each member In ResolvedMembers(_method, _method.NotNullMembers)
                    If MemberState(wayOut.Snapshot, SlotFor(member)) <> FlowNullability.NotNull Then
                        Report(ERRID.WRN_MemberContractNotSatisfied, If(wayOut.Syntax, MemberSyntax(_method)), member.Name)
                    End If
                Next

                Dim trueRequired = wayOut.Sense = ExitSense.WhenTrue OrElse wayOut.Sense = ExitSense.UnknownBoolean
                Dim falseRequired = wayOut.Sense = ExitSense.WhenFalse OrElse wayOut.Sense = ExitSense.UnknownBoolean
                If trueRequired Then
                    For Each member In ResolvedMembers(_method, _method.NotNullWhenTrueMembers)
                        If MemberState(wayOut.Snapshot, SlotFor(member)) <> FlowNullability.NotNull Then
                            Report(ERRID.WRN_MemberContractNotSatisfiedWhen, If(wayOut.Syntax, MemberSyntax(_method)), member.Name, "True")
                        End If
                    Next
                End If

                If falseRequired Then
                    For Each member In ResolvedMembers(_method, _method.NotNullWhenFalseMembers)
                        If MemberState(wayOut.Snapshot, SlotFor(member)) <> FlowNullability.NotNull Then
                            Report(ERRID.WRN_MemberContractNotSatisfiedWhen, If(wayOut.Syntax, MemberSyntax(_method)), member.Name, "False")
                        End If
                    Next
                End If
            Next
        End Sub

        Private Shared Function MemberState(snapshot As FlowSnapshot, slot As MemberSlot) As FlowNullability
            Dim state As FlowNullability
            If snapshot IsNot Nothing AndAlso snapshot.Members IsNot Nothing AndAlso snapshot.Members.TryGetValue(slot, state) Then
                Return state
            End If

            Return FlowNullability.MaybeNull
        End Function

        Private Sub ValidateMemberNames()
            If _summaryOnly OrElse _initializerPass OrElse _method.DeclaringCompilation Is Nothing OrElse _method.IsImplicitlyDeclared Then
                Return
            End If

            Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each attribute In ContractAttributes(_method)
                If Not IsMemberContractAttribute(attribute) Then
                    Continue For
                End If

                Dim names = AttributeMemberNames(attribute)
                If names.Count = 0 Then
                    Report(ERRID.WRN_BadNullableMemberName, AttributeSyntax(attribute), "")
                    Continue For
                End If

                For Each name In names
                    If String.IsNullOrEmpty(name) Then
                        Report(ERRID.WRN_BadNullableMemberName, AttributeSyntax(attribute), "")
                        Continue For
                    End If

                    If Not seen.Add(name) Then
                        Report(ERRID.WRN_BadNullableMemberName, AttributeSyntax(attribute), name)
                        Continue For
                    End If

                    If ResolveMemberName(_method, name) Is Nothing Then
                        Report(ERRID.WRN_BadNullableMemberName, AttributeSyntax(attribute), name)
                    End If
                Next
            Next
        End Sub

        Private Shared Function ContractAttributes(method As MethodSymbol) As List(Of VisualBasicAttributeData)
            Dim result As New List(Of VisualBasicAttributeData)()
            result.AddRange(method.GetAttributes())
            Dim [property] = TryCast(method.AssociatedSymbol, PropertySymbol)
            If [property] IsNot Nothing Then
                result.AddRange([property].GetAttributes())
            End If

            Return result
        End Function

        Private Shared Function IsMemberContractAttribute(attribute As VisualBasicAttributeData) As Boolean
            Dim name = If(attribute Is Nothing OrElse attribute.AttributeClass Is Nothing, Nothing, attribute.AttributeClass.Name)
            Return name = "MemberNotNullAttribute" OrElse name = "MemberNotNullWhenAttribute"
        End Function

        Private Shared Function AttributeMemberNames(attribute As VisualBasicAttributeData) As List(Of String)
            Dim names As New List(Of String)()
            If attribute Is Nothing OrElse attribute.CommonConstructorArguments.IsDefaultOrEmpty Then
                Return names
            End If

            Dim start = 0
            If attribute.AttributeClass IsNot Nothing AndAlso attribute.AttributeClass.Name.StartsWith("MemberNotNullWhen", StringComparison.Ordinal) Then
                start = 1
            End If

            If start >= attribute.CommonConstructorArguments.Length Then
                Return names
            End If

            Dim payload = attribute.CommonConstructorArguments(start)
            CollectNames(payload, names)
            Return names
        End Function

        Private Shared Sub CollectNames(argument As TypedConstant, names As List(Of String))
            If argument.Kind = TypedConstantKind.Array Then
                For Each item In argument.Values
                    CollectNames(item, names)
                Next

                Return
            End If

            Dim name = TryCast(argument.ValueInternal, String)
            names.Add(If(name, ""))
        End Sub

        Private Shared Function AttributeSyntax(attribute As VisualBasicAttributeData) As SyntaxNode
            Dim reference = attribute.ApplicationSyntaxReference
            If reference Is Nothing Then
                Return Nothing
            End If

            Return reference.GetSyntax()
        End Function

        Private Shared Function ResolveMemberName(method As MethodSymbol, name As String) As Symbol
            If method Is Nothing OrElse String.IsNullOrEmpty(name) OrElse method.ContainingType Is Nothing Then
                Return Nothing
            End If

            Dim current As NamedTypeSymbol = method.ContainingType
            While current IsNot Nothing
                Dim matched As Symbol = Nothing
                Dim blocked = False
                For Each member In current.GetMembers()
                    If Not String.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase) Then
                        Continue For
                    End If

                    Dim field = TryCast(member, FieldSymbol)
                    Dim [property] = TryCast(member, PropertySymbol)
                    If field Is Nothing AndAlso ([property] Is Nothing OrElse [property].ParameterCount <> 0) Then
                        blocked = True
                        Continue For
                    End If

                    If method.IsShared AndAlso Not member.IsShared Then
                        blocked = True
                        Continue For
                    End If

                    If Not IsMemberAccessible(method, member) Then
                        blocked = True
                        Continue For
                    End If

                    matched = member
                    Exit For
                Next

                If matched IsNot Nothing Then
                    Return matched
                End If

                If blocked Then
                    Return Nothing
                End If

                current = current.BaseTypeNoUseSiteDiagnostics
            End While

            Return Nothing
        End Function

        Private Function ResolvedMembers(method As MethodSymbol, names As ImmutableArray(Of String)) As List(Of Symbol)
            Dim result As New List(Of Symbol)()
            If method Is Nothing OrElse names.IsDefaultOrEmpty Then
                Return result
            End If

            For Each name In names
                Dim member = ResolveMemberOn(method, name)
                If member IsNot Nothing AndAlso Not result.Contains(member) Then
                    result.Add(member)
                End If
            Next

            Return result
        End Function

        Private Shared Function ResolveMemberOn(method As MethodSymbol, name As String) As Symbol
            Return ResolveMemberName(method, name)
        End Function

        Private Shared Function IsMemberAccessible(method As MethodSymbol, member As Symbol) As Boolean
            If member.DeclaredAccessibility = Accessibility.Private Then
                Return member.ContainingType.OriginalDefinition.Equals(method.ContainingType.OriginalDefinition)
            End If

            Return True
        End Function

        Private Shared Function SameMethod(left As MethodSymbol, right As MethodSymbol) As Boolean
            If left Is Nothing OrElse right Is Nothing Then
                Return False
            End If

            Return left.OriginalDefinition.Equals(right.OriginalDefinition)
        End Function

        Private Sub Report(code As ERRID, syntax As SyntaxNode, ParamArray args As Object())
            If _summaryOnly OrElse syntax Is Nothing OrElse syntax.SyntaxTree Is Nothing OrElse Not WarningsEnabled(syntax) Then
                Return
            End If

            Dim location = syntax.GetLocation()
            Dim key = CInt(code).ToString() & ":" & location.SourceSpan.Start.ToString() & ":" & String.Join("|", args)
            If Not _reported.Add(key) Then
                Return
            End If

            _diagnostics.Add(New VBDiagnostic(ErrorFactory.ErrorInfo(code, args), location))
        End Sub

        Private Shared Function MemberSyntax(member As Symbol) As SyntaxNode
            Dim location = member.Locations.FirstOrDefault()
            If location Is Nothing OrElse location.SourceTree Is Nothing Then
                Return Nothing
            End If

            Return location.SourceTree.GetRoot().FindNode(location.SourceSpan)
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
