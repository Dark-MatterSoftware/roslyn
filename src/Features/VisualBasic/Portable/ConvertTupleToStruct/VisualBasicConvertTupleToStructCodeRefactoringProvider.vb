' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Composition
Imports System.Diagnostics.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.ConvertTupleToStruct
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.ConvertTupleToStruct
    <ExtensionOrder(Before:=PredefinedCodeRefactoringProviderNames.IntroduceVariable)>
    <ExportLanguageService(GetType(IConvertTupleToStructCodeRefactoringProvider), LanguageNames.VisualBasic)>
    <ExportCodeRefactoringProvider(LanguageNames.VisualBasic, Name:=PredefinedCodeRefactoringProviderNames.ConvertTupleToStruct), [Shared]>
    Friend Class VisualBasicConvertTupleToStructCodeRefactoringProvider
        Inherits AbstractConvertTupleToStructCodeRefactoringProvider(Of
            ExpressionSyntax,
            NameSyntax,
            IdentifierNameSyntax,
            LiteralExpressionSyntax,
            ObjectCreationExpressionSyntax,
            TupleExpressionSyntax,
            ArgumentSyntax,
            TupleTypeSyntax,
            TypeBlockSyntax,
            NamespaceBlockSyntax)

        <ImportingConstructor>
        <SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification:="Used in test code: https://github.com/dotnet/roslyn/issues/42814")>
        Public Sub New()
        End Sub

        Protected Overrides Function GetArgumentWithChangedName(argument As ArgumentSyntax, name As String) As ArgumentSyntax
            Dim simpleArgument = TryCast(argument, SimpleArgumentSyntax)

            If simpleArgument Is Nothing Then
                Return argument
            End If

            Dim nameColonEquals = simpleArgument.NameColonEquals

            Return simpleArgument.WithNameColonEquals(ChangeName(nameColonEquals, name))
        End Function

        ''' <summary>
        ''' On vbnext, a tuple that a deconstruction assigns to, a tuple that = or &lt;&gt; compares, and the tuple an Imports
        ''' alias names stay tuples.
        ''' </summary>
        Protected Overrides Function CanConvert(tupleExpressionOrType As SyntaxNode) As Boolean
            Dim current = tupleExpressionOrType
            While current.Parent IsNot Nothing
                Dim parent = current.Parent
                Select Case parent.Kind
                    Case SyntaxKind.ParenthesizedExpression, SyntaxKind.SimpleArgument, SyntaxKind.TupleExpression
                        current = parent
                        Continue While
                    Case SyntaxKind.SimpleAssignmentStatement
                        Return DirectCast(parent, AssignmentStatementSyntax).Left IsNot current
                    Case SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression
                        Return False
                End Select

                Exit While
            End While

            Return tupleExpressionOrType.GetAncestor(Of ImportsClauseSyntax)() Is Nothing
        End Function

        Private Shared Function ChangeName(nameColonEquals As NameColonEqualsSyntax, name As String) As NameColonEqualsSyntax
            If nameColonEquals Is Nothing Then
                Return Nothing
            End If

            Dim newName = SyntaxFactory.IdentifierName(name).WithTriviaFrom(nameColonEquals.Name)
            Return nameColonEquals.WithName(newName)
        End Function
    End Class
End Namespace
