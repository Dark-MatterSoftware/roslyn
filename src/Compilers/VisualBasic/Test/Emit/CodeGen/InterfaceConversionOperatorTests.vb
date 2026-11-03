' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class InterfaceConversionOperatorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const InterfaceSource As String = "
Public Interface IFromInteger(Of T As IFromInteger(Of T))
    Shared MustOverride Widening Operator CType(value As Integer) As T

    Shared Overridable Narrowing Operator CType(value As T) As Integer
        Return -1
    End Operator
End Interface

Public Structure Meters
    Implements IFromInteger(Of Meters)
    Public Value As Integer

    Public Shared Widening Operator CType(value As Integer) As Meters
        Return New Meters With {.Value = value}
    End Operator

    Public Shared Narrowing Operator CType(value As Meters) As Integer
        Return value.Value
    End Operator
End Structure

Public Structure Feet
    Implements IFromInteger(Of Feet)
    Public Value As Integer

    Public Shared Widening Operator CType(value As Integer) As Feet
        Return New Feet With {.Value = value * 3}
    End Operator
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ConversionsThroughTypeParameters()
            Dim compilation = CreateCompilation({InterfaceSource, "
Imports System

Module Program
    Function Make(Of T As IFromInteger(Of T))(n As Integer) As T
        Dim result As T = n
        Return result
    End Function

    Function Back(Of T As IFromInteger(Of T))(value As T) As Integer
        Return CType(value, Integer)
    End Function

    Sub Main()
        Dim m = Make(Of Meters)(5)
        Dim f = Make(Of Feet)(2)
        Console.Write(m.Value & "" "" & f.Value & "" "" & Back(m) & "" "" & Back(f))
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5 6 5 -1", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CSharpReadsInterfaceConversions()
            Dim library = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public static class Use
{
    public static T Make<T>(int n) where T : IFromInteger<T> => n;
    public static int Back<T>(T value) where T : IFromInteger<T> => (int)value;
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim implicitOp = DirectCast(csCompilation.GetTypeByMetadataName("IFromInteger`1").GetMembers("op_Implicit").Single(), IMethodSymbol)
            Assert.True(implicitOp.IsStatic)
            Assert.True(implicitOp.IsAbstract)
            Dim explicitOp = DirectCast(csCompilation.GetTypeByMetadataName("IFromInteger`1").GetMembers("op_Explicit").Single(), IMethodSymbol)
            Assert.True(explicitOp.IsVirtual)
        End Sub

        <Fact>
        Public Sub InterfaceConversionErrors()
            Dim compilation = CreateCompilation({InterfaceSource, "
Structure Missing
    Implements IFromInteger(Of Missing)
End Structure

Module Program
    Function Narrow(Of T As IFromInteger(Of T))(value As T) As Integer
        Return value
    End Function
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37315: Structure 'Missing' cannot implement interface 'IFromInteger(Of Missing)' because it contains shared abstract or virtual 'Widening Operator CType(value As Integer) As Missing'.
    Implements IFromInteger(Of Missing)
               ~~~~~~~~~~~~~~~~~~~~~~~~
BC30512: Option Strict On disallows implicit conversions from 'T' to 'Integer'.
        Return value
               ~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub InterfaceConversionFromAnUndeclaredName()
            Dim compilation = CreateCompilation({InterfaceSource, "
Module Program
    Function Make(Of T As IFromInteger(Of T))() As T
        Dim result As T = missing
        Return result
    End Function
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30451: 'missing' is not declared. It may be inaccessible due to its protection level.
        Dim result As T = missing
                          ~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub InterfaceConversionsInExpressionTrees()
            Dim compilation = CreateCompilation({InterfaceSource, "
Imports System
Imports System.Linq.Expressions

Module Program
    Function Back(Of T As IFromInteger(Of T))(value As T) As Expression(Of Func(Of Integer))
        Return Function() CType(value, Integer)
    End Function

    Function Make(Of T As IFromInteger(Of T))(n As Integer) As Expression(Of Func(Of T))
        Return Function() n
    End Function
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseEmitDiagnostics(
<expected>
BC36534: Expression cannot be converted into an expression tree.
        Return Function() CType(value, Integer)
                          ~~~~~~~~~~~~~~~~~~~~~
BC36534: Expression cannot be converted into an expression tree.
        Return Function() n
                          ~
</expected>)
        End Sub

        <Fact>
        Public Sub InterfaceConversionsNeedVbNext()
            Dim compilation = CreateCompilation("
Interface IFromInteger(Of T As IFromInteger(Of T))
    Shared MustOverride Widening Operator CType(value As Integer) As T
End Interface
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30603: Statement cannot appear within an interface body.
    Shared MustOverride Widening Operator CType(value As Integer) As T
    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC33013: Operators cannot be declared 'MustOverride'.
    Shared MustOverride Widening Operator CType(value As Integer) As T
           ~~~~~~~~~~~~
</expected>)
        End Sub

    End Class

End Namespace
