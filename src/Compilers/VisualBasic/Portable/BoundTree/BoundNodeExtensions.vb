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

        ''' <summary>
        ''' Marks the nodes of a synthesized tree as compiler generated, except the nodes of the source trees that it
        ''' shares, which stay as the user wrote them.
        ''' </summary>
        <Extension()>
        Public Function MakeTreeCompilerGenerated(Of T As BoundNode)(this As T, ParamArray sourceTrees As BoundNode()) As T
            If this IsNot Nothing Then
                Dim sourceNodes = New HashSet(Of BoundNode)()
                Dim collector As New NodeCollector(sourceNodes)
                For Each tree In sourceTrees
                    collector.Visit(tree)
                Next

                Dim marker As New CompilerGeneratedMarker(sourceNodes)
                marker.Visit(this)
            End If

            Return this
        End Function

        Private NotInheritable Class CompilerGeneratedMarker
            Inherits BoundTreeWalkerWithStackGuard

            Private ReadOnly _sourceNodes As HashSet(Of BoundNode)

            Public Sub New(Optional sourceNodes As HashSet(Of BoundNode) = Nothing)
                _sourceNodes = sourceNodes
            End Sub

            Public Overrides Function Visit(node As BoundNode) As BoundNode
                If node Is Nothing OrElse (_sourceNodes IsNot Nothing AndAlso _sourceNodes.Contains(node)) Then
                    Return Nothing
                End If

                node.SetWasCompilerGenerated()
                Return MyBase.Visit(node)
            End Function
        End Class

        Private NotInheritable Class NodeCollector
            Inherits BoundTreeWalkerWithStackGuard

            Private ReadOnly _nodes As HashSet(Of BoundNode)

            Public Sub New(nodes As HashSet(Of BoundNode))
                _nodes = nodes
            End Sub

            Public Overrides Function Visit(node As BoundNode) As BoundNode
                If node Is Nothing OrElse Not _nodes.Add(node) Then
                    Return Nothing
                End If

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

