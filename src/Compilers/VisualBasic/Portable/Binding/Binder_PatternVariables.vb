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
            ElseIf node.TypeOfExpression.Kind <> SyntaxKind.TypeOfIsExpression Then
                ' IsNot and a list of types have no single type for the name.
                ReportDiagnostic(diagnostics, node.Identifier, ERRID.ERR_PatternVariableNotAllowedHere)
                Return BadTypeOfInto(node, test)
            Else
                local = BindPatternVariable(node.Identifier, diagnostics)
            End If

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

        Private Shared Function BadTypeOfInto(node As TypeOfIntoExpressionSyntax, test As BoundExpression) As BoundExpression
            Return New BoundBadExpression(node, LookupResultKind.Empty, ImmutableArray(Of Symbol).Empty, ImmutableArray.Create(test), test.Type, hasErrors:=True)
        End Function
    End Class
End Namespace
