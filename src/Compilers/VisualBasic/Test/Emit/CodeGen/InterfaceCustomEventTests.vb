' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit

    Public Class InterfaceCustomEventTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const InterfaceSource As String = "
Imports System

Public Interface INotify
    Custom Event Changed As EventHandler
        AddHandler(value As EventHandler)
            Console.Write(""add "")
        End AddHandler
        RemoveHandler(value As EventHandler)
            Console.Write(""remove "")
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
            Console.Write(""raise "")
        End RaiseEvent
    End Event
End Interface

Public Class Quiet
    Implements INotify
End Class

Public Class Loud
    Implements INotify

    Public Custom Event Changed As EventHandler Implements INotify.Changed
        AddHandler(value As EventHandler)
            Console.Write(""loud-add "")
        End AddHandler
        RemoveHandler(value As EventHandler)
            Console.Write(""loud-remove "")
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent
    End Event
End Class
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub DefaultEventAccessors()
            Dim compilation = CreateCompilation({InterfaceSource, "
Imports System

Module Program
    Sub Handler(sender As Object, e As EventArgs)
    End Sub

    Sub Main()
        Dim q As INotify = New Quiet()
        AddHandler q.Changed, AddressOf Handler
        RemoveHandler q.Changed, AddressOf Handler
        Dim l As INotify = New Loud()
        AddHandler l.Changed, AddressOf Handler
        RemoveHandler l.Changed, AddressOf Handler
    End Sub
End Module
"}, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="add remove loud-add loud-remove ", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub CSharpReadsDefaultEventAccessors()
            Dim library = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            library.AssertNoDiagnostics()

            Dim csCompilation = CreateCSharpCompilation("
public class Use
{
    public static void Call(INotify n) { n.Changed += (s, e) => { }; }
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90, {library.EmitToImageReference()}))
            csCompilation.VerifyDiagnostics()

            Dim changed = DirectCast(csCompilation.GetTypeByMetadataName("INotify").GetMembers("Changed").Single(), IEventSymbol)
            Assert.False(changed.AddMethod.IsAbstract)
            Assert.True(changed.AddMethod.IsVirtual)
        End Sub

        <Fact>
        Public Sub InterfaceCustomEventErrors()
            Dim compilation = CreateCompilation("
Imports System

Interface IBad
    Custom Event NoType
        AddHandler(value As EventHandler)
        End AddHandler
    End Event

    Custom Event Incomplete As EventHandler
        AddHandler(value As EventHandler)
        End AddHandler
    End Event
End Interface
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC31121: 'Custom' modifier is not valid on events declared in interfaces.
    Custom Event NoType
    ~~~~~~~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
        AddHandler(value As EventHandler)
        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
        End AddHandler
        ~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Event
    ~~~~~~~~~
BC31131: 'RemoveHandler' definition missing for event 'Event Incomplete As EventHandler'.
    Custom Event Incomplete As EventHandler
                 ~~~~~~~~~~
BC31132: 'RaiseEvent' definition missing for event 'Event Incomplete As EventHandler'.
    Custom Event Incomplete As EventHandler
                 ~~~~~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub InterfaceCustomEventsNeedVbNext()
            Dim compilation = CreateCompilation(InterfaceSource, targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular17_13)

            compilation.AssertTheseDiagnostics(
<expected>
BC31121: 'Custom' modifier is not valid on events declared in interfaces.
    Custom Event Changed As EventHandler
    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
        AddHandler(value As EventHandler)
        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC30188: Declaration expected.
            Console.Write("add ")
            ~~~~~~~
BC30603: Statement cannot appear within an interface body.
        End AddHandler
        ~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
        RemoveHandler(value As EventHandler)
        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC30188: Declaration expected.
            Console.Write("remove ")
            ~~~~~~~
BC30603: Statement cannot appear within an interface body.
        End RemoveHandler
        ~~~~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
        RaiseEvent(sender As Object, e As EventArgs)
        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC30188: Declaration expected.
            Console.Write("raise ")
            ~~~~~~~
BC30603: Statement cannot appear within an interface body.
        End RaiseEvent
        ~~~~~~~~~~~~~~
BC30603: Statement cannot appear within an interface body.
    End Event
    ~~~~~~~~~
BC30149: Class 'Quiet' must implement 'Event Changed As EventHandler' for interface 'INotify'.
    Implements INotify
               ~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
