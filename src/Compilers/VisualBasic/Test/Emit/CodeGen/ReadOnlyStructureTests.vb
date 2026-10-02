' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ReadOnlyStructureTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const IsReadOnlyAttributeSource As String = "
Namespace System.Runtime.CompilerServices
    Public Class IsReadOnlyAttribute
        Inherits Attribute
    End Class
End Namespace
"

        <Fact>
        Public Sub ReadOnlyStructure_ConstructorCanAssign()
            Dim compilation = CreateCompilation(IsReadOnlyAttributeSource & "
ReadOnly Structure Point
    Public ReadOnly X As Integer

    Public Sub New(x As Integer)
        Me.X = x
    End Sub
End Structure

Class Program
    Shared Sub Main()
        Dim point = New Point(4)
        System.Console.Write(point.X)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="4").VerifyDiagnostics()

            Dim point = compilation.GetTypeByMetadataName("Point")
            Assert.True(point.IsReadOnly)

            Dim consumer = CreateCompilation("", references:={MetadataReference.CreateFromImage(compilation.EmitToArray())}, parseOptions:=VbNext)
            Dim consumed = consumer.GetTypeByMetadataName("Point")
            Assert.True(consumed.IsReadOnly)
            Assert.Equal("IsReadOnlyAttribute", consumed.GetAttributes().Single().AttributeClass.Name)
        End Sub

        <Fact>
        Public Sub ReadOnlyStructure_InstanceMembersThatMutateAreErrors()
            Dim compilation = CreateCompilation(IsReadOnlyAttributeSource & "
ReadOnly Structure Point
    Public X As Integer
    Public Property Name As String
    Public Event Changed As System.Action
End Structure
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_FieldInReadOnlyStructure, "X").WithLocation(9, 12),
                Diagnostic(ERRID.ERR_AutoPropertyInReadOnlyStructure, "Name").WithLocation(10, 21),
                Diagnostic(ERRID.ERR_EventInReadOnlyStructure, "Changed").WithLocation(11, 18))
        End Sub

        <Fact>
        Public Sub ReadOnlyStructure_RejectedOn17_13()
            Dim compilation = CreateCompilation("
ReadOnly Structure Point
    Public ReadOnly X As Integer
End Structure
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadRecordFlags1, "ReadOnly").WithArguments("ReadOnly").WithLocation(2, 1))
            Assert.False(compilation.GetTypeByMetadataName("Point").IsReadOnly)
        End Sub
    End Class
End Namespace
