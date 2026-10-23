' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class GenericMathOperatorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const Numbers As String = "
Imports System
Imports System.Numerics

Module Program
    Function Add(Of T As INumber(Of T))(a As T, b As T) As T
        Return a + b
    End Function

    Function Sum(Of T As INumber(Of T))(values As T()) As T
        Dim total = T.Zero
        For Each value In values
            total = total + value
        Next
        Return total
    End Function

    Function Average(Of T As INumber(Of T))(a As T, b As T) As T
        Return (a + b) / T.CreateChecked(2)
    End Function

    Function Larger(Of T As IComparisonOperators(Of T, T, Boolean))(a As T, b As T) As T
        Return If(a > b, a, b)
    End Function

    Function Describe(Of T As INumber(Of T))(a As T, b As T) As String
        Return $""{a - b} {a * b} {a Mod b} {-a} {+a} {a = b} {a <> b} {a < b} {a <= b} {a >= b}""
    End Function

    Function Flags(Of T As IBinaryInteger(Of T))(a As T, b As T) As String
        Return $""{a And b} {a Or b} {a Xor b} {Not a} {a << 1} {a >> 1}""
    End Function

    Sub Main()
        Console.WriteLine($""{Add(2, 3)} {Add(1.5, 2.25)} {Sum({1, 2, 3, 4})} {Sum({0.5D, 0.25D})}"")
        Console.WriteLine($""{Average(3, 6)} {Average(3.0, 6.0)} {Larger(4, 9)} {Larger(""p""c, ""a""c)}"")
        Console.WriteLine(Describe(7, 3))
        Console.WriteLine(Flags(Of Byte)(12, 10))
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub OperatorsOfTheConstraintsRun()
            Dim compilation = CreateCompilation(Numbers, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="
5 3.75 10 0.75
4 4.5 9 p
4 21 1 -7 7 False True False False True
8 14 6 243 24 6
", verify:=Verification.Skipped).VerifyDiagnostics().
                VerifyIL("Program.Add(Of T)", <![CDATA[
{
  // Code size       14 (0xe)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. "T"
  IL_0008:  call       "Function System.Numerics.IAdditionOperators(Of T, T, T).op_CheckedAddition(T, T) As T"
  IL_000d:  ret
}
]]>)
        End Sub

        Private Const Accumulate As String = "
Imports System
Imports System.Numerics

Module Program
    Function Add(Of T As INumber(Of T))(a As T, b As T) As T
        Return a + b
    End Function

    Function SumAll(Of T As INumber(Of T))(values As T()) As T
        Dim total = T.Zero
        For Each value In values
            total += value
        Next
        Return total
    End Function

    Function Shrink(Of T As INumber(Of T))(value As T, by As T) As T
        Dim result = value
        result -= by
        result *= by
        result /= by
        Return -result
    End Function

    Function Negate(Of T As INumber(Of T))(value As T) As T
        Return -value
    End Function

    Sub Main()
        Console.Write($""{SumAll({1, 2, 3})} {Shrink(10, 2)} {Shrink(1.5, 0.5)}"")
        Try
            Console.Write("" "" & SumAll({Integer.MaxValue, 1}))
        Catch ex As OverflowException
            Console.Write("" overflow"")
        End Try
        Try
            Console.Write("" "" & SumAll(Of Byte)({200, 100}))
        Catch ex As OverflowException
            Console.Write("" overflow"")
        End Try
        Try
            Console.Write("" "" & Negate(Integer.MinValue))
        Catch ex As OverflowException
            Console.Write("" overflow"")
        End Try
    End Sub
End Module
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CompoundAssignmentAndCheckedOperators()
            Dim compilation = CreateCompilation(Accumulate, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="6 -8 -1 overflow overflow overflow", verify:=Verification.Skipped).VerifyDiagnostics().
                VerifyIL("Program.Add(Of T)", <![CDATA[
{
  // Code size       14 (0xe)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. "T"
  IL_0008:  call       "Function System.Numerics.IAdditionOperators(Of T, T, T).op_CheckedAddition(T, T) As T"
  IL_000d:  ret
}
]]>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CompoundAssignmentWithoutOverflowChecks()
            Dim compilation = CreateCompilation(Accumulate, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOverflowChecks(False), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="6 -8 -1 -2147483648 44 -2147483648", verify:=Verification.Skipped).VerifyDiagnostics().
                VerifyIL("Program.Add(Of T)", <![CDATA[
{
  // Code size       14 (0xe)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. "T"
  IL_0008:  call       "Function System.Numerics.IAdditionOperators(Of T, T, T).op_Addition(T, T) As T"
  IL_000d:  ret
}
]]>)
        End Sub

        Private Const MoneyType As String = "
Imports System
Imports System.Numerics

Structure Money
    Implements IAdditionOperators(Of Money, Money, Money), IAdditiveIdentity(Of Money, Money),
               IEqualityOperators(Of Money, Money, Boolean), IIncrementOperators(Of Money)

    Public Amount As Decimal

    Public Shared Operator +(left As Money, right As Money) As Money
        Return New Money With {.Amount = left.Amount + right.Amount}
    End Operator

    Public Shared Operator =(left As Money, right As Money) As Boolean
        Return left.Amount = right.Amount
    End Operator

    Public Shared Operator <>(left As Money, right As Money) As Boolean
        Return left.Amount <> right.Amount
    End Operator

    Public Shared ReadOnly Property AdditiveIdentity As Money Implements IAdditiveIdentity(Of Money, Money).AdditiveIdentity
        Get
            Return New Money()
        End Get
    End Property

    Public Shared Function op_Increment(value As Money) As Money Implements IIncrementOperators(Of Money).op_Increment
        Return New Money With {.Amount = value.Amount + 1}
    End Function
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub VbOperatorsImplementInterfaceOperators()
            Dim compilation = CreateCompilation(MoneyType & "
Module Program
    Function Sum(Of T As {IAdditionOperators(Of T, T, T), IAdditiveIdentity(Of T, T)})(values As T()) As T
        Dim total = T.AdditiveIdentity
        For Each value In values
            total += value
        Next
        Return total
    End Function

    Function Same(Of T As IEqualityOperators(Of T, T, Boolean))(a As T, b As T) As Boolean
        Return a = b
    End Function

    Function Bump(Of T As IIncrementOperators(Of T))(value As T) As T
        Return T.op_Increment(value)
    End Function

    Sub Main()
        Dim values = {New Money With {.Amount = 1.5D}, New Money With {.Amount = 2.25D}}
        Dim direct = values(0) + values(1)
        Console.Write($""{Sum(values).Amount} {direct.Amount} {Same(values(0), values(0))} {Same(values(0), values(1))} {Bump(values(0)).Amount}"")
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3.75 3.75 True False 2.5", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub OperatorWithAnotherSignatureDoesNotImplement()
            Dim compilation = CreateCompilation("
Imports System.Numerics

Structure Money
    Implements IAdditionOperators(Of Money, Money, Money)

    Public Amount As Decimal

    Public Shared Operator +(left As Money, right As Integer) As Money
        Return New Money With {.Amount = left.Amount + right}
    End Operator
End Structure
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37315: Structure 'Money' cannot implement interface 'IAdditionOperators(Of Money, Money, Money)' because it contains shared abstract or virtual 'Operator +(left As Money, right As Money) As Money'.
    Implements IAdditionOperators(Of Money, Money, Money)
               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub VbOperatorsDoNotImplementInterfaceOperatorsOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Numerics

Structure Money
    Implements IAdditionOperators(Of Money, Money, Money)

    Public Amount As Decimal

    Public Shared Operator +(left As Money, right As Money) As Money
        Return New Money With {.Amount = left.Amount + right.Amount}
    End Operator
End Structure
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37315: Structure 'Money' cannot implement interface 'IAdditionOperators(Of Money, Money, Money)' because it contains shared abstract or virtual 'Function op_CheckedAddition(left As Money, right As Money) As Money'.
    Implements IAdditionOperators(Of Money, Money, Money)
               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC37315: Structure 'Money' cannot implement interface 'IAdditionOperators(Of Money, Money, Money)' because it contains shared abstract or virtual 'Operator +(left As Money, right As Money) As Money'.
    Implements IAdditionOperators(Of Money, Money, Money)
               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub OperatorsOfTheConstraintsAreAnErrorOn17_13()
            Dim compilation = CreateCompilation("
Imports System.Numerics

Module Program
    Function Add(Of T As INumber(Of T))(a As T, b As T) As T
        Return a + b
    End Function

    Function Negate(Of T As INumber(Of T))(a As T) As T
        Return -a
    End Function
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30452: Operator '+' is not defined for types 'T' and 'T'.
        Return a + b
               ~~~~~
BC30487: Operator '-' is not defined for type 'T'.
        Return -a
               ~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub ConstraintsWithoutTheOperator()
            Dim compilation = CreateCompilation("
Imports System

Module Program
    Function Add(Of T As IComparable)(a As T, b As T) As T
        Return a + b
    End Function

    Function LateBound(Of T As IComparable)(a As T) As Object
        Dim o As Object = 1
        Return o + a
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.Off), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30452: Operator '+' is not defined for types 'T' and 'T'.
        Return a + b
               ~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
