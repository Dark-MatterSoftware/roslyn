' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class AnonymousTypeWithTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub CopyAnonymousTypes()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Function Trace(name As String, value As Integer) As Integer
        Console.Write(name)
        Return value
    End Function

    Sub Main()
        Dim a = New With {.X = 1, Key .Y = 2, .Name = ""a""}
        Dim b = a With {.Y = 5, .Name = ""b""}
        Dim c = a With {.X = .X + 10}
        Dim d = a With {.X = CShort(3)}
        Dim e = New With {Key .Id = 1, .Label = ""x""}
        Dim f = e With {.Label = ""y""}
        Console.WriteLine(a.ToString() & "" "" & b.ToString())
        Console.WriteLine($""{c.X} {d.X} {b.GetType() Is a.GetType()} {e.Equals(f)}"")
        Dim g = (New With {.P = Trace(""a"", 1), .Q = 2}) With {.Q = Trace(""c"", 3), .P = Trace(""b"", 4)}
        Console.WriteLine($"" {g.P}{g.Q}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True).WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="{ X = 1, Y = 2, Name = a } { X = 1, Y = 5, Name = b }
11 3 True True
acb 43").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub AnonymousTypeWithErrors()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a = New With {.X = 1, .Name = ""a""}
        Dim b = a With {.Missing = 1}
        Dim c = a With {.X = 1, .X = 2}
        Dim d = a With {.X = ""text""}
        Dim e = a With {.X = 1.5}
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True).WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30456: 'Missing' is not a member of '&lt;anonymous type: X As Integer, Name As String&gt;'.
        Dim b = a With {.Missing = 1}
                         ~~~~~~~
BC30989: Multiple initializations of 'X'.  Fields and properties can be initialized only once in an object initializer expression.
        Dim c = a With {.X = 1, .X = 2}
                                 ~
BC30512: Option Strict On disallows implicit conversions from 'String' to 'Integer'.
        Dim d = a With {.X = "text"}
                             ~~~~~~
BC30512: Option Strict On disallows implicit conversions from 'Double' to 'Integer'.
        Dim e = a With {.X = 1.5}
                             ~~~
</expected>)
        End Sub

        <Fact>
        Public Sub AnonymousTypeWithNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a = New With {.X = 1}
        Dim b = a With {.X = 2}
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30205: End of statement expected.
        Dim b = a With {.X = 2}
                  ~~~~
</expected>)
        End Sub
        <Fact>
        Public Sub CopiedNamesAreTheProperties()
            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim label = New With {.Name = ""a"", .Size = 1}
        Dim bigger = label With {.Size = 3}
        System.Console.WriteLine(bigger.Size)
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim sizes = tree.GetRoot().DescendantNodes().OfType(Of Microsoft.CodeAnalysis.VisualBasic.Syntax.IdentifierNameSyntax)().Where(Function(n) n.Identifier.ValueText = "Size").ToArray()
            Assert.Equal(3, sizes.Length)
            Dim copied = model.GetSymbolInfo(sizes(1)).Symbol
            Assert.Equal(SymbolKind.Property, copied.Kind)
            Assert.Equal(copied, model.GetSymbolInfo(sizes(2)).Symbol)
        End Sub
    End Class
End Namespace
