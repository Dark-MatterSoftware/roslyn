' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class CollectionExpressionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub CollectionExpression_TargetsArray()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer() = (1, 2, 3)
        System.Console.Write(values(0))
        System.Console.Write(values(1))
        System.Console.Write(values(2))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="123").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_EmptyArray()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer() = ()
        System.Console.Write(values.Length)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_NestedArray()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer()() = ((1, 2), (3))
        System.Console.Write(values(0)(1))
        System.Console.Write(values(1)(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="23").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_ParenthesesAroundIdentifier()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim name = ""ok""
        Dim values As String() = (name)
        System.Console.Write(values(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_TupleWithoutTargetStaysTuple()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values = (1, 2)
        System.Console.Write(values.Item1)
        System.Console.Write(values.Item2)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="12").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_EscapedIdentifierStaysIdentifier()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim [Stop] = 4
        System.Console.Write([Stop])
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_EmptyRequiresTargetType()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values = ()
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_CollectionExpressionRequiresTarget, "()").WithLocation(4, 22))
        End Sub

        <Fact>
        Public Sub CollectionExpression_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer() = (1, 2)
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TypeMismatch2, "(1, 2)").WithArguments("(Integer, Integer)", "Integer()").WithLocation(4, 35))
        End Sub

        <Fact>
        Public Sub CollectionExpression_TargetsList()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        Dim values As List(Of Integer) = (1, 2, 3)
        System.Console.Write(values(0))
        System.Console.Write(values(1))
        System.Console.Write(values(2))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="123").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_EmptyList()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        Dim values As List(Of Integer) = ()
        System.Console.Write(values.Count)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_NestedList()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        Dim values As List(Of List(Of Integer)) = ((1, 2), (3))
        System.Console.Write(values(0).Count)
        System.Console.Write(values(1)(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="23").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_ParenthesesAroundValue()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        Dim name = 4
        Dim values As List(Of Integer) = (name)
        System.Console.Write(values(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_ExistingListStaysList()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        Dim existing As New List(Of Integer) From {7}
        Dim values As List(Of Integer) = (existing)
        System.Console.Write(values.Count)
        System.Console.Write(values(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="17").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_AddOnlyTypeIsNotACollection()
            Dim compilation = CreateCompilation("
Class Bucket
    Public Sub Add(value As Integer)
    End Sub
End Class
Module Program
    Sub Main()
        Dim values As Bucket = (1, 2)
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TypeMismatch2, "(1, 2)").WithArguments("(Integer, Integer)", "Bucket").WithLocation(8, 32))
        End Sub

        <Fact>
        Public Sub CollectionExpression_CollectionRequiresParameterlessConstructor()
            Dim compilation = CreateCompilation("
Class NeedsArg
    Implements System.Collections.IEnumerable
    Public Sub New(capacity As Integer)
    End Sub
    Public Sub Add(value As Integer)
    End Sub
    Public Function GetEnumerator() As System.Collections.IEnumerator Implements System.Collections.IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class
Module Program
    Sub Main()
        Dim values As NeedsArg = (1, 2)
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TypeMismatch2, "(1, 2)").WithArguments("(Integer, Integer)", "NeedsArg").WithLocation(14, 34))
        End Sub

        <Fact>
        Public Sub CollectionExpression_ListRejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        Dim values As List(Of Integer) = (1, 2)
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TypeMismatch2, "(1, 2)").WithArguments("(Integer, Integer)", "System.Collections.Generic.List(Of Integer)").WithLocation(5, 42))
        End Sub

        <Fact>
        Public Sub CollectionExpression_SpreadIntoArray()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim extras = {8, 9}
        Dim values As Integer() = (1, .. extras, 4)
        System.Console.Write(values(0))
        System.Console.Write(values(1))
        System.Console.Write(values(2))
        System.Console.Write(values(3))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1894").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_SpreadIntoList()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Module Program
    Sub Main()
        Dim extras As New List(Of Integer) From {8, 9}
        Dim values As List(Of Integer) = (1, .. extras, 4)
        System.Console.Write(values(0))
        System.Console.Write(values(1))
        System.Console.Write(values(2))
        System.Console.Write(values(3))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1894").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionExpression_ParenthesizedRangeIsNotSpread()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim values As Integer() = (1, (..4))
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_RangeRequiresArray, "..4").WithLocation(4, 40))
        End Sub
    End Class
End Namespace
