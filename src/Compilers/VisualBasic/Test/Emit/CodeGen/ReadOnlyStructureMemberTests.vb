' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class ReadOnlyStructureMemberTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const PointSource As String = "
Public Structure Counter
    Public Value As Integer

    Public Sub Bump()
        Value += 1
    End Sub
End Structure

Public Structure Point2
    Public X As Integer
    Public Y As Integer
    Public Inner As Counter

    Public ReadOnly Function Length() As Integer
        Return X + Y
    End Function

    Public ReadOnly Sub Print()
        System.Console.Write(""("" & X & "","" & Y & "")"")
    End Sub

    Public ReadOnly Function Poke() As Integer
        Inner.Bump()
        Return Inner.Value
    End Function

    Public ReadOnly Function CallsMutator() As Integer
        Shift()
        Return X
    End Function

    Public Sub Shift()
        X += 100
    End Sub

    Public Property Scale As Integer
        ReadOnly Get
            Return X * 10
        End Get
        Set(value As Integer)
            X = value \ 10
        End Set
    End Property

    Public Property Name As String
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub ReadOnlyMembersDoNotChangeTheStructure()
            Dim compilation = CreateCompilation({PointSource, "
Imports System

Class Holder
    Public ReadOnly P As New Point2 With {.X = 1, .Y = 2}

    Public Function Use() As Integer
        Return P.Length() + P.Scale
    End Function
End Class

Module Program
    Sub Main()
        Dim p As New Point2 With {.X = 3, .Y = 4, .Name = ""p""}
        p.Print()
        Console.Write("" "" & p.Length() & "" "" & p.Poke() & "" "" & p.Inner.Value & "" "" & p.CallsMutator() & "" "" & p.X)
        p.Scale = 50
        Console.Write("" "" & p.Scale & "" "" & p.Name & "" "" & New Holder().Use())
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            Dim verifier = CompileAndVerify(compilation, expectedOutput:="(3,4) 7 0 0 3 3 50 p 13", verify:=Verification.Skipped)
            verifier.VerifyDiagnostics()
            verifier.VerifyIL("Holder.Use", "
{
  // Code size       24 (0x18)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldflda     ""Holder.P As Point2""
  IL_0006:  call       ""Function Point2.Length() As Integer""
  IL_000b:  ldarg.0
  IL_000c:  ldflda     ""Holder.P As Point2""
  IL_0011:  call       ""Function Point2.get_Scale() As Integer""
  IL_0016:  add.ovf
  IL_0017:  ret
}
")
        End Sub

        <Fact>
        Public Sub CSharpSeesReadOnlyMembers()
            Dim library = CreateCompilation(PointSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public static class Use
{
    public static int Call(in Point2 p) => p.Length() + p.Scale;
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim point = csCompilation.GetTypeByMetadataName("Point2")
            Assert.True(DirectCast(point.GetMembers("Length").Single(), IMethodSymbol).IsReadOnly)
            Assert.True(DirectCast(point.GetMembers("Print").Single(), IMethodSymbol).IsReadOnly)
            Assert.False(DirectCast(point.GetMembers("Shift").Single(), IMethodSymbol).IsReadOnly)
            Assert.True(DirectCast(point.GetMembers("get_Scale").Single(), IMethodSymbol).IsReadOnly)
            Assert.False(DirectCast(point.GetMembers("set_Scale").Single(), IMethodSymbol).IsReadOnly)
            Assert.True(DirectCast(point.GetMembers("get_Name").Single(), IMethodSymbol).IsReadOnly)

            Dim vbCompilation = CreateCompilation("
Class C
End Class
", targetFramework:=TargetFramework.Net90, references:={library.EmitToImageReference()}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            Dim vbPoint = vbCompilation.GetTypeByMetadataName("Point2")
            Assert.True(DirectCast(vbPoint.GetMembers("Length").Single(), IMethodSymbol).IsReadOnly)
            Assert.False(DirectCast(vbPoint.GetMembers("Shift").Single(), IMethodSymbol).IsReadOnly)
        End Sub

        <Fact>
        Public Sub ReadOnlyMemberErrors()
            Dim compilation = CreateCompilation("
Structure S
    Public X As Integer

    ReadOnly Function Assign() As Integer
        X = 1
        Return X
    End Function

    Shared ReadOnly Function NoMe() As Integer
        Return 0
    End Function

    Property P As Integer
        Get
            Return X
        End Get
        ReadOnly Set(value As Integer)
        End Set
    End Property
End Structure

Class C
    ReadOnly Function F() As Integer
        Return 0
    End Function

    Property Q As Integer
        ReadOnly Get
            Return 0
        End Get
        Set(value As Integer)
        End Set
    End Property
End Class
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30068: Expression is a value and therefore cannot be the target of an assignment.
        X = 1
        ~
BC30242: 'ReadOnly' is not valid on a method declaration.
    Shared ReadOnly Function NoMe() As Integer
           ~~~~~~~~
BC31099: Property accessors cannot be declared 'ReadOnly'.
        ReadOnly Set(value As Integer)
        ~~~~~~~~
BC30242: 'ReadOnly' is not valid on a method declaration.
    ReadOnly Function F() As Integer
    ~~~~~~~~
BC31099: Property accessors cannot be declared 'ReadOnly'.
        ReadOnly Get
        ~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub ReadOnlyMembersNeedIsReadOnlyAttribute()
            Dim compilation = CreateCompilationWithMscorlib40("
Structure S
    Public X As Integer

    ReadOnly Function Length() As Integer
        Return X
    End Function

    Property P As Integer
        ReadOnly Get
            Return X
        End Get
        Set(value As Integer)
        End Set
    End Property

    Property Auto As Integer
End Structure
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC35000: Requested operation is not available because the runtime library function 'System.Runtime.CompilerServices.IsReadOnlyAttribute..ctor' is not defined.
    ReadOnly Function Length() As Integer
    ~~~~~~~~
BC35000: Requested operation is not available because the runtime library function 'System.Runtime.CompilerServices.IsReadOnlyAttribute..ctor' is not defined.
        ReadOnly Get
        ~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub ReadOnlyMembersNeedVbNext()
            Dim compilation = CreateCompilation("
Structure S
    Public X As Integer

    ReadOnly Function Length() As Integer
        Return X
    End Function
End Structure
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30242: 'ReadOnly' is not valid on a method declaration.
    ReadOnly Function Length() As Integer
    ~~~~~~~~
</expected>)
        End Sub

    End Class

End Namespace
