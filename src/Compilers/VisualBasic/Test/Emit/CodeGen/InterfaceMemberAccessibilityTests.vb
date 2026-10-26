' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class InterfaceMemberAccessibilityTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const InterfaceSource As String = "
Public Interface IShape
    Private Function Secret() As Integer
        Return 40
    End Function

    Friend Function Inner() As Integer
        Return 1
    End Function

    Protected Function Guarded() As Integer
        Return 1
    End Function

    Private ReadOnly Property Hidden As Integer
        Get
            Return Secret() + Inner() + Guarded()
        End Get
    End Property

    Function Area() As Integer
        Return Hidden
    End Function
End Interface

Public Interface ISquare
    Inherits IShape

    Function Twice() As Integer
        Return Guarded() * 2
    End Function
End Interface

Public Class Square
    Implements ISquare
End Class
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CallsInterfaceMembersWithAccessibility()
            Dim compilation = CreateCompilation({InterfaceSource, "
Imports System

Module Program
    Sub Main()
        Dim s As ISquare = New Square()
        Console.Write(s.Area() & "" "" & s.Twice())
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="42 2", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CSharpReadsInterfaceMemberAccessibility()
            Dim library = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("",
                compilationOptions:=New CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.All),
                referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            Dim shape = csCompilation.GetTypeByMetadataName("IShape")
            Dim secret = DirectCast(shape.GetMembers("Secret").Single(), IMethodSymbol)
            Assert.Equal(Accessibility.Private, secret.DeclaredAccessibility)
            Assert.False(secret.IsVirtual)
            Assert.False(secret.IsAbstract)
            Assert.Equal(Accessibility.Internal, shape.GetMembers("Inner").Single().DeclaredAccessibility)
            Dim guarded = DirectCast(shape.GetMembers("Guarded").Single(), IMethodSymbol)
            Assert.Equal(Accessibility.Protected, guarded.DeclaredAccessibility)
            Assert.True(guarded.IsVirtual)
        End Sub

        <Fact>
        Public Sub InterfaceMemberAccessibilityErrors()
            Dim compilation = CreateCompilation({InterfaceSource, "
Interface IBad
    Private Function NoBody() As Integer
    Friend ReadOnly Property NoBodyEither As Integer
End Interface

Class Outside
    Implements IShape

    Function Secret() As Integer Implements IShape.Secret
        Return 0
    End Function

    Sub Use(s As IShape)
        Dim a = s.Secret()
        Dim b = s.Guarded()
    End Sub
End Class
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30270: 'Private' is not valid on an interface method declaration.
    Private Function NoBody() As Integer
    ~~~~~~~
BC30273: 'Friend' is not valid on an interface property declaration.
    Friend ReadOnly Property NoBodyEither As Integer
    ~~~~~~
BC30401: 'Secret' cannot implement 'Secret' because there is no matching function on interface 'IShape'.
    Function Secret() As Integer Implements IShape.Secret
                                            ~~~~~~~~~~~~~
BC30390: 'IShape.Function Secret() As Integer' is not accessible in this context because it is 'Private'.
        Dim a = s.Secret()
                ~~~~~~~~
BC30390: 'IShape.Function Guarded() As Integer' is not accessible in this context because it is 'Protected'.
        Dim b = s.Guarded()
                ~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub InterfaceMemberAccessibilityNeedsVbNext()
            Dim compilation = CreateCompilation("
Interface IShape
    Private Function Secret() As Integer
        Return 40
    End Function
End Interface
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30270: 'Private' is not valid on an interface method declaration.
    Private Function Secret() As Integer
    ~~~~~~~
BC30603: Statement cannot appear within an interface body.
        Return 40
        ~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Function
    ~~~~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
