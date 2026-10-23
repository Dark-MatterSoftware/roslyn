' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class InlineArrayTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const BufferSource As String = "
Imports System.Runtime.CompilerServices

<InlineArray(4)>
Structure Buffer4
    Private _element As Integer
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ElementsSpansAndForEach()
            Dim compilation = CreateCompilation({BufferSource, "
Imports System

Class Holder
    Public Buffer As Buffer4
    Public ReadOnly Frozen As Buffer4

    Sub New()
        Frozen(1) = 7
    End Sub
End Class

Module Program
    Sub Main()
        Dim b As Buffer4
        b(0) = 1
        b(3) = 4
        b(1) += 5
        Bump(b(2))
        Dim i = 3
        Console.Write($""{b(0)} {b(1)} {b(2)} {b(i)}"")

        Dim s As Span(Of Integer) = b
        s(1) = 9
        Console.Write($"" {b(1)} {s.Length}"")

        Dim total = 0
        For Each x In b
            total += x
        Next
        Console.Write($"" {total}"")

        Dim h As New Holder()
        h.Buffer(2) = 6
        Dim r As ReadOnlySpan(Of Integer) = h.Frozen
        Console.Write($"" {h.Buffer(2)} {r(1)} {h.Frozen(1)} {Sum(b)}"")

        Try
            Dim j = 4
            Console.Write(b(j))
        Catch ex As IndexOutOfRangeException
            Console.Write("" range"")
        End Try
    End Sub

    Sub Bump(ByRef value As Integer)
        value += 10
    End Sub

    Function Sum(values As ReadOnlySpan(Of Integer)) As Integer
        Dim result = 0
        For Each v In values
            result += v
        Next
        Return result
    End Function
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1 5 10 4 9 4 24 6 7 7 24 range", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ElementsOfAnInlineArrayValue()
            Dim compilation = CreateCompilation({BufferSource, "
Imports System

Module Program
    Function Make(start As Integer) As Buffer4
        Dim b As Buffer4
        For i = 0 To 3
            b(i) = start + i
        Next
        Return b
    End Function

    Sub Main()
        Console.Write(Make(10)(2))

        Dim total = 0
        For Each v In Make(1)
            total += v
        Next
        Console.Write("" "" & total)

        Console.Write("" "" & Make(20)(Make(0)(1)))

        Dim sum = 0
        For Each a In Make(100)
            For Each c In Make(1)
                sum += c
            Next
        Next
        Console.Write("" "" & sum)
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="12 10 21 40", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SpanConversionEmitsUnsafeAsAndCreateSpan()
            Dim compilation = CreateCompilation({BufferSource, "
Imports System

Module Program
    Function First(ByRef b As Buffer4) As Integer
        Dim s As Span(Of Integer) = b
        Return s(0)
    End Function
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.Skipped).VerifyIL("Program.First", "
{
  // Code size       23 (0x17)
  .maxstack  2
  .locals init (System.Span(Of Integer) V_0) //s
  IL_0000:  ldarg.0
  IL_0001:  call       ""ByRef Function System.Runtime.CompilerServices.Unsafe.As(Of Buffer4, Integer)(ByRef Buffer4) As Integer""
  IL_0006:  ldc.i4.4
  IL_0007:  call       ""Function System.Runtime.InteropServices.MemoryMarshal.CreateSpan(Of Integer)(ByRef Integer, Integer) As System.Span(Of Integer)""
  IL_000c:  stloc.0
  IL_000d:  ldloca.s   V_0
  IL_000f:  ldc.i4.0
  IL_0010:  call       ""ByRef Function System.Span(Of Integer).get_Item(Integer) As Integer""
  IL_0015:  ldind.i4
  IL_0016:  ret
}
")
        End Sub

        <Fact>
        Public Sub InlineArrayErrors()
            Dim compilation = CreateCompilation({BufferSource, "
Imports System

Module Program
    Function Make() As Buffer4
        Return New Buffer4()
    End Function

    Sub Test()
        Dim b As Buffer4
        b(4) = 1
        b(-1) = 1
        Make()(0) = 1
        Dim s As Span(Of Long) = b
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37417: The index must be at least 0 and less than 4, the length of the inline array.
        b(4) = 1
          ~
BC37417: The index must be at least 0 and less than 4, the length of the inline array.
        b(-1) = 1
          ~~
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        Make()(0) = 1
        ~~~~~~~~~
BC30311: Value of type 'Buffer4' cannot be converted to 'Span(Of Long)'.
        Dim s As Span(Of Long) = b
                                 ~
</expected>)
        End Sub

        <Fact>
        Public Sub InlineArrayNeedsVbNext()
            Dim compilation = CreateCompilation({BufferSource, "
Module Program
    Sub Test()
        Dim b As Buffer4
        b(0) = 1
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(
<expected>
BC30690: Structure 'Buffer4' cannot be indexed because it has no default property.
        b(0) = 1
        ~
</expected>)
        End Sub

    End Class

End Namespace
