' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Remote.Testing

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.FindReferences
    <Trait(Traits.Feature, Traits.Features.FindReferences)>
    Partial Public Class FindReferencesTests
        <WpfTheory, CombinatorialData>
        Public Async Function TestVbNextCallIndexedFromTheEnd(kind As TestKind, host As TestHost) As Task
            Dim input =
<Workspace>
    <Project Language="Visual Basic" CommonReferencesNet9="true" LanguageVersion="vbnext">
        <Document>
Imports System.Collections.Generic

Module M
    Function {|Definition:Make|}() As List(Of Integer)
        Return New List(Of Integer)
    End Function

    Sub Run()
        Dim a = [|Ma$$ke|]()(^3)
        [|Make|]()(^2) += 5
        Dim b = [|Make|]()(2)
    End Sub
End Module
        </Document>
    </Project>
</Workspace>
            Await TestAPIAndFeature(input, kind, host)
        End Function

        <WpfTheory, CombinatorialData>
        Public Async Function TestVbNextNamesInAnIndexFromTheEndAndASlice(kind As TestKind, host As TestHost) As Task
            Dim input =
<Workspace>
    <Project Language="Visual Basic" CommonReferencesNet9="true" LanguageVersion="vbnext">
        <Document>
Imports System.Collections.Generic

Class C
    Private items As New List(Of Integer)

    ReadOnly Property {|Definition:Depth|} As Integer
        Get
            Return 1
        End Get
    End Property

    Sub Run()
        Dim e = items(^[|Dep$$th|])
        Dim g = items(1..[|Depth|])
        Dim f = items([|Depth|])
    End Sub
End Class
        </Document>
    </Project>
</Workspace>
            Await TestAPIAndFeature(input, kind, host)
        End Function
    End Class
End Namespace
