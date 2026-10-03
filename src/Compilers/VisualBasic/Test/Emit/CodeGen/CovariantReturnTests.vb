' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Reflection
Imports System.Reflection.Metadata
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class CovariantReturnTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub CovariantReturn_NarrowsClassAndEmitsOverride()
            Dim compilation = CreateCompilation("
Public Class Animal
    Public Overridable Function Create() As Animal
        Return Me
    End Function
End Class

Public Class Dog
    Inherits Animal
    Public Overrides Function Create() As Dog
        Return Me
    End Function

    Public Function Kind() As String
        Return ""dog""
    End Function
End Class

Public Class Use
    Public Shared Function Describe(dog As Dog) As String
        Return dog.Create().Kind()
    End Function
End Class
", parseOptions:=VbNext, targetFramework:=TargetFramework.Net50)

            compilation.VerifyDiagnostics()

            Dim method = compilation.GetMember(Of MethodSymbol)("Dog.Create")
            Assert.Equal("Dog", method.ReturnType.Name)
            Assert.Equal("Animal", method.OverriddenMethod.ReturnType.Name)

            Dim image = compilation.EmitToArray()
            AssertCovariantSignature(image)

            Dim consumer = CreateCompilation("
Public Class Use
    Public Shared Function Describe(dog As Dog) As String
        Return dog.Create().Kind()
    End Function
End Class
", references:={MetadataReference.CreateFromImage(image)}, parseOptions:=VbNext, targetFramework:=TargetFramework.Net50)

            consumer.VerifyDiagnostics()
            Dim emitted = consumer.GetTypeByMetadataName("Dog").GetMembers("Create").OfType(Of MethodSymbol)().Single()
            Assert.Equal("Dog", emitted.ReturnType.Name)
            Assert.Contains(emitted.GetAttributes(), Function(attribute) attribute.AttributeClass.Name = "PreserveBaseOverridesAttribute")
        End Sub

        <Fact>
        Public Sub CovariantReturn_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Public Class Animal
    Public Overridable Function Create() As Animal
        Return Me
    End Function
End Class

Public Class Dog
    Inherits Animal
    Public Overrides Function Create() As Dog
        Return Me
    End Function
End Class
", parseOptions:=Vb17_13, targetFramework:=TargetFramework.Net50)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_InvalidOverrideDueToReturn2, "Create").WithArguments(
                    "Public Overrides Function Create() As Dog",
                    "Public Overridable Function Create() As Animal").WithLocation(10, 31))
        End Sub

        <Fact>
        Public Sub CovariantReturn_RejectedWithoutRuntimeSupport()
            Dim compilation = CreateCompilation("
Public Class Animal
    Public Overridable Function Create() As Animal
        Return Me
    End Function
End Class

Public Class Dog
    Inherits Animal
    Public Overrides Function Create() As Dog
        Return Me
    End Function
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_InvalidOverrideDueToReturn2, "Create").WithArguments(
                    "Public Overrides Function Create() As Dog",
                    "Public Overridable Function Create() As Animal").WithLocation(10, 31))
        End Sub

        <Fact>
        Public Sub CovariantReturn_RejectsUnrelatedClass()
            Dim compilation = CreateCompilation("
Public Class Animal
    Public Overridable Function Create() As Animal
        Return Me
    End Function
End Class

Public Class Cat
End Class

Public Class Dog
    Inherits Animal
    Public Overrides Function Create() As Cat
        Return Nothing
    End Function
End Class
", parseOptions:=VbNext, targetFramework:=TargetFramework.Net50)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_InvalidOverrideDueToReturn2, "Create").WithArguments(
                    "Public Overrides Function Create() As Cat",
                    "Public Overridable Function Create() As Animal").WithLocation(13, 31))
        End Sub

        Private Shared Sub AssertCovariantSignature(image As ImmutableArray(Of Byte))
            Dim reader = ModuleMetadata.CreateFromImage(image).Module.GetMetadataReader()
            Dim dogCreate As MethodDefinitionHandle = Nothing
            Dim animalCreate As MethodDefinitionHandle = Nothing

            For Each handle In reader.MethodDefinitions
                Dim definition = reader.GetMethodDefinition(handle)
                If reader.GetString(definition.Name) <> "Create" Then
                    Continue For
                End If

                Dim typeName = reader.GetString(reader.GetTypeDefinition(definition.GetDeclaringType()).Name)
                If typeName = "Dog" Then
                    dogCreate = handle
                    Assert.NotEqual(MethodAttributes.PrivateScope, definition.Attributes And MethodAttributes.NewSlot)
                    Assert.NotEqual(MethodAttributes.PrivateScope, definition.Attributes And MethodAttributes.Virtual)
                ElseIf typeName = "Animal" Then
                    animalCreate = handle
                End If
            Next

            Assert.False(dogCreate.IsNil)
            Assert.False(animalCreate.IsNil)

            Dim matched = False
            For Each typeHandle In reader.TypeDefinitions
                For Each implHandle In reader.GetTypeDefinition(typeHandle).GetMethodImplementations()
                    Dim implementation = reader.GetMethodImplementation(implHandle)
                    If implementation.MethodBody.Equals(CType(dogCreate, EntityHandle)) AndAlso implementation.MethodDeclaration.Equals(CType(animalCreate, EntityHandle)) Then
                        matched = True
                    End If
                Next
            Next

            Assert.True(matched)
        End Sub
    End Class
End Namespace
