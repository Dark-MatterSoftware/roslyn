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

        <Fact>
        Public Sub GenericAttribute_NameWithoutAttributeSuffix()
            Dim compilation = CreateCompilation("
Imports System

Namespace Tags
    Public Class TagAttribute(Of T)
        Inherits Attribute
    End Class
End Namespace

<Tags.Tag(Of String)>
Public Class Qualified
End Class

Namespace Tags
    <Tag(Of Integer)>
    Public Class Brief
    End Class

    <TagAttribute(Of Long)>
    Public Class [Long]
    End Class
End Namespace
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Assert.Equal("Tags.TagAttribute(Of System.String)", compilation.GetTypeByMetadataName("Qualified").GetAttributes().Single().AttributeClass.ToTestDisplayString())
            Assert.Equal("Tags.TagAttribute(Of System.Int32)", compilation.GetTypeByMetadataName("Tags.Brief").GetAttributes().Single().AttributeClass.ToTestDisplayString())
            Assert.Equal("Tags.TagAttribute(Of System.Int64)", compilation.GetTypeByMetadataName("Tags.Long").GetAttributes().Single().AttributeClass.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub GenericAttribute_DeclaredInCSharp()
            Dim library = CreateCSharpCompilation("
[System.AttributeUsage(System.AttributeTargets.All)]
public sealed class TagAttribute<T> : System.Attribute
{
    public string Note { get; set; }
}
", referencedAssemblies:=TargetFrameworkUtil.GetReferences(TargetFramework.Net90)).EmitToImageReference()

            Dim compilation = CreateCompilation("
<Tag(Of Integer)(Note:=""n"")>
Public Class Tagged
End Class
", targetFramework:=TargetFramework.Net90, references:={library}, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim attribute = compilation.GetTypeByMetadataName("Tagged").GetAttributes().Single()
            Assert.Equal("TagAttribute(Of System.Int32)", attribute.AttributeClass.ToTestDisplayString())
            Assert.Equal("n", attribute.NamedArguments.Single().Value.Value)
        End Sub

        <Fact>
        Public Sub GenericAttribute_TypeParameterArgumentIsAnError()
            Dim compilation = CreateCompilation("
Imports System

Public Class TagAttribute(Of T)
    Inherits Attribute
End Class

Public Class Holder(Of U)
    <Tag(Of U)>
    Public Sub M()
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(
<expected>
BC32067: Type parameters, generic types or types contained in generic types cannot be used as attributes.
    &lt;Tag(Of U)>
     ~~~~~~~~~
</expected>)
        End Sub
    End Class
End Namespace
