' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class ArrayLiteralCollectionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub ArrayLiteralsBuildCollections()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic

Class Bag
    Implements IEnumerable(Of String)

    Private ReadOnly _items As New List(Of String)

    Public Sub Add(item As String)
        _items.Add(item.ToUpperInvariant())
    End Sub

    Public Function GetEnumerator() As IEnumerator(Of String) Implements IEnumerable(Of String).GetEnumerator
        Return _items.GetEnumerator()
    End Function

    Private Function GetEnumerator1() As Collections.IEnumerator Implements Collections.IEnumerable.GetEnumerator
        Return GetEnumerator()
    End Function
End Class

Class Holder
    Public Shared Primes As List(Of Integer) = {2, 3, 5}
End Class

Module Program
    Function Evens() As List(Of Long)
        Return {2, 4, 6}
    End Function

    Sub Main()
        Dim list As List(Of Integer) = {1, 2, 3}
        Dim unique As HashSet(Of Integer) = {1, 1, 2}
        Dim bag As Bag = {""a"", ""b""}
        Dim empty As List(Of String) = {}
        Dim sequence As IEnumerable(Of Integer) = {1, 2}
        Dim indexed As IList(Of Integer) = {1, 2}
        list = {7, 8}
        Console.Write($""{String.Join("","", list)} {unique.Count} {String.Join("","", bag)} {empty.Count} {Evens().Count} {Holder.Primes(2)} "")
        Console.Write($""{sequence.GetType().Name} {indexed.GetType().Name} {list.GetType().Name}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="7,8 2 A,B 0 3 5 Int32[] Int32[] List`1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionLiteralArguments()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic

Class Holder
    Public Function Take(values As List(Of String)) As String
        Return String.Join(""+"", values)
    End Function
End Class

Module Program
    Function Count(values As List(Of Integer)) As Integer
        Return values.Count
    End Function

    Function Kind(values As Integer()) As String
        Return ""array""
    End Function

    Function Kind(values As List(Of Integer)) As String
        Return ""list""
    End Function

    Function Unique(values As HashSet(Of String)) As Integer
        Return values.Count
    End Function

    Function Join(separator As String, values As List(Of Integer)) As String
        Return String.Join(separator, values)
    End Function

    Sub Main()
        Dim a As Integer() = {7, 8}
        Console.Write($""{Count({1, 2, 3})} {Count((1, 2))} {Count(())} {Count({..a, 9})} "")
        Console.Write($""{Kind({1, 2})} {Unique({""a"", ""a"", ""b""})} {New Holder().Take({""x"", ""y""})} {Join(""-"", {4, 5})} "")
        Console.Write($""{Join(separator:=""-"", values:={6, 7})} {Join(values:=(8, 9), separator:=""+"")} {Join(""/"", values:={1, 2})}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3 2 0 3 array 2 x+y 4-5 6-7 8+9 1/2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionLiteralsInIf()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic

Module Program
    Function Pick(flag As Boolean) As List(Of Integer)
        Return If(flag, {1}, {2, 3})
    End Function

    Sub Main()
        Dim a As Integer() = {1, 2}
        Dim chosen As List(Of Integer) = If(a.Length > 1, {1}, {2, 3})
        Dim mixed As HashSet(Of Integer) = If(a.Length > 5, {1}, (2, 2, 3))
        Dim existing As New List(Of Integer) From {9}
        Dim withVariable = If(a.Length > 5, existing, {4, 5})
        Dim kept = If(existing, {7})
        Dim missing As List(Of Integer) = Nothing
        Dim fallback = If(missing, {7, 8})
        Dim array As Integer() = If(a.Length > 1, {1}, {2})
        Console.Write($""{Pick(False).Count} {chosen.Count} {mixed.Count} {withVariable.GetType().Name}{withVariable.Count} {kept.Count} {fallback.Count} {array.GetType().Name}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="2 1 2 List`12 1 2 Int32[]").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CollectionLiteralsInIfErrors()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Sub Main()
        Dim flag = True
        Dim a As List(Of Integer) = If(flag, {1}, {""text""})
        Dim b As String = If(flag, {1}, {2})
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True).WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC36913: Cannot infer a common type because more than one type is possible.
        Dim a As List(Of Integer) = If(flag, {1}, {"text"})
                                    ~~~~~~~~~~~~~~~~~~~~~~~
BC30311: Value of type 'Integer()' cannot be converted to 'String'.
        Dim b As String = If(flag, {1}, {2})
                          ~~~~~~~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub CollectionLiteralsInIfNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Sub Main()
        Dim flag = True
        Dim a As List(Of Integer) = If(flag, {1}, {2, 3})
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30311: Value of type 'Integer()' cannot be converted to 'List(Of Integer)'.
        Dim a As List(Of Integer) = If(flag, {1}, {2, 3})
                                    ~~~~~~~~~~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub CollectionLiteralArgumentErrors()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Function Pick(values As List(Of Integer)) As String
        Return ""list""
    End Function

    Function Pick(values As HashSet(Of Integer)) As String
        Return ""set""
    End Function

    Function Generic(Of T)(values As List(Of T)) As Integer
        Return values.Count
    End Function

    Function Strings(values As List(Of String)) As Integer
        Return values.Count
    End Function

    Sub Main()
        Dim a = Pick({1, 2})
        Dim b = Generic({1, 2})
        Dim c = Strings({1, 2})
        Dim d = Strings(texts:={""a""})
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True).WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30518: Overload resolution failed because no accessible 'Pick' can be called with these arguments:
    'Public Function Pick(values As List(Of Integer)) As String': Value of type 'Integer()' cannot be converted to 'List(Of Integer)'.
    'Public Function Pick(values As HashSet(Of Integer)) As String': Value of type 'Integer()' cannot be converted to 'HashSet(Of Integer)'.
        Dim a = Pick({1, 2})
                ~~~~
BC36645: Data type(s) of the type parameter(s) in method 'Public Function Generic(Of T)(values As List(Of T)) As Integer' cannot be inferred from these arguments. Specifying the data type(s) explicitly might correct this error.
        Dim b = Generic({1, 2})
                ~~~~~~~
BC30311: Value of type 'Integer()' cannot be converted to 'List(Of String)'.
        Dim c = Strings({1, 2})
                        ~~~~~~
BC30455: Argument not specified for parameter 'values' of 'Public Function Strings(values As List(Of String)) As Integer'.
        Dim d = Strings(texts:={"a"})
                ~~~~~~~
BC30272: 'texts' is not a parameter of 'Public Function Strings(values As List(Of String)) As Integer'.
        Dim d = Strings(texts:={"a"})
                        ~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub SpreadsInArrayLiterals()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic

Module Program
    Sub Main()
        Dim a As Integer() = {1, 2}
        Dim more As New List(Of Integer) From {7, 8}
        Dim list As List(Of Integer) = {..a, 3}
        Dim array As Integer() = {0, ..a, ..more}
        Dim sequence As IEnumerable(Of Integer) = {..more}
        Dim indexed As IList(Of Integer) = {..a, 4}
        Dim unique As HashSet(Of Integer) = {..a, ..a, 5}
        Dim sorted As SortedSet(Of Integer) = (..more, 1)
        Console.Write($""{String.Join("","", list)} {String.Join("","", array)} {String.Join("","", sequence)} {indexed.GetType().Name}{indexed.Count} {unique.Count} {String.Join("","", sorted)}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1,2,3 0,1,2,7,8 7,8 List`13 3 1,7,8").VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SpreadsWithCollectionBuilder()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Immutable

Module Program
    Sub Main()
        Dim a As Integer() = {1, 2}
        Dim frozen As ImmutableArray(Of Integer) = {..a, 3}
        Dim span As ReadOnlySpan(Of Integer) = {..a, 9}
        Console.Write($""{frozen.Length} {frozen(2)} {span.Length}{span(2)}"")
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3 3 39", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SpreadsInArrayLiteralErrors()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a As Integer() = {1, 2}
        Dim b = {..a, 3}
        Dim c As String = {..a}
        Dim d As Integer(,) = {..a}
        Dim e As Bag = {..a, 5}
        Dim f As Object = {..a}
    End Sub
End Module

Class Bag
    Implements System.Collections.Generic.IEnumerable(Of Integer)

    Public Sub Add(item As Integer)
    End Sub

    Public Function GetEnumerator() As System.Collections.Generic.IEnumerator(Of Integer) Implements System.Collections.Generic.IEnumerable(Of Integer).GetEnumerator
        Return Nothing
    End Function

    Private Function GetEnumerator1() As System.Collections.IEnumerator Implements System.Collections.IEnumerable.GetEnumerator
        Return Nothing
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37359: A range can only be used to slice a one-dimensional array.
        Dim b = {..a, 3}
                 ~~~
BC37359: A range can only be used to slice a one-dimensional array.
        Dim c As String = {..a}
                           ~~~
BC37359: A range can only be used to slice a one-dimensional array.
        Dim d As Integer(,) = {..a}
                               ~~~
BC30456: 'AddRange' is not a member of 'Bag'.
        Dim e As Bag = {..a, 5}
                        ~~~
BC37359: A range can only be used to slice a one-dimensional array.
        Dim f As Object = {..a}
                           ~~~
</expected>)
        End Sub

        <Fact>
        Public Sub SpreadsInArrayLiteralsNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Sub Main()
        Dim a As Integer() = {1, 2}
        Dim list As List(Of Integer) = {..a, 3}
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30157: Leading '.' or '!' can only appear inside a 'With' statement.
        Dim list As List(Of Integer) = {..a, 3}
                                        ~
BC30203: Identifier expected.
        Dim list As List(Of Integer) = {..a, 3}
                                         ~
</expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ArrayLiteralBuildsWithCollectionBuilder()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Immutable

Module Program
    Sub Main()
        Dim frozen As ImmutableArray(Of Integer) = {1, 2, 3}
        Dim names As ImmutableList(Of String) = {""a"", ""b""}
        Console.Write($""{frozen.Length} {frozen(2)} {names.Count}"")
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3 3 2", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ArrayLiteralCollectionErrors()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Sub Use(values As List(Of Integer))
    End Sub

    Sub Main()
        Dim a As List(Of Integer) = {""text""}
        Dim b As List(Of Integer) = {{1, 2}, {3, 4}}
        Dim c As String = {1, 2}
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30512: Option Strict On disallows implicit conversions from 'String' to 'Integer'.
        Dim a As List(Of Integer) = {"text"}
                                     ~~~~~~
BC30311: Value of type 'Integer(*,*)' cannot be converted to 'List(Of Integer)'.
        Dim b As List(Of Integer) = {{1, 2}, {3, 4}}
                                    ~~~~~~~~~~~~~~~~
BC30311: Value of type 'Integer()' cannot be converted to 'String'.
        Dim c As String = {1, 2}
                          ~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub ArrayLiteralCollectionNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Module Program
    Sub Main()
        Dim list As List(Of Integer) = {1, 2, 3}
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30311: Value of type 'Integer()' cannot be converted to 'List(Of Integer)'.
        Dim list As List(Of Integer) = {1, 2, 3}
                                       ~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
