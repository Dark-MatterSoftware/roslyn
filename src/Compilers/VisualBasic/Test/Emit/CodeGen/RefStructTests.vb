' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RefStructTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ByRefStructure_ReadsAndWritesField()
            Dim compilation = CreateCompilation("
ByRef Structure Buffer
    Public Value As Integer
End Structure

ByRef Structure Pair
    Public Item As Buffer
End Structure

Module Program
    Sub Main()
        Dim item As Pair
        item.Item.Value = 4
        System.Console.Write(item.Item.Value)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefStructure_RejectsFieldOfClass()
            Dim compilation = CreateCompilation("
ByRef Structure Buffer
    Public Value As Integer
End Structure

Class Holder
    Public Item As Buffer
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ByRefStructField2, "Buffer").WithArguments("Buffer", "Holder").WithLocation(7, 20))
        End Sub

        <Fact>
        Public Sub ByRefStructure_RejectsBoxing()
            Dim compilation = CreateCompilation("
ByRef Structure Buffer
    Public Value As Integer
End Structure

Module Program
    Sub Main()
        Dim item As Buffer
        Dim boxed As Object = item
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TypeMismatch2, "item").WithArguments("Buffer", "Object").WithLocation(9, 31))
        End Sub

        <Fact>
        Public Sub ByRefStructure_RejectsIteratorLocal()
            Dim compilation = CreateCompilation("
ByRef Structure Buffer
    Public Value As Integer
End Structure

Module Program
    Iterator Function Run() As System.Collections.Generic.IEnumerable(Of Integer)
        Dim item As Buffer
        item.Value = 1
        Yield item.Value
    End Function
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_CannotLiftRestrictedTypeResumable1, "Buffer").WithArguments("Buffer").WithLocation(8, 21))
        End Sub

        <Fact>
        Public Sub ByRefStructure_RejectedOn17_13()
            Dim compilation = CreateCompilation("
ByRef Structure Buffer
    Public Value As Integer
End Structure
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_Syntax, "ByRef").WithLocation(2, 1),
                Diagnostic(ERRID.ERR_InvalidInNamespace, "Public Value As Integer").WithLocation(3, 5),
                Diagnostic(ERRID.ERR_EndStructureNoStructure, "End Structure").WithLocation(4, 1))
        End Sub

        <Fact>
        Public Sub Span_LocalIsLegalOnVbNext()
            Dim compilation = CreateCompilation("
Imports System
Module Program
    Sub Main()
        Dim values As Span(Of Integer) = Nothing
        System.Console.Write(values.Length)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext, targetFramework:=TargetFramework.Net70)

            compilation.VerifyDiagnostics()
            compilation.VerifyEmitDiagnostics()
        End Sub

        <Fact>
        Public Sub Span_RejectsBoxingAndClassField()
            Dim compilation = CreateCompilation("
Imports System
Class Holder
    Public Item As Span(Of Integer)
End Class

Module Program
    Sub Main()
        Dim values As Span(Of Integer) = Nothing
        Dim boxed As Object = values
    End Sub
End Module
", parseOptions:=VbNext, targetFramework:=TargetFramework.Net70)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ByRefStructField2, "Span(Of Integer)").WithArguments("System.Span(Of Integer)", "Holder").WithLocation(4, 20),
                Diagnostic(ERRID.ERR_TypeMismatch2, "values").WithArguments("System.Span(Of Integer)", "Object").WithLocation(10, 31))
        End Sub

        <Fact>
        Public Sub Span_StillObsoleteOn17_13()
            Dim compilation = CreateCompilation("
Imports System
Module Program
    Sub Main()
        Dim values As Span(Of Integer) = Nothing
    End Sub
End Module
", parseOptions:=Vb17_13, targetFramework:=TargetFramework.Net70)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_UseOfObsoleteSymbol2, "Span(Of Integer)").WithArguments("System.Span(Of Integer)", "Types with embedded references are not supported in this version of your compiler.").WithLocation(5, 23))
        End Sub

        <Fact>
        Public Sub Span_IndexAndSliceCompile()
            Dim compilation = CreateCompilation("
Imports System
Module Program
    Sub Main()
        Dim values As Span(Of Integer) = Nothing
        Dim last = values(^1)
        Dim middle = values(1..4)
        values(^1) = 9
        Dim text As ReadOnlySpan(Of Char) = Nothing
        Dim lastChar = text(^1)
        Dim piece = text(1..3)
    End Sub
End Module
", parseOptions:=VbNext, targetFramework:=TargetFramework.Net70)

            compilation.VerifyDiagnostics()
            compilation.VerifyEmitDiagnostics()
        End Sub
    End Class
End Namespace
