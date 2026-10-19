' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Structure
    Friend Class ExtensionBlockStructureProvider
        Inherits AbstractSyntaxNodeStructureProvider(Of ExtensionBlockSyntax)

        Protected Overrides Sub CollectBlockSpans(previousToken As SyntaxToken,
                                                  block As ExtensionBlockSyntax,
                                                  spans As ArrayBuilder(Of BlockSpan),
                                                  options As BlockStructureOptions,
                                                  cancellationToken As CancellationToken)
            CollectCommentsRegions(block, spans, options)

            If Not block.EndExtensionStatement.IsMissing Then
                spans.AddIfNotNull(CreateBlockSpanFromBlock(
                    block, bannerNode:=block.ExtensionStatement, autoCollapse:=False,
                    type:=BlockTypes.Type, isCollapsible:=True))

                CollectCommentsRegions(block.EndExtensionStatement, spans, options)
            End If
        End Sub
    End Class
End Namespace
