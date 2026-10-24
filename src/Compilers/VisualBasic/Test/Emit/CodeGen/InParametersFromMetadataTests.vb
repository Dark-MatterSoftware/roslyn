' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class InParametersFromMetadataTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const CSharpLibrary As String = "
namespace ProbeLib
{
    public abstract class Scorer
    {
        public abstract int Score(in int value);
    }

    public interface IReader
    {
        int Read(in long value);
    }

    public delegate int Measure(in int value);

    public struct Counter
    {
        public int Value;
        public void Bump() { Value++; }
    }

    public static class Calls
    {
        public static int Twice(in int value) => value * 2;
        public static int Peek(ref readonly Counter c) { return c.Value; }
        public static int Add(ref int value) => ++value;
        public static int Run(Scorer s, IReader r, int x) => s.Score(in x) + r.Read(2);
    }

    public class Tens : Scorer
    {
        public override int Score(in int value) => value * 10;
    }

    public class Ones : IReader
    {
        public int Read(in long value) => (int)value;
    }

    public static class Extensions
    {
        public static int Total(this in Counter c) => c.Value + 1000;
    }

    public class Table
    {
        public virtual int this[in int index] => index * 3;
    }
}
"

        Private Function CSharpLibraryReference() As MetadataReference
            Return CreateCSharpCompilation(CSharpLibrary, referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()
        End Function

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub CallsCSharpInMembers()
            Dim compilation = CreateCompilation("
Imports System
Imports ProbeLib

Class Holder
    Public ReadOnly Frozen As Integer = 5
End Class

Module Program
    Sub Main()
        Dim s As Scorer = New Tens()
        Dim r As IReader = New Ones()
        Dim m As Measure = AddressOf Calls.Twice
        Dim x = 3
        Dim h As New Holder()
        Console.Write(s.Score(x))
        Console.Write("" "" & s.Score(4))
        Console.Write("" "" & s.Score(h.Frozen))
        Console.Write("" "" & r.Read(7L))
        Console.Write("" "" & m(x))
        Console.Write("" "" & m.Invoke(6))
        Dim c As New Counter With {.Value = 2}
        Console.Write("" "" & c.Total())
        Dim t As New Table()
        Console.Write("" "" & t(x))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibraryReference()}, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="30 40 50 7 6 12 1002 9", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub OverridesAndImplementsCSharpInMembers()
            Dim compilation = CreateCompilation("
Imports System
Imports ProbeLib

Class Doubler
    Inherits Scorer

    Public Overrides Function Score(ByRef ReadOnly value As Integer) As Integer
        Return value * 2
    End Function
End Class

Class Reader
    Implements IReader

    Public Function Read(ByRef ReadOnly value As Long) As Integer Implements IReader.Read
        Return CInt(value) + 100
    End Function
End Class

Module Program
    Sub Main()
        Console.Write(Calls.Run(New Doubler(), New Reader(), 6))
        Dim s As Scorer = New Doubler()
        Console.Write("" "" & s.Score(21))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibraryReference()}, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="114 42", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub MetadataInParametersReportRefKindIn()
            Dim compilation = CreateCompilation("
Class C
End Class
", targetFramework:=TargetFramework.Net90, references:={CSharpLibraryReference()}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim parameterOf = Function(typeName As String, methodName As String) DirectCast(compilation.GetTypeByMetadataName(typeName).GetMembers(methodName).Single(), IMethodSymbol).Parameters.Single()

            Assert.Equal(Microsoft.CodeAnalysis.RefKind.In, parameterOf("ProbeLib.Scorer", "Score").RefKind)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.In, parameterOf("ProbeLib.IReader", "Read").RefKind)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.In, parameterOf("ProbeLib.Measure", "Invoke").RefKind)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.In, parameterOf("ProbeLib.Calls", "Twice").RefKind)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.In, parameterOf("ProbeLib.Calls", "Peek").RefKind)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.Ref, parameterOf("ProbeLib.Calls", "Add").RefKind)
            Assert.False(compilation.GetTypeByMetadataName("ProbeLib.Scorer").GetMembers("Score").Single().HasUnsupportedMetadata)
        End Sub

        <Fact>
        Public Sub VisualBasic17_13StillReportsInModifierMembersUnsupported()
            Dim compilation = CreateCompilation("
Imports ProbeLib

Module Program
    Function Use(s As Scorer, r As IReader, t As Table) As Integer
        Dim x = 3
        Return s.Score(x) + r.Read(1L) + Calls.Twice(x) + t(x)
    End Function
End Module
", targetFramework:=TargetFramework.Net90, references:={CSharpLibraryReference()}, options:=TestOptions.ReleaseDll.WithOptionInfer(True), parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30657: 'Score' has a return type that is not supported or parameter types that are not supported.
        Return s.Score(x) + r.Read(1L) + Calls.Twice(x) + t(x)
                 ~~~~~
BC30657: 'Read' has a return type that is not supported or parameter types that are not supported.
        Return s.Score(x) + r.Read(1L) + Calls.Twice(x) + t(x)
                              ~~~~
BC30643: Property 'ProbeLib.Table.Item(ByRef ReadOnly index As Integer)' is of an unsupported type.
        Return s.Score(x) + r.Read(1L) + Calls.Twice(x) + t(x)
                                                          ~
</expected>)
        End Sub

        <Fact>
        Public Sub ReadOnlyMismatchOnOverrideOrImplementation()
            Dim compilation = CreateCompilation("
Imports ProbeLib

Class Doubler
    Inherits Scorer

    Public Overrides Function Score(ByRef value As Integer) As Integer
        value = 1
        Return value
    End Function
End Class

Class Reader
    Implements IReader

    Public Function Read(ByRef value As Long) As Integer Implements IReader.Read
        Return 0
    End Function
End Class

Class Writer
    Inherits Scorer

    Public Overrides Function Score(ByRef ReadOnly value As Integer) As Integer
        value = 2
        Return value
    End Function
End Class
", targetFramework:=TargetFramework.Net90, references:={CSharpLibraryReference()}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC37420: 'Public Overrides Function Score(ByRef value As Integer) As Integer' cannot override 'Public MustOverride Overloads Function Score(ByRef ReadOnly value As Integer) As Integer' because they differ by a 'ByRef' parameter that is marked as 'ReadOnly' in only one of them.
    Public Overrides Function Score(ByRef value As Integer) As Integer
                              ~~~~~
BC37421: 'Read' cannot implement 'Function Read(ByRef ReadOnly value As Long) As Integer' because they differ by a 'ByRef' parameter that is marked as 'ReadOnly' in only one of them.
    Public Function Read(ByRef value As Long) As Integer Implements IReader.Read
                                                                    ~~~~~~~~~~~~
BC30064: 'ReadOnly' variable cannot be the target of an assignment.
        value = 2
        ~~~~~
</expected>)
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub VisualBasicInterfaceWithByRefReadOnlyParameterFromMetadata()
            Dim library = CreateCompilation("
Public Interface IMeasure
    Function Measure(ByRef ReadOnly value As Integer) As Integer
End Interface

Public MustInherit Class Base
    Public MustOverride Function Twice(ByRef ReadOnly value As Integer) As Integer
End Class
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim compilation = CreateCompilation("
Imports System

Class Ruler
    Inherits Base
    Implements IMeasure

    Public Function Measure(ByRef ReadOnly value As Integer) As Integer Implements IMeasure.Measure
        Return value + 1
    End Function

    Public Overrides Function Twice(ByRef ReadOnly value As Integer) As Integer
        Return value * 2
    End Function
End Class

Module Program
    Sub Main()
        Dim m As IMeasure = New Ruler()
        Dim b As Base = New Ruler()
        Dim x = 4
        Console.Write(m.Measure(x) & "" "" & b.Twice(x))
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, references:={library.EmitToImageReference()}, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5 8", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

    End Class

End Namespace
