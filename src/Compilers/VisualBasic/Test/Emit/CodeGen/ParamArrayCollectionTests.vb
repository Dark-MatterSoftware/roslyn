' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ParamArrayCollectionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ParamArray_ListExpansion()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        WriteList(1, 2, 3)
        WriteList()
        Dim existing As New List(Of Integer) From {4}
        WriteList(existing)
    End Sub

    Sub WriteList(ParamArray values As List(Of Integer))
        System.Console.Write(values.Count)
        For Each value In values
            System.Console.Write(value)
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3123014").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ParamArray_IEnumerableExpansion()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        WriteValues(1, 2, 3)
        Dim existing = {4, 5}
        WriteValues(existing)
    End Sub

    Sub WriteValues(ParamArray values As IEnumerable(Of Integer))
        For Each value In values
            System.Console.Write(value)
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="12345").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ParamArray_GenericListExpansion()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        WriteValues(1, 2)
    End Sub

    Sub WriteValues(Of T)(ParamArray values As List(Of T))
        System.Console.Write(values.Count)
        For Each value In values
            System.Console.Write(value)
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="212").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ParamArray_ListRejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub WriteList(ParamArray values As List(Of Integer))
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ParamArrayNotArray, "values").WithLocation(4, 30))
        End Sub
    End Class
End Namespace
