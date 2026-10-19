' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Outlining
    <Trait(Traits.Feature, Traits.Features.Outlining)>
    Public Class ExtensionBlockStructureTests
        Inherits AbstractVisualBasicSyntaxNodeStructureProviderTests(Of ExtensionBlockSyntax)

        Protected Overrides ReadOnly Property ParseOptions As ParseOptions
            Get
                Return VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)
            End Get
        End Property

        Friend Overrides Function CreateProvider() As AbstractSyntaxStructureProvider
            Return New ExtensionBlockStructureProvider()
        End Function

        <Fact>
        Public Async Function TestExtensionBlock() As Task
            Const code = "
Module M
    {|span:Extension(s As String) $$
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property
    End Extension|}
End Module
"

            Await VerifyBlockSpansAsync(code,
                Region("span", "Extension(s As String) ...", autoCollapse:=False))
        End Function

        <Fact>
        Public Async Function TestExtensionBlockWithLeadingComments() As Task
            Const code = "
Module M
    {|span1:' One
    ' Two|}
    {|span2:Extension(s As String) $$
    End Extension|}
End Module
"

            Await VerifyBlockSpansAsync(code,
                Region("span1", "' One ...", autoCollapse:=True),
                Region("span2", "Extension(s As String) ...", autoCollapse:=False))
        End Function

        <Fact>
        Public Async Function TestUnfinishedExtensionBlock() As Task
            Const code = "
Module M
    Extension(s As String) $$
End Module
"

            Await VerifyNoBlockSpansAsync(code)
        End Function
    End Class
End Namespace
