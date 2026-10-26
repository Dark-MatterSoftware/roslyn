' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE

    ''' <summary>
    ''' A property that a C# 14 extension block declares, read as VB reads its own extension properties: a Shared property
    ''' of the enclosing type whose accessors are the implementation methods C# emits there. An instance extension
    ''' property takes the receiver as its first parameter; a static one is a static extension member of the receiver's type.
    ''' </summary>
    Friend NotInheritable Class PEExtensionPropertySymbol
        Inherits SynthesizedPropertyBase

        Private ReadOnly _containingType As NamedTypeSymbol
        Private ReadOnly _declaration As PropertySymbol
        Private ReadOnly _getMethod As MethodSymbol
        Private ReadOnly _setMethod As MethodSymbol
        Private ReadOnly _extendedType As TypeSymbol
        Private ReadOnly _isStatic As Boolean

        Public Sub New(containingType As NamedTypeSymbol, declaration As PropertySymbol, getMethod As MethodSymbol, setMethod As MethodSymbol, extendedType As TypeSymbol)
            Debug.Assert(getMethod IsNot Nothing OrElse setMethod IsNot Nothing)
            _containingType = containingType
            _declaration = declaration
            _getMethod = getMethod
            _setMethod = setMethod
            _extendedType = extendedType
            _isStatic = declaration.IsShared
        End Sub

        Public Overrides ReadOnly Property Name As String
            Get
                Return _declaration.Name
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return _containingType
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return _containingType
            End Get
        End Property

        Public Overrides ReadOnly Property Type As TypeSymbol
            Get
                If _getMethod IsNot Nothing Then
                    Return _getMethod.ReturnType
                End If

                Return _setMethod.Parameters(_setMethod.ParameterCount - 1).Type
            End Get
        End Property

        ''' <summary>
        ''' The type parameters of a generic extension block, as the accessors declare them. Empty once constructed.
        ''' </summary>
        Friend ReadOnly Property ExtensionTypeParameters As ImmutableArray(Of TypeParameterSymbol)
            Get
                Dim accessor = If(_getMethod, _setMethod)
                Return If(accessor.IsDefinition, accessor.TypeParameters, ImmutableArray(Of TypeParameterSymbol).Empty)
            End Get
        End Property

        ''' <summary>
        ''' The type that a generic extension block extends, as the accessors declare it.
        ''' </summary>
        Friend ReadOnly Property ExtendedType As TypeSymbol
            Get
                Return _extendedType
            End Get
        End Property

        ''' <summary>
        ''' The property of a generic extension block for the type arguments inferred from the receiver.
        ''' </summary>
        Friend Function Construct(typeArguments As ImmutableArray(Of TypeSymbol)) As PEExtensionPropertySymbol
            Dim typeParameters = ExtensionTypeParameters
            Dim substitution = TypeSubstitution.Create(If(_getMethod, _setMethod), typeParameters, typeArguments)
            Return New PEExtensionPropertySymbol(_containingType,
                                                 _declaration,
                                                 _getMethod?.Construct(typeArguments),
                                                 _setMethod?.Construct(typeArguments),
                                                 _extendedType.InternalSubstituteTypeParameters(substitution).Type)
        End Function

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                If _getMethod IsNot Nothing Then
                    Return _getMethod.Parameters
                End If

                Dim setterParameters = _setMethod.Parameters
                Return setterParameters.RemoveAt(setterParameters.Length - 1)
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

        Public Overrides ReadOnly Property IsShared As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return _declaration.DeclaredAccessibility
            End Get
        End Property

        Friend Overrides ReadOnly Property IsExtensionProperty As Boolean
            Get
                Return Not _isStatic
            End Get
        End Property

        Friend Overrides ReadOnly Property IsStaticExtensionMember As Boolean
            Get
                Return _isStatic
            End Get
        End Property

        Friend Overrides ReadOnly Property StaticExtensionType As TypeSymbol
            Get
                Return If(_isStatic, _extendedType, Nothing)
            End Get
        End Property

        Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return _containingType.Locations
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return ImmutableArray(Of SyntaxReference).Empty
            End Get
        End Property

        Friend Overrides ReadOnly Property CallingConvention As Microsoft.Cci.CallingConvention
            Get
                Return Microsoft.Cci.CallingConvention.Default
            End Get
        End Property
    End Class

End Namespace
