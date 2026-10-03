' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
'-----------------------------------------------------------------------------
' Contains the definition of the BlockContext
'-----------------------------------------------------------------------------

Namespace Microsoft.CodeAnalysis.VisualBasic.Syntax.InternalSyntax

    ''' <summary>
    ''' A Sub or Function started inside an interface on vbnext.
    ''' A following declaration means the member is abstract and stays a statement.
    ''' Statements through End Sub or End Function are a default implementation.
    ''' </summary>
    Friend NotInheritable Class InterfaceMethodBlockContext
        Inherits MethodBlockContext

        Friend Sub New(contextKind As SyntaxKind, statement As StatementSyntax, prevContext As BlockContext)
            MyBase.New(contextKind, statement, prevContext)
        End Sub

        Friend Overrides Function ProcessSyntax(node As VisualBasicSyntaxNode) As BlockContext
            If Statements.Count = 0 AndAlso Parser.IsDeclarationStatement(node.Kind) Then
                Return FinishAsAbstractMember(node)
            End If

            Return MyBase.ProcessSyntax(node)
        End Function

        Friend Overrides Function EndBlock(endStmt As StatementSyntax) As BlockContext
            If endStmt Is Nothing AndAlso Statements.Count = 0 Then
                Dim context = PrevBlock
                context.Add(BeginStatement)
                FreeStatements()
                Return context
            End If

            Return MyBase.EndBlock(endStmt)
        End Function

        Private Function FinishAsAbstractMember(nextMember As VisualBasicSyntaxNode) As BlockContext
            Dim context = PrevBlock
            context.Add(BeginStatement)
            FreeStatements()
            Return context.ProcessSyntax(nextMember)
        End Function

    End Class

End Namespace
