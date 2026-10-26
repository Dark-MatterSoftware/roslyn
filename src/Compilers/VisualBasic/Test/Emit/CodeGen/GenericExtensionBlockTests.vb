' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class GenericExtensionBlockTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Function CSharpLibrary() As MetadataReference
            Return CreateCSharpCompilation("
using System.Collections.Generic;

namespace Gen
{
    public static class ListExtensions
    {
        extension<T>(List<T> list)
        {
            public T Tail => list[list.Count - 1];
            public static List<T> Empty() => new List<T>();
            public static string Kind => typeof(T).Name;
        }

        extension<T>(IEnumerable<T> items)
        {
            public int Size { get { var n = 0; foreach (var item in items) n++; return n; } }
        }

        extension<T>(T value) where T : struct
        {
            public T Self => value;
        }
    }
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
   referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ReadsGenericExtensionBlocks()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports Gen

Module Program
    Sub Main()
        Dim numbers As New List(Of Integer) From {1, 2, 3}
        Dim empty = List(Of Integer).Empty()
        Console.Write(numbers.Tail & "" "" & empty.Count & "" "" & List(Of String).Kind & "" "" & numbers.Size)
        Console.Write("" "" & (New Integer() {4, 5}).Size & "" "" & (7).Self)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3 0 String 3 2 7", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub GenericExtensionBlockErrors()
            Dim compilation = CreateCompilation("
Imports Gen

Module Program
    Function Use() As String
        Return ""text"".Self
    End Function
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30456: 'Self' is not a member of 'String'.
        Return "text".Self
               ~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub GenericExtensionBlocksNeedVbNext()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Imports Gen

Module Program
    Function Use(numbers As List(Of Integer)) As Integer
        Return numbers.Tail
    End Function
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30456: 'Tail' is not a member of 'List(Of Integer)'.
        Return numbers.Tail
               ~~~~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
