' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class InstanceInterfacePropertyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const InterfaceSource As String = "
Public Interface IGreeter
    ReadOnly Property Name As String
    Property Item(index As Integer) As String

    ReadOnly Property Greeting As String
        Get
            Return ""hello "" & Name
        End Get
    End Property

    Property Volume As Integer
        Get
            Return 5
        End Get
        Set(value As Integer)
            System.Console.Write(""<volume "" & value & ""> "")
        End Set
    End Property
End Interface
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub DefaultInterfaceProperties()
            Dim compilation = CreateCompilation({InterfaceSource, "
Imports System

Class Greeter
    Implements IGreeter

    Public ReadOnly Property Name As String Implements IGreeter.Name
        Get
            Return ""ann""
        End Get
    End Property

    Public Property Item(index As Integer) As String Implements IGreeter.Item
        Get
            Return ""item"" & index
        End Get
        Set(value As String)
        End Set
    End Property
End Class

Class Loud
    Inherits Greeter
    Implements IGreeter

    Public ReadOnly Property Greeting As String Implements IGreeter.Greeting
        Get
            Return ""HELLO""
        End Get
    End Property
End Class

Module Program
    Sub Main()
        Dim g As IGreeter = New Greeter()
        Dim l As IGreeter = New Loud()
        g.Volume = 7
        Console.Write(g.Greeting & "" "" & l.Greeting & "" "" & g.Volume & "" "" & g.Item(3))
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="<volume 7> hello ann HELLO 5 item3", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CSharpReadsDefaultInterfaceProperties()
            Dim library = CreateCompilation(InterfaceSource.Replace("    Property Item(index As Integer) As String" & vbCrLf, ""), targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public class Named : IGreeter
{
    public string Name => ""cs"";
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim greeting = DirectCast(csCompilation.GetTypeByMetadataName("IGreeter").GetMembers("Greeting").Single(), IPropertySymbol)
            Assert.True(greeting.IsVirtual)
            Assert.False(greeting.IsAbstract)
            Dim name = DirectCast(csCompilation.GetTypeByMetadataName("IGreeter").GetMembers("Name").Single(), IPropertySymbol)
            Assert.True(name.IsAbstract)
        End Sub

        <Fact>
        Public Sub DefaultInterfacePropertyErrors()
            Dim compilation = CreateCompilation({InterfaceSource, "
Interface IBad
    Shared ReadOnly Property Count As Integer
        Get
            Return 0
        End Get
    End Property
End Interface

Class Missing
    Implements IGreeter
End Class
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30149: Class 'Missing' must implement 'Property Item(index As Integer) As String' for interface 'IGreeter'.
    Implements IGreeter
               ~~~~~~~~
BC30149: Class 'Missing' must implement 'ReadOnly Property Name As String' for interface 'IGreeter'.
    Implements IGreeter
               ~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub DefaultInterfacePropertiesNeedVbNext()
            Dim compilation = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.ERR_InvInsideInterface)
            Assert.DoesNotContain(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.ERR_AutoPropertyCantHaveParams)
        End Sub

    End Class

End Namespace
