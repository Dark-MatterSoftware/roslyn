' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class SharedInterfaceOperatorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const InterfaceSource As String = "
Public Interface IAddable(Of T As IAddable(Of T))
    Shared MustOverride Operator +(left As T, right As T) As T

    Shared Overridable Operator -(value As T) As T
        Return value
    End Operator

    Shared MustOverride Operator =(left As T, right As T) As Boolean
    Shared MustOverride Operator <>(left As T, right As T) As Boolean
End Interface
"

        Private Const MoneySource As String = "
Public Structure Money
    Implements IAddable(Of Money)
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
End Structure

Public Structure Debt
    Implements IAddable(Of Debt)
    Public Amount As Decimal

    Public Shared Operator +(left As Debt, right As Debt) As Debt
        Return New Debt With {.Amount = left.Amount + right.Amount}
    End Operator

    Public Shared Operator -(value As Debt) As Debt
        Return New Debt With {.Amount = -value.Amount}
    End Operator

    Public Shared Operator =(left As Debt, right As Debt) As Boolean
        Return left.Amount = right.Amount
    End Operator

    Public Shared Operator <>(left As Debt, right As Debt) As Boolean
        Return left.Amount <> right.Amount
    End Operator
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedAbstractAndVirtualOperators()
            Dim compilation = CreateCompilation({InterfaceSource, MoneySource, "
Imports System

Module Program
    Function Sum(Of T As IAddable(Of T))(a As T, b As T) As T
        Return a + b
    End Function

    Function Negate(Of T As IAddable(Of T))(a As T) As T
        Return -a
    End Function

    Function Same(Of T As IAddable(Of T))(a As T, b As T) As Boolean
        Return a = b
    End Function

    Sub Main()
        Dim m As New Money With {.Amount = 2}
        Dim d As New Debt With {.Amount = 3}
        Console.Write(Sum(m, m).Amount & "" "" & Negate(m).Amount & "" "" & Negate(d).Amount & "" "" & Same(m, m) & "" "" & Same(d, Sum(d, d)))
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4 2 -3 True False", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CSharpUsesSharedInterfaceOperators()
            Dim library = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public struct Meters : IAddable<Meters>
{
    public double Value;
    public static Meters operator +(Meters a, Meters b) => new Meters { Value = a.Value + b.Value };
    public static bool operator ==(Meters a, Meters b) => a.Value == b.Value;
    public static bool operator !=(Meters a, Meters b) => a.Value != b.Value;
    public override bool Equals(object o) => o is Meters m && m == this;
    public override int GetHashCode() => Value.GetHashCode();
}

public static class Use
{
    public static T Sum<T>(T a, T b) where T : IAddable<T> => a + b;
    public static T Negate<T>(T a) where T : IAddable<T> => -a;
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim interfaceType = csCompilation.GetTypeByMetadataName("IAddable`1")
            Dim add = DirectCast(interfaceType.GetMembers("op_Addition").Single(), IMethodSymbol)
            Assert.True(add.IsStatic)
            Assert.True(add.IsAbstract)
            Dim negate = DirectCast(interfaceType.GetMembers("op_UnaryNegation").Single(), IMethodSymbol)
            Assert.True(negate.IsStatic)
            Assert.True(negate.IsVirtual)
        End Sub

        <Fact>
        Public Sub SharedInterfaceOperatorErrors()
            Dim compilation = CreateCompilation({InterfaceSource, "
Interface IBad(Of T As IBad(Of T))
    Shared Operator *(left As T, right As T) As T
    Shared MustOverride Widening Operator CType(value As T) As Integer
    Shared MustOverride Operator <(left As T, right As T) As Boolean
    Shared MustOverride Overridable Operator Not(value As T) As T
End Interface

Structure Missing
    Implements IAddable(Of Missing)

    Public Shared Operator =(left As Missing, right As Missing) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(left As Missing, right As Missing) As Boolean
        Return False
    End Operator
End Structure
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30603: Statement cannot appear within an interface body.
    Shared Operator *(left As T, right As T) As T
    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC33013: Operators cannot be declared 'MustOverride'.
    Shared MustOverride Widening Operator CType(value As T) As Integer
           ~~~~~~~~~~~~
BC33033: Matching '>' operator is required for 'Operator &lt;(left As T, right As T) As Boolean'.
    Shared MustOverride Operator &lt;(left As T, right As T) As Boolean
                                 ~
BC30177: Only one of 'NotOverridable', 'MustOverride', or 'Overridable' can be specified.
    Shared MustOverride Overridable Operator Not(value As T) As T
                        ~~~~~~~~~~~
BC37315: Structure 'Missing' cannot implement interface 'IAddable(Of Missing)' because it contains shared abstract or virtual 'Operator +(left As Missing, right As Missing) As Missing'.
    Implements IAddable(Of Missing)
               ~~~~~~~~~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub SharedInterfaceOperatorsNeedVbNext()
            Dim compilation = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30603: Statement cannot appear within an interface body.
    Shared MustOverride Operator +(left As T, right As T) As T
    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC33013: Operators cannot be declared 'MustOverride'.
    Shared MustOverride Operator +(left As T, right As T) As T
           ~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    Shared Overridable Operator -(value As T) As T
    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC33013: Operators cannot be declared 'Overridable'.
    Shared Overridable Operator -(value As T) As T
           ~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
        Return value
        ~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Operator
    ~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    Shared MustOverride Operator =(left As T, right As T) As Boolean
    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC33013: Operators cannot be declared 'MustOverride'.
    Shared MustOverride Operator =(left As T, right As T) As Boolean
           ~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    Shared MustOverride Operator &lt;&gt;(left As T, right As T) As Boolean
    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC33013: Operators cannot be declared 'MustOverride'.
    Shared MustOverride Operator &lt;&gt;(left As T, right As T) As Boolean
           ~~~~~~~~~~~~
</expected>)
        End Sub

    End Class

End Namespace
