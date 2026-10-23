' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class RefReadOnlyParameterTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const LibrarySource As String = "
Public Structure Counter
    Public Value As Integer

    Public Sub Bump()
        Value += 1
    End Sub
End Structure

Public Interface IMeasure
    Function Measure(ByRef ReadOnly value As Integer) As Integer
End Interface

Public Module Calculator
    Public Function Twice(ByRef ReadOnly value As Integer) As Integer
        Return value * 2
    End Function

    Public Function Peek(ByRef ReadOnly c As Counter) As Integer
        c.Bump()
        Return c.Value
    End Function
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ByRefReadOnlyParameterReadsTheCallersVariable()
            Dim compilation = CreateCompilation({LibrarySource, "
Imports System

Class Holder
    Public ReadOnly Frozen As Integer = 5
End Class

Module Program
    Sub Main()
        Dim x = 21
        Console.Write(Twice(x))
        Console.Write("" "" & Twice(4))
        Dim c As New Counter With {.Value = 1}
        Console.Write("" "" & Peek(c) & "" "" & c.Value)
        Dim h As New Holder()
        Console.Write("" "" & Twice(h.Frozen))
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="42 8 1 1 10", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefReadOnlyParameterIsInForCSharp()
            Dim compilation = CreateCompilation(LibrarySource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim vbParameter = DirectCast(compilation.GetTypeByMetadataName("Calculator").GetMembers("Twice").Single(), IMethodSymbol).Parameters.Single()
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.In, vbParameter.RefKind)

            Dim csCompilation = CreateCSharpCompilation("
public class Ruler : IMeasure
{
    public int Measure(in int value) => value;
}

public static class Use
{
    public static int Call(int x) => Calculator.Twice(in x) + Calculator.Twice(3) + new Ruler().Measure(x);
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {compilation.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim parameter = DirectCast(csCompilation.GetTypeByMetadataName("Calculator").GetMembers("Twice").Single(), IMethodSymbol).Parameters.Single()
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.In, parameter.RefKind)
        End Sub

        <Fact>
        Public Sub ByRefReadOnlyParameterErrors()
            Dim source = "
Module Program
    Sub Assign(ByRef ReadOnly value As Integer)
        value = 3
        value += 1
        Dim ByRef r = value
        Dim ByRef ReadOnly ok = value
    End Sub

    Sub NotByRef(ReadOnly value As Integer)
    End Sub

    Sub Lambda()
        Dim f = Function(ByRef ReadOnly v As Integer) v
    End Sub
End Module
"
            Dim compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)
            compilation.AssertTheseDiagnostics(
<expected>
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        value = 3
        ~~~~~
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        value += 1
        ~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef r = value
                      ~~~~~
BC37419: 'ReadOnly' is valid only on a 'ByRef' parameter of a method, constructor, operator, or delegate.
    Sub NotByRef(ReadOnly value As Integer)
                 ~~~~~~~~
BC37419: 'ReadOnly' is valid only on a 'ByRef' parameter of a method, constructor, operator, or delegate.
        Dim f = Function(ByRef ReadOnly v As Integer) v
                               ~~~~~~~~
</expected>)

            compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True),
                                            parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            compilation.AssertTheseDiagnostics(
<expected>
BC30181: Specifiers valid only at the beginning of a declaration.
    Sub Assign(ByRef ReadOnly value As Integer)
                     ~~~~~~~~
BC30183: Keyword is not valid as an identifier.
        Dim ByRef r = value
            ~~~~~
BC30183: Keyword is not valid as an identifier.
        Dim ByRef ReadOnly ok = value
            ~~~~~
BC30288: Local variable 'ByRef' is already declared in the current block.
        Dim ByRef ReadOnly ok = value
            ~~~~~
BC30181: Specifiers valid only at the beginning of a declaration.
    Sub NotByRef(ReadOnly value As Integer)
                 ~~~~~~~~
BC30181: Specifiers valid only at the beginning of a declaration.
        Dim f = Function(ByRef ReadOnly v As Integer) v
                               ~~~~~~~~
</expected>)
        End Sub

    End Class

End Namespace
