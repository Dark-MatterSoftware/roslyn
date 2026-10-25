' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class CSharpExtensionMemberTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Function CSharpLibrary() As MetadataReference
            Return CreateCSharpCompilation("
namespace Text
{
    public static class TextExtensions
    {
        extension(string s)
        {
            public int WordCount => s.Split(' ').Length;
            public string this[int i] => s.Split(' ')[i];
            public string Shout { get => s.ToUpper(); set { System.Console.Write(""<"" + value + s + ""> ""); } }
            public static string Empty2 => ""(empty)"";
            public static string Repeat(int n) => new string('x', n);
            public string Twice() => s + s;
        }
    }
}
", parseOptions:=CSharp.CSharpParseOptions.Default.WithLanguageVersion(CSharp.LanguageVersion.Preview),
   referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ReadsCSharpExtensionMembers()
            Dim compilation = CreateCompilation("
Imports System
Imports Text

Module Program
    Sub Main()
        Dim text = ""one two three""
        Console.Write(text.WordCount & "" "" & text.Shout & "" "" & text.Twice().Length & "" "" & String.Empty2 & "" "" & String.Repeat(3) & "" "")
        text.Shout = ""!""
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3 ONE TWO THREE 26 (empty) xxx <!one two three> ", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub ExtensionPropertiesNeedVbNext()
            Dim compilation = CreateCompilation("
Imports Text

Module Program
    Function Count(text As String) As Integer
        Return text.WordCount
    End Function
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibrary()}, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30456: 'WordCount' is not a member of 'String'.
        Return text.WordCount
               ~~~~~~~~~~~~~~
</expected>)
        End Sub

    End Class

End Namespace
