' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    Friend NotInheritable Class ReturnTypeWellKnownAttributeData
        Inherits CommonReturnTypeWellKnownAttributeData

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
