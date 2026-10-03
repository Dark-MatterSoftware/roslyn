' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    Friend NotInheritable Class SynthesizedRecordPositionalPropertySymbol
        Inherits SynthesizedPropertyBase

        Private ReadOnly _container As NamedTypeSymbol
        Private ReadOnly _name As String
        Private ReadOnly _type As TypeSymbol
        Private ReadOnly _syntax As VisualBasicSyntaxNode
        Private _backingField As FieldSymbol
        Private _getMethod As MethodSymbol
        Private _setMethod As MethodSymbol

        Public Sub New(syntax As VisualBasicSyntaxNode, container As NamedTypeSymbol, name As String, type As TypeSymbol)
            _syntax = syntax
            _container = container
            _name = name
            _type = type
        End Sub

        Friend Sub SetBackingField(field As FieldSymbol)
            _backingField = field
        End Sub

        Friend Sub SetAccessors(getter As MethodSymbol, setter As MethodSymbol)
            _getMethod = getter
            _setMethod = setter
        End Sub

        Public Overrides ReadOnly Property Name As String
            Get
                Return _name
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
                Return _setMethod
            End Get
        End Property

        Friend Overrides ReadOnly Property AssociatedField As FieldSymbol
            Get
                Return _backingField
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
                Return False
            End Get
        End Property
    End Class

    Friend NotInheritable Class SynthesizedRecordPositionalBackingFieldSymbol
        Inherits SynthesizedBackingFieldBase(Of SynthesizedRecordPositionalPropertySymbol)

        Public Sub New([property] As SynthesizedRecordPositionalPropertySymbol)
            MyBase.New([property], "_" & [property].Name, isShared:=False)
        End Sub

        Public Overrides ReadOnly Property Type As TypeSymbol
            Get
                Return _propertyOrEvent.Type
            End Get
        End Property
    End Class

    Friend NotInheritable Class SynthesizedRecordPositionalAccessorSymbol
        Inherits SynthesizedPropertyAccessorBase(Of SynthesizedRecordPositionalPropertySymbol)

        Private ReadOnly _methodKind As Microsoft.CodeAnalysis.MethodKind
        Private ReadOnly _parameters As ImmutableArray(Of ParameterSymbol)

        Public Sub New(container As NamedTypeSymbol, [property] As SynthesizedRecordPositionalPropertySymbol, kind As Microsoft.CodeAnalysis.MethodKind)
            MyBase.New(container, [property])
            _methodKind = kind
            If kind = Microsoft.CodeAnalysis.MethodKind.PropertySet Then
                _parameters = ImmutableArray.Create(Of ParameterSymbol)(SynthesizedParameterSymbol.CreateSetAccessorValueParameter(Me, [property], "Value"))
            Else
                _parameters = ImmutableArray(Of ParameterSymbol).Empty
            End If
        End Sub

        Friend Overrides ReadOnly Property BackingFieldSymbol As FieldSymbol
            Get
                Return PropertyOrEvent.AssociatedField
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

        Public Overrides ReadOnly Property MethodKind As Microsoft.CodeAnalysis.MethodKind
            Get
                Return _methodKind
            End Get
        End Property

        Public Overrides ReadOnly Property IsSub As Boolean
            Get
                Return _methodKind = Microsoft.CodeAnalysis.MethodKind.PropertySet
            End Get
        End Property

        Public Overrides ReadOnly Property IsInitOnly As Boolean
            Get
                Return _methodKind = Microsoft.CodeAnalysis.MethodKind.PropertySet
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnType As TypeSymbol
            Get
                If _methodKind = Microsoft.CodeAnalysis.MethodKind.PropertyGet Then
                    Return PropertyOrEvent.Type
                End If

                Return DeclaringCompilation.GetSpecialType(SpecialType.System_Void)
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return _parameters
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnTypeCustomModifiers As ImmutableArray(Of CustomModifier)
            Get
                If Not IsInitOnly Then
                    Return ImmutableArray(Of CustomModifier).Empty
                End If

                Dim modifierType = DeclaringCompilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_IsExternalInit)
                Return ImmutableArray.Create(Of CustomModifier)(VisualBasicCustomModifier.CreateRequired(modifierType))
            End Get
        End Property
    End Class

End Namespace
