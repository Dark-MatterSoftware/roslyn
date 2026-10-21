' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Reflection
Imports System.Reflection.Metadata
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class SharedAbstractImplementationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        Private Const Shapes As String = "
Interface IShape(Of TSelf As IShape(Of TSelf))
    Shared MustOverride Function Create() As TSelf
    Function Name() As String
End Interface

Class Circle
    Implements IShape(Of Circle)

    Public Shared Function Create() As Circle Implements IShape(Of Circle).Create
        Return New Circle()
    End Function

    Public Function Name() As String Implements IShape(Of Circle).Name
        Return ""circle""
    End Function
End Class

Structure Square
    Implements IShape(Of Square)

    Public Shared Function Create() As Square Implements IShape(Of Square).Create
        Return New Square()
    End Function

    Public Function Name() As String Implements IShape(Of Square).Name
        Return ""square""
    End Function
End Structure
"

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub VbDeclaredInterface_TypeLoadsAndRuns()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Function Make(Of T As IShape(Of T))() As String
        Return T.Create().Name()
    End Function

    Sub Main()
        System.Console.Write(Make(Of Circle)() & "" "" & Make(Of Square)())
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="circle square", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <ConditionalFact(GetType(CoreClrOnly))>
        Public Sub DotNetInterface_IParsable()
            Dim compilation = CreateCompilation("
Imports System

Structure Money
    Implements IParsable(Of Money)

    Public Amount As Decimal

    Public Shared Function Parse(s As String, provider As IFormatProvider) As Money Implements IParsable(Of Money).Parse
        Return New Money With {.Amount = Decimal.Parse(s, Globalization.CultureInfo.InvariantCulture)}
    End Function

    Public Shared Function TryParse(s As String, provider As IFormatProvider, ByRef result As Money) As Boolean Implements IParsable(Of Money).TryParse
        result = Parse(s, provider)
        Return True
    End Function
End Structure

Module Program
    Sub Main()
        Dim parsed As Money = Nothing
        Money.TryParse(""2.25"", Nothing, parsed)
        System.Console.Write((Money.Parse(""1.5"", Nothing).Amount + parsed.Amount).ToString(Globalization.CultureInfo.InvariantCulture))

        Dim map = GetType(Money).GetInterfaceMap(GetType(IParsable(Of Money)))
        For i = 0 To map.InterfaceMethods.Length - 1
            System.Console.Write("" "" & map.InterfaceMethods(i).Name & ""="" & map.TargetMethods(i).DeclaringType.Name & ""."" & map.TargetMethods(i).Name)
        Next
    End Sub
End Module
", targetFramework:=TargetFramework.Net90, options:=TestOptions.ReleaseExe.WithOptionInfer(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="3.75 Parse=Money.Parse TryParse=Money.TryParse", verify:=Verification.Skipped).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub MetadataFlagsAndMethodImplementations()
            Dim compilation = CreateCompilation(Shapes, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            compilation.AssertNoDiagnostics()

            Dim image = compilation.EmitToArray()
            Using metadata = ModuleMetadata.CreateFromImage(image)
                Dim reader = metadata.MetadataReader
                Dim flags = New Dictionary(Of String, MethodAttributes)()
                For Each typeHandle In reader.TypeDefinitions
                    Dim type = reader.GetTypeDefinition(typeHandle)
                    For Each methodHandle In type.GetMethods()
                        Dim method = reader.GetMethodDefinition(methodHandle)
                        flags(reader.GetString(type.Name) & "." & reader.GetString(method.Name)) = method.Attributes
                    Next
                Next

                Dim abstractVirtualStatic = MethodAttributes.Abstract Or MethodAttributes.Virtual Or MethodAttributes.Static
                Assert.Equal(abstractVirtualStatic, flags("IShape`1.Create") And abstractVirtualStatic)
                For Each implementation In {"Circle.Create", "Square.Create"}
                    Assert.Equal(MethodAttributes.Static, flags(implementation) And (MethodAttributes.Static Or MethodAttributes.Virtual Or MethodAttributes.Final Or MethodAttributes.NewSlot))
                Next

                Dim implemented = reader.TypeDefinitions.
                    SelectMany(Function(t) reader.GetTypeDefinition(t).GetMethodImplementations()).
                    Select(Function(h) reader.GetMethodImplementation(h)).
                    Select(Function(m) reader.GetString(reader.GetTypeDefinition(m.Type).Name) & "." & reader.GetString(reader.GetMethodDefinition(CType(m.MethodBody, MethodDefinitionHandle)).Name)).
                    ToArray()
                Assert.Contains("Circle.Create", implemented)
                Assert.Contains("Square.Create", implemented)
            End Using
        End Sub
    End Class
End Namespace
