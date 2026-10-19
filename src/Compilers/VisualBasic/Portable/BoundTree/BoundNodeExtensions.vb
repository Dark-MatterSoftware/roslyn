' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System
Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Runtime.CompilerServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    Friend Module BoundNodeExtensions

        'Return if any node in an array of nodes has errors.
        <Extension()>
        Public Function NonNullAndHasErrors(Of T As BoundNode)(nodeArray As ImmutableArray(Of T)) As Boolean
            If nodeArray.IsDefault Then
                Return False
            End If

            For i As Integer = 0 To nodeArray.Length - 1
                If nodeArray(i).HasErrors Then
                    Return True
                End If
            Next

            Return False
        End Function

        ' Like HasErrors property, but also returns false for a null node. 
        <Extension()>
        Public Function NonNullAndHasErrors(node As BoundNode) As Boolean
            Return node IsNot Nothing AndAlso node.HasErrors
        End Function

        <Extension()>
        Public Function MakeCompilerGenerated(Of T As BoundNode)(this As T) As T
            this.SetWasCompilerGenerated()
            Return this
        End Function

        ''' <summary>
        ''' Marks a node and every node below it as compiler generated. Use it for a tree that the binder
        ''' synthesizes from one piece of syntax, so that the tree adds no explicit node to the operation tree.
        ''' </summary>
        <Extension()>
        Public Function MakeTreeCompilerGenerated(Of T As BoundNode)(this As T) As T
            If this IsNot Nothing Then
                Dim marker As New CompilerGeneratedMarker()
                marker.Visit(this)
            End If

            Return this
        End Function

        Private NotInheritable Class CompilerGeneratedMarker
            Inherits BoundTreeWalkerWithStackGuard

            Public Overrides Function Visit(node As BoundNode) As BoundNode
                node?.SetWasCompilerGenerated()
                Return MyBase.Visit(node)
            End Function
        End Class

        ''' <summary>
        ''' Get the Binder from a lambda node, or return Nothing if this isn't 
        ''' a lambda node.
        ''' </summary>
        <Extension()>
        Public Function GetBinderFromLambda(boundNode As BoundNode) As Binder
            Select Case boundNode.Kind
                Case BoundKind.UnboundLambda
                    Return DirectCast(boundNode, UnboundLambda).Binder
                Case BoundKind.QueryLambda
                    Return DirectCast(boundNode, BoundQueryLambda).LambdaSymbol.ContainingBinder
                Case BoundKind.GroupTypeInferenceLambda
                    Return DirectCast(boundNode, GroupTypeInferenceLambda).Binder
                Case Else
                    Return Nothing
            End Select
        End Function

        ' Is this bound node any kind of lambda?
        <Extension()>
        Public Function IsAnyLambda(boundNode As BoundNode) As Boolean
            Dim kind = boundNode.Kind
            Return kind = BoundKind.UnboundLambda OrElse kind = BoundKind.Lambda OrElse kind = BoundKind.QueryLambda OrElse kind = BoundKind.GroupTypeInferenceLambda
        End Function
    End Module
End Namespace

