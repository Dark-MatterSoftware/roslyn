' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class ReadOnlyByRefOperatorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const VecSource As String = "
Public Structure Vec
    Public X As Double

    Public Sub New(x As Double)
        Me.X = x
    End Sub

    Public Shared Operator +(ByRef ReadOnly a As Vec, ByRef ReadOnly b As Vec) As Vec
        Return New Vec(a.X + b.X)
    End Operator

    Public Shared Operator -(ByRef ReadOnly a As Vec) As Vec
        Return New Vec(-a.X)
    End Operator

    Public Shared Operator *(ByRef ReadOnly a As Vec, factor As Double) As Vec
        Return New Vec(a.X * factor)
    End Operator

    Public Shared Operator =(ByRef ReadOnly a As Vec, ByRef ReadOnly b As Vec) As Boolean
        Return a.X = b.X
    End Operator

    Public Shared Operator <>(ByRef ReadOnly a As Vec, ByRef ReadOnly b As Vec) As Boolean
        Return a.X <> b.X
    End Operator
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub OperatorTakesByRefReadOnlyOperands()
            Dim compilation = CreateCompilation({VecSource, "
Imports System

Class Holder
    Public ReadOnly Origin As New Vec(1)
End Class

Module Program
    Function Make(x As Double) As Vec
        Return New Vec(x)
    End Function

    Sub Main()
        Dim a As New Vec(2)
        Dim b As New Vec(3)
        Dim h As New Holder()
        Dim c = a + b
        Console.Write(c.X & "" "" & (-a).X & "" "" & (a * 4).X & "" "" & (Make(5) + h.Origin).X & "" "" & (a = b) & "" "" & (a <> b))
        a += b
        Console.Write("" "" & a.X)
        Dim yes As New Flag(True)
        Dim no As New Flag(False)
        Console.Write("" "" & (yes AndAlso no).Value & "" "" & (yes AndAlso yes).Value & "" "" & Flag.AndCalls)
    End Sub
End Module

Structure Flag
    Public Shared AndCalls As Integer
    Public Value As Boolean

    Public Sub New(value As Boolean)
        Me.Value = value
    End Sub

    Public Shared Operator And(ByRef ReadOnly a As Flag, ByRef ReadOnly b As Flag) As Flag
        AndCalls += 1
        Return New Flag(a.Value And b.Value)
    End Operator

    Public Shared Operator IsTrue(a As Flag) As Boolean
        Return a.Value
    End Operator

    Public Shared Operator IsFalse(a As Flag) As Boolean
        Return Not a.Value
    End Operator
End Structure
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5 -2 8 6 False True 5 False True 2", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub OperatorsFromMetadata()
            Dim vbLibrary = CreateCompilation(VecSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            vbLibrary.AssertNoDiagnostics()

            Dim csLibrary = CreateCSharpCompilation("
public struct Money
{
    public decimal Amount;
    public Money(decimal amount) { Amount = amount; }
    public static Money operator +(in Money a, in Money b) => new Money(a.Amount + b.Amount);
}

public static class UseVec
{
    public static double Sum(Vec a, Vec b) => (a + b).X;
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {vbLibrary.EmitToImageReference()})).EmitToImageReference()

            Dim compilation = CreateCompilation("
Imports System

Module Program
    Sub Main()
        Dim m = New Money(2) + New Money(3)
        Dim v = New Vec(1) + New Vec(2)
        Console.Write(m.Amount & "" "" & v.X & "" "" & UseVec.Sum(New Vec(4), New Vec(5)))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={vbLibrary.EmitToImageReference(), csLibrary}, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5 3 9", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub OperatorOperandErrors()
            Dim compilation = CreateCompilation("
Public Structure Vec
    Public X As Double

    Public Shared Operator +(ByRef a As Vec, ByRef ReadOnly b As Vec) As Vec
        Return a
    End Operator

    Public Shared Operator -(ByRef ReadOnly a As Vec, b As Vec) As Vec
        a = b
        Return a
    End Operator

    Public Shared Widening Operator CType(ByRef ReadOnly a As Vec) As Double
        Return a.X
    End Operator

    Public Shared Operator IsTrue(ByRef ReadOnly a As Vec) As Boolean
        Return True
    End Operator

    Public Shared Operator IsFalse(a As Vec) As Boolean
        Return False
    End Operator

    Public Shared Narrowing Operator CType(ByRef value As Vec) As Integer
        Return 0
    End Operator
End Structure
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionInfer(True).WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30651: operator parameters cannot be declared 'ByRef'.
    Public Shared Operator +(ByRef a As Vec, ByRef ReadOnly b As Vec) As Vec
                             ~~~~~
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        a = b
        ~
BC30651: operator parameters cannot be declared 'ByRef'.
    Public Shared Narrowing Operator CType(ByRef value As Vec) As Integer
                                           ~~~~~
</expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ConversionsTruthOperatorsAndLifting()
            Dim compilation = CreateCompilation({VecSource, "
Imports System

Structure Flag
    Public Value As Boolean

    Public Shared Widening Operator CType(ByRef ReadOnly value As Boolean) As Flag
        Return New Flag With {.Value = value}
    End Operator

    Public Shared Narrowing Operator CType(ByRef ReadOnly f As Flag) As Integer
        Return If(f.Value, 1, 0)
    End Operator

    Public Shared Operator IsTrue(ByRef ReadOnly f As Flag) As Boolean
        Return f.Value
    End Operator

    Public Shared Operator IsFalse(ByRef ReadOnly f As Flag) As Boolean
        Return Not f.Value
    End Operator

    Public Shared Operator And(ByRef ReadOnly a As Flag, ByRef ReadOnly b As Flag) As Flag
        Return New Flag With {.Value = a.Value And b.Value}
    End Operator
End Structure

Class Holder
    Public ReadOnly Enabled As Flag = True
End Class

Module Program
    Sub Main()
        Dim yes As Flag = True
        Dim no As Flag = False
        Dim h As New Holder()
        Console.Write(CInt(yes) & "" "" & CInt(h.Enabled) & "" "")
        If yes Then Console.Write(""if "")
        If no AndAlso yes Then Console.Write(""wrong "") Else Console.Write(""else "")
        Console.Write((yes AndAlso h.Enabled).Value & "" "")

        Dim a As Vec? = New Vec(2)
        Dim b As Vec? = New Vec(3)
        Dim c As Vec? = a + b
        Dim d As Vec? = a + Nothing
        Console.Write(c.Value.X & "" "" & d.HasValue & "" "" & (a = b).HasValue)
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True).WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1 1 if else True 5 False True", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CSharpInConversionFromMetadata()
            Dim csLibrary = CreateCSharpCompilation("
public struct Celsius
{
    public double Degrees;
    public static implicit operator double(in Celsius c) => c.Degrees;
    public static explicit operator Celsius(in double d) => new Celsius { Degrees = d };
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()

            Dim source = "
Imports System

Module Program
    Sub Main()
        Dim c = CType(21.5, Celsius)
        Dim d As Double = c
        Console.Write(d)
    End Sub
End Module
"
            Dim compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, references:={csLibrary}, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)
            CompileAndVerify(compilation, expectedOutput:="21.5", verify:=Verification.Skipped).VerifyDiagnostics()

            Dim compilation17 = CreateCompilation(source, targetFramework:=TargetFramework.Net90, references:={csLibrary}, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=TestOptions.Regular17_13)
            compilation17.AssertTheseDiagnostics(
<expected>
BC30311: Value of type 'Double' cannot be converted to 'Celsius'.
        Dim c = CType(21.5, Celsius)
                      ~~~~
BC30311: Value of type 'Celsius' cannot be converted to 'Double'.
        Dim d As Double = c
                          ~
</expected>)
        End Sub

        <Fact>
        Public Sub VisualBasic17_13DoesNotUseByRefReadOnlyOperators()
            Dim vbLibrary = CreateCompilation(VecSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            Dim compilation = CreateCompilation("
Module Program
    Sub Main()
        Dim a As New Vec(1)
        Dim c = a + a
        Dim d = Vec.op_Addition(a, a)
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={vbLibrary.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30452: Operator '+' is not defined for types 'Vec' and 'Vec'.
        Dim c = a + a
                ~~~~~
</expected>)
        End Sub

    End Class

End Namespace
