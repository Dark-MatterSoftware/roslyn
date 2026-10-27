' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class TupleTypeAliasTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub TupleAliases()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports Pair = (Left As Integer, Right As Integer)
Imports Named = (Name As String, Count As Integer), Plain = (Integer, String)
Imports MaybePair = (Integer, Integer)?

Module Program
    Function Swap(p As Pair) As Pair
        Return (p.Right, p.Left)
    End Function

    Sub Main()
        Dim p As Pair = (1, 2)
        Dim s = Swap(p)
        Dim items As New List(Of Named) From {(""a"", 1), (""b"", 2)}
        Dim plain As Plain = (3, ""c"")
        Dim maybe As MaybePair = Nothing
        Console.Write(s.Left & s.Right & "" "" & items(1).Name & items(1).Count & "" "" & plain.Item2 & "" "" & maybe.HasValue)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="21 b2 c False").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleAliasSyntaxAndSemanticModel()
            Dim compilation = CreateCompilation("
Imports Pair = (Left As Integer, Right As Integer)

Module Program
    Dim p As Pair
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees(0)
            Dim clause = tree.GetRoot().DescendantNodes().OfType(Of TypeAliasImportsClauseSyntax)().Single()
            Assert.Equal("Pair", clause.Alias.Identifier.ValueText)
            Assert.Equal(SyntaxKind.TupleType, clause.Type.Kind)

            Dim model = compilation.GetSemanticModel(tree)
            Dim aliasSymbol = DirectCast(model.GetDeclaredSymbol(clause), IAliasSymbol)
            Assert.Equal("Pair", aliasSymbol.Name)
            Assert.Equal("(Left As System.Int32, Right As System.Int32)", aliasSymbol.Target.ToTestDisplayString())
            Assert.Same(clause, aliasSymbol.DeclaringSyntaxReferences.Single().GetSyntax())

            Dim usage = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().Single(Function(n) n.Identifier.ValueText = "Pair")
            Assert.Equal(aliasSymbol, model.GetAliasInfo(usage))
        End Sub

        <Fact>
        Public Sub TupleAliasErrors()
            Dim compilation = CreateCompilation("
Imports Pair = (Integer, Integer)
Imports Pair = (String, String)
Imports Bad = (Integer, Missing)

Module Program
    Dim p As Pair
    Dim b As Bad
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30572: Alias 'Pair' is already declared.
Imports Pair = (String, String)
        ~~~~
BC30002: Type 'Missing' is not defined.
Imports Bad = (Integer, Missing)
                        ~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub PredefinedArrayAndNullableAliases()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic
Imports Num = Integer
Imports Text = String
Imports Ints = Integer()
Imports Grid = Integer(,)
Imports MaybeNum = Integer?
Imports MaybeDay = System.DayOfWeek?
Imports Days = System.DayOfWeek()
Imports Lists = System.Collections.Generic.List(Of String)()

Module Program
    Function Total(values As Ints) As Num
        Dim sum As Num = 0
        For Each value As Num In values
            sum += value
        Next
        Return sum
    End Function

    Sub Main()
        Dim t As Text = ""total""
        Dim g As Grid = New Integer(1, 1) {}
        Dim m As MaybeNum = Nothing
        Dim d As MaybeDay = DayOfWeek.Friday
        Dim days As Days = {DayOfWeek.Monday}
        Dim lists As Lists = {New List(Of String) From {""a""}}
        Console.Write(t & "" "" & Total({1, 2, 3}) & "" "" & g.Rank & "" "" & m.HasValue & "" "" & d.Value.ToString() & "" "" & days.Length & "" "" & lists(0)(0))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="total 6 2 False Friday 1 a").VerifyDiagnostics()
        End Sub

        <Theory>
        <InlineData(DebugInformationFormat.Pdb)>
        <InlineData(DebugInformationFormat.PortablePdb)>
        Public Sub TypeAliasesEmitPdb(format As DebugInformationFormat)
            Dim compilation = CreateCompilation("
Imports Num = Integer
Imports Ints = Integer()
Imports Pair = (Left As Integer, Right As Integer)
Imports MaybeNum = Integer?

Module Program
    Function Use(values As Ints, p As Pair, m As MaybeNum) As Num
        Return values.Length + p.Left + m.GetValueOrDefault()
    End Function
End Module
", options:=TestOptions.DebugDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()

            Using peStream As New IO.MemoryStream(), pdbStream As New IO.MemoryStream()
                Dim result = compilation.Emit(peStream, pdbStream, options:=New EmitOptions(debugInformationFormat:=format))
                result.Diagnostics.Verify()
                Assert.True(result.Success)
                Assert.True(pdbStream.Length > 0)
            End Using
        End Sub

        <Fact>
        Public Sub PredefinedAndArrayAliasSemanticModel()
            Dim compilation = CreateCompilation("
Imports Num = Integer
Imports Ints = Integer()

Module Program
    Dim n As Num
    Dim values As Ints
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees(0)
            Dim clauses = tree.GetRoot().DescendantNodes().OfType(Of TypeAliasImportsClauseSyntax)().ToArray()
            Assert.Equal(SyntaxKind.PredefinedType, clauses(0).Type.Kind)
            Assert.Equal(SyntaxKind.ArrayType, clauses(1).Type.Kind)

            Dim model = compilation.GetSemanticModel(tree)
            Assert.Equal("System.Int32", DirectCast(model.GetDeclaredSymbol(clauses(0)), IAliasSymbol).Target.ToTestDisplayString())
            Assert.Equal("System.Int32()", DirectCast(model.GetDeclaredSymbol(clauses(1)), IAliasSymbol).Target.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub PredefinedAndArrayAliasesNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Imports Num = Integer
Imports Days = System.DayOfWeek()

Module Program
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30183: Keyword is not valid as an identifier.
Imports Num = Integer
              ~~~~~~~
BC40056: Namespace or type specified in the Imports 'Integer' doesn't contain any public member or cannot be found. Make sure the namespace or the type is defined and contains at least one public member. Make sure the imported element name doesn't use any aliases.
Imports Num = Integer
              ~~~~~~~
BC30205: End of statement expected.
Imports Days = System.DayOfWeek()
                               ~
</expected>)
            Assert.Empty(compilation.SyntaxTrees(0).GetRoot().DescendantNodes().OfType(Of TypeAliasImportsClauseSyntax)())
        End Sub

        <Fact>
        Public Sub TupleAliasNotAvailableBeforeVbNext()
            Dim compilation = CreateCompilation("
Imports Pair = (Integer, Integer)

Module Program
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30203: Identifier expected.
Imports Pair = (Integer, Integer)
               ~
BC40056: Namespace or type specified in the Imports '' doesn't contain any public member or cannot be found. Make sure the namespace or the type is defined and contains at least one public member. Make sure the imported element name doesn't use any aliases.
Imports Pair = (Integer, Integer)
               ~~~~~~~~~~~~~~~~~~
BC32093: 'Of' required when specifying type arguments for a generic type or method.
Imports Pair = (Integer, Integer)
                ~
</expected>)
            Assert.Empty(compilation.SyntaxTrees(0).GetRoot().DescendantNodes().OfType(Of TypeAliasImportsClauseSyntax)())
        End Sub
    End Class
End Namespace
