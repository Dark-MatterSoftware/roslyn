' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RecordTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub RecordClass_EmitsEqualityHashCodeAndPrint()
            Dim compilation = CreateCompilation("
Record Class Point
    Public Property X As Integer
    Public Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1, .Y = 2}
        Dim b As New Point With {.X = 1, .Y = 2}
        Dim c As New Point With {.X = 1, .Y = 3}
        System.Console.Write(a.Equals(b))
        System.Console.Write(a.Equals(c))
        System.Console.Write(a.GetHashCode() = b.GetHashCode())
        System.Console.Write(a.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="TrueFalseTruePoint { X = 1, Y = 2 }").VerifyDiagnostics()
            Dim point = DirectCast(compilation.GlobalNamespace.GetMembers("Point").Single(), INamedTypeSymbol)
            Assert.True(point.IsRecord)
        End Sub

        <Fact>
        Public Sub RecordClass_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Record Class Point
    Public Property X As Integer
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedDeclaration, "Record").WithLocation(2, 1))
        End Sub

        <Fact>
        Public Sub RecordStructure_EmitsEqualityAndWithCopy()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Structure Point
    Public Property X As Integer
    Public InitOnly Property Y As Integer
End Structure

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1, .Y = 2}
        Dim b = a With {.Y = 3}
        Dim same As New Point With {.X = 1, .Y = 2}
        System.Console.Write(a.X)
        System.Console.Write(a.Y)
        System.Console.Write(b.X)
        System.Console.Write(b.Y)
        System.Console.Write(a.Equals(b))
        System.Console.Write(a.ToString())
        System.Console.Write(a.Equals(CObj(same)))
        System.Console.Write(a.Equals(Nothing))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1213FalsePoint { X = 1, Y = 2 }TrueFalse").VerifyDiagnostics()
            Dim point = DirectCast(compilation.GlobalNamespace.GetMembers("Point").Single(), INamedTypeSymbol)
            Assert.True(point.IsRecord)
        End Sub

        <Fact>
        Public Sub RecordStructure_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Record Structure Point
    Public Property X As Integer
End Structure
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedDeclaration, "Record").WithLocation(2, 1))
        End Sub

        <Fact>
        Public Sub PositionalRecord_ConstructsPrintsAndDeconstructs()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point(X As Integer, Y As Integer)
End Class

Module Program
    Sub Main()
        Dim a As New Point(1, 2)
        Dim b As New Point(1, 2)
        Dim (x, y) = a
        System.Console.Write(a.X)
        System.Console.Write(a.Y)
        System.Console.Write(x)
        System.Console.Write(y)
        System.Console.Write(a.Equals(b))
        System.Console.Write(a.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1212TruePoint { X = 1, Y = 2 }").VerifyDiagnostics()
        End Sub

        Private Const ExternalInit As String = "
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace
"

        <Fact>
        Public Sub RecordWith_CopiesAPositionalRecordUnderOptionStrict()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Point(X As Integer, Y As Integer)
End Class

Module Program
    Sub Main()
        Dim a As Point = New Point(1, 2)
        Dim b As Point = a With {.Y = 3}
        System.Console.Write(a.ToString() & "" "" & b.ToString())
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(False).WithOptionExplicit(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Point { X = 1, Y = 2 } Point { X = 1, Y = 3 }").VerifyDiagnostics().VerifyIL("Program.Main", "
{
  // Code size       48 (0x30)
  .maxstack  4
  .locals init (Point V_0) //b
  IL_0000:  ldc.i4.1
  IL_0001:  ldc.i4.2
  IL_0002:  newobj     ""Sub Point..ctor(Integer, Integer)""
  IL_0007:  dup
  IL_0008:  callvirt   ""Function Point.<Clone>$() As Point""
  IL_000d:  dup
  IL_000e:  ldc.i4.3
  IL_000f:  callvirt   ""Sub Point.set_Y(Integer)""
  IL_0014:  stloc.0
  IL_0015:  callvirt   ""Function Point.ToString() As String""
  IL_001a:  ldstr      "" ""
  IL_001f:  ldloc.0
  IL_0020:  callvirt   ""Function Point.ToString() As String""
  IL_0025:  call       ""Function String.Concat(String, String, String) As String""
  IL_002a:  call       ""Sub System.Console.Write(String)""
  IL_002f:  ret
}")
        End Sub

        <Fact>
        Public Sub RecordWith_CopiesPrivateStateAndSkipsInitializers()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Account(Owner As String)
    Private _secret As Integer = Program.NextId()

    Public ReadOnly Property Secret As Integer
        Get
            Return _secret
        End Get
    End Property
End Class

Module Program
    Private _id As Integer

    Function NextId() As Integer
        _id += 1
        Return _id * 10
    End Function

    Sub Main()
        Dim a As New Account(""Ada"")
        Dim b As Account = a With {.Owner = ""Bo""}
        System.Console.Write(a.Owner & a.Secret & "" "" & b.Owner & b.Secret & "" "" & _id)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Ada10 Bo10 1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordWith_CopiesTheRuntimeType()
            Dim compilation = CreateCompilation(ExternalInit & "
Record Class Shape
    Public Property Name As String
End Class

Record Class Circle
    Inherits Shape
    Public Property Radius As Integer
End Class

Module Program
    Sub Main()
        Dim s As Shape = New Circle With {.Name = ""c"", .Radius = 4}
        Dim t As Shape = s With {.Name = ""d""}
        System.Console.Write(t.GetType().Name & "" "" & t.Name & "" "" & DirectCast(t, Circle).Radius)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Circle d 4").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordWith_CopiesAndAssignsInitOnly()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point
    Public Property X As Integer
    Public InitOnly Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1, .Y = 2}
        Dim b = a With {.Y = 3}
        System.Console.Write(a.X)
        System.Console.Write(a.Y)
        System.Console.Write(b.X)
        System.Console.Write(b.Y)
        System.Console.Write(a.Equals(b))
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1213False").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub RecordWith_InitOnlyStaysReadOnly()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point
    Public InitOnly Property Y As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.Y = 2}
        a.Y = 9
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AssignmentInitOnly, "a.Y = 9").WithArguments("Y").WithLocation(14, 9))
        End Sub

        <Fact>
        Public Sub RecordWith_RejectsNonRecord()
            Dim compilation = CreateCompilation("
Class Point
    Public Property X As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1}
        Dim b = a With {.X = 2}
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_WithCopyNotRecord, "With {.X = 2}").WithArguments("Point").WithLocation(9, 19))
        End Sub

        <Fact>
        Public Sub RecordWith_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Point
    Public Property X As Integer
End Class

Module Program
    Sub Main()
        Dim a As New Point With {.X = 1}
        Dim b = a With {.X = 2}
    End Sub
End Module
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedEOS, "With").WithLocation(9, 19))
        End Sub
    End Class

End Namespace
