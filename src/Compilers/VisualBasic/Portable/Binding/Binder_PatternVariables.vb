' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend Class Binder

        ''' <summary>
        ''' The local that an Into name declares, checked for clashes with the function name and with the locals
        ''' and parameters of enclosing blocks. Reports an error and returns Nothing when no enclosing binder
        ''' declares the name, because Into is not allowed where it appears.
        ''' </summary>
        Private Function BindPatternVariable(identifier As SyntaxToken, diagnostics As BindingDiagnosticBag) As LocalSymbol
            Dim binder As Binder = Me
            While binder IsNot Nothing
                Dim patternBinder = TryCast(binder, PatternVariableBinder)
                Dim local = patternBinder?.GetLocal(identifier)
                If local IsNot Nothing Then
                    Dim functionValue = GetLocalForFunctionValue()
                    If functionValue IsNot Nothing AndAlso CaseInsensitiveComparison.Equals(local.Name, functionValue.Name) Then
                        ReportDiagnostic(diagnostics, identifier, ERRID.ERR_LocalSameAsFunc)
                    ElseIf patternBinder.Locals.Any(Function(other) other IsNot local AndAlso
                                                                     CaseInsensitiveComparison.Equals(other.Name, local.Name) AndAlso
                                                                     other.IdentifierToken.SpanStart < identifier.SpanStart) Then
                        ReportDiagnostic(diagnostics, identifier, ERRID.ERR_DuplicateLocals1, local.Name)
                    Else
                        patternBinder.ContainingBinder?.VerifyNameShadowingInMethodBody(local, identifier, identifier, diagnostics)
                    End If

                    Return local
                End If

                binder = binder.ContainingBinder
            End While

            ReportDiagnostic(diagnostics, identifier, ERRID.ERR_PatternVariableNotAllowedHere)
            Return Nothing
        End Function

        ''' <summary>
        ''' An If or ElseIf condition, bound with the binder that declares its Into names.
        ''' </summary>
        Private Function BindIfCondition(condition As ExpressionSyntax, diagnostics As BindingDiagnosticBag) As BoundExpression
            Dim conditionBinder = If(GetBinder(DirectCast(condition, VisualBasicSyntaxNode)), Me)
            Return conditionBinder.BindBooleanExpression(condition, diagnostics)
        End Function

        ''' <summary>
        ''' TypeOf value Is T Into name: the type test, which also assigns the local when it succeeds.
        ''' </summary>
        Private Function BindTypeOfIntoExpression(node As TypeOfIntoExpressionSyntax, diagnostics As BindingDiagnosticBag) As BoundExpression
            Dim test = BindExpression(node.TypeOfExpression, diagnostics)

            Dim local As LocalSymbol = Nothing
            If node.Identifier.IsMissing Then
                Return BadTypeOfInto(node, test)
            ElseIf node.TypeOfExpression.Kind = SyntaxKind.TypeOfIsNotExpression Then
                Return BindGuardTypeOfInto(node, test, diagnostics)
            ElseIf node.TypeOfExpression.Kind <> SyntaxKind.TypeOfIsExpression Then
                ' A list of types has no single type for the name.
                ReportDiagnostic(diagnostics, node.Identifier, ERRID.ERR_PatternVariableNotAllowedHere)
                Return BadTypeOfInto(node, test)
            Else
                local = BindPatternVariable(node.Identifier, diagnostics)
            End If

            Return MakeTypeOfDeclaration(node, test, local, diagnostics)
        End Function

        ''' <summary>
        ''' The successful type test that assigns local, or a bad expression when the test cannot assign it.
        ''' </summary>
        Private Function MakeTypeOfDeclaration(node As TypeOfIntoExpressionSyntax, test As BoundExpression, local As LocalSymbol, diagnostics As BindingDiagnosticBag) As BoundExpression
            If local Is Nothing OrElse test.Kind <> BoundKind.TypeOf OrElse test.HasErrors Then
                Return BadTypeOfInto(node, test)
            End If

            Dim typeTest = DirectCast(test, BoundTypeOf)
            Dim conversion = ConversionKind.NarrowingReference
            If typeTest.Operand.Type IsNot Nothing Then
                Dim useSiteInfo = GetNewCompoundUseSiteInfo(diagnostics)
                Dim classified = Conversions.ClassifyDirectCastConversion(typeTest.Operand.Type, local.Type, useSiteInfo)
                diagnostics.Add(node, useSiteInfo)
                If Conversions.ConversionExists(classified) Then
                    conversion = classified
                End If
            End If

            Return New BoundTypeOfDeclaration(node, typeTest.Operand, local, conversion, typeTest.Type)
        End Function

        ''' <summary>
        ''' TypeOf value IsNot T Into name as the whole condition of a guard If: Not the type test, whose name the
        ''' statements after the If see. The Then part must leave, and the If has no Else or ElseIf.
        ''' </summary>
        Private Function BindGuardTypeOfInto(node As TypeOfIntoExpressionSyntax, test As BoundExpression, diagnostics As BindingDiagnosticBag) As BoundExpression
            Dim guardStatement = GetGuardStatement(node)
            Dim guardBinder = If(guardStatement IsNot Nothing, FindGuardBinder(guardStatement), Nothing)
            If guardBinder Is Nothing Then
                ReportDiagnostic(diagnostics, node.Identifier, ERRID.ERR_PatternVariableNotAllowedHere)
                Return BadTypeOfInto(node, test)
            End If

            Dim local = guardBinder.BindPatternVariable(node.Identifier, diagnostics)
            Dim name = node.Identifier.ValueText

            Dim thenStatements As SyntaxList(Of StatementSyntax)
            Dim hasElse As Boolean
            If guardStatement.Kind = SyntaxKind.SingleLineIfStatement Then
                Dim singleLine = DirectCast(guardStatement, SingleLineIfStatementSyntax)
                thenStatements = singleLine.Statements
                hasElse = singleLine.ElseClause IsNot Nothing
            Else
                Dim multiLine = DirectCast(guardStatement, MultiLineIfBlockSyntax)
                thenStatements = multiLine.Statements
                hasElse = multiLine.ElseIfBlocks.Count > 0 OrElse multiLine.ElseBlock IsNot Nothing
            End If

            If hasElse Then
                ReportDiagnostic(diagnostics, node.Identifier, ERRID.ERR_GuardCannotHaveElse, name)
            ElseIf thenStatements.Count = 0 OrElse Not LeavesTheBlock(thenStatements.Last()) Then
                ReportDiagnostic(diagnostics, node.Identifier, ERRID.ERR_GuardThenMustLeave, name)
            End If

            Dim declaration = MakeTypeOfDeclaration(node, test, local, diagnostics)
            If declaration.HasErrors Then
                Return declaration
            End If

            Return New BoundUnaryOperator(node, UnaryOperatorKind.Not, declaration, checked:=False, constantValueOpt:=Nothing, type:=declaration.Type).MakeCompilerGenerated()
        End Function

        ''' <summary>
        ''' The If statement whose whole condition, through parentheses, is the given test.
        ''' </summary>
        Private Shared Function GetGuardStatement(node As TypeOfIntoExpressionSyntax) As StatementSyntax
            Dim current As SyntaxNode = node
            While current.Parent IsNot Nothing AndAlso current.Parent.Kind = SyntaxKind.ParenthesizedExpression
                current = current.Parent
            End While

            Dim parent = current.Parent
            If parent Is Nothing Then
                Return Nothing
            End If

            If parent.Kind = SyntaxKind.SingleLineIfStatement AndAlso DirectCast(parent, SingleLineIfStatementSyntax).Condition Is current Then
                Return DirectCast(parent, StatementSyntax)
            End If

            If parent.Kind = SyntaxKind.IfStatement AndAlso DirectCast(parent, IfStatementSyntax).Condition Is current AndAlso
               parent.Parent IsNot Nothing AndAlso parent.Parent.Kind = SyntaxKind.MultiLineIfBlock Then
                Return DirectCast(parent.Parent, StatementSyntax)
            End If

            Return Nothing
        End Function

        ''' <summary>
        ''' The binder that the statement list holding a guard If made for the statements after it.
        ''' </summary>
        Private Function FindGuardBinder(guardStatement As StatementSyntax) As PatternVariableBinder
            Dim binder As Binder = Me
            While binder IsNot Nothing
                Dim listBinder = TryCast(binder, StatementListBinder)
                Dim guardBinder As PatternVariableBinder = Nothing
                If listBinder IsNot Nothing AndAlso listBinder.GuardBinders.TryGetValue(guardStatement, guardBinder) Then
                    Return guardBinder
                End If

                binder = binder.ContainingBinder
            End While

            Return Nothing
        End Function

        ''' <summary>
        ''' Whether a statement always leaves the statements that follow it in its block.
        ''' </summary>
        Private Shared Function LeavesTheBlock(statement As StatementSyntax) As Boolean
            Select Case statement.Kind
                Case SyntaxKind.ReturnStatement,
                     SyntaxKind.ThrowStatement,
                     SyntaxKind.GoToStatement,
                     SyntaxKind.ExitDoStatement,
                     SyntaxKind.ExitForStatement,
                     SyntaxKind.ExitSubStatement,
                     SyntaxKind.ExitFunctionStatement,
                     SyntaxKind.ExitOperatorStatement,
                     SyntaxKind.ExitPropertyStatement,
                     SyntaxKind.ExitTryStatement,
                     SyntaxKind.ExitSelectStatement,
                     SyntaxKind.ExitWhileStatement,
                     SyntaxKind.ContinueWhileStatement,
                     SyntaxKind.ContinueDoStatement,
                     SyntaxKind.ContinueForStatement
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Shared Function BadTypeOfInto(node As TypeOfIntoExpressionSyntax, test As BoundExpression) As BoundExpression
            Return New BoundBadExpression(node, LookupResultKind.Empty, ImmutableArray(Of Symbol).Empty, ImmutableArray.Create(test), test.Type, hasErrors:=True)
        End Function
    End Class
End Namespace
