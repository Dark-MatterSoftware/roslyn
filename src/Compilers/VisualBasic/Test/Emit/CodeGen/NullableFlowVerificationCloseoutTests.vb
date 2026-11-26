' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGen

    ''' <summary>
    ''' LC-1F.6a verification for scaled flow, recursive summaries, generic substitution, and combined control flow.
    ''' Timing is recorded and is not asserted.
    ''' </summary>
    Public Class NullableFlowVerificationCloseoutTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = New CSharpParseOptions(CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        <Fact>
        Public Sub RecursiveDepth_BaseCaseDoesNotProveTheCycle()
            Dim compilation = Compile(DepthSample(""))
            Assert.Equal("42511:A(depth)|42511:B(depth - 1)", Picture(compilation))
            Assert.Equal(Picture(compilation), Picture(Compile(DepthSample("", reversed:=True))))
        End Sub

        <Fact>
        Public Sub RecursiveDepth_CSharpTrustsTheDeclaredReturn()
            Dim csharp = CreateCSharpCompilation(CSharpDepthSample(), parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable)
            Assert.Equal("", CSharpPicture(csharp))
        End Sub

        <Fact>
        Public Sub RecursiveSummaries_ShapesAndCallerSplit()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Function Self(depth As Integer) As String
        If depth <= 0 Then Return ""ready""
        Return Self(depth - 1)
    End Function
    Public Function One(depth As Integer) As String
        If depth <= 0 Then Return ""ready""
        Return Two(depth)
    End Function
    Public Function Two(depth As Integer) As String
        Return Three(depth)
    End Function
    Public Function Three(depth As Integer) As String
        Return Four(depth)
    End Function
    Public Function Four(depth As Integer) As String
        Return One(depth - 1)
    End Function
    Public Function Left() As String
        Return Right()
    End Function
    Public Function Right() As String
        Return Left()
    End Function
    Public Function Echo(Of T As Class)(value As T, depth As Integer) As T
        If depth <= 0 Then Return value
        Return Echo(value, depth - 1)
    End Function
    Public Function Established() As String
        Return ""ready""
    End Function
    Public Function UseEstablished() As String
        Return Established()
    End Function
    Public Function Bad() As String
        Return Nothing
    End Function
    Public Sub Clear(ByRef value As String)
        Wave(value)
    End Sub
    Public Sub Wave(ByRef value As String)
        Clear(value)
    End Sub
    Public Sub UseClear()
        Dim carried As String = Nothing
        Clear(carried)
        Dim length = carried.Length
    End Sub
    Public Function Again(<NotNullWhen(True)> ByRef value As String?) As Boolean
        Return Other(value)
    End Function
    Public Function Other(<NotNullWhen(True)> ByRef value As String?) As Boolean
        Return Again(value)
    End Function
    Public Sub UseConditional(text As String?)
        If Again(text) Then
            Dim length = text.Length
        End If
    End Sub
    Public Function Known(<NotNullWhen(True)> ByRef value As String?) As Boolean
        If value Is Nothing Then Return False
        Return True
    End Function
    Public Sub UseKnown(ready As String?)
        If Known(ready) Then
            Dim length = ready.Length
        End If
    End Sub
    Public Sub Boom()
        Boom()
    End Sub
    Public Sub AfterBoom()
        Boom()
        Dim loose As String = Nothing
        Dim length = loose.Length
    End Sub
    <DoesNotReturn>
    Public Sub StopNow()
        Throw New System.Exception()
    End Sub
    Public Sub AfterStop()
        StopNow()
        Dim quiet As String = Nothing
        Dim length = quiet.Length
    End Sub
    Public Function UseOne() As String
        Return One(2)
    End Function
    Public Function Mid() As String
        Return One(2)
    End Function
    Public Function UseMid() As String
        Return Mid()
    End Function
    Public Sub UseEcho(text As String)
        Dim echoed = Echo(text, 1)
        Dim length = echoed.Length
    End Sub
End Class
")
            Assert.Equal(1, Count(compilation, 42511, "Self(depth - 1)"))
            Assert.Equal(1, Count(compilation, 42511, "Two(depth)"))
            Assert.Equal(1, Count(compilation, 42511, "Three(depth)"))
            Assert.Equal(1, Count(compilation, 42511, "Four(depth)"))
            Assert.Equal(1, Count(compilation, 42511, "One(depth - 1)"))
            Assert.Equal(1, Count(compilation, 42511, "Right()"))
            Assert.Equal(1, Count(compilation, 42511, "Left()"))
            Assert.Equal(1, Count(compilation, 42511, "Echo(value, depth - 1)"))
            ' A direct recursive call warns in its own body. It does not mark the method as a cycle,
            ' so an acyclic caller still uses the declared return type.
            Assert.Equal(0, Count(compilation, 42512, "echoed"))
            Assert.Equal(0, Count(compilation, 42511, "Established()"))
            Assert.Equal(0, Count(compilation, 42511, """ready"""))
            Assert.Equal(1, Count(compilation, 42511, "Nothing"))
            Assert.Equal(2, Count(compilation, 42510, "Nothing"))
            Assert.Equal(0, Count(compilation, 42512, "carried"))
            Assert.Equal(1, Count(compilation, 42513, "carried"))
            Assert.Equal(1, Count(compilation, 42512, "text"))
            Assert.Equal(0, Count(compilation, 42512, "ready"))
            Assert.Equal(1, Count(compilation, 42512, "loose"))
            Assert.Equal(2, Count(compilation, 42511, "One(2)"))
            Assert.Equal(1, Count(compilation, 42511, "Mid()"))
            Assert.Equal(0, Count(compilation, 42512, "quiet"))
            Assert.Equal(1, Count(compilation, 42528, "Return Again(value)"))
            Assert.Equal(1, Count(compilation, 42528, "Return Other(value)"))
        End Sub

        <Fact>
        Public Sub RecursiveSummaries_MetadataUsesDeclaredContracts()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DoesNotReturnAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class NotNullAttribute : Attribute { }
}
public static class Api
{
    [return: NotNull]
    public static string? A(int depth) => depth <= 0 ? ""ready"" : B(depth - 1);
    [return: NotNull]
    public static string? B(int depth) => A(depth);
    [DoesNotReturn]
    public static void Boom() { Boom(); }
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable)
            Dim consumer = CreateCompilation("
Public Module Program
    Public Function UseA() As String
        Return Api.A(3)
    End Function
    Public Sub AfterBoom()
        Api.Boom()
        Dim loose As String = Nothing
        Dim length = loose.Length
    End Sub
End Module
", references:={csharp.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.Equal("", Picture(consumer))
        End Sub

        <Fact>
        Public Sub Substitution_EveryConstructedSymbolKeepsItsArgument()
            Dim compilation = Compile("
Public Delegate Sub Handler(Of T)(value As T)
Public Class Wrap(Of T)
    Public Function Unwrap(value As T) As T
        Return value
    End Function
End Class
Public Class Pair(Of T, U)
    Public Left As T
    Public Right As U
    Public Property Item As T
    Public Event Changed As Handler(Of U)
    Public Sub Take(value As U)
    End Sub
    Public Function LeftOf(left As T, right As U) As T
        Return left
    End Function
    Public Function RightOf(left As T, right As U) As U
        Return right
    End Function
End Class
Public Interface IGetter(Of T)
    Function Fetch(value As T) As T
End Interface
Public Class Impl(Of T)
    Implements IGetter(Of T)
    Public Value As T
    Public Property Item As T
    Public Event Raised As Handler(Of T)
    Public Sub Take(value As T)
    End Sub
    Public Function Fetch(value As T) As T Implements IGetter(Of T).Fetch
        Return value
    End Function
    Public Function FromMethod(Of U As Class)(value As U) As U
        Return value
    End Function
    Public Function Nest(value As Wrap(Of T)) As Wrap(Of T)
        Return value
    End Function
    Public Function Already(value As T?) As T?
        Return value
    End Function
End Class
Public Class Derived
    Inherits Impl(Of String?)
End Class
Public Module Program
    Public Sub Annotated(box As Impl(Of String?), pair As Pair(Of String, String?), getter As IGetter(Of String?), derived As Derived)
        Dim text = box.Fetch(Nothing)
        Dim textLength = text.Length
        Dim fieldLength = box.Value.Length
        Dim propertyLength = box.Item.Length
        box.Take(Nothing)
        Dim methodText = box.FromMethod(Of String)(""x"")
        Dim methodLength = methodText.Length
        Dim loose = box.FromMethod(Of String?)(Nothing)
        Dim looseLength = loose.Length
        Dim nested = box.Nest(Nothing).Unwrap(Nothing)
        Dim nestedLength = nested.Length
        Dim left = pair.LeftOf(""x"", Nothing)
        Dim leftLength = left.Length
        Dim right = pair.RightOf(""x"", Nothing)
        Dim rightLength = right.Length
        Dim iface = getter.Fetch(Nothing)
        Dim ifaceLength = iface.Length
        Dim inherited = derived.Fetch(Nothing)
        Dim inheritedLength = inherited.Length
        Dim marked = box.Already(Nothing)
        Dim markedLength = marked.Length
    End Sub
    Public Sub Plain(box As Impl(Of String))
        Dim text = box.Fetch(""x"")
        Dim textLength = text.Length
        Dim marked = box.Already(Nothing)
        Dim markedLength = marked.Length
    End Sub
End Module
")
            Dim program = compilation.GlobalNamespace.GetTypeMembers("Program").Single()
            Dim annotatedUse = DirectCast(program.GetMembers("Annotated").Single(), IMethodSymbol)
            Dim plainUse = DirectCast(program.GetMembers("Plain").Single(), IMethodSymbol)
            Dim markedImpl = DirectCast(annotatedUse.Parameters(0).Type, INamedTypeSymbol)
            Dim plainImpl = DirectCast(plainUse.Parameters(0).Type, INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, markedImpl.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, plainImpl.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(markedImpl.GetMembers("Fetch").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(plainImpl.GetMembers("Fetch").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(markedImpl.GetMembers("Value").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(plainImpl.GetMembers("Value").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(markedImpl.GetMembers("Item").Single(), IPropertySymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(plainImpl.GetMembers("Item").Single(), IPropertySymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(markedImpl.GetMembers("Take").Single(), IMethodSymbol).Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(plainImpl.GetMembers("Take").Single(), IMethodSymbol).Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(markedImpl.GetMembers("Raised").Single(), IEventSymbol).Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(DirectCast(plainImpl.GetMembers("Raised").Single(), IEventSymbol).Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())

            Dim boundFromMethod = DirectCast(markedImpl.GetMembers("FromMethod").Single(), IMethodSymbol)
            Dim closedFromMethod = boundFromMethod.Construct(plainImpl.TypeArguments.Single().WithNullableAnnotation(NullableAnnotation.NotAnnotated))
            Assert.Equal(NullableAnnotation.NotAnnotated, closedFromMethod.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(markedImpl.GetMembers("Already").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(plainImpl.GetMembers("Already").Single(), IMethodSymbol).ReturnNullableAnnotation)

            Dim nest = DirectCast(DirectCast(markedImpl.GetMembers("Nest").Single(), IMethodSymbol).ReturnType, INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, nest.TypeArgumentNullableAnnotations.Single())
            Dim plainNest = DirectCast(DirectCast(plainImpl.GetMembers("Nest").Single(), IMethodSymbol).ReturnType, INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, plainNest.TypeArgumentNullableAnnotations.Single())

            Dim pair = DirectCast(annotatedUse.Parameters(1).Type, INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, pair.TypeArgumentNullableAnnotations(0))
            Assert.Equal(NullableAnnotation.Annotated, pair.TypeArgumentNullableAnnotations(1))
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(pair.GetMembers("Left").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(pair.GetMembers("Right").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(pair.GetMembers("Item").Single(), IPropertySymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(pair.GetMembers("Take").Single(), IMethodSymbol).Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(pair.GetMembers("Changed").Single(), IEventSymbol).Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())

            Dim getter = DirectCast(annotatedUse.Parameters(2).Type, INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(getter.GetMembers("Fetch").Single(), IMethodSymbol).ReturnNullableAnnotation)

            Assert.Equal(1, Count(compilation, 42512, "text"))
            Assert.Equal(1, Count(compilation, 42512, "box.Value"))
            Assert.Equal(1, Count(compilation, 42512, "box.Item"))
            Assert.Equal(0, Count(compilation, 42512, "methodText"))
            Assert.Equal(1, Count(compilation, 42512, "loose"))
            Assert.Equal(1, Count(compilation, 42512, "nested"))
            Assert.Equal(0, Count(compilation, 42512, "left"))
            Assert.Equal(1, Count(compilation, 42512, "right"))
            Assert.Equal(1, Count(compilation, 42512, "iface"))
            Dim derivedType = DirectCast(annotatedUse.Parameters(3).Type, INamedTypeSymbol)
            Dim baseFetch = DirectCast(derivedType.BaseType.GetMembers("Fetch").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, baseFetch.ReturnNullableAnnotation)
            Assert.Equal(1, Count(compilation, 42512, "inherited"))
            Assert.Equal(2, Count(compilation, 42512, "marked"))
        End Sub

        <Fact>
        Public Sub Substitution_LegacyWarningsStayOffAndIlMatches()
            Const source = "
Public Class Impl(Of T)
    Public Function Fetch(value As T) As T
        Return value
    End Function
End Class
Public Module Program
    Public Function Go(box As Impl(Of String)) As String
        Return box.Fetch(""x"")
    End Function
End Module
"
            Const legacyMaybe = "
Public Module Program
    Public Sub LegacyMaybe(value As String?)
        Dim length = value.Length
    End Sub
End Module
"
            Dim legacy = CreateCompilation(legacyMaybe, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17)
            Assert.Contains(legacy.GetDiagnostics(), Function(d) d.Code = ERRID.ERR_BadTypeArgForStructConstraintNull)
            Assert.DoesNotContain(legacy.GetDiagnostics(), Function(d) d.Code >= 42510 AndAlso d.Code <= 42532)
            Dim modern = Compile(legacyMaybe)
            Assert.Equal(1, Count(modern, 42512, "value"))
            Assert.Equal(CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=Vb17)).VisualizeIL("Program.Go"),
                         CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)).VisualizeIL("Program.Go"))
        End Sub

        <Fact>
        Public Sub ControlFlow_NestedLoopsAliasesJoinsAndUnstructured()
            Dim compilation = Compile(Attributes() & "
Public Class Node
    Public Child As String
End Class
Public Class Api
    Public Function Known(<NotNullWhen(True)> ByRef value As String?) As Boolean
        If value Is Nothing Then Return False
        Return True
    End Function
    Public Sub NestedLoops()
        Dim text As String = ""x""
        Dim i = 0
        Do
            Dim j = 0
            Do
                text = ""y""
                j += 1
                If j > 1 Then Exit Do
            Loop
            i += 1
            If i > 1 Then Exit Do
        Loop
        Dim kept = text.Length
        Dim maybe As String = ""x""
        For outer = 1 To 2
            For inner = 1 To 2
                If outer = inner Then maybe = Nothing
            Next
        Next
        Dim unsafe = maybe.Length
    End Sub
    Public Sub CarriedAlias()
        Dim node As New Node()
        node.Child = ""x""
        Dim current = node
        For i = 1 To 2
            current = node
        Next
        Dim safe = current.Child.Length
        Dim carried = node
        Do
            carried = node
            node.Child = Nothing
            Exit Do
        Loop
        Dim unsafe = carried.Child.Length
    End Sub
    Public Function ContinueAndReturn(flag As Boolean) As String
        Dim text As String = ""x""
        For i = 1 To 3
            If i = 1 Then Continue For
            text = ""y""
        Next
        Dim safe = text.Length
        If flag Then Return ""ready""
        Throw New System.Exception()
    End Sub
    Public Sub EarlyThrow(flag As Boolean)
        Dim early As String = Nothing
        If flag Then Throw New System.Exception()
        Dim unsafe = early.Length
    End Sub
    Public Sub NestedTry()
        Dim attempt As String = ""x""
        Try
            Try
                attempt = Nothing
                Throw New System.Exception()
            Catch
                Dim caught = attempt.Length
            End Try
        Finally
            Dim finished = attempt.Length
        End Try
    End Sub
    Public Sub NestedJoins(a As Boolean, b As Boolean, c As Boolean)
        Dim chosen As String = ""x""
        If a Then
            If b Then chosen = ""y"" Else chosen = ""z""
        Else
            chosen = ""w""
        End If
        Dim chosenLength = chosen.Length
        Dim mixed As String = ""x""
        If a Then
            If b Then mixed = Nothing Else mixed = ""y""
        Else
            If c Then mixed = ""z"" Else mixed = Nothing
        End If
        Dim mixedLength = mixed.Length
    End Sub
    Public Sub ConditionalAlias(text As String?)
        Dim other = text
        If Known(text) Then
            Dim length = other.Length
        End If
    End Sub
    Public Sub GotoSkipsNull(flag As Boolean)
        Dim joined As String = ""x""
        If flag Then GoTo UseJoined
        joined = Nothing
UseJoined:
        Dim joinedLength = joined.Length
    End Sub
    Public Sub GotoJumpOverNull()
        Dim skipped As String = ""x""
        GoTo UseSkipped
        skipped = Nothing
UseSkipped:
        Dim skippedLength = skipped.Length
    End Sub
End Class
")
            Assert.Equal(0, Count(compilation, 42512, "text"))
            Assert.Equal(1, Count(compilation, 42512, "maybe"))
            Assert.Equal(0, Count(compilation, 42512, "current.Child"))
            Assert.Equal(1, Count(compilation, 42512, "carried.Child"))
            Assert.Equal(1, Count(compilation, 42512, "early"))
            Assert.Equal(2, Count(compilation, 42512, "attempt"))
            Assert.Equal(0, Count(compilation, 42512, "chosen"))
            Assert.Equal(1, Count(compilation, 42512, "mixed"))
            Assert.Equal(1, Count(compilation, 42512, "other"))
            Assert.Equal(1, Count(compilation, 42512, "joined"))
            Assert.Equal(0, Count(compilation, 42512, "skipped"))
            Assert.Equal(1, Count(compilation, 42517, "Child"))
        End Sub

        <Fact>
        Public Sub Performance_ScaledFixturesStayStable()
            Dim sizes = {
                Tuple.Create("alias", AliasSource(8), 0),
                Tuple.Create("alias", AliasSource(32), 0),
                Tuple.Create("alias", AliasSource(128), 0),
                Tuple.Create("alias-null", AliasNullSource(32), 2),
                Tuple.Create("cycle", CycleSource(2), 2),
                Tuple.Create("cycle", CycleSource(8), 8),
                Tuple.Create("cycle", CycleSource(32), 32),
                Tuple.Create("cycles", IndependentCycles(1), 2),
                Tuple.Create("cycles", IndependentCycles(4), 8),
                Tuple.Create("cycles", IndependentCycles(16), 32),
                Tuple.Create("joins", JoinSource(8, nullOnLeft:=False), 0),
                Tuple.Create("joins", JoinSource(32, nullOnLeft:=False), 0),
                Tuple.Create("joins", JoinSource(128, nullOnLeft:=False), 0),
                Tuple.Create("joins-null", JoinSource(32, nullOnLeft:=True), 33),
                Tuple.Create("nest", NestSource(4), 1),
                Tuple.Create("nest", NestSource(8), 1),
                Tuple.Create("nest", NestSource(16), 1)
            }
            For Each item In sizes
                Dim source = item.Item2
                Dim expected = item.Item3
                Dim warmup = System.Diagnostics.Stopwatch.StartNew()
                Dim first = Compile(source)
                warmup.Stop()
                Dim picture = NullablePicture(first)
                Dim nullable = NullableCount(first)
                If item.Item1 = "alias" Then
                    ' Member initialization does not follow an alias chain, so the read warns BC42517.
                    Assert.True(nullable = 1, picture)
                ElseIf item.Item1 = "alias-null" Then
                    Assert.True(nullable >= 2, picture)
                Else
                    Assert.True(nullable = expected, item.Item1 & " " & picture)
                End If
                Dim samples As New List(Of Long)()
                For repetition = 1 To 5
                    Dim timer = System.Diagnostics.Stopwatch.StartNew()
                    Dim again = Compile(source)
                    timer.Stop()
                    samples.Add(timer.ElapsedMilliseconds)
                    Assert.Equal(picture, NullablePicture(again))
                Next
                Dim ordered = samples.OrderBy(Function(value) value).ToArray()
                Console.WriteLine("PERF name={0} expectedNullable={1} warmupMs={2} reps={3} medianMs={4} nullable={5} managed={6} workingSet={7}",
                    item.Item1,
                    expected,
                    warmup.ElapsedMilliseconds,
                    String.Join(",", samples),
                    ordered(2),
                    NullableCount(first),
                    GC.GetTotalMemory(False),
                    System.Diagnostics.Process.GetCurrentProcess().WorkingSet64)
            Next
        End Sub

        Private Shared Function DepthSample(suffix As String, Optional reversed As Boolean = False) As String
            Dim a = "
    Public Function A(depth As Integer) As String
        If depth <= 0 Then
            Return ""ready""
        End If
        Return B(depth - 1)
    End Function"
            Dim b = "
    Public Function B(depth As Integer) As String
        Return A(depth)
    End Function"
            Return "Public Class Api" & If(reversed, b & a, a & b) & suffix & "
End Class
"
        End Function

        Private Shared Function CSharpDepthSample() As String
            Return "
#nullable enable
public static class Api
{
    public static string A(int depth)
    {
        if (depth <= 0) return ""ready"";
        return B(depth - 1);
    }
    public static string B(int depth) => A(depth);
}
"
        End Function

        Private Shared Function AliasSource(count As Integer) As String
            Dim body = "Dim a0 As New Node() : a0.Child = ""x""" & Environment.NewLine
            For index = 1 To count - 1
                body &= "Dim a" & index.ToString() & " = a" & (index - 1).ToString() & Environment.NewLine
            Next
            body &= "Dim length = a" & (count - 1).ToString() & ".Child.Length"
            Return NodeModule(body)
        End Function

        Private Shared Function AliasNullSource(count As Integer) As String
            Dim body = AliasSource(count)
            Return body.Replace("Dim length = ", "a" & (count - 1).ToString() & ".Child = Nothing" & Environment.NewLine & "Dim length = ")
        End Function

        Private Shared Function CycleSource(count As Integer) As String
            Dim body = ""
            For index = 0 To count - 1
                Dim nextIndex = (index + 1) Mod count
                body &= "Public Function M" & index.ToString() & "() As String" & Environment.NewLine &
                    "Return M" & nextIndex.ToString() & "()" & Environment.NewLine &
                    "End Function" & Environment.NewLine
            Next
            Return "Public Class Api" & Environment.NewLine & body & "End Class" & Environment.NewLine
        End Function

        Private Shared Function IndependentCycles(count As Integer) As String
            Dim body = ""
            For index = 0 To count - 1
                body &= "Public Function L" & index.ToString() & "() As String" & Environment.NewLine &
                    "Return R" & index.ToString() & "()" & Environment.NewLine &
                    "End Function" & Environment.NewLine &
                    "Public Function R" & index.ToString() & "() As String" & Environment.NewLine &
                    "Return L" & index.ToString() & "()" & Environment.NewLine &
                    "End Function" & Environment.NewLine
            Next
            Return "Public Class Api" & Environment.NewLine & body & "End Class" & Environment.NewLine
        End Function

        Private Shared Function JoinSource(count As Integer, nullOnLeft As Boolean) As String
            Dim body = "Dim text As String = ""x""" & Environment.NewLine & "Dim flag = True" & Environment.NewLine
            For index = 1 To count
                Dim left = If(nullOnLeft, "Nothing", """a""")
                body &= "If flag Then text = " & left & " Else text = ""b""" & Environment.NewLine
            Next
            body &= "Dim length = text.Length"
            Return "Public Module Program" & Environment.NewLine & "Public Sub Go()" & Environment.NewLine & body & Environment.NewLine & "End Sub" & Environment.NewLine & "End Module" & Environment.NewLine
        End Function

        Private Shared Function NestSource(depth As Integer) As String
            Dim body = "Dim text As String = ""x""" & Environment.NewLine
            For index = 1 To depth
                body &= "If text IsNot Nothing Then" & Environment.NewLine
            Next
            body &= "text = ""y""" & Environment.NewLine
            For index = 1 To depth
                body &= "End If" & Environment.NewLine
            Next
            body &= "Dim length = text.Length"
            Return "Public Module Program" & Environment.NewLine & "Public Sub Go()" & Environment.NewLine & body & Environment.NewLine & "End Sub" & Environment.NewLine & "End Module" & Environment.NewLine
        End Function

        Private Shared Function NodeModule(body As String) As String
            Return "
Public Class Node
    Public Child As String
End Class
Public Module Program
    Public Sub Go()
" & body & "
    End Sub
End Module
"
        End Function

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function

        Private Shared Function Attributes() As String
            Return "
Imports System
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullWhenAttribute
        Inherits Attribute
        Public Sub New(returnValue As Boolean)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Method)>
    Public NotInheritable Class DoesNotReturnAttribute
        Inherits Attribute
    End Class
End Namespace
"
        End Function

        Private Shared Function Picture(compilation As VisualBasicCompilation) As String
            Return String.Join("|", compilation.GetDiagnostics().
                Where(Function(d) d.Severity <> DiagnosticSeverity.Hidden AndAlso d.Code >= 42510 AndAlso d.Code <= 42532).
                Select(Function(d) CInt(d.Code).ToString() & ":" & SpanText(d)).
                OrderBy(Function(text) text, StringComparer.Ordinal))
        End Function

        Private Shared Function NullablePicture(compilation As VisualBasicCompilation) As String
            Return Picture(compilation)
        End Function

        Private Shared Function NullableCount(compilation As VisualBasicCompilation) As Integer
            Return compilation.GetDiagnostics().Count(Function(d) d.Code >= 42510 AndAlso d.Code <= 42532)
        End Function

        Private Shared Function Count(compilation As VisualBasicCompilation, code As Integer, span As String) As Integer
            Return compilation.GetDiagnostics().Count(Function(d) d.Code = code AndAlso SpanText(d) = span)
        End Function

        Private Shared Function CSharpPicture(compilation As CSharpCompilation) As String
            Return String.Join("|", compilation.GetDiagnostics().
                Where(Function(d) d.Severity = DiagnosticSeverity.Warning AndAlso d.Id.StartsWith("CS86", StringComparison.Ordinal)).
                Select(Function(d) d.Id).
                OrderBy(Function(text) text, StringComparer.Ordinal))
        End Function

        Private Shared Function SpanText(diagnostic As Diagnostic) As String
            If diagnostic.Location Is Nothing OrElse diagnostic.Location.SourceTree Is Nothing Then
                Return ""
            End If

            Return diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan)
        End Function

    End Class

End Namespace
