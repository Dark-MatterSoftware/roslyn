' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Structure
    Friend Class SelectCaseExpressionStructureProvider
        Inherits AbstractSyntaxNodeStructureProvider(Of SelectCaseExpressionSyntax)

        Protected Overrides Sub CollectBlockSpans(previousToken As SyntaxToken,
                                                  node As SelectCaseExpressionSyntax,
                                                  spans As ArrayBuilder(Of BlockSpan),
                                                  options As BlockStructureOptions,
                                                  cancellationToken As CancellationToken)
            If node.EndKeyword.IsMissing Then
                Return
            End If

            Dim header = node.SelectKeyword.ToString()
            If Not node.CaseKeyword.IsKind(SyntaxKind.None) Then
                header &= " " & node.CaseKeyword.ToString()
            End If

            spans.AddIfNotNull(CreateBlockSpanFromBlock(
                node, bannerText:=header & " " & node.Expression.ConvertToSingleLine().ToString() & SpaceEllipsis,
                autoCollapse:=False, type:=BlockTypes.Conditional, isCollapsible:=True))
        End Sub
    End Class
End Namespace
