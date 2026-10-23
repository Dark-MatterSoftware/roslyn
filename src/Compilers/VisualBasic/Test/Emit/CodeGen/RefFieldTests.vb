' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class RefFieldTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const CellSource As String = "
ByRef Structure Cell
    Public ByRef Value As Integer

    Sub New(ByRef target As Integer)
        Value = ByRef target
    End Sub

    Sub New(items As Integer(), index As Integer)
        Value = ByRef items(index)
    End Sub

    Sub New(holder As Holder)
        Me.Value = ByRef holder.Count
    End Sub

    Sub New(items As Integer(), useFirst As Boolean)
        Value = ByRef Pick(items, useFirst)
    End Sub

    Private Shared ByRef Function Pick(items As Integer(), useFirst As Boolean) As Integer
        If useFirst Then Return items(0)
        Return items(items.Length - 1)
    End Function
End Structure

Class Holder
    Public Count As Integer
End Class
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefFieldReadsAndWritesThroughToItsVariable()
            Dim compilation = CreateCompilation({CellSource, "
Imports System

Module Program
    Sub Main()
        Dim x = 1
        Dim c As New Cell(x)
        c.Value += 10
        Console.Write(x)
        x = 5
        Console.Write("" "" & c.Value)

        Dim items = {1, 2, 3}
        Dim e As New Cell(items, 1)
        e.Value = 20
        Bump(e.Value)
        Console.Write("" "" & items(1))

        Dim holder As New Holder()
        Dim h As New Cell(holder)
        h.Value = 7
        Dim ByRef view = h.Value
        view *= 3
        Console.Write("" "" & holder.Count)

        Dim last As New Cell(items, False)
        last.Value = 30
        Console.Write("" "" & String.Join("","", items))
    End Sub

    Sub Bump(ByRef value As Integer)
        value += 1
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="11 5 21 21 1,21,30", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RefFieldIsByReferenceInMetadataAndSymbols()
            Dim compilation = CreateCompilation(CellSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim field = compilation.GetTypeByMetadataName("Cell").GetMember(Of FieldSymbol)("Value")
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.Ref, DirectCast(field, IFieldSymbol).RefKind)

            Dim verifier = CompileAndVerify(compilation, verify:=Verification.Skipped)
            verifier.VerifyIL("Cell..ctor(ByRef Integer)", "
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  initobj    ""Cell""
  IL_0007:  ldarg.0
  IL_0008:  ldarg.1
  IL_0009:  stfld      ""Cell.Value As Integer""
  IL_000e:  ret
}
")
        End Sub

        <Fact>
        Public Sub RefFieldLoadAndStoreGoThroughTheReference()
            Dim compilation = CreateCompilation({CellSource, "
Module Program
    Function Twice(c As Cell) As Integer
        c.Value = c.Value * 2
        Return c.Value
    End Function
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.Skipped).VerifyIL("Program.Twice", "
{
  // Code size       25 (0x19)
  .maxstack  3
  IL_0000:  ldarga.s   V_0
  IL_0002:  ldfld      ""Cell.Value As Integer""
  IL_0007:  ldarg.0
  IL_0008:  ldfld      ""Cell.Value As Integer""
  IL_000d:  ldind.i4
  IL_000e:  ldc.i4.2
  IL_000f:  mul.ovf
  IL_0010:  stind.i4
  IL_0011:  ldarg.0
  IL_0012:  ldfld      ""Cell.Value As Integer""
  IL_0017:  ldind.i4
  IL_0018:  ret
}
")
        End Sub

        <Fact>
        Public Sub ByRefAssignmentParsesOnlyOnVbNext()
            Dim source = "
Class C
    Sub M(ByRef x As Integer)
        y = ByRef x
    End Sub
End Class
"
            Dim vbNextTree = SyntaxFactory.ParseSyntaxTree(source, VbNext)
            Assert.Empty(vbNextTree.GetDiagnostics())
            Dim byRefExpression = vbNextTree.GetRoot().DescendantNodes().OfType(Of ByRefExpressionSyntax)().Single()
            Assert.Equal("x", byRefExpression.Expression.ToString())

            Dim oldTree = SyntaxFactory.ParseSyntaxTree(source, TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            Assert.NotEmpty(oldTree.GetDiagnostics())
            Assert.Empty(oldTree.GetRoot().DescendantNodes().OfType(Of ByRefExpressionSyntax)())
        End Sub

        <Fact>
        Public Sub ByRefFieldOutsideByRefStructureIsAnError()
            Dim compilation = CreateCompilation("
Structure Plain
    Public ByRef Value As Integer
End Structure

ByRef Structure Cell
    Public Shared ByRef Total As Integer
End Structure
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30235: 'ByRef' is not valid on a member variable declaration.
    Public ByRef Value As Integer
           ~~~~~
BC30235: 'ByRef' is not valid on a member variable declaration.
    Public Shared ByRef Total As Integer
                  ~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub RefFieldNeedsRuntimeSupport()
            Dim compilation = CreateCompilation("
ByRef Structure Cell
    Public ByRef Value As Integer
End Structure
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37409: The target runtime does not support ref fields.
    Public ByRef Value As Integer
           ~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub ByRefAssignmentOnlySetsARefFieldOfMeInSubNew()
            Dim compilation = CreateCompilation("
ByRef Structure Cell
    Public ByRef Value As Integer
    Public Plain As Integer

    Sub New(ByRef target As Integer, other As Cell)
        Plain = ByRef target
        other.Value = ByRef target
    End Sub

    Sub Reset(ByRef target As Integer)
        Value = ByRef target
    End Sub
End Structure
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37410: '= ByRef' can only make a ref field of this structure refer to a variable, in a 'Sub New' of the structure.
        Plain = ByRef target
        ~~~~~
BC37410: '= ByRef' can only make a ref field of this structure refer to a variable, in a 'Sub New' of the structure.
        other.Value = ByRef target
        ~~~~~~~~~~~
BC37410: '= ByRef' can only make a ref field of this structure refer to a variable, in a 'Sub New' of the structure.
        Value = ByRef target
        ~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub RefFieldReferentMustOutliveTheStructure()
            Dim compilation = CreateCompilation("
ByRef Structure Cell
    Public ByRef Value As Integer
    Public ByRef Wide As Long

    Sub New(target As Integer, ByRef wide As Integer)
        Dim local = 1
        Value = ByRef local
        Value = ByRef target
        Value = ByRef 42
        Me.Wide = ByRef wide
    End Sub
End Structure
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37411: A ref field can refer only to a 'ByRef' parameter of 'Sub New', an array element, a field of a class, or the result of a 'ByRef' function, property, or indexer.
        Value = ByRef local
                      ~~~~~
BC37411: A ref field can refer only to a 'ByRef' parameter of 'Sub New', an array element, a field of a class, or the result of a 'ByRef' function, property, or indexer.
        Value = ByRef target
                      ~~~~~~
BC37411: A ref field can refer only to a 'ByRef' parameter of 'Sub New', an array element, a field of a class, or the result of a 'ByRef' function, property, or indexer.
        Value = ByRef 42
                      ~~
BC37412: A ref field of type 'Long' cannot refer to a variable of type 'Integer'.
        Me.Wide = ByRef wide
                        ~~~~
</expected>)
        End Sub

    End Class

End Namespace
