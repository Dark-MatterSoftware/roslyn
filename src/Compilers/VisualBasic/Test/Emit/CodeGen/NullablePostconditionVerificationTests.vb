' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullablePostconditionVerificationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub NotNull_ByRef_ValidAndInvalid()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Sub Ready(<NotNull> ByRef value As String?)
        value = ""ready""
    End Sub
    Public Sub Clear(<NotNull> ByRef value As String?)
        value = Nothing
    End Sub
    Public Sub Miss(<NotNull> ByRef value As String?)
        If value Is Nothing Then
            Return
        End If
        value = ""x""
    End Sub
End Class
")
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_ParameterDisallowsNull AndAlso d.GetMessage().Contains("Ready")))
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ParameterDisallowsNull AndAlso d.GetMessage().Contains("value"))
        End Sub

        <Fact>
        Public Sub Return_AndFunctionVariable()
            Dim compilation = Compile("
Public Class Api
    Public Function Early() As String
        Return Nothing
    End Function
    Public Function Named() As String
        Named = Nothing
        Exit Function
    End Function
    Public Function Good() As String
        Return ""x""
    End Function
    Public Function Maybe() As String?
        Return Nothing
    End Function
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_PossibleNullReturn)
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_PossibleNullReturn))
        End Sub

        <Fact>
        Public Sub NotNullWhen_TrueAndFalse()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Function BadTrue(<NotNullWhen(True)> ByRef name As String?) As Boolean
        name = Nothing
        Return True
    End Function
    Public Function GoodTrue(<NotNullWhen(True)> ByRef name As String?) As Boolean
        name = ""x""
        Return True
    End Function
    Public Function BadFalse(<NotNullWhen(False)> ByRef name As String?) As Boolean
        name = Nothing
        Return False
    End Function
    Public Function GoodFalse(<NotNullWhen(False)> ByRef name As String?) As Boolean
        name = ""x""
        Return False
    End Function
End Class
")
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) d.Code = ERRID.WRN_ParameterConditionallyDisallowsNull))
        End Sub

        <Fact>
        Public Sub NotNullIfNotNull_ReturnInput()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Function Normalize(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        Return value
    End Function
    Public Function Broken(value As String?) As <NotNullIfNotNull(NameOf(value))> String?
        If value IsNot Nothing Then
            Return Nothing
        End If
        Return value
    End Function
    Public Function Missing(value As String?) As <NotNullIfNotNull(""missing"")> String?
        Return value
    End Function
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ReturnNotNullIfNotNull)
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_BadNullableParameterName)
        End Sub

        <Fact>
        Public Sub DoesNotReturn_BodyIsNotSelfProving()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    <DoesNotReturn>
    Public Sub Fail()
        Fail()
    End Sub
    <DoesNotReturn>
    Public Sub Sometimes(flag As Boolean)
        If flag Then
            Throw New System.Exception()
        End If
    End Sub
End Class
")
            Assert.False(compilation.GetDiagnostics().Any(Function(d) d.Code = ERRID.WRN_ParameterDisallowsNull))
            Assert.Equal(2, compilation.GetDiagnostics().Count(Function(d) CInt(d.Code) = 42532))
        End Sub

        <Fact>
        Public Sub Finally_ClearsByRef()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Sub Clear(<NotNull> ByRef value As String?)
        Try
            value = ""x""
            Return
        Finally
            value = Nothing
        End Try
    End Sub
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ParameterDisallowsNull)
        End Sub

        <Fact>
        Public Sub DirectRecursion_DoesNotProveNotNull()
            Dim compilation = Compile(Attributes() & "
Public Class Api
    Public Sub Looping(<NotNull> ByRef value As String?)
        Looping(value)
    End Sub
End Class
")
            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.WRN_ParameterDisallowsNull)
        End Sub

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function

        Private Shared Function Attributes() As String
            Return "
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.ReturnValue, AllowMultiple:=True)>
    Public NotInheritable Class NotNullIfNotNullAttribute
        Inherits Attribute
        Public Sub New(parameterName As String)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Parameter)>
    Public NotInheritable Class NotNullWhenAttribute
        Inherits Attribute
        Public Sub New(returnWhen As Boolean)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Method)>
    Public NotInheritable Class DoesNotReturnAttribute
        Inherits Attribute
    End Class
End Namespace
"
        End Function
    End Class
End Namespace
