' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.IO
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class EnumConstraintTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub EnumConstraint_OnExperimentalVersion()
            Dim compilation = CreateCompilation("
Class C(Of T As {Structure, Enum})
End Class

Class D(Of T As Enum)
End Class

Module M
    Sub Use()
        Dim a As C(Of System.DayOfWeek) = Nothing
        Dim b As C(Of Integer) = Nothing
        Dim c As C(Of System.Enum) = Nothing
        Dim d As D(Of System.DayOfWeek) = Nothing
        Dim e As D(Of System.Enum) = Nothing
        Dim f As D(Of Integer) = Nothing
        Dim g As D(Of String) = Nothing
    End Sub
End Module
", parseOptions:=VbNext)

            Dim boxed = compilation.GlobalNamespace.GetTypeMembers("C").Single()
            Assert.True(boxed.TypeParameters(0).HasValueTypeConstraint)
            Assert.Equal(SpecialType.System_Enum, boxed.TypeParameters(0).ConstraintTypes.Single().SpecialType)

            Dim open = compilation.GlobalNamespace.GetTypeMembers("D").Single()
            Assert.False(open.TypeParameters(0).HasValueTypeConstraint)
            Assert.Equal(SpecialType.System_Enum, open.TypeParameters(0).ConstraintTypes.Single().SpecialType)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_GenericConstraintNotSatisfied2, "Integer").WithArguments("Integer", "System.Enum").WithLocation(11, 23),
                Diagnostic(ERRID.ERR_BadTypeArgForStructConstraint2, "System.Enum").WithArguments("System.Enum", "T").WithLocation(12, 23),
                Diagnostic(ERRID.ERR_GenericConstraintNotSatisfied2, "Integer").WithArguments("Integer", "System.Enum").WithLocation(15, 23),
                Diagnostic(ERRID.ERR_GenericConstraintNotSatisfied2, "String").WithArguments("String", "System.Enum").WithLocation(16, 23))
        End Sub

        <Fact>
        Public Sub EnumConstraint_OrderIndependent()
            Dim compilation = CreateCompilation("
Class C(Of T As {Enum, Structure})
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim typeParameter = compilation.GlobalNamespace.GetTypeMembers("C").Single().TypeParameters(0)
            Assert.True(typeParameter.HasValueTypeConstraint)
            Assert.Equal(SpecialType.System_Enum, typeParameter.ConstraintTypes.Single().SpecialType)
        End Sub

        <Fact>
        Public Sub EnumConstraint_MethodTypeParameter()
            Dim compilation = CreateCompilation("
Module M
    Sub Use(Of T As {Structure, Enum})(value As T)
    End Sub

    Sub CallSite()
        Use(System.DayOfWeek.Monday)
        Use(1)
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_GenericConstraintNotSatisfied2, "Use").WithArguments("Integer", "System.Enum").WithLocation(8, 9))
        End Sub

        <Fact>
        Public Sub EnumConstraint_TypeParameterArgument()
            Dim compilation = CreateCompilation("
Module M
    Sub Outer(Of T As Enum)(value As T)
        Inner(value)
    End Sub

    Sub Inner(Of U As Enum)(value As U)
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub EnumConstraint_ClassConstraintStillConflicts()
            Dim compilation = CreateCompilation("
Class C(Of T As {Class, Enum})
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_RefAndClassTypeConstrCombined, "Enum"))
        End Sub

        <Fact>
        Public Sub EnumConstraint_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class C(Of T As {Structure, Enum})
End Class

Class D(Of T As Enum)
End Class

Module M
    Sub Use(Of T As Enum)()
    End Sub
End Module
", parseOptions:=Vb17_13)

            Dim languageVersionError = Function(line As Integer, column As Integer) As DiagnosticDescription
                                           Return Diagnostic(ERRID.ERR_LanguageVersion, "Enum").WithArguments("17.13", "enum constraints", "vbnext").WithLocation(line, column)
                                       End Function

            compilation.VerifyDiagnostics(
                languageVersionError(2, 29),
                languageVersionError(5, 17),
                languageVersionError(9, 21))
        End Sub

        <Fact>
        Public Sub SystemEnumConstraint_StillRestrictedOn17_13()
            Dim compilation = CreateCompilation("
Class C(Of T As System.Enum)
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ConstraintIsRestrictedType1, "System.Enum").WithArguments("System.Enum").WithLocation(2, 17))
        End Sub

        <Fact>
        Public Sub SystemEnumConstraint_AllowedOnExperimentalVersion()
            Dim compilation = CreateCompilation("
Class C(Of T As System.Enum)
End Class

Module M
    Sub Use()
        Dim a As C(Of System.DayOfWeek) = Nothing
        Dim b As C(Of Integer) = Nothing
    End Sub
End Module
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_GenericConstraintNotSatisfied2, "Integer").WithArguments("Integer", "System.Enum").WithLocation(8, 23))
        End Sub

        <Fact>
        Public Sub EnumConstraint_MetadataRoundTrip()
            Dim library = CreateCompilation("
Public Class Holder(Of T As {Structure, Enum})
End Class
", parseOptions:=VbNext, assemblyName:="EnumConstraintLib")

            library.VerifyDiagnostics()

            Using stream As New MemoryStream()
                Dim emitResult = library.Emit(stream)
                Assert.True(emitResult.Success)
                stream.Position = 0

                Dim reference = AssemblyMetadata.CreateFromStream(stream).GetReference()
                Dim consumer = CreateCompilation("
Class Use
    Shared Function Ok() As Holder(Of System.DayOfWeek)
        Return Nothing
    End Function

    Shared Function Bad() As Holder(Of Integer)
        Return Nothing
    End Function
End Class
", references:={reference}, parseOptions:=Vb17_13)

                consumer.VerifyDiagnostics(
                    Diagnostic(ERRID.ERR_GenericConstraintNotSatisfied2, "Holder(Of Integer)").WithArguments("Integer", "System.Enum").WithLocation(7, 30))
            End Using
        End Sub
    End Class

End Namespace
