' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class SharedInterfacePropertyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const InterfaceSource As String = "
Public Interface IHasZero(Of T As IHasZero(Of T))
    Shared MustOverride ReadOnly Property Zero As T
End Interface

Public Interface ILabelled
    Shared Overridable ReadOnly Property Label As String
        Get
            Return ""none""
        End Get
    End Property

    Shared Overridable Property Scale As Integer
        Get
            Return 1
        End Get
        Set(value As Integer)
            System.Console.Write(""<set "" & value & "">"")
        End Set
    End Property
End Interface
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedAbstractAndVirtualProperties()
            Dim compilation = CreateCompilation({InterfaceSource, "
Imports System

Structure Money
    Implements IHasZero(Of Money), ILabelled
    Public Amount As Decimal

    Public Shared ReadOnly Property Zero As Money Implements IHasZero(Of Money).Zero
        Get
            Return New Money With {.Amount = 0.5D}
        End Get
    End Property
End Structure

Class Named
    Implements ILabelled

    Public Shared ReadOnly Property Label As String Implements ILabelled.Label
        Get
            Return ""named""
        End Get
    End Property
End Class

Module Program
    Function ZeroOf(Of T As IHasZero(Of T))() As T
        Return T.Zero
    End Function

    Function LabelOf(Of T As ILabelled)() As String
        Return T.Label
    End Function

    Sub Rescale(Of T As ILabelled)()
        T.Scale = T.Scale + 4
    End Sub

    Sub Main()
        Console.Write(ZeroOf(Of Money)().Amount & "" "" & LabelOf(Of Money)() & "" "" & LabelOf(Of Named)() & "" "")
        Rescale(Of Named)()
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="0.5 none named <set 5>", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CSharpReadsSharedInterfaceProperties()
            Dim library = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public struct Meters : IHasZero<Meters>, ILabelled
{
    public static Meters Zero => default;
}

public static class Use
{
    public static T ZeroOf<T>() where T : IHasZero<T> => T.Zero;
    public static string LabelOf<T>() where T : ILabelled => T.Label;
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim zero = DirectCast(csCompilation.GetTypeByMetadataName("IHasZero`1").GetMembers("Zero").Single(), IPropertySymbol)
            Assert.True(zero.IsStatic)
            Assert.True(zero.IsAbstract)
            Dim label = DirectCast(csCompilation.GetTypeByMetadataName("ILabelled").GetMembers("Label").Single(), IPropertySymbol)
            Assert.True(label.IsStatic)
            Assert.True(label.IsVirtual)
        End Sub

        <Fact>
        Public Sub SharedInterfacePropertyErrors()
            Dim compilation = CreateCompilation({InterfaceSource, "
Interface IBad
    Shared Overridable ReadOnly Property NoBody As Integer
End Interface

Structure Missing
    Implements IHasZero(Of Missing)
End Structure

Module Program
    Sub Main()
        Dim s = ILabelled.Label
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30273: 'Overridable' is not valid on an interface property declaration.
    Shared Overridable ReadOnly Property NoBody As Integer
           ~~~~~~~~~~~
BC37315: Structure 'Missing' cannot implement interface 'IHasZero(Of Missing)' because it contains shared abstract or virtual 'ReadOnly Property Zero As Missing'.
    Implements IHasZero(Of Missing)
               ~~~~~~~~~~~~~~~~~~~~
BC37314: A shared abstract or virtual interface member cannot be accessed.
        Dim s = ILabelled.Label
                ~~~~~~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub SharedInterfacePropertiesNeedVbNext()
            Dim compilation = CreateCompilation("
Interface IHasZero(Of T As IHasZero(Of T))
    Shared MustOverride ReadOnly Property Zero As T
End Interface
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30273: 'Shared' is not valid on an interface property declaration.
    Shared MustOverride ReadOnly Property Zero As T
    ~~~~~~
BC30502: 'Shared' cannot be combined with 'MustOverride' on a property declaration.
    Shared MustOverride ReadOnly Property Zero As T
           ~~~~~~~~~~~~
</expected>)
        End Sub

    End Class

End Namespace
