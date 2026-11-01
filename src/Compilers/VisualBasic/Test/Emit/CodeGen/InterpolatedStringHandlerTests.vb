' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class InterpolatedStringHandlerTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const HandlerAttribute As String = "
Namespace System.Runtime.CompilerServices
    <System.AttributeUsage(System.AttributeTargets.Class Or System.AttributeTargets.Struct, AllowMultiple:=False, Inherited:=False)>
    Public NotInheritable Class InterpolatedStringHandlerAttribute
        Inherits System.Attribute
    End Class
End Namespace
"

        ' Writes each call it receives, so the output shows the construction and the Append calls in order.
        Private Const TraceHandler As String = HandlerAttribute & "
<System.Runtime.CompilerServices.InterpolatedStringHandler>
Public Structure TraceHandler
    Private _text As String

    Public Sub New(literalLength As Integer, formattedCount As Integer)
        _text = ""new("" & literalLength & "","" & formattedCount & "")""
    End Sub

    Public Sub AppendLiteral(value As String)
        _text &= "" literal("" & value & "")""
    End Sub

    Public Sub AppendFormatted(Of T)(value As T)
        _text &= "" formatted("" & value.ToString() & "")""
    End Sub

    Public Sub AppendFormatted(Of T)(value As T, format As String)
        _text &= "" formatted("" & value.ToString() & "":"" & format & "")""
    End Sub

    Public Sub AppendFormatted(Of T)(value As T, alignment As Integer)
        _text &= "" formatted("" & value.ToString() & "","" & alignment & "")""
    End Sub

    Public Sub AppendFormatted(Of T)(value As T, alignment As Integer, format As String)
        _text &= "" formatted("" & value.ToString() & "","" & alignment & "":"" & format & "")""
    End Sub

    Public Overrides Function ToString() As String
        Return _text
    End Function
End Structure
"

        Private Const UndeclaredInInterpolations As String = "
Module Program
    Sub Main()
        Dim x = Undefined
        System.Console.WriteLine($""plain {x}"")
        Dim f As System.FormattableString = $""formattable {x}""
    End Sub
End Module
"

        Private Shared Function ErrorIds(compilation As VisualBasicCompilation) As String()
            Return compilation.GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error).Select(Function(d) d.Id).ToArray()
        End Function

        <Fact>
        Public Sub ErrorTypedInterpolation_ReportsOnlyItsError()
            Dim compilation = CreateCompilation(UndeclaredInInterpolations, parseOptions:=VbNext)
            AssertEx.Equal({"BC30451"}, ErrorIds(compilation))
        End Sub

        <Fact>
        Public Sub ErrorTypedInterpolation_CascadesOn17_13()
            Dim compilation = CreateCompilation(UndeclaredInInterpolations, parseOptions:=Vb17_13)
            AssertEx.Equal({"BC30451", "BC37251", "BC37251"}, ErrorIds(compilation))
        End Sub

        <Fact>
        Public Sub ErrorTypedInterpolation_HandlerReportsOnlyItsError()
            Dim compilation = CreateCompilation(TraceHandler & "
Module Program
    Sub Log(message As TraceHandler)
    End Sub

    Sub Main()
        Dim x = Undefined
        Log($""value {x}"")
    End Sub
End Module
", parseOptions:=VbNext)
            AssertEx.Equal({"BC30451"}, ErrorIds(compilation))
        End Sub

        <Fact>
        Public Sub HandlerWithoutAppendFormatted_StillReportsFactoryError()
            Dim compilation = CreateCompilation(HandlerAttribute & "
<System.Runtime.CompilerServices.InterpolatedStringHandler>
Public Structure LiteralOnlyHandler
    Public Sub New(literalLength As Integer, formattedCount As Integer)
    End Sub

    Public Sub AppendLiteral(value As String)
    End Sub
End Structure

Module Program
    Sub Log(message As LiteralOnlyHandler)
    End Sub

    Sub Main()
        Log($""value {1}"")
    End Sub
End Module
", parseOptions:=VbNext)
            AssertEx.Equal({"BC37251"}, ErrorIds(compilation))
        End Sub

        <Fact>
        Public Sub Handler_ConstructsAndAppendsInOrder()
            Dim compilation = CreateCompilation(TraceHandler & "
Module Program
    Sub Log(message As TraceHandler)
        System.Console.Write(message.ToString())
    End Sub

    Sub Main()
        Dim name = ""Ada""
        Dim count = 3
        Log($""Hi {name}, {count:D2} and {count,4} or {count,-2:X}{{}}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="new(16,4) literal(Hi ) formatted(Ada) literal(, ) formatted(3:D2) literal( and ) formatted(3,4) literal( or ) formatted(3,-2:X) literal({})").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Handler_ShouldAppendFalseSkipsAppends()
            Dim compilation = CreateCompilation(HandlerAttribute & "
<System.Runtime.CompilerServices.InterpolatedStringHandler>
Public Structure GateHandler
    Public Sub New(literalLength As Integer, formattedCount As Integer, ByRef shouldAppend As Boolean)
        System.Console.Write(""new "")
        shouldAppend = Program.Enabled
    End Sub

    Public Sub AppendLiteral(value As String)
        System.Console.Write(""literal "")
    End Sub

    Public Sub AppendFormatted(Of T)(value As T)
        System.Console.Write(""formatted "")
    End Sub
End Structure

Module Program
    Public Enabled As Boolean

    Function Value() As Integer
        System.Console.Write(""value "")
        Return 1
    End Function

    Sub Log(message As GateHandler)
    End Sub

    Sub Main()
        Enabled = False
        Log($""a{Value()}"")
        System.Console.Write(""| "")
        Enabled = True
        Log($""a{Value()}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="new | new literal value formatted").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Handler_BooleanAppendStopsAtFalse()
            Dim compilation = CreateCompilation(HandlerAttribute & "
<System.Runtime.CompilerServices.InterpolatedStringHandler>
Public Structure LimitHandler
    Private _remaining As Integer

    Public Sub New(literalLength As Integer, formattedCount As Integer)
        _remaining = 2
    End Sub

    Public Function AppendLiteral(value As String) As Boolean
        System.Console.Write(value)
        _remaining -= 1
        Return _remaining > 0
    End Function

    Public Function AppendFormatted(Of T)(value As T) As Boolean
        System.Console.Write(value)
        _remaining -= 1
        Return _remaining > 0
    End Function
End Structure

Module Program
    Function Value(n As Integer) As Integer
        System.Console.Write(""["" & n & ""]"")
        Return n
    End Function

    Sub Log(message As LimitHandler)
    End Sub

    Sub Main()
        Log($""a{Value(1)}b{Value(2)}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="a[1]1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Handler_PreferredOverStringForNonConstant()
            Dim compilation = CreateCompilation(TraceHandler & "
Module Program
    Sub Log(message As String)
        System.Console.Write(""string:"" & message & "" "")
    End Sub

    Sub Log(message As TraceHandler)
        System.Console.Write(""handler:"" & message.ToString() & "" "")
    End Sub

    Sub Main()
        Dim name = ""Ada""
        Log($""Hi {name}"")
        Log($""Hi"")
        Log(""plain"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="handler:new(3,1) literal(Hi ) formatted(Ada) string:Hi string:plain").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Handler_NotAConversionOn17_13()
            Dim compilation = CreateCompilation(TraceHandler & "
Module Program
    Sub Log(message As TraceHandler)
    End Sub

    Sub Main()
        Dim name = ""Ada""
        Log($""Hi {name}"")
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30311: Value of type 'String' cannot be converted to 'TraceHandler'.
        Log($"Hi {name}")
            ~~~~~~~~~~~~
]]></expected>)
        End Sub

        Private Const ArgumentAttribute As String = HandlerAttribute & "
Namespace System.Runtime.CompilerServices
    <System.AttributeUsage(System.AttributeTargets.Parameter, AllowMultiple:=False, Inherited:=False)>
    Public NotInheritable Class InterpolatedStringHandlerArgumentAttribute
        Inherits System.Attribute

        Public Sub New(argument As String)
        End Sub

        Public Sub New(ParamArray arguments As String())
        End Sub
    End Class
End Namespace
"

        <Fact>
        Public Sub HandlerArgument_ReadsAnEarlierArgumentOnce()
            Dim compilation = CreateCompilation("Imports System.Runtime.CompilerServices
" & ArgumentAttribute & "

<InterpolatedStringHandler>
Public Structure PrefixHandler
    Private _text As String

    Public Sub New(literalLength As Integer, formattedCount As Integer, prefix As String, level As Integer)
        _text = ""["" & prefix & level & ""] ""
    End Sub

    Public Sub AppendLiteral(value As String)
        _text &= value
    End Sub

    Public Sub AppendFormatted(Of T)(value As T)
        _text &= value.ToString()
    End Sub

    Public Overrides Function ToString() As String
        Return _text
    End Function
End Structure

Module Program
    Function Prefix() As String
        System.Console.Write(""prefix "")
        Return ""p""
    End Function

    Function Level() As Integer
        System.Console.Write(""level "")
        Return 7
    End Function

    Sub Log(prefix As String, level As Integer, <InterpolatedStringHandlerArgument(""prefix"", ""level"")> message As PrefixHandler)
        System.Console.Write(prefix & level & "": "" & message.ToString())
    End Sub

    Sub Main()
        Dim n = 2
        Log(Prefix(), Level(), $""n={n}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="prefix level p7: [p7] n=2").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub HandlerArgument_ReadsTheReceiverOnce()
            Dim compilation = CreateCompilation("Imports System.Runtime.CompilerServices
" & ArgumentAttribute & "

Public Class Logger
    Public Name As String

    Public Sub Log(<InterpolatedStringHandlerArgument("""")> message As LoggerHandler)
        System.Console.Write(message.ToString())
    End Sub
End Class

<InterpolatedStringHandler>
Public Structure LoggerHandler
    Private _text As String

    Public Sub New(literalLength As Integer, formattedCount As Integer, logger As Logger)
        _text = logger.Name & "": ""
    End Sub

    Public Sub AppendLiteral(value As String)
        _text &= value
    End Sub

    Public Sub AppendFormatted(Of T)(value As T)
        _text &= value.ToString()
    End Sub

    Public Overrides Function ToString() As String
        Return _text
    End Function
End Structure

Module Program
    Function GetLogger() As Logger
        System.Console.Write(""get "")
        Return New Logger With {.Name = ""app""}
    End Function

    Sub Main()
        GetLogger().Log($""ready {1}"")
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="get app: ready 1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub HandlerArgument_MustNameAnEarlierParameterOrTheInstance()
            Dim compilation = CreateCompilation("Imports System.Runtime.CompilerServices
" & ArgumentAttribute & "

<InterpolatedStringHandler>
Public Structure NamedHandler
    Public Sub New(literalLength As Integer, formattedCount As Integer, value As Integer)
    End Sub

    Public Sub AppendLiteral(value As String)
    End Sub
End Structure

Module Program
    Sub Missing(<InterpolatedStringHandlerArgument(""count"")> message As NamedHandler)
    End Sub

    Sub Later(<InterpolatedStringHandlerArgument(""count"")> message As NamedHandler, count As Integer)
    End Sub

    Sub Main()
        Missing($""a"")
        Later($""a"", 1)
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37387: The interpolated string handler for parameter 'message' reads 'count', which is not the instance ("") or a ByVal parameter that comes before 'message'.
        Missing($"a")
                ~~~~
BC37387: The interpolated string handler for parameter 'message' reads 'count', which is not the instance ("") or a ByVal parameter that comes before 'message'.
        Later($"a", 1)
              ~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub HandlerArgument_DebugAssertReadsTheCondition()
            Dim compilation = CreateCompilation("
#Const DEBUG = True
Imports System.Diagnostics

Module Program
    Sub Check(ok As Boolean, value As Integer)
        Debug.Assert(ok, $""value {value}"")
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.FailsPEVerify).VerifyDiagnostics().VerifyIL("Program.Check", "
{
  // Code size       53 (0x35)
  .maxstack  6
  .locals init (Boolean V_0,
                System.Diagnostics.Debug.AssertInterpolatedStringHandler V_1,
                System.Diagnostics.Debug.AssertInterpolatedStringHandler V_2,
                Boolean V_3)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloc.0
  IL_0003:  ldloca.s   V_2
  IL_0005:  ldc.i4.6
  IL_0006:  ldc.i4.1
  IL_0007:  ldloc.0
  IL_0008:  ldloca.s   V_3
  IL_000a:  call       ""Sub System.Diagnostics.Debug.AssertInterpolatedStringHandler..ctor(Integer, Integer, Boolean, ByRef Boolean)""
  IL_000f:  ldloc.3
  IL_0010:  brfalse.s  IL_0029
  IL_0012:  ldloca.s   V_2
  IL_0014:  ldstr      ""value ""
  IL_0019:  call       ""Sub System.Diagnostics.Debug.AssertInterpolatedStringHandler.AppendLiteral(String)""
  IL_001e:  ldloca.s   V_2
  IL_0020:  ldarg.1
  IL_0021:  call       ""Sub System.Diagnostics.Debug.AssertInterpolatedStringHandler.AppendFormatted(Of Integer)(Integer)""
  IL_0026:  ldc.i4.1
  IL_0027:  br.s       IL_002a
  IL_0029:  ldc.i4.0
  IL_002a:  pop
  IL_002b:  ldloc.2
  IL_002c:  stloc.1
  IL_002d:  ldloca.s   V_1
  IL_002f:  call       ""Sub System.Diagnostics.Debug.Assert(Boolean, ByRef System.Diagnostics.Debug.AssertInterpolatedStringHandler)""
  IL_0034:  ret
}")
        End Sub

        <Fact>
        Public Sub HandlerArgument_StringBuilderAppendReadsTheReceiver()
            Dim compilation = CreateCompilation("
Imports System.Text

Module Program
    Function Build(count As Integer) As String
        Dim builder As New StringBuilder()
        builder.Append($""{count} items"")
        Return builder.ToString()
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.FailsPEVerify).VerifyDiagnostics().VerifyIL("Program.Build", "
{
  // Code size       54 (0x36)
  .maxstack  6
  .locals init (System.Text.StringBuilder V_0,
                System.Text.StringBuilder.AppendInterpolatedStringHandler V_1,
                System.Text.StringBuilder.AppendInterpolatedStringHandler V_2)
  IL_0000:  newobj     ""Sub System.Text.StringBuilder..ctor()""
  IL_0005:  dup
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  ldloca.s   V_2
  IL_000a:  ldc.i4.6
  IL_000b:  ldc.i4.1
  IL_000c:  ldloc.0
  IL_000d:  call       ""Sub System.Text.StringBuilder.AppendInterpolatedStringHandler..ctor(Integer, Integer, System.Text.StringBuilder)""
  IL_0012:  ldloca.s   V_2
  IL_0014:  ldarg.0
  IL_0015:  call       ""Sub System.Text.StringBuilder.AppendInterpolatedStringHandler.AppendFormatted(Of Integer)(Integer)""
  IL_001a:  ldloca.s   V_2
  IL_001c:  ldstr      "" items""
  IL_0021:  call       ""Sub System.Text.StringBuilder.AppendInterpolatedStringHandler.AppendLiteral(String)""
  IL_0026:  ldloc.2
  IL_0027:  stloc.1
  IL_0028:  ldloca.s   V_1
  IL_002a:  callvirt   ""Function System.Text.StringBuilder.Append(ByRef System.Text.StringBuilder.AppendInterpolatedStringHandler) As System.Text.StringBuilder""
  IL_002f:  pop
  IL_0030:  callvirt   ""Function System.Text.StringBuilder.ToString() As String""
  IL_0035:  ret
}")
        End Sub

        <Fact>
        Public Sub Handler_DefaultInterpolatedStringHandler()
            Dim compilation = CreateCompilation("
Imports System.Runtime.CompilerServices

Module Program
    Function Describe(count As Integer) As String
        Dim handler As DefaultInterpolatedStringHandler = $""{count} items""
        Return handler.ToStringAndClear()
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.FailsPEVerify).VerifyDiagnostics().VerifyIL("Program.Describe", "
{
  // Code size       39 (0x27)
  .maxstack  3
  .locals init (System.Runtime.CompilerServices.DefaultInterpolatedStringHandler V_0, //handler
                System.Runtime.CompilerServices.DefaultInterpolatedStringHandler V_1)
  IL_0000:  ldloca.s   V_1
  IL_0002:  ldc.i4.6
  IL_0003:  ldc.i4.1
  IL_0004:  call       ""Sub System.Runtime.CompilerServices.DefaultInterpolatedStringHandler..ctor(Integer, Integer)""
  IL_0009:  ldloca.s   V_1
  IL_000b:  ldarg.0
  IL_000c:  call       ""Sub System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendFormatted(Of Integer)(Integer)""
  IL_0011:  ldloca.s   V_1
  IL_0013:  ldstr      "" items""
  IL_0018:  call       ""Sub System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.AppendLiteral(String)""
  IL_001d:  ldloc.1
  IL_001e:  stloc.0
  IL_001f:  ldloca.s   V_0
  IL_0021:  call       ""Function System.Runtime.CompilerServices.DefaultInterpolatedStringHandler.ToStringAndClear() As String""
  IL_0026:  ret
}")
        End Sub
    End Class
End Namespace
