' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ForEachDeconstructionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Function CompileAndRun(source As String, expectedOutput As String, Optional options As VisualBasicCompilationOptions = Nothing) As CompilationVerifier
            Dim compilation = CreateCompilation(source, options:=If(options, TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True)), parseOptions:=VbNext)
            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput).VerifyDiagnostics()
        End Function

        Private Const Basics As String = "
Imports System
Imports System.Collections.Generic

Class Point
    Public ReadOnly X As Integer
    Public ReadOnly Y As Integer

    Public Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub

    Public Sub Deconstruct(ByRef x As Integer, ByRef y As Integer)
        x = Me.X
        y = Me.Y
    End Sub
End Class

Module Program
    Sub Main()
        Dim pairs = New List(Of (String, Integer)) From {(""a"", 1), (""b"", 2)}
        For Each (name, count) In pairs
            Console.Write(name & count.ToString() & "" "")
        Next

        For Each (x, y) In {New Point(1, 2), New Point(3, 4)}
            Console.Write((x + y).ToString() & "" "")
        Next

        For Each (big As Long, small As Integer) In {(5, 6)}
            Console.Write((big * 1000000000000L + small).ToString() & "" "")
        Next

        Dim last As String = ""none""
        Dim total As Integer = 0
        For Each (last, value) In pairs
            total += value
        Next
        Console.Write(last & total.ToString() & "" "")

        Dim actions As New List(Of Func(Of String))()
        For Each (name, count) In pairs
            actions.Add(Function() name)
        Next
        For Each action In actions
            Console.Write(action())
        Next
    End Sub
End Module
"

        <Fact>
        Public Sub ForEachDeconstruction_TuplesDeconstructAndTypes()
            CompileAndRun(Basics, "a1 b2 3 7 5000000000006 b3 ab")
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_InDebugBuild()
            CompileAndRun(Basics, "a1 b2 3 7 5000000000006 b3 ab", TestOptions.DebugExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True))
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ForEachDeconstruction_Dictionary()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic

Module Program
    Sub Main()
        Dim prices As New Dictionary(Of String, Decimal) From {{""tea"", 2.5D}, {""cake"", 4D}}
        For Each (item, price) In prices
            Console.Write(item & ""="" & price.ToString(Globalization.CultureInfo.InvariantCulture) & "" "")
        Next
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="tea=2.5 cake=4", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_Errors()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        For Each (a, b) In {1, 2}
        Next

        For Each (c, d, e) In {(1, 2)}
        Next

        Dim f As Integer = 0
        For Each (f As Integer, g As Integer) In {(1, 2)}
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37363: Deconstruction requires a tuple with 2 elements.
        For Each (a, b) In {1, 2}
                 ~~~~~~
BC37363: Deconstruction requires a tuple with 3 elements.
        For Each (c, d, e) In {(1, 2)}
                 ~~~~~~~~~
BC30616: Variable 'f' hides a variable in an enclosing block.
        For Each (f As Integer, g As Integer) In {(1, 2)}
                  ~
]]></expected>)
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_NestedErrorKeepsAsClauseTypes()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        For Each ((name, count As Long), ) In {(1, 2)}
            System.Console.WriteLine(count)
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37363: Deconstruction requires a tuple with 2 elements.
        For Each ((name, count As Long), ) In {(1, 2)}
                  ~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim count = tree.GetRoot().DescendantNodes().OfType(Of Syntax.IdentifierNameSyntax)().Single(Function(n) n.Identifier.ValueText = "count")
            Assert.Equal("Long", model.GetTypeInfo(count).Type.ToDisplayString())
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_OptionInferOff()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim known As Integer
        For Each (known, unknown) In {(1, 2)}
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(False), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30451: 'unknown' is not declared. It may be inaccessible due to its protection level.
        For Each (known, unknown) In {(1, 2)}
                         ~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_SemanticModel()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        For Each (name, count) In {(""a"", 1)}
            System.Console.Write(name & count.ToString())
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim usages = tree.GetRoot().DescendantNodes().OfType(Of Syntax.IdentifierNameSyntax)().Where(Function(n) n.Identifier.ValueText = "name" OrElse n.Identifier.ValueText = "count").ToArray()
            Assert.Equal(2, usages.Length)
            Assert.Equal("System.String", DirectCast(model.GetSymbolInfo(usages(0)).Symbol, ILocalSymbol).Type.ToTestDisplayString())
            Assert.Equal("System.Int32", DirectCast(model.GetSymbolInfo(usages(1)).Symbol, ILocalSymbol).Type.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_EmptySlots()
            CompileAndRun("
Imports System

Class Point
    Public ReadOnly X As Integer
    Public ReadOnly Y As Integer

    Public Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub

    Public Sub Deconstruct(ByRef x As Integer, ByRef y As Integer)
        x = Me.X
        y = Me.Y
    End Sub
End Class

Module Program
    Sub Main()
        For Each (k, ) In {(1, ""x""), (2, ""y"")}
            Console.Write(k & "" "")
        Next
        For Each (, v) In {(1, ""x""), (2, ""y"")}
            Console.Write(v & "" "")
        Next
        For Each (a, , c) In {(1, 2, 3)}
            Console.Write(a + c & "" "")
        Next
        For Each (x, ) In {New Point(5, 6)}
            Console.Write(x & "" "")
        Next
        For Each (, y As Long) In {New Point(7, 8)}
            Console.Write(y)
        Next
    End Sub
End Module
", "1 2 x y 4 5 8")
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ForEachDeconstruction_EmptySlotInAwaitEach()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Async Iterator Function Stream() As IAsyncEnumerable(Of (Integer, String))
        Await Task.Yield()
        Yield (1, ""one"")
        Yield (2, ""two"")
    End Function

    Async Function RunAsync() As Task
        Await Each (, word) In Stream()
            Console.Write(word & "" "")
        Next
    End Function

    Sub Main()
        RunAsync().GetAwaiter().GetResult()
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="one two ", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_Nested()
            CompileAndRun("
Imports System
Imports System.Collections.Generic

Class Point
    Public ReadOnly X As Integer
    Public ReadOnly Y As Integer

    Public Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub

    Public Sub Deconstruct(ByRef x As Integer, ByRef y As Integer)
        x = Me.X
        y = Me.Y
    End Sub
End Class

Module Program
    Sub Main()
        Dim pairs = {((1, 2), ""a""), ((3, 4), ""b"")}
        For Each ((x, y), label) In pairs
            Console.Write(x + y & label & "" "")
        Next
        For Each ((first, ), ) In pairs
            Console.Write(first & "" "")
        Next
        For Each ((big As Long, small), name As String) In pairs
            Console.Write(big * 10 + small & name & "" "")
        Next
        For Each (n, (px, py)) In {(1, New Point(5, 6))}
            Console.Write(n + px + py & "" "")
        Next
        Dim lastLabel As String = ""none""
        For Each ((a, b), lastLabel) In pairs
        Next
        Console.Write(lastLabel & "" "")
        For Each (k As Dictionary(Of String, Integer), v) In {(New Dictionary(Of String, Integer), 1)}
            Console.Write(k.Count + v)
        Next
    End Sub
End Module
", "3a 7b 1 3 12a 34b 12 b 1")
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_FlatListWithGenericAsClauseStaysFlat()
            Dim tree = VisualBasicSyntaxTree.ParseText("
Module Program
    Sub Main()
        For Each (k As System.Collections.Generic.Dictionary(Of String, Integer), v) In {(New System.Collections.Generic.Dictionary(Of String, Integer), 1)}
        Next
        For Each ((x, y), label) In {((1, 2), ""a"")}
        Next
    End Sub
End Module
", VbNext)

            Dim controls = tree.GetRoot().DescendantNodes().OfType(Of Syntax.ForEachStatementSyntax)().Select(Function(s) s.ControlVariable.Kind()).ToArray()
            Assert.Equal({SyntaxKind.ForEachDeconstruction, SyntaxKind.DeconstructionList}, controls)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ForEachDeconstruction_NestedInAwaitEach()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Async Iterator Function Stream() As IAsyncEnumerable(Of ((Integer, Integer), String))
        Await Task.Yield()
        Yield ((1, 2), ""one"")
        Yield ((3, 4), ""two"")
    End Function

    Async Function RunAsync() As Task
        Await Each ((a, b), word) In Stream()
            Console.Write(a + b & word & "" "")
        Next
    End Function

    Sub Main()
        RunAsync().GetAwaiter().GetResult()
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3one 7two ", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_NestedNotAvailableIn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        For Each ((x, y), label) In {((1, 2), ""a"")}
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotDeclared1, "x").WithArguments("x").WithLocation(4, 20),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "y").WithArguments("y").WithLocation(4, 23),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "label").WithArguments("label").WithLocation(4, 27))
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_EmptySlotNotAvailableIn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        For Each (k, ) In {(1, ""a"")}
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotDeclared1, "k").WithArguments("k").WithLocation(4, 19),
                Diagnostic(ERRID.ERR_ExpectedExpression, "").WithLocation(4, 22))
        End Sub

        <Fact>
        Public Sub ForEachDeconstruction_NotAvailableIn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        For Each (name, count) In {(""a"", 1)}
        Next
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30451: 'name' is not declared. It may be inaccessible due to its protection level.
        For Each (name, count) In {("a", 1)}
                  ~~~~
BC30451: 'count' is not declared. It may be inaccessible due to its protection level.
        For Each (name, count) In {("a", 1)}
                        ~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
