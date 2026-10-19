' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces
Imports Microsoft.CodeAnalysis.Formatting
Imports Xunit.Abstractions

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Formatting.Indentation
    <[UseExportProvider]>
    <Trait(Traits.Feature, Traits.Features.SmartIndent)>
    Public Class VbNextSmartIndenterTests
        Inherits VisualBasicFormatterTestBase

        Private Shared ReadOnly s_vbNext As VisualBasicParseOptions = VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)

        Public Sub New(output As ITestOutputHelper)
            MyBase.New(output)
        End Sub

        <WpfFact>
        Public Sub InsideExtensionBlock()
            AssertSmartIndent("
Module M
    Extension(s As String)

    End Extension
End Module
", indentationLine:=3, expectedIndentation:=8)
        End Sub

        <WpfFact>
        Public Sub AfterUnfinishedExtensionHeader()
            AssertSmartIndent("
Module M
    Extension(s As String)

End Module
", indentationLine:=3, expectedIndentation:=8)
        End Sub

        <WpfFact>
        Public Sub AfterExtensionMember()
            AssertSmartIndent("
Module M
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return s.Length
            End Get
        End Property

    End Extension
End Module
", indentationLine:=8, expectedIndentation:=8)
        End Sub

        <WpfFact>
        Public Sub AfterSelectCaseExpressionHeader()
            AssertSmartIndent("
Module M
    Sub Main(order As Integer)
        Dim label = Select Case order

                    End Select
    End Sub
End Module
", indentationLine:=4, expectedIndentation:=24)
        End Sub

        <WpfFact>
        Public Sub AfterSelectCaseExpressionCase()
            AssertSmartIndent("
Module M
    Sub Main(order As Integer)
        Dim label = Select Case order
                        Case 1

                    End Select
    End Sub
End Module
", indentationLine:=5, expectedIndentation:=28)
        End Sub

        <WpfFact>
        Public Sub AfterSelectCaseExpressionArmValue()
            AssertSmartIndent("
Module M
    Sub Main(order As Integer)
        Dim label = Select Case order
                        Case 1
                            ""one""

                    End Select
    End Sub
End Module
", indentationLine:=6, expectedIndentation:=28)
        End Sub

        ''' <param name="indentationLine">0-based. The line number in code to get indentation for.</param>
        Private Sub AssertSmartIndent(code As String, indentationLine As Integer, expectedIndentation As Integer?)
            Using workspace = EditorTestWorkspace.CreateVisualBasic(code, parseOptions:=s_vbNext)
                TestIndentation(workspace, indentationLine, expectedIndentation, FormattingOptions2.IndentStyle.Smart, useTabs:=False)
            End Using
        End Sub
    End Class
End Namespace
