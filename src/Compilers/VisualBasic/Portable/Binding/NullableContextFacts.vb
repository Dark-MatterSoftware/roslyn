' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Runtime.CompilerServices
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Nullable context for a syntax tree. <c>#Nullable</c> replaces the project switch from that point on.
    ''' <c>Restore</c> returns to the project switch.
    ''' </summary>
    Friend NotInheritable Class NullableContextMap
        Private Shared ReadOnly s_cache As New ConditionalWeakTable(Of SyntaxTree, NullableContextMap)

        Private ReadOnly _project As NullableContextOptions
        Private ReadOnly _positions As Integer()
        Private ReadOnly _contexts As NullableContextOptions()

        Private Sub New(project As NullableContextOptions, positions As Integer(), contexts As NullableContextOptions())
            _project = project
            _positions = positions
            _contexts = contexts
        End Sub

        Public ReadOnly Property HasDirective As Boolean
            Get
                Return _positions.Length > 0
            End Get
        End Property

        Public Shared Function ForTree(tree As SyntaxTree, project As NullableContextOptions) As NullableContextMap
            Dim cached As NullableContextMap = Nothing
            If s_cache.TryGetValue(tree, cached) AndAlso cached._project = project Then
                Return cached
            End If

            Dim created = Create(tree, project)
            Try
                s_cache.Add(tree, created)
            Catch ex As ArgumentException
            End Try

            Return created
        End Function

        Public Function GetContext(position As Integer) As NullableContextOptions
            Dim context = _project
            For i = 0 To _positions.Length - 1
                If _positions(i) <= position Then
                    context = _contexts(i)
                Else
                    Exit For
                End If
            Next

            Return context
        End Function

        Private Shared Function Create(tree As SyntaxTree, project As NullableContextOptions) As NullableContextMap
            Dim positions As New List(Of Integer)()
            Dim contexts As New List(Of NullableContextOptions)()
            Collect(tree.GetRoot(), project, positions, contexts)
            Return New NullableContextMap(project, positions.ToArray(), contexts.ToArray())
        End Function

        Private Shared Sub Collect(node As SyntaxNode, project As NullableContextOptions, positions As List(Of Integer), contexts As List(Of NullableContextOptions))
            CollectTrivia(node.GetLeadingTrivia(), project, positions, contexts)
            CollectTrivia(node.GetTrailingTrivia(), project, positions, contexts)

            For Each child In node.ChildNodesAndTokens()
                If child.IsNode Then
                    Collect(child.AsNode(), project, positions, contexts)
                Else
                    CollectTrivia(child.GetLeadingTrivia(), project, positions, contexts)
                    CollectTrivia(child.GetTrailingTrivia(), project, positions, contexts)
                End If
            Next
        End Sub

        Private Shared Sub CollectTrivia(triviaList As SyntaxTriviaList, project As NullableContextOptions, positions As List(Of Integer), contexts As List(Of NullableContextOptions))
            For Each trivia In triviaList
                If trivia.Kind = SyntaxKind.DisabledTextTrivia Then
                    Continue For
                End If

                If trivia.Kind = SyntaxKind.NullableDirectiveTrivia Then
                    Dim directive = DirectCast(trivia.GetStructure(), NullableDirectiveTriviaSyntax)
                    Dim setting = ContextOf(directive.SettingKeyword.ValueText, project)
                    If setting.HasValue Then
                        positions.Add(trivia.SpanStart)
                        contexts.Add(setting.Value)
                    End If
                End If
            Next
        End Sub

        Private Shared Function ContextOf(setting As String, project As NullableContextOptions) As NullableContextOptions?
            If String.Equals(setting, "Enable", StringComparison.OrdinalIgnoreCase) Then
                Return NullableContextOptions.Enable
            ElseIf String.Equals(setting, "Disable", StringComparison.OrdinalIgnoreCase) Then
                Return NullableContextOptions.Disable
            ElseIf String.Equals(setting, "Warnings", StringComparison.OrdinalIgnoreCase) Then
                Return NullableContextOptions.Warnings
            ElseIf String.Equals(setting, "Annotations", StringComparison.OrdinalIgnoreCase) Then
                Return NullableContextOptions.Annotations
            ElseIf String.Equals(setting, "Restore", StringComparison.OrdinalIgnoreCase) Then
                Return project
            End If

            Return Nothing
        End Function
    End Class

    Friend Module NullableContextFacts
        Public Function ForTree(tree As SyntaxTree, project As NullableContextOptions) As NullableContextMap
            Return NullableContextMap.ForTree(tree, project)
        End Function
    End Module

End Namespace
