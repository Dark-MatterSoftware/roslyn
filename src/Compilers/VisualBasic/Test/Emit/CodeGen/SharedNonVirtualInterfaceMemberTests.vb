' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class SharedNonVirtualInterfaceMemberTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const InterfaceSource As String = "
Public Interface IFactory
    Function Make() As String

    Shared Function Describe() As String
        Return ""factory v"" & Version
    End Function

    Shared Sub Log(message As String)
        System.Console.Write(""["" & message & ""] "")
    End Sub

    Shared ReadOnly Property Version As Integer
        Get
            Return 2
        End Get
    End Property
End Interface
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedMembersWithBodies()
            Dim compilation = CreateCompilation({InterfaceSource, "
Imports System

Class Widgets
    Implements IFactory

    Public Function Make() As String Implements IFactory.Make
        Return ""widget""
    End Function
End Class

Module Program
    Sub Main()
        IFactory.Log(""start"")
        Dim f As IFactory = New Widgets()
        Console.Write(IFactory.Describe() & "" "" & IFactory.Version & "" "" & f.Make())
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="[start] factory v2 2 widget", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CSharpReadsStaticInterfaceMembers()
            Dim library = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public static class Use
{
    public static string Call() => IFactory.Describe() + IFactory.Version;
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim describe = DirectCast(csCompilation.GetTypeByMetadataName("IFactory").GetMembers("Describe").Single(), IMethodSymbol)
            Assert.True(describe.IsStatic)
            Assert.False(describe.IsVirtual)
            Assert.False(describe.IsAbstract)
        End Sub

        <Fact>
        Public Sub SharedMemberErrors()
            Dim compilation = CreateCompilation({InterfaceSource, "
Class Supplies
    Implements IFactory

    Public Function Make() As String Implements IFactory.Make
        Return """"
    End Function

    Public Shared Function Describe() As String Implements IFactory.Describe
        Return """"
    End Function
End Class

Module Program
    Function Through(Of T As IFactory)() As String
        Return T.Describe()
    End Function
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30401: 'Describe' cannot implement 'Describe' because there is no matching function on interface 'IFactory'.
    Public Shared Function Describe() As String Implements IFactory.Describe
                                                           ~~~~~~~~~~~~~~~~~
BC32098: Type parameters cannot be used as qualifiers.
        Return T.Describe()
               ~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub SharedMembersWithBodiesNeedVbNext()
            Dim compilation = CreateCompilation("
Interface IFactory
    Shared Function Describe() As String
        Return """"
    End Function
End Interface
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30270: 'Shared' is not valid on an interface method declaration.
    Shared Function Describe() As String
    ~~~~~~
BC30603: Statement cannot appear within an interface body.
        Return ""
        ~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Function
    ~~~~~~~~~~~~
</expected>)
        End Sub

    End Class

End Namespace
