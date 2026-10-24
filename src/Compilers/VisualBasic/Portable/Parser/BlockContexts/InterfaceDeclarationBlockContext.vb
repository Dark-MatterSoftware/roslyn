' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
'-----------------------------------------------------------------------------
' Contains the definition of the BlockContext
'-----------------------------------------------------------------------------

Namespace Microsoft.CodeAnalysis.VisualBasic.Syntax.InternalSyntax

    Friend NotInheritable Class InterfaceDeclarationBlockContext
        Inherits TypeBlockContext

        Friend Sub New(statement As StatementSyntax, prevContext As BlockContext)
            MyBase.New(SyntaxKind.InterfaceBlock, statement, prevContext)

            Debug.Assert(BlockKind = SyntaxKind.InterfaceBlock)
        End Sub

        Friend Overrides Function ProcessSyntax(node As VisualBasicSyntaxNode) As BlockContext

            ' davidsch - This replaces ParseInterfaceGroupStatement and ParseInterfaceMember
            Dim kind As SyntaxKind = node.Kind

            Do
                Select Case _state
                    Case SyntaxKind.None
                        Select Case node.Kind
                            Case SyntaxKind.InheritsStatement
                                _state = SyntaxKind.InheritsStatement

                            Case Else
                                _state = SyntaxKind.InterfaceStatement ' done with inherits
                        End Select

                    Case SyntaxKind.InheritsStatement
                        Select Case node.Kind
                            Case SyntaxKind.InheritsStatement
                                Add(node)
                                Return Me

                            Case Else
                                _inheritsDecls = BaseDeclarations(Of InheritsStatementSyntax)()
                                _state = SyntaxKind.InterfaceStatement ' done with inherits
                        End Select

                    Case Else
                        Exit Do

                End Select
            Loop

            ' we are in the interface's body
            Debug.Assert(_state = SyntaxKind.InterfaceStatement)

            Select Case kind
                Case SyntaxKind.SubStatement, SyntaxKind.FunctionStatement
                    ' On vbnext a body is a default implementation. An abstract member stays a statement
                    ' when the next member is another declaration, so existing interfaces keep their shape.
                    If Parser.Options.LanguageVersion >= LanguageVersion.VbNext AndAlso
                       Not DirectCast(node, MethodBaseSyntax).Modifiers.Any(SyntaxKind.MustOverrideKeyword) Then
                        Dim methodBlockKind = If(kind = SyntaxKind.SubStatement, SyntaxKind.SubBlock, SyntaxKind.FunctionBlock)
                        Return New InterfaceMethodBlockContext(methodBlockKind, DirectCast(node, StatementSyntax), Me)
                    End If

                    Add(node)

                Case _
                    SyntaxKind.DelegateSubStatement,
                    SyntaxKind.DelegateFunctionStatement,
                    SyntaxKind.EmptyStatement
                    Add(node)

                Case SyntaxKind.IncompleteMember
                    ' Incomplete members are always an error.
                    node = Parser.ReportSyntaxError(node, ERRID.ERR_InterfaceMemberSyntax)
                    Add(node)

                Case SyntaxKind.PropertyStatement
                    ' Properties in interfaces cannot be initialized
                    Dim propertyStatement = PropertyBlockContext.ReportErrorIfHasInitializer(DirectCast(node, PropertyStatementSyntax))

                    ' On vbnext a Shared Overridable property has Get and Set bodies, its default implementation.
                    If Parser.Options.LanguageVersion >= LanguageVersion.VbNext AndAlso
                       propertyStatement.Modifiers.Any(SyntaxKind.SharedKeyword) AndAlso
                       propertyStatement.Modifiers.Any(SyntaxKind.OverridableKeyword) Then
                        Return New PropertyBlockContext(propertyStatement, Me, isPropertyBlock:=False)
                    End If

                    Add(propertyStatement)

                Case SyntaxKind.SubNewStatement
                    ' In Dev10 this is reported in Declared
                    Add(Parser.ReportSyntaxError(node, ERRID.ERR_NewInInterface))

                Case SyntaxKind.EventStatement
                    Dim eventStatement = DirectCast(node, EventStatementSyntax)
                    ' 'Custom' modifier invalid on event declared in an interface.
                    If eventStatement.CustomKeyword IsNot Nothing Then
                        eventStatement = Parser.ReportSyntaxError(eventStatement, ERRID.ERR_CustomEventInvInInterface)
                    End If
                    Add(eventStatement)

                Case SyntaxKind.EnumStatement
                    Return New EnumDeclarationBlockContext(DirectCast(node, StatementSyntax), Me)

                Case SyntaxKind.ClassStatement
                    Return New TypeBlockContext(SyntaxKind.ClassBlock, DirectCast(node, StatementSyntax), Me)

                Case SyntaxKind.StructureStatement
                    Return New TypeBlockContext(SyntaxKind.StructureBlock, DirectCast(node, StatementSyntax), Me)

                Case SyntaxKind.InterfaceStatement
                    Return New InterfaceDeclarationBlockContext(DirectCast(node, StatementSyntax), Me)

                Case SyntaxKind.FieldDeclaration
                    Add(Parser.ReportSyntaxError(node, ERRID.ERR_InterfaceMemberSyntax))

                Case SyntaxKind.LabelStatement
                    node = Parser.ReportSyntaxError(node, ERRID.ERR_InvOutsideProc)
                    Add(node)

                Case SyntaxKind.EnumBlock,
                    SyntaxKind.ClassBlock,
                    SyntaxKind.StructureBlock,
                    SyntaxKind.InterfaceBlock,
                    SyntaxKind.SubBlock,
                    SyntaxKind.FunctionBlock,
                    SyntaxKind.PropertyBlock
                    ' Handle any block that can be created by this context
                    Add(node)

                Case _
                     SyntaxKind.EndSubStatement,
                     SyntaxKind.EndFunctionStatement,
                     SyntaxKind.EndOperatorStatement,
                     SyntaxKind.EndPropertyStatement,
                     SyntaxKind.EndGetStatement,
                     SyntaxKind.EndSetStatement,
                     SyntaxKind.EndEventStatement,
                     SyntaxKind.EndAddHandlerStatement,
                     SyntaxKind.EndRemoveHandlerStatement,
                     SyntaxKind.EndRaiseEventStatement
                    Add(Parser.ReportSyntaxError(node, ERRID.ERR_InvInsideInterface))

                Case _
                    SyntaxKind.StructureStatement,
                    SyntaxKind.ClassStatement,
                    SyntaxKind.InterfaceStatement,
                    SyntaxKind.EnumStatement,
                    SyntaxKind.DelegateSubStatement,
                    SyntaxKind.NamespaceStatement

                    ' End the current block and add the block to the context above which should be able to handle this kind of statement.
                    Dim outerContext = EndBlock(Nothing)
                    Return outerContext.ProcessSyntax(Parser.ReportSyntaxError(node, ERRID.ERR_InvInsideEndsInterface))

                Case Else
                    Add(Parser.ReportSyntaxError(node, ERRID.ERR_InvInsideInterface))

            End Select

            Return Me
        End Function

        Friend Overrides Function TryLinkSyntax(node As VisualBasicSyntaxNode, ByRef newContext As BlockContext) As LinkResult
            newContext = Nothing

            If KindEndsBlock(node.Kind) Then
                Return UseSyntax(node, newContext)
            End If

            Select Case node.Kind

                Case _
                    SyntaxKind.DelegateSubStatement,
                    SyntaxKind.DelegateFunctionStatement,
                    SyntaxKind.EventStatement,
                    SyntaxKind.SubStatement,
                    SyntaxKind.SubNewStatement,
                    SyntaxKind.FunctionStatement,
                    SyntaxKind.PropertyStatement,
                    SyntaxKind.InheritsStatement,
                    SyntaxKind.EndSubStatement,
                    SyntaxKind.EndFunctionStatement,
                    SyntaxKind.EndOperatorStatement,
                    SyntaxKind.EndPropertyStatement,
                    SyntaxKind.EndGetStatement,
                    SyntaxKind.EndSetStatement,
                    SyntaxKind.EndEventStatement,
                    SyntaxKind.EndAddHandlerStatement,
                    SyntaxKind.EndRemoveHandlerStatement,
                    SyntaxKind.EndRaiseEventStatement
                    Return UseSyntax(node, newContext)

                Case _
                    SyntaxKind.ClassBlock,
                    SyntaxKind.StructureBlock,
                    SyntaxKind.InterfaceBlock
                    Return UseSyntax(node, newContext, DirectCast(node, TypeBlockSyntax).EndBlockStatement.IsMissing)

                Case SyntaxKind.SubBlock, SyntaxKind.FunctionBlock
                    ' Before vbnext an interface method has no body, so a method block cannot be reused here.
                    If Parser.Options.LanguageVersion < LanguageVersion.VbNext Then
                        newContext = Me
                        Return LinkResult.Crumble
                    End If

                    Return UseSyntax(node, newContext, DirectCast(node, MethodBlockBaseSyntax).End.IsMissing)

                Case SyntaxKind.PropertyBlock
                    ' Before vbnext an interface property has no body, so a property block cannot be reused here.
                    If Parser.Options.LanguageVersion < LanguageVersion.VbNext Then
                        newContext = Me
                        Return LinkResult.Crumble
                    End If

                    Return UseSyntax(node, newContext, DirectCast(node, PropertyBlockSyntax).EndPropertyStatement.IsMissing)

                Case SyntaxKind.EnumBlock
                    Return UseSyntax(node, newContext, DirectCast(node, EnumBlockSyntax).EndEnumStatement.IsMissing)

                Case Else
                    newContext = Me
                    Return LinkResult.Crumble
            End Select
        End Function

        Friend Overrides Function RecoverFromMismatchedEnd(statement As StatementSyntax) As BlockContext
            Debug.Assert(statement IsNot Nothing)
            ' The end construct is extraneous. Report an error and leave
            ' the current context alone.

            Dim stmtKind = statement.Kind

            Select Case (stmtKind)

                Case SyntaxKind.EndSubStatement,
                      SyntaxKind.EndFunctionStatement,
                      SyntaxKind.EndOperatorStatement,
                      SyntaxKind.EndPropertyStatement,
                      SyntaxKind.EndGetStatement,
                      SyntaxKind.EndSetStatement,
                      SyntaxKind.EndEventStatement,
                      SyntaxKind.EndAddHandlerStatement,
                      SyntaxKind.EndRemoveHandlerStatement,
                      SyntaxKind.EndRaiseEventStatement
                    ' Don't call the base RecoverFromMismatchedEnd which will
                    ' add the wrong error. Let the process syntax add the error message.
                    Return ProcessSyntax(statement)

                Case Else
                    Return MyBase.RecoverFromMismatchedEnd(statement)

            End Select

        End Function

    End Class

End Namespace
