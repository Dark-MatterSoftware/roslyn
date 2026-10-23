' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' On vbnext, keeps a ref structure value that can refer to a variable of the method from leaving the method,
    ''' by Return, by assigning the function name, or by assignment to a ByRef parameter or through Me
    ''' (docs/vbnext-vb093-syntax.md). Which locals hold such a value does not depend on the order of statements.
    ''' </summary>
    Friend NotInheritable Class RefStructEscapeAnalysis
        Inherits BoundTreeWalkerWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator

        Private ReadOnly _diagnostics As DiagnosticBag
        Private ReadOnly _localStorageLocals As New HashSet(Of LocalSymbol)()
        Private _reporting As Boolean
        Private _changed As Boolean

        Private Sub New(diagnostics As DiagnosticBag)
            _diagnostics = diagnostics
        End Sub

        Public Shared Sub Analyze(node As BoundNode, diagnostics As DiagnosticBag)
            Dim analysis As New RefStructEscapeAnalysis(diagnostics)

            Do
                analysis._changed = False
                analysis.Visit(node)
            Loop While analysis._changed

            analysis._reporting = True
            analysis.Visit(node)
        End Sub

        Public Overrides Function VisitLocalDeclaration(node As BoundLocalDeclaration) As BoundNode
            If node.DeclarationInitializerOpt IsNot Nothing AndAlso Not node.LocalSymbol.IsByRef Then
                NoteLocalValue(node.LocalSymbol, node.DeclarationInitializerOpt)
            End If

            Return MyBase.VisitLocalDeclaration(node)
        End Function

        Public Overrides Function VisitAsNewLocalDeclarations(node As BoundAsNewLocalDeclarations) As BoundNode
            For Each declaration In node.LocalDeclarations
                NoteLocalValue(declaration.LocalSymbol, node.Initializer)
            Next

            Return MyBase.VisitAsNewLocalDeclarations(node)
        End Function

        Public Overrides Function VisitAssignmentOperator(node As BoundAssignmentOperator) As BoundNode
            Dim target = node.Left
            Dim value = node.Right

            If target.Kind = BoundKind.Local AndAlso Not DirectCast(target, BoundLocal).LocalSymbol.IsByRef Then
                Dim local = DirectCast(target, BoundLocal).LocalSymbol
                If local.IsFunctionValue Then
                    ReportIfEscapes(value)
                Else
                    NoteLocalValue(local, value)
                End If

            ElseIf Not target.Type.HoldsRefField() AndAlso Binder.IsReachedThroughByRefParameterOrMe(target) Then
                ' A structure that holds a ref field reports BC37415 there already, whatever it refers to.
                ReportIfEscapes(value)
            End If

            Return MyBase.VisitAssignmentOperator(node)
        End Function

        Public Overrides Function VisitReturnStatement(node As BoundReturnStatement) As BoundNode
            If node.ExpressionOpt IsNot Nothing Then
                ReportIfEscapes(node.ExpressionOpt)
            End If

            Return MyBase.VisitReturnStatement(node)
        End Function

        Private Sub NoteLocalValue(local As LocalSymbol, value As BoundExpression)
            If Not _reporting AndAlso IsRefStructure(local.Type) AndAlso Not _localStorageLocals.Contains(local) AndAlso RefersToLocalStorage(value) Then
                _localStorageLocals.Add(local)
                _changed = True
            End If
        End Sub

        Private Sub ReportIfEscapes(value As BoundExpression)
            If _reporting AndAlso Not value.HasErrors AndAlso RefersToLocalStorage(value) Then
                Binder.ReportDiagnostic(_diagnostics, value.Syntax, ERRID.ERR_RefStructValueEscapes)
            End If
        End Sub

        Private Shared Function IsRefStructure(type As TypeSymbol) As Boolean
            Return type IsNot Nothing AndAlso type.IsRefLikeType
        End Function

        ''' <summary>
        ''' Whether a value of a ref structure type can refer to a variable of this method.
        ''' </summary>
        Private Function RefersToLocalStorage(value As BoundExpression) As Boolean
            If value Is Nothing OrElse Not IsRefStructure(value.Type) Then
                Return False
            End If

            Select Case value.Kind
                Case BoundKind.Conversion
                    Dim conversion = DirectCast(value, BoundConversion)
                    If Conversions.IsInlineArrayConversion(conversion.ConversionKind) Then
                        Return IsLocalStorage(conversion.Operand)
                    End If

                    Return RefersToLocalStorage(conversion.Operand)

                Case BoundKind.UserDefinedConversion
                    Return RefersToLocalStorage(DirectCast(value, BoundUserDefinedConversion).UnderlyingExpression)

                Case BoundKind.Parenthesized
                    Return RefersToLocalStorage(DirectCast(value, BoundParenthesized).Expression)

                Case BoundKind.Sequence
                    Return RefersToLocalStorage(DirectCast(value, BoundSequence).ValueOpt)

                Case BoundKind.TernaryConditionalExpression
                    Dim conditional = DirectCast(value, BoundTernaryConditionalExpression)
                    Return RefersToLocalStorage(conditional.WhenTrue) OrElse RefersToLocalStorage(conditional.WhenFalse)

                Case BoundKind.Local
                    Return _localStorageLocals.Contains(DirectCast(value, BoundLocal).LocalSymbol)

                Case BoundKind.FieldAccess
                    Return RefersToLocalStorage(DirectCast(value, BoundFieldAccess).ReceiverOpt)

                Case BoundKind.ObjectCreationExpression
                    Dim creation = DirectCast(value, BoundObjectCreationExpression)
                    Return creation.ConstructorOpt IsNot Nothing AndAlso
                           ArgumentsReferToLocalStorage(creation.Arguments, creation.ConstructorOpt.Parameters)

                Case BoundKind.Call
                    Dim [call] = DirectCast(value, BoundCall)
                    Return RefersToLocalStorage([call].ReceiverOpt) OrElse
                           ArgumentsReferToLocalStorage([call].Arguments, [call].Method.Parameters)

                Case BoundKind.PropertyAccess
                    Dim access = DirectCast(value, BoundPropertyAccess)
                    Return RefersToLocalStorage(access.ReceiverOpt) OrElse
                           ArgumentsReferToLocalStorage(access.Arguments, access.PropertySymbol.Parameters)
            End Select

            Return False
        End Function

        Private Function ArgumentsReferToLocalStorage(arguments As ImmutableArray(Of BoundExpression), parameters As ImmutableArray(Of ParameterSymbol)) As Boolean
            For i = 0 To arguments.Length - 1
                Dim argument = arguments(i)
                If argument.Kind = BoundKind.ByRefArgumentWithCopyBack Then
                    ' The argument is copied into a temporary, which the call may refer to.
                    Return True
                End If

                If i < parameters.Length AndAlso parameters(i).IsByRef AndAlso IsLocalStorage(argument) Then
                    Return True
                End If

                If RefersToLocalStorage(argument) Then
                    Return True
                End If
            Next

            Return False
        End Function

        ''' <summary>
        ''' Whether a variable, whose storage a reference is taken to, is a variable of this method.
        ''' </summary>
        Private Function IsLocalStorage(storage As BoundExpression) As Boolean
            Select Case storage.Kind
                Case BoundKind.Parameter
                    Return Not DirectCast(storage, BoundParameter).ParameterSymbol.IsByRef

                Case BoundKind.ArrayAccess
                    Return False

                Case BoundKind.FieldAccess
                    Dim access = DirectCast(storage, BoundFieldAccess)
                    Dim field = access.FieldSymbol
                    If field.IsShared Then
                        Return False
                    End If

                    If field.FieldRefKind <> RefKind.None Then
                        ' The storage is the variable that the ref field refers to.
                        Return RefersToLocalStorage(access.ReceiverOpt)
                    End If

                    Dim receiver = access.ReceiverOpt
                    Return receiver IsNot Nothing AndAlso Not receiver.Type.IsReferenceType AndAlso IsLocalStorage(receiver)

                Case BoundKind.Call
                    Dim [call] = DirectCast(storage, BoundCall)
                    Return Not [call].Method.ReturnsByRef OrElse
                           RefersToLocalStorage([call].ReceiverOpt) OrElse
                           ArgumentsReferToLocalStorage([call].Arguments, [call].Method.Parameters)

                Case BoundKind.PropertyAccess
                    Dim access = DirectCast(storage, BoundPropertyAccess)
                    Return Not access.PropertySymbol.ReturnsByRef OrElse
                           RefersToLocalStorage(access.ReceiverOpt) OrElse
                           ArgumentsReferToLocalStorage(access.Arguments, access.PropertySymbol.Parameters)

                Case BoundKind.Parenthesized
                    Return IsLocalStorage(DirectCast(storage, BoundParenthesized).Expression)
            End Select

            ' Locals, including ByRef locals, Me, and values that are copied into temporaries.
            Return True
        End Function

    End Class

End Namespace
