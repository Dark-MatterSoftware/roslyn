' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class RequiredPropertyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const RequiredMemberAttributeSource As String = "
Namespace System.Runtime.CompilerServices
    Public Class RequiredMemberAttribute
        Inherits Attribute
    End Class
End Namespace
"

        <Fact>
        Public Sub RequiredProperty_ObjectInitializerSetsIt()
            Dim compilation = CreateCompilation(RequiredMemberAttributeSource & "
Class Box
    Public Required Property Name As String
End Class

Class Program
    Shared Sub Main()
        Dim box = New Box() With {.Name = ""ok""}
        System.Console.Write(box.Name)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()

            Dim [property] = compilation.GetMember(Of PropertySymbol)("Box.Name")
            Assert.True([property].IsRequired)

            Dim consumer = CreateCompilation("", references:={MetadataReference.CreateFromImage(compilation.EmitToArray())}, parseOptions:=VbNext)
            Dim box = DirectCast(consumer.GetTypeByMetadataName("Box"), NamedTypeSymbol)
            Assert.False(box.HasRequiredMembersError)
            Assert.True(box.AllRequiredMembers("Name").IsRequired())
        End Sub

        <Fact>
        Public Sub RequiredProperty_OmittedInitializerIsAnError()
            Dim compilation = CreateCompilation(RequiredMemberAttributeSource & "
Class Box
    Public Required Property Name As String

    Public Sub New()
        Me.Name = String.Empty
    End Sub
End Class

Class Program
    Shared Sub Main()
        Dim unused = New Box()
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_RequiredMemberMustBeSet, "Box").WithArguments("Public Property Name As String").WithLocation(18, 26))
        End Sub

        <Fact>
        Public Sub RequiredProperty_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Box
    Public Required Property Name As String
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_LanguageVersion, "Required").WithArguments("17.13", "required properties", "vbnext").WithLocation(3, 12))
        End Sub
    End Class
End Namespace
