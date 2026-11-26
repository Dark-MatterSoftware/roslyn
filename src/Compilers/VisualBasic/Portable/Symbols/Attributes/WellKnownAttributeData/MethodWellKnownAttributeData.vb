' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Information decoded from well-known custom attributes applied on a method.
    ''' </summary>
    Friend Class MethodWellKnownAttributeData
        Inherits CommonMethodWellKnownAttributeData
        Implements IMemberNotNullAttributeTarget

        Public Sub New()
            MyBase.New(preserveSigFirstWriteWins:=True)
        End Sub

#Region "STAThreadAttribute"
        Private _hasSTAThreadAttribute As Boolean = False
        Friend Property HasSTAThreadAttribute As Boolean
            Get
                VerifySealed(expected:=True)
                Return Me._hasSTAThreadAttribute
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                Me._hasSTAThreadAttribute = value
                SetDataStored()
            End Set
        End Property
#End Region

#Region "MTAThreadAttribute"
        Private _hasMTAThreadAttribute As Boolean = False
        Friend Property HasMTAThreadAttribute As Boolean
            Get
                VerifySealed(expected:=True)
                Return Me._hasMTAThreadAttribute
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                Me._hasMTAThreadAttribute = value
                SetDataStored()
            End Set
        End Property
#End Region

#Region "DebuggerHiddenAttribute"
        Private _isPropertyAccessorWithDebuggerHiddenAttribute As Boolean = False
        Friend Property IsPropertyAccessorWithDebuggerHiddenAttribute As Boolean
            Get
                VerifySealed(expected:=True)
                Return Me._isPropertyAccessorWithDebuggerHiddenAttribute
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                Me._isPropertyAccessorWithDebuggerHiddenAttribute = value
                SetDataStored()
            End Set
        End Property
#End Region

#Region "DoesNotReturnAttribute"
        Private _hasDoesNotReturnAttribute As Boolean
        Friend Property HasDoesNotReturnAttribute As Boolean
            Get
                VerifySealed(expected:=True)
                Return _hasDoesNotReturnAttribute
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                _hasDoesNotReturnAttribute = value
                SetDataStored()
            End Set
        End Property
#End Region

#Region "MemberNotNull"
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
#End Region

    End Class
End Namespace
