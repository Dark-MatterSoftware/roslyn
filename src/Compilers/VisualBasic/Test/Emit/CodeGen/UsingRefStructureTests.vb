' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class UsingRefStructureTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Function CreateLibrary() As Compilation
            Dim library = CreateCSharpCompilation("RefStructureLibrary", <![CDATA[
public ref struct Scope
{
    private readonly string _name;
    public Scope(string name) { _name = name; System.Console.Write("enter " + name + " "); }
    public void Dispose() { System.Console.Write("exit " + _name + " "); }
}

public ref struct NoDispose { }

public ref struct DisposeWithArgument
{
    public void Dispose(int code) { }
}

public static class Scopes
{
    public static Scope Enter(string name) => new Scope(name);
    public static NoDispose None() => new NoDispose();
    public static DisposeWithArgument WithArgument() => new DisposeWithArgument();
}
]]>, compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            library.VerifyDiagnostics()
            Return library
        End Function

        <Fact>
        Public Sub Using_RefStructureDisposesByPattern()
            Dim compilation = CreateVisualBasicCompilation("Program", <![CDATA[
Imports System

Module Program
    Sub Main()
        Using Scopes.Enter("a")
            Console.Write("body ")
        End Using

        Using s As Scope = Scopes.Enter("b")
            Console.Write("body ")
        End Using

        Try
            Using Scopes.Enter("c")
                Throw New InvalidOperationException("boom ")
            End Using
        Catch ex As InvalidOperationException
            Console.Write(ex.Message)
        End Try

        Declared()
    End Sub

    Sub Declared()
        Using d As Scope = Scopes.Enter("d")
        Console.Write("work ")
    End Sub
End Module
]]>,
                parseOptions:=VbNext,
                compilationOptions:=TestOptions.ReleaseExe,
                referencedCompilations:={CreateLibrary()})

            CompileAndVerify(compilation, expectedOutput:="enter a body exit a enter b body exit b enter c exit c boom enter d work exit d").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Using_RefStructureWithoutDispose()
            Dim compilation = CreateVisualBasicCompilation("Program", <![CDATA[
Module Program
    Sub Main()
        Using Scopes.None()
        End Using
        Using Scopes.WithArgument()
        End Using
    End Sub
End Module
]]>,
                parseOptions:=VbNext,
                compilationOptions:=TestOptions.ReleaseExe,
                referencedCompilations:={CreateLibrary()})

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36010: 'Using' operand of type 'NoDispose' must implement 'System.IDisposable'.
        Using Scopes.None()
              ~~~~~~~~~~~~~
BC36010: 'Using' operand of type 'DisposeWithArgument' must implement 'System.IDisposable'.
        Using Scopes.WithArgument()
              ~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub Using_RefStructureRejectedOn17_13()
            Dim compilation = CreateVisualBasicCompilation("Program", <![CDATA[
Module Program
    Sub Main()
        Using Scopes.Enter("a")
        End Using
    End Sub
End Module
]]>,
                parseOptions:=Vb17_13,
                compilationOptions:=TestOptions.ReleaseExe,
                referencedCompilations:={CreateLibrary()})

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36010: 'Using' operand of type 'Scope' must implement 'System.IDisposable'.
        Using Scopes.Enter("a")
              ~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub Using_LockEnterScope()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Threading

Module Program
    Private ReadOnly Gate As New Lock()

    Sub Main()
        Using Gate.EnterScope()
            Console.Write(Gate.IsHeldByCurrentThread & "" "")
        End Using
        Console.Write(Gate.IsHeldByCurrentThread)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="True False", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub
    End Class
End Namespace
