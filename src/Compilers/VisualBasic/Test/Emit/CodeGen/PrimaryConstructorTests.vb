' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class PrimaryConstructorTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub PrimaryConstructor_InstanceInitializerUsesParameter()
            Dim compilation = CreateCompilation("
Class Customer(name As String, id As Integer)
    Public ReadOnly Property Label As String = name
    Private ReadOnly _id As Integer = id
    Public Function Describe() As String
        Return Label & _id
    End Function
End Class
Module Program
    Sub Main()
        Dim c = New Customer(NextName(), 7)
        System.Console.Write(c.Describe())
    End Sub
    Function NextName() As String
        System.Console.Write(""1"")
        Return ""ok""
    End Function
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1ok7").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_GenericParameterInInitializer()
            Dim compilation = CreateCompilation("
Class Box(Of T)(value As T)
    Public ReadOnly Property Value As T = value
End Class
Module Program
    Sub Main()
        Dim box = New Box(Of String)(""ok"")
        System.Console.Write(box.Value)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_SharedInitializerCannotUseParameter()
            Dim compilation = CreateCompilation("
Class Customer(name As String)
    Public Shared ReadOnly Broken As String = name
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotDeclared1, "name").WithArguments("name").WithLocation(3, 47))
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_ParameterIsNotAProperty()
            Dim compilation = CreateCompilation("
Class Customer(name As String)
    Public ReadOnly Property Label As String = name
End Class
Module Program
    Sub Main()
        Dim c = New Customer(""ok"")
        System.Console.Write(c.name)
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotMember2, "c.name").WithArguments("name", "Customer").WithLocation(8, 30))
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_DuplicateOnPartialClass()
            Dim compilation = CreateCompilation("
Partial Class Customer(name As String)
End Class
Partial Class Customer(id As Integer)
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DuplicatePrimaryConstructor, "(id As Integer)").WithLocation(4, 23))
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_InheritsPassesParameter()
            Dim compilation = CreateCompilation("
Class Base
    Public ReadOnly Property Name As String
    Public Sub New(name As String)
        Me.Name = name
    End Sub
End Class
Class Derived(name As String)
    Inherits Base(name)
End Class
Module Program
    Sub Main()
        Dim derived = New Derived(NextName())
        System.Console.Write(derived.Name)
    End Sub
    Function NextName() As String
        System.Console.Write(""1"")
        Return ""ok""
    End Function
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1ok").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_InheritsWithoutArgumentsStaysParameterless()
            Dim compilation = CreateCompilation("
Class Base
    Public Sub New()
        System.Console.Write(""base"")
    End Sub
End Class
Class Derived(name As String)
    Inherits Base
    Public ReadOnly Property Label As String = name
End Class
Module Program
    Sub Main()
        Dim derived = New Derived(""ok"")
        System.Console.Write(derived.Label)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="baseok").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_MeNewChainsToPrimary()
            Dim compilation = CreateCompilation("
Class Customer(name As String)
    Public ReadOnly Property Label As String = name
    Public Sub New()
        Me.New(""ok"")
    End Sub
End Class
Module Program
    Sub Main()
        Dim customer = New Customer()
        System.Console.Write(customer.Label)
    End Sub
End Module
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_BaseArgumentsRequirePrimaryConstructor()
            Dim compilation = CreateCompilation("
Class Base
    Public Sub New(name As String)
    End Sub
End Class
Class Derived
    Inherits Base(""ok"")
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NoConstructorOnBase2, "Derived").WithArguments("Derived", "Base").WithLocation(6, 7),
                Diagnostic(ERRID.ERR_BaseConstructorArgumentsRequirePrimaryConstructor, "(""ok"")").WithLocation(7, 18))
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_InheritsArgumentsRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Base
    Public Sub New(name As String)
    End Sub
End Class
Class Derived
    Inherits Base(name)
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_TypeOrMemberNotGeneric1, "Base(name)").WithArguments("Base").WithLocation(7, 14),
                Diagnostic(ERRID.ERR_OfExpected, "").WithLocation(7, 19),
                Diagnostic(ERRID.ERR_UndefinedType1, "name").WithArguments("name").WithLocation(7, 19))
        End Sub

        <Fact>
        Public Sub PrimaryConstructor_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Customer(name As String)
    Public ReadOnly Property Label As String = name
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_OfExpected, "").WithLocation(2, 16),
                Diagnostic(ERRID.ERR_ClassConstraintNotInheritable1, "String").WithLocation(2, 24),
                Diagnostic(ERRID.ERR_TypeNotExpression1, "name").WithArguments("name").WithLocation(3, 48))
        End Sub
    End Class
End Namespace
