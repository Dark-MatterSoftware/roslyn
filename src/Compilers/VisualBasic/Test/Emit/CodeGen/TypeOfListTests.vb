' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class TypeOfListTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub TypeOf_MatchesAnyListedType()
            Dim compilation = CreateCompilation("
Class Customer
End Class
Class Order
End Class
Class Program
    Shared n As Integer
    Shared Function NextValue() As Object
        n += 1
        Return New Customer()
    End Function
    Shared Sub Main()
        If TypeOf NextValue() Is {Customer, Order} Then
            System.Console.Write(""yes"")
        Else
            System.Console.Write(""no"")
        End If
        Dim order As Object = New Order()
        If TypeOf order IsNot {Customer} Then
            System.Console.Write(""other"")
        End If
        Dim text As Object = ""hi""
        If TypeOf text Is {Customer, Order} Then
            System.Console.Write(""bad"")
        Else
            System.Console.Write(""text"")
        End If
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="yesothertext1").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TypeOf_ListRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Customer
End Class
Class Program
    Shared Sub Main()
        Dim value As Object = New Customer()
        If TypeOf value Is {Customer} Then
        End If
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_UnrecognizedType, "").WithLocation(7, 28))
        End Sub
    End Class
End Namespace
