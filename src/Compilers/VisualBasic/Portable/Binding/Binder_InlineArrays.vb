' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend Class Binder

        ''' <summary>
        ''' Whether an inline array expression is a variable whose elements a span can refer to. Span(Of T) needs a
        ''' writable variable. ReadOnlySpan(Of T) also accepts a ReadOnly variable or a ref readonly result.
        ''' </summary>
        Friend Shared Function IsInlineArrayVariable(expression As BoundExpression, writable As Boolean) As Boolean
            If expression.IsLValue Then
                Return True
            End If

            If writable Then
                Return False
            End If

            Select Case expression.Kind
                Case BoundKind.Local
                    Return Not DirectCast(expression, BoundLocal).LocalSymbol.IsConst

                Case BoundKind.FieldAccess
                    Dim access = DirectCast(expression, BoundFieldAccess)
                    If access.FieldSymbol.IsConst Then
                        Return False
                    End If

                    Dim receiver = access.ReceiverOpt
                    Return receiver Is Nothing OrElse
                           receiver.Type.IsReferenceType OrElse
                           IsInlineArrayVariable(receiver, writable:=False)

                Case BoundKind.Call
                    Return DirectCast(expression, BoundCall).Method.ReturnsByRef

                Case BoundKind.PropertyAccess
                    Return DirectCast(expression, BoundPropertyAccess).PropertySymbol.ReturnsByRef

                Case BoundKind.MeReference
                    Return True
            End Select

            Return False
        End Function

        Friend Shared Function IsSpanOrReadOnlySpan(compilation As VisualBasicCompilation, type As TypeSymbol) As Boolean
            Dim named = TryCast(type, NamedTypeSymbol)
            If named Is Nothing OrElse named.Arity <> 1 Then
                Return False
            End If

            Dim definition = named.OriginalDefinition
            Return TypeSymbol.Equals(definition, compilation.GetWellKnownType(WellKnownType.System_Span_T), TypeCompareKind.ConsiderEverything) OrElse
                   TypeSymbol.Equals(definition, compilation.GetWellKnownType(WellKnownType.System_ReadOnlySpan_T), TypeCompareKind.ConsiderEverything)
        End Function

        ''' <summary>
        ''' Whether a type is Span(Of T) or ReadOnlySpan(Of T) of the element type, and the helpers that build it exist.
        ''' </summary>
        Friend Shared Function IsInlineArraySpanType(compilation As VisualBasicCompilation, type As TypeSymbol, elementType As TypeSymbol, <Out> ByRef isReadOnly As Boolean) As Boolean
            isReadOnly = False

            Dim named = TryCast(type, NamedTypeSymbol)
            If named Is Nothing OrElse named.Arity <> 1 Then
                Return False
            End If

            Dim definition = named.OriginalDefinition
            If TypeSymbol.Equals(definition, compilation.GetWellKnownType(WellKnownType.System_ReadOnlySpan_T), TypeCompareKind.ConsiderEverything) Then
                isReadOnly = True
            ElseIf Not TypeSymbol.Equals(definition, compilation.GetWellKnownType(WellKnownType.System_Span_T), TypeCompareKind.ConsiderEverything) Then
                Return False
            End If

            If Not TypeSymbol.Equals(named.TypeArgumentsNoUseSiteDiagnostics(0), elementType, TypeCompareKind.ConsiderEverything) Then
                Return False
            End If

            Return compilation.GetWellKnownTypeMember(WellKnownMember.System_Runtime_CompilerServices_Unsafe__As_T) IsNot Nothing AndAlso
                   compilation.GetWellKnownTypeMember(If(isReadOnly,
                                                         WellKnownMember.System_Runtime_InteropServices_MemoryMarshal__CreateReadOnlySpan,
                                                         WellKnownMember.System_Runtime_InteropServices_MemoryMarshal__CreateSpan)) IsNot Nothing
        End Function

        ''' <summary>
        ''' On vbnext, binds buffer(i) on an inline array as an element of a span over the buffer: Span(Of T) when the
        ''' buffer is a writable variable, so the element can be assigned, and ReadOnlySpan(Of T) otherwise.
        ''' </summary>
        Private Function TryBindInlineArrayElementAccess(
            node As InvocationExpressionSyntax,
            target As BoundExpression,
            boundArguments As ImmutableArray(Of BoundExpression),
            argumentNames As ImmutableArray(Of String),
            argumentNamesLocations As ImmutableArray(Of Location),
            diagnostics As BindingDiagnosticBag
        ) As BoundExpression
            If target.HasErrors OrElse Compilation.LanguageVersion < LanguageVersion.VbNext Then
                Return Nothing
            End If

            Dim length As Integer
            Dim elementType As TypeSymbol = Nothing
            If Not target.Type.TryGetInlineArrayInfo(length, elementType) Then
                Return Nothing
            End If

            Dim isWritable = IsInlineArrayVariable(target, writable:=True)
            If Not isWritable AndAlso Not IsInlineArrayVariable(target, writable:=False) Then
                ReportDiagnostic(diagnostics, target.Syntax, ERRID.ERR_InlineArrayNotVariable)
                Return BadExpression(node, ImmutableArray.Create(target).AddRange(boundArguments), ErrorTypeSymbol.UnknownResultType)
            End If

            Dim spanDefinition = Compilation.GetWellKnownType(If(isWritable, WellKnownType.System_Span_T, WellKnownType.System_ReadOnlySpan_T))
            Dim spanType = spanDefinition.Construct(elementType)
            Dim isReadOnly As Boolean
            If spanDefinition.IsErrorType() OrElse Not IsInlineArraySpanType(Compilation, spanType, elementType, isReadOnly) Then
                Return Nothing
            End If

            If boundArguments.Length = 1 AndAlso argumentNames.IsDefault Then
                Dim index = boundArguments(0)
                Dim constant = index.ConstantValueOpt
                If constant IsNot Nothing AndAlso Not constant.IsBad AndAlso constant.IsIntegral AndAlso
                   (constant.Int64Value < 0 OrElse constant.Int64Value >= length) Then
                    ReportDiagnostic(diagnostics, index.Syntax, ERRID.ERR_InlineArrayIndexOutOfRange, length)
                    Return BadExpression(node, ImmutableArray.Create(target).AddRange(boundArguments), ErrorTypeSymbol.UnknownResultType)
                End If
            End If

            Dim span = New BoundConversion(target.Syntax,
                                           MakeRValue(target, diagnostics),
                                           ConversionKind.InlineArray,
                                           checked:=False,
                                           explicitCastInCode:=False,
                                           type:=spanType).MakeCompilerGenerated()

            Dim defaultPropertyGroup = BindDefaultPropertyGroup(If(node.Expression, node), span, diagnostics)
            If defaultPropertyGroup Is Nothing OrElse defaultPropertyGroup.Kind <> BoundKind.PropertyGroup Then
                Return Nothing
            End If

            Return BindInvocationExpressionPossiblyWithoutArguments(
                node,
                TypeCharacter.None,
                DirectCast(defaultPropertyGroup, BoundMethodOrPropertyGroup),
                boundArguments,
                argumentNames,
                argumentNamesLocations,
                allowBindingWithoutArguments:=False,
                diagnostics)
        End Function

        ''' <summary>
        ''' On vbnext, For Each over an inline array iterates a span over its elements.
        ''' </summary>
        Private Function TryConvertInlineArrayCollection(collection As BoundExpression, diagnostics As BindingDiagnosticBag) As BoundExpression
            If collection.HasErrors OrElse Compilation.LanguageVersion < LanguageVersion.VbNext Then
                Return Nothing
            End If

            Dim length As Integer
            Dim elementType As TypeSymbol = Nothing
            If Not collection.Type.TryGetInlineArrayInfo(length, elementType) Then
                Return Nothing
            End If

            Dim isWritable = IsInlineArrayVariable(collection, writable:=True)
            If Not isWritable AndAlso Not IsInlineArrayVariable(collection, writable:=False) Then
                ReportDiagnostic(diagnostics, collection.Syntax, ERRID.ERR_InlineArrayNotVariable)
                Return BadExpression(collection.Syntax, collection, ErrorTypeSymbol.UnknownResultType)
            End If

            Dim spanDefinition = Compilation.GetWellKnownType(If(isWritable, WellKnownType.System_Span_T, WellKnownType.System_ReadOnlySpan_T))
            Dim spanType = spanDefinition.Construct(elementType)
            Dim isReadOnly As Boolean
            If spanDefinition.IsErrorType() OrElse Not IsInlineArraySpanType(Compilation, spanType, elementType, isReadOnly) Then
                Return Nothing
            End If

            Return New BoundConversion(collection.Syntax,
                                       MakeRValue(collection, diagnostics),
                                       ConversionKind.InlineArray,
                                       checked:=False,
                                       explicitCastInCode:=False,
                                       type:=spanType).MakeCompilerGenerated()
        End Function

    End Class

End Namespace
