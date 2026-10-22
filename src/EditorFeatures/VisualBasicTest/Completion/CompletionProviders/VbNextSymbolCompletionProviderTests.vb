' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.VisualBasic.Completion.Providers

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Completion.CompletionProviders
    <Trait(Traits.Feature, Traits.Features.Completion)>
    Public Class VbNextSymbolCompletionProviderTests
        Inherits AbstractVisualBasicCompletionProviderTests

        Friend Overrides Function GetCompletionProviderType() As Type
            Return GetType(SymbolCompletionProvider)
        End Function

        Private Shared Function Shapes(languageVersion As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""" & languageVersion & """>
        <Document>
Interface IShape(Of TSelf As IShape(Of TSelf))
    Shared MustOverride Function Create() As TSelf
    Function Name() As String
End Interface

Module M
    Function Make(Of T As IShape(Of T))() As T
        Return T.$$
    End Function
End Module
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function SharedMustOverrideMemberAfterTypeParameter() As Task
            Await VerifyItemExistsAsync(Shapes("vbnext"), "Create")
        End Function

        <Fact>
        Public Async Function InstanceMemberMissingAfterTypeParameter() As Task
            Await VerifyItemIsAbsentAsync(Shapes("vbnext"), "Name")
        End Function

        <Fact>
        Public Async Function SharedMemberMissingAfterTypeParameterOn17_13() As Task
            Await VerifyItemIsAbsentAsync(Shapes("17.13"), "Create")
        End Function
    End Class
End Namespace
