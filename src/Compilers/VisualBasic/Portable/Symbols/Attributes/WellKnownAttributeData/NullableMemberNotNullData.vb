' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Member names from <c>MemberNotNull</c> and <c>MemberNotNullWhen</c>.
    ''' </summary>
    Friend NotInheritable Class NullableMemberNotNullData
        Private _members As ImmutableArray(Of String) = ImmutableArray(Of String).Empty
        Private _whenTrue As ImmutableArray(Of String) = ImmutableArray(Of String).Empty
        Private _whenFalse As ImmutableArray(Of String) = ImmutableArray(Of String).Empty

        Friend Sub AddMember(memberName As String)
            _members = _members.Add(memberName)
        End Sub

        Friend Sub AddMembers(memberNames As ArrayBuilder(Of String))
            _members = _members.AddRange(memberNames)
        End Sub

        Friend ReadOnly Property Members As ImmutableArray(Of String)
            Get
                Return _members
            End Get
        End Property

        Friend Sub AddWhenMember(sense As Boolean, memberName As String)
            If sense Then
                _whenTrue = _whenTrue.Add(memberName)
            Else
                _whenFalse = _whenFalse.Add(memberName)
            End If
        End Sub

        Friend Sub AddWhenMembers(sense As Boolean, memberNames As ArrayBuilder(Of String))
            If sense Then
                _whenTrue = _whenTrue.AddRange(memberNames)
            Else
                _whenFalse = _whenFalse.AddRange(memberNames)
            End If
        End Sub

        Friend ReadOnly Property WhenTrueMembers As ImmutableArray(Of String)
            Get
                Return _whenTrue
            End Get
        End Property

        Friend ReadOnly Property WhenFalseMembers As ImmutableArray(Of String)
            Get
                Return _whenFalse
            End Get
        End Property
    End Class
End Namespace
