' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ByRefLocalCodeGenTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const References As String = "
Imports System

Class Counter
    Public Count As Integer
End Class

Structure Point
    Public X As Integer
End Structure

Module Program
    Private _items As Integer() = {1, 2, 3}

    ByRef Function Slot(i As Integer) As Integer
        Return _items(i)
    End Function

    Sub Increment(ByRef value As Integer)
        value += 1
    End Sub

    Sub Main()
        Dim values As Integer() = {10, 20, 30}
        Dim ByRef first = values(0)
        first = 11
        first += 1
        Dim ByRef second As Integer = values(1)
        Increment(second)

        Dim counter As New Counter()
        Dim ByRef count = counter.Count
        count = 5

        Dim point As Point
        Dim ByRef x = point.X
        x = 3

        Dim ByRef middle = Slot(1)
        middle *= 7

        Dim ByRef again = first
        again = 100

        Dim total = 0
        Dim ByRef sum = total
        For Each value In values
            sum += value
        Next

        Console.Write($""{String.Join("","", values)} {counter.Count} {point.X} {String.Join("","", _items)} {total}"")
    End Sub
End Module
"

        <Fact>
        Public Sub ByRefLocalsWriteThrough()
            Dim compilation = CreateCompilation(References, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="100,21,30 5 3 1,14,3 151").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefLocalsWriteThroughInDebugBuild()
            Dim compilation = CreateCompilation(References, options:=TestOptions.DebugExe.WithOptionInfer(True), parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="100,21,30 5 3 1,14,3 151").VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ByRefLocalsOverCollectionsMarshal()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports System.Runtime.InteropServices

Module Program
    Sub Main()
        Dim counts As New Dictionary(Of String, Integer)()
        For Each word In {""a"", ""b"", ""a""}
            Dim exists As Boolean
            Dim ByRef slot = CollectionsMarshal.GetValueRefOrAddDefault(counts, word, exists)
            slot += 1
        Next

        Dim list As New List(Of Integer) From {1, 2}
        Dim ByRef head As Integer = CollectionsMarshal.AsSpan(list)(0)
        head = 9

        Console.Write($""{counts(""a"")} {counts(""b"")} {list(0)}"")
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2 1 9", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefLocalErrors()
            Dim compilation = CreateCompilation("
Class Counter
    Public Count As Integer
    Public ReadOnly Fixed As Integer
    Public Property Name As String
End Class

Module Program
    Sub M(values As Integer(), counter As Counter)
        Dim ByRef missing As Integer
        Dim ByRef constant = 5
        Dim ByRef computed = values(0) + 1
        Dim ByRef widened As Long = values(0)
        Dim ByRef viaSetter = counter.Name
        Dim ByRef fixedField = counter.Fixed
        Dim ByRef created As New Counter()
        Static ByRef kept As Integer
        Dim ByRef first = values(0)
        Dim read = Function() first
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef missing As Integer
                  ~~~~~~~
BC42024: Unused local variable: 'missing'.
        Dim ByRef missing As Integer
                  ~~~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef constant = 5
                             ~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef computed = values(0) + 1
                             ~~~~~~~~~~~~~
BC37407: A 'ByRef' local of type 'Long' cannot refer to a variable of type 'Integer'.
        Dim ByRef widened As Long = values(0)
                                    ~~~~~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef viaSetter = counter.Name
                              ~~~~~~~~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef fixedField = counter.Fixed
                               ~~~~~~~~~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef created As New Counter()
                  ~~~~~~~
BC42024: Unused local variable: 'created'.
        Dim ByRef created As New Counter()
                  ~~~~~~~
BC30247: 'ByRef' is not valid on a local variable declaration.
        Static ByRef kept As Integer
               ~~~~~
BC42024: Unused local variable: 'kept'.
        Static ByRef kept As Integer
                     ~~~~
BC37384: 'ByRef' variable 'first' cannot be used in a lambda expression.
        Dim read = Function() first
                              ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub ByRefFunctionReturnsAByRefLocal()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    ByRef Function First(values As Integer()) As Integer
        Dim ByRef head = values(0)
        Return head
    End Function

    Sub Main()
        Dim values = {3, 7, 5}
        First(values) = 4
        First(values) += 1
        Console.Write(String.Join("","", values))
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            ' Neither PEVerify nor ILVerify accepts ByRef return types.
            CompileAndVerify(compilation, expectedOutput:="5,7,5", verify:=Verification.Fails).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefReadOnlyLocalsRead()
            Dim compilation = CreateCompilation("
Imports System

Class Holder
    Public ReadOnly Fixed As Integer = 7
    Public Count As Integer
End Class

Module Program
    Sub Show(ByRef value As Integer)
        value += 100
        Console.Write(value & "" "")
    End Sub

    Sub Main()
        Dim holder As New Holder()
        Dim ByRef ReadOnly fixedRef = holder.Fixed
        Dim ByRef ReadOnly countRef As Integer = holder.Count
        holder.Count = 3
        Show(countRef)
        Dim ByRef ReadOnly again = countRef
        Console.Write($""{fixedRef} {countRef} {again} {holder.Count}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            ' Taking the address of a ReadOnly field, as C# does for ref readonly, is not verifiable.
            CompileAndVerify(compilation, expectedOutput:="103 7 3 3 3", verify:=Verification.FailsPEVerify).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ByRefReadOnlyLocalsOverReadOnlySpans()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim text = ""hello""
        Dim ByRef ReadOnly letter = text.AsSpan()(1)
        Dim numbers As Integer() = {1, 2, 3}
        Dim view As ReadOnlySpan(Of Integer) = numbers
        Dim ByRef ReadOnly second = view(1)
        numbers(1) = 42
        Console.Write($""{letter} {second}"")
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="e 42", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefReadOnlyLocalErrors()
            Dim compilation = CreateCompilation("
Imports System

Class Holder
    Public ReadOnly Fixed As Integer = 7
End Class

Module Program
    Sub M(holder As Holder, numbers As Integer())
        Dim ByRef ReadOnly fixedRef = holder.Fixed
        fixedRef = 1
        fixedRef += 1
        Dim ByRef writable = fixedRef
        Dim view As ReadOnlySpan(Of Integer) = numbers
        Dim ByRef element = view(0)
        Dim ByRef ReadOnly computed = numbers(0) + 1
        Dim ReadOnly plain = 1
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        fixedRef = 1
        ~~~~~~~~
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        fixedRef += 1
        ~~~~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef writable = fixedRef
                             ~~~~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef element = view(0)
                            ~~~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef ReadOnly computed = numbers(0) + 1
                                      ~~~~~~~~~~~~~~
BC30247: 'ReadOnly' is not valid on a local variable declaration.
        Dim ReadOnly plain = 1
            ~~~~~~~~
]]></expected>)
        End Sub

        Private Const Suspending As String = "
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Async Function Run(values As Integer()) As Task
        Dim ByRef first = values(0)
        first = 5
        Await Task.Yield()
        Dim ByRef second = values(1)
        second += 10
    End Function

    Iterator Function Items(values As Integer()) As IEnumerable(Of Integer)
        Dim ByRef first = values(0)
        first = 7
        Yield values(0)
        Dim ByRef second = values(1)
        second = 8
        Yield values(1)
    End Function

    Sub Main()
        Dim values = {1, 2}
        Run(values).GetAwaiter().GetResult()
        Console.Write(String.Join("","", values))
        For Each item In Items({1, 2})
            Console.Write("" "" & item)
        Next
    End Sub
End Module
"

        <Theory>
        <InlineData(OptimizationLevel.Release)>
        <InlineData(OptimizationLevel.Debug)>
        Public Sub ByRefLocalsBetweenSuspensions(optimization As OptimizationLevel)
            Dim compilation = CreateCompilation(Suspending, options:=TestOptions.ReleaseExe.WithOptimizationLevel(optimization).WithOptionInfer(True), parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="5,12 7 8").VerifyDiagnostics()
        End Sub

        <Theory>
        <InlineData(OptimizationLevel.Release)>
        <InlineData(OptimizationLevel.Debug)>
        Public Sub ByRefLocalsAcrossSuspensions(optimization As OptimizationLevel)
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Imports System.Threading.Tasks

Module Program
    Async Function AfterAwait(values As Integer()) As Task
        Dim ByRef first = values(0)
        Await Task.Yield()
        first = 1
    End Function

    Iterator Function AfterYield(values As Integer()) As IEnumerable(Of Integer)
        Dim ByRef first = values(0)
        Yield 1
        Yield first
    End Function

    Async Function EachElement(values As Integer()) As Task
        For Each ByRef value In values
            Await Task.Yield()
            value += 1
        Next
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithOptimizationLevel(optimization).WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseEmitDiagnostics(<expected><![CDATA[
BC37408: 'ByRef' local 'first' cannot be used across an 'Await' or 'Yield', because its reference cannot be stored while the method is suspended.
        first = 1
        ~~~~~
BC37408: 'ByRef' local 'first' cannot be used across an 'Await' or 'Yield', because its reference cannot be stored while the method is suspended.
        Yield first
              ~~~~~
BC37408: 'ByRef' local 'value' cannot be used across an 'Await' or 'Yield', because its reference cannot be stored while the method is suspended.
            value += 1
            ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub ByRefReadOnlyLocalSemanticModel()
            Dim compilation = CreateCompilation("
Module Program
    Sub M(values As Integer())
        Dim ByRef ReadOnly first = values(0)
        System.Console.Write(first)
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim declaration = tree.GetRoot().DescendantNodes().OfType(Of Syntax.ModifiedIdentifierSyntax)().Single(Function(n) n.Identifier.ValueText = "first")
            Dim local = DirectCast(model.GetDeclaredSymbol(declaration), ILocalSymbol)
            Assert.True(local.IsRef)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.RefReadOnly, local.RefKind)
        End Sub

        <Fact>
        Public Sub ByRefLocalSemanticModel()
            Dim compilation = CreateCompilation("
Module Program
    Sub M(values As Integer())
        Dim ByRef first = values(0)
        first = 1
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim declaration = tree.GetRoot().DescendantNodes().OfType(Of Syntax.ModifiedIdentifierSyntax)().Single(Function(n) n.Identifier.ValueText = "first")
            Dim local = DirectCast(model.GetDeclaredSymbol(declaration), ILocalSymbol)
            Assert.True(local.IsRef)
            Assert.Equal("System.Int32", local.Type.ToTestDisplayString())
        End Sub
    End Class
End Namespace
