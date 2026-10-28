' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' The <c>Protected Overridable ReadOnly Property EqualityContract As Type</c> of a record class, the name and shape C# 9 uses,
    ''' so records of different runtime types are not equal and either language can derive from the other's records.
    ''' </summary>
    Friend NotInheritable Class SynthesizedRecordEqualityContractSymbol
        Inherits SynthesizedPropertyBase

        Friend Const PropertyName As String = "EqualityContract"

        Private ReadOnly _container As NamedTypeSymbol
        Private ReadOnly _type As TypeSymbol
        Private ReadOnly _syntax As VisualBasicSyntaxNode
        Private ReadOnly _overriddenProperty As PropertySymbol
        Private ReadOnly _getMethod As MethodSymbol

        Public Sub New(syntax As VisualBasicSyntaxNode, container As NamedTypeSymbol, type As TypeSymbol, overriddenProperty As PropertySymbol)
            _syntax = syntax
            _container = container
            _type = type
            _overriddenProperty = overriddenProperty
            _getMethod = New SynthesizedRecordEqualityContractGetterSymbol(container, Me)
        End Sub

        Friend ReadOnly Property BaseProperty As PropertySymbol
            Get
                Return _overriddenProperty
            End Get
        End Property

        Friend Overrides ReadOnly Property OverriddenMembers As OverriddenMembersResult(Of PropertySymbol)
            Get
                If _overriddenProperty Is Nothing Then
                    Return OverriddenMembersResult(Of PropertySymbol).Empty
                End If

                Return OverriddenMembersResult(Of PropertySymbol).Create(ImmutableArray.Create(_overriddenProperty),
                                                                       ImmutableArray(Of PropertySymbol).Empty,
                                                                       ImmutableArray(Of PropertySymbol).Empty)
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return PropertyName
            End Get
        End Property

        Public Overrides ReadOnly Property Type As TypeSymbol
            Get
                Return _type
            End Get
        End Property

        Public Overrides ReadOnly Property GetMethod As MethodSymbol
            Get
                Return _getMethod
            End Get
        End Property

        Public Overrides ReadOnly Property SetMethod As MethodSymbol
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return Accessibility.Protected
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverridable As Boolean
            Get
                Return _overriddenProperty Is Nothing AndAlso Not _container.IsNotInheritable
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverrides As Boolean
            Get
                Return _overriddenProperty IsNot Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverloads As Boolean
            Get
                Return _overriddenProperty IsNot Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return _container
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return ImmutableArray.Create(_syntax.GetLocation())
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return ImmutableArray(Of SyntaxReference).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
            Get
                Return True
            End Get
        End Property

        Friend ReadOnly Property Syntax As VisualBasicSyntaxNode
            Get
                Return _syntax
            End Get
        End Property
    End Class

    Friend NotInheritable Class SynthesizedRecordEqualityContractGetterSymbol
        Inherits SynthesizedAccessor(Of SynthesizedRecordEqualityContractSymbol)

        Public Sub New(container As NamedTypeSymbol, [property] As SynthesizedRecordEqualityContractSymbol)
            MyBase.New(container, [property])
        End Sub

        Public Overrides ReadOnly Property MethodKind As MethodKind
            Get
                Return MethodKind.PropertyGet
            End Get
        End Property

        Public Overrides ReadOnly Property IsSub As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnType As TypeSymbol
            Get
                Return PropertyOrEvent.Type
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return ImmutableArray(Of ParameterSymbol).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property OverriddenMethod As MethodSymbol
            Get
                Return PropertyOrEvent.BaseProperty?.GetMethod
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

        Friend Overrides Function GetBoundMethodBody(compilationState As TypeCompilationState, diagnostics As BindingDiagnosticBag, Optional ByRef methodBodyBinder As Binder = Nothing) As BoundBlock
            methodBodyBinder = Nothing
            Dim factory As New SyntheticBoundNodeFactory(Me, Me, PropertyOrEvent.Syntax, compilationState, diagnostics)
            Return factory.Block(factory.Return(factory.Typeof(ContainingType, PropertyOrEvent.Type)))
        End Function
    End Class

End Namespace
