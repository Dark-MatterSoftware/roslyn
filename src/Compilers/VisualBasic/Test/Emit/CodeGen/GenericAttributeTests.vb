' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class GenericAttributeTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub GenericAttribute_UseSitePassesTypeArgument()
            Dim compilation = CreateCompilation("
Imports System

Public Class Info(Of T)
    Inherits Attribute
End Class

<Info(Of String)>
Public Class Box
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()

            Dim attribute = compilation.GetTypeByMetadataName("Box").GetAttributes().Single()
            Assert.Equal("Info(Of System.String)", attribute.AttributeClass.ToTestDisplayString())

            Dim consumer = CreateCompilation("", references:={MetadataReference.CreateFromImage(compilation.EmitToArray())}, parseOptions:=VbNext)
            Dim consumed = consumer.GetTypeByMetadataName("Box").GetAttributes().Single()
            Assert.Equal("Info(Of System.String)", consumed.AttributeClass.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub GenericAttribute_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Imports System

Public Class Info(Of T)
    Inherits Attribute
End Class

<Info(Of String)>
Public Class Box
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_GenericClassCannotInheritAttr, "Info").WithLocation(4, 14),
                Diagnostic(ERRID.ERR_GenericArgsOnAttributeSpecifier, "Info").WithLocation(8, 2),
                Diagnostic(ERRID.ERR_TooFewGenericArguments1, "Info").WithArguments("Info(Of T)").WithLocation(8, 2))
        End Sub
    End Class
End Namespace
