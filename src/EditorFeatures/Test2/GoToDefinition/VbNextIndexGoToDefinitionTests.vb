' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.GoToDefinition
    ''' <summary>
    ''' Go to definition on the names in the receiver and the operand of an index from the end or a slice.
    ''' </summary>
    <[UseExportProvider]>
    <Trait(Traits.Feature, Traits.Features.GoToDefinition)>
    Public Class VbNextIndexGoToDefinitionTests
        Inherits GoToDefinitionTestsBase

        <WpfFact>
        Public Async Function TestOnACallIndexedFromTheEnd() As Task
            Dim workspace =
<Workspace>
    <Project Language="Visual Basic" CommonReferencesNet9="true" LanguageVersion="vbnext">
        <Document>
Imports System.Collections.Generic

Module M
    Function [|Make|]() As List(Of Integer)
        Return New List(Of Integer)
    End Function

    Sub Run()
        Dim a = Ma$$ke()(^3)
    End Sub
End Module
        </Document>
    </Project>
</Workspace>

            Await TestAsync(workspace)
        End Function

        <WpfFact>
        Public Async Function TestOnACallAssignedThroughAnIndexFromTheEnd() As Task
            Dim workspace =
<Workspace>
    <Project Language="Visual Basic" CommonReferencesNet9="true" LanguageVersion="vbnext">
        <Document>
Imports System.Collections.Generic

Module M
    Function [|Make|]() As List(Of Integer)
        Return New List(Of Integer)
    End Function

    Sub Run()
        Ma$$ke()(^2) += 5
    End Sub
End Module
        </Document>
    </Project>
</Workspace>

            Await TestAsync(workspace)
        End Function

        <WpfFact>
        Public Async Function TestOnAPropertyInTheOperandOfAnIndexFromTheEnd() As Task
            Dim workspace =
<Workspace>
    <Project Language="Visual Basic" CommonReferencesNet9="true" LanguageVersion="vbnext">
        <Document>
Imports System.Collections.Generic

Class C
    Private items As New List(Of Integer)

    ReadOnly Property [|Depth|] As Integer
        Get
            Return 1
        End Get
    End Property

    Sub Run()
        Dim e = items(^De$$pth)
    End Sub
End Class
        </Document>
    </Project>
</Workspace>

            Await TestAsync(workspace)
        End Function

        <WpfFact>
        Public Async Function TestOnAFieldThatASliceReceives() As Task
            Dim workspace =
<Workspace>
    <Project Language="Visual Basic" CommonReferencesNet9="true" LanguageVersion="vbnext">
        <Document>
Imports System.Collections.Generic

Class C
    Private [|items|] As New List(Of Integer)

    Sub Run()
        Dim g = ite$$ms(1..^1)
    End Sub
End Class
        </Document>
    </Project>
</Workspace>

            Await TestAsync(workspace)
        End Function
    End Class
End Namespace
