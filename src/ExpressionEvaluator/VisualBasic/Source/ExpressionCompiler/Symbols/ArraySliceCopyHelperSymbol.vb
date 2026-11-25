' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator

    ''' <summary>
    ''' Shared helper emitted beside &lt;&gt;m0 for debugger array-slice evaluation.
    ''' Copies elements with GetValue/SetValue so Concord function evaluation observes the writes
    ''' (unlike <see cref="System.Array.Copy"/> on some FE paths). Invalid bounds still call
    ''' <see cref="System.Array.Copy"/> so throw behavior matches BindArrayRange.
    ''' </summary>
    Friend NotInheritable Class ArraySliceCopyHelperSymbol
        Inherits SynthesizedMethodBase

        Friend Const MethodName As String = "<>ArraySliceCopy"

        Private ReadOnly _parameters As ImmutableArray(Of ParameterSymbol)
        Private ReadOnly _voidType As TypeSymbol
        Private ReadOnly _int32Type As TypeSymbol
        Private ReadOnly _arrayType As TypeSymbol
        Private ReadOnly _objectType As TypeSymbol
        Private ReadOnly _booleanType As TypeSymbol
        Private ReadOnly _arrayCopy As MethodSymbol
        Private ReadOnly _arrayGetValue As MethodSymbol
        Private ReadOnly _arraySetValue As MethodSymbol
        Private ReadOnly _syntax As SyntaxNode

        Friend Sub New(container As NamedTypeSymbol, compilation As VisualBasicCompilation)
            MyBase.New(container)

            _syntax = SyntaxFactory.IdentifierName(SyntaxFactory.MissingToken(SyntaxKind.IdentifierToken))
            _voidType = compilation.GetSpecialType(SpecialType.System_Void)
            _int32Type = compilation.GetSpecialType(SpecialType.System_Int32)
            _arrayType = compilation.GetSpecialType(SpecialType.System_Array)
            _objectType = compilation.GetSpecialType(SpecialType.System_Object)
            _booleanType = compilation.GetSpecialType(SpecialType.System_Boolean)

            _arrayCopy = FindArrayCopy(compilation)
            _arrayGetValue = FindArrayGetValue(compilation)
            _arraySetValue = FindArraySetValue(compilation)

            _parameters = ImmutableArray.Create(Of ParameterSymbol)(
                New SynthesizedParameterSymbol(Me, _arrayType, 0, False, "source"),
                New SynthesizedParameterSymbol(Me, _int32Type, 1, False, "start"),
                New SynthesizedParameterSymbol(Me, _arrayType, 2, False, "dest"),
                New SynthesizedParameterSymbol(Me, _int32Type, 3, False, "length"))
        End Sub

        Private Shared Function FindArrayCopy(compilation As VisualBasicCompilation) As MethodSymbol
            For Each member In compilation.GetSpecialType(SpecialType.System_Array).GetMembers("Copy")
                Dim method = TryCast(member, MethodSymbol)
                If method Is Nothing OrElse Not method.IsShared OrElse method.ParameterCount <> 5 Then
                    Continue For
                End If

                Dim parameters = method.Parameters
                If parameters(0).Type.SpecialType = SpecialType.System_Array AndAlso
                   parameters(1).Type.SpecialType = SpecialType.System_Int32 AndAlso
                   parameters(2).Type.SpecialType = SpecialType.System_Array AndAlso
                   parameters(3).Type.SpecialType = SpecialType.System_Int32 AndAlso
                   parameters(4).Type.SpecialType = SpecialType.System_Int32 Then
                    Return method
                End If
            Next

            Throw ExceptionUtilities.Unreachable
        End Function

        Private Shared Function FindArrayGetValue(compilation As VisualBasicCompilation) As MethodSymbol
            For Each member In compilation.GetSpecialType(SpecialType.System_Array).GetMembers("GetValue")
                Dim method = TryCast(member, MethodSymbol)
                If method IsNot Nothing AndAlso Not method.IsShared AndAlso method.ParameterCount = 1 AndAlso
                   method.Parameters(0).Type.SpecialType = SpecialType.System_Int32 AndAlso
                   method.ReturnType.SpecialType = SpecialType.System_Object Then
                    Return method
                End If
            Next

            Throw ExceptionUtilities.Unreachable
        End Function

        Private Shared Function FindArraySetValue(compilation As VisualBasicCompilation) As MethodSymbol
            For Each member In compilation.GetSpecialType(SpecialType.System_Array).GetMembers("SetValue")
                Dim method = TryCast(member, MethodSymbol)
                If method IsNot Nothing AndAlso Not method.IsShared AndAlso method.ParameterCount = 2 AndAlso
                   method.Parameters(0).Type.SpecialType = SpecialType.System_Object AndAlso
                   method.Parameters(1).Type.SpecialType = SpecialType.System_Int32 Then
                    Return method
                End If
            Next

            Throw ExceptionUtilities.Unreachable
        End Function

        Public Overrides ReadOnly Property Name As String
            Get
                Return MethodName
            End Get
        End Property

        Public Overrides ReadOnly Property MetadataName As String
            Get
                Return MethodName
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return Accessibility.Friend
            End Get
        End Property

        Public Overrides ReadOnly Property IsShared As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property IsSub As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property IsMustOverride As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsNotOverridable As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverridable As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverrides As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverloads As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function GetOverloadResolutionPriority() As Integer
            Return 0
        End Function

        Public Overrides ReadOnly Property MethodKind As MethodKind
            Get
                Return MethodKind.Ordinary
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return ImmutableArray(Of Location).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return ImmutableArray(Of SyntaxReference).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return _parameters
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnType As TypeSymbol
            Get
                Return _voidType
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnTypeCustomModifiers As ImmutableArray(Of CustomModifier)
            Get
                Return ImmutableArray(Of CustomModifier).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property TypeParameters As ImmutableArray(Of TypeParameterSymbol)
            Get
                Return ImmutableArray(Of TypeParameterSymbol).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property TypeArguments As ImmutableArray(Of TypeSymbol)
            Get
                Return ImmutableArray(Of TypeSymbol).Empty
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return True
            End Get
        End Property

        Friend Overrides ReadOnly Property GenerateDebugInfoImpl As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides Function CalculateLocalSyntaxOffset(localPosition As Integer, localTree As SyntaxTree) As Integer
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetBoundMethodBody(compilationState As TypeCompilationState, diagnostics As BindingDiagnosticBag, <Out> ByRef Optional methodBodyBinder As Binder = Nothing) As BoundBlock
            methodBodyBinder = Nothing
            Dim syntax = _syntax
            Dim source = New BoundParameter(syntax, _parameters(0), isLValue:=False, type:=_arrayType).MakeCompilerGenerated()
            Dim start = New BoundParameter(syntax, _parameters(1), isLValue:=False, type:=_int32Type).MakeCompilerGenerated()
            Dim dest = New BoundParameter(syntax, _parameters(2), isLValue:=False, type:=_arrayType).MakeCompilerGenerated()
            Dim length = New BoundParameter(syntax, _parameters(3), isLValue:=False, type:=_int32Type).MakeCompilerGenerated()

            Dim indexLocal As LocalSymbol = New SynthesizedLocal(Me, _int32Type, SynthesizedLocalKind.LoweringTemp)
            Dim statements = ArrayBuilder(Of BoundStatement).GetInstance()

            Dim fallbackLabel = New GeneratedLabelSymbol("sliceHelperFallback")
            Dim loopLabel = New GeneratedLabelSymbol("sliceHelperLoop")
            Dim checkLabel = New GeneratedLabelSymbol("sliceHelperCheck")
            Dim doneLabel = New GeneratedLabelSymbol("sliceHelperDone")

            Dim sourceIsNothing = New BoundBinaryOperator(syntax, BinaryOperatorKind.Is, source, New BoundLiteral(syntax, ConstantValue.Null, _arrayType).MakeCompilerGenerated(), False, _booleanType).MakeCompilerGenerated()
            statements.Add(New BoundConditionalGoto(syntax, sourceIsNothing, True, fallbackLabel).MakeCompilerGenerated())

            Dim startIsNegative = New BoundBinaryOperator(syntax, BinaryOperatorKind.LessThan, start, New BoundLiteral(syntax, ConstantValue.Create(0), _int32Type).MakeCompilerGenerated(), False, _booleanType).MakeCompilerGenerated()
            statements.Add(New BoundConditionalGoto(syntax, startIsNegative, True, fallbackLabel).MakeCompilerGenerated())

            Dim endIndex = New BoundBinaryOperator(syntax, BinaryOperatorKind.Add, start, length, False, _int32Type).MakeCompilerGenerated()
            Dim sourceLength = New BoundArrayLength(syntax, source, _int32Type).MakeCompilerGenerated()
            Dim endPastSource = New BoundBinaryOperator(syntax, BinaryOperatorKind.GreaterThan, endIndex, sourceLength, False, _booleanType).MakeCompilerGenerated()
            statements.Add(New BoundConditionalGoto(syntax, endPastSource, True, fallbackLabel).MakeCompilerGenerated())

            statements.Add(Assign(New BoundLocal(syntax, indexLocal, isLValue:=True, type:=_int32Type).MakeCompilerGenerated(), New BoundLiteral(syntax, ConstantValue.Create(0), _int32Type).MakeCompilerGenerated(), syntax, _int32Type))
            statements.Add(New BoundGotoStatement(syntax, checkLabel, Nothing).MakeCompilerGenerated())

            statements.Add(New BoundLabelStatement(syntax, loopLabel).MakeCompilerGenerated())
            Dim sourceIndex = New BoundBinaryOperator(syntax, BinaryOperatorKind.Add, start, New BoundLocal(syntax, indexLocal, isLValue:=False, type:=_int32Type).MakeCompilerGenerated(), False, _int32Type).MakeCompilerGenerated()
            Dim element = New BoundCall(syntax, _arrayGetValue, Nothing, source, ImmutableArray.Create(Of BoundExpression)(sourceIndex), Nothing, suppressObjectClone:=True, type:=_objectType).MakeCompilerGenerated()
            Dim setValue = New BoundCall(
                syntax,
                _arraySetValue,
                Nothing,
                dest,
                ImmutableArray.Create(Of BoundExpression)(element, New BoundLocal(syntax, indexLocal, isLValue:=False, type:=_int32Type).MakeCompilerGenerated()),
                Nothing,
                suppressObjectClone:=True,
                type:=_voidType).MakeCompilerGenerated()
            statements.Add(New BoundExpressionStatement(syntax, setValue).MakeCompilerGenerated())

            Dim nextIndex = New BoundBinaryOperator(
                syntax,
                BinaryOperatorKind.Add,
                New BoundLocal(syntax, indexLocal, isLValue:=False, type:=_int32Type).MakeCompilerGenerated(),
                New BoundLiteral(syntax, ConstantValue.Create(1), _int32Type).MakeCompilerGenerated(),
                False,
                _int32Type).MakeCompilerGenerated()
            statements.Add(Assign(New BoundLocal(syntax, indexLocal, isLValue:=True, type:=_int32Type).MakeCompilerGenerated(), nextIndex, syntax, _int32Type))

            statements.Add(New BoundLabelStatement(syntax, checkLabel).MakeCompilerGenerated())
            Dim more = New BoundBinaryOperator(
                syntax,
                BinaryOperatorKind.LessThan,
                New BoundLocal(syntax, indexLocal, isLValue:=False, type:=_int32Type).MakeCompilerGenerated(),
                length,
                False,
                _booleanType).MakeCompilerGenerated()
            statements.Add(New BoundConditionalGoto(syntax, more, True, loopLabel).MakeCompilerGenerated())
            statements.Add(New BoundGotoStatement(syntax, doneLabel, Nothing).MakeCompilerGenerated())

            statements.Add(New BoundLabelStatement(syntax, fallbackLabel).MakeCompilerGenerated())
            Dim copyCall = New BoundCall(
                syntax,
                _arrayCopy,
                Nothing,
                Nothing,
                ImmutableArray.Create(Of BoundExpression)(
                    source,
                    start,
                    dest,
                    New BoundLiteral(syntax, ConstantValue.Create(0), _int32Type).MakeCompilerGenerated(),
                    length),
                Nothing,
                suppressObjectClone:=True,
                type:=_voidType).MakeCompilerGenerated()
            statements.Add(New BoundExpressionStatement(syntax, copyCall).MakeCompilerGenerated())

            statements.Add(New BoundLabelStatement(syntax, doneLabel).MakeCompilerGenerated())
            statements.Add(New BoundReturnStatement(syntax, Nothing, Nothing, Nothing).MakeCompilerGenerated())

            Return New BoundBlock(syntax, Nothing, ImmutableArray.Create(indexLocal), statements.ToImmutableAndFree()).MakeCompilerGenerated()
        End Function

        Private Shared Function Assign(left As BoundExpression, right As BoundExpression, syntax As SyntaxNode, type As TypeSymbol) As BoundStatement
            Return New BoundExpressionStatement(
                syntax,
                New BoundAssignmentOperator(syntax, left, right, suppressObjectClone:=True, type:=type).MakeCompilerGenerated()).MakeCompilerGenerated()
        End Function
    End Class
End Namespace
