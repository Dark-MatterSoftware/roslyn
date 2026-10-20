' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.EndConstructGeneration
    <[UseExportProvider]>
    <Trait(Traits.Feature, Traits.Features.EndConstructGeneration)>
    Public Class VbNextBlockTests
        Private Shared ReadOnly s_vbNext As VisualBasicParseOptions = VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VbNext)

        <WpfFact>
        Public Async Function TestApplyAfterRecordClassStatement() As Task
            Await VerifyStatementEndConstructAppliedAsync(
                before:="Record Class Point",
                beforeCaret:={0, -1},
                after:="Record Class Point

End Class",
                afterCaret:={1, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function TestApplyAfterRecordStructureStatement() As Task
            Await VerifyStatementEndConstructAppliedAsync(
                before:="Record Structure Point",
                beforeCaret:={0, -1},
                after:="Record Structure Point

End Structure",
                afterCaret:={1, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function TestApplyAfterExtensionStatement() As Task
            Await VerifyStatementEndConstructAppliedAsync(
                before:="Module M
    Extension(s As String)
End Module",
                beforeCaret:={1, -1},
                after:="Module M
    Extension(s As String)

    End Extension
End Module",
                afterCaret:={2, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function DoNotApplyForMatchedExtension() As Task
            Await VerifyStatementEndConstructNotAppliedAsync(
                text:="Module M
    Extension(s As String)
    End Extension
End Module",
                caret:={1, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function TestApplyAfterSelectCaseExpression() As Task
            Await VerifyStatementEndConstructAppliedAsync(
                before:="Module M
    Sub Main(order As Integer)
        Dim label = Select Case order
    End Sub
End Module",
                beforeCaret:={2, -1},
                after:="Module M
    Sub Main(order As Integer)
        Dim label = Select Case order

        End Select
    End Sub
End Module",
                afterCaret:={3, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function DoNotApplyForMatchedSelectCaseExpression() As Task
            Await VerifyStatementEndConstructNotAppliedAsync(
                text:="Module M
    Sub Main(order As Integer)
        Dim label = Select Case order
            Case Else
                ""x""
        End Select
    End Sub
End Module",
                caret:={2, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function DoNotApplyAfterCompactFunction() As Task
            Await VerifyStatementEndConstructNotAppliedAsync(
                text:="Module M
    Function Twice(x As Integer) As Integer = Return x * 2
End Module",
                caret:={1, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function DoNotApplyAfterCompactSub() As Task
            Await VerifyStatementEndConstructNotAppliedAsync(
                text:="Module M
    Sub Log(message As String) = Call System.Console.WriteLine(message)
End Module",
                caret:={1, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function DoNotApplyAfterCompactProperty() As Task
            Await VerifyStatementEndConstructNotAppliedAsync(
                text:="Class C
    Private _name As String
    ReadOnly Property Name As String = Get _name
End Class",
                caret:={2, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function DoNotApplyAfterUsingDeclaration() As Task
            Await VerifyStatementEndConstructNotAppliedAsync(
                text:="Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
    End Sub
End Class",
                caret:={2, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function ApplyAfterUsingStatementOn17_13() As Task
            Await VerifyStatementEndConstructAppliedAsync(
                before:="Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()
    End Sub
End Class",
                beforeCaret:={2, -1},
                after:="Class C
    Sub M()
        Using stream = New System.IO.MemoryStream()

        End Using
    End Sub
End Class",
                afterCaret:={3, -1},
                parseOptions:=VisualBasicParseOptions.Default.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
        End Function

        <WpfFact>
        Public Async Function TestApplyAfterAwaitEachStatement() As Task
            Await VerifyStatementEndConstructAppliedAsync(
                before:="Class C
    Async Sub M()
        Await Each item In Items()
    End Sub
End Class",
                beforeCaret:={2, -1},
                after:="Class C
    Async Sub M()
        Await Each item In Items()

        Next
    End Sub
End Class",
                afterCaret:={3, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function DoNotApplyAfterAsyncUsingDeclaration() As Task
            Await VerifyStatementEndConstructNotAppliedAsync(
                text:="Class C
    Async Sub M()
        Async Using stream = Open()
    End Sub
End Class",
                caret:={2, -1},
                parseOptions:=s_vbNext)
        End Function

        <WpfFact>
        Public Async Function TestApplyAfterAsyncIteratorFunction() As Task
            Await VerifyStatementEndConstructAppliedAsync(
                before:="Class C
    Async Iterator Function Items() As System.Collections.Generic.IAsyncEnumerable(Of Integer)
End Class",
                beforeCaret:={1, -1},
                after:="Class C
    Async Iterator Function Items() As System.Collections.Generic.IAsyncEnumerable(Of Integer)

    End Function
End Class",
                afterCaret:={2, -1},
                parseOptions:=s_vbNext)
        End Function
    End Class
End Namespace
