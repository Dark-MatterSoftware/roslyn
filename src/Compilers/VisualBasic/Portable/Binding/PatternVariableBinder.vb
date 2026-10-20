' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic
    ''' <summary>
    ''' Hosts the locals that type tests name with Into, such as Case Is TypeOf T Into name, for the
    ''' block where the names are in scope.
    ''' </summary>
    Friend NotInheritable Class PatternVariableBinder
        Inherits BlockBaseBinder

        Private ReadOnly _declarations As ImmutableArray(Of (Identifier As SyntaxToken, Type As TypeSyntax))
        Private _locals As ImmutableArray(Of LocalSymbol) = Nothing

        Public Sub New(enclosing As Binder, declarations As ImmutableArray(Of (Identifier As SyntaxToken, Type As TypeSyntax)))
            MyBase.New(enclosing)
            Debug.Assert(Not declarations.IsEmpty)
            _declarations = declarations
        End Sub

        ''' <summary>
        ''' The names that the clauses of a Case statement declare with Into.
        ''' </summary>
        Public Shared Function GetDeclarations(caseStatement As CaseStatementSyntax) As ImmutableArray(Of (Identifier As SyntaxToken, Type As TypeSyntax))
            If caseStatement.Kind <> SyntaxKind.CaseStatement Then
                Return ImmutableArray(Of (Identifier As SyntaxToken, Type As TypeSyntax)).Empty
            End If

            Dim builder = ArrayBuilder(Of (Identifier As SyntaxToken, Type As TypeSyntax)).GetInstance()
            For Each clause In caseStatement.Cases
                Dim typeClause = TryCast(clause, TypeCaseClauseSyntax)
                If typeClause IsNot Nothing AndAlso typeClause.IntoKeyword.Kind = SyntaxKind.IntoKeyword AndAlso
                   Not typeClause.Identifier.IsMissing AndAlso typeClause.Identifier.Kind = SyntaxKind.IdentifierToken Then
                    builder.Add((typeClause.Identifier, typeClause.Type))
                End If
            Next

            Return builder.ToImmutableAndFree()
        End Function

        Friend Overrides ReadOnly Property Locals As ImmutableArray(Of LocalSymbol)
            Get
                If _locals.IsDefault Then
                    ImmutableInterlocked.InterlockedCompareExchange(_locals, BuildLocals(), Nothing)
                End If

                Return _locals
            End Get
        End Property

        Private Function BuildLocals() As ImmutableArray(Of LocalSymbol)
            ' The tested type is bound outside this binder, so binding it does not look up these locals.
            Return _declarations.SelectAsArray(
                Function(declaration)
                    Dim type = ContainingBinder.BindTypeSyntax(declaration.Type, BindingDiagnosticBag.Discarded)
                    Return LocalSymbol.Create(ContainingMember, Me, declaration.Identifier, LocalDeclarationKind.Variable, type)
                End Function)
        End Function

        ''' <summary>
        ''' The local declared by the given Into name.
        ''' </summary>
        Public Function GetLocal(identifier As SyntaxToken) As LocalSymbol
            For Each local In Locals
                If local.IdentifierToken = identifier Then
                    Return local
                End If
            Next

            Return Nothing
        End Function
    End Class
End Namespace
