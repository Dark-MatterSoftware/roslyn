' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableAnnotationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub Annotations_StoredOnLocalParameterAndReturn()
            Dim compilation = CreateCompilation("
Module Program
    Function Find(text As String?) As String?
        Dim plain As String = Nothing
        Dim marked As String? = text
        Dim value As Integer? = Nothing
        Return marked
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("plain").WithLocation(4, 31))

            Dim method = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Program").Single().GetMembers("Find").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.Annotated, method.ReturnNullableAnnotation)
            Assert.Equal(SpecialType.System_String, method.ReturnType.SpecialType)
            Assert.Equal(NullableAnnotation.Annotated, method.Parameters(0).NullableAnnotation)
            Assert.Equal(SpecialType.System_String, method.Parameters(0).Type.SpecialType)

            Dim tree = compilation.SyntaxTrees(0)
            Dim model = compilation.GetSemanticModel(tree)
            Dim plain = LocalNamed(model, tree, "plain")
            Dim marked = LocalNamed(model, tree, "marked")
            Dim value = LocalNamed(model, tree, "value")

            Assert.Equal(NullableAnnotation.NotAnnotated, plain.NullableAnnotation)
            Assert.Equal(SpecialType.System_String, plain.Type.SpecialType)
            Assert.Equal(NullableAnnotation.Annotated, marked.NullableAnnotation)
            Assert.Equal(SpecialType.System_String, marked.Type.SpecialType)
            Assert.Equal(NullableAnnotation.None, value.NullableAnnotation)
            Assert.Equal("System.Nullable(Of System.Int32)", value.Type.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub Flow_WarnsOnNullAssignmentDereferenceReturnAndArgument()
            Dim compilation = CreateCompilation("
Module Program
    Function Find(marked As String?) As String
        Dim plain As String = Nothing
        Dim length = marked.Length
        If marked IsNot Nothing Then
            Dim safe = marked.Length
        End If
        Return Nothing
    End Function

    Sub Take(text As String)
    End Sub

    Sub Pass(marked As String?)
        Take(marked)
        Dim value As Integer? = Nothing
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("plain").WithLocation(4, 31),
                Diagnostic(ERRID.WRN_PossibleNullReference, "marked").WithArguments("marked").WithLocation(5, 22),
                Diagnostic(ERRID.WRN_PossibleNullReturn, "Nothing").WithLocation(9, 16),
                Diagnostic(ERRID.WRN_PossibleNullArgument, "marked").WithArguments("text").WithLocation(16, 14))
        End Sub

        <Fact>
        Public Sub NullableDirective_OverridesTheProjectSwitch()
            Dim compilation = CreateCompilation("
#Nullable Disable
Module Program
    Function Quiet() As String
        Dim plain As String = Nothing
        Return plain
    End Function
End Module

#Nullable Enable
Module Program2
    Function Loud() As String
        Dim plain As String = Nothing
        Return plain
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_PossibleNullAssignment, "Nothing").WithArguments("plain").WithLocation(13, 31),
                Diagnostic(ERRID.WRN_PossibleNullReturn, "plain").WithLocation(14, 16))
        End Sub

        <Fact>
        Public Sub NullableDirective_RejectedOn17_13()
            Dim compilation = CreateCompilation("
#Nullable Enable
Module Program
    Sub Main()
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedConditionalDirective, "#").WithLocation(2, 1))
        End Sub

        <Fact>
        Public Sub NullableDirective_RejectsAnUnknownSetting()
            Dim compilation = CreateCompilation("
#Nullable Sometimes
Module Program
    Sub Main()
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadNullableDirective, "Sometimes").WithLocation(2, 11))
        End Sub

        <Fact>
        Public Sub AnnotationsOnly_StoresWithoutWarnings()
            Dim compilation = CreateCompilation("
Module Program
    Function Find(text As String) As String
        Dim plain As String = Nothing
        Return plain
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Annotations), parseOptions:=VbNext)

            compilation.VerifyDiagnostics()

            Dim method = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Program").Single().GetMembers("Find").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, method.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, method.Parameters(0).NullableAnnotation)
        End Sub

        Private Const NullableAttributeSource As String = "
Namespace System.Runtime.CompilerServices
    Public Class NullableAttribute
        Inherits Attribute
        Public Sub New(flag As Byte)
        End Sub
        Public Sub New(flags As Byte())
        End Sub
    End Class
    Public Class NullableContextAttribute
        Inherits Attribute
        Public Sub New(flag As Byte)
        End Sub
    End Class
End Namespace
"

        <Fact>
        Public Sub Metadata_EmitsContextWhenTheSignatureAgrees()
            Dim compilation = CreateCompilation(NullableAttributeSource & "
Module Program
    Function Plain(text As String) As String
        Return text
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim attribute = EmittedMethodAttribute(compilation, "Program", "Plain", "NullableContextAttribute")
            Assert.Equal(CByte(1), attribute.ConstructorArguments.Single().Value)
        End Sub

        <Fact>
        Public Sub Metadata_EmitsFlagsWhenTheSignatureDiffers()
            Dim compilation = CreateCompilation(NullableAttributeSource & "
Module Program
    Function Mix(text As String) As String?
        Return text
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim attribute = EmittedMethodAttribute(compilation, "Program", "Mix", "NullableAttribute")
            Dim values = attribute.ConstructorArguments.Single().Values
            Assert.Equal(2, values.Length)
            Assert.Equal(CByte(2), values(0).Value)
            Assert.Equal(CByte(1), values(1).Value)
        End Sub

        <Fact>
        Public Sub Metadata_OmittedWhenAnnotationsAreOff()
            Dim compilation = CreateCompilation(NullableAttributeSource & "
Module Program
    Function Plain(text As String) As String
        Return text
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim method = EmittedMethod(compilation, "Program", "Plain")
            Assert.DoesNotContain(method.GetAttributes(), Function(attribute) attribute.AttributeClass.Name.StartsWith("Nullable", StringComparison.Ordinal))
        End Sub

        <Fact>
        Public Sub StringQuestion_StillErrorsWhenAnnotationsAreOff()
            Dim compilation = CreateCompilation("
Module Program
    Function Find(text As String?) As String?
        Return text
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraintNull, "text").WithArguments("String").WithLocation(3, 19),
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraintNull, "String?").WithArguments("String").WithLocation(3, 39))
        End Sub

        <Fact>
        Public Sub StringQuestion_StillErrorsOn17_13()
            Dim compilation = CreateCompilation("
Module Program
    Function Find(text As String?) As String?
        Return text
    End Function
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraintNull, "text").WithArguments("String").WithLocation(3, 19),
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraintNull, "String?").WithArguments("String").WithLocation(3, 39))
        End Sub

        <Fact>
        Public Sub FieldAndTypeParameter_StayOnTheValueNullablePath()
            Dim compilation = CreateCompilation("
Class Box
    Public F As String?
    Public Function Id(Of T)(value As T?) As T?
        Return value
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraintNull, "F").WithArguments("String").WithLocation(3, 12),
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraintNull, "value").WithArguments("T").WithLocation(4, 30),
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraintNull, "T?").WithArguments("T").WithLocation(4, 46))
        End Sub

        <Fact>
        Public Sub NullableSwitch_IsParsed()
            Dim enabled = VisualBasicCommandLineParser.Default.Parse(
                {"/nullable:enable", "a.vb"},
                baseDirectory:=System.IO.Path.GetTempPath(),
                sdkDirectory:=System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                additionalReferenceDirectories:=Nothing)
            Assert.Equal(NullableContextOptions.Enable, DirectCast(enabled.CompilationOptions, VisualBasicCompilationOptions).NullableContext)

            Dim omitted = VisualBasicCommandLineParser.Default.Parse(
                {"a.vb"},
                baseDirectory:=System.IO.Path.GetTempPath(),
                sdkDirectory:=System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                additionalReferenceDirectories:=Nothing)
            Assert.Equal(NullableContextOptions.Disable, DirectCast(omitted.CompilationOptions, VisualBasicCompilationOptions).NullableContext)
        End Sub

        Private Shared Function EmittedMethod(compilation As VisualBasicCompilation, typeName As String, methodName As String) As IMethodSymbol
            Dim image = compilation.EmitToArray()
            Dim consumer = CreateCompilation("", references:={MetadataReference.CreateFromImage(image)}, parseOptions:=VbNext)
            Return DirectCast(consumer.GetTypeByMetadataName(typeName).GetMembers(methodName).Single(), IMethodSymbol)
        End Function

        Private Shared Function EmittedMethodAttribute(compilation As VisualBasicCompilation, typeName As String, methodName As String, attributeName As String) As AttributeData
            Return EmittedMethod(compilation, typeName, methodName).GetAttributes().Single(Function(attribute) attribute.AttributeClass.Name = attributeName)
        End Function

        Private Shared Function LocalNamed(model As SemanticModel, tree As SyntaxTree, name As String) As ILocalSymbol
            Dim syntax = tree.GetRoot().DescendantNodes().OfType(Of ModifiedIdentifierSyntax)().Single(Function(identifier) identifier.Identifier.ValueText = name)
            Return DirectCast(model.GetDeclaredSymbol(syntax), ILocalSymbol)
        End Function
    End Class
End Namespace
