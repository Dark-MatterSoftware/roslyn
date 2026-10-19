' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Outlining
    <Trait(Traits.Feature, Traits.Features.Outlining)>
    Public Class SelectCaseExpressionStructureTests
        Inherits AbstractVisualBasicSyntaxNodeStructureProviderTests(Of SelectCaseExpressionSyntax)

        Protected Overrides ReadOnly Property ParseOptions As ParseOptions
            Get
                Return VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)
            End Get
        End Property

        Friend Overrides Function CreateProvider() As AbstractSyntaxStructureProvider
            Return New SelectCaseExpressionStructureProvider()
        End Function

        <Fact>
        Public Async Function TestSelectCaseExpression() As Task
            Const code = "
Module M
    Sub Main(order As Integer)
        Dim label = {|span:Select Case $$order
                        Case 1
                            ""one""
                        Case Else
                            ""other""
                    End Select|}
    End Sub
End Module
"

            Await VerifyBlockSpansAsync(code,
                Region("span", "Select Case order ...", autoCollapse:=False))
        End Function

        <Fact>
        Public Async Function TestSelectExpressionWithoutCase() As Task
            Const code = "
Module M
    Sub Main(order As Integer)
        Dim label = {|span:Select $$order
                        Case Else
                            ""other""
                    End Select|}
    End Sub
End Module
"

            Await VerifyBlockSpansAsync(code,
                Region("span", "Select order ...", autoCollapse:=False))
        End Function

        <Fact>
        Public Async Function TestUnfinishedSelectCaseExpression() As Task
            Const code = "
Module M
    Sub Main(order As Integer)
        Dim label = Select Case $$order
    End Sub
End Module
"

            Await VerifyNoBlockSpansAsync(code)
        End Function
    End Class
End Namespace
