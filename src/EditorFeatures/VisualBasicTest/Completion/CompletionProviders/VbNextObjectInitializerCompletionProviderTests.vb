' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.VisualBasic.Completion.Providers

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Completion.CompletionProviders
    <Trait(Traits.Feature, Traits.Features.Completion)>
    Public Class VbNextObjectInitializerCompletionProviderTests
        Inherits AbstractVisualBasicCompletionProviderTests

        Friend Overrides Function GetCompletionProviderType() As Type
            Return GetType(ObjectInitializerCompletionProvider)
        End Function

        Private Shared Function Code(languageVersion As String, statement As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""" & languageVersion & """>
        <Document>
Structure Point
    Public Left As Integer
    Public Top As Integer
    Public Property Label As String
    ReadOnly Property Total As Integer
        Get
            Return Left + Top
        End Get
    End Property
End Structure

Record Class Person(Name As String, Age As Integer)
End Class

Module M
    Sub Use(p As Point, person As Person)
        Dim a = New With {.Size = 1, Key .Id = 2}
        " & statement & "
    End Sub
End Module
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function MembersAfterWithOnStructure() As Task
            Dim text = Code("vbnext", "Dim q = p With {.$$")
            Await VerifyItemExistsAsync(text, "Left")
            Await VerifyItemExistsAsync(text, "Label")
            Await VerifyItemIsAbsentAsync(text, "Total")
        End Function

        <Fact>
        Public Async Function SetMembersMissingAfterWithOnStructure() As Task
            Dim text = Code("vbnext", "Dim q = p With {.Left = 1, .$$")
            Await VerifyItemIsAbsentAsync(text, "Left")
            Await VerifyItemExistsAsync(text, "Top")
        End Function

        <Fact>
        Public Async Function PropertiesAfterWithOnAnonymousType() As Task
            Dim text = Code("vbnext", "Dim b = a With {.$$")
            Await VerifyItemExistsAsync(text, "Size")
            Await VerifyItemExistsAsync(text, "Id")
        End Function

        <Fact>
        Public Async Function PropertiesAfterWithOnRecord() As Task
            Dim text = Code("vbnext", "Dim older = person With {.$$")
            Await VerifyItemExistsAsync(text, "Age")
        End Function

        <Fact>
        Public Async Function NothingAfterWithOn17_13() As Task
            Await VerifyNoItemsExistAsync(Code("17.13", "Dim q = p With {.$$"))
        End Function
    End Class
End Namespace
