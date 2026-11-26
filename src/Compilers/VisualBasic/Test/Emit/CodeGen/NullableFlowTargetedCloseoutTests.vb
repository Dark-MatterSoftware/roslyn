' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGen

    ''' <summary>
    ''' LC-1F.6d targeted regressions for inferred extension receivers and
    ''' inferred returns that are not identities.
    ''' </summary>
    Public Class NullableFlowTargetedCloseoutTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ExtensionReceiver_InferredAndExplicitResultsWarn()
            Dim compilation = Compile("
Imports System
Imports System.Runtime.CompilerServices
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Sub Test(value As String?, ready As String)
        Dim ordinary = Echo(value)
        Dim reduced = value.Echo()
        Dim explicitType = value.Echo(Of String?)()
        Dim ordinaryReady = Echo(ready)
        Dim reducedReady = ready.Echo()
        Console.WriteLine(ordinary.Length)
        Console.WriteLine(reduced.Length)
        Console.WriteLine(explicitType.Length)
        Console.WriteLine(ordinaryReady.Length)
        Console.WriteLine(reducedReady.Length)
    End Sub
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:ordinary"), seen)
            Assert.True(seen.Contains("42512:reduced"), seen)
            Assert.True(seen.Contains("42512:explicitType"), seen)
            Assert.True(Not seen.Contains("42512:ordinaryReady"), seen)
            Assert.True(Not seen.Contains("42512:reducedReady"), seen)
        End Sub

        <Fact>
        Public Sub NonIdentityInferredReturn_DoesNotUseArgumentState()
            Dim compilation = Compile("
Imports System
Module Program
    Function Other(Of T)(value As T) As T
        Return Nothing
    End Function
    Sub Test(value As String?)
        Dim current As String? = ""kept""
        Dim result = Other(current)
        Console.WriteLine(result.Length)
        If value IsNot Nothing Then
            Dim narrowed = Other(value)
            Console.WriteLine(narrowed.Length)
        End If
    End Sub
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:result"), seen)
            Assert.True(seen.Contains("42512:narrowed"), seen)
        End Sub

        <Fact>
        Public Sub ExtensionReceiver_CoversNestedArgumentsContractsAndOrder()
            Dim compilation = Compile("
Imports System
Imports System.Runtime.CompilerServices
Class Pair(Of T)
    Public Item As T
End Class
Module Extensions
    <Extension>
    Public Function First(Of T)(value As Pair(Of T)) As T
        Return value.Item
    End Function
    <Extension>
    Public Function Pick(Of T, U)(value As T, other As U) As U
        Return other
    End Function
    <Extension>
    Public Function Mix(Of T)(value As T, other As T) As T
        Return other
    End Function
    <Extension>
    Public Function Keep(Of T As Class)(value As T) As <NotNull> T
        Return value
    End Function
End Module
Module Program
    Sub Test(value As String?, ready As String)
        Dim pair As New Pair(Of String?)()
        Dim pairReady As New Pair(Of String)()
        Dim nested = pair.First()
        Dim nestedReady = pairReady.First()
        Dim fromOther = ready.Pick(value)
        Dim fromReceiver = value.Pick(ready)
        Dim mixed = ready.Mix(value)
        Dim current As String? = ""kept""
        Dim kept = current.Keep()
        Dim firstReduced = value.EchoReady()
        Dim secondReduced = ready.EchoReady()
        Dim secondFirst = ready.EchoReady()
        Dim firstAgain = value.EchoReady()
        Console.WriteLine(nested.Length)
        Console.WriteLine(nestedReady.Length)
        Console.WriteLine(fromOther.Length)
        Console.WriteLine(fromReceiver.Length)
        Console.WriteLine(mixed.Length)
        Console.WriteLine(kept.Length)
        Console.WriteLine(firstReduced.Length)
        Console.WriteLine(secondReduced.Length)
        Console.WriteLine(secondFirst.Length)
        Console.WriteLine(firstAgain.Length)
    End Sub
    <Extension>
    Public Function EchoReady(Of T)(value As T) As T
        Return value
    End Function
End Module
" & Attributes())
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:nested"), seen)
            Assert.True(Not seen.Contains("42512:nestedReady"), seen)
            Assert.True(seen.Contains("42512:fromOther"), seen)
            Assert.True(Not seen.Contains("42512:fromReceiver"), seen)
            Assert.True(seen.Contains("42512:mixed"), seen)
            Assert.True(Not seen.Contains("42512:kept"), seen)
            Assert.True(seen.Contains("42512:firstReduced"), seen)
            Assert.True(Not seen.Contains("42512:secondReduced"), seen)
            Assert.True(Not seen.Contains("42512:secondFirst"), seen)
            Assert.True(seen.Contains("42512:firstAgain"), seen)
        End Sub

        <Fact>
        Public Sub InferredShapes_UseTheContributingArguments()
            Dim compilation = Compile("
Imports System
Module Program
    Function Copy(Of T)(ByRef value As T) As T
        Return value
    End Function
    Function Choose(Of T)(value As T, picker As Func(Of T, T)) As T
        Return picker(value)
    End Function
    Async Function Pause(Of T)(value As T) As System.Threading.Tasks.Task(Of T)
        Return value
    End Function
    Function FirstOf(Of T)(ParamArray values As T()) As T
        Return values(0)
    End Function
    Function OptionalValue(Of T)(Optional value As T = Nothing) As T
        Return value
    End Function
    Function OptionalSame(Of T)(value As T, Optional other As T = Nothing) As T
        Return value
    End Function
    Async Function Test(value As String?, ready As String) As System.Threading.Tasks.Task
        Dim copied = Copy(value)
        Dim chosen = Choose(value, Function(item) item)
        Dim paused = Await Pause(value)
        Dim packed = FirstOf(value, ready)
        Dim supplied = OptionalValue(value)
        Dim fromReady = OptionalSame(ready)
        Dim fromValue = OptionalSame(value)
        Console.WriteLine(copied.Length)
        Console.WriteLine(chosen.Length)
        Console.WriteLine(paused.Length)
        Console.WriteLine(packed.Length)
        Console.WriteLine(supplied.Length)
        Console.WriteLine(fromReady.Length)
        Console.WriteLine(fromValue.Length)
    End Function
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:copied"), seen)
            Assert.True(seen.Contains("42512:chosen"), seen)
            Assert.True(seen.Contains("42512:paused"), seen)
            Assert.True(seen.Contains("42512:packed"), seen)
            Assert.True(seen.Contains("42512:supplied"), seen)
            Assert.True(Not seen.Contains("42512:fromReady"), seen)
            Assert.True(seen.Contains("42512:fromValue"), seen)
        End Sub

        <Fact>
        Public Sub MetadataExtension_PreservesTheReceiverAnnotation()
            Dim library = CreateCompilation("
Imports System.Runtime.CompilerServices
Public Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim compilation = CreateCompilation("
Imports System
Module Program
    Sub Test(value As String?, ready As String)
        Dim reduced = value.Echo()
        Dim reducedReady = ready.Echo()
        Console.WriteLine(reduced.Length)
        Console.WriteLine(reducedReady.Length)
    End Sub
End Module
", references:={library.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:reduced"), seen)
            Assert.True(Not seen.Contains("42512:reducedReady"), seen)
        End Sub

        <Fact>
        Public Sub HandlerNullableWrites_AreNotRetained()
            Dim compilation = Compile("
Imports System
Class Box
    Public Name As String
    Public Property Title As String
End Class
Module Program
    Sub Clear(ByRef value As String)
        value = Nothing
    End Sub
    Function NullableName() As String?
        Return Nothing
    End Function
    Sub Mutate(target As Box)
        target.Name = Nothing
    End Sub
    Sub NothingLocal()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = Nothing
        Resume Next
    End Sub
    Sub FromLocal()
        Dim text As String = ""Hello""
        Dim other As String? = Nothing
        On Error GoTo Handler
        Error 5
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = other
        Resume Next
    End Sub
    Sub FromCall()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = NullableName()
        Resume Next
    End Sub
    Sub FromByRef()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        Clear(text)
        Resume Next
    End Sub
    Sub FromField()
        Dim box As New Box()
        box.Name = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(box.Name.Length)
        Exit Sub
Handler:
        box.Name = Nothing
        Resume Next
    End Sub
    Sub FromProperty()
        Dim box As New Box()
        box.Title = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(box.Title.Length)
        Exit Sub
Handler:
        box.Title = Nothing
        Resume Next
    End Sub
    Sub FromAlias()
        Dim box As New Box()
        Dim other As Box = box
        box.Name = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(box.Name.Length)
        Exit Sub
Handler:
        other.Name = Nothing
        Resume Next
    End Sub
    Sub FromMutator()
        Dim box As New Box()
        box.Name = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(box.Name.Length)
        Exit Sub
Handler:
        Mutate(box)
        Resume Next
    End Sub
    Sub ProvenWrite()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = ""safe""
        Resume Next
    End Sub
    Sub Unrelated()
        Dim text As String = ""Hello""
        Dim other As String = ""kept""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(other.Length)
        Exit Sub
Handler:
        text = Nothing
        Resume Next
    End Sub
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:text"), seen)
            Assert.True(seen.Contains("42512:box.Name"), seen)
            Assert.True(seen.Contains("42512:box.Title"), seen)
            Assert.True(Count(compilation, "42512:text") >= 4, seen)
            Assert.True(Count(compilation, "42512:other") = 0, seen)
        End Sub

        <Fact>
        Public Sub HandlerTaint_StaysAfterALaterNonNullWrite()
            Dim compilation = Compile("
Imports System
Module Program
    Sub Reassigned()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        text = ""restored""
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = Nothing
        Resume Next
    End Sub
    Sub Checked()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        If text IsNot Nothing Then
            Console.WriteLine(text.Length)
        End If
        Exit Sub
Handler:
        text = Nothing
        Resume Next
    End Sub
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(Count(compilation, "42512:text") = 2, seen)
        End Sub

        <Fact>
        Public Sub HandlerNameMatch_DoesNotSeeALambdaLocal()
            Dim compilation = Compile("
Imports System
Module Program
    Sub Test()
        Dim text As String = ""Hello""
        Dim unused = Sub()
                         Dim text As String = Nothing
                     End Sub
        On Error GoTo Handler
        Error 5
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        Resume Next
    End Sub
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(Not seen.Contains("42512:text"), seen)
        End Sub

        <Fact>
        Public Sub ExceptionalContinuation_DoesNotKeepANormalPostcondition()
            Dim compilation = Compile(Attributes() & "
Imports System
Module Program
    Function GetName() As <NotNull> String
        Return ""ok""
    End Function
    Sub WhileResumeNext()
        On Error Resume Next
        Dim name = GetName()
        Console.WriteLine(name.Length)
    End Sub
    Sub AfterDisable()
        On Error Resume Next
        On Error GoTo 0
        Dim name = GetName()
        Console.WriteLine(name.Length)
    End Sub
    Sub ResumeLabel()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
Retry:
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = Nothing
        Resume Retry
    End Sub
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:name"), seen)
            Assert.True(Count(compilation, "42512:name") = 1, seen)
            Assert.True(seen.Contains("42512:text"), seen)
        End Sub

        <Fact>
        Public Sub ExtensionEcho_EmitsTheSameILAsVisualBasic17()
            Dim source = "
Imports System.Runtime.CompilerServices
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Public Function Go(value As String) As String
        Return value.Echo()
    End Function
End Module
"
            Dim legacy = CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=Vb17))
            Dim modern = CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext))
            Assert.Equal(legacy.VisualizeIL("Program.Go"), modern.VisualizeIL("Program.Go"))
            Assert.Equal(legacy.VisualizeIL("Extensions.Echo"), modern.VisualizeIL("Extensions.Echo"))
        End Sub

        <Fact>
        Public Sub OnErrorResumeNext_EmitsTheSameILAsVisualBasic17()
            Dim source =
<compilation>
    <file name="a.vb">
Module Program
    Public Sub Go()
        On Error Resume Next
        Dim text As String = "Hello"
        System.Console.WriteLine(text)
    End Sub
End Module
    </file>
</compilation>
            Dim legacy = CompileAndVerify(CompilationUtils.CreateCompilationWithMscorlib40AndVBRuntime(source, options:=TestOptions.ReleaseDll, parseOptions:=Vb17))
            Dim modern = CompileAndVerify(CompilationUtils.CreateCompilationWithMscorlib40AndVBRuntime(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext))
            Assert.Equal(legacy.VisualizeIL("Program.Go"), modern.VisualizeIL("Program.Go"))
        End Sub

        Private Shared Function Attributes() As String
            Return "
Imports System
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Method Or AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.Field Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Method Or AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.Field Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullIfNotNullAttribute
        Inherits Attribute
        Public Sub New(parameterName As String)
        End Sub
    End Class
End Namespace
"
        End Function

        Private Shared Function Count(compilation As VisualBasicCompilation, text As String) As Integer
            Return Picture(compilation).Split("|"c).Count(Function(item) item = text)
        End Function

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function

        Private Shared Function Picture(compilation As VisualBasicCompilation) As String
            Return String.Join("|", compilation.GetDiagnostics().
                Where(Function(diagnostic) diagnostic.Severity <> DiagnosticSeverity.Hidden AndAlso diagnostic.Code >= 42510 AndAlso diagnostic.Code <= 42532).
                Select(Function(diagnostic) CInt(diagnostic.Code).ToString() & ":" & SpanText(diagnostic)).
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
