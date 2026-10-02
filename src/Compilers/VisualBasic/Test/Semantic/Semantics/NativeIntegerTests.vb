' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NativeIntegerTests
        Inherits BasicTestBase

        <Fact>
        Public Sub TypeDefinitions_FromMetadata()
            Dim source0 =
"public interface I
{
    public void F1(System.IntPtr x, nint y);
    public void F2(System.UIntPtr x, nuint y);
}"
            Dim comp As Compilation = CreateCSharpCompilation(source0, parseOptions:=New CSharp.CSharpParseOptions(CSharp.LanguageVersion.CSharp9))
            Dim ref0 = comp.EmitToImageReference()

            Dim type = comp.GlobalNamespace.GetTypeMembers("I").Single()
            Dim method = DirectCast(type.GetMembers("F1").Single(), IMethodSymbol)
            Assert.Equal("Sub I.F1(x As System.IntPtr, y As System.IntPtr)", SymbolDisplay.ToDisplayString(method, SymbolDisplayFormat.TestFormat))
            VerifyType(DirectCast(method.Parameters(0).Type, INamedTypeSymbol), signed:=True, isNativeInteger:=False)
            VerifyType(DirectCast(method.Parameters(1).Type, INamedTypeSymbol), signed:=True, isNativeInteger:=True)

            method = DirectCast(type.GetMembers("F2").Single(), IMethodSymbol)
            Assert.Equal("Sub I.F2(x As System.UIntPtr, y As System.UIntPtr)", SymbolDisplay.ToDisplayString(method, SymbolDisplayFormat.TestFormat))
            VerifyType(DirectCast(method.Parameters(0).Type, INamedTypeSymbol), signed:=False, isNativeInteger:=False)
            VerifyType(DirectCast(method.Parameters(1).Type, INamedTypeSymbol), signed:=False, isNativeInteger:=True)

            comp = CreateCompilation("", references:={ref0})
            comp.VerifyDiagnostics()

            type = comp.GlobalNamespace.GetTypeMembers("I").Single()
            method = DirectCast(type.GetMembers("F1").Single(), IMethodSymbol)
            Assert.Equal("Sub I.F1(x As System.IntPtr, y As System.IntPtr)", SymbolDisplay.ToDisplayString(method, SymbolDisplayFormat.TestFormat))
            VerifyType(DirectCast(method.Parameters(0).Type, INamedTypeSymbol), signed:=True, isNativeInteger:=False)
            VerifyType(DirectCast(method.Parameters(1).Type, INamedTypeSymbol), signed:=True, isNativeInteger:=False)

            method = DirectCast(type.GetMembers("F2").Single(), IMethodSymbol)
            Assert.Equal("Sub I.F2(x As System.UIntPtr, y As System.UIntPtr)", SymbolDisplay.ToDisplayString(method, SymbolDisplayFormat.TestFormat))
            VerifyType(DirectCast(method.Parameters(0).Type, INamedTypeSymbol), signed:=False, isNativeInteger:=False)
            VerifyType(DirectCast(method.Parameters(1).Type, INamedTypeSymbol), signed:=False, isNativeInteger:=False)
        End Sub

        Private Shared Sub VerifyType(type As INamedTypeSymbol, signed As Boolean, isNativeInteger As Boolean)
            Assert.Equal(If(signed, SpecialType.System_IntPtr, SpecialType.System_UIntPtr), type.SpecialType)
            Assert.Equal(SymbolKind.NamedType, type.Kind)
            Assert.Equal(TypeKind.Struct, type.TypeKind)
            Assert.Same(type, type.ConstructedFrom)
            Assert.Equal(isNativeInteger, type.IsNativeIntegerType)

            Dim underlyingType = type.NativeIntegerUnderlyingType
            If isNativeInteger Then
                Assert.NotNull(underlyingType)
                VerifyType(underlyingType, signed, isNativeInteger:=False)
            Else
                Assert.Null(underlyingType)
            End If
        End Sub

        <Fact>
        Public Sub TypeNames_UseIntegerAndLongConversions()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim signed As nint = CType(4, nint)
        Dim fromLong As nint = CType(4L, nint)
        Dim asInteger As Integer = CType(signed, Integer)
        Dim asLong As Long = CType(fromLong, Long)
        Dim unsigned As nuint = CType(4UI, nuint)
        Dim asULong As ULong = CType(unsigned, ULong)
        System.Console.Write(asInteger)
        System.Console.Write(asLong)
        System.Console.Write(asULong)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))

            CompileAndVerify(compilation, expectedOutput:="444").VerifyDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim signedName = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().First(Function(name) name.Identifier.ValueText = "nint")
            Assert.Equal(SpecialType.System_IntPtr, model.GetTypeInfo(signedName).Type.SpecialType)
            Dim unsignedName = tree.GetRoot().DescendantNodes().OfType(Of IdentifierNameSyntax)().First(Function(name) name.Identifier.ValueText = "nuint")
            Assert.Equal(SpecialType.System_UIntPtr, model.GetTypeInfo(unsignedName).Type.SpecialType)
            Assert.False(model.GetTypeInfo(signedName).Type.IsNativeIntegerType)
        End Sub

        <Fact>
        Public Sub TypeName_UserDefinitionWins()
            Dim compilation = CreateCompilation("
Class nint
End Class

Class Holder
    Public Value As nint
End Class
", parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))

            compilation.VerifyDiagnostics()
            Dim field = compilation.GetMember(Of FieldSymbol)("Holder.Value")
            Assert.Equal("nint", field.Type.Name)
            Assert.Equal(SpecialType.None, field.Type.SpecialType)
        End Sub

        <Fact>
        Public Sub TypeNames_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Dim Value As nint
End Class
", parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_UndefinedType1, "nint").WithArguments("nint").WithLocation(3, 18))
        End Sub

        <Fact>
        Public Sub CreateNativeIntegerTypeSymbol()
            Dim comp = VisualBasicCompilation.Create(assemblyName:=GetUniqueName(), references:=TargetFrameworkUtil.GetReferences(TargetFramework.DefaultVb), options:=TestOptions.ReleaseDll)
            comp.AssertNoDiagnostics()
            Assert.Throws(Of NotSupportedException)(Function() comp.CreateNativeIntegerTypeSymbol(signed:=True))
            Assert.Throws(Of NotSupportedException)(Function() comp.CreateNativeIntegerTypeSymbol(signed:=False))
        End Sub

    End Class

End Namespace
