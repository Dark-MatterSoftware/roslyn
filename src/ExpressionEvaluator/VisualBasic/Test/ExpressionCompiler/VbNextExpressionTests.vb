' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.ObjectModel
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.ExpressionEvaluator
Imports Microsoft.CodeAnalysis.ExpressionEvaluator.UnitTests
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator.UnitTests
    ''' <summary>
    ''' The expression evaluator parses with the language version that a module's portable PDB records, so the Watch and
    ''' Immediate windows take vbnext forms in a module built with vbnext.
    ''' </summary>
    Public Class VbNextExpressionTests
        Inherits ExpressionCompilerTestBase

        Private ReadOnly _output As Xunit.Abstractions.ITestOutputHelper

        Public Sub New(output As Xunit.Abstractions.ITestOutputHelper)
            _output = output
        End Sub

        Private Const Source = "
Structure Spot
    Public X As Integer
    Public Y As Integer
End Structure
Class C
    Shared Sub M(a As Integer(), s As Spot, n As Integer)
    End Sub
End Class
"

        Private Shared ReadOnly s_vbNextExpressions As String() = {
            "a(^1)",
            "a(1..2)",
            "s With {.X = 5}",
            "New With {.A = 1} With {.A = 2}",
            "Select Case n" & vbCrLf & "Case Is > 0" & vbCrLf & """positive""" & vbCrLf & "Case Else" & vbCrLf & """other""" & vbCrLf & "End Select"}

        Private Function Compile(parseOptions As VisualBasicParseOptions, debugFormat As DebugInformationFormat, expr As String, ByRef formatSpecifiers As ReadOnlyCollection(Of String)) As String
            Dim comp = CreateCompilationWithMscorlib40({Source}, options:=TestOptions.DebugDll, parseOptions:=parseOptions)
            Dim runtime = CreateRuntimeInstance(comp, debugFormat:=debugFormat)
            Dim context = CreateMethodContext(runtime, "C.M")
            Dim errorMessage As String = Nothing
            Dim result = context.CompileExpression(expr, errorMessage, formatter:=DebuggerDiagnosticFormatter.Instance)
            formatSpecifiers = result?.FormatSpecifiers
            Return If(errorMessage, If(result Is Nothing, "no result", "OK"))
        End Function

        <Fact>
        Public Sub VbNextModule_EvaluatesVbNextForms()
            Dim vbNext = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
            For Each expr In s_vbNextExpressions
                Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
                Assert.Equal(expr & ": OK", expr & ": " & Compile(vbNext, DebugInformationFormat.PortablePdb, expr, formatSpecifiers))
            Next
        End Sub

        <Fact>
        Public Sub VbNextModule_KeepsFormatSpecifiers()
            Dim vbNext = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
            Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
            Assert.Equal("OK", Compile(vbNext, DebugInformationFormat.PortablePdb, "a(^1), h", formatSpecifiers))
            Assert.Equal({"h"}, formatSpecifiers)
        End Sub

        <Fact>
        Public Sub Vb17_13Module_KeepsCurrentSyntax()
            Dim vb17_13 = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
            Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
            Assert.Equal("error BC30201: Expression expected.", Compile(vb17_13, DebugInformationFormat.PortablePdb, "a(^1)", formatSpecifiers))
        End Sub

        <Fact>
        Public Sub WindowsPdb_KeepsCurrentSyntax()
            Dim vbNext = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
            Dim formatSpecifiers As ReadOnlyCollection(Of String) = Nothing
            Assert.Equal("error BC30201: Expression expected.", Compile(vbNext, DebugInformationFormat.Pdb, "a(^1)", formatSpecifiers))
        End Sub

        Private Const ArrayRangeSource = "
Namespace System
    Public Structure Index
        Private ReadOnly _value As Integer
        Public Sub New(value As Integer, fromEnd As Boolean)
            _value = If(fromEnd, Not value, value)
        End Sub
        Public ReadOnly Property Value As Integer
            Get
                Return If(_value < 0, Not _value, _value)
            End Get
        End Property
        Public ReadOnly Property IsFromEnd As Boolean
            Get
                Return _value < 0
            End Get
        End Property
        Public Function GetOffset(length As Integer) As Integer
            Return If(_value < 0, length + _value + 1, _value)
        End Function
    End Structure
    Public Structure Range
        Private ReadOnly _start As Index
        Private ReadOnly _end As Index
        Public Sub New(start As Index, [end] As Index)
            _start = start
            _end = [end]
        End Sub
        Public ReadOnly Property Start As Index
            Get
                Return _start
            End Get
        End Property
        Public ReadOnly Property [End] As Index
            Get
                Return _end
            End Get
        End Property
    End Structure
End Namespace
Class D
    Shared Sub M()
        Dim a As Integer() = {10, 20, 30, 40, 50}
        Dim r As System.Range = 1..^1
        System.Console.WriteLine(a.Length)
        System.Console.WriteLine(r.Start.Value)
    End Sub
End Class
"

        ''' <summary>
        ''' Watch evaluation of an array slice of a local of the paused frame: the generated code must copy the elements
        ''' of the frame's array (V_0) into the array it allocates, and return that same array.
        ''' </summary>
        <Theory>
        <InlineData("a(1..^1)")>
        <InlineData("a(r)")>
        Public Sub VbNextModule_ArraySliceOfFrameLocal_IL(expr As String)
            Dim comp = CreateCompilationWithMscorlib40({ArrayRangeSource}, options:=TestOptions.DebugDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))
            comp.VerifyDiagnostics()
            ' A portable PDB records vbnext, so the evaluator parses vbnext forms (as in Compile above).
            Dim runtime = CreateRuntimeInstance(comp, debugFormat:=DebugInformationFormat.PortablePdb)
            Dim context = CreateMethodContext(runtime, "D.M")
            Dim errorMessage As String = Nothing
            Dim testData = New CompilationTestData()
            context.CompileExpression(expr:=expr, error:=errorMessage, testData:=testData, formatter:=DebuggerDiagnosticFormatter.Instance)
            Assert.Null(errorMessage)
            Dim expectedIL As String
            Select Case expr
                Case "a(1..^1)"
                    ' start = 1, end = a.Length - 1, result = New Integer(end - start), Array.Copy(a, start, result, 0, end - start), return result
                    expectedIL = "
{
  // Code size       37 (0x25)
  .maxstack  6
  .locals init (Integer() V_0, //a
                System.Range V_1, //r
                System.Index V_2,
                Integer V_3,
                Integer V_4,
                Integer() V_5)
  IL_0000:  ldc.i4.1
  IL_0001:  stloc.3
  IL_0002:  ldloc.0
  IL_0003:  ldlen
  IL_0004:  conv.i4
  IL_0005:  ldc.i4.1
  IL_0006:  sub.ovf
  IL_0007:  stloc.s    V_4
  IL_0009:  ldloc.s    V_4
  IL_000b:  ldloc.3
  IL_000c:  sub.ovf
  IL_000d:  newarr     ""Integer""
  IL_0012:  stloc.s    V_5
  IL_0014:  ldloc.0
  IL_0015:  ldloc.3
  IL_0016:  ldloc.s    V_5
  IL_0018:  ldc.i4.0
  IL_0019:  ldloc.s    V_4
  IL_001b:  ldloc.3
  IL_001c:  sub.ovf
  IL_001d:  call       ""Sub System.Array.Copy(System.Array, Integer, System.Array, Integer, Integer)""
  IL_0022:  ldloc.s    V_5
  IL_0024:  ret
}"
                Case "a(r)"
                    ' r is stored once, start = r.Start.GetOffset(a.Length), end = r.End.GetOffset(a.Length), then the same allocation, copy and return
                    expectedIL = "
{
  // Code size       72 (0x48)
  .maxstack  6
  .locals init (Integer() V_0, //a
                System.Range V_1, //r
                System.Index V_2,
                Integer V_3,
                Integer V_4,
                System.Range V_5,
                Integer() V_6,
                System.Index V_7)
  IL_0000:  ldloc.1
  IL_0001:  stloc.s    V_5
  IL_0003:  ldloca.s   V_5
  IL_0005:  call       ""Function System.Range.get_Start() As System.Index""
  IL_000a:  stloc.s    V_7
  IL_000c:  ldloca.s   V_7
  IL_000e:  ldloc.0
  IL_000f:  ldlen
  IL_0010:  conv.i4
  IL_0011:  call       ""Function System.Index.GetOffset(Integer) As Integer""
  IL_0016:  stloc.3
  IL_0017:  ldloca.s   V_5
  IL_0019:  call       ""Function System.Range.get_End() As System.Index""
  IL_001e:  stloc.s    V_7
  IL_0020:  ldloca.s   V_7
  IL_0022:  ldloc.0
  IL_0023:  ldlen
  IL_0024:  conv.i4
  IL_0025:  call       ""Function System.Index.GetOffset(Integer) As Integer""
  IL_002a:  stloc.s    V_4
  IL_002c:  ldloc.s    V_4
  IL_002e:  ldloc.3
  IL_002f:  sub.ovf
  IL_0030:  newarr     ""Integer""
  IL_0035:  stloc.s    V_6
  IL_0037:  ldloc.0
  IL_0038:  ldloc.3
  IL_0039:  ldloc.s    V_6
  IL_003b:  ldc.i4.0
  IL_003c:  ldloc.s    V_4
  IL_003e:  ldloc.3
  IL_003f:  sub.ovf
  IL_0040:  call       ""Sub System.Array.Copy(System.Array, Integer, System.Array, Integer, Integer)""
  IL_0045:  ldloc.s    V_6
  IL_0047:  ret
}"
                Case Else
                    Throw New System.ArgumentException(expr)
            End Select
            testData.GetMethodData("<>x.<>m0").VerifyIL(expectedIL)
        End Sub

        ''' <summary>
        ''' Compiles an expression in a frame's evaluation context, passing the per-evaluation
        ''' useArraySliceLoop option. Returns the errors, or "" when there are none. By default, the frame is D.M in a vbnext
        ''' module compiled from ArrayRangeSource.
        ''' </summary>
        Private Function CompileSliceExpression(expr As String, enabled As Boolean, testData As CompilationTestData,
                                                Optional sources As String() = Nothing,
                                                Optional methodName As String = "D.M",
                                                Optional parseOptions As VisualBasicParseOptions = Nothing,
                                                Optional atLineNumber As Integer = -1,
                                                Optional debugFormat As DebugInformationFormat = DebugInformationFormat.PortablePdb) As String
            Dim comp = CreateCompilationWithMscorlib40(If(sources, {ArrayRangeSource}), options:=TestOptions.DebugDll, parseOptions:=If(parseOptions, TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)))
            comp.VerifyDiagnostics()
            Dim runtime = CreateRuntimeInstance(comp, debugFormat:=debugFormat)
            Dim context = CreateMethodContext(runtime, methodName, atLineNumber:=atLineNumber)
            Dim diagnostics = DiagnosticBag.GetInstance()
            Dim resultProperties As ResultProperties = Nothing
            context.CompileExpressionCore(
                expr,
                Microsoft.VisualStudio.Debugger.Evaluation.DkmEvaluationFlags.TreatAsExpression,
                System.Collections.Immutable.ImmutableArray(Of [Alias]).Empty,
                diagnostics,
                resultProperties,
                testData,
                useArraySliceLoop:=enabled)
            Dim errors = String.Join(vbCrLf, diagnostics.AsEnumerable().Where(Function(d) d.Severity = DiagnosticSeverity.Error).Select(Function(d) d.ToString()))
            diagnostics.Free()
            Return errors
        End Function

        ''' <summary>
        ''' The IL that a frame's evaluator generates for an expression, with the loop rewrite on or off
        ''' (through the per-evaluation option; there is no shared switch).
        ''' </summary>
        Private Function SliceRewriterIL(expr As String, enabled As Boolean, Optional ByRef methodData As CompilationTestData.MethodData = Nothing,
                                         Optional sources As String() = Nothing,
                                         Optional methodName As String = "D.M",
                                         Optional parseOptions As VisualBasicParseOptions = Nothing,
                                         Optional atLineNumber As Integer = -1,
                                         Optional methodDataName As String = "<>x.<>m0",
                                         Optional debugFormat As DebugInformationFormat = DebugInformationFormat.PortablePdb) As String
            Dim testData = New CompilationTestData()
            Dim errors = CompileSliceExpression(expr, enabled, testData, sources, methodName, parseOptions, atLineNumber, debugFormat)
            Assert.Equal("", errors)
            methodData = testData.GetMethodData(methodDataName)
            Dim il = methodData.GetMethodIL()
            _output.WriteLine($"{methodName}: {expr} (rewriter {If(enabled, "on", "off")}):{vbCrLf}{il}")
            Return il
        End Function

        Private Shared Function ILLines(il As String) As String()
            Return il.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).Select(Function(line) line.Trim()).Where(Function(line) line.StartsWith("IL_", StringComparison.Ordinal)).ToArray()
        End Function

        Private Shared Function CountOf(text As String, value As String) As Integer
            Return text.Split({value}, StringSplitOptions.None).Length - 1
        End Function

        ''' <summary>
        ''' A slice that is the whole expression runs its own side effects exactly as bound, through the allocation
        ''' of the result, then copies with a loop. The original Array.Copy call remains only on the path where it throws.
        ''' </summary>
        <Theory>
        <InlineData("a(1..^1)")>
        <InlineData("a(r)")>
        Public Sub SliceRewriter_WholeExpressionSlice_CopiesWithLoop(expr As String)
            Dim original = SliceRewriterIL(expr, enabled:=False)
            Dim rewrittenData As CompilationTestData.MethodData = Nothing
            Dim rewritten = SliceRewriterIL(expr, enabled:=True, methodData:=rewrittenData)
            Dim rewrittenLines = ILLines(rewritten)

            ' The exact IL: the slice's own side effects as bound (the same instructions as with the rewriter off; only the
            ' slot of the System.Index temporary differs, because the loop index and the copy source are declared first), through
            ' the store of the allocated result, then the source read once into the copy source, the guard, the copy
            ' loop, the original Array.Copy call on the copy source where it throws, and the result.
            Dim expectedIL As String
            Select Case expr
                Case "a(1..^1)"
                    expectedIL = "
{
  // Code size       90 (0x5a)
  .maxstack  6
  .locals init (Integer() V_0, //a
                System.Range V_1, //r
                System.Index V_2,
                Integer V_3,
                Integer V_4,
                Integer() V_5,
                Integer V_6,
                Integer() V_7)
  IL_0000:  ldc.i4.1
  IL_0001:  stloc.3
  IL_0002:  ldloc.0
  IL_0003:  ldlen
  IL_0004:  conv.i4
  IL_0005:  ldc.i4.1
  IL_0006:  sub.ovf
  IL_0007:  stloc.s    V_4
  IL_0009:  ldloc.s    V_4
  IL_000b:  ldloc.3
  IL_000c:  sub.ovf
  IL_000d:  newarr     ""Integer""
  IL_0012:  stloc.s    V_5
  IL_0014:  ldloc.0
  IL_0015:  stloc.s    V_7
  IL_0017:  ldloc.s    V_7
  IL_0019:  brfalse.s  IL_0048
  IL_001b:  ldloc.3
  IL_001c:  ldc.i4.0
  IL_001d:  blt.s      IL_0048
  IL_001f:  ldloc.s    V_4
  IL_0021:  ldloc.s    V_7
  IL_0023:  ldlen
  IL_0024:  conv.i4
  IL_0025:  bgt.s      IL_0048
  IL_0027:  ldc.i4.0
  IL_0028:  stloc.s    V_6
  IL_002a:  br.s       IL_003e
  IL_002c:  ldloc.s    V_5
  IL_002e:  ldloc.s    V_6
  IL_0030:  ldloc.s    V_7
  IL_0032:  ldloc.3
  IL_0033:  ldloc.s    V_6
  IL_0035:  add
  IL_0036:  ldelem.i4
  IL_0037:  stelem.i4
  IL_0038:  ldloc.s    V_6
  IL_003a:  ldc.i4.1
  IL_003b:  add
  IL_003c:  stloc.s    V_6
  IL_003e:  ldloc.s    V_6
  IL_0040:  ldloc.s    V_4
  IL_0042:  ldloc.3
  IL_0043:  sub
  IL_0044:  blt.s      IL_002c
  IL_0046:  br.s       IL_0057
  IL_0048:  ldloc.s    V_7
  IL_004a:  ldloc.3
  IL_004b:  ldloc.s    V_5
  IL_004d:  ldc.i4.0
  IL_004e:  ldloc.s    V_4
  IL_0050:  ldloc.3
  IL_0051:  sub.ovf
  IL_0052:  call       ""Sub System.Array.Copy(System.Array, Integer, System.Array, Integer, Integer)""
  IL_0057:  ldloc.s    V_5
  IL_0059:  ret
}"
                Case "a(r)"
                    expectedIL = "
{
  // Code size      125 (0x7d)
  .maxstack  6
  .locals init (Integer() V_0, //a
                System.Range V_1, //r
                System.Index V_2,
                Integer V_3,
                Integer V_4,
                System.Range V_5,
                Integer() V_6,
                Integer V_7,
                Integer() V_8,
                System.Index V_9)
  IL_0000:  ldloc.1
  IL_0001:  stloc.s    V_5
  IL_0003:  ldloca.s   V_5
  IL_0005:  call       ""Function System.Range.get_Start() As System.Index""
  IL_000a:  stloc.s    V_9
  IL_000c:  ldloca.s   V_9
  IL_000e:  ldloc.0
  IL_000f:  ldlen
  IL_0010:  conv.i4
  IL_0011:  call       ""Function System.Index.GetOffset(Integer) As Integer""
  IL_0016:  stloc.3
  IL_0017:  ldloca.s   V_5
  IL_0019:  call       ""Function System.Range.get_End() As System.Index""
  IL_001e:  stloc.s    V_9
  IL_0020:  ldloca.s   V_9
  IL_0022:  ldloc.0
  IL_0023:  ldlen
  IL_0024:  conv.i4
  IL_0025:  call       ""Function System.Index.GetOffset(Integer) As Integer""
  IL_002a:  stloc.s    V_4
  IL_002c:  ldloc.s    V_4
  IL_002e:  ldloc.3
  IL_002f:  sub.ovf
  IL_0030:  newarr     ""Integer""
  IL_0035:  stloc.s    V_6
  IL_0037:  ldloc.0
  IL_0038:  stloc.s    V_8
  IL_003a:  ldloc.s    V_8
  IL_003c:  brfalse.s  IL_006b
  IL_003e:  ldloc.3
  IL_003f:  ldc.i4.0
  IL_0040:  blt.s      IL_006b
  IL_0042:  ldloc.s    V_4
  IL_0044:  ldloc.s    V_8
  IL_0046:  ldlen
  IL_0047:  conv.i4
  IL_0048:  bgt.s      IL_006b
  IL_004a:  ldc.i4.0
  IL_004b:  stloc.s    V_7
  IL_004d:  br.s       IL_0061
  IL_004f:  ldloc.s    V_6
  IL_0051:  ldloc.s    V_7
  IL_0053:  ldloc.s    V_8
  IL_0055:  ldloc.3
  IL_0056:  ldloc.s    V_7
  IL_0058:  add
  IL_0059:  ldelem.i4
  IL_005a:  stelem.i4
  IL_005b:  ldloc.s    V_7
  IL_005d:  ldc.i4.1
  IL_005e:  add
  IL_005f:  stloc.s    V_7
  IL_0061:  ldloc.s    V_7
  IL_0063:  ldloc.s    V_4
  IL_0065:  ldloc.3
  IL_0066:  sub
  IL_0067:  blt.s      IL_004f
  IL_0069:  br.s       IL_007a
  IL_006b:  ldloc.s    V_8
  IL_006d:  ldloc.3
  IL_006e:  ldloc.s    V_6
  IL_0070:  ldc.i4.0
  IL_0071:  ldloc.s    V_4
  IL_0073:  ldloc.3
  IL_0074:  sub.ovf
  IL_0075:  call       ""Sub System.Array.Copy(System.Array, Integer, System.Array, Integer, Integer)""
  IL_007a:  ldloc.s    V_6
  IL_007c:  ret
}"
                Case Else
                    Throw New System.ArgumentException(expr)
            End Select
            rewrittenData.VerifyIL(expectedIL)

            Assert.Equal(1, CountOf(original, "System.Array.Copy"))
            Assert.DoesNotContain("stelem", original)
            Assert.Equal(1, CountOf(rewritten, "System.Array.Copy"))
            Assert.Equal(1, CountOf(rewritten, "newarr"))
            Assert.Contains("ldelem.i4", rewritten)
            Assert.Contains("stelem.i4", rewritten)
            Assert.Contains(rewrittenLines, Function(line) line.Contains(" blt"))
        End Sub

        ''' <summary>
        ''' A slice inside a larger expression is left as bound.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_NestedSlice_IsNotRewritten()
            Dim original = SliceRewriterIL("a(1..2)(0)", enabled:=False)
            Dim rewritten = SliceRewriterIL("a(1..2)(0)", enabled:=True)
            Assert.Equal(original, rewritten)
            Assert.Equal(1, CountOf(rewritten, "System.Array.Copy"))
            Assert.DoesNotContain("stelem", rewritten)
        End Sub

        ''' <summary>
        ''' The rewritten slice reads its source exactly as often as the original, through the allocation of the
        ''' result, and then once more into the copy source, where the original Array.Copy call reads its source argument.
        ''' The guard, the loop and the fallback Array.Copy call read only the copy source.
        ''' </summary>
        <Theory>
        <InlineData("byRefArray(1..^1)", "S.M", -1, "ldarg.3", "ldind.ref")>
        <InlineData("captured(1..^1)", "S.L", 999, "$VB$Local_captured", Nothing)>
        <InlineData("strings(1..^1)", "S.M", -1, "ldarg.1", Nothing)>
        Public Sub SliceRewriter_SourceIsReadOnceForCopy(expr As String, frame As String, atLineNumber As Integer, sourceLoad As String, dereference As String)
            Dim original = SliceRewriterIL(expr, enabled:=False, sources:=s_sliceSources, methodName:=frame, atLineNumber:=atLineNumber)
            Dim rewritten = SliceRewriterIL(expr, enabled:=True, sources:=s_sliceSources, methodName:=frame, atLineNumber:=atLineNumber)
            VerifyLoopRewrite(original, rewritten)

            Dim lines = ILLines(rewritten)
            Dim loads = System.Linq.Enumerable.Range(0, lines.Length).Where(Function(i) lines(i).Contains(sourceLoad)).ToArray()
            ' As many reads of the source as the original: the same reads in the side effects, and the Array.Copy argument's read becomes the capture.
            Assert.Equal(CountOf(original, sourceLoad), loads.Length)
            Dim capture = loads.Last()
            Assert.True(System.Array.FindIndex(lines, Function(line) line.Contains("newarr")) < capture)
            Dim store = capture + 1
            If dereference IsNot Nothing Then
                Assert.Contains(dereference, lines(store))
                store += 1
            End If
            Assert.Contains("stloc", lines(store))
            Dim copySource = lines(store).Split(" "c).Last()
            Assert.StartsWith("V_", copySource)
            Dim copySourceReads = System.Linq.Enumerable.Range(0, lines.Length).Where(Function(i) lines(i).Contains("ldloc") AndAlso lines(i).EndsWith(" " & copySource, StringComparison.Ordinal)).ToArray()
            ' The null check, the length check, the loop's element read, and the fallback Array.Copy call's source argument.
            Assert.Equal(4, copySourceReads.Length)
            Assert.True(copySourceReads.All(Function(i) i > store))
            Assert.Equal(store + 1, copySourceReads(0))
            Assert.Contains("brfalse", lines(store + 2))
            Dim copyCall = System.Array.FindIndex(lines, Function(line) line.Contains("System.Array.Copy"))
            Assert.Equal(copyCall - 7, copySourceReads(3))
        End Sub

        ''' <summary>
        ''' A spilled receiver. Me._items is read once (IL_0001), as bound; the evaluator's code generator keeps the
        ''' spilled reference on the stack (dup for ^1's length at IL_0008) instead of in a local. After the result is
        ''' allocated (IL_0016), that same reference is stored into the copy source V_4 (IL_0017). The null check (IL_0019),
        ''' the length check (IL_0022), the loop's element read (IL_002e) and the fallback Array.Copy call's source argument
        ''' (IL_0041) all read V_4; S._items is not read again.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_SpilledReceiver_SourceIsReadOnceForCopy()
            Dim original = SliceRewriterIL("_items(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.M")
            Dim rewrittenData As CompilationTestData.MethodData = Nothing
            Dim rewritten = SliceRewriterIL("_items(1..^1)", enabled:=True, methodData:=rewrittenData, sources:=s_sliceSources, methodName:="S.M")
            VerifyLoopRewrite(original, rewritten)
            Assert.Equal(1, CountOf(original, "S._items"))
            Assert.Equal(1, CountOf(rewritten, "S._items"))
            rewrittenData.VerifyIL("
{
  // Code size       80 (0x50)
  .maxstack  6
  .locals init (Integer V_0,
                Integer V_1,
                Integer() V_2,
                Integer V_3,
                Integer() V_4)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""S._items As Integer()""
  IL_0006:  ldc.i4.1
  IL_0007:  stloc.0
  IL_0008:  dup
  IL_0009:  ldlen
  IL_000a:  conv.i4
  IL_000b:  ldc.i4.1
  IL_000c:  sub.ovf
  IL_000d:  stloc.1
  IL_000e:  ldloc.1
  IL_000f:  ldloc.0
  IL_0010:  sub.ovf
  IL_0011:  newarr     ""Integer""
  IL_0016:  stloc.2
  IL_0017:  stloc.s    V_4
  IL_0019:  ldloc.s    V_4
  IL_001b:  brfalse.s  IL_0041
  IL_001d:  ldloc.0
  IL_001e:  ldc.i4.0
  IL_001f:  blt.s      IL_0041
  IL_0021:  ldloc.1
  IL_0022:  ldloc.s    V_4
  IL_0024:  ldlen
  IL_0025:  conv.i4
  IL_0026:  bgt.s      IL_0041
  IL_0028:  ldc.i4.0
  IL_0029:  stloc.3
  IL_002a:  br.s       IL_0039
  IL_002c:  ldloc.2
  IL_002d:  ldloc.3
  IL_002e:  ldloc.s    V_4
  IL_0030:  ldloc.0
  IL_0031:  ldloc.3
  IL_0032:  add
  IL_0033:  ldelem.i4
  IL_0034:  stelem.i4
  IL_0035:  ldloc.3
  IL_0036:  ldc.i4.1
  IL_0037:  add
  IL_0038:  stloc.3
  IL_0039:  ldloc.3
  IL_003a:  ldloc.1
  IL_003b:  ldloc.0
  IL_003c:  sub
  IL_003d:  blt.s      IL_002c
  IL_003f:  br.s       IL_004e
  IL_0041:  ldloc.s    V_4
  IL_0043:  ldloc.0
  IL_0044:  ldloc.2
  IL_0045:  ldc.i4.0
  IL_0046:  ldloc.1
  IL_0047:  ldloc.0
  IL_0048:  sub.ovf
  IL_0049:  call       ""Sub System.Array.Copy(System.Array, Integer, System.Array, Integer, Integer)""
  IL_004e:  ldloc.2
  IL_004f:  ret
}")
        End Sub

        ''' <summary>
        ''' Frames for String(), Object(), generic T(), spilled, ByRef, and lambda-captured slice sources.
        ''' </summary>
        Private Const SliceRewriterSource = "
Class S
    Private _items As Integer() = {1, 2, 3, 4}
    Shared Function F() As Integer()
        Return {1, 2, 3, 4}
    End Function
    Sub M(strings As String(), objects As Object(), ByRef byRefArray As Integer())
        System.Console.WriteLine(strings.Length)
    End Sub
    Shared Sub G(Of T)(items As T())
        System.Console.WriteLine(items.Length)
    End Sub
    Shared Sub L()
        Dim captured As Integer() = {1, 2, 3, 4}
        Dim getLength As System.Func(Of Integer) = Function() captured.Length
#ExternalSource(""test"", 999)
        System.Console.WriteLine(getLength())
#End ExternalSource
    End Sub
End Class
"

        Private Shared ReadOnly s_sliceSources As String() = {ArrayRangeSource, SliceRewriterSource}

        ''' <summary>
        ''' The checks that every rewritten whole-expression slice shares. With the option off, the slice copies
        ''' with Array.Copy only. With it on, there is one allocation, a copy loop, and the original Array.Copy call on
        ''' the fallback path.
        ''' </summary>
        Private Shared Sub VerifyLoopRewrite(original As String, rewritten As String)
            Assert.Equal(1, CountOf(original, "System.Array.Copy"))
            Assert.DoesNotContain("stelem", original)
            Assert.Equal(1, CountOf(rewritten, "System.Array.Copy"))
            Assert.Equal(1, CountOf(rewritten, "newarr"))
            Assert.Contains("stelem", rewritten)
            Assert.Contains(ILLines(rewritten), Function(line) line.Contains(" blt"))
        End Sub

        <Fact>
        Public Sub SliceRewriter_StringArray_CopiesWithLoop()
            Dim original = SliceRewriterIL("strings(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.M")
            Dim rewritten = SliceRewriterIL("strings(1..^1)", enabled:=True, sources:=s_sliceSources, methodName:="S.M")
            VerifyLoopRewrite(original, rewritten)
            Assert.Contains("newarr     ""String""", rewritten)
            Assert.Contains("ldelem.ref", rewritten)
            Assert.Contains("stelem.ref", rewritten)
        End Sub

        ''' <summary>
        ''' Elements of an Object() are copied as references. The loop's element access suppresses the
        ''' RuntimeHelpers.GetObjectValue call that VB otherwise emits for an Object value, so boxed values are not cloned,
        ''' as with Array.Copy.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_ObjectArray_CopiesWithLoop()
            Dim original = SliceRewriterIL("objects(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.M")
            Dim rewritten = SliceRewriterIL("objects(1..^1)", enabled:=True, sources:=s_sliceSources, methodName:="S.M")
            VerifyLoopRewrite(original, rewritten)
            Assert.Contains("newarr     ""Object""", rewritten)
            Assert.Contains("ldelem.ref", rewritten)
            Assert.Contains("stelem.ref", rewritten)
            Assert.DoesNotContain("GetObjectValue", original)
            Assert.DoesNotContain("GetObjectValue", rewritten)
        End Sub

        <Fact>
        Public Sub SliceRewriter_GenericArray_CopiesWithLoop()
            Dim original = SliceRewriterIL("items(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.G", methodDataName:="<>x.<>m0(Of T)")
            Dim rewritten = SliceRewriterIL("items(1..^1)", enabled:=True, sources:=s_sliceSources, methodName:="S.G", methodDataName:="<>x.<>m0(Of T)")
            VerifyLoopRewrite(original, rewritten)
            Dim rewrittenLines = ILLines(rewritten)
            Assert.Contains(rewrittenLines, Function(line) line.Contains("newarr") AndAlso line.EndsWith("""T""", StringComparison.Ordinal))
            Assert.Contains(rewrittenLines, Function(line) line.Contains("ldelem") AndAlso line.EndsWith("""T""", StringComparison.Ordinal))
            Assert.Contains(rewrittenLines, Function(line) line.Contains("stelem") AndAlso line.EndsWith("""T""", StringComparison.Ordinal))
        End Sub

        ''' <summary>
        ''' A receiver that is not a local or parameter is spilled into a temporary once, as bound; the loop reads
        ''' the temporary.
        ''' </summary>
        <Theory>
        <InlineData("_items(1..^1)", "S._items")>
        <InlineData("F()(1..^1)", "Function S.F()")>
        Public Sub SliceRewriter_SpilledReceiver_IsEvaluatedOnce(expr As String, receiver As String)
            Dim original = SliceRewriterIL(expr, enabled:=False, sources:=s_sliceSources, methodName:="S.M")
            Dim rewritten = SliceRewriterIL(expr, enabled:=True, sources:=s_sliceSources, methodName:="S.M")
            VerifyLoopRewrite(original, rewritten)
            Assert.Equal(1, CountOf(original, receiver))
            Assert.Equal(1, CountOf(rewritten, receiver))
        End Sub

        <Fact>
        Public Sub SliceRewriter_ByRefParameter_CopiesWithLoop()
            Dim original = SliceRewriterIL("byRefArray(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.M")
            Dim rewritten = SliceRewriterIL("byRefArray(1..^1)", enabled:=True, sources:=s_sliceSources, methodName:="S.M")
            VerifyLoopRewrite(original, rewritten)
            Assert.Contains("ldind.ref", rewritten)
            Assert.Contains("ldelem.i4", rewritten)
            Assert.Contains("stelem.i4", rewritten)
        End Sub

        ''' <summary>
        ''' A captured local binds as a local, so the top-level rewriter sees it; CapturedVariableRewriter then
        ''' replaces each read with the display class field.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_LambdaCapturedLocal_CopiesWithLoop()
            Dim original = SliceRewriterIL("captured(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.L", atLineNumber:=999)
            Dim rewritten = SliceRewriterIL("captured(1..^1)", enabled:=True, sources:=s_sliceSources, methodName:="S.L", atLineNumber:=999)
            VerifyLoopRewrite(original, rewritten)
            Assert.Contains("$VB$Local_captured", original)
            Assert.Contains("$VB$Local_captured", rewritten)
            Assert.Contains("ldelem.i4", rewritten)
        End Sub

        ''' <summary>
        ''' In a module that is not vbnext, the option changes nothing: the IL is the same with it on, and a
        ''' slice is still a syntax error.
        ''' </summary>
        <Theory>
        <InlineData("a(1)")>
        <InlineData("a.Length")>
        <InlineData("s.X + n")>
        Public Sub SliceRewriter_Vb17_13Module_IsUnchanged(expr As String)
            Dim vb17_13 = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
            Dim offIL = SliceRewriterIL(expr, enabled:=False, sources:={Source}, methodName:="C.M", parseOptions:=vb17_13)
            Dim onIL = SliceRewriterIL(expr, enabled:=True, sources:={Source}, methodName:="C.M", parseOptions:=vb17_13)
            Assert.Equal(offIL, onIL)
            Assert.DoesNotContain("System.Array.Copy", onIL)

            Dim offErrors = CompileSliceExpression("a(1..^1)", enabled:=False, testData:=New CompilationTestData(), sources:={Source}, methodName:="C.M", parseOptions:=vb17_13)
            Dim onErrors = CompileSliceExpression("a(1..^1)", enabled:=True, testData:=New CompilationTestData(), sources:={Source}, methodName:="C.M", parseOptions:=vb17_13)
            Assert.NotEqual("", offErrors)
            Assert.Equal(offErrors, onErrors)
        End Sub

        ''' <summary>
        ''' A vbnext module with a Windows PDB. A Windows PDB records no language version, so the evaluator parses
        ''' with the current language version (as in WindowsPdb_KeepsCurrentSyntax): the IL is the same with the option on,
        ''' and a slice is the same syntax error, so the loop rewrite is never reached.
        ''' </summary>
        <Theory>
        <InlineData("a(1)")>
        <InlineData("a.Length")>
        <InlineData("s.X + n")>
        Public Sub SliceRewriter_WindowsPdb_IsUnchanged(expr As String)
            Dim vbNext = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
            Dim offIL = SliceRewriterIL(expr, enabled:=False, sources:={Source}, methodName:="C.M", parseOptions:=vbNext, debugFormat:=DebugInformationFormat.Pdb)
            Dim onIL = SliceRewriterIL(expr, enabled:=True, sources:={Source}, methodName:="C.M", parseOptions:=vbNext, debugFormat:=DebugInformationFormat.Pdb)
            Assert.Equal(offIL, onIL)
            Assert.DoesNotContain("System.Array.Copy", onIL)

            For Each slice In {"a(1..^1)", "a(1..2)"}
                Dim offErrors = CompileSliceExpression(slice, enabled:=False, testData:=New CompilationTestData(), sources:={Source}, methodName:="C.M", parseOptions:=vbNext, debugFormat:=DebugInformationFormat.Pdb)
                Dim onErrors = CompileSliceExpression(slice, enabled:=True, testData:=New CompilationTestData(), sources:={Source}, methodName:="C.M", parseOptions:=vbNext, debugFormat:=DebugInformationFormat.Pdb)
                _output.WriteLine($"Windows PDB, {slice}: off: {offErrors} | on: {onErrors}")
                Assert.NotEqual("", offErrors)
                Assert.Equal(offErrors, onErrors)
            Next
        End Sub
        ''' <summary>
        ''' Frames whose slice sources are all parameters, so a reflection call supplies every value that the
        ''' generated method reads. The frames have no locals.
        ''' </summary>
        Private Const RuntimeSliceSource = "
Public Class P
    Public Shared Sub M(a As Integer(), r As System.Range, strings As String(), objects As Object())
        System.Console.WriteLine(a)
    End Sub
    Public Sub N(ByRef byRefArray As Integer(), r As System.Range)
        System.Console.WriteLine(byRefArray)
    End Sub
    Public Shared Sub G(Of T)(items As T(), r As System.Range)
        System.Console.WriteLine(items)
    End Sub
End Class
"

        Private NotInheritable Class RuntimeFrames
            Friend Runtime As RuntimeInstance
            Friend SourceAssembly As System.Reflection.Assembly
            Friend AssemblyName As String
        End Class

        ''' <summary>
        ''' Compiles ArrayRangeSource and RuntimeSliceSource once, for the evaluator (through a portable PDB, so
        ''' vbnext) and for running (the same compilation, emitted and loaded from bytes).
        ''' </summary>
        Private Function CreateRuntimeFrames() As RuntimeFrames
            Dim comp = CreateCompilationWithMscorlib40({ArrayRangeSource, RuntimeSliceSource}, options:=TestOptions.DebugDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))
            comp.VerifyDiagnostics()
            Return New RuntimeFrames With {
                .Runtime = CreateRuntimeInstance(comp, debugFormat:=DebugInformationFormat.PortablePdb),
                .SourceAssembly = System.Reflection.Assembly.Load(comp.EmitToArray().ToArray()),
                .AssemblyName = comp.AssemblyName}
        End Function

        ''' <summary>
        ''' Compiles an expression in a frame with the loop rewrite off or on, loads the evaluator's assembly, and
        ''' returns its generated method.
        ''' </summary>
        Private Shared Function CompileRuntimeSlice(frames As RuntimeFrames, methodName As String, expr As String, enabled As Boolean) As System.Reflection.MethodInfo
            Dim context = CreateMethodContext(frames.Runtime, methodName)
            Dim diagnostics = DiagnosticBag.GetInstance()
            Dim resultProperties As ResultProperties = Nothing
            Dim result = context.CompileExpressionCore(
                expr,
                Microsoft.VisualStudio.Debugger.Evaluation.DkmEvaluationFlags.TreatAsExpression,
                System.Collections.Immutable.ImmutableArray(Of [Alias]).Empty,
                diagnostics,
                resultProperties,
                New CompilationTestData(),
                useArraySliceLoop:=enabled)
            Dim errors = String.Join(vbCrLf, diagnostics.AsEnumerable().Where(Function(d) d.Severity = DiagnosticSeverity.Error).Select(Function(d) d.ToString()))
            diagnostics.Free()
            Assert.Equal("", errors)
            Dim evaluatorAssembly = System.Reflection.Assembly.Load(result.Assembly)
            Dim method = evaluatorAssembly.GetType(result.TypeName, throwOnError:=True).GetMethod(result.MethodName, System.Reflection.BindingFlags.Static Or System.Reflection.BindingFlags.Public Or System.Reflection.BindingFlags.NonPublic)
            Assert.NotNull(method)
            Return method
        End Function

        Private Shared Function SignatureOf(method As System.Reflection.MethodInfo) As String
            Dim typeParameters = If(method.IsGenericMethodDefinition, "(Of " & String.Join(", ", method.GetGenericArguments().Select(Function(type) type.Name)) & ")", "")
            Dim parameters = String.Join(", ", method.GetParameters().Select(Function(parameter) $"{parameter.Name} As {parameter.ParameterType}"))
            Return $"{If(method.IsStatic, "Shared ", "")}{method.DeclaringType}.{method.Name}{typeParameters}({parameters}) As {method.ReturnType}"
        End Function

        Private Shared Function MakeRange(frames As RuntimeFrames, start As Integer, startFromEnd As Boolean, [end] As Integer, endFromEnd As Boolean) As Object
            Dim indexType = frames.SourceAssembly.GetType("System.Index", throwOnError:=True)
            Dim rangeType = frames.SourceAssembly.GetType("System.Range", throwOnError:=True)
            Dim startIndex = System.Activator.CreateInstance(indexType, New Object() {start, startFromEnd})
            Dim endIndex = System.Activator.CreateInstance(indexType, New Object() {[end], endFromEnd})
            Return System.Activator.CreateInstance(rangeType, New Object() {startIndex, endIndex})
        End Function

        ''' <summary>
        ''' Invokes a generated method and describes what it observably did: the result's runtime type, length,
        ''' elements, whether each element is the same reference as the source element it came from, and whether the
        ''' result is the source array itself; or the type of the exception it threw, unwrapped from
        ''' TargetInvocationException.
        ''' </summary>
        Private Shared Function DescribeInvocation(method As System.Reflection.MethodInfo, args As Object(), sourceIndex As Integer, offset As Integer) As String
            Dim source = TryCast(args(sourceIndex), System.Array)
            Dim result As Object
            Try
                result = method.Invoke(Nothing, args)
            Catch ex As System.Reflection.TargetInvocationException
                Dim inner = ex.InnerException
                Dim argumentException = TryCast(inner, System.ArgumentException)
                Return $"throws {inner.GetType().FullName}{If(argumentException?.ParamName Is Nothing, "", $" (ParamName={argumentException.ParamName})")}"
            Catch ex As Exception
                Return $"INVOKE-FAILED {ex.GetType().FullName}: {ex.Message}"
            End Try
            If result Is Nothing Then
                Return "returns Nothing"
            End If
            Dim sliced = DirectCast(result, System.Array)
            Dim values = String.Join(",", sliced.Cast(Of Object)().Select(Function(value) If(value Is Nothing, "Nothing", value.ToString())))
            Dim identity As String
            If sliced.GetType().GetElementType().IsValueType OrElse source Is Nothing Then
                identity = "n/a"
            Else
                identity = String.Join(",", System.Linq.Enumerable.Range(0, sliced.Length).Select(Function(i) If(Object.ReferenceEquals(sliced.GetValue(i), source.GetValue(offset + i)), "same", "different")))
            End If
            Return $"type={sliced.GetType().FullName} length={sliced.Length} values=[{values}] elementIdentity=[{identity}] resultIsSource={Object.ReferenceEquals(sliced, source)}"
        End Function

        ''' <summary>
        ''' The generated methods are shared, take Me first for an instance frame, then each frame parameter
        ''' (ByRef kept ByRef), and receive the arrays that are passed.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_Runtime_GeneratedSignaturesReceiveInputs()
            Dim frames = CreateRuntimeFrames()
            Dim handler As System.ResolveEventHandler = Function(sender, e) If(New System.Reflection.AssemblyName(e.Name).Name = frames.AssemblyName, frames.SourceAssembly, Nothing)
            AddHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            Try
                Dim pType = frames.SourceAssembly.GetType("P", throwOnError:=True)
                Dim rangeType = frames.SourceAssembly.GetType("System.Range", throwOnError:=True)
                Dim ints = New Integer() {10, 20, 30, 40, 50}
                Dim strings = New String() {"s0", "s1", "s2", "s3", "s4"}
                Dim objects = New Object() {1, 2, 3, 4, 5}
                Dim range = MakeRange(frames, 1, False, 1, True)

                For Each enabled In {False, True}
                    Dim m = CompileRuntimeSlice(frames, "P.M", "a", enabled)
                    _output.WriteLine($"P.M (rewriter {If(enabled, "on", "off")}): {SignatureOf(m)}")
                    Assert.True(m.IsStatic)
                    Dim mParameters = m.GetParameters()
                    Assert.Equal({"a", "r", "strings", "objects"}, mParameters.Select(Function(p) p.Name).ToArray())
                    Assert.Equal({GetType(Integer()), rangeType, GetType(String()), GetType(Object())}, mParameters.Select(Function(p) p.ParameterType).ToArray())
                    Assert.Same(ints, m.Invoke(Nothing, {ints, range, strings, objects}))
                    Assert.Same(strings, CompileRuntimeSlice(frames, "P.M", "strings", enabled).Invoke(Nothing, {ints, range, strings, objects}))
                    Assert.Same(objects, CompileRuntimeSlice(frames, "P.M", "objects", enabled).Invoke(Nothing, {ints, range, strings, objects}))

                    Dim n = CompileRuntimeSlice(frames, "P.N", "byRefArray", enabled)
                    _output.WriteLine($"P.N (rewriter {If(enabled, "on", "off")}): {SignatureOf(n)}")
                    Assert.True(n.IsStatic)
                    Dim nParameters = n.GetParameters()
                    Assert.Equal(3, nParameters.Length)
                    Assert.Same(pType, nParameters(0).ParameterType)
                    Assert.True(nParameters(1).ParameterType.IsByRef)
                    Assert.Same(GetType(Integer()), nParameters(1).ParameterType.GetElementType())
                    Assert.Same(rangeType, nParameters(2).ParameterType)
                    Dim instance = System.Activator.CreateInstance(pType)
                    Assert.Same(ints, n.Invoke(Nothing, {instance, ints, range}))

                    Dim g = CompileRuntimeSlice(frames, "P.G", "items", enabled)
                    _output.WriteLine($"P.G (rewriter {If(enabled, "on", "off")}): {SignatureOf(g)}")
                    Assert.True(g.IsStatic)
                    Assert.True(g.IsGenericMethodDefinition)
                    Assert.Equal(1, g.GetGenericArguments().Length)
                    Assert.Equal(2, g.GetParameters().Length)
                    Assert.Same(strings, g.MakeGenericMethod(GetType(String)).Invoke(Nothing, {strings, range}))
                Next
            Finally
                RemoveHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            End Try
        End Sub

        ''' <summary>
        ''' Runs each slice with the loop rewrite off and on, with the same fresh inputs, and compares what each
        ''' observably did. All rows are written to the test output before any comparison is asserted.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_Runtime_OffAndOnAgree()
            Dim frames = CreateRuntimeFrames()
            Dim handler As System.ResolveEventHandler = Function(sender, e) If(New System.Reflection.AssemblyName(e.Name).Name = frames.AssemblyName, frames.SourceAssembly, Nothing)
            AddHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            Try
                Dim pType = frames.SourceAssembly.GetType("P", throwOnError:=True)
                Dim ints = Function() New Integer() {10, 20, 30, 40, 50}
                Dim strings = Function() New String() {"s0", "s1", "s2", "s3", "s4"}
                Dim boxed = Function() New Object() {1, 2.5, 3L, #1/2/2026#, True}
                Dim covariant = Function() DirectCast(New String() {"c0", "c1", "c2", "c3", "c4"}, Object())
                Dim r = Function(start As Integer, startFromEnd As Boolean, [end] As Integer, endFromEnd As Boolean) MakeRange(frames, start, startFromEnd, [end], endFromEnd)
                Dim oneToHat1 = Function() r(1, False, 1, True)

                ' name, frame, expression, fresh arguments, index of the source argument, the source offset of element 0, generic type argument
                Dim cases = New List(Of (Name As String, Frame As String, Expr As String, Args As Func(Of Object()), SourceIndex As Integer, Offset As Integer, TypeArgument As Type)) From {
                    ("Integer() a(1..^1)", "P.M", "a(1..^1)", Function() {ints(), oneToHat1(), Nothing, Nothing}, 0, 1, Nothing),
                    ("Integer() a(0..0)", "P.M", "a(0..0)", Function() {ints(), oneToHat1(), Nothing, Nothing}, 0, 0, Nothing),
                    ("Integer() a(5..5)", "P.M", "a(5..5)", Function() {ints(), oneToHat1(), Nothing, Nothing}, 0, 5, Nothing),
                    ("Integer() a(r), r = 1..^1", "P.M", "a(r)", Function() {ints(), oneToHat1(), Nothing, Nothing}, 0, 1, Nothing),
                    ("Integer() a(r), r = 0..0", "P.M", "a(r)", Function() {ints(), r(0, False, 0, False), Nothing, Nothing}, 0, 0, Nothing),
                    ("Integer() a(r), r = 5..5", "P.M", "a(r)", Function() {ints(), r(5, False, 5, False), Nothing, Nothing}, 0, 5, Nothing),
                    ("String() strings(r), r = 1..^1", "P.M", "strings(r)", Function() {Nothing, oneToHat1(), strings(), Nothing}, 2, 1, Nothing),
                    ("Object() of boxed values, objects(r), r = 1..^1", "P.M", "objects(r)", Function() {Nothing, oneToHat1(), Nothing, boxed()}, 3, 1, Nothing),
                    ("Object() of boxed values, objects(0..5)", "P.M", "objects(0..5)", Function() {Nothing, oneToHat1(), Nothing, boxed()}, 3, 0, Nothing),
                    ("Object() holding a String(), objects(r), r = 1..^1", "P.M", "objects(r)", Function() {Nothing, oneToHat1(), Nothing, covariant()}, 3, 1, Nothing),
                    ("T() with T = String, items(r), r = 1..^1", "P.G", "items(r)", Function() {strings(), oneToHat1()}, 0, 1, GetType(String)),
                    ("T() with T = Integer, items(r), r = 1..^1", "P.G", "items(r)", Function() {ints(), oneToHat1()}, 0, 1, GetType(Integer)),
                    ("T() with T = Object, boxed values, items(r), r = 1..^1", "P.G", "items(r)", Function() {boxed(), oneToHat1()}, 0, 1, GetType(Object)),
                    ("ByRef Integer() byRefArray(r), r = 1..^1", "P.N", "byRefArray(r)", Function() {System.Activator.CreateInstance(pType), ints(), oneToHat1()}, 1, 1, Nothing),
                    ("Nothing array, a(r), r = 1..^1", "P.M", "a(r)", Function() {Nothing, oneToHat1(), Nothing, Nothing}, 0, 1, Nothing),
                    ("Nothing array, a(0..2)", "P.M", "a(0..2)", Function() {Nothing, oneToHat1(), Nothing, Nothing}, 0, 0, Nothing),
                    ("Nothing array, a(1..^1)", "P.M", "a(1..^1)", Function() {Nothing, oneToHat1(), Nothing, Nothing}, 0, 1, Nothing),
                    ("Nothing String(), strings(0..2)", "P.M", "strings(0..2)", Function() {Nothing, oneToHat1(), Nothing, Nothing}, 2, 0, Nothing),
                    ("Negative start, a(r), r = ^10..^0", "P.M", "a(r)", Function() {ints(), r(10, True, 0, True), Nothing, Nothing}, 0, 0, Nothing),
                    ("End beyond length, a(r), r = 0..10", "P.M", "a(r)", Function() {ints(), r(0, False, 10, False), Nothing, Nothing}, 0, 0, Nothing),
                    ("End beyond length, a(3..10)", "P.M", "a(3..10)", Function() {ints(), oneToHat1(), Nothing, Nothing}, 0, 3, Nothing),
                    ("End beyond length, String() strings(r), r = 2..9", "P.M", "strings(r)", Function() {Nothing, r(2, False, 9, False), strings(), Nothing}, 2, 2, Nothing),
                    ("Reversed range, a(r), r = 3..1", "P.M", "a(r)", Function() {ints(), r(3, False, 1, False), Nothing, Nothing}, 0, 3, Nothing),
                    ("Reversed range, a(r), r = ^1..1", "P.M", "a(r)", Function() {ints(), r(1, True, 1, False), Nothing, Nothing}, 0, 4, Nothing)}

                Dim rows = New List(Of (Name As String, Off As String, [On] As String))()
                For Each c In cases
                    Dim results = New String(1) {}
                    For Each enabled In {False, True}
                        Dim method = CompileRuntimeSlice(frames, c.Frame, c.Expr, enabled)
                        If c.TypeArgument IsNot Nothing Then
                            method = method.MakeGenericMethod(c.TypeArgument)
                        End If
                        results(If(enabled, 1, 0)) = DescribeInvocation(method, c.Args(), c.SourceIndex, c.Offset)
                    Next
                    rows.Add((c.Name, results(0), results(1)))
                Next

                For Each row In rows
                    _output.WriteLine($"{row.Name}{vbCrLf}  off: {row.Off}{vbCrLf}  on:  {row.On}{vbCrLf}  {If(row.Off = row.On, "same", "DIFFERENT")}")
                Next
                Assert.DoesNotContain(rows, Function(row) row.Off.StartsWith("INVOKE-FAILED", StringComparison.Ordinal) OrElse row.On.StartsWith("INVOKE-FAILED", StringComparison.Ordinal))
                For Each row In rows
                    Assert.Equal(row.Name & ": " & row.Off, row.Name & ": " & row.On)
                Next
            Finally
                RemoveHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            End Try
        End Sub

        ''' <summary>
        ''' The experimental opt-in. Only VBNEXT_EE_ARRAYSLICE_LOOP=1, exactly, enables the loop.
        ''' </summary>
        <Theory>
        <InlineData(Nothing, False)>
        <InlineData("0", False)>
        <InlineData("1", True)>
        <InlineData("true", False)>
        <InlineData("True", False)>
        <InlineData(" 1", False)>
        <InlineData("1 ", False)>
        <InlineData("01", False)>
        <InlineData("", False)>
        <InlineData("2", False)>
        <InlineData("yes", False)>
        Public Sub ArraySliceLoopOption_OnlyExactOneEnables(value As String, expected As Boolean)
            Assert.Equal(expected, EvaluationContext.IsArraySliceLoopRequested(value))
        End Sub

        ''' <summary>
        ''' An evaluation context reads VBNEXT_EE_ARRAYSLICE_LOOP when it is created. These tests set the test
        ''' process's environment, so they run alone (VbNextArraySliceEnvironmentCollection) and restore the previous value.
        ''' </summary>
        <Xunit.CollectionAttribute(VbNextArraySliceEnvironmentCollection.Name)>
        Public Class ArraySliceLoopEnvironmentTests
            Inherits ExpressionCompilerTestBase

            Private Shared Function CompileWithOption(context As EvaluationContext, expr As String, useArraySliceLoop As Boolean?) As String
                Dim testData = New CompilationTestData()
                Dim diagnostics = DiagnosticBag.GetInstance()
                Dim resultProperties As ResultProperties = Nothing
                Dim flags = Microsoft.VisualStudio.Debugger.Evaluation.DkmEvaluationFlags.TreatAsExpression
                If useArraySliceLoop.HasValue Then
                    context.CompileExpressionCore(expr, flags, NoAliases, diagnostics, resultProperties, testData, useArraySliceLoop:=useArraySliceLoop.Value)
                Else
                    context.CompileExpression(expr, flags, NoAliases, diagnostics, resultProperties, testData)
                End If
                Dim errors = String.Join(vbCrLf, diagnostics.AsEnumerable().Where(Function(d) d.Severity = DiagnosticSeverity.Error).Select(Function(d) d.ToString()))
                diagnostics.Free()
                Assert.Equal("", errors)
                Return testData.GetMethodData("<>x.<>m0").GetMethodIL()
            End Function

            Private Function CreateRuntime(source As String, parseOptions As VisualBasicParseOptions, debugFormat As DebugInformationFormat) As RuntimeInstance
                Dim comp = CreateCompilationWithMscorlib40({source}, options:=TestOptions.DebugDll, parseOptions:=parseOptions)
                comp.VerifyDiagnostics()
                Return CreateRuntimeInstance(comp, debugFormat:=debugFormat)
            End Function

            <Fact>
            Public Sub EnvironmentVariable_EnablesLoopOnlyForExactOne()
                Dim previous = Environment.GetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable)
                Try
                    Dim runtime = CreateRuntime(ArrayRangeSource, TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext), DebugInformationFormat.PortablePdb)
                    For Each expr In {"a(1..^1)", "a(r)"}
                        Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, Nothing)
                        Dim baseline = CreateMethodContext(runtime, "D.M")
                        Assert.False(baseline.IsArraySliceLoopEnabled)
                        Dim offIL = CompileWithOption(baseline, expr, False)
                        Dim onIL = CompileWithOption(baseline, expr, True)
                        Assert.NotEqual(offIL, onIL)
                        Assert.Equal(offIL, CompileWithOption(baseline, expr, Nothing))

                        For Each value In {Nothing, "0", "1", "true", "True", " 1", "1 ", "01", "2"}
                            Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, value)
                            Dim enabled = String.Equals(value, "1", StringComparison.Ordinal)
                            Dim context = CreateMethodContext(runtime, "D.M")
                            Assert.Equal(enabled, context.IsArraySliceLoopEnabled)
                            Assert.Equal(If(enabled, onIL, offIL), CompileWithOption(context, expr, Nothing))
                            ' The test override selects either form, whatever the environment.
                            Assert.Equal(offIL, CompileWithOption(context, expr, False))
                            Assert.Equal(onIL, CompileWithOption(context, expr, True))
                        Next

                        ' A context keeps the value that it read when it was created.
                        Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, "1")
                        Dim created = CreateMethodContext(runtime, "D.M")
                        Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, Nothing)
                        Assert.True(created.IsArraySliceLoopEnabled)
                        Assert.Equal(onIL, CompileWithOption(created, expr, Nothing))
                        Assert.False(CreateMethodContext(runtime, "D.M").IsArraySliceLoopEnabled)
                    Next
                Finally
                    Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, previous)
                End Try
                Assert.Equal(previous, Environment.GetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable))
            End Sub

            ''' <summary>
            ''' With VBNEXT_EE_ARRAYSLICE_LOOP=1, a VB 17.13 module, and a vbnext module with a Windows PDB (which records no
            ''' language version), are evaluated exactly as without it.
            ''' </summary>
            <Fact>
            Public Sub EnvironmentVariable_LeavesLegacyModulesUnchanged()
                Dim previous = Environment.GetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable)
                Try
                    Dim runtimes = {
                        CreateRuntime(Source, TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13), DebugInformationFormat.PortablePdb),
                        CreateRuntime(Source, TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext), DebugInformationFormat.Pdb)}
                    For Each runtime As RuntimeInstance In runtimes
                        For Each expr In {"a(1)", "a.Length", "s.X + n"}
                            Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, Nothing)
                            Dim offIL = CompileWithOption(CreateMethodContext(runtime, "C.M"), expr, Nothing)
                            Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, "1")
                            Dim context = CreateMethodContext(runtime, "C.M")
                            Assert.False(context.IsArraySliceLoopEnabled)
                            Assert.Equal(offIL, CompileWithOption(context, expr, Nothing))
                            Assert.DoesNotContain("System.Array.Copy", offIL)
                        Next
                    Next
                Finally
                    Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, previous)
                End Try
                Assert.Equal(previous, Environment.GetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable))
            End Sub
        End Class
    End Class

    ''' <summary>
    ''' Tests that set VBNEXT_EE_ARRAYSLICE_LOOP in the test process run in this collection, with no other test.
    ''' </summary>
    <Xunit.CollectionDefinitionAttribute(VbNextArraySliceEnvironmentCollection.Name, DisableParallelization:=True)>
    Public NotInheritable Class VbNextArraySliceEnvironmentCollection
        Public Const Name As String = "VbNext EE array-slice environment"
    End Class
End Namespace
