' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class InterfaceSharedFieldTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const InterfaceSource As String = "
Imports System

Public Interface ICounter
    Const Limit As Integer = 10
    Shared Count As Integer = 5
    Shared ReadOnly Name As String

    Shared Sub New()
        Name = ""counter""
        Console.Write(""init "")
    End Sub

    Shared Function Increment() As Integer
        Count += 1
        Return Count
    End Function
End Interface

Public Interface IDefaults
    Shared Total As Integer = 3

    Shared Function GetTotal() As Integer
        Return Total
    End Function
End Interface
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub SharedFieldsAndConstructor()
            Dim compilation = CreateCompilation({InterfaceSource, "
Imports System

Module Program
    Sub Main()
        Console.Write(ICounter.Increment() & "" "" & ICounter.Increment() & "" "" & ICounter.Name & "" "" & ICounter.Limit & "" "" & ICounter.Count)
        Console.Write("" "" & IDefaults.GetTotal())
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="init 6 7 counter 10 7 3", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CSharpReadsSharedInterfaceFields()
            Dim library = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public static class Use
{
    public static int Call() => ICounter.Count + ICounter.Limit + ICounter.Increment() + ICounter.Name.Length;
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SharedInterfaceFieldErrors()
            Dim compilation = CreateCompilation("
Interface IBad
    Value As Integer
    Dim Other As Integer

    Sub New()
    End Sub
End Interface
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC30188: Declaration expected.
    Value As Integer
    ~~~~~
BC30602: Interface members must be methods, properties, events, or type definitions.
    Dim Other As Integer
    ~~~~~~~~~~~~~~~~~~~~
BC30363: 'Sub New' cannot be declared in an interface.
    Sub New()
    ~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Sub
    ~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub SharedInterfaceFieldsNeedVbNext()
            Dim compilation = CreateCompilation("
Interface ICounter
    Shared Count As Integer

    Shared Sub New()
    End Sub
End Interface
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC30602: Interface members must be methods, properties, events, or type definitions.
    Shared Count As Integer
    ~~~~~~~~~~~~~~~~~~~~~~~
BC30270: 'Shared' is not valid on an interface method declaration.
    Shared Sub New()
    ~~~~~~
BC30363: 'Sub New' cannot be declared in an interface.
    Shared Sub New()
    ~~~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Sub
    ~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
