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
  IL_0009:  stfld      ""ByRef Cell.Value As Integer""
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
  // Code size       24 (0x18)
  .maxstack  3
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""ByRef Cell.Value As Integer""
  IL_0006:  ldarg.0
  IL_0007:  ldfld      ""ByRef Cell.Value As Integer""
  IL_000c:  ldind.i4
  IL_000d:  ldc.i4.2
  IL_000e:  mul.ovf
  IL_000f:  stind.i4
  IL_0010:  ldarg.0
  IL_0011:  ldfld      ""ByRef Cell.Value As Integer""
  IL_0016:  ldind.i4
  IL_0017:  ret
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

        Private Function CSharpRefFieldLibrary() As MetadataReference
            Dim compilation = CreateCSharpCompilation("
public ref struct RefBox
{
    public ref int Value;
    public ref readonly int Frozen;
    public RefBox(ref int value) { Value = ref value; Frozen = ref value; }
}

public ref struct GenericBox<T>
{
    public ref T Item;
    public GenericBox(ref T item) { Item = ref item; }
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90))
            compilation.VerifyDiagnostics()
            Return compilation.EmitToImageReference()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefFieldsFromCSharpReadAndWriteThrough()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim x = 1
        Dim box As New RefBox(x)
        box.Value += 10
        Console.Write(x)
        Bump(box.Value)
        Console.Write("" "" & x & "" "" & box.Frozen)

        Dim s = ""a""
        Dim g As New GenericBox(Of String)(s)
        g.Item = ""b""
        Console.Write("" "" & s)
    End Sub

    Sub Bump(ByRef value As Integer)
        value += 1
    End Sub
End Module
", references:={CSharpRefFieldLibrary()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="11 12 12 b", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub RefFieldOfGenericByRefStructure()
            Dim compilation = CreateCompilation("
Imports System

ByRef Structure Slot(Of T)
    Public ByRef Value As T

    Sub New(ByRef target As T)
        Value = ByRef target
    End Sub
End Structure

Module Program
    Sub Main()
        Dim s = ""a""
        Dim slot As New Slot(Of String)(s)
        slot.Value = ""b""
        Console.Write(s)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="b", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RefFieldsFromCSharpSymbols()
            Dim compilation = CreateCompilation("", references:={CSharpRefFieldLibrary()}, targetFramework:=TargetFramework.Net90, parseOptions:=VbNext)

            Dim value = DirectCast(compilation.GetTypeByMetadataName("RefBox").GetMembers("Value").Single(), IFieldSymbol)
            Dim frozen = DirectCast(compilation.GetTypeByMetadataName("RefBox").GetMembers("Frozen").Single(), IFieldSymbol)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.Ref, value.RefKind)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.RefReadOnly, frozen.RefKind)
            Assert.Equal("ByRef RefBox.Value As Integer", value.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
            Assert.Equal("ByRef ReadOnly RefBox.Frozen As Integer", frozen.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
            Assert.Null(DirectCast(value, FieldSymbol).GetUseSiteErrorInfo())
        End Sub

        <Fact>
        Public Sub RefFieldsFromCSharpErrors()
            Dim source = "
Module Program
    Sub Test(ByRef x As Integer)
        Dim box As New RefBox(x)
        box.Frozen = 5
        System.Console.Write(box.Value)
    End Sub
End Module
"
            Dim compilation = CreateCompilation(source, references:={CSharpRefFieldLibrary()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertTheseDiagnostics(
<expected>
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        box.Frozen = 5
        ~~~~~~~~~~
</expected>)

            compilation = CreateCompilation(source, references:={CSharpRefFieldLibrary()}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll,
                                            parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))
            compilation.AssertTheseDiagnostics(
<expected>
BC30668: 'RefBox' is obsolete: 'Types with embedded references are not supported in this version of your compiler.'.
        Dim box As New RefBox(x)
                       ~~~~~~
BC30656: Field 'Frozen' is of an unsupported type.
        box.Frozen = 5
        ~~~~~~~~~~
BC30656: Field 'Value' is of an unsupported type.
        System.Console.Write(box.Value)
                             ~~~~~~~~~
</expected>)
        End Sub

        Private Const ViewSource As String = "
Imports System.Collections.Immutable

ByRef Structure View
    Public ByRef ReadOnly Value As Integer

    Sub New(ByRef target As Integer)
        Value = ByRef target
    End Sub

    Sub New(settings As Settings)
        Value = ByRef settings.Limit
    End Sub

    Sub New(items As ImmutableArray(Of Integer), index As Integer)
        Value = ByRef items.ItemRef(index)
    End Sub
End Structure

Class Settings
    Public ReadOnly Limit As Integer

    Sub New(limit As Integer)
        Me.Limit = limit
    End Sub
End Class
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ByRefReadOnlyFieldReadsItsVariable()
            Dim compilation = CreateCompilation({ViewSource, "
Imports System
Imports System.Collections.Immutable

Module Program
    Sub Main()
        Dim x = 1
        Dim v As New View(x)
        x = 5
        Console.Write(v.Value)
        Bump(v.Value)
        Console.Write("" "" & x)

        Dim ByRef ReadOnly r = v.Value
        x = 8
        Console.Write("" "" & r)

        Console.Write("" "" & New View(New Settings(3)).Value)
        Console.Write("" "" & New View(ImmutableArray.Create(4, 6), 1).Value)
    End Sub

    Sub Bump(ByRef value As Integer)
        value += 1
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5 5 8 3 6", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RefFieldsReadAsRefAndRefReadOnlyFromCSharp()
            Dim compilation = CreateCompilation({ViewSource.Replace("ByRef Structure View", "Public ByRef Structure View").Replace("Class Settings", "Public Class Settings"),
                                                 CellSource.Replace("ByRef Structure Cell", "Public ByRef Structure Cell").Replace("Class Holder", "Public Class Holder")},
                                                targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
class D
{
    static int Read(View v) => v.Value;
    static void Write(View v) { v.Value = 1; }
    static void WriteCell(Cell c) { c.Value = 1; }
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {compilation.EmitToImageReference()}))

            Dim view = DirectCast(csCompilation.GetTypeByMetadataName("View").GetMembers("Value").Single(), IFieldSymbol)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.RefReadOnly, view.RefKind)
            Dim cell = DirectCast(csCompilation.GetTypeByMetadataName("Cell").GetMembers("Value").Single(), IFieldSymbol)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.Ref, cell.RefKind)

            Dim errors = csCompilation.GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error).ToArray()
            Assert.True(errors.Length = 1, String.Join(vbLf, errors.Select(Function(d) d.ToString())))
            Assert.Equal("v.Value", errors(0).Location.SourceTree.GetText().ToString(errors(0).Location.SourceSpan))
            Assert.Equal(4, errors(0).Location.GetLineSpan().StartLinePosition.Line)
        End Sub

        <Fact>
        Public Sub StructureHoldingRefFieldCannotOutliveItsReferents()
            Dim compilation = CreateCompilation("
Imports System

ByRef Structure Cell
    Public ByRef Value As Integer

    Sub New(ByRef target As Integer)
        Value = ByRef target
    End Sub
End Structure

ByRef Structure Pair
    Public First As Cell

    Sub Reset(ByRef target As Integer)
        First = New Cell(target)
    End Sub
End Structure

ByRef Structure Plain
    Public Count As Integer
End Structure

Module Program
    Function Make(ByRef x As Integer) As Cell
        Return New Cell(x)
    End Function

    ReadOnly Property Current As Pair
        Get
            Return New Pair()
        End Get
    End Property

    Function MakePlain() As Plain
        Return New Plain()
    End Function

    Function MakeSpan(items As Integer()) As Span(Of Integer)
        Return items.AsSpan()
    End Function

    Sub Store(ByRef c As Cell, ByRef p As Pair)
        Dim local = 1
        c = New Cell(local)
        p.First = New Cell(local)

        Dim copy As Cell = c
        copy = New Cell(local)
        Dim pair As New Pair()
        pair.First = copy
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37415: 'Cell' holds a ref field, so it cannot be assigned to a 'ByRef' parameter or to a field of a structure reached through a 'ByRef' parameter or 'Me'.
        First = New Cell(target)
        ~~~~~
BC37414: 'Cell' holds a ref field, so it cannot be the return type of a function or property.
    Function Make(ByRef x As Integer) As Cell
                                         ~~~~
BC37414: 'Pair' holds a ref field, so it cannot be the return type of a function or property.
    ReadOnly Property Current As Pair
                                 ~~~~
BC37415: 'Cell' holds a ref field, so it cannot be assigned to a 'ByRef' parameter or to a field of a structure reached through a 'ByRef' parameter or 'Me'.
        c = New Cell(local)
        ~
BC37415: 'Cell' holds a ref field, so it cannot be assigned to a 'ByRef' parameter or to a field of a structure reached through a 'ByRef' parameter or 'Me'.
        p.First = New Cell(local)
        ~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub ByRefReadOnlyFieldCannotBeWrittenThrough()
            Dim compilation = CreateCompilation("
ByRef Structure View
    Public ByRef ReadOnly Value As Integer
    Public ByRef Cell As Integer

    Sub New(ByRef target As Integer, holder As Holder)
        Value = ByRef target
        Value = 2
        Value += 1
        Cell = ByRef holder.Limit
    End Sub

    Sub Reset()
        Value = 3
        Dim ByRef r = Value
    End Sub
End Structure

Class Holder
    Public ReadOnly Limit As Integer
End Class
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        Value = 2
        ~~~~~
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        Value += 1
        ~~~~~
BC37413: Only a 'ByRef ReadOnly' field can refer to a 'ReadOnly' field or a read-only 'ByRef' result.
        Cell = ByRef holder.Limit
                     ~~~~~~~~~~~~
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        Value = 3
        ~~~~~
BC37406: A 'ByRef' local must be initialized with a variable or with the result of a 'ByRef' function, property, or indexer.
        Dim ByRef r = Value
                      ~~~~~
</expected>)
        End Sub

    End Class

End Namespace
