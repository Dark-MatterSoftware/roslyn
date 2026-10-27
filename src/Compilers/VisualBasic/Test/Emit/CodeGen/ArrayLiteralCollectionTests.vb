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
        Use({1, 2})
        Use((1, 2))
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
BC30311: Value of type 'Integer()' cannot be converted to 'List(Of Integer)'.
        Use({1, 2})
            ~~~~~~
BC30311: Value of type '(Integer, Integer)' cannot be converted to 'List(Of Integer)'.
        Use((1, 2))
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
