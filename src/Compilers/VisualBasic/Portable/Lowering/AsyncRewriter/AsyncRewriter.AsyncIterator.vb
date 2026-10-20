' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Async Iterator Function support. The state machine follows the C# async-iterator design: a class that
    ''' implements IAsyncEnumerable(Of T) (for enumerables), IAsyncEnumerator(Of T), IAsyncDisposable and
    ''' IValueTaskSource, driven by AsyncIteratorMethodBuilder and a ManualResetValueTaskSourceCore(Of Boolean)
    ''' promise that MoveNextAsync and DisposeAsync hand out as ValueTasks.
    ''' </summary>
    Partial Friend NotInheritable Class AsyncRewriter

        Private _isEnumerable As Boolean
        Private _elementType As TypeSymbol
        Private _promiseOfValueOrEndField As FieldSymbol
        Private _currentField As FieldSymbol
        Private _disposeModeField As FieldSymbol
        Private _initialThreadIdField As FieldSymbol

        Private Sub InitializeAsyncIterator()
            Dim returnType = DirectCast(Me.Method.ReturnType, NamedTypeSymbol)
            Me._isEnumerable = TypeSymbol.Equals(returnType.OriginalDefinition,
                                                 Me.F.Compilation.GetWellKnownType(WellKnownType.System_Collections_Generic_IAsyncEnumerable_T),
                                                 TypeCompareKind.ConsiderEverything)
            Me._elementType = returnType.TypeArgumentsNoUseSiteDiagnostics(0).InternalSubstituteTypeParameters(Me.TypeMap).Type
        End Sub

        Private Sub EnsureAsyncIteratorSymbols(bag As BindingDiagnosticBag)
            EnsureWellKnownType(WellKnownType.System_Runtime_CompilerServices_AsyncIteratorMethodBuilder, bag)
            EnsureWellKnownMember(WellKnownMember.System_Runtime_CompilerServices_AsyncIteratorMethodBuilder__Create, bag)
            EnsureWellKnownMember(WellKnownMember.System_Runtime_CompilerServices_AsyncIteratorMethodBuilder__Complete, bag)
            EnsureWellKnownMember(WellKnownMember.System_Runtime_CompilerServices_AsyncIteratorMethodBuilder__AwaitOnCompleted, bag)
            EnsureWellKnownMember(WellKnownMember.System_Runtime_CompilerServices_AsyncIteratorMethodBuilder__AwaitUnsafeOnCompleted, bag)
            EnsureWellKnownMember(WellKnownMember.System_Runtime_CompilerServices_AsyncIteratorMethodBuilder__MoveNext_T, bag)

            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__Reset, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__SetResult, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__SetException, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__GetResult, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__GetStatus, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__OnCompleted, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__get_Version, bag)

            If Me._isEnumerable Then
                EnsureWellKnownMember(WellKnownMember.System_Collections_Generic_IAsyncEnumerable_T__GetAsyncEnumerator, bag)
            End If

            EnsureWellKnownMember(WellKnownMember.System_Collections_Generic_IAsyncEnumerator_T__MoveNextAsync, bag)
            EnsureWellKnownMember(WellKnownMember.System_Collections_Generic_IAsyncEnumerator_T__get_Current, bag)
            EnsureWellKnownMember(WellKnownMember.System_IAsyncDisposable__DisposeAsync, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource_T__GetResult, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource_T__GetStatus, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource_T__OnCompleted, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource__GetResult, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource__GetStatus, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource__OnCompleted, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_ValueTask_T__ctorSourceAndToken, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_ValueTask_T__ctorValue, bag)
            EnsureWellKnownMember(WellKnownMember.System_Threading_Tasks_ValueTask__ctor, bag)
            EnsureWellKnownType(WellKnownType.System_NotSupportedException, bag)
        End Sub

        Private Sub GenerateAsyncIteratorControlFields()
            Dim promiseType = Me.F.WellKnownType(WellKnownType.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T).Construct(Me.F.SpecialType(SpecialType.System_Boolean))
            Me._promiseOfValueOrEndField = Me.F.StateMachineField(promiseType, Me.Method, GeneratedNames.MakeAsyncIteratorPromiseOfValueOrEndFieldName(), Accessibility.Public)
            Me._currentField = Me.F.StateMachineField(Me._elementType, Me.Method, GeneratedNames.MakeIteratorCurrentFieldName(), Accessibility.Public)
            Me._disposeModeField = Me.F.StateMachineField(Me.F.SpecialType(SpecialType.System_Boolean), Me.Method, GeneratedNames.MakeAsyncIteratorDisposeModeFieldName(), Accessibility.Public)
            Me._initialThreadIdField = If(Me._isEnumerable,
                                          Me.F.StateMachineField(Me.F.SpecialType(SpecialType.System_Int32), Me.Method, GeneratedNames.MakeIteratorInitialThreadIdName(), Accessibility.Public),
                                          Nothing)
        End Sub

        Private Sub InitializeAsyncIteratorStateMachine(bodyBuilder As ArrayBuilder(Of BoundStatement), frameType As NamedTypeSymbol, stateMachineLocal As LocalSymbol)
            ' An enumerable starts finished, so that GetAsyncEnumerator on the creating thread reuses this instance.
            ' STAT:   localStateMachine = New AsyncIteratorClass(initialState)
            Dim initialState = If(Me._isEnumerable, StateMachineState.FinishedState, StateMachineState.InitialAsyncIteratorState)
            bodyBuilder.Add(
                Me.F.Assignment(
                    Me.F.Local(stateMachineLocal, True),
                    Me.F.[New](StateMachineType.Constructor.AsMember(frameType), Me.F.Literal(initialState))))
        End Sub

        Private Function GenerateManagedThreadId() As BoundExpression
            Dim currentManagedThreadIdProperty As PropertySymbol = Me.F.WellKnownMember(Of PropertySymbol)(WellKnownMember.System_Environment__CurrentManagedThreadId, isOptional:=True)
            Dim currentManagedThreadIdMethod As MethodSymbol = currentManagedThreadIdProperty?.GetMethod()

            If currentManagedThreadIdMethod IsNot Nothing Then
                Return Me.F.Call(Nothing, currentManagedThreadIdMethod)
            End If

            Return Me.F.Property(Me.F.Property(WellKnownMember.System_Threading_Thread__CurrentThread), WellKnownMember.System_Threading_Thread__ManagedThreadId)
        End Function

        Private Sub GenerateAsyncIteratorConstructor()
            Me.F.CurrentMethod = StateMachineType.Constructor
            Dim bodyBuilder = ArrayBuilder(Of BoundStatement).GetInstance()
            bodyBuilder.Add(Me.F.BaseInitialization())

            ' STAT:   Me.$State = state
            bodyBuilder.Add(Me.F.Assignment(Me.F.Field(Me.F.Me(), Me.StateField, True), Me.F.Parameter(Me.F.CurrentMethod.Parameters(0)).MakeRValue()))

            ' STAT:   Me.$Builder = AsyncIteratorMethodBuilder.Create()
            bodyBuilder.Add(Me.F.Assignment(Me.F.Field(Me.F.Me(), Me._builderField, True), Me.GenerateMethodCall(Nothing, Me._builderType, "Create")))

            If Me._isEnumerable Then
                ' STAT:   Me.$InitialThreadId = Environment.CurrentManagedThreadId
                bodyBuilder.Add(Me.F.Assignment(Me.F.Field(Me.F.Me(), Me._initialThreadIdField, True), GenerateManagedThreadId()))
            End If

            bodyBuilder.Add(Me.F.Return())
            Me.CloseMethod(Me.F.Block(bodyBuilder.ToImmutableAndFree()))
        End Sub

        Private Sub GenerateAsyncIteratorMethodImplementations()
            Dim enumeratorType = Me.F.WellKnownType(WellKnownType.System_Collections_Generic_IAsyncEnumerator_T).Construct(Me._elementType)

            If Me._isEnumerable Then
                GenerateGetAsyncEnumerator()
            End If

            ' Function MoveNextAsync() As ValueTask(Of Boolean)
            Me.OpenMethodImplementation(enumeratorType, WellKnownMember.System_Collections_Generic_IAsyncEnumerator_T__MoveNextAsync, "MoveNextAsync", Accessibility.Private)
            GenerateMoveNextAsyncOrDisposeAsync(isDispose:=False)

            ' ReadOnly Property Current As T
            Me.OpenPropertyImplementation(enumeratorType, WellKnownMember.System_Collections_Generic_IAsyncEnumerator_T__get_Current, "Current", Accessibility.Private)
            Me.F.CloseMethod(Me.F.Return(Me.F.Field(Me.F.Me(), Me._currentField, False)))

            ' Function DisposeAsync() As ValueTask
            Me.OpenMethodImplementation(WellKnownMember.System_IAsyncDisposable__DisposeAsync, "DisposeAsync", Accessibility.Private)
            GenerateMoveNextAsyncOrDisposeAsync(isDispose:=True)

            ' IValueTaskSource(Of Boolean) and IValueTaskSource forward to the promise.
            Dim valueTaskSourceOfBoolean = Me.F.WellKnownType(WellKnownType.System_Threading_Tasks_Sources_IValueTaskSource_T).Construct(Me.F.SpecialType(SpecialType.System_Boolean))

            Me.OpenMethodImplementation(valueTaskSourceOfBoolean, WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource_T__GetResult, "GetResult", Accessibility.Private)
            Me.F.CloseMethod(Me.F.Return(GeneratePromiseCall(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__GetResult)))

            Me.OpenMethodImplementation(valueTaskSourceOfBoolean, WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource_T__GetStatus, "GetStatus", Accessibility.Private)
            Me.F.CloseMethod(Me.F.Return(GeneratePromiseCall(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__GetStatus)))

            Me.OpenMethodImplementation(valueTaskSourceOfBoolean, WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource_T__OnCompleted, "OnCompleted", Accessibility.Private)
            Me.F.CloseMethod(Me.F.Block(Me.F.ExpressionStatement(GeneratePromiseCall(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__OnCompleted)), Me.F.Return()))

            Me.OpenMethodImplementation(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource__GetResult, "IValueTaskSource.GetResult", Accessibility.Private)
            Me.F.CloseMethod(Me.F.Block(Me.F.ExpressionStatement(GeneratePromiseCall(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__GetResult)), Me.F.Return()))

            Me.OpenMethodImplementation(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource__GetStatus, "IValueTaskSource.GetStatus", Accessibility.Private)
            Me.F.CloseMethod(Me.F.Return(GeneratePromiseCall(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__GetStatus)))

            Me.OpenMethodImplementation(WellKnownMember.System_Threading_Tasks_Sources_IValueTaskSource__OnCompleted, "IValueTaskSource.OnCompleted", Accessibility.Private)
            Me.F.CloseMethod(Me.F.Block(Me.F.ExpressionStatement(GeneratePromiseCall(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__OnCompleted)), Me.F.Return()))
        End Sub

        ''' <summary>
        ''' Calls a promise member, passing the parameters of the current method through.
        ''' </summary>
        Private Function GeneratePromiseCall(member As WellKnownMember) As BoundExpression
            Dim arguments = Me.F.CurrentMethod.Parameters.SelectAsArray(Function(p) DirectCast(Me.F.Parameter(p).MakeRValue(), BoundExpression))
            Return Me.F.Call(Me.F.Field(Me.F.Me(), Me._promiseOfValueOrEndField, False), GetPromiseMethod(member), arguments)
        End Function

        Friend Function GetPromiseMethod(member As WellKnownMember) As MethodSymbol
            Return Me.F.WellKnownMember(Of MethodSymbol)(member).AsMember(DirectCast(Me._promiseOfValueOrEndField.Type, NamedTypeSymbol))
        End Function

        Private Sub GenerateGetAsyncEnumerator()
            Dim enumerableType = Me.F.WellKnownType(WellKnownType.System_Collections_Generic_IAsyncEnumerable_T).Construct(Me._elementType)
            Me.OpenMethodImplementation(enumerableType, WellKnownMember.System_Collections_Generic_IAsyncEnumerable_T__GetAsyncEnumerator, "GetAsyncEnumerator", Accessibility.Private)

            Dim bodyBuilder = ArrayBuilder(Of BoundStatement).GetInstance()
            Dim resultVariable = Me.F.SynthesizedLocal(StateMachineType)

            ' If Me.$State = FinishedState AndAlso Me.$InitialThreadId = Environment.CurrentManagedThreadId Then
            '     Me.$State = InitialAsyncIteratorState
            '     Me.$Builder = AsyncIteratorMethodBuilder.Create()
            '     Me.$DisposeMode = False
            '     result = Me
            '     ' Me is already initialized for a reference type
            ' Else
            '     result = New AsyncIteratorClass(InitialAsyncIteratorState)
            ' End If
            ' thisInitialized:
            ' ' copy the initial parameter values
            Dim thisInitialized = Me.F.GenerateLabel("thisInitialized")
            bodyBuilder.Add(
                Me.F.If(
                    condition:=Me.F.LogicalAndAlso(
                        Me.F.IntEqual(Me.F.Field(Me.F.Me(), Me.StateField, False), Me.F.Literal(StateMachineState.FinishedState)),
                        Me.F.IntEqual(Me.F.Field(Me.F.Me(), Me._initialThreadIdField, False), GenerateManagedThreadId())),
                    thenClause:=Me.F.Block(
                        Me.F.Assignment(Me.F.Field(Me.F.Me(), Me.StateField, True), Me.F.Literal(StateMachineState.InitialAsyncIteratorState)),
                        Me.F.Assignment(Me.F.Field(Me.F.Me(), Me._builderField, True), Me.GenerateMethodCall(Nothing, Me._builderType, "Create")),
                        Me.F.Assignment(Me.F.Field(Me.F.Me(), Me._disposeModeField, True), Me.F.Literal(False)),
                        Me.F.Assignment(Me.F.Local(resultVariable, True), Me.F.Me()),
                        If(Me.Method.IsShared OrElse Me.Method.MeParameter.Type.IsReferenceType,
                           Me.F.Goto(thisInitialized),
                           DirectCast(Me.F.StatementList(), BoundStatement))),
                    elseClause:=Me.F.Assignment(Me.F.Local(resultVariable, True), Me.F.[New](StateMachineType.Constructor, Me.F.Literal(StateMachineState.InitialAsyncIteratorState)))))

            If Not Me.Method.IsShared Then
                AddParameterCopy(bodyBuilder, resultVariable, Me.Method.MeParameter)
            End If

            bodyBuilder.Add(Me.F.Label(thisInitialized))

            For Each parameter In Me.Method.Parameters
                AddParameterCopy(bodyBuilder, resultVariable, parameter)
            Next

            bodyBuilder.Add(Me.F.Return(Me.F.Local(resultVariable, False)))
            Me.CloseMethod(Me.F.Block(ImmutableArray.Create(resultVariable), bodyBuilder.ToImmutableAndFree()))
        End Sub

        Private Sub AddParameterCopy(bodyBuilder As ArrayBuilder(Of BoundStatement), resultVariable As LocalSymbol, parameter As ParameterSymbol)
            Dim proxy As CapturedSymbolOrExpression = Nothing
            If Not Me.nonReusableLocalProxies.TryGetValue(parameter, proxy) Then
                Return
            End If

            Dim destination = DirectCast(proxy, CapturedParameterSymbol).Field
            Dim source = DirectCast(Me.InitialParameters(parameter), CapturedParameterSymbol).Field
            bodyBuilder.Add(
                Me.F.Assignment(
                    Me.F.Field(Me.F.Local(resultVariable, True), destination, True),
                    Me.F.Field(Me.F.Me(), source, False)))
        End Sub

        Private Sub GenerateMoveNextAsyncOrDisposeAsync(isDispose As Boolean)
            Dim bodyBuilder = ArrayBuilder(Of BoundStatement).GetInstance()
            Dim returnType = DirectCast(Me.F.CurrentMethod.ReturnType, NamedTypeSymbol)
            Dim stateMachineVariable = Me.F.SynthesizedLocal(StateMachineType)

            If isDispose Then
                ' STAT:   If Me.$State >= NotStartedOrRunningState Then Throw New NotSupportedException()
                bodyBuilder.Add(
                    Me.F.If(
                        Me.F.Not(Me.F.IntLessThan(Me.F.Field(Me.F.Me(), Me.StateField, False), Me.F.Literal(StateMachineState.NotStartedOrRunningState))),
                        Me.F.Throw(Me.F.[New](Me.F.WellKnownType(WellKnownType.System_NotSupportedException)))))

                ' STAT:   If Me.$State = FinishedState Then Return Nothing
                bodyBuilder.Add(
                    Me.F.If(
                        Me.F.IntEqual(Me.F.Field(Me.F.Me(), Me.StateField, False), Me.F.Literal(StateMachineState.FinishedState)),
                        Me.F.Return(Me.F.Null(returnType))))

                ' STAT:   Me.$DisposeMode = True
                bodyBuilder.Add(Me.F.Assignment(Me.F.Field(Me.F.Me(), Me._disposeModeField, True), Me.F.Literal(True)))
            Else
                ' STAT:   If Me.$State = FinishedState Then Return New ValueTask(Of Boolean)(False)
                Dim ctorValue = Me.F.WellKnownMember(Of MethodSymbol)(WellKnownMember.System_Threading_Tasks_ValueTask_T__ctorValue).AsMember(returnType)
                bodyBuilder.Add(
                    Me.F.If(
                        Me.F.IntEqual(Me.F.Field(Me.F.Me(), Me.StateField, False), Me.F.Literal(StateMachineState.FinishedState)),
                        Me.F.Return(Me.F.[New](ctorValue, Me.F.Literal(False)))))
            End If

            ' STAT:   Me.$PromiseOfValueOrEnd.Reset()
            bodyBuilder.Add(Me.F.ExpressionStatement(Me.F.Call(Me.F.Field(Me.F.Me(), Me._promiseOfValueOrEndField, False), GetPromiseMethod(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__Reset))))

            ' STAT:   stateMachine = Me
            ' STAT:   Me.$Builder.MoveNext(stateMachine)
            bodyBuilder.Add(Me.F.Assignment(Me.F.Local(stateMachineVariable, True), Me.F.Me()))
            bodyBuilder.Add(
                Me.F.ExpressionStatement(
                    Me.GenerateMethodCall(
                        Me.F.Field(Me.F.Me(), Me._builderField, False),
                        Me._builderType,
                        "MoveNext",
                        ImmutableArray.Create(Of TypeSymbol)(StateMachineType),
                        Me.F.Local(stateMachineVariable, True))))

            ' STAT:   Return New ValueTask(Of Boolean)(Me, Me.$PromiseOfValueOrEnd.Version)
            '    or   Return New ValueTask(Me, Me.$PromiseOfValueOrEnd.Version)
            Dim ctorSourceAndToken = Me.F.WellKnownMember(Of MethodSymbol)(
                If(isDispose, WellKnownMember.System_Threading_Tasks_ValueTask__ctor, WellKnownMember.System_Threading_Tasks_ValueTask_T__ctorSourceAndToken))
            If Not isDispose Then
                ctorSourceAndToken = ctorSourceAndToken.AsMember(returnType)
            End If

            Dim version = Me.F.Call(Me.F.Field(Me.F.Me(), Me._promiseOfValueOrEndField, False), GetPromiseMethod(WellKnownMember.System_Threading_Tasks_Sources_ManualResetValueTaskSourceCore_T__get_Version))
            bodyBuilder.Add(
                Me.F.Return(
                    Me.F.[New](ctorSourceAndToken,
                               Me.F.Convert(ctorSourceAndToken.Parameters(0).Type, Me.F.Me(), ConversionKind.WideningReference),
                               version)))

            Me.CloseMethod(Me.F.Block(ImmutableArray.Create(stateMachineVariable), bodyBuilder.ToImmutableAndFree()))
        End Sub
    End Class
End Namespace
