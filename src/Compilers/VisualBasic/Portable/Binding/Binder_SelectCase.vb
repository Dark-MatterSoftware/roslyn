' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend Class Binder

#Region "Bind select case statement"

    Private NotInheritable Class PlaceholderReplacer
        Inherits BoundTreeRewriterWithStackGuard

            Private ReadOnly _replacement As BoundExpression

            Public Sub New(replacement As BoundExpression)
                _replacement = replacement
            End Sub

            Public Overrides Function VisitRValuePlaceholder(node As BoundRValuePlaceholder) As BoundNode
                Return _replacement
            End Function
        End Class

        Private Function BindSelectCaseExpression(node As SelectCaseExpressionSyntax, diagnostics As BindingDiagnosticBag) As BoundExpression
            Dim expression = BindValue(node.Expression, diagnostics)
            Dim placeholder = New BoundRValuePlaceholder(node.Expression, If(expression.Type, ErrorTypeSymbol.UnknownResultType))
            placeholder.SetWasCompilerGenerated()

            Dim conditions = ArrayBuilder(Of BoundExpression).GetInstance()
            Dim values = ArrayBuilder(Of BoundExpression).GetInstance()
            Dim elseValue As BoundExpression = Nothing
            Dim convertElements = expression.Type IsNot Nothing AndAlso expression.Type.IsIntrinsicOrEnumType()

            For Each arm In node.Arms
                Dim armValue = BindValue(arm.Value, diagnostics)
                If arm.CaseStatement.Kind = SyntaxKind.CaseElseStatement Then
                    elseValue = armValue
                    Continue For
                End If

                Dim boundCase = BindCaseStatement(arm.CaseStatement, placeholder, convertElements, diagnostics)
                Dim condition As BoundExpression = Nothing
                For Each clause In boundCase.CaseClauses
                    Dim clauseCondition As BoundExpression = Nothing
                    ComputeCaseClauseCondition(clause, clauseCondition, placeholder, diagnostics)
                    If condition Is Nothing Then
                        condition = clauseCondition
                    Else
                        condition = BindBinaryOperator(
                            node:=arm,
                            left:=condition,
                            right:=clauseCondition,
                            operatorTokenKind:=SyntaxKind.OrElseKeyword,
                            preliminaryOperatorKind:=BinaryOperatorKind.OrElse,
                            isOperandOfConditionalBranch:=False,
                            diagnostics:=diagnostics,
                            isSelectCase:=True).MakeCompilerGenerated()
                    End If
                Next

                If condition IsNot Nothing Then
                    conditions.Add(condition)
                    values.Add(armValue)
                End If
            Next

            If elseValue Is Nothing Then
                ReportDiagnostic(diagnostics, node, ERRID.ERR_ExpectedEndSelect)
                elseValue = BadExpression(node, ErrorTypeSymbol.UnknownResultType).MakeCompilerGenerated()
            End If

            Dim resultType = If(values.Count > 0 AndAlso values(0).Type IsNot Nothing, values(0).Type, If(elseValue.Type, ErrorTypeSymbol.UnknownResultType))
            elseValue = ApplyImplicitConversion(elseValue.Syntax, resultType, elseValue, diagnostics)
            Dim result As BoundExpression = elseValue

            For i = values.Count - 1 To 0 Step -1
                Dim armValue = ApplyImplicitConversion(values(i).Syntax, resultType, values(i), diagnostics)
                result = New BoundTernaryConditionalExpression(node, conditions(i), armValue, result, Nothing, resultType)
            Next

            conditions.Free()
            values.Free()

            Dim temp As LocalSymbol = New SynthesizedLocal(ContainingMember, expression.Type, SynthesizedLocalKind.LoweringTemp)
            Dim stored = New BoundAssignmentOperator(
                node,
                New BoundLocal(node, temp, expression.Type),
                expression,
                suppressObjectClone:=True,
                type:=expression.Type)
            Dim replacement = New BoundLocal(node, temp, expression.Type).MakeRValue()
            result = DirectCast(New PlaceholderReplacer(replacement).Visit(result), BoundExpression)
            Return New BoundSequence(node, ImmutableArray.Create(temp), ImmutableArray.Create(Of BoundExpression)(stored), result, resultType)
        End Function

        Private Function BindSelectBlock(node As SelectBlockSyntax, diagnostics As BindingDiagnosticBag) As BoundStatement
            Debug.Assert(node IsNot Nothing)

            ' Bind select expression
            Dim selectExprStatementSyntax = node.SelectStatement
            Dim expression = BindSelectExpression(selectExprStatementSyntax.Expression, diagnostics)

            If expression.HasErrors Then
                diagnostics = BindingDiagnosticBag.Discarded
            End If

            Dim exprPlaceHolder = New BoundRValuePlaceholder(selectExprStatementSyntax.Expression, expression.Type)
            exprPlaceHolder.SetWasCompilerGenerated()

            Dim expressionStmt = New BoundExpressionStatement(selectExprStatementSyntax, expression)

            ' Get the binder for the select block. This defines the exit label.
            Dim selectBinder = GetBinder(DirectCast(node, VisualBasicSyntaxNode))

            ' Flag to determine if we need to generate switch table based code or If list based code.
            ' See OptimizeSelectStatement method for more details.
            Dim recommendSwitchTable = False

            ' Bind case blocks.
            Dim caseBlocks As ImmutableArray(Of BoundCaseBlock) = selectBinder.BindCaseBlocks(
                                                                    node.CaseBlocks,
                                                                    exprPlaceHolder,
                                                                    convertCaseElements:=expression.Type.IsIntrinsicOrEnumType(),
                                                                    recommendSwitchTable:=recommendSwitchTable,
                                                                    diagnostics:=diagnostics)

            ' Create the bound node.
            Return New BoundSelectStatement(node, expressionStmt, exprPlaceHolder, caseBlocks, recommendSwitchTable,
                                            exitLabel:=selectBinder.GetExitLabel(SyntaxKind.ExitSelectStatement))
        End Function

        Private Function BindSelectExpression(node As ExpressionSyntax, diagnostics As BindingDiagnosticBag) As BoundExpression
            ' SPEC: A Select Case statement executes statements based on the value of an expression.
            ' SPEC: The expression must be classified as a value.

            ' We want to generate select case specific diagnostics if select expression is
            ' AddressOf expression or Lambda expression.
            ' For remaining expression kinds, bind the select expression as value.

            ' We also need to handle SyntaxKind.ParenthesizedExpression here.
            ' We might have a AddressOf expression or Lambda expression within a parenthesized expression.
            ' We want to generate ERRID.ERR_AddressOfInSelectCaseExpr/ERRID.ERR_LambdaInSelectCaseExpr for this case.
            ' See test BindingErrorTests.BC36635ERR_LambdaInSelectCaseExpr.

            Dim errorId As ERRID = Nothing

            Select Case node.Kind
                Case SyntaxKind.ParenthesizedExpression
                    Dim parenthesizedExpr = DirectCast(node, ParenthesizedExpressionSyntax)
                    Dim boundExpression = BindSelectExpression(parenthesizedExpr.Expression, diagnostics)
                    Return New BoundParenthesized(node, boundExpression, boundExpression.Type)

                Case SyntaxKind.AddressOfExpression
                    errorId = ERRID.ERR_AddressOfInSelectCaseExpr

                Case SyntaxKind.MultiLineFunctionLambdaExpression, SyntaxKind.MultiLineSubLambdaExpression,
                    SyntaxKind.SingleLineFunctionLambdaExpression, SyntaxKind.SingleLineSubLambdaExpression
                    errorId = ERRID.ERR_LambdaInSelectCaseExpr
            End Select

            Dim boundExpr = BindExpression(node, diagnostics)

            If boundExpr.HasErrors() Then
                boundExpr = MakeRValue(boundExpr, diagnostics)

            ElseIf errorId <> Nothing Then
                ReportDiagnostic(diagnostics, node, errorId)
                boundExpr = MakeRValueAndIgnoreDiagnostics(boundExpr)

            Else
                boundExpr = MakeRValue(boundExpr, diagnostics)
            End If

            Return boundExpr
        End Function

        Private Function BindCaseBlocks(
            caseBlocks As SyntaxList(Of CaseBlockSyntax),
            selectExpression As BoundRValuePlaceholder,
            convertCaseElements As Boolean,
            ByRef recommendSwitchTable As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As ImmutableArray(Of BoundCaseBlock)

            If Not caseBlocks.IsEmpty() Then
                Dim caseBlocksBuilder = ArrayBuilder(Of BoundCaseBlock).GetInstance()

                ' Bind case blocks.
                For Each caseBlock In caseBlocks
                    caseBlocksBuilder.Add(BindCaseBlock(caseBlock, selectExpression, convertCaseElements, diagnostics))
                Next

                Return OptimizeSelectStatement(selectExpression, caseBlocksBuilder, recommendSwitchTable, diagnostics)
            End If

            Return ImmutableArray(Of BoundCaseBlock).Empty
        End Function

        Private Function BindCaseBlock(
            node As CaseBlockSyntax,
            selectExpression As BoundRValuePlaceholder,
            convertCaseElements As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As BoundCaseBlock

            Dim caseStatement As BoundCaseStatement = BindCaseStatement(node.CaseStatement, selectExpression, convertCaseElements, diagnostics)

            Dim statementsSyntax As SyntaxList(Of StatementSyntax) = node.Statements
            Dim bodyBinder = GetBinder(statementsSyntax)
            Dim body As BoundBlock = bodyBinder.BindBlock(node, statementsSyntax, diagnostics).MakeCompilerGenerated()

            Return New BoundCaseBlock(node, caseStatement, body)
        End Function

        Private Function BindCaseStatement(
            node As CaseStatementSyntax,
            selectExpressionOpt As BoundRValuePlaceholder,
            convertCaseElements As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As BoundCaseStatement

            Dim caseClauses As ImmutableArray(Of BoundCaseClause)

            If node.Kind = SyntaxKind.CaseStatement Then
                Dim caseClauseBuilder = ArrayBuilder(Of BoundCaseClause).GetInstance()

                ' Bind case clauses.
                For Each caseClause In node.Cases
                    caseClauseBuilder.Add(BindCaseClause(caseClause, selectExpressionOpt, convertCaseElements, diagnostics))
                Next

                caseClauses = caseClauseBuilder.ToImmutableAndFree()
            Else
                Debug.Assert(node.Kind = SyntaxKind.CaseElseStatement)
                caseClauses = ImmutableArray(Of BoundCaseClause).Empty
            End If

            Return New BoundCaseStatement(node, caseClauses, conditionOpt:=Nothing)
        End Function

        Private Function BindCaseClause(
            node As CaseClauseSyntax,
            selectExpressionOpt As BoundRValuePlaceholder,
            convertCaseElements As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As BoundCaseClause
            Select Case node.Kind
                Case SyntaxKind.CaseEqualsClause, SyntaxKind.CaseNotEqualsClause,
                     SyntaxKind.CaseGreaterThanClause, SyntaxKind.CaseGreaterThanOrEqualClause,
                     SyntaxKind.CaseLessThanClause, SyntaxKind.CaseLessThanOrEqualClause

                    Return BindRelationalCaseClause(DirectCast(node, RelationalCaseClauseSyntax), selectExpressionOpt, convertCaseElements, diagnostics)

                Case SyntaxKind.SimpleCaseClause
                    Return BindSimpleCaseClause(DirectCast(node, SimpleCaseClauseSyntax), selectExpressionOpt, convertCaseElements, diagnostics)

                Case SyntaxKind.RangeCaseClause
                    Return BindRangeCaseClause(DirectCast(node, RangeCaseClauseSyntax), selectExpressionOpt, convertCaseElements, diagnostics)

                Case SyntaxKind.TypeCaseClause
                    Return BindTypeCaseClause(DirectCast(node, TypeCaseClauseSyntax), selectExpressionOpt, convertCaseElements, diagnostics)

                Case SyntaxKind.PositionalCaseClause
                    Return BindPositionalCaseClause(DirectCast(node, PositionalCaseClauseSyntax), selectExpressionOpt, diagnostics)

                Case SyntaxKind.ListCaseClause
                    Return BindListCaseClause(DirectCast(node, ListCaseClauseSyntax), selectExpressionOpt, diagnostics)

                Case Else
                    Throw ExceptionUtilities.UnexpectedValue(node.Kind)
            End Select
        End Function

        Private Function BindRelationalCaseClause(
            node As RelationalCaseClauseSyntax,
            selectExpressionOpt As BoundRValuePlaceholder,
            convertCaseElements As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As BoundCaseClause
            ' SPEC:     A Case clause may take two forms.
            ' SPEC:     One form is an optional Is keyword, a comparison operator, and an expression.
            ' SPEC:     The expression is converted to the type of the Select expression;
            ' SPEC:     if the expression is not implicitly convertible to the type of
            ' SPEC:     the Select expression, a compile-time error occurs.
            ' SPEC:     If the Select expression is E, the comparison operator is Op,
            ' SPEC:     and the Case expression is E1, the case is evaluated as E OP E1.
            ' SPEC:     The operator must be valid for the types of the two expressions;
            ' SPEC:     otherwise a compile-time error occurs.

            ' Bind relational case clause as binary operator: E OP E1.
            ' BindBinaryOperator will generate the appropriate diagnostics.

            Debug.Assert(SyntaxFacts.IsRelationalOperator(node.OperatorToken.Kind) OrElse node.ContainsDiagnostics)

            Dim operatorKind As BinaryOperatorKind
            Select Case node.Kind
                Case SyntaxKind.CaseEqualsClause : operatorKind = BinaryOperatorKind.Equals
                Case SyntaxKind.CaseNotEqualsClause : operatorKind = BinaryOperatorKind.NotEquals
                Case SyntaxKind.CaseLessThanOrEqualClause : operatorKind = BinaryOperatorKind.LessThanOrEqual
                Case SyntaxKind.CaseGreaterThanOrEqualClause : operatorKind = BinaryOperatorKind.GreaterThanOrEqual
                Case SyntaxKind.CaseLessThanClause : operatorKind = BinaryOperatorKind.LessThan
                Case SyntaxKind.CaseGreaterThanClause : operatorKind = BinaryOperatorKind.GreaterThan
                Case Else
                    Throw ExceptionUtilities.UnexpectedValue(node.Kind)
            End Select

            Dim conditionOpt As BoundExpression = Nothing
            Dim operandE1 As BoundExpression = BindCaseClauseExpression(
                expressionSyntax:=node.Value,
                caseClauseSyntax:=node,
                selectExpressionOpt:=selectExpressionOpt,
                operatorTokenKind:=node.OperatorToken.Kind,
                operatorKind:=operatorKind,
                convertCaseElements:=convertCaseElements,
                conditionOpt:=conditionOpt,
                diagnostics:=diagnostics)

            Return New BoundRelationalCaseClause(node, operatorKind, operandE1, conditionOpt)
        End Function

        Private Function BindSimpleCaseClause(
            node As SimpleCaseClauseSyntax,
            selectExpressionOpt As BoundRValuePlaceholder,
            convertCaseElements As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As BoundCaseClause
            ' SPEC:     The other form is an expression optionally followed by the keyword To and
            ' SPEC:     a second expression. Both expressions are converted to the type of the
            ' SPEC:     Select expression; if either expression is not implicitly convertible to
            ' SPEC:     the type of the Select expression, a compile-time error occurs.
            ' SPEC:     If the Select expression is E, the first Case expression is E1,
            ' SPEC:     and the second Case expression is E2, the Case is evaluated either
            ' SPEC:     as E = E1 (if no E2 is specified) or (E >= E1) And (E <= E2).
            ' SPEC:     The operators must be valid for the types of the two expressions;
            ' SPEC:     otherwise a compile-time error occurs.

            Dim conditionOpt As BoundExpression = Nothing

            ' Bind the Case clause as E = E1 (no E2 is specified)
            Dim value As BoundExpression = BindCaseClauseExpression(
                expressionSyntax:=node.Value,
                caseClauseSyntax:=node,
                selectExpressionOpt:=selectExpressionOpt,
                operatorTokenKind:=SyntaxKind.EqualsToken,
                operatorKind:=BinaryOperatorKind.Equals,
                convertCaseElements:=convertCaseElements,
                conditionOpt:=conditionOpt,
                diagnostics:=diagnostics)

            Return New BoundSimpleCaseClause(node, value, conditionOpt)
        End Function

        Private Function BindRangeCaseClause(
            node As RangeCaseClauseSyntax,
            selectExpressionOpt As BoundRValuePlaceholder,
            convertCaseElements As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As BoundCaseClause
            ' SPEC:     The other form is an expression optionally followed by the keyword To and
            ' SPEC:     a second expression. Both expressions are converted to the type of the
            ' SPEC:     Select expression; if either expression is not implicitly convertible to
            ' SPEC:     the type of the Select expression, a compile-time error occurs.
            ' SPEC:     If the Select expression is E, the first Case expression is E1,
            ' SPEC:     and the second Case expression is E2, the Case is evaluated either
            ' SPEC:     as E = E1 (if no E2 is specified) or (E >= E1) And (E <= E2).
            ' SPEC:     The operators must be valid for the types of the two expressions;
            ' SPEC:     otherwise a compile-time error occurs.

            Dim lowerBoundConditionOpt As BoundExpression = Nothing

            ' Bind case clause lower bound value (E >= E1)
            Dim lowerBound As BoundExpression = BindCaseClauseExpression(
                expressionSyntax:=node.LowerBound,
                caseClauseSyntax:=node,
                selectExpressionOpt:=selectExpressionOpt,
                operatorTokenKind:=SyntaxKind.GreaterThanEqualsToken,
                operatorKind:=BinaryOperatorKind.GreaterThanOrEqual,
                convertCaseElements:=convertCaseElements,
                conditionOpt:=lowerBoundConditionOpt,
                diagnostics:=diagnostics)

            ' Bind case clause upper bound value (E <= E2)
            Dim upperBoundConditionOpt As BoundExpression = Nothing
            Dim upperBound As BoundExpression = BindCaseClauseExpression(
                expressionSyntax:=node.UpperBound,
                caseClauseSyntax:=node,
                selectExpressionOpt:=selectExpressionOpt,
                operatorTokenKind:=SyntaxKind.LessThanEqualsToken,
                operatorKind:=BinaryOperatorKind.LessThanOrEqual,
                convertCaseElements:=convertCaseElements,
                conditionOpt:=upperBoundConditionOpt,
                diagnostics:=diagnostics)

            Return New BoundRangeCaseClause(node, lowerBound, upperBound, lowerBoundConditionOpt, upperBoundConditionOpt)
        End Function

        Private Function BindCaseClauseExpression(
            expressionSyntax As ExpressionSyntax,
            caseClauseSyntax As CaseClauseSyntax,
            selectExpressionOpt As BoundRValuePlaceholder,
            operatorTokenKind As SyntaxKind,
            operatorKind As BinaryOperatorKind,
            convertCaseElements As Boolean,
            ByRef conditionOpt As BoundExpression,
            diagnostics As BindingDiagnosticBag
        ) As BoundExpression

            Dim caseExpr As BoundExpression = BindValue(expressionSyntax, diagnostics)

            If selectExpressionOpt Is Nothing Then
                ' In error scenarios, such as a Case statement outside of a
                ' Select statement, the Select expression may be Nothing.
                conditionOpt = Nothing
                Return MakeRValue(caseExpr, diagnostics)
            End If

            If convertCaseElements AndAlso caseExpr.Type.IsIntrinsicOrEnumType() Then
                ' SPEC:     The expression is converted to the type of the Select expression;
                ' SPEC:     if the expression is not implicitly convertible to the type of the Select expression, a compile-time error occurs.

                Debug.Assert(selectExpressionOpt.Type IsNot Nothing)
                Return ApplyImplicitConversion(expressionSyntax, selectExpressionOpt.Type, caseExpr, diagnostics)

            Else
                ' SPEC:     If the Select expression is E, the comparison operator is Op,
                ' SPEC:     and the Case expression is E1, the case is evaluated as E OP E1.

                ' Bind binary operator "selectExpression OP caseExpr" to generate necessary diagnostics.
                conditionOpt = BindBinaryOperator(
                    node:=caseClauseSyntax,
                    left:=selectExpressionOpt,
                    right:=caseExpr,
                    operatorTokenKind:=operatorTokenKind,
                    preliminaryOperatorKind:=operatorKind,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True).MakeCompilerGenerated()

                Return Nothing
            End If
        End Function

#Region "Helper methods for Binding Select case statement"

        ' This function is identical to the Semantics::OptimizeSelectStatement in native compiler.
        ' It performs two primary tasks:
        ' 1) Determines what kind of byte codes will be used for select: Switch Table or If List.
        '    Ideally we would like to delay these kind of optimizations till rewriting/emit phase.
        '    However, the computation of the case statement conditional would be redundant if
        '    we are eventually going to emit Switch table based byte code. Hence we match the 
        '    native compiler behavior and check RecommendSwitchTable() here and store this
        '    value in the BoundSelectStatement node to be reused in the rewriter.
        ' 2) If we are going to generate If list based byte code, this function computes the 
        '    conditional expression for case statements and stores it in the BoundCaseStatement nodes.
        '    Condition for each case statement "Case clause1, clause2, ..., clauseN"
        '    is computed as "clause1 OrElse clause2 OrElse ... OrElse clauseN" expression.

        Private Function OptimizeSelectStatement(
            selectExpression As BoundRValuePlaceholder,
            caseBlockBuilder As ArrayBuilder(Of BoundCaseBlock),
            ByRef generateSwitchTable As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As ImmutableArray(Of BoundCaseBlock)
            Debug.Assert(Not selectExpression.HasErrors)

            generateSwitchTable = RecommendSwitchTable(selectExpression, caseBlockBuilder, diagnostics)

            ' CONSIDER: Do we need to compute the case statement conditional expression
            ' CONSIDER: even for generateSwitchTable case? We might want to do so to
            ' CONSIDER: maintain consistency of bound nodes coming out of the binder.
            ' CONSIDER: With the current design, value of BoundCaseStatement.ConditionOpt field 
            ' CONSIDER: is dependent on the value of generateSwitchTable.

            If Not generateSwitchTable AndAlso caseBlockBuilder.Any() Then
                Dim booleanType = GetSpecialType(SpecialType.System_Boolean, selectExpression.Syntax, diagnostics)
                Dim caseClauseBuilder = ArrayBuilder(Of BoundCaseClause).GetInstance()

                For index = 0 To caseBlockBuilder.Count - 1
                    Dim caseBlock = caseBlockBuilder(index)
                    If caseBlock.Syntax.Kind <> SyntaxKind.CaseElseBlock AndAlso
                        Not caseBlock.CaseStatement.Syntax.IsMissing Then

                        Dim caseStatement = caseBlock.CaseStatement
                        Dim caseStatementSyntax = caseStatement.Syntax
                        Dim caseStatementCondition As BoundExpression = Nothing

                        Debug.Assert(caseStatement.CaseClauses.Any())
                        Dim clausesChanged = False

                        ' Compute conditional expression for case statement
                        For Each caseClause In caseStatement.CaseClauses
                            Dim clauseCondition As BoundExpression = Nothing

                            ' Compute conditional expression for case clause, if not already computed.
                            Dim newCaseClause = ComputeCaseClauseCondition(caseClause, clauseCondition, selectExpression, diagnostics)
                            caseClauseBuilder.Add(newCaseClause)

                            clausesChanged = clausesChanged OrElse Not newCaseClause.Equals(caseClause)

                            Debug.Assert(clauseCondition IsNot Nothing)

                            If caseStatementCondition Is Nothing Then
                                caseStatementCondition = clauseCondition
                            Else
                                ' caseStatementCondition = caseStatementCondition OrElse clauseCondition
                                caseStatementCondition = BindBinaryOperator(
                                    node:=caseStatementSyntax,
                                    left:=caseStatementCondition,
                                    right:=clauseCondition,
                                    operatorTokenKind:=SyntaxKind.OrElseKeyword,
                                    preliminaryOperatorKind:=BinaryOperatorKind.OrElse,
                                    isOperandOfConditionalBranch:=False,
                                    diagnostics:=diagnostics,
                                    isSelectCase:=True).MakeCompilerGenerated()
                            End If
                        Next

                        Dim newCaseClauses As ImmutableArray(Of BoundCaseClause)
                        If clausesChanged Then
                            newCaseClauses = caseClauseBuilder.ToImmutable()
                        Else
                            newCaseClauses = caseStatement.CaseClauses
                        End If
                        caseClauseBuilder.Clear()

                        caseStatementCondition = ApplyImplicitConversion(caseStatementCondition.Syntax, booleanType, caseStatementCondition, diagnostics:=diagnostics, isOperandOfConditionalBranch:=True)

                        caseStatement = caseStatement.Update(newCaseClauses, caseStatementCondition)
                        caseBlockBuilder(index) = caseBlock.Update(caseStatement, caseBlock.Body)
                    End If
                Next

                caseClauseBuilder.Free()
            End If

            Return caseBlockBuilder.ToImmutableAndFree()
        End Function

        Private Function ComputeCaseClauseCondition(caseClause As BoundCaseClause, <Out()> ByRef conditionOpt As BoundExpression, selectExpression As BoundRValuePlaceholder, diagnostics As BindingDiagnosticBag) As BoundCaseClause
            Select Case caseClause.Kind
                Case BoundKind.RelationalCaseClause
                    Return ComputeRelationalCaseClauseCondition(DirectCast(caseClause, BoundRelationalCaseClause), conditionOpt, selectExpression, diagnostics)

                Case BoundKind.SimpleCaseClause
                    Return ComputeSimpleCaseClauseCondition(DirectCast(caseClause, BoundSimpleCaseClause), conditionOpt, selectExpression, diagnostics)

                Case BoundKind.RangeCaseClause
                    Return ComputeRangeCaseClauseCondition(DirectCast(caseClause, BoundRangeCaseClause), conditionOpt, selectExpression, diagnostics)

                Case Else
                    Throw ExceptionUtilities.UnexpectedValue(caseClause.Kind)
            End Select
        End Function

        Private Function BindTypeCaseClause(
            node As TypeCaseClauseSyntax,
            selectExpressionOpt As BoundRValuePlaceholder,
            convertCaseElements As Boolean,
            diagnostics As BindingDiagnosticBag
        ) As BoundCaseClause

            If selectExpressionOpt Is Nothing Then
                Dim missing = BadExpression(node, ErrorTypeSymbol.UnknownResultType).MakeCompilerGenerated()
                Return New BoundRelationalCaseClause(node, BinaryOperatorKind.Equals, missing, Nothing, hasErrors:=True)
            End If

            Dim targetSymbol = BindTypeOrAliasSyntax(node.Type, diagnostics)
            Dim targetType = DirectCast(If(TryCast(targetSymbol, TypeSymbol), DirectCast(targetSymbol, AliasSymbol).Target), TypeSymbol)
            Dim booleanType = GetSpecialType(SpecialType.System_Boolean, node, diagnostics)
            Dim operandType = selectExpressionOpt.Type
            Dim hasErrors = targetType.IsErrorType()

            If operandType IsNot Nothing AndAlso Not operandType.IsErrorType() AndAlso Not targetType.IsErrorType() Then
                If Not operandType.IsReferenceType AndAlso Not operandType.IsTypeParameter() Then
                    ReportDiagnostic(diagnostics, node.Type, ERRID.ERR_TypeOfRequiresReferenceType1, operandType)
                    hasErrors = True
                Else
                    Dim useSiteInfo = GetNewCompoundUseSiteInfo(diagnostics)
                    Dim convKind = Conversions.ClassifyTryCastConversion(operandType, targetType, useSiteInfo)
                    If Not diagnostics.Add(node, useSiteInfo) AndAlso Not Conversions.ConversionExists(convKind) Then
                        ReportDiagnostic(diagnostics, node, ERRID.ERR_TypeOfExprAlwaysFalse2, operandType, targetType)
                    End If
                End If
            End If

            Dim typeTest As BoundExpression = New BoundTypeOf(node, selectExpressionOpt, isTypeOfIsNotExpression:=False, targetType, booleanType, hasErrors:=hasErrors)
            Dim condition As BoundExpression = typeTest
            Dim typedReceiver As BoundExpression = Nothing

            If node.Arguments.Count > 0 Then
                typedReceiver = New BoundDirectCast(node, selectExpressionOpt, ConversionKind.NarrowingReference, targetType, hasErrors:=hasErrors OrElse targetType.IsErrorType())
                Dim positional = BindPositionalComparisons(node, typedReceiver, node.Arguments, diagnostics)
                condition = BindBinaryOperator(
                    node:=node,
                    left:=condition,
                    right:=positional,
                    operatorTokenKind:=SyntaxKind.AndAlsoKeyword,
                    preliminaryOperatorKind:=BinaryOperatorKind.AndAlso,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True).MakeCompilerGenerated()
                hasErrors = hasErrors OrElse condition.HasErrors
            End If

            For Each filter In node.Filters
                Dim filterCondition As BoundExpression
                If filter.Clause.Kind = SyntaxKind.PropertyCaseClause Then
                    If typedReceiver Is Nothing Then
                        typedReceiver = New BoundDirectCast(node, selectExpressionOpt, ConversionKind.NarrowingReference, targetType, hasErrors:=hasErrors OrElse targetType.IsErrorType())
                    End If

                    filterCondition = BindPropertyCaseFilter(DirectCast(filter.Clause, PropertyCaseClauseSyntax), typedReceiver, diagnostics)
                Else
                    Dim relational = DirectCast(BindRelationalCaseClause(DirectCast(filter.Clause, RelationalCaseClauseSyntax), selectExpressionOpt, convertCaseElements, diagnostics), BoundRelationalCaseClause)
                    Dim relationalCondition As BoundExpression = Nothing
                    ComputeRelationalCaseClauseCondition(relational, relationalCondition, selectExpressionOpt, diagnostics)
                    filterCondition = relationalCondition
                End If

                condition = BindBinaryOperator(
                    node:=filter,
                    left:=condition,
                    right:=filterCondition,
                    operatorTokenKind:=SyntaxKind.AndAlsoKeyword,
                    preliminaryOperatorKind:=BinaryOperatorKind.AndAlso,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True).MakeCompilerGenerated()
                hasErrors = hasErrors OrElse condition.HasErrors
            Next

            Return New BoundRelationalCaseClause(node, BinaryOperatorKind.Equals, valueOpt:=Nothing, conditionOpt:=condition, hasErrors:=hasErrors)
        End Function

        Private Function BindPropertyCaseFilter(node As PropertyCaseClauseSyntax, receiver As BoundExpression, diagnostics As BindingDiagnosticBag) As BoundExpression
            Dim memberName = node.Name.ValueText
            Dim result = LookupResult.GetInstance()
            Dim useSiteInfo = GetNewCompoundUseSiteInfo(diagnostics)
            LookupMember(result, receiver.Type, memberName, 0, LookupOptions.AllMethodsOfAnyArity, useSiteInfo)
            diagnostics.Add(node.Name, useSiteInfo)

            Dim member As BoundExpression = Nothing
            If result.IsGood AndAlso result.HasSingleSymbol Then
                Dim symbol = result.SingleSymbol
                If symbol.Kind = SymbolKind.Field Then
                    Dim field = DirectCast(symbol, FieldSymbol)
                    Dim memberReceiver = If(field.IsShared, Nothing, receiver)
                    member = New BoundFieldAccess(node, memberReceiver, field, isLValue:=False, type:=field.Type)
                ElseIf symbol.Kind = SymbolKind.Property Then
                    Dim [property] = DirectCast(symbol, PropertySymbol)
                    If [property].Parameters.Length = 0 Then
                        Dim memberReceiver = If([property].IsShared, Nothing, receiver)
                        member = New BoundPropertyAccess(node, [property], Nothing, PropertyAccessKind.Get, isWriteable:=False, memberReceiver, ImmutableArray(Of BoundExpression).Empty)
                    End If
                End If
            End If

            result.Free()

            If member Is Nothing Then
                ReportDiagnostic(diagnostics, node.Name, ERRID.ERR_NameNotDeclared1, memberName)
                member = BadExpression(node, ErrorTypeSymbol.UnknownResultType)
            End If

            Dim value = BindValue(node.Value, diagnostics)
            Dim operatorKind As BinaryOperatorKind
            Select Case node.OperatorToken.Kind
                Case SyntaxKind.EqualsToken : operatorKind = BinaryOperatorKind.Equals
                Case SyntaxKind.LessThanGreaterThanToken : operatorKind = BinaryOperatorKind.NotEquals
                Case SyntaxKind.LessThanToken : operatorKind = BinaryOperatorKind.LessThan
                Case SyntaxKind.LessThanEqualsToken : operatorKind = BinaryOperatorKind.LessThanOrEqual
                Case SyntaxKind.GreaterThanToken : operatorKind = BinaryOperatorKind.GreaterThan
                Case SyntaxKind.GreaterThanEqualsToken : operatorKind = BinaryOperatorKind.GreaterThanOrEqual
                Case Else : operatorKind = BinaryOperatorKind.Equals
            End Select

            Return BindBinaryOperator(
                node:=node,
                left:=member,
                right:=value,
                operatorTokenKind:=node.OperatorToken.Kind,
                preliminaryOperatorKind:=operatorKind,
                isOperandOfConditionalBranch:=False,
                diagnostics:=diagnostics,
                isSelectCase:=True)
        End Function

        Private Function BindListCaseClause(node As ListCaseClauseSyntax, selectExpressionOpt As BoundRValuePlaceholder, diagnostics As BindingDiagnosticBag) As BoundCaseClause
            If selectExpressionOpt Is Nothing Then
                Dim missing = BadExpression(node, ErrorTypeSymbol.UnknownResultType).MakeCompilerGenerated()
                Return New BoundRelationalCaseClause(node, BinaryOperatorKind.Equals, missing, Nothing, hasErrors:=True)
            End If

            Dim condition = BindListComparisons(node, selectExpressionOpt, diagnostics)
            Return New BoundRelationalCaseClause(node, BinaryOperatorKind.Equals, valueOpt:=Nothing, conditionOpt:=condition, hasErrors:=condition.HasErrors)
        End Function

        Private Function BindListComparisons(node As ListCaseClauseSyntax, receiver As BoundExpression, diagnostics As BindingDiagnosticBag) As BoundExpression
            Dim booleanType = GetSpecialType(SpecialType.System_Boolean, node, diagnostics)
            Dim arrayType = TryCast(receiver.Type, ArrayTypeSymbol)
            If arrayType Is Nothing OrElse arrayType.Rank <> 1 Then
                Dim collection = TryBindCollectionListComparisons(node, receiver, booleanType, diagnostics)
                If collection IsNot Nothing Then
                    Return collection
                End If

                If Not receiver.HasErrors Then
                    ReportDiagnostic(diagnostics, node, ERRID.ERR_ListPatternRequiresArray)
                End If

                Dim int32ForError = GetSpecialType(SpecialType.System_Int32, node, diagnostics)
                Dim missingLength = BadExpression(node, int32ForError).MakeCompilerGenerated()
                Return New BoundBinaryOperator(node, BinaryOperatorKind.Equals, missingLength, New BoundLiteral(node, ConstantValue.Create(0), int32ForError), checked:=False, constantValueOpt:=Nothing, booleanType, hasErrors:=True)
            End If

            Dim int32Type = GetSpecialType(SpecialType.System_Int32, node, diagnostics)
            Dim length = New BoundArrayLength(node, CopyArrayReceiver(receiver), int32Type)
            Dim expectedLength = New BoundLiteral(node, ConstantValue.Create(node.Arguments.Count), int32Type)
            Dim condition = BindBinaryOperator(
                node:=node,
                left:=length,
                right:=expectedLength,
                operatorTokenKind:=SyntaxKind.EqualsToken,
                preliminaryOperatorKind:=BinaryOperatorKind.Equals,
                isOperandOfConditionalBranch:=False,
                diagnostics:=diagnostics,
                isSelectCase:=True)

            For i = 0 To node.Arguments.Count - 1
                Dim elementSyntax = node.Arguments(i)
                Dim expected = BindValue(elementSyntax, diagnostics)
                Dim index = New BoundLiteral(elementSyntax, ConstantValue.Create(i), int32Type)
                Dim actual = New BoundArrayAccess(
                    elementSyntax,
                    CopyArrayReceiver(receiver),
                    ImmutableArray.Create(Of BoundExpression)(index),
                    isLValue:=False,
                    type:=arrayType.ElementType)
                Dim compare = BindBinaryOperator(
                    node:=elementSyntax,
                    left:=actual,
                    right:=expected,
                    operatorTokenKind:=SyntaxKind.EqualsToken,
                    preliminaryOperatorKind:=BinaryOperatorKind.Equals,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True)
                condition = BindBinaryOperator(
                    node:=node,
                    left:=condition,
                    right:=compare,
                    operatorTokenKind:=SyntaxKind.AndAlsoKeyword,
                    preliminaryOperatorKind:=BinaryOperatorKind.AndAlso,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True).MakeCompilerGenerated()
            Next

            Return condition
        End Function

        Private Function TryBindCollectionListComparisons(node As ListCaseClauseSyntax, receiver As BoundExpression, booleanType As TypeSymbol, diagnostics As BindingDiagnosticBag) As BoundExpression
            Dim count = TryBindIntegerMember(node, receiver, "Count", diagnostics)
            If count Is Nothing Then
                Return Nothing
            End If

            Dim group = TryCast(BindDefaultPropertyGroup(node, receiver, diagnostics), BoundMethodOrPropertyGroup)
            If group Is Nothing Then
                Return Nothing
            End If

            Dim int32Type = GetSpecialType(SpecialType.System_Int32, node, diagnostics)
            Dim expectedLength = New BoundLiteral(node, ConstantValue.Create(node.Arguments.Count), int32Type)
            Dim condition = BindBinaryOperator(
                node:=node,
                left:=count,
                right:=expectedLength,
                operatorTokenKind:=SyntaxKind.EqualsToken,
                preliminaryOperatorKind:=BinaryOperatorKind.Equals,
                isOperandOfConditionalBranch:=False,
                diagnostics:=diagnostics,
                isSelectCase:=True)

            For i = 0 To node.Arguments.Count - 1
                Dim elementSyntax = node.Arguments(i)
                Dim index = New BoundLiteral(elementSyntax, ConstantValue.Create(i), int32Type)
                Dim actual = BindInvocationExpression(
                    node:=elementSyntax,
                    target:=elementSyntax,
                    typeChar:=TypeCharacter.None,
                    group:=group,
                    boundArguments:=ImmutableArray.Create(Of BoundExpression)(index),
                    argumentNames:=Nothing,
                    diagnostics:=diagnostics,
                    callerInfoOpt:=elementSyntax,
                    isDefaultMemberAccess:=True)
                Dim expected = BindValue(elementSyntax, diagnostics)
                Dim compare = BindBinaryOperator(
                    node:=elementSyntax,
                    left:=actual,
                    right:=expected,
                    operatorTokenKind:=SyntaxKind.EqualsToken,
                    preliminaryOperatorKind:=BinaryOperatorKind.Equals,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True)
                condition = BindBinaryOperator(
                    node:=node,
                    left:=condition,
                    right:=compare,
                    operatorTokenKind:=SyntaxKind.AndAlsoKeyword,
                    preliminaryOperatorKind:=BinaryOperatorKind.AndAlso,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True).MakeCompilerGenerated()
            Next

            Return condition
        End Function

        Private Function TryBindIntegerMember(syntax As VisualBasicSyntaxNode, receiver As BoundExpression, name As String, diagnostics As BindingDiagnosticBag) As BoundExpression
            If receiver.Type Is Nothing OrElse receiver.Type.IsErrorType() Then
                Return Nothing
            End If

            Dim result = LookupResult.GetInstance()
            Dim useSiteInfo = GetNewCompoundUseSiteInfo(diagnostics)
            LookupMember(result, receiver.Type, name, 0, LookupOptions.AllMethodsOfAnyArity, useSiteInfo)
            diagnostics.Add(syntax, useSiteInfo)

            Dim member As BoundExpression = Nothing
            If result.IsGood AndAlso result.HasSingleSymbol AndAlso result.SingleSymbol.Kind = SymbolKind.Property Then
                Dim [property] = DirectCast(result.SingleSymbol, PropertySymbol)
                If [property].Parameters.Length = 0 AndAlso [property].Type.SpecialType = SpecialType.System_Int32 Then
                    Dim memberReceiver = If([property].IsShared, Nothing, receiver)
                    member = New BoundPropertyAccess(syntax, [property], Nothing, PropertyAccessKind.Get, isWriteable:=False, memberReceiver, ImmutableArray(Of BoundExpression).Empty)
                End If
            End If

            result.Free()
            Return member
        End Function

        Private Function BindPositionalCaseClause(node As PositionalCaseClauseSyntax, selectExpressionOpt As BoundRValuePlaceholder, diagnostics As BindingDiagnosticBag) As BoundCaseClause
            If selectExpressionOpt Is Nothing Then
                Dim missing = BadExpression(node, ErrorTypeSymbol.UnknownResultType).MakeCompilerGenerated()
                Return New BoundRelationalCaseClause(node, BinaryOperatorKind.Equals, missing, Nothing, hasErrors:=True)
            End If

            Dim condition = BindPositionalComparisons(node, selectExpressionOpt, node.Arguments, diagnostics)
            Return New BoundRelationalCaseClause(node, BinaryOperatorKind.Equals, valueOpt:=Nothing, conditionOpt:=condition, hasErrors:=condition.HasErrors)
        End Function

        Private Function BindPositionalComparisons(syntax As SyntaxNode, receiver As BoundExpression, arguments As SeparatedSyntaxList(Of ExpressionSyntax), diagnostics As BindingDiagnosticBag) As BoundExpression
            Dim booleanType = GetSpecialType(SpecialType.System_Boolean, syntax, diagnostics)
            Dim elements = GetDeconstructionElements(receiver, arguments.Count)
            If Not elements.IsDefault Then
                Return ComparePositionalValues(syntax, arguments, diagnostics, booleanType, Function(index) New BoundFieldAccess(syntax, receiver, elements(index), isLValue:=False, type:=elements(index).Type))
            End If

            Dim method = FindDeconstructMethod(receiver, arguments.Count, diagnostics, syntax)
            If method Is Nothing Then
                If Not receiver.HasErrors Then
                    ReportDiagnostic(diagnostics, syntax, ERRID.ERR_DeconstructionRequiresTuple, arguments.Count)
                End If

                Return BadExpression(syntax, booleanType).MakeCompilerGenerated()
            End If

            Dim locals = ArrayBuilder(Of LocalSymbol).GetInstance()
            Dim argumentsBuilder = ArrayBuilder(Of BoundExpression).GetInstance(arguments.Count)
            For i = 0 To arguments.Count - 1
                Dim parameterType = method.Parameters(i).Type
                Dim slot = New SynthesizedLocal(ContainingMember, parameterType, SynthesizedLocalKind.LoweringTemp)
                locals.Add(slot)
                argumentsBuilder.Add(New BoundLocal(syntax, slot, parameterType))
            Next

            Dim callReceiver = receiver
            If Not receiver.Type.IsReferenceType Then
                callReceiver = receiver
            Else
                callReceiver = receiver.MakeRValue()
            End If

            Dim invocation = New BoundCall(syntax, method, Nothing, callReceiver, argumentsBuilder.ToImmutableAndFree(), Nothing, method.ReturnType)
            Dim comparisons = ComparePositionalValues(syntax, arguments, diagnostics, booleanType, Function(index) New BoundLocal(syntax, locals(index), locals(index).Type).MakeRValue())
            Dim sideEffects = ImmutableArray.Create(Of BoundExpression)(invocation)
            Return New BoundSequence(syntax, locals.ToImmutableAndFree(), sideEffects, comparisons, booleanType)
        End Function

        Private Function ComparePositionalValues(syntax As SyntaxNode, arguments As SeparatedSyntaxList(Of ExpressionSyntax), diagnostics As BindingDiagnosticBag, booleanType As TypeSymbol, readElement As Func(Of Integer, BoundExpression)) As BoundExpression
            Dim condition As BoundExpression = Nothing
            For i = 0 To arguments.Count - 1
                Dim expected = BindValue(arguments(i), diagnostics)
                Dim actual = readElement(i)
                Dim compare = BindBinaryOperator(
                    node:=arguments(i),
                    left:=actual,
                    right:=expected,
                    operatorTokenKind:=SyntaxKind.EqualsToken,
                    preliminaryOperatorKind:=BinaryOperatorKind.Equals,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True)
                If condition Is Nothing Then
                    condition = compare
                Else
                    condition = BindBinaryOperator(
                        node:=syntax,
                        left:=condition,
                        right:=compare,
                        operatorTokenKind:=SyntaxKind.AndAlsoKeyword,
                        preliminaryOperatorKind:=BinaryOperatorKind.AndAlso,
                        isOperandOfConditionalBranch:=False,
                        diagnostics:=diagnostics,
                        isSelectCase:=True).MakeCompilerGenerated()
                End If
            Next

            If condition Is Nothing Then
                Return New BoundLiteral(syntax, ConstantValue.True, booleanType)
            End If

            Return condition
        End Function

        Private Function ComputeRelationalCaseClauseCondition(boundClause As BoundRelationalCaseClause, <Out()> ByRef conditionOpt As BoundExpression, selectExpression As BoundRValuePlaceholder, diagnostics As BindingDiagnosticBag) As BoundCaseClause
            Debug.Assert(boundClause.ConditionOpt IsNot Nothing Xor boundClause.ValueOpt IsNot Nothing)

            If boundClause.ConditionOpt IsNot Nothing Then
                conditionOpt = boundClause.ConditionOpt
                Return boundClause
            End If

            Dim syntax = DirectCast(boundClause.Syntax, RelationalCaseClauseSyntax)

            ' Exactly one of the operand or condition must be non-null
            Debug.Assert(boundClause.ConditionOpt IsNot Nothing Xor boundClause.ValueOpt IsNot Nothing)

            conditionOpt = If(boundClause.ConditionOpt, BindBinaryOperator(node:=syntax,
                                                                           left:=selectExpression,
                                                                           right:=boundClause.ValueOpt,
                                                                           operatorTokenKind:=syntax.OperatorToken.Kind,
                                                                           preliminaryOperatorKind:=boundClause.OperatorKind,
                                                                           isOperandOfConditionalBranch:=False,
                                                                           diagnostics:=diagnostics,
                                                                           isSelectCase:=True).MakeCompilerGenerated())

            Return boundClause.Update(boundClause.OperatorKind, valueOpt:=Nothing, conditionOpt:=conditionOpt)
        End Function

        Private Function ComputeSimpleCaseClauseCondition(boundClause As BoundSimpleCaseClause, <Out()> ByRef conditionOpt As BoundExpression, selectExpression As BoundRValuePlaceholder, diagnostics As BindingDiagnosticBag) As BoundCaseClause
            ' Exactly one of the value or condition must be non-null
            Debug.Assert(boundClause.ConditionOpt IsNot Nothing Xor boundClause.ValueOpt IsNot Nothing)

            conditionOpt = If(boundClause.ConditionOpt, BindBinaryOperator(node:=boundClause.Syntax,
                                                                           left:=selectExpression,
                                                                           right:=boundClause.ValueOpt,
                                                                           operatorTokenKind:=SyntaxKind.EqualsToken,
                                                                           preliminaryOperatorKind:=BinaryOperatorKind.Equals,
                                                                           isOperandOfConditionalBranch:=False,
                                                                           diagnostics:=diagnostics,
                                                                           isSelectCase:=True).MakeCompilerGenerated())

            Return boundClause.Update(valueOpt:=Nothing, conditionOpt:=conditionOpt)
        End Function

        Private Function ComputeRangeCaseClauseCondition(boundClause As BoundRangeCaseClause, <Out()> ByRef conditionOpt As BoundExpression, selectExpression As BoundRValuePlaceholder, diagnostics As BindingDiagnosticBag) As BoundCaseClause
            Dim syntax = boundClause.Syntax

            ' Exactly one of the LowerBoundOpt or LowerBoundConditionOpt must be non-null
            Debug.Assert(boundClause.LowerBoundOpt IsNot Nothing Xor boundClause.LowerBoundConditionOpt IsNot Nothing)

            Dim lowerBoundConditionOpt = boundClause.LowerBoundConditionOpt
            If lowerBoundConditionOpt Is Nothing Then
                lowerBoundConditionOpt = BindBinaryOperator(
                    node:=boundClause.Syntax,
                    left:=selectExpression,
                    right:=boundClause.LowerBoundOpt,
                    operatorTokenKind:=SyntaxKind.GreaterThanEqualsToken,
                    preliminaryOperatorKind:=BinaryOperatorKind.GreaterThanOrEqual,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True).MakeCompilerGenerated()
            End If

            ' Exactly one of the UpperBoundOpt or UpperBoundConditionOpt must be non-null
            Debug.Assert(boundClause.UpperBoundOpt IsNot Nothing Xor boundClause.UpperBoundConditionOpt IsNot Nothing)

            Dim upperBoundConditionOpt = boundClause.UpperBoundConditionOpt
            If upperBoundConditionOpt Is Nothing Then
                upperBoundConditionOpt = BindBinaryOperator(
                    node:=syntax,
                    left:=selectExpression,
                    right:=boundClause.UpperBoundOpt,
                    operatorTokenKind:=SyntaxKind.LessThanEqualsToken,
                    preliminaryOperatorKind:=BinaryOperatorKind.LessThanOrEqual,
                    isOperandOfConditionalBranch:=False,
                    diagnostics:=diagnostics,
                    isSelectCase:=True).MakeCompilerGenerated()
            End If

            conditionOpt = BindBinaryOperator(
                node:=syntax,
                left:=lowerBoundConditionOpt,
                right:=upperBoundConditionOpt,
                operatorTokenKind:=SyntaxKind.AndAlsoKeyword,
                preliminaryOperatorKind:=BinaryOperatorKind.AndAlso,
                isOperandOfConditionalBranch:=False,
                diagnostics:=diagnostics,
                isSelectCase:=True).MakeCompilerGenerated()

            Return boundClause.Update(lowerBoundOpt:=Nothing, upperBoundOpt:=Nothing, lowerBoundConditionOpt:=lowerBoundConditionOpt, upperBoundConditionOpt:=upperBoundConditionOpt)
        End Function

        ' Helper method to determine if we must rewrite the select case statement as an IF list or a SWITCH table
        Private Function RecommendSwitchTable(selectExpr As BoundRValuePlaceholder, caseBlocks As ArrayBuilder(Of BoundCaseBlock), diagnostics As BindingDiagnosticBag) As Boolean
            ' We can rewrite select case statement as an IF list or a SWITCH table
            ' This function determines which method to use.
            ' The conditions for choosing the SWITCH table are:
            '   select case expression type must be integral/boolean/string (see TypeSymbolExtensions.IsValidTypeForSwitchTable())
            '   no "Is <relop>" cases, except for <relop> = BinaryOperatorKind.Equals
            '   no "<lb> To <ub>" cases for string type
            '   for integral/boolean type, case values must be (or expand to, as in ranges) integer constants
            '   for string type, all case values must be string constants and OptionCompareText must be False.
            '   beneficial over IF lists (as per a threshold on size ratio)
            '   ranges must have lower bound first

            ' We also generate warnings for Invalid range clauses in this function.
            ' Ideally we would like to generate them in BindRangeCaseClause.
            ' However, Dev10 doesn't do this check individually for each CaseClause.
            ' It is performed only if bounds for all clauses in the Select are integer constants and
            ' all clauses are either range clauses or equality clause.
            ' Doing this differently will produce warnings in more scenarios - breaking change.

            If Not caseBlocks.Any() OrElse Not selectExpr.Type.IsValidTypeForSwitchTable() Then
                Return False
            End If

            Dim isSelectExprStringType = selectExpr.Type.IsStringType
            If isSelectExprStringType AndAlso OptionCompareText Then
                Return False
            End If

            Dim recommendSwitch = True

            For Each caseBlock In caseBlocks
                For Each caseClause In caseBlock.CaseStatement.CaseClauses
                    Select Case caseClause.Kind
                        Case BoundKind.RelationalCaseClause
                            Dim relationalClause = DirectCast(caseClause, BoundRelationalCaseClause)

                            ' Exactly one of the operand or condition must be non-null
                            Debug.Assert(relationalClause.ValueOpt IsNot Nothing Xor relationalClause.ConditionOpt IsNot Nothing)

                            Dim operand = relationalClause.ValueOpt

                            If operand Is Nothing OrElse
                                relationalClause.OperatorKind <> BinaryOperatorKind.Equals OrElse
                                operand.ConstantValueOpt Is Nothing OrElse
                                Not SwitchConstantValueHelper.IsValidSwitchCaseLabelConstant(operand.ConstantValueOpt) Then

                                Return False
                            End If

                        Case BoundKind.RangeCaseClause
                            ' TODO: RecommendSwitchTable for Range clause.
                            ' TODO: If we decide to implement it we will need to
                            ' TODO: add heuristic to determine when the range is
                            ' TODO: big enough to prefer IF lists over SWITCH table.
                            ' TODO: We will also need to add logic in the emitter
                            ' TODO: to iterate through ConstantValues in a range.

                            ' For now we use IF lists if we encounter BoundRangeCaseClause
                            Dim rangeCaseClause = DirectCast(caseClause, BoundRangeCaseClause)

                            ' Exactly one of the LowerBoundOpt or LowerBoundConditionOpt must be non-null
                            Debug.Assert(rangeCaseClause.LowerBoundOpt IsNot Nothing Xor rangeCaseClause.LowerBoundConditionOpt IsNot Nothing)

                            ' Exactly one of the UpperBoundOpt or UpperBoundConditionOpt must be non-null
                            Debug.Assert(rangeCaseClause.UpperBoundOpt IsNot Nothing Xor rangeCaseClause.UpperBoundConditionOpt IsNot Nothing)

                            Dim lowerBound = rangeCaseClause.LowerBoundOpt
                            Dim upperBound = rangeCaseClause.UpperBoundOpt

                            If lowerBound Is Nothing OrElse
                                upperBound Is Nothing OrElse
                                lowerBound.ConstantValueOpt Is Nothing OrElse
                                upperBound.ConstantValueOpt Is Nothing OrElse
                                Not SwitchConstantValueHelper.IsValidSwitchCaseLabelConstant(lowerBound.ConstantValueOpt) OrElse
                                Not SwitchConstantValueHelper.IsValidSwitchCaseLabelConstant(upperBound.ConstantValueOpt) Then

                                Return False
                            End If

                            recommendSwitch = False

                        Case Else
                            Dim simpleCaseClause = DirectCast(caseClause, BoundSimpleCaseClause)

                            ' Exactly one of the value or condition must be non-null
                            Debug.Assert(simpleCaseClause.ValueOpt IsNot Nothing Xor simpleCaseClause.ConditionOpt IsNot Nothing)

                            Dim value = simpleCaseClause.ValueOpt

                            If value Is Nothing OrElse
                                value.ConstantValueOpt Is Nothing OrElse
                                Not SwitchConstantValueHelper.IsValidSwitchCaseLabelConstant(value.ConstantValueOpt) Then

                                Return False
                            End If
                    End Select
                Next
            Next

            ' TODO: beneficial over IF lists (as per a threshold on size ratio)

            Return Not ReportInvalidSelectCaseRange(caseBlocks, diagnostics) AndAlso recommendSwitch
        End Function

        ' Function reports WRN_SelectCaseInvalidRange for any invalid select case range.
        ' Returns True if an invalid range was found, False otherwise.
        Private Function ReportInvalidSelectCaseRange(caseBlocks As ArrayBuilder(Of BoundCaseBlock), diagnostics As BindingDiagnosticBag) As Boolean
            For Each caseBlock In caseBlocks
                For Each caseClause In caseBlock.CaseStatement.CaseClauses
                    Select Case caseClause.Kind
                        Case BoundKind.RangeCaseClause
                            Dim rangeCaseClause = DirectCast(caseClause, BoundRangeCaseClause)

                            Dim lowerBound = rangeCaseClause.LowerBoundOpt
                            Dim upperBound = rangeCaseClause.UpperBoundOpt

                            Debug.Assert(lowerBound IsNot Nothing)
                            Debug.Assert(lowerBound.ConstantValueOpt IsNot Nothing)
                            Debug.Assert(upperBound IsNot Nothing)
                            Debug.Assert(upperBound.ConstantValueOpt IsNot Nothing)
                            Debug.Assert(rangeCaseClause.LowerBoundConditionOpt Is Nothing)
                            Debug.Assert(rangeCaseClause.UpperBoundConditionOpt Is Nothing)

                            If IsInvalidSelectCaseRange(lowerBound.ConstantValueOpt, upperBound.ConstantValueOpt) Then
                                ReportDiagnostic(diagnostics, rangeCaseClause.Syntax, ERRID.WRN_SelectCaseInvalidRange)
                                Return True
                            End If
                    End Select
                Next
            Next

            Return False
        End Function

        Private Shared Function IsInvalidSelectCaseRange(lbConstantValue As ConstantValue, ubConstantValue As ConstantValue) As Boolean
            Debug.Assert(lbConstantValue IsNot Nothing)
            Debug.Assert(ubConstantValue IsNot Nothing)

            Debug.Assert(lbConstantValue.SpecialType = ubConstantValue.SpecialType)

            Select Case lbConstantValue.SpecialType
                Case SpecialType.System_Boolean,
                     SpecialType.System_Byte,
                     SpecialType.System_UInt16,
                     SpecialType.System_UInt32,
                     SpecialType.System_UInt64
                    Return lbConstantValue.UInt64Value > ubConstantValue.UInt64Value

                Case SpecialType.System_SByte,
                     SpecialType.System_Int16,
                     SpecialType.System_Int32,
                     SpecialType.System_Int64,
                     SpecialType.System_Char
                    Return lbConstantValue.Int64Value > ubConstantValue.Int64Value

                Case SpecialType.System_Single,
                     SpecialType.System_Double
                    Return lbConstantValue.DoubleValue > ubConstantValue.DoubleValue

                Case SpecialType.System_Decimal
                    Return lbConstantValue.DecimalValue > ubConstantValue.DecimalValue

                Case Else
                    Return False
            End Select
        End Function

#End Region

#End Region
    End Class

End Namespace
