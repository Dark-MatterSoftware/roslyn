' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Linq
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    Friend NotInheritable Class SynthesizedRecordMethodSymbol
        Inherits SynthesizedRegularMethodBase

        Friend Enum RecordMethodKind
            ObjectEquals
            TypedEquals
            GetHashCode
            Print
            Deconstruct
            Clone
            OperatorEquality
            OperatorInequality
        End Enum

        ''' <summary>
        ''' The name C# also gives the method that a With copy calls, so each language can copy the other's records.
        ''' </summary>
        Friend Const CloneMethodName As String = "<Clone>$"

        Private ReadOnly _kind As RecordMethodKind
        Private ReadOnly _returnType As TypeSymbol
        Private ReadOnly _overriddenMethod As MethodSymbol
        Private ReadOnly _isOverloads As Boolean
        Private _parameters As ImmutableArray(Of ParameterSymbol)

        Public Sub New(syntax As VisualBasicSyntaxNode,
                       container As NamedTypeSymbol,
                       name As String,
                       returnType As TypeSymbol,
                       kind As RecordMethodKind,
                       Optional overriddenMethod As MethodSymbol = Nothing,
                       Optional isOverloads As Boolean = False)
            MyBase.New(syntax, container, name, isShared:=IsOperatorKind(kind))
            _kind = kind
            _returnType = returnType
            _overriddenMethod = overriddenMethod
            _isOverloads = isOverloads
        End Sub

        Private Shared Function IsOperatorKind(kind As RecordMethodKind) As Boolean
            Return kind = RecordMethodKind.OperatorEquality OrElse kind = RecordMethodKind.OperatorInequality
        End Function

        Public Overrides ReadOnly Property MethodKind As MethodKind
            Get
                Return If(IsOperatorKind(_kind), MethodKind.UserDefinedOperator, MethodKind.Ordinary)
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return IsOperatorKind(_kind)
            End Get
        End Property

        Friend Sub SetParameters(parameters As ImmutableArray(Of ParameterSymbol))
            _parameters = parameters
        End Sub

        Public Overrides ReadOnly Property IsOverloads As Boolean
            Get
                Return _kind <> RecordMethodKind.Deconstruct OrElse _isOverloads
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverrides As Boolean
            Get
                Return _overriddenMethod IsNot Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverridable As Boolean
            Get
                If _kind = RecordMethodKind.Clone Then
                    Return _overriddenMethod Is Nothing AndAlso Not ContainingType.IsNotInheritable
                End If

                Return _overriddenMethod IsNot Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property OverriddenMethod As MethodSymbol
            Get
                Return _overriddenMethod
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return Accessibility.Public
            End Get
        End Property

        Public Overrides ReadOnly Property IsSub As Boolean
            Get
                Return _kind = RecordMethodKind.Deconstruct
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnType As TypeSymbol
            Get
                Return _returnType
            End Get
        End Property

        Friend Overrides ReadOnly Property ParameterCount As Integer
            Get
                Return _parameters.Length
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return _parameters
            End Get
        End Property

        Friend Overrides Function GetLexicalSortKey() As LexicalSortKey
            If _kind = RecordMethodKind.TypedEquals Then
                Return New LexicalSortKey(m_SyntaxNode.SyntaxTree, m_SyntaxNode.SpanStart + 1, DirectCast(DeclaringCompilation, VisualBasicCompilation))
            End If

            Return MyBase.GetLexicalSortKey()
        End Function

        Friend Overrides ReadOnly Property GenerateDebugInfoImpl As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides Function CalculateLocalSyntaxOffset(localPosition As Integer, localTree As SyntaxTree) As Integer
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetBoundMethodBody(compilationState As TypeCompilationState, diagnostics As BindingDiagnosticBag, Optional ByRef methodBodyBinder As Binder = Nothing) As BoundBlock
            methodBodyBinder = Nothing
            Dim factory As New SyntheticBoundNodeFactory(Me, Me, m_SyntaxNode, compilationState, diagnostics)
            Select Case _kind
                Case RecordMethodKind.ObjectEquals
                    Return ObjectEqualsBody(factory)
                Case RecordMethodKind.TypedEquals
                    Return TypedEqualsBody(factory)
                Case RecordMethodKind.GetHashCode
                    Return HashCodeBody(factory)
                Case RecordMethodKind.Deconstruct
                    Return DeconstructBody(factory)
                Case RecordMethodKind.Clone
                    Return CloneBody(factory)
                Case RecordMethodKind.OperatorEquality
                    Return factory.Block(factory.Return(OperatorEqualityValue(factory)))
                Case RecordMethodKind.OperatorInequality
                    Return factory.Block(factory.Return(factory.Not(OperatorEqualityValue(factory))))
                Case Else
                    Return PrintBody(factory)
            End Select
        End Function

        Private Function CloneBody(factory As SyntheticBoundNodeFactory) As BoundBlock
            Dim copyConstructor = ContainingType.InstanceConstructors.OfType(Of SynthesizedRecordCopyConstructorSymbol)().Single()
            Dim copy As BoundExpression = New BoundObjectCreationExpression(m_SyntaxNode, copyConstructor, Nothing,
                                                                           ImmutableArray.Create(Of BoundExpression)(factory.Me()),
                                                                           BitVector.Null, Nothing, ContainingType).MakeCompilerGenerated()
            If Not TypeSymbol.Equals(_returnType, ContainingType, TypeCompareKind.ConsiderEverything) Then
                copy = New BoundConversion(m_SyntaxNode, copy, ConversionKind.WideningReference, False, False, Nothing, _returnType, Nothing).MakeCompilerGenerated()
            End If

            Return factory.Block(factory.Return(copy))
        End Function

        Private Function DeconstructBody(factory As SyntheticBoundNodeFactory) As BoundBlock
            Dim statements = ArrayBuilder(Of BoundStatement).GetInstance()
            For Each parameter In _parameters
                Dim [property] = FindPositionalProperty(parameter.Name)
                If [property] Is Nothing Then
                    Continue For
                End If

                statements.Add(factory.Assignment(factory.Parameter(parameter, isLValue:=True), factory.Call(factory.Me(), [property].GetMethod)))
            Next

            statements.Add(factory.Return())
            Return factory.Block(statements.ToImmutableAndFree())
        End Function

        ''' <summary>
        ''' <c>left Is right OrElse (left IsNot Nothing AndAlso left.Equals(right))</c> for a record class, as C# 9 defines <c>==</c>,
        ''' and <c>left.Equals(right)</c> for a record structure.
        ''' </summary>
        Private Function OperatorEqualityValue(factory As SyntheticBoundNodeFactory) As BoundExpression
            Dim boolType = factory.SpecialType(SpecialType.System_Boolean)
            Dim left = factory.Parameter(_parameters(0), isLValue:=False)
            Dim right = factory.Parameter(_parameters(1), isLValue:=False)
            Dim typedEquals = TypedEqualsMethod()
            Dim callEquals As BoundExpression = If(typedEquals Is Nothing, DirectCast(factory.Literal(False), BoundExpression), factory.Call(left, typedEquals, right))
            If ContainingType.IsValueType Then
                Return callEquals
            End If

            Dim objectType = factory.SpecialType(SpecialType.System_Object)
            Dim same = factory.Binary(BinaryOperatorKind.Is, boolType, AsObject(left, objectType), AsObject(factory.Parameter(_parameters(1), isLValue:=False), objectType))
            Dim leftIsSomething = factory.Binary(BinaryOperatorKind.IsNot, boolType, factory.Parameter(_parameters(0), isLValue:=False), NothingLiteral(factory, ContainingType))
            Return factory.Binary(BinaryOperatorKind.OrElse, boolType, same, factory.Binary(BinaryOperatorKind.AndAlso, boolType, leftIsSomething, callEquals))
        End Function

        Private Function AsObject(value As BoundExpression, objectType As TypeSymbol) As BoundExpression
            Return New BoundConversion(m_SyntaxNode, value, ConversionKind.WideningReference, False, False, Nothing, objectType, Nothing).MakeCompilerGenerated()
        End Function

        Private Function FindPositionalProperty(name As String) As PropertySymbol
            Dim current As TypeSymbol = ContainingType
            While current IsNot Nothing AndAlso Not current.IsErrorType()
                Dim candidates = current.GetMembers(name)
                If Not candidates.IsEmpty Then
                    Return candidates.OfType(Of PropertySymbol)().FirstOrDefault(
                        Function(candidate) Not candidate.IsShared AndAlso candidate.Parameters.Length = 0 AndAlso candidate.GetMethod IsNot Nothing)
                End If

                current = current.BaseTypeNoUseSiteDiagnostics
            End While

            Return Nothing
        End Function

        Private Function BaseRecord() As NamedTypeSymbol
            Dim baseType = ContainingType.BaseTypeNoUseSiteDiagnostics
            If baseType Is Nothing OrElse baseType.IsErrorType() OrElse Not baseType.IsRecord Then
                Return Nothing
            End If

            Return baseType
        End Function

        Private Function BaseTypedEquals(factory As SyntheticBoundNodeFactory, other As BoundExpression) As BoundExpression
            Dim baseType = BaseRecord()
            If baseType Is Nothing Then
                Return Nothing
            End If

            Dim method = baseType.GetMembers(WellKnownMemberNames.ObjectEquals).OfType(Of MethodSymbol)().FirstOrDefault(
                Function(candidate) Not candidate.IsShared AndAlso candidate.ParameterCount = 1 AndAlso
                    TypeSymbol.Equals(candidate.Parameters(0).Type, baseType, TypeCompareKind.ConsiderEverything))
            If method Is Nothing Then
                Return Nothing
            End If

            Dim converted = New BoundConversion(m_SyntaxNode, other, ConversionKind.WideningReference, False, False, Nothing, baseType, Nothing).MakeCompilerGenerated()
            Return factory.Call(factory.Base(), method, converted)
        End Function

        Private Function BaseHashCode(factory As SyntheticBoundNodeFactory) As BoundExpression
            Dim baseType = BaseRecord()
            If baseType Is Nothing Then
                Return Nothing
            End If

            Dim method = baseType.GetMembers(WellKnownMemberNames.ObjectGetHashCode).OfType(Of MethodSymbol)().FirstOrDefault(
                Function(candidate) Not candidate.IsShared AndAlso candidate.ParameterCount = 0)
            If method Is Nothing Then
                Return Nothing
            End If

            Return factory.Call(factory.Base(), method)
        End Function

        Private Function ObjectEqualsBody(factory As SyntheticBoundNodeFactory) As BoundBlock
            If ContainingType.IsValueType Then
                Return StructureObjectEqualsBody(factory)
            End If

            Dim boolType = factory.SpecialType(SpecialType.System_Boolean)
            Dim other = factory.SynthesizedLocal(ContainingType)
            Dim cast = factory.TryCast(factory.Parameter(_parameters(0), isLValue:=False), ContainingType)
            Dim isNothing = factory.Binary(BinaryOperatorKind.Is, boolType, factory.Local(other, isLValue:=False), NothingLiteral(factory, ContainingType))
            Dim typedEquals = TypedEqualsMethod()
            Dim callEquals As BoundExpression
            If typedEquals Is Nothing Then
                callEquals = factory.Literal(True)
            Else
                callEquals = factory.Call(factory.Me(), typedEquals, factory.Local(other, isLValue:=False))
            End If
            Return factory.Block(
                ImmutableArray.Create(other),
                factory.Assignment(factory.Local(other, isLValue:=True), cast),
                factory.If(isNothing, factory.Return(factory.Literal(False)), factory.Return(callEquals)))
        End Function

        Private Function StructureObjectEqualsBody(factory As SyntheticBoundNodeFactory) As BoundBlock
            Dim boolType = factory.SpecialType(SpecialType.System_Boolean)
            Dim objectType = factory.SpecialType(SpecialType.System_Object)
            Dim value = factory.Parameter(_parameters(0), isLValue:=False)
            Dim isNothing = factory.Binary(BinaryOperatorKind.Is, boolType, value, NothingLiteral(factory, objectType))
            Dim isType = New BoundTypeOf(m_SyntaxNode, value, isTypeOfIsNotExpression:=False, ContainingType, boolType).MakeCompilerGenerated()
            Dim conversion = Conversions.ClassifyDirectCastConversion(objectType, ContainingType, CompoundUseSiteInfo(Of AssemblySymbol).Discarded)
            Dim unboxed = New BoundDirectCast(m_SyntaxNode, value, conversion, ContainingType).MakeCompilerGenerated()
            Dim typedEquals = TypedEqualsMethod()
            Dim callEquals As BoundExpression
            If typedEquals Is Nothing Then
                callEquals = factory.Literal(True)
            Else
                callEquals = factory.Call(factory.Me(), typedEquals, unboxed)
            End If
            Return factory.Block(
                factory.If(isNothing, factory.Return(factory.Literal(False)),
                           factory.If(isType, factory.Return(callEquals), factory.Return(factory.Literal(False)))))
        End Function

        Private Function TypedEqualsBody(factory As SyntheticBoundNodeFactory) As BoundBlock
            Dim boolType = factory.SpecialType(SpecialType.System_Boolean)
            Dim other = factory.Parameter(_parameters(0), isLValue:=False)
            If ContainingType.IsValueType Then
                Dim valueComparison As BoundExpression = factory.Literal(True)
                For Each member In EqualityMembers()
                    Dim equals = ComparerCall(factory, MemberType(member), WellKnownMember.System_Collections_Generic_EqualityComparer_T__Equals, ReadMember(factory, factory.Me(), member), ReadMember(factory, other, member))
                    If equals Is Nothing Then
                        Continue For
                    End If

                    valueComparison = factory.Binary(BinaryOperatorKind.AndAlso, boolType, valueComparison, equals)
                Next

                Return factory.Block(factory.Return(valueComparison))
            End If

            Dim isNothing = factory.Binary(BinaryOperatorKind.Is, boolType, other, NothingLiteral(factory, ContainingType))
            Dim comparison As BoundExpression = If(BaseTypedEquals(factory, factory.Parameter(_parameters(0), isLValue:=False)), factory.Literal(True))
            For Each member In EqualityMembers()
                Dim equals = ComparerCall(factory, MemberType(member), WellKnownMember.System_Collections_Generic_EqualityComparer_T__Equals, ReadMember(factory, factory.Me(), member), ReadMember(factory, other, member))
                If equals Is Nothing Then
                    Continue For
                End If

                comparison = factory.Binary(BinaryOperatorKind.AndAlso, boolType, comparison, equals)
            Next

            Return factory.Block(factory.If(isNothing, factory.Return(factory.Literal(False)), factory.Return(comparison)))
        End Function

        Private Function HashCodeBody(factory As SyntheticBoundNodeFactory) As BoundBlock
            Dim intType = factory.SpecialType(SpecialType.System_Int32)
            Dim hash As BoundExpression = If(BaseHashCode(factory), factory.Literal(0))
            For Each member In EqualityMembers()
                Dim memberHash = ComparerCall(factory, MemberType(member), WellKnownMember.System_Collections_Generic_EqualityComparer_T__GetHashCode, ReadMember(factory, factory.Me(), member))
                If memberHash Is Nothing Then
                    Continue For
                End If

                hash = factory.Binary(
                    BinaryOperatorKind.Add,
                    intType,
                    factory.Binary(BinaryOperatorKind.Multiply, intType, hash, factory.Literal(-1521134295)),
                    memberHash)
            Next

            Return factory.Block(factory.Return(hash))
        End Function

        Private Function PrintBody(factory As SyntheticBoundNodeFactory) As BoundBlock
            Dim stringType = factory.SpecialType(SpecialType.System_String)
            Dim text As BoundExpression = factory.Literal(ContainingType.Name & " { ")
            Dim first = True
            For Each member In PrintedMembers()
                If Not first Then
                    text = Concat(factory, stringType, text, factory.Literal(", "))
                End If

                text = Concat(factory, stringType, text, factory.Literal(member.Name & " = "))
                text = Concat(factory, stringType, text, PrintValue(factory, ReadMember(factory, factory.Me(), member)))
                first = False
            Next

            text = Concat(factory, stringType, text, factory.Literal(" }"))
            Return factory.Block(factory.Return(text))
        End Function

        Private Function PrintValue(factory As SyntheticBoundNodeFactory, value As BoundExpression) As BoundExpression
            Dim method = FindToString(value.Type)
            If method Is Nothing Then
                Return factory.Literal("")
            End If

            If method.ContainingType.SpecialType = SpecialType.System_Object AndAlso value.Type.IsValueType Then
                Dim objectType = factory.SpecialType(SpecialType.System_Object)
                value = New BoundConversion(value.Syntax, value, ConversionKind.WideningValue, False, False, Nothing, objectType, Nothing).MakeCompilerGenerated()
            End If

            Return factory.Call(value, method)
        End Function

        Private Shared Function FindToString(type As TypeSymbol) As MethodSymbol
            Dim current = type
            While current IsNot Nothing AndAlso Not current.IsErrorType()
                Dim found = current.GetMembers("ToString").OfType(Of MethodSymbol)().FirstOrDefault(
                    Function(candidate) candidate.ParameterCount = 0 AndAlso Not candidate.IsShared AndAlso candidate.DeclaredAccessibility = Accessibility.Public)
                If found IsNot Nothing Then
                    Return found
                End If

                current = current.BaseTypeNoUseSiteDiagnostics
            End While

            Return Nothing
        End Function

        Private Function ComparerCall(factory As SyntheticBoundNodeFactory, elementType As TypeSymbol, member As WellKnownMember, ParamArray arguments As BoundExpression()) As BoundExpression
            Dim comparer = factory.Compilation.GetWellKnownType(WellKnownType.System_Collections_Generic_EqualityComparer_T)
            If comparer.TypeKind = TypeKind.Error Then
                Return Nothing
            End If

            Dim constructed = comparer.Construct(elementType)
            Dim definition = TryCast(factory.Compilation.GetWellKnownTypeMember(member), MethodSymbol)
            Dim defaultDefinition = TryCast(factory.Compilation.GetWellKnownTypeMember(WellKnownMember.System_Collections_Generic_EqualityComparer_T__get_Default), MethodSymbol)
            If definition Is Nothing OrElse defaultDefinition Is Nothing Then
                Return Nothing
            End If

            Dim defaultValue = factory.Call(Nothing, defaultDefinition.AsMember(constructed))
            Return factory.Call(defaultValue, definition.AsMember(constructed), arguments)
        End Function

        Private Function TypedEqualsMethod() As MethodSymbol
            Return ContainingType.GetMembers("Equals").OfType(Of MethodSymbol)().FirstOrDefault(
                Function(candidate) candidate.ParameterCount = 1 AndAlso TypeSymbol.Equals(candidate.Parameters(0).Type, ContainingType, TypeCompareKind.ConsiderEverything))
        End Function

        Private Function EqualityMembers() As IEnumerable(Of Symbol)
            Return ContainingType.GetMembers().Where(Function(member) IsEqualityMember(member))
        End Function

        ''' <summary>
        ''' The members of each base record, from the first record in the chain, then this record's, as C# 9 prints them.
        ''' </summary>
        Private Function PrintedMembers() As IEnumerable(Of Symbol)
            Dim records As New List(Of NamedTypeSymbol)
            Dim current As NamedTypeSymbol = ContainingType
            While current IsNot Nothing AndAlso Not current.IsErrorType() AndAlso current.IsRecord
                records.Insert(0, current)
                current = current.BaseTypeNoUseSiteDiagnostics
            End While

            Return records.SelectMany(Function(record) record.GetMembers().Where(Function(member) IsEqualityMember(member)))
        End Function

        Private Shared Function IsEqualityMember(member As Symbol) As Boolean
            Dim field = TryCast(member, FieldSymbol)
            If field IsNot Nothing Then
                Return Not field.IsShared AndAlso Not field.IsConst AndAlso field.DeclaredAccessibility = Accessibility.Public AndAlso Not field.IsImplicitlyDeclared
            End If

            Dim [property] = TryCast(member, PropertySymbol)
            Return [property] IsNot Nothing AndAlso
                Not [property].IsShared AndAlso
                [property].DeclaredAccessibility = Accessibility.Public AndAlso
                [property].Parameters.Length = 0 AndAlso
                [property].GetMethod IsNot Nothing
        End Function

        Private Shared Function MemberType(member As Symbol) As TypeSymbol
            Dim field = TryCast(member, FieldSymbol)
            If field IsNot Nothing Then
                Return field.Type
            End If

            Return DirectCast(member, PropertySymbol).Type
        End Function

        Private Shared Function ReadMember(factory As SyntheticBoundNodeFactory, receiver As BoundExpression, member As Symbol) As BoundExpression
            Dim field = TryCast(member, FieldSymbol)
            If field IsNot Nothing Then
                Return factory.Field(receiver, field, isLValue:=False)
            End If

            Return factory.Call(receiver, DirectCast(member, PropertySymbol).GetMethod)
        End Function

        Private Shared Function Concat(factory As SyntheticBoundNodeFactory, stringType As TypeSymbol, left As BoundExpression, right As BoundExpression) As BoundExpression
            Return factory.Binary(BinaryOperatorKind.Concatenate, stringType, left, right)
        End Function

        Private Function NothingLiteral(factory As SyntheticBoundNodeFactory, type As TypeSymbol) As BoundLiteral
            Return New BoundLiteral(m_SyntaxNode, ConstantValue.Nothing, type).MakeCompilerGenerated()
        End Function
    End Class

End Namespace
