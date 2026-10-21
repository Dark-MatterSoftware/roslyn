' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RefReadOnlyReturnTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Function CreateLibrary() As Compilation
            Dim library = CreateCSharpCompilation("RefReadOnlyLibrary", <![CDATA[
public struct Point
{
    public int X, Y;
    public Point(int x, int y) { X = x; Y = y; }
    public void Move() { X += 100; }
    public override string ToString() => "(" + X + ", " + Y + ")";
}

public class Store
{
    private readonly int[] _items = { 10, 20, 30 };
    private Point _origin = new Point(1, 2);

    public ref readonly int this[int index] => ref _items[index];
    public ref readonly int First() => ref _items[0];
    public ref readonly Point Origin => ref _origin;
    public static ref readonly int Second(int[] items) => ref items[1];
    public int[] Items => _items;
}
]]>, compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            library.VerifyDiagnostics()
            Return library
        End Function

        <Fact>
        Public Sub RefReadOnly_ReadsTheValue()
            Dim compilation = CreateVisualBasicCompilation("Program", <![CDATA[
Imports System

Module Program
    Sub Main()
        Dim s As New Store()
        Console.Write(s(1) & " " & s.First() & " " & Store.Second(s.Items) & " ")

        Dim copy As Point = s.Origin
        copy.Move()
        Console.Write(copy.ToString() & " " & s.Origin.ToString() & " ")

        s.Origin.Move()
        Console.Write(s.Origin.ToString() & " " & (s(0) + s(2)).ToString() & " ")

        Bump(s(1))
        Bump(s.First())
        Console.Write(s(1) & " " & s.First())
    End Sub

    Sub Bump(ByRef value As Integer)
        value += 1
    End Sub
End Module
]]>,
                parseOptions:=VbNext,
                compilationOptions:=TestOptions.ReleaseExe,
                referencedCompilations:={CreateLibrary()})

            CompileAndVerify(compilation, expectedOutput:="20 10 20 (101, 2) (1, 2) (1, 2) 40 20 10").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RefReadOnly_CannotBeWritten()
            Dim compilation = CreateVisualBasicCompilation("Program", <![CDATA[
Module Program
    Sub Main()
        Dim s As New Store()
        s(0) = 5
        s.First() = 5
    End Sub
End Module
]]>,
                parseOptions:=VbNext,
                compilationOptions:=TestOptions.ReleaseExe,
                referencedCompilations:={CreateLibrary()})

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30526: Property 'Item' is 'ReadOnly'.
        s(0) = 5
        ~~~~~~~~
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        s.First() = 5
        ~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub RefReadOnly_UnsupportedOn17_13()
            Dim compilation = CreateVisualBasicCompilation("Program", <![CDATA[
Module Program
    Sub Main()
        Dim s As New Store()
        System.Console.Write(s(1))
        System.Console.Write(s.First())
    End Sub
End Module
]]>,
                parseOptions:=Vb17_13,
                compilationOptions:=TestOptions.ReleaseExe,
                referencedCompilations:={CreateLibrary()})

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30643: Property 'Store.Item(index As Integer)' is of an unsupported type.
        System.Console.Write(s(1))
                             ~
BC30657: 'First' has a return type that is not supported or parameter types that are not supported.
        System.Console.Write(s.First())
                               ~~~~~
]]></expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefReadOnly_ReadOnlySpanElements()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Function Sum(values As ReadOnlySpan(Of Integer)) As Integer
        Dim total As Integer = 0
        For i As Integer = 0 To values.Length - 1
            total += values(i)
        Next
        Return total
    End Function

    Function CountVowels(text As String) As Integer
        Dim count As Integer = 0
        For Each c As Char In text.AsSpan()
            If ""aeiou"".IndexOf(c) >= 0 Then count += 1
        Next
        Return count
    End Function

    Sub Main()
        Dim chars As ReadOnlySpan(Of Char) = ""hello"".AsSpan()
        Console.Write(chars(0) & chars(chars.Length - 1) & "" "")
        Console.Write(Sum(New Integer() {1, 2, 3}) & "" "")
        Console.Write(CountVowels(""education""))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ho 6 5", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefReadOnly_ReadOnlySpanUnsupportedOn17_13()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim chars As ReadOnlySpan(Of Char) = ""hello"".AsSpan()
        Console.Write(chars(0))
        For Each c As Char In chars
        Next
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30668: 'ReadOnlySpan(Of Char)' is obsolete: 'Types with embedded references are not supported in this version of your compiler.'.
        Dim chars As ReadOnlySpan(Of Char) = "hello".AsSpan()
                     ~~~~~~~~~~~~~~~~~~~~~
BC30643: Property 'System.ReadOnlySpan(Of T).Item(index As Integer)' is of an unsupported type.
        Console.Write(chars(0))
                      ~~~~~
BC30643: Property 'System.ReadOnlySpan(Of T).Enumerator.Current' is of an unsupported type.
        For Each c As Char In chars
                              ~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
