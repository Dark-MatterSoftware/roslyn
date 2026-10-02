' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class SetsRequiredMembersTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const AttributeSource As String = "
Namespace System.Runtime.CompilerServices
    Public Class RequiredMemberAttribute
        Inherits Attribute
    End Class
End Namespace

Namespace System.Diagnostics.CodeAnalysis
    Public Class SetsRequiredMembersAttribute
        Inherits Attribute
    End Class
End Namespace
"

        <Fact>
        Public Sub SetsRequiredMembers_ConstructorOptsOutOfInitializer()
            Dim compilation = CreateCompilation(AttributeSource & "
Public Class Box
    Public Required Property Name As String

    <System.Diagnostics.CodeAnalysis.SetsRequiredMembers>
    Public Sub New()
        Me.Name = ""ok""
    End Sub
End Class

Class Program
    Shared Sub Main()
        Dim box = New Box()
        System.Console.Write(box.Name)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="ok").VerifyDiagnostics()

            Dim constructor = compilation.GetMember(Of MethodSymbol)("Box..ctor")
            Assert.True(constructor.HasSetsRequiredMembers)

            Dim consumer = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim box = New Box()
    End Sub
End Class
", references:={MetadataReference.CreateFromImage(compilation.EmitToArray())}, parseOptions:=VbNext)
            consumer.VerifyDiagnostics()
            Dim consumed = DirectCast(consumer.GetTypeByMetadataName("Box"), NamedTypeSymbol).Constructors.Single()
            Assert.True(consumed.HasSetsRequiredMembers)
        End Sub

        <Fact>
        Public Sub SetsRequiredMembers_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Namespace System.Diagnostics.CodeAnalysis
    Public Class SetsRequiredMembersAttribute
        Inherits Attribute
    End Class
End Namespace

Class Box
    <System.Diagnostics.CodeAnalysis.SetsRequiredMembers>
    Public Sub New()
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_AttributeNotSupportedInVB, "System.Diagnostics.CodeAnalysis.SetsRequiredMembers").WithArguments("System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute").WithLocation(9, 6))

            Dim constructor = compilation.GetMember(Of MethodSymbol)("Box..ctor")
            Assert.False(constructor.HasSetsRequiredMembers)
        End Sub
    End Class
End Namespace
