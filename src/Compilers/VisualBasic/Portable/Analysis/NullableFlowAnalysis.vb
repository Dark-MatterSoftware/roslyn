' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Warns when a value that may be Nothing reaches a non-nullable local, parameter, return, or member access.
    ''' An <c>IsNot Nothing</c> test treats the value as not null in the taken branch.
    ''' </summary>
    Friend NotInheritable Class NullableFlowAnalysis
        Inherits BoundTreeWalkerWithStackGuard

        Private ReadOnly _method As MethodSymbol
        Private ReadOnly _diagnostics As DiagnosticBag
        Private ReadOnly _project As NullableContextOptions
        Private _state As Dictionary(Of Symbol, Boolean)

        Private Sub New(method As MethodSymbol, diagnostics As DiagnosticBag, project As NullableContextOptions)
            _method = method
            _diagnostics = diagnostics
            _project = project
            _state = New Dictionary(Of Symbol, Boolean)()
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
            For Each parameter In method.Parameters
                If Not parameter.IsMe Then
                    walker._state(parameter) = AnnotationOf(parameter) = NullableAnnotation.Annotated
                End If
            Next

            walker.Visit(body)
        End Sub

        Public Overrides Function VisitIfStatement(node As BoundIfStatement) As BoundNode
            Dim tested As Symbol = Nothing
            Dim trueWhenNull As Boolean = False
            Dim saved = CloneState()

            If TryGetNullTest(node.Condition, tested, trueWhenNull) Then
                _state(tested) = trueWhenNull
                Visit(node.Consequence)
                Dim afterThen = CloneState()
                _state = saved

                If node.AlternativeOpt IsNot Nothing Then
                    _state(tested) = Not trueWhenNull
                    Visit(node.AlternativeOpt)
                    Merge(afterThen)
                Else
                    Merge(afterThen)
                End If
            Else
                ExpressionMayBeNull(node.Condition)
                saved = CloneState()
                Visit(node.Consequence)
                Dim afterThen = CloneState()
                _state = saved
                If node.AlternativeOpt IsNot Nothing Then
                    Visit(node.AlternativeOpt)
                End If
                Merge(afterThen)
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitLocalDeclaration(node As BoundLocalDeclaration) As BoundNode
            Dim mayBeNull = False
            If node.DeclarationInitializerOpt IsNot Nothing Then
                mayBeNull = ExpressionMayBeNull(node.DeclarationInitializerOpt)
                If mayBeNull AndAlso AnnotationOf(node.LocalSymbol) = NullableAnnotation.NotAnnotated Then
                    Warn(ERRID.WRN_PossibleNullAssignment, node.DeclarationInitializerOpt.Syntax, node.LocalSymbol.Name)
                End If
            End If

            _state(node.LocalSymbol) = mayBeNull
            Return Nothing
        End Function

        Public Overrides Function VisitAssignmentOperator(node As BoundAssignmentOperator) As BoundNode
            Dim mayBeNull = ExpressionMayBeNull(node.Right)
            Dim target = TrackedSymbol(node.Left)
            If target IsNot Nothing Then
                If mayBeNull AndAlso AnnotationOf(target) = NullableAnnotation.NotAnnotated Then
                    Warn(ERRID.WRN_PossibleNullAssignment, node.Right.Syntax, target.Name)
                End If
                _state(target) = mayBeNull
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitReturnStatement(node As BoundReturnStatement) As BoundNode
            If node.ExpressionOpt IsNot Nothing Then
                Dim mayBeNull = ExpressionMayBeNull(node.ExpressionOpt)
                If mayBeNull AndAlso AnnotationOf(_method) = NullableAnnotation.NotAnnotated Then
                    Warn(ERRID.WRN_PossibleNullReturn, node.ExpressionOpt.Syntax)
                End If
            End If

            Return Nothing
        End Function

        Public Overrides Function VisitCall(node As BoundCall) As BoundNode
            WarnReceiver(node.ReceiverOpt)
            CheckArguments(node.Method, node.Arguments)
            Return Nothing
        End Function

        Public Overrides Function VisitPropertyAccess(node As BoundPropertyAccess) As BoundNode
            WarnReceiver(node.ReceiverOpt)
            If Not node.Arguments.IsDefault Then
                For Each argument In node.Arguments
                    ExpressionMayBeNull(argument)
                Next
            End If

            Return Nothing
        End Function

        Private Function ExpressionMayBeNull(expr As BoundExpression) As Boolean
            If expr Is Nothing Then
                Return False
            End If

            If expr.ConstantValueOpt IsNot Nothing AndAlso expr.ConstantValueOpt.IsNothing Then
                Return True
            End If

            Select Case expr.Kind
                Case BoundKind.Parenthesized
                    Return ExpressionMayBeNull(DirectCast(expr, BoundParenthesized).Expression)
                Case BoundKind.Conversion, BoundKind.DirectCast, BoundKind.TryCast
                    Return ExpressionMayBeNull(DirectCast(expr, BoundConversionOrCast).Operand)
                Case BoundKind.Local, BoundKind.Parameter
                    Return MayBeNull(TrackedSymbol(expr))
                Case BoundKind.Call
                    Dim invocation = DirectCast(expr, BoundCall)
                    WarnReceiver(invocation.ReceiverOpt)
                    CheckArguments(invocation.Method, invocation.Arguments)
                    Return AnnotationOf(invocation.Method) = NullableAnnotation.Annotated
                Case BoundKind.PropertyAccess
                    VisitPropertyAccess(DirectCast(expr, BoundPropertyAccess))
                    Return False
                Case BoundKind.BinaryOperator
                    Dim binary = DirectCast(expr, BoundBinaryOperator)
                    ExpressionMayBeNull(binary.Left)
                    ExpressionMayBeNull(binary.Right)
                    Return False
                Case Else
                    Visit(expr)
                    Return False
            End Select
        End Function

        Private Sub WarnReceiver(receiver As BoundExpression)
            If receiver Is Nothing Then
                Return
            End If

            Select Case receiver.Kind
                Case BoundKind.TypeExpression, BoundKind.MeReference, BoundKind.MyBaseReference, BoundKind.MyClassReference
                    Return
            End Select

            If ExpressionMayBeNull(receiver) Then
                Dim symbol = TrackedSymbol(receiver)
                Dim name = If(symbol Is Nothing, "expression", symbol.Name)
                Warn(ERRID.WRN_PossibleNullReference, receiver.Syntax, name)
            End If
        End Sub

        Private Sub CheckArguments(method As MethodSymbol, arguments As ImmutableArray(Of BoundExpression))
            If arguments.IsDefault OrElse method Is Nothing Then
                Return
            End If

            Dim count = Math.Min(arguments.Length, method.ParameterCount)
            For i = 0 To count - 1
                Dim parameter = method.Parameters(i)
                If parameter.IsParamArray Then
                    ExpressionMayBeNull(arguments(i))
                    Continue For
                End If

                If ExpressionMayBeNull(arguments(i)) AndAlso AnnotationOf(parameter) = NullableAnnotation.NotAnnotated Then
                    Warn(ERRID.WRN_PossibleNullArgument, arguments(i).Syntax, parameter.Name)
                End If
            Next

            For i = count To arguments.Length - 1
                ExpressionMayBeNull(arguments(i))
            Next
        End Sub

        Private Function MayBeNull(symbol As Symbol) As Boolean
            If symbol Is Nothing Then
                Return False
            End If

            Dim state As Boolean = False
            If _state.TryGetValue(symbol, state) Then
                Return state
            End If

            Return AnnotationOf(symbol) = NullableAnnotation.Annotated
        End Function

        Private Shared Function TrackedSymbol(expr As BoundExpression) As Symbol
            If expr Is Nothing Then
                Return Nothing
            End If

            Select Case expr.Kind
                Case BoundKind.Local
                    Return DirectCast(expr, BoundLocal).LocalSymbol
                Case BoundKind.Parameter
                    Return DirectCast(expr, BoundParameter).ParameterSymbol
                Case BoundKind.Parenthesized
                    Return TrackedSymbol(DirectCast(expr, BoundParenthesized).Expression)
                Case BoundKind.Conversion, BoundKind.DirectCast, BoundKind.TryCast
                    Return TrackedSymbol(DirectCast(expr, BoundConversionOrCast).Operand)
            End Select

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
            Dim current = condition
            While current IsNot Nothing AndAlso current.Kind = BoundKind.Parenthesized
                current = DirectCast(current, BoundParenthesized).Expression
            End While

            Dim binary = TryCast(current, BoundBinaryOperator)
            If binary Is Nothing Then
                Return False
            End If

            Dim kind = binary.OperatorKind And BinaryOperatorKind.OpMask
            If kind <> BinaryOperatorKind.Is AndAlso kind <> BinaryOperatorKind.IsNot Then
                Return False
            End If

            Dim tested As Symbol = Nothing
            Dim comparedWithNothing = False
            If IsNothing(binary.Right) Then
                tested = TrackedSymbol(binary.Left)
                comparedWithNothing = tested IsNot Nothing
            ElseIf IsNothing(binary.Left) Then
                tested = TrackedSymbol(binary.Right)
                comparedWithNothing = tested IsNot Nothing
            End If

            If Not comparedWithNothing Then
                Return False
            End If

            symbol = tested
            trueWhenNull = kind = BinaryOperatorKind.Is
            Return True
        End Function

        Private Shared Function IsNothing(expr As BoundExpression) As Boolean
            Return expr IsNot Nothing AndAlso expr.ConstantValueOpt IsNot Nothing AndAlso expr.ConstantValueOpt.IsNothing
        End Function

        Private Sub Warn(code As ERRID, syntax As SyntaxNode, ParamArray args As Object())
            If syntax Is Nothing OrElse Not WarningsEnabled(syntax) Then
                Return
            End If

            _diagnostics.Add(New VBDiagnostic(ErrorFactory.ErrorInfo(code, args), syntax.GetLocation()))
        End Sub

        Private Function WarningsEnabled(node As SyntaxNode) As Boolean
            If node.SyntaxTree Is Nothing Then
                Return False
            End If

            Dim context = NullableContextFacts.ForTree(node.SyntaxTree, _project).GetContext(node.SpanStart)
            Return (context And NullableContextOptions.Warnings) <> 0
        End Function

        Private Function CloneState() As Dictionary(Of Symbol, Boolean)
            Return New Dictionary(Of Symbol, Boolean)(_state)
        End Function

        Private Sub Merge(other As Dictionary(Of Symbol, Boolean))
            For Each pair In other
                Dim current As Boolean = False
                _state.TryGetValue(pair.Key, current)
                _state(pair.Key) = current OrElse pair.Value
            Next
        End Sub
    End Class

End Namespace
