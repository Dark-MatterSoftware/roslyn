' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    Friend NotInheritable Class ParameterWellKnownAttributeData
        Inherits CommonParameterWellKnownAttributeData

        Private _hasAllowNullAttribute As Boolean
        Friend Property HasAllowNullAttribute As Boolean
            Get
                VerifySealed(expected:=True)
                Return _hasAllowNullAttribute
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                _hasAllowNullAttribute = value
                SetDataStored()
            End Set
        End Property

        Private _hasDisallowNullAttribute As Boolean
        Friend Property HasDisallowNullAttribute As Boolean
            Get
                VerifySealed(expected:=True)
                Return _hasDisallowNullAttribute
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                _hasDisallowNullAttribute = value
                SetDataStored()
            End Set
        End Property

        Private _hasMaybeNullAttribute As Boolean
        Friend Property HasMaybeNullAttribute As Boolean
            Get
                VerifySealed(expected:=True)
                Return _hasMaybeNullAttribute
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                _hasMaybeNullAttribute = value
                SetDataStored()
            End Set
        End Property

        Private _maybeNullWhenAttribute As Boolean?
        Friend Property MaybeNullWhenAttribute As Boolean?
            Get
                VerifySealed(expected:=True)
                Return _maybeNullWhenAttribute
            End Get
            Set(value As Boolean?)
                VerifySealed(expected:=False)
                _maybeNullWhenAttribute = value
                SetDataStored()
            End Set
        End Property

        Private _hasNotNullAttribute As Boolean
        Friend Property HasNotNullAttribute As Boolean
            Get
                VerifySealed(expected:=True)
                Return _hasNotNullAttribute
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                _hasNotNullAttribute = value
                SetDataStored()
            End Set
        End Property

        Private _notNullWhenAttribute As Boolean?
        Friend Property NotNullWhenAttribute As Boolean?
            Get
                VerifySealed(expected:=True)
                Return _notNullWhenAttribute
            End Get
            Set(value As Boolean?)
                VerifySealed(expected:=False)
                _notNullWhenAttribute = value
                SetDataStored()
            End Set
        End Property

        Private _doesNotReturnIfAttribute As Boolean?
        Friend Property DoesNotReturnIfAttribute As Boolean?
            Get
                VerifySealed(expected:=True)
                Return _doesNotReturnIfAttribute
            End Get
            Set(value As Boolean?)
                VerifySealed(expected:=False)
                _doesNotReturnIfAttribute = value
                SetDataStored()
            End Set
        End Property

        Private _notNullIfParameterNotNull As ImmutableHashSet(Of String) = ImmutableHashSet(Of String).Empty
        Friend ReadOnly Property NotNullIfParameterNotNull As ImmutableHashSet(Of String)
            Get
                VerifySealed(expected:=True)
                Return _notNullIfParameterNotNull
            End Get
        End Property

        Friend Sub AddNotNullIfParameterNotNull(parameterName As String)
            VerifySealed(expected:=False)
            If parameterName Is Nothing Then
                Return
            End If

            _notNullIfParameterNotNull = _notNullIfParameterNotNull.Add(parameterName)
            SetDataStored()
        End Sub
    End Class
End Namespace
