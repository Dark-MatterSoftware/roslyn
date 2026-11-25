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
            ' Documents the legacy Array.Copy lowering (rewriter forced off). Default vbnext CompileExpression uses the loop.
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression(expr, enabled:=False, testData:=testData))
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
        ''' A whole-expression slice keeps BindArrayRange side effects through allocation, then calls
        ''' &lt;&gt;ArraySliceCopy instead of Array.Copy. The helper holds the GetValue/SetValue loop and the Array.Copy fallback.
        ''' </summary>
        <Theory>
        <InlineData("a(1..^1)")>
        <InlineData("a(r)")>
        Public Sub SliceRewriter_WholeExpressionSlice_CopiesWithLoop(expr As String)
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression(expr, enabled:=True, testData:=testData))
            Dim rewritten = testData.GetMethodData("<>x.<>m0").GetMethodIL()
            Dim original = SliceRewriterIL(expr, enabled:=False)
            VerifyLoopRewrite(original, rewritten)
            AssertHelperCopiesElements(testData)
            ' <>m0 shape matches legacy BindArrayRange except Array.Copy becomes <>ArraySliceCopy (same argument count shape: source, start, dest, length).
            Assert.Equal(CountOf(original, "newarr"), CountOf(rewritten, "newarr"))
            Assert.Contains("call       ""Sub <>x.<>ArraySliceCopy(System.Array, Integer, System.Array, Integer)""", rewritten)
        End Sub

        ''' <summary>
        ''' A slice inside a larger expression is rewritten in place (helper call stays in the slice BoundSequence).
        ''' </summary>
        <Theory>
        <InlineData("a(1..2)(0)")>
        <InlineData("a(1..^1)(0)")>
        <InlineData("a(1..^1).Length")>
        Public Sub SliceRewriter_NestedSlice_CopiesWithLoop(expr As String)
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression(expr, enabled:=True, testData:=testData))
            Dim rewritten = testData.GetMethodData("<>x.<>m0").GetMethodIL()
            Dim original = SliceRewriterIL(expr, enabled:=False)
            Assert.NotEqual(original, rewritten)
            VerifyLoopRewrite(original, rewritten)
            AssertHelperCopiesElements(testData)
        End Sub

        <Fact>
        Public Sub SliceRewriter_SliceAsMethodArgument_CopiesWithLoop()
            Dim sources = {
                ArrayRangeSource.Replace(
                    "Shared Sub M()",
                    "Shared Function Id(values As Integer()) As Integer()" & vbCrLf &
                    "        Return values" & vbCrLf &
                    "    End Function" & vbCrLf &
                    "    Shared Sub M()")}
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression("Id(a(1..^1))", enabled:=True, testData:=testData, sources:=sources, methodName:="D.M"))
            Dim rewritten = testData.GetMethodData("<>x.<>m0").GetMethodIL()
            Dim original = SliceRewriterIL("Id(a(1..^1))", enabled:=False, sources:=sources, methodName:="D.M")
            Assert.NotEqual(original, rewritten)
            VerifyLoopRewrite(original, rewritten)
            AssertHelperCopiesElements(testData)
        End Sub

        ''' <summary>
        ''' Receiver / parameter source reads in &lt;&gt;m0 match the legacy Array.Copy path: BindArrayRange side effects
        ''' are unchanged, and the trailing helper call reads the source once as its first argument (as Array.Copy did).
        ''' </summary>
        <Theory>
        <InlineData("byRefArray(1..^1)", "S.M", -1, "ldarg.3")>
        <InlineData("captured(1..^1)", "S.L", 999, "$VB$Local_captured")>
        <InlineData("strings(1..^1)", "S.M", -1, "ldarg.1")>
        Public Sub SliceRewriter_SourceIsReadOnceForCopy(expr As String, frame As String, atLineNumber As Integer, sourceLoad As String)
            Dim original = SliceRewriterIL(expr, enabled:=False, sources:=s_sliceSources, methodName:=frame, atLineNumber:=atLineNumber)
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression(expr, enabled:=True, testData:=testData, sources:=s_sliceSources, methodName:=frame, atLineNumber:=atLineNumber))
            Dim rewritten = testData.GetMethodData("<>x.<>m0").GetMethodIL()
            VerifyLoopRewrite(original, rewritten)
            Assert.Equal(CountOf(original, sourceLoad), CountOf(rewritten, sourceLoad))
            AssertHelperCopiesElements(testData)
        End Sub

        ''' <summary>
        ''' A spilled receiver is evaluated once as bound; &lt;&gt;m0 passes that reference to &lt;&gt;ArraySliceCopy.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_SpilledReceiver_SourceIsReadOnceForCopy()
            Dim original = SliceRewriterIL("_items(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.M")
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression("_items(1..^1)", enabled:=True, testData:=testData, sources:=s_sliceSources, methodName:="S.M"))
            Dim rewritten = testData.GetMethodData("<>x.<>m0").GetMethodIL()
            VerifyLoopRewrite(original, rewritten)
            Assert.Equal(1, CountOf(original, "S._items"))
            Assert.Equal(1, CountOf(rewritten, "S._items"))
            AssertHelperCopiesElements(testData)
            Assert.Contains("call       ""Sub <>x.<>ArraySliceCopy(System.Array, Integer, System.Array, Integer)""", rewritten)
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
        ''' Shared checks for the in-place helper rewrite: legacy path uses Array.Copy only; rewritten &lt;&gt;m0
        ''' allocates once and calls &lt;&gt;ArraySliceCopy (no inline stelem / Array.Copy in &lt;&gt;m0).
        ''' </summary>
        Private Shared Sub VerifyLoopRewrite(original As String, rewritten As String)
            Assert.Equal(1, CountOf(original, "System.Array.Copy"))
            Assert.DoesNotContain("<>ArraySliceCopy", original)
            Assert.DoesNotContain("stelem", original)
            Assert.Equal(1, CountOf(rewritten, "<>ArraySliceCopy"))
            Assert.Equal(1, CountOf(rewritten, "newarr"))
            Assert.DoesNotContain("System.Array.Copy", rewritten)
            Assert.DoesNotContain("stelem", rewritten)
        End Sub

        Private Shared Sub AssertHelperCopiesElements(testData As CompilationTestData)
            Dim helperIL = testData.GetMethodData("<>x.<>ArraySliceCopy").GetMethodIL()
            Assert.Contains("GetValue", helperIL)
            Assert.Contains("SetValue", helperIL)
            Assert.Contains("System.Array.Copy", helperIL)
            Assert.Contains(ILLines(helperIL), Function(line) line.Contains(" blt"))
        End Sub

        <Fact>
        Public Sub SliceRewriter_StringArray_CopiesWithLoop()
            Dim original = SliceRewriterIL("strings(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.M")
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression("strings(1..^1)", enabled:=True, testData:=testData, sources:=s_sliceSources, methodName:="S.M"))
            Dim rewritten = testData.GetMethodData("<>x.<>m0").GetMethodIL()
            VerifyLoopRewrite(original, rewritten)
            Assert.Contains("newarr     ""String""", rewritten)
            AssertHelperCopiesElements(testData)
        End Sub

        ''' <summary>
        ''' Object() slices allocate Object() and call the helper; element identity is covered by runtime off≡on tests.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_ObjectArray_CopiesWithLoop()
            Dim original = SliceRewriterIL("objects(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.M")
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression("objects(1..^1)", enabled:=True, testData:=testData, sources:=s_sliceSources, methodName:="S.M"))
            Dim rewritten = testData.GetMethodData("<>x.<>m0").GetMethodIL()
            VerifyLoopRewrite(original, rewritten)
            Assert.Contains("newarr     ""Object""", rewritten)
            Assert.DoesNotContain("GetObjectValue", original)
            Assert.DoesNotContain("GetObjectValue", rewritten)
            AssertHelperCopiesElements(testData)
        End Sub

        <Fact>
        Public Sub SliceRewriter_GenericArray_CopiesWithLoop()
            Dim original = SliceRewriterIL("items(1..^1)", enabled:=False, sources:=s_sliceSources, methodName:="S.G", methodDataName:="<>x.<>m0(Of T)")
            Dim testData = New CompilationTestData()
            Assert.Equal("", CompileSliceExpression("items(1..^1)", enabled:=True, testData:=testData, sources:=s_sliceSources, methodName:="S.G"))
            Dim rewritten = testData.GetMethodData("<>x.<>m0(Of T)").GetMethodIL()
            VerifyLoopRewrite(original, rewritten)
            Assert.Contains(ILLines(rewritten), Function(line) line.Contains("newarr") AndAlso line.EndsWith("""T""", StringComparison.Ordinal))
            AssertHelperCopiesElements(testData)
        End Sub

        ''' <summary>
        ''' A receiver that is not a local or parameter is spilled into a temporary once, as bound; the helper receives that reference.
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

        Private Const SemanticSliceSource = "
Public Class Sem
    Public Shared Log As System.Text.StringBuilder
    Public Shared Function Mark(tag As String, value As Object) As Object
        Log.Append(tag & "";"")
        Return value
    End Function
    Public Shared Function GetItems() As Integer()
        Log.Append(""GetItems;"")
        Return New Integer() {10, 20, 30, 40, 50}
    End Function
    Public Shared Function GetStart() As Integer
        Log.Append(""GetStart;"")
        Return 1
    End Function
    Public Shared Function GetEnd() As Integer
        Log.Append(""GetEnd;"")
        Return 4
    End Function
    Public Shared Function GetFirstValue() As Integer
        Log.Append(""GetFirstValue;"")
        Return 7
    End Function
    Public Shared Function Process(first As Integer, values As Integer()) As String
        Log.Append(""Process;"")
        Return first.ToString() & "":"" & String.Join("","", values)
    End Function
    Public Shared Function Cond(value As Boolean) As Boolean
        Log.Append(""Cond;"" & value.ToString() & "";"")
        Return value
    End Function
    Public Shared Function Id(values As Integer()) As Integer()
        Return values
    End Function
    Public Shared Function MakeSpots() As Spot()
        Return New Spot() {
            New Spot With {.X = 1, .Y = 2},
            New Spot With {.X = 3, .Y = 4},
            New Spot With {.X = 5, .Y = 6},
            New Spot With {.X = 7, .Y = 8}}
    End Function
    Public Shared Sub M(items As String(), nums As Integer(), refs As Object(), structs As Spot())
        System.Console.WriteLine(items)
    End Sub
End Class
Public Structure Spot
    Public X As Integer
    Public Y As Integer
    Public Overrides Function ToString() As String
        Return X.ToString() & ""/"" & Y.ToString()
    End Function
End Structure
"

        Private Function CreateSemanticFrames() As RuntimeFrames
            Dim comp = CreateCompilationWithMscorlib40({ArrayRangeSource, SemanticSliceSource}, options:=TestOptions.DebugDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))
            comp.VerifyDiagnostics()
            Return New RuntimeFrames With {
                .Runtime = CreateRuntimeInstance(comp, debugFormat:=DebugInformationFormat.PortablePdb),
                .SourceAssembly = System.Reflection.Assembly.Load(comp.EmitToArray().ToArray()),
                .AssemblyName = comp.AssemblyName}
        End Function

        Private Shared Function InvokeSemantic(frames As RuntimeFrames, expr As String, enabled As Boolean, ParamArray args As Object()) As Object
            Dim method = CompileRuntimeSlice(frames, "Sem.M", expr, enabled)
            Try
                Return method.Invoke(Nothing, args)
            Catch ex As System.Reflection.TargetInvocationException
                Throw ex.InnerException
            End Try
        End Function

        ''' <summary>
        ''' PART A: rewritten slices must not run when their containing conditional / short-circuit operand is skipped.
        ''' Constant and non-constant conditions; invalid bounds must not throw when unevaluated.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_Semantic_ConditionalEvaluation_NotHoisted()
            Dim frames = CreateSemanticFrames()
            Dim handler As System.ResolveEventHandler = Function(sender, e) If(New System.Reflection.AssemblyName(e.Name).Name = frames.AssemblyName, frames.SourceAssembly, Nothing)
            AddHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            Try
                Dim semType = frames.SourceAssembly.GetType("Sem", throwOnError:=True)
                Dim logField = semType.GetField("Log")
                Dim items = New String() {"alpha", "beta", "gamma"}
                Dim nums = New Integer() {10, 20, 30, 40, 50}
                Dim emptyArgs = New Object() {items, nums, Nothing, Nothing}

                For Each enabled In {False, True}
                    logField.SetValue(Nothing, New System.Text.StringBuilder())
                    Assert.Equal("safe", InvokeSemantic(frames, "If(False, items(100..200)(0), ""safe"")", enabled, emptyArgs))
                    Assert.Equal("", DirectCast(logField.GetValue(Nothing), System.Text.StringBuilder).ToString())

                    logField.SetValue(Nothing, New System.Text.StringBuilder())
                    Assert.Equal(False, InvokeSemantic(frames, "False AndAlso items(100..200).Length > 0", enabled, emptyArgs))
                    Assert.Equal("", DirectCast(logField.GetValue(Nothing), System.Text.StringBuilder).ToString())

                    logField.SetValue(Nothing, New System.Text.StringBuilder())
                    Assert.Equal(True, InvokeSemantic(frames, "True OrElse items(100..200).Length > 0", enabled, emptyArgs))
                    Assert.Equal("", DirectCast(logField.GetValue(Nothing), System.Text.StringBuilder).ToString())

                    ' Non-constant conditions: Cond is observed; the skipped slice must not run (no throw).
                    logField.SetValue(Nothing, New System.Text.StringBuilder())
                    Assert.Equal("safe", InvokeSemantic(frames, "If(Cond(False), items(100..200)(0), ""safe"")", enabled, emptyArgs))
                    Assert.Equal("Cond;False;", DirectCast(logField.GetValue(Nothing), System.Text.StringBuilder).ToString())

                    logField.SetValue(Nothing, New System.Text.StringBuilder())
                    Assert.Equal(False, InvokeSemantic(frames, "Cond(False) AndAlso items(100..200).Length > 0", enabled, emptyArgs))
                    Assert.Equal("Cond;False;", DirectCast(logField.GetValue(Nothing), System.Text.StringBuilder).ToString())

                    logField.SetValue(Nothing, New System.Text.StringBuilder())
                    Assert.Equal(True, InvokeSemantic(frames, "Cond(True) OrElse items(100..200).Length > 0", enabled, emptyArgs))
                    Assert.Equal("Cond;True;", DirectCast(logField.GetValue(Nothing), System.Text.StringBuilder).ToString())
                Next

                ' IL: with a non-constant condition, <>ArraySliceCopy stays in the true branch after the short-circuit branch.
                Dim testData = New CompilationTestData()
                Assert.Equal("", CompileSliceExpression("If(Cond(False), items(100..200)(0), ""safe"")", enabled:=True, testData:=testData, sources:={ArrayRangeSource, SemanticSliceSource}, methodName:="Sem.M"))
                Dim il = testData.GetMethodData("<>x.<>m0").GetMethodIL()
                Dim helperAt = il.IndexOf("<>ArraySliceCopy", StringComparison.Ordinal)
                Dim branchAt = Math.Max(il.IndexOf("brfalse", StringComparison.Ordinal), il.IndexOf("brtrue", StringComparison.Ordinal))
                Assert.True(helperAt > 0 AndAlso branchAt >= 0 AndAlso branchAt < helperAt, $"helper must stay after short-circuit branch; branch@{branchAt} helper@{helperAt}{vbCrLf}{il}")
                Assert.Contains("ldstr      ""safe""", il)
                Assert.Contains("Function Sem.Cond(Boolean)", il)
            Finally
                RemoveHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            End Try
        End Sub

        ''' <summary>
        ''' PART B: receiver, endpoints, surrounding arguments execute once, in BindArrayRange / call order.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_Semantic_EvaluationOrder_MatchesLegacy()
            Dim frames = CreateSemanticFrames()
            Dim handler As System.ResolveEventHandler = Function(sender, e) If(New System.Reflection.AssemblyName(e.Name).Name = frames.AssemblyName, frames.SourceAssembly, Nothing)
            AddHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            Try
                Dim semType = frames.SourceAssembly.GetType("Sem", throwOnError:=True)
                Dim logField = semType.GetField("Log")
                Dim emptyArgs = New Object() {Nothing, Nothing, Nothing, Nothing}
                Dim expectedOrder = "GetFirstValue;GetItems;GetStart;GetEnd;Process;"
                Dim results = New String(1) {}
                Dim logs = New String(1) {}
                For Each enabled In {False, True}
                    logField.SetValue(Nothing, New System.Text.StringBuilder())
                    results(If(enabled, 1, 0)) = DirectCast(InvokeSemantic(frames, "Process(GetFirstValue(), GetItems()(GetStart()..GetEnd()))", enabled, emptyArgs), String)
                    logs(If(enabled, 1, 0)) = DirectCast(logField.GetValue(Nothing), System.Text.StringBuilder).ToString()
                Next
                Assert.Equal("7:20,30,40", results(0))
                Assert.Equal(results(0), results(1))
                Assert.Equal(expectedOrder, logs(0))
                Assert.Equal(logs(0), logs(1))

                ' Multiple slices in one expression; nested slice; slice as argument — off≡on values and a single helper call per slice in IL.
                Assert.Equal(InvokeSemantic(frames, "nums(1..2)(0)", False, {Nothing, New Integer() {10, 20, 30, 40}, Nothing, Nothing}),
                             InvokeSemantic(frames, "nums(1..2)(0)", True, {Nothing, New Integer() {10, 20, 30, 40}, Nothing, Nothing}))
                Dim nestedData = New CompilationTestData()
                Assert.Equal("", CompileSliceExpression("nums(1..3)(0..1)(0)", enabled:=True, testData:=nestedData, sources:={ArrayRangeSource, SemanticSliceSource}, methodName:="Sem.M"))
                Assert.Equal(2, CountOf(nestedData.GetMethodData("<>x.<>m0").GetMethodIL(), "<>ArraySliceCopy"))
            Finally
                RemoveHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            End Try
        End Sub

        ''' <summary>
        ''' PART C: non-default values, identity, empty / invalid / Nothing — loop path agrees with Array.Copy path.
        ''' </summary>
        <Fact>
        Public Sub SliceRewriter_Semantic_ValuesAndExceptions_AgreeWithLegacy()
            Dim frames = CreateSemanticFrames()
            Dim handler As System.ResolveEventHandler = Function(sender, e) If(New System.Reflection.AssemblyName(e.Name).Name = frames.AssemblyName, frames.SourceAssembly, Nothing)
            AddHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            Try
                Dim semType = frames.SourceAssembly.GetType("Sem", throwOnError:=True)
                Dim makeSpots = Function() DirectCast(semType.GetMethod("MakeSpots").Invoke(Nothing, Nothing), System.Array)
                Dim refA As New Object()
                Dim refB As New Object()
                Dim refC As New Object()
                Dim cases = New List(Of (Name As String, Expr As String, Args As Func(Of Object()), Describe As Func(Of Object, String))) From {
                    ("Integer non-default", "nums(1..^1)", Function() {Nothing, New Integer() {10, 20, 30, 40, 50}, Nothing, Nothing},
                        Function(r) String.Join(",", DirectCast(r, Integer()))),
                    ("String non-default", "items(1..^1)", Function() {New String() {"alpha", "beta", "gamma"}, Nothing, Nothing, Nothing},
                        Function(r) String.Join(",", DirectCast(r, String()))),
                    ("Reference identity", "refs(0..3)", Function() {Nothing, Nothing, New Object() {refA, refB, refC}, Nothing},
                        Function(r)
                            Dim arr = DirectCast(r, Object())
                            Return arr.Length.ToString() & ":" & String.Join(",", New String() {
                                Object.ReferenceEquals(arr(0), refA).ToString(),
                                Object.ReferenceEquals(arr(1), refB).ToString(),
                                Object.ReferenceEquals(arr(2), refC).ToString()})
                        End Function),
                    ("Structure fields", "structs(1..3)", Function() {Nothing, Nothing, Nothing, makeSpots()},
                        Function(r)
                            Dim arr = DirectCast(r, System.Array)
                            Return String.Join(",", System.Linq.Enumerable.Range(0, arr.Length).Select(Function(i) arr.GetValue(i).ToString()))
                        End Function),
                    ("Empty slice", "nums(2..2)", Function() {Nothing, New Integer() {10, 20, 30}, Nothing, Nothing},
                        Function(r) "len=" & DirectCast(r, Integer()).Length)}

                Dim expected = New Dictionary(Of String, String) From {
                    {"Integer non-default", "20,30,40"},
                    {"String non-default", "beta"},
                    {"Reference identity", "3:True,True,True"},
                    {"Structure fields", "3/4,5/6"},
                    {"Empty slice", "len=0"}}
                For Each c In cases
                    Dim off = c.Describe(InvokeSemantic(frames, c.Expr, False, c.Args()))
                    Dim [on] = c.Describe(InvokeSemantic(frames, c.Expr, True, c.Args()))
                    _output.WriteLine($"{c.Name}: off={off} on={[on]}")
                    Assert.Equal(c.Name & ": " & expected(c.Name), c.Name & ": " & off)
                    Assert.Equal(c.Name & ": " & off, c.Name & ": " & [on])
                Next

                Assert.Equal("beta", InvokeSemantic(frames, "items(1..^1)(0)", True, {New String() {"alpha", "beta", "gamma"}, Nothing, Nothing, Nothing}))
                Assert.Equal(20, InvokeSemantic(frames, "nums(1..^1)(0)", True, {Nothing, New Integer() {10, 20, 30, 40}, Nothing, Nothing}))

                For Each enabled In {False, True}
                    Dim thrown As Exception = Nothing
                    Try
                        InvokeSemantic(frames, "nums(100..200)", enabled, {Nothing, New Integer() {1, 2, 3}, Nothing, Nothing})
                    Catch ex As Exception
                        thrown = ex
                    End Try
                    ' Array.Copy throws ArgumentException for destination length / bounds; both paths must agree.
                    Assert.IsType(Of ArgumentException)(thrown)

                    thrown = Nothing
                    Try
                        InvokeSemantic(frames, "CType(Nothing, Integer())(0..1)", enabled, {Nothing, Nothing, Nothing, Nothing})
                    Catch ex As Exception
                        thrown = ex
                    End Try
                    Assert.True(TypeOf thrown Is NullReferenceException OrElse TypeOf thrown Is ArgumentNullException, $"{enabled}: {thrown}")
                Next
            Finally
                RemoveHandler System.AppDomain.CurrentDomain.AssemblyResolve, handler
            End Try
        End Sub

        ''' <summary>
        ''' Escape hatch: only VBNEXT_EE_ARRAYSLICE_LOOP=0, exactly, forces the legacy Array.Copy path off.
        ''' </summary>
        <Theory>
        <InlineData(Nothing, False)>
        <InlineData("0", True)>
        <InlineData("1", False)>
        <InlineData("true", False)>
        <InlineData(" 0", False)>
        <InlineData("0 ", False)>
        <InlineData("00", False)>
        <InlineData("", False)>
        Public Sub ArraySliceLoopOption_OnlyExactZeroForcesOff(value As String, expected As Boolean)
            Assert.Equal(expected, EvaluationContext.IsArraySliceLoopForcedOff(value))
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
            Public Sub EnvironmentVariable_ZeroForcesLegacyCopy_DefaultIsLoopForVbNext()
                Dim previous = Environment.GetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable)
                Try
                    Dim runtime = CreateRuntime(ArrayRangeSource, TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext), DebugInformationFormat.PortablePdb)
                    For Each expr In {"a(1..^1)", "a(r)", "a(1..^1)(0)"}
                        Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, Nothing)
                        Dim baseline = CreateMethodContext(runtime, "D.M")
                        Assert.True(baseline.IsArraySliceLoopEnabled)
                        Dim offIL = CompileWithOption(baseline, expr, False)
                        Dim onIL = CompileWithOption(baseline, expr, True)
                        Assert.NotEqual(offIL, onIL)
                        Assert.Equal(onIL, CompileWithOption(baseline, expr, Nothing))
                        Assert.Contains("<>ArraySliceCopy", onIL)
                        Assert.DoesNotContain("System.Array.Copy", onIL)

                        Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, "0")
                        Dim forcedOff = CreateMethodContext(runtime, "D.M")
                        Assert.False(forcedOff.IsArraySliceLoopEnabled)
                        Assert.Equal(offIL, CompileWithOption(forcedOff, expr, Nothing))
                        Assert.Contains("System.Array.Copy", offIL)
                        Assert.DoesNotContain("<>ArraySliceCopy", offIL)

                        ' A context keeps the force-off value that it read when it was created.
                        Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, "0")
                        Dim created = CreateMethodContext(runtime, "D.M")
                        Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, Nothing)
                        Assert.False(created.IsArraySliceLoopEnabled)
                        Assert.Equal(offIL, CompileWithOption(created, expr, Nothing))
                        Assert.True(CreateMethodContext(runtime, "D.M").IsArraySliceLoopEnabled)
                    Next
                Finally
                    Environment.SetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable, previous)
                End Try
                Assert.Equal(previous, Environment.GetEnvironmentVariable(EvaluationContext.ArraySliceLoopVariable))
            End Sub

            ''' <summary>
            ''' VB 17.13 modules and vbnext modules with a Windows PDB (no recorded language version) never enable the loop,
            ''' including when the former opt-in value "1" is set.
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
