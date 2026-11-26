' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    Friend NotInheritable Class PropertyWellKnownAttributeData
        Inherits CommonPropertyWellKnownAttributeData
        Implements IMemberNotNullAttributeTarget

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

        Private _memberState As NullableMemberNotNullData

        Private Function State() As NullableMemberNotNullData
            If _memberState Is Nothing Then
                _memberState = New NullableMemberNotNullData()
            End If

            Return _memberState
        End Function

        Public Sub AddNotNullMember(memberName As String) Implements IMemberNotNullAttributeTarget.AddNotNullMember
            VerifySealed(expected:=False)
            State().AddMember(memberName)
            SetDataStored()
        End Sub

        Public Sub AddNotNullMember(memberNames As ArrayBuilder(Of String)) Implements IMemberNotNullAttributeTarget.AddNotNullMember
            VerifySealed(expected:=False)
            State().AddMembers(memberNames)
            SetDataStored()
        End Sub

        Public ReadOnly Property NotNullMembers As ImmutableArray(Of String) Implements IMemberNotNullAttributeTarget.NotNullMembers
            Get
                VerifySealed(expected:=True)
                If _memberState Is Nothing Then
                    Return ImmutableArray(Of String).Empty
                End If

                Return _memberState.Members
            End Get
        End Property

        Public Sub AddNotNullWhenMember(sense As Boolean, memberName As String) Implements IMemberNotNullAttributeTarget.AddNotNullWhenMember
            VerifySealed(expected:=False)
            State().AddWhenMember(sense, memberName)
            SetDataStored()
        End Sub

        Public Sub AddNotNullWhenMember(sense As Boolean, memberNames As ArrayBuilder(Of String)) Implements IMemberNotNullAttributeTarget.AddNotNullWhenMember
            VerifySealed(expected:=False)
            State().AddWhenMembers(sense, memberNames)
            SetDataStored()
        End Sub

        Public ReadOnly Property NotNullWhenTrueMembers As ImmutableArray(Of String) Implements IMemberNotNullAttributeTarget.NotNullWhenTrueMembers
            Get
                VerifySealed(expected:=True)
                If _memberState Is Nothing Then
                    Return ImmutableArray(Of String).Empty
                End If

                Return _memberState.WhenTrueMembers
            End Get
        End Property

        Public ReadOnly Property NotNullWhenFalseMembers As ImmutableArray(Of String) Implements IMemberNotNullAttributeTarget.NotNullWhenFalseMembers
            Get
                VerifySealed(expected:=True)
                If _memberState Is Nothing Then
                    Return ImmutableArray(Of String).Empty
                End If

                Return _memberState.WhenFalseMembers
            End Get
        End Property
    End Class
End Namespace
