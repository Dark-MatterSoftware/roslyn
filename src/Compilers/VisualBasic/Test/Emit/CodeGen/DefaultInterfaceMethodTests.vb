' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class DefaultInterfaceMethodTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub InterfaceMethodBody_ClassInheritsIt()
            Dim compilation = CreateCompilation("
Interface I
    Function M() As Integer
        Return 1
    End Function
End Interface

Class C
    Implements I
End Class

Module Program
    Sub Main()
        Dim value As I = New C()
        System.Console.Write(value.M())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()

            Dim interfaceMethod = compilation.GetMember(Of MethodSymbol)("I.M")
            Assert.False(interfaceMethod.IsMustOverride)
            Assert.True(interfaceMethod.IsOverridable)
            Assert.Empty(compilation.GetMember(Of NamedTypeSymbol)("C").GetMembers("M"))

            CompileAndVerify(compilation, verify:=Verification.Skipped).
                VerifyIL("I.M",
                <![CDATA[
{
  // Code size        2 (0x2)
  .maxstack  1
  IL_0000:  ldc.i4.1
  IL_0001:  ret
}
]]>)
        End Sub

        <Fact>
        Public Sub InterfaceMethodWithoutBody_StillRequiresImplementation()
            Dim compilation = CreateCompilation("
Interface I
    Function M() As Integer
    Sub N()
End Interface

Class C
    Implements I
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_UnimplementedMember3, "I").WithArguments("Class", "C", "Function M() As Integer", "I").WithLocation(8, 16),
                Diagnostic(ERRID.ERR_UnimplementedMember3, "I").WithArguments("Class", "C", "Sub N()", "I").WithLocation(8, 16))
        End Sub

        <Fact>
        Public Sub InterfaceMethodBody_ClassCanSupplyItsOwn()
            Dim compilation = CreateCompilation("
Interface I
    Function M() As Integer
        Return 1
    End Function
End Interface

Class C
    Implements I

    Public Function M() As Integer Implements I.M
        Return 2
    End Function
End Class

Module Program
    Sub Main()
        Dim value As I = New C()
        System.Console.Write(value.M())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim classMethod = compilation.GetMember(Of MethodSymbol)("C.M")
            Assert.Equal(compilation.GetMember(Of MethodSymbol)("I.M"), classMethod.ExplicitInterfaceImplementations.Single())
            CompileAndVerify(compilation, verify:=Verification.Skipped).
                VerifyIL("C.M",
                <![CDATA[
{
  // Code size        2 (0x2)
  .maxstack  1
  IL_0000:  ldc.i4.2
  IL_0001:  ret
}
]]>)
        End Sub

        <Fact>
        Public Sub MostSpecificInterfaceMethodBody_IsChosen()
            Dim compilation = CreateCompilation("
Interface IA
    Function M() As Integer
        Return 1
    End Function
End Interface

Interface IB
    Inherits IA
    Function M() As Integer
        Return 2
    End Function
End Interface

Class C
    Implements IB
End Class

Module Program
    Sub Main()
        Dim value As IA = New C()
        System.Console.Write(value.M())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()

            Dim classType = compilation.GetMember(Of NamedTypeSymbol)("C")
            Dim baseMethod = compilation.GetMember(Of MethodSymbol)("IA.M")
            Dim derivedMethod = compilation.GetMember(Of MethodSymbol)("IB.M")
            Assert.Equal(derivedMethod, classType.FindImplementationForInterfaceMember(baseMethod))
            Assert.Equal(derivedMethod, classType.FindImplementationForInterfaceMember(derivedMethod))
        End Sub

        <Fact>
        Public Sub DerivedInterfaceMethodBody_CoversAbstractBase()
            Dim compilation = CreateCompilation("
Interface IA
    Function M() As Integer
End Interface

Interface IB
    Inherits IA
    Function M() As Integer
        Return 2
    End Function
End Interface

Class C
    Implements IB
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim classType = compilation.GetMember(Of NamedTypeSymbol)("C")
            Assert.Equal(compilation.GetMember(Of MethodSymbol)("IB.M"), classType.FindImplementationForInterfaceMember(compilation.GetMember(Of MethodSymbol)("IA.M")))
        End Sub

        <Fact>
        Public Sub EquallySpecificInterfaceMethodBodies_StillRequireImplementation()
            Dim compilation = CreateCompilation("
Interface IA
    Function M() As Integer
End Interface

Interface IB
    Inherits IA
    Function M() As Integer
        Return 1
    End Function
End Interface

Interface IC
    Inherits IA
    Function M() As Integer
        Return 2
    End Function
End Interface

Class C
    Implements IB, IC
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_UnimplementedMember3, "IB").WithArguments("Class", "C", "Function M() As Integer", "IA").WithLocation(21, 16))
        End Sub

        <Fact>
        Public Sub InterfaceMethodBody_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Interface I
    Function M() As Integer
        Return 1
    End Function
End Interface
", options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_InvInsideInterface, "Return 1").WithLocation(4, 9),
                Diagnostic(ERRID.ERR_InvInsideInterface, "End Function").WithLocation(5, 5))
        End Sub

        <Fact>
        Public Sub InterfaceSharedMustOverride_ClassSuppliesIt()
            Dim compilation = CreateCompilation("
Interface IName
    Shared MustOverride Function GetName() As String
End Interface

Class Person
    Implements IName

    Public Shared Function GetName() As String Implements IName.GetName
        Return ""ok""
    End Function
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()

            Dim method = DirectCast(compilation.GlobalNamespace.GetTypeMembers("IName").Single().GetMembers("GetName").Single(), MethodSymbol)
            Assert.True(method.IsShared)
            Assert.True(method.IsMustOverride)
        End Sub

        <Fact>
        Public Sub ConstrainedTypeParameter_CallsSharedInterfaceMethod()
            Dim compilation = CreateCompilation("
Interface IName
    Shared MustOverride Function GetName() As String
End Interface

Class Person
    Implements IName

    Public Shared Function GetName() As String Implements IName.GetName
        Return ""ok""
    End Function
End Class

Module Program
    Public Function Show(Of T As IName)() As String
        Return T.GetName()
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()

            CompileAndVerify(compilation, verify:=Verification.Skipped).
                VerifyIL("Program.Show",
                <![CDATA[
{
  // Code size       12 (0xc)
  .maxstack  1
  IL_0000:  constrained. "T"
  IL_0006:  call       "Function IName.GetName() As String"
  IL_000b:  ret
}
]]>)
        End Sub

        <Fact>
        Public Sub InterfaceSharedMustOverride_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Interface IName
    Shared MustOverride Function GetName() As String
End Interface
", options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadFlagsOnSharedMeth1, "MustOverride").WithArguments("MustOverride").WithLocation(3, 12),
                Diagnostic(ERRID.ERR_BadInterfaceMethodFlags1, "Shared").WithArguments("Shared").WithLocation(3, 5))
        End Sub

    End Class

End Namespace
