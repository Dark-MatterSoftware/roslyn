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
    ''' LC-1F.6 regressions for named-argument order, ByRef DoesNotReturnIf, metadata contract names,
    ''' indirect publication, generic awaiters, combined flow, and recursive summaries.
    ''' </summary>
    Public Class NullableFlowConsolidatedAcceptanceTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)
        Private Shared ReadOnly CSharpPreview As CSharpParseOptions = New CSharpParseOptions(CSharp.LanguageVersion.Preview)
        Private Shared ReadOnly CSharpNullable As CSharpCompilationOptions =
            New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable)

        <Fact>
        Public Sub NamedArguments_RuntimeUsesFormalOrderAndReverseCopyBack()
            Dim compilation = CreateCompilation("
Module Program
    Dim log As String = """"
    Function Mark(name As String) As Integer
        log &= name
        Return 1
    End Function
    Sub Take(a As Integer, b As Integer)
    End Sub
    Sub SetBoth(ByRef a As Integer, ByRef b As Integer)
        a = 1
        b = 2
    End Sub
    Function Assign(ByRef target As String) As Integer
        target = ""set""
        Return 1
    End Function
    Function See(value As String) As Integer
        log &= If(value Is Nothing, ""null"", ""set"")
        Return 2
    End Function
    Sub Take2(first As Integer, second As Integer)
    End Sub
    Sub Main()
        Take(b:=Mark(""b""), a:=Mark(""a""))
        Dim i As Integer = 0
        SetBoth(b:=i, a:=i)
        Dim s As String = Nothing
        Take2(second:=See(s), first:=Assign(s))
        System.Console.Write(log)
        System.Console.Write("":"")
        System.Console.Write(i)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)
            ' Formal evaluation writes "ab" then "set". A direct ByRef local is the address
            ' itself, so the callee's later write (b = 2) is the final value.
            CompileAndVerify(compilation, expectedOutput:="abset:2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NamedArguments_CopyBackIsLastFormalFirst()
            Dim compilation = CreateCompilation("
Class Box
    Public Property Item(index As Integer) As Integer
        Get
            Log &= ""get"" & index.ToString()
            Return 0
        End Get
        Set(value As Integer)
            Log &= ""set"" & index.ToString() & ""="" & value.ToString()
        End Set
    End Property
    Public Shared Log As String = """"
End Class
Module Program
    Sub SetBoth(ByRef a As Integer, ByRef b As Integer)
        a = 1
        b = 2
    End Sub
    Sub Main()
        Dim box As New Box()
        SetBoth(b:=box.Item(2), a:=box.Item(1))
        System.Console.Write(Box.Log)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="get1get2set2=2set1=1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NamedArguments_NullableFlowMatchesFormalOrder()
            Dim compilation = Compile(Attributes() & "
Module Program
    Function Assign(<NotNull> ByRef target As String?) As String
        target = ""set""
        Return ""y""
    End Function
    Sub Take(first As String, second As String)
    End Sub
    Sub FormalSetsBeforeRead()
        Dim s As String? = Nothing
        Take(second:=s, first:=Assign(s))
    End Sub
    Sub FormalReadsBeforeSet()
        Dim s As String? = Nothing
        Take(second:=Assign(s), first:=s)
    End Sub
End Module
")
            Assert.Equal("42513:s", Picture(compilation))
        End Sub

        <Fact>
        Public Sub NamedArguments_LegacyIlMatchesVbNext()
            Const source = "
Module Program
    Sub Take(a As Integer, b As Integer)
    End Sub
    Sub Go(value As Integer)
        Take(b:=value, a:=value + 1)
    End Sub
End Module
"
            Dim legacy = CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=Vb17))
            Dim modern = CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext))
            Assert.Equal(legacy.VisualizeIL("Program.Go"), modern.VisualizeIL("Program.Go"))
        End Sub

        <Fact>
        Public Sub DoesNotReturnIf_ByRefIsIneffective_ByValNarrows()
            Dim compilation = Compile(Attributes() & "
Module Program
    Sub FailRef(<DoesNotReturnIf(True)> ByRef condition As Boolean)
        condition = False
    End Sub
    Sub FailVal(<DoesNotReturnIf(True)> condition As Boolean)
    End Sub
    Sub AfterByRef(value As String?)
        Dim flag = value Is Nothing
        FailRef(flag)
        Dim length = value.Length
    End Sub
    Sub AfterByVal(value As String?)
        FailVal(value Is Nothing)
        Dim length = value.Length
    End Sub
    Sub ConstantByRef()
        Dim flag = True
        FailRef(flag)
        Dim text As String = ""still""
        Dim length = text.Length
    End Sub
End Module
")
            Assert.Equal("42512:value", Picture(compilation))
        End Sub

        <Fact>
        Public Sub DoesNotReturnIf_ByRefMetadataAgreesWithSource()
            Dim library = Compile(Attributes() & "
Public Module Api
    Public Sub FailRef(<DoesNotReturnIf(True)> ByRef condition As Boolean)
    End Sub
    Public Sub FailVal(<DoesNotReturnIf(True)> condition As Boolean)
    End Sub
End Module
")
            Dim consumer = CreateCompilation("
Public Module Program
    Public Sub AfterByRef(value As String?)
        Dim flag = value Is Nothing
        Api.FailRef(flag)
        Dim length = value.Length
    End Sub
    Public Sub AfterByVal(value As String?)
        Api.FailVal(value Is Nothing)
        Dim length = value.Length
    End Sub
End Module
", references:={library.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.Equal("42512:value", Picture(consumer))
        End Sub

        <Fact>
        Public Sub DoesNotReturnIf_CSharpAlsoIgnoresByRef()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class DoesNotReturnIfAttribute : Attribute
    {
        public DoesNotReturnIfAttribute(bool parameterValue) { }
    }
}
public class Api
{
    public static void FailRef([DoesNotReturnIf(true)] ref bool condition) { condition = false; }
    public static void FailVal([DoesNotReturnIf(true)] bool condition) { }
    public static int AfterByRef(string? value)
    {
        bool flag = value == null;
        FailRef(ref flag);
        return value.Length;
    }
    public static int AfterByVal(string? value)
    {
        FailVal(value == null);
        return value.Length;
    }
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable)
            Dim warnings = csharp.GetDiagnostics().Where(Function(d) d.Id = "CS8602").ToArray()
            Assert.Single(warnings)
            Assert.Equal("value", warnings(0).Location.SourceTree.GetText().ToString(warnings(0).Location.SourceSpan))
        End Sub

        <Fact>
        Public Sub NotNullIfNotNull_OrdinalNameMatching()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Function Exact(value As String?) As <NotNullIfNotNull(""value"")> String?
        Return Nothing
    End Function
    Public Function DifferentCase(value As String?) As <NotNullIfNotNull(""Value"")> String?
        Return Nothing
    End Function
    Public Function Missing(value As String?) As <NotNullIfNotNull(""missing"")> String?
        Return value
    End Function
    Public Function Either(left As String?, right As String?) As <NotNullIfNotNull(""left""), NotNullIfNotNull(""right"")> String?
        Return Nothing
    End Function
    Public Function Pick(left As String?, right As String?) As <NotNullIfNotNull(""left"")> String?
        Return left
    End Function
    Public Sub Use(ready As String, loose As String?)
        Dim matched = Exact(ready)
        Dim matchedLength = matched.Length
        Dim open = Exact(loose)
        Dim openLength = open.Length
        Dim renamed = Pick(right:=loose, left:=ready)
        Dim renamedLength = renamed.Length
        Dim swapped = Pick(right:=ready, left:=loose)
        Dim swappedLength = swapped.Length
    End Sub
End Class
")
            Assert.Equal(1, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_BadNullableParameterName AndAlso d.GetMessage().Contains("Value")))
            Assert.Equal(1, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_BadNullableParameterName AndAlso d.GetMessage().Contains("missing")))
            Assert.Equal(0, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull AndAlso d.GetMessage().Contains("Value")))
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull AndAlso d.GetMessage().Contains("value"))
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull AndAlso d.GetMessage().Contains("left"))
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull AndAlso d.GetMessage().Contains("right"))
            Assert.Equal(1, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "open"))
            Assert.Equal(1, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "swapped"))
            Assert.Equal(0, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "matched"))
            Assert.Equal(0, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "renamed"))
        End Sub

        <Fact>
        Public Sub NotNullIfNotNull_MetadataOrdinalNames()
            Dim csharp = CreateCSharpCompilation("
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = true)]
    public sealed class NotNullIfNotNullAttribute : Attribute
    {
        public NotNullIfNotNullAttribute(string parameterName) { }
    }
}
public class Api
{
    [return: NotNullIfNotNull(""value"")]
    public static string? Exact(string? value) => value;
    [return: NotNullIfNotNull(""Value"")]
    public static string? DifferentCase(string? value) => value;
}
", parseOptions:=CSharpPreview, compilationOptions:=CSharpNullable)
            Dim consumer = CreateCompilation("
Public Module Program
    Public Sub Use(ready As String, loose As String?)
        Dim matched = Api.Exact(ready)
        Dim matchedLength = matched.Length
        Dim open = Api.Exact(loose)
        Dim openLength = open.Length
        Dim renamed = Api.DifferentCase(ready)
        Dim renamedLength = renamed.Length
    End Sub
End Module
", references:={csharp.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.Equal(0, consumer.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "matched"))
            Assert.Equal(1, consumer.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "open"))
            Assert.Equal(1, consumer.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "renamed"))
        End Sub

        <Fact>
        Public Sub NotNullIfNotNull_OriginalInputStillRejectsNullReturn()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        value = Nothing
        Return Nothing
    End Function
    Public Function Keep(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        Return value
    End Function
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull AndAlso d.GetMessage().Contains("value"))
            Assert.Equal(1, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull))
        End Sub

        <Fact>
        Public Sub Publication_FieldMutationClearsProvenStore_UnrelatedReadSurvives()
            Dim compilation = Compile("
Public Class Node
    Public Child As String?
End Class
Public Class Box
    Public Item As Node
End Class
Public Module Program
    Public Sub Mutate()
        Dim node As New Node()
        node.Child = ""ok""
        Dim box As New Box()
        box.Item = node
        box.Item.Child = Nothing
        Dim length = node.Child.Length
    End Sub
    Public Sub Keep()
        Dim node As New Node()
        node.Child = ""ok""
        Dim box As New Box()
        box.Item = node
        Dim length = node.Child.Length
    End Sub
    Public Sub OtherObject()
        Dim node As New Node()
        node.Child = ""ok""
        Dim other As New Node()
        other.Child = Nothing
        Dim length = node.Child.Length
    End Sub
End Module
")
            Assert.Equal(1, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference))
            Assert.Equal("node.Child", SpanText(compilation.GetDiagnostics().Single(Function(d) d.Code = ERRID.WRN_PossibleNullReference)))
        End Sub

        <Fact>
        Public Sub Publication_LocalAliasWritePropagates_ReadKeepsFact()
            Dim compilation = Compile("
Public Class Node
    Public Child As String?
End Class
Public Module Program
    Public Sub WriteThroughAlias()
        Dim node As New Node()
        node.Child = ""ok""
        Dim other = node
        other.Child = Nothing
        Dim length = node.Child.Length
    End Sub
    Public Sub ReadThroughAlias()
        Dim node As New Node()
        node.Child = ""ok""
        Dim other = node
        Dim length = other.Child.Length
    End Sub
End Module
")
            Assert.Equal("42512:node.Child", Picture(compilation))
        End Sub

        <Fact>
        Public Sub Publication_ParameterAliasByRefDelegateAndCall()
            Dim compilation = Compile("
Public Class Node
    Public Child As String?
End Class
Delegate Sub Callback()
Public Module Program
    Public Sub Replace(ByRef target As Node)
    End Sub
    Public Sub Keep(value As Node)
    End Sub
    Public Sub Test(node As Node)
        node.Child = ""ok""
        Dim other = node
        other.Child = Nothing
        Dim length = node.Child.Length
        node.Child = ""ok""
        Replace(node)
        Dim afterByRef = node.Child.Length
        node.Child = ""ok""
        Keep(node)
        Dim afterCall = node.Child.Length
        node.Child = ""ok""
        Dim action As Callback = Sub() node.Child = Nothing
        action.Invoke()
        Dim afterDelegate = node.Child.Length
    End Sub
End Module
")
            Assert.True(compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference) >= 4, Picture(compilation))
        End Sub

        <Fact>
        Public Sub Publication_CustomSetterDropsFacts_ExceptionSeesMutation()
            Dim compilation = Compile("
Public Class Node
    Public Child As String?
End Class
Public Class Box
    Private _item As Node
    Public Property Item As Node
        Get
            Return _item
        End Get
        Set(value As Node)
            _item = value
        End Set
    End Property
End Class
Public Module Program
    Public Sub Store(box As Box, node As Node)
        node.Child = ""ok""
        box.Item = node
        Dim length = node.Child.Length
    End Sub
    Public Sub Explode()
        Dim node As New Node()
        node.Child = ""ok""
        Dim box As New Box()
        Try
            box.Item = node
            box.Item.Child = Nothing
            Throw New System.Exception()
        Catch
            Dim length = node.Child.Length
        End Try
    End Sub
End Module
")
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "node.Child"))
        End Sub

        <Fact>
        Public Sub Awaiters_GenericInterfaceNestedAndInvalidation()
            Dim compilation = Compile("
Imports System
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks
Public Interface IGetter(Of T)
    Inherits INotifyCompletion
    ReadOnly Property IsCompleted As Boolean
    Function GetResult() As T
End Interface
Public Structure Getter(Of T)
    Implements IGetter(Of T)
    Public ReadOnly Property IsCompleted As Boolean Implements IGetter(Of T).IsCompleted
        Get
            Return True
        End Get
    End Property
    Public Sub OnCompleted(continuation As Action) Implements INotifyCompletion.OnCompleted
    End Sub
    Public Function GetResult() As T Implements IGetter(Of T).GetResult
        Throw New System.Exception()
    End Function
End Structure
Public Class Awaitable(Of T)
    Public Function GetAwaiter() As IGetter(Of T)
        Return New Getter(Of T)()
    End Function
End Class
Public Class Outer(Of T)
    Public Function Make() As Awaitable(Of T)
        Return New Awaitable(Of T)()
    End Function
End Class
Public Class Node
    Public Child As String?
End Class
Public Module Program
    Public Async Function Results(ready As Awaitable(Of String), loose As Awaitable(Of String?), made As Outer(Of String?)) As Task
        Dim stable = Await ready
        Dim kept = stable.Length
        Dim text = Await loose
        Dim length = text.Length
        Dim nested = Await made.Make()
        Dim nestedLength = nested.Length
    End Function
    Public Async Function FreshSurvives() As Task
        Dim node As New Node()
        node.Child = ""ok""
        Await New Awaitable(Of String)()
        Dim length = node.Child.Length
    End Function
    Public Async Function ParameterDrops(node As Node) As Task
        node.Child = ""ok""
        Await New Awaitable(Of String)()
        Dim length = node.Child.Length
    End Function
End Module
")
            Dim references = compilation.GetDiagnostics().Where(Function(d) d.Code = ERRID.WRN_PossibleNullReference).ToArray()
            Assert.True(references.Length = 3, Picture(compilation))
        End Sub

        <Fact>
        Public Sub CombinedFlow_AliasContractByRefAndException()
            Dim compilation = Compile(Attributes() & "
Public Class Node
    Public Child As String?
End Class
Public Module Program
    Public Sub Fail(<DoesNotReturn> )
    End Sub
    Public Sub Clear(ByRef child As String?)
        child = Nothing
    End Sub
    Public Function Pick(value As String?) As <NotNullIfNotNull(""value"")> String?
        Return value
    End Function
    Public Sub Use(node As Node, backup As Node)
        node.Child = ""ok""
        Dim other = node
        Try
            If other.Child IsNot Nothing Then
                Dim ready = Pick(other.Child)
                Dim kept = ready.Length
                Clear(other.Child)
                Dim lost = node.Child.Length
                Fail()
                node.Child = ""restored""
            End If
        Catch
            Dim caught = node.Child.Length
        Finally
            Dim finalLength = node.Child.Length
        End Try
        backup.Child = ""ok""
        Dim separate = backup.Child.Length
    End Sub
End Module
")
            Assert.Equal(3, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "node.Child"))
            Assert.Equal(0, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "ready"))
            Assert.Equal(0, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "backup.Child"))
        End Sub

        <Fact>
        Public Sub ControlFlow_JoinsLoopsSelectAndFinally()
            Dim compilation = Compile("
Public Module Program
    Public Sub Branches(value As String?, flag As Boolean, mode As Integer)
        Dim text As String = ""ready""
        If flag Then
            text = value
        Else
            text = ""ok""
        End If
        Dim length = text.Length
        Select Case mode
            Case 1
                text = ""one""
            Case Else
                text = value
        End Select
        Dim selected = text.Length
        If value IsNot Nothing AndAlso value.Length > 0 Then
            Dim known = value.Length
        End If
    End Sub
    Public Sub LoopExit(value As String?)
        Do
            If value IsNot Nothing Then
                Exit Do
            End If
            Return
        Loop
        Dim length = value.Length
    End Sub
    Public Function FinallyAssigns() As String
        Try
            FinallyAssigns = Nothing
        Finally
            FinallyAssigns = ""ready""
        End Try
    End Function
End Module
")
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullAssignment))
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference))
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))
        End Sub

        <Fact>
        Public Sub RecursiveSummaries_DoNotProveCycles_OrderIndependent()
            Const body = "
Public Class Api
    Public Function First(value As String?) As String
        Return Second(value)
    End Function
    Public Function Second(value As String?) As String
        Return Third(value)
    End Function
    Public Function Third(value As String?) As String
        Return First(value)
    End Function
    Public Function Ready() As String
        Return ""ready""
    End Function
    Public Function UseCycle(value As String?) As Integer
        Return First(value).Length
    End Function
    Public Function UseReady() As Integer
        Return Ready().Length
    End Function
    Public Function Left() As String
        Return Right()
    End Function
    Public Function Right() As String
        Return Left()
    End Function
End Class
"
            Const reversed = "
Public Class Api
    Public Function Right() As String
        Return Left()
    End Function
    Public Function Left() As String
        Return Right()
    End Function
    Public Function UseReady() As Integer
        Return Ready().Length
    End Function
    Public Function Ready() As String
        Return ""ready""
    End Function
    Public Function UseCycle(value As String?) As Integer
        Return First(value).Length
    End Function
    Public Function Third(value As String?) As String
        Return First(value)
    End Function
    Public Function Second(value As String?) As String
        Return Third(value)
    End Function
    Public Function First(value As String?) As String
        Return Second(value)
    End Function
End Class
"
            Dim forward = Compile(body)
            Dim backward = Compile(reversed)
            Assert.Equal(5, forward.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))
            Assert.Equal(1, forward.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference))
            Assert.Equal(0, forward.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReference AndAlso SpanText(d) = "Ready()"))
            Assert.Equal(Picture(forward), Picture(backward))
        End Sub

        <Fact>
        Public Sub RecursiveSummaries_EstablishedGuaranteeAndInvalidDeclaration()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Function Established() As String
        If System.DateTime.Now.Ticks < 0 Then
            Return Cycle()
        End If
        Return ""ready""
    End Function
    Public Function Cycle() As String
        Return Established()
    End Function
    <NotNull>
    Public Function Claims(value As String?) As String?
        Return Claims(value)
    End Function
    Public Function Echo(Of T As Class)(value As T) As T
        Return Echo(value)
    End Function
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_PossibleNullReturn)
            Assert.True(compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReturn) >= 2)
        End Sub

        <Fact>
        Public Sub LegacyLanguage_DoesNotReportVbNextNullableWarnings()
            Dim source = Attributes() & "
Public Class Api
    Public Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        value = Nothing
        Return Nothing
    End Function
    Public Sub Use(value As String?)
        Dim length = value.Length
    End Sub
End Class
"
            Dim legacy = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17)
            Assert.DoesNotContain(legacy.GetDiagnostics(), Function(d) CInt(d.Code) >= 42510 AndAlso CInt(d.Code) <= 42532)
        End Sub

        <Fact>
        Public Sub Convergence_RepeatedAnalysesStayStable()
            Dim source = "
Public Class Node
    Public Child As String?
End Class
Public Module Program
    Public Sub Walk(node As Node, flag As Boolean)
        Dim i = 0
        While i < 4
            Dim copy = node
            If flag Then
                copy.Child = Nothing
            ElseIf copy.Child IsNot Nothing Then
                Dim length = copy.Child.Length
            End If
            i += 1
        End While
    End Sub
End Module
"
            Dim started = System.Diagnostics.Stopwatch.StartNew()
            Dim first = Picture(Compile(source))
            Dim second = Picture(Compile(source))
            started.Stop()
            Assert.Equal(first, second)
            Assert.True(started.Elapsed < System.TimeSpan.FromSeconds(30))
        End Sub

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function

        Private Shared Function Attributes() As String
            Return "
Imports System
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue, AllowMultiple:=True)>
    Public NotInheritable Class NotNullIfNotNullAttribute
        Inherits Attribute
        Public Sub New(parameterName As String)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Parameter)>
    Public NotInheritable Class DoesNotReturnIfAttribute
        Inherits Attribute
        Public Sub New(parameterValue As Boolean)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Method)>
    Public NotInheritable Class DoesNotReturnAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
End Namespace
"
        End Function

        Private Shared Function Picture(compilation As VisualBasicCompilation) As String
            Return String.Join("|", compilation.GetDiagnostics().
                Where(Function(d) d.Severity <> DiagnosticSeverity.Hidden).
                Select(Function(d) CInt(d.Code).ToString() & ":" & SpanText(d)).
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
