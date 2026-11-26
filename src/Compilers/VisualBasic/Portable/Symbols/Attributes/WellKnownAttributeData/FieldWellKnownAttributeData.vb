' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    Friend NotInheritable Class FieldWellKnownAttributeData
        Inherits CommonFieldWellKnownAttributeData

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
    End Class
End Namespace
