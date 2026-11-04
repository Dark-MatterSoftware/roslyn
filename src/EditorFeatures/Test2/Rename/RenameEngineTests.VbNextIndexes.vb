' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.Rename
    Partial Public Class RenameEngineTests
        <Theory, CombinatorialData>
        Public Sub RenameVbNextCallIndexedFromTheEnd(host As RenameTestHost)
            Using result = RenameEngineResult.Create(_outputHelper,
                <Workspace>
                    <Project Language="Visual Basic" CommonReferencesNet9="true" LanguageVersion="vbnext">
                        <Document>
Imports System.Collections.Generic

Module M
    Function [|$$Make|]() As List(Of Integer)
        Return New List(Of Integer)
    End Function

    ReadOnly Property Items As List(Of Integer)
        Get
            Return [|Make|]()
        End Get
    End Property

    Sub Run()
        Dim a = [|Make|]()(^3)
        [|Make|]()(^2) += 5
        Dim b = [|Make|]()(2)
        Dim c = Items(^1)
    End Sub
End Module
                        </Document>
                    </Project>
                </Workspace>, host:=host, renameTo:="Build")
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameVbNextNamesInAnIndexFromTheEndAndASlice(host As RenameTestHost)
            Using result = RenameEngineResult.Create(_outputHelper,
                <Workspace>
                    <Project Language="Visual Basic" CommonReferencesNet9="true" LanguageVersion="vbnext">
                        <Document>
Imports System.Collections.Generic

Class C
    Private items As New List(Of Integer)

    ReadOnly Property [|$$Depth|] As Integer
        Get
            Return 1
        End Get
    End Property

    Sub Run()
        Dim e = items(^[|Depth|])
        Dim g = items(1..[|Depth|])
        Dim f = items([|Depth|])
    End Sub
End Class
                        </Document>
                    </Project>
                </Workspace>, host:=host, renameTo:="Level")
            End Using
        End Sub
    End Class
End Namespace
