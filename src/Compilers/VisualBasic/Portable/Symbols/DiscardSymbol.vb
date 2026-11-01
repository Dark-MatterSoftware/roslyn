' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' An empty slot of a deconstruction, as operations report it.
    ''' </summary>
    Friend NotInheritable Class DiscardSymbol
        Inherits Symbol
        Implements IDiscardSymbol

        Private ReadOnly _type As TypeSymbol

        Public Sub New(type As TypeSymbol)
            _type = type
        End Sub

        Public ReadOnly Property Type As TypeSymbol
            Get
                Return _type
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "_"
            End Get
        End Property

        Public Overrides ReadOnly Property Kind As SymbolKind
            Get
                Return SymbolKind.Discard
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return Nothing
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

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return Accessibility.NotApplicable
            End Get
        End Property

        Public Overrides ReadOnly Property IsShared As Boolean
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

        Friend Overrides ReadOnly Property ObsoleteAttributeData As ObsoleteAttributeData
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides Function Equals(obj As Object) As Boolean
            Dim other = TryCast(obj, DiscardSymbol)
            Return other IsNot Nothing AndAlso TypeSymbol.Equals(_type, other._type, TypeCompareKind.ConsiderEverything)
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return _type.GetHashCode()
        End Function

        Friend Overloads Overrides Function Accept(Of TArgument, TResult)(visitor As VisualBasicSymbolVisitor(Of TArgument, TResult), arg As TArgument) As TResult
            Return visitor.DefaultVisit(Me, arg)
        End Function

#Region "IDiscardSymbol"
        Private ReadOnly Property IDiscardSymbol_Type As ITypeSymbol Implements IDiscardSymbol.Type
            Get
                Return _type
            End Get
        End Property

        Private ReadOnly Property IDiscardSymbol_NullableAnnotation As NullableAnnotation Implements IDiscardSymbol.NullableAnnotation
            Get
                Return NullableAnnotation.None
            End Get
        End Property
#End Region

#Region "ISymbol"
        Public Overrides Sub Accept(visitor As SymbolVisitor)
            visitor.VisitDiscard(Me)
        End Sub

        Public Overrides Function Accept(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
            Return visitor.VisitDiscard(Me)
        End Function

        Public Overrides Function Accept(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
            Return visitor.VisitDiscard(Me, argument)
        End Function

        Public Overrides Sub Accept(visitor As VisualBasicSymbolVisitor)
            visitor.DefaultVisit(Me)
        End Sub

        Public Overrides Function Accept(Of TResult)(visitor As VisualBasicSymbolVisitor(Of TResult)) As TResult
            Return visitor.DefaultVisit(Me)
        End Function
#End Region

    End Class

End Namespace
