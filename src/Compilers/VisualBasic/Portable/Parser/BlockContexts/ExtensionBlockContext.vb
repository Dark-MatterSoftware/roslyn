' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports InternalSyntaxFactory = Microsoft.CodeAnalysis.VisualBasic.Syntax.InternalSyntax.SyntaxFactory

Namespace Microsoft.CodeAnalysis.VisualBasic.Syntax.InternalSyntax

    Friend NotInheritable Class ExtensionBlockContext
        Inherits DeclarationContext

        Friend Sub New(statement As StatementSyntax, prevContext As BlockContext)
            MyBase.New(SyntaxKind.ExtensionBlock, statement, prevContext)
        End Sub

        Friend Overrides Function ProcessSyntax(node As VisualBasicSyntaxNode) As BlockContext
            Select Case node.Kind
                Case SyntaxKind.PropertyStatement
                    Return New PropertyBlockContext(DirectCast(node, PropertyStatementSyntax), Me, isPropertyBlock:=False)

                Case SyntaxKind.PropertyBlock
                    Add(node)

                Case SyntaxKind.SubStatement, SyntaxKind.FunctionStatement
                    ' A Shared method is a static member of the extended type. It is not an instance extension.
                    Dim methodBase = DirectCast(node, MethodBaseSyntax)
                    If methodBase.Modifiers.Any(SyntaxKind.SharedKeyword) AndAlso
                       Not methodBase.Modifiers.Any(SyntaxKind.MustOverrideKeyword) Then

                        If Not Parser.IsFirstStatementOnLine(node.GetFirstToken) Then
                            node = Parser.ReportSyntaxError(node, ERRID.ERR_MethodMustBeFirstStatementOnLine)
                            methodBase = DirectCast(node, MethodBaseSyntax)
                        End If

                        Dim methodBlockKind = If(node.Kind = SyntaxKind.SubStatement, SyntaxKind.SubBlock, SyntaxKind.FunctionBlock)
                        Return New MethodBlockContext(methodBlockKind, methodBase, Me)
                    End If

                    node = Parser.ReportSyntaxError(node, ERRID.ERR_ExpectedDeclaration)
                    Add(node)

                Case SyntaxKind.SubBlock, SyntaxKind.FunctionBlock
                    Add(node)

                Case Else
                    If Not SyntaxFacts.IsEndBlockLoopOrNextStatement(node.Kind) Then
                        node = Parser.ReportSyntaxError(node, ERRID.ERR_ExpectedDeclaration)
                    End If

                    Add(node)
            End Select

            Return Me
        End Function

        Friend Overrides Function CreateBlockSyntax(endStmt As StatementSyntax) As VisualBasicSyntaxNode
            Dim beginStmt As ExtensionStatementSyntax = Nothing
            Dim endBlockStmt As EndBlockStatementSyntax = DirectCast(endStmt, EndBlockStatementSyntax)
            GetBeginEndStatements(beginStmt, endBlockStmt)

            Dim result = InternalSyntaxFactory.ExtensionBlock(beginStmt, Body(), endBlockStmt)
            FreeStatements()
            Return result
        End Function

    End Class

End Namespace
