' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class InitOnlyPropertyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub InitOnlyProperty_ConstructorCanAssign()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Class Box
    Public InitOnly Property Value As Integer

    Public Sub New(value As Integer)
        Me.Value = value
    End Sub
End Class

Class Program
    Shared Sub Main()
        Dim box = New Box(4)
        System.Console.Write(box.Value)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4").VerifyDiagnostics()

            Dim [property] = compilation.GetMember(Of PropertySymbol)("Box.Value")
            Assert.True([property].SetMethod.IsInitOnly)
            Assert.Equal("System.Runtime.CompilerServices.IsExternalInit", [property].SetMethod.ReturnTypeCustomModifiers.Single().Modifier.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub InitOnlyProperty_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Box
    Public InitOnly Property Value As Integer
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_LanguageVersion, "InitOnly").WithArguments("17.13", "init-only properties", "vbnext").WithLocation(3, 12))
        End Sub

        <Fact>
        Public Sub InitOnlyProperty_AssignmentOutsideConstructorIsAnError()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Class Box
    Public InitOnly Property Value As Integer

    Public Sub New()
        Me.Value = 1
    End Sub

    Public Sub SetLater()
        Me.Value = 2
    End Sub
End Class

Class Program
    Shared Sub Main()
        Dim box = New Box()
        box.Value = 3
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AssignmentInitOnly, "Me.Value = 2").WithArguments("Value").WithLocation(15, 9),
                Diagnostic(ERRID.ERR_AssignmentInitOnly, "box.Value = 3").WithArguments("Value").WithLocation(22, 9))
        End Sub

        <Fact>
        Public Sub InitOnlyProperty_ObjectInitializerCanAssign()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Class Box
    Public InitOnly Property Value As Integer
End Class

Class Program
    Shared Sub Main()
        Dim box = New Box() With {.Value = 5}
        System.Console.Write(box.Value)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub InitOnlyProperty_ImplementationMustMatchAccessor()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Interface IBox
    InitOnly Property Value As Integer
End Interface

Class Good
    Implements IBox
    Public InitOnly Property Value As Integer Implements IBox.Value

    Public Sub New()
        Me.Value = 1
    End Sub
End Class

Class Bad
    Implements IBox
    Public Property Value As Integer Implements IBox.Value
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_PropertyDoesntImplementInitOnly, "IBox.Value").WithArguments("Property Value As Integer").WithLocation(22, 49))

            Assert.True(compilation.GetMember(Of PropertySymbol)("IBox.Value").SetMethod.IsInitOnly)
            Assert.True(compilation.GetMember(Of PropertySymbol)("Good.Value").SetMethod.IsInitOnly)
        End Sub
    End Class
End Namespace
