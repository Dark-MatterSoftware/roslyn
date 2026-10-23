' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class RefReadOnlyFunctionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const StoreSource As String = "
Public Class Store
    Public Values As Integer() = {1, 2}
    Private ReadOnly _total As Integer = 10

    Public ByRef ReadOnly Function First() As Integer
        Return Values(0)
    End Function

    Public ByRef ReadOnly Function Total() As Integer
        Return _total
    End Function
End Class
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ByRefReadOnlyFunctionReturnsAReference()
            Dim compilation = CreateCompilation({StoreSource, "
Imports System

Module Program
    Sub Main()
        Dim s As New Store()
        Dim ByRef ReadOnly first = s.First()
        s.Values(0) = 9
        Console.Write(first & "" "" & (s.First() + 1) & "" "" & s.Total())
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="9 10 10", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ByRefReadOnlyFunctionIsRefReadOnlyForCSharp()
            Dim compilation = CreateCompilation(StoreSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim vbMethod = DirectCast(compilation.GetTypeByMetadataName("Store").GetMembers("First").Single(), IMethodSymbol)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.RefReadOnly, vbMethod.RefKind)

            Dim csCompilation = CreateCSharpCompilation("
public static class Use
{
    public static int Read(Store s) { ref readonly int x = ref s.First(); return x; }
    public static void Write(Store s) { s.First() = 1; }
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {compilation.EmitToImageReference()}))

            Dim method = DirectCast(csCompilation.GetTypeByMetadataName("Store").GetMembers("First").Single(), IMethodSymbol)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.RefReadOnly, method.RefKind)

            Dim errors = csCompilation.GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 1, String.Join(vbLf, errors.Select(Function(d) d.ToString())))
            Assert.Equal("s.First()", errors(0).Location.SourceTree.GetText().ToString(errors(0).Location.SourceSpan))
        End Sub

        <Fact>
        Public Sub ByRefReadOnlyFunctionErrors()
            Dim source = "
Class Store
    Public Values As Integer() = {1, 2}

    ByRef ReadOnly Function First() As Integer
        Return Values(0)
    End Function

    ByRef ReadOnly Function NotAVariable() As Integer
        Return 5
    End Function
End Class

Module Program
    Sub Test(s As Store)
        s.First() = 3
        Dim ByRef r = s.First()
    End Sub
End Module
"
            Dim compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)
            compilation.AssertTheseDiagnostics(
<expected>
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        Return 5
               ~
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        s.First() = 3
        ~~~~~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef r = s.First()
                      ~~~~~~~~~
</expected>)

            compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True),
                                            parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            compilation.AssertTheseDiagnostics(
<expected>
BC30035: Syntax error.
    ByRef ReadOnly Function First() As Integer
    ~~~~~
BC30689: Statement cannot appear outside of a method body.
        Return Values(0)
        ~~~~~~~~~~~~~~~~
BC30430: 'End Function' must be preceded by a matching 'Function'.
    End Function
    ~~~~~~~~~~~~
BC30035: Syntax error.
    ByRef ReadOnly Function NotAVariable() As Integer
    ~~~~~
BC30689: Statement cannot appear outside of a method body.
        Return 5
        ~~~~~~~~
BC30430: 'End Function' must be preceded by a matching 'Function'.
    End Function
    ~~~~~~~~~~~~
BC30456: 'First' is not a member of 'Store'.
        s.First() = 3
        ~~~~~~~
BC30183: Keyword is not valid as an identifier.
        Dim ByRef r = s.First()
            ~~~~~
BC30456: 'First' is not a member of 'Store'.
        Dim ByRef r = s.First()
                      ~~~~~~~
</expected>)
        End Sub

    End Class

End Namespace
