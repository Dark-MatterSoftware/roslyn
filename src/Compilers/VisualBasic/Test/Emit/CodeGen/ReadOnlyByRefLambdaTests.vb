' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class ReadOnlyByRefLambdaTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const Declarations As String = "
Public Structure Counter
    Public Value As Integer

    Public Sub Bump()
        Value += 1
    End Sub
End Structure

Public Delegate Function Measure(ByRef ReadOnly value As Integer) As Integer
Public Delegate Function Peeker(ByRef ReadOnly c As Counter) As Integer
Public Delegate Sub Show(ByRef ReadOnly value As Integer)
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub LambdaConvertsToByRefReadOnlyDelegate()
            Dim compilation = CreateCompilation({Declarations, "
Imports System

Module Program
    Function Apply(m As Measure, value As Integer) As Integer
        Return m(value)
    End Function

    Sub Main()
        Dim untyped As Measure = Function(v) v * 10
        Dim typed As Measure = Function(v As Integer) v + 1
        Dim byRefLambda As Measure = Function(ByRef v As Integer) v * 2
        Dim show As Show = Sub(v) Console.Write(""["" & v & ""]"")
        Dim x = 3
        Console.Write(untyped(x) & "" "" & typed(x) & "" "" & byRefLambda(4) & "" "" & Apply(Function(v) v - 1, 9) & "" "")
        show(x)

        Dim peek As Peeker = Function(c)
                                 c.Bump()
                                 Return c.Value
                             End Function
        Dim counter As New Counter With {.Value = 1}
        Console.Write("" "" & peek(counter) & "" "" & counter.Value)
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="30 4 8 8 [3] 1 1", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub LambdaConvertsToCSharpInDelegate()
            Dim library = CreateCSharpCompilation("
public delegate int Scale(in int value);

public static class Use
{
    public static int Call(Scale s, int x) => s(in x);
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()

            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim s As Scale = Function(v) v * 3
        Console.Write(Use.Call(s, 5) & "" "" & Use.Call(Function(v) v + 100, 1))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={library}, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="15 101", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub LambdaParameterIsReadOnly()
            Dim compilation = CreateCompilation({Declarations, "
Module Program
    Sub Main()
        Dim a As Measure = Function(v)
                               v = 1
                               Return v
                           End Function
        Dim b As Measure = Function(ByRef v As Integer)
                               v += 1
                               Dim ByRef r = v
                               Return v
                           End Function
        Dim c As Measure = Function(ByVal v As Integer) v
        Dim d As Measure = Function(ByRef ReadOnly v As Integer) v
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
                               v = 1
                               ~
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
                               v += 1
                               ~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
                               Dim ByRef r = v
                                             ~
BC36532: Nested function does not have the same signature as delegate 'Delegate Function Measure(ByRef ReadOnly value As Integer) As Integer'.
        Dim c As Measure = Function(ByVal v As Integer) v
                           ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC37419: 'ReadOnly' is valid only on a 'ByRef' parameter of a method, constructor, operator, or delegate.
        Dim d As Measure = Function(ByRef ReadOnly v As Integer) v
                                          ~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub LambdaParameterReportsRefKindIn()
            Dim compilation = CreateCompilation({Declarations, "
Module Program
    Sub Main()
        Dim m As Measure = Function(v) v * 10
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees(1)
            Dim model = compilation.GetSemanticModel(tree)
            Dim parameter = tree.GetRoot().DescendantNodes().OfType(Of ParameterSyntax)().Single()
            Dim symbol = DirectCast(model.GetDeclaredSymbol(parameter), IParameterSymbol)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.In, symbol.RefKind)
        End Sub

    End Class

End Namespace
