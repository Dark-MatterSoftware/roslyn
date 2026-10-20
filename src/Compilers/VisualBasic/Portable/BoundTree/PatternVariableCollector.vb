' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    ''' <summary>
    ''' Finds the locals that type tests name with Into in a condition, not counting nested lambdas.
    ''' </summary>
    Friend NotInheritable Class PatternVariableCollector
        Inherits BoundTreeWalkerWithStackGuard

        Private ReadOnly _locals As ArrayBuilder(Of LocalSymbol)

        Private Sub New(locals As ArrayBuilder(Of LocalSymbol))
            _locals = locals
        End Sub

        Public Shared Function Collect(node As BoundNode) As ImmutableArray(Of LocalSymbol)
            If node Is Nothing Then
                Return ImmutableArray(Of LocalSymbol).Empty
            End If

            Dim locals = ArrayBuilder(Of LocalSymbol).GetInstance()
            Dim collector = New PatternVariableCollector(locals)
            collector.Visit(node)
            Return locals.ToImmutableAndFree()
        End Function

        Public Shared Function Collect(caseStatement As BoundCaseStatement) As ImmutableArray(Of LocalSymbol)
            Dim locals = ArrayBuilder(Of LocalSymbol).GetInstance()
            Dim collector = New PatternVariableCollector(locals)
            For Each clause In caseStatement.CaseClauses
                collector.Visit(clause)
            Next

            Return locals.ToImmutableAndFree()
        End Function

        Public Overrides Function VisitTypeOfDeclaration(node As BoundTypeOfDeclaration) As BoundNode
            _locals.Add(node.Local)
            Return MyBase.VisitTypeOfDeclaration(node)
        End Function

        Public Overrides Function VisitLambda(node As BoundLambda) As BoundNode
            Return Nothing
        End Function
    End Class
End Namespace
