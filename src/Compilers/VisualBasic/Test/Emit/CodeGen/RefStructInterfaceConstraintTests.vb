' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class RefStructInterfaceConstraintTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Function CSharpLibrary() As MetadataReference
            Return CreateCSharpCompilation("
using System;

public interface IGreeter { string Greet(); }

public ref struct CSharpWrapper : IDisposable { public void Dispose() { } }

public static class Generic
{
    public static void Use<T>(T value) where T : IDisposable, allows ref struct { value.Dispose(); }
    public static string Call<T>(T value) where T : IGreeter, allows ref struct => value.Greet();
    public static string NoRefStruct<T>(T value) where T : IGreeter => value.Greet();
    public static string StructOnly<T>(T value) where T : struct, IGreeter, allows ref struct => value.Greet();
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        Private Const WrapperSource As String = "
Imports System

ByRef Structure Wrapper
    Implements IDisposable, IGreeter

    Public Name As String

    Public Sub Dispose() Implements IDisposable.Dispose
        Console.Write(""disposed "")
    End Sub

    Public Function Greet() As String Implements IGreeter.Greet
        Return ""hi "" & Name
    End Function
End Structure

ByRef Structure Plain
    Public Value As Integer
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefStructuresMeetInterfaceConstraints()
            Dim compilation = CreateCompilation({WrapperSource, "
Imports System

Module Program
    Sub Main()
        Generic.Use(New Wrapper())
        Console.Write(Generic.Call(New Wrapper With {.Name = ""vb""}) & "" "" & Generic.StructOnly(New Wrapper With {.Name = ""too""}))
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="disposed hi vb hi too", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RefStructureConstraintErrors()
            Dim compilation = CreateCompilation({WrapperSource, "
Module Program
    Sub Main()
        Generic.NoRefStruct(New Wrapper())
        Generic.Use(New Plain())
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37376: Type argument 'Wrapper' may be a ByRef structure, and type parameter 'T' does not allow one.
        Generic.NoRefStruct(New Wrapper())
                ~~~~~~~~~~~
BC32044: Type argument 'Plain' does not inherit from or implement the constraint type 'IDisposable'.
        Generic.Use(New Plain())
                ~~~
</expected>)
        End Sub

        <Fact>
        Public Sub RefStructureConstraintsNeedVbNext()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Generic.Use(New CSharpWrapper())
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseExe, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC32044: Type argument 'CSharpWrapper' does not inherit from or implement the constraint type 'IDisposable'.
        Generic.Use(New CSharpWrapper())
                ~~~
BC30668: 'CSharpWrapper' is obsolete: 'Types with embedded references are not supported in this version of your compiler.'.
        Generic.Use(New CSharpWrapper())
                        ~~~~~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
