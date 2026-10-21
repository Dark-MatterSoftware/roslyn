' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class SyncLockLockObjectTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        ' The shape of System.Threading.Lock that SyncLock uses, writing what each member does.
        Private Const LockDefinition As String = "
Namespace System.Threading
    Public NotInheritable Class Lock
        Public Function EnterScope() As Scope
            System.Console.Write(""enter "")
            Return New Scope()
        End Function

        Public Structure Scope
            Private _unused As Integer

            Public Sub Dispose()
                System.Console.Write(""dispose "")
            End Sub
        End Structure
    End Class
End Namespace
"

        <Fact>
        Public Sub SyncLock_EntersAndLeavesThroughScope()
            Dim compilation = CreateCompilation(LockDefinition & "
Class Program
    Shared ReadOnly Gate As New System.Threading.Lock()

    Shared Sub Main()
        SyncLock Gate
            System.Console.Write(""body "")
        End SyncLock
        System.Console.Write(""after"")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="enter body dispose after").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SyncLock_LeavesWhenBodyThrows()
            Dim compilation = CreateCompilation(LockDefinition & "
Class Program
    Shared Sub Main()
        Dim gate As New System.Threading.Lock()
        Try
            SyncLock gate
                Throw New System.InvalidOperationException()
            End SyncLock
        Catch ex As System.InvalidOperationException
            System.Console.Write(""caught"")
        End Try
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="enter dispose caught").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SyncLock_LeavesOnExitSub()
            Dim compilation = CreateCompilation(LockDefinition & "
Class Program
    Shared Sub Run(gate As System.Threading.Lock)
        SyncLock gate
            System.Console.Write(""body "")
            Exit Sub
        End SyncLock
    End Sub

    Shared Sub Main()
        Run(New System.Threading.Lock())
        System.Console.Write(""after"")
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="enter body dispose after").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SyncLock_RejectedOn17_13()
            Dim compilation = CreateCompilation(LockDefinition & "
Class Program
    Shared Sub Main()
        SyncLock New System.Threading.Lock()
        End SyncLock
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37329: A value of type 'System.Threading.Lock' is not supported in SyncLock. Consider manually calling 'Enter' and 'Exit' methods in a Try/Finally block instead.
        SyncLock New System.Threading.Lock()
                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub SyncLock_RequiresTheLockShape()
            Dim compilation = CreateCompilation("
Namespace System.Threading
    Public NotInheritable Class Lock
        Public Function EnterScope() As Integer
            Return 0
        End Function
    End Class
End Namespace

Class Program
    Shared Sub Main()
        SyncLock New System.Threading.Lock()
        End SyncLock
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37329: A value of type 'System.Threading.Lock' is not supported in SyncLock. Consider manually calling 'Enter' and 'Exit' methods in a Try/Finally block instead.
        SyncLock New System.Threading.Lock()
                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub SyncLock_ConvertedLockStillWarns()
            Dim compilation = CreateCompilation(LockDefinition & "
Class Program
    Shared Sub Main()
        Dim gate As Object = New System.Threading.Lock()
        SyncLock gate
        End SyncLock
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC42508: A value of type 'System.Threading.Lock' converted to a different type will use likely unintended monitor-based locking in SyncLock statement.
        Dim gate As Object = New System.Threading.Lock()
                             ~~~~~~~~~~~~~~~~~~~~~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub SyncLock_RealLockCallsEnterScopeAndDispose()
            Dim compilation = CreateCompilation("
Class Program
    Shared ReadOnly Gate As New System.Threading.Lock()
    Shared Count As Integer

    Shared Sub Increment()
        SyncLock Gate
            Count += 1
        End SyncLock
    End Sub
End Class
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.FailsPEVerify).VerifyDiagnostics().VerifyIL("Program.Increment", "
{
  // Code size       34 (0x22)
  .maxstack  2
  .locals init (System.Threading.Lock.Scope V_0)
  IL_0000:  ldsfld     ""Program.Gate As System.Threading.Lock""
  IL_0005:  callvirt   ""Function System.Threading.Lock.EnterScope() As System.Threading.Lock.Scope""
  IL_000a:  stloc.0
  .try
  {
    IL_000b:  ldsfld     ""Program.Count As Integer""
    IL_0010:  ldc.i4.1
    IL_0011:  add.ovf
    IL_0012:  stsfld     ""Program.Count As Integer""
    IL_0017:  leave.s    IL_0021
  }
  finally
  {
    IL_0019:  ldloca.s   V_0
    IL_001b:  call       ""Sub System.Threading.Lock.Scope.Dispose()""
    IL_0020:  endfinally
  }
  IL_0021:  ret
}")
        End Sub

        <Fact>
        Public Sub SyncLock_RealLockInAsyncMethodInDebugBuild()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks

Class Program
    Shared ReadOnly Gate As New System.Threading.Lock()
    Shared Count As Integer

    Shared Async Function IncrementAsync() As Task
        Await Task.Yield()
        SyncLock Gate
            Count += 1
        End SyncLock
        Await Task.Yield()
    End Function
End Class
", targetFramework:=TargetFramework.Net90, options:=TestOptions.DebugDll, parseOptions:=VbNext)

            CompileAndVerify(compilation, verify:=Verification.FailsPEVerify).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub SyncLock_RealLockCannotHoldAcrossYield()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic

Class Program
    Shared ReadOnly Gate As New System.Threading.Lock()

    Shared Iterator Function Items() As IEnumerable(Of Integer)
        SyncLock Gate
            Yield 1
        End SyncLock
    End Function
End Class
", targetFramework:=TargetFramework.Net90, parseOptions:=VbNext)

            compilation.AssertTheseEmitDiagnostics(<expected><![CDATA[
BC37405: A value of type 'Lock.Scope' cannot be kept across an 'Await' or 'Yield', because a ref structure cannot be stored while the method is suspended.
        SyncLock Gate
        ~~~~~~~~~~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
