' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Semantics

    Public Class ByRefLocalTests
        Inherits BasicTestBase

        <Fact>
        Public Sub ByRefOnALocalDeclarationIsAnError()
            Dim compilation = CreateCompilation("
Class C
    Private ByRef _field As Integer
    Public ByRef Event Changed As System.Action

    ByRef Sub Reset()
    End Sub

    ByRef Sub New()
    End Sub

    ByRef Declare Function GetTickCount Lib ""kernel32"" () As Integer

    ByRef Shared Operator +(a As C, b As C) As C
        Return a
    End Operator

    ByRef Function First(values As Integer()) As Integer
        Return values(0)
    End Function

    Sub M(values As Integer())
        Dim ByRef first As Integer = values(0)
        Static ByRef count As Integer
        Const ByRef limit As Integer = 3
        first += count + limit
    End Sub
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30235: 'ByRef' is not valid on a member variable declaration.
    Private ByRef _field As Integer
            ~~~~~
BC30243: 'ByRef' is not valid on an event declaration.
    Public ByRef Event Changed As System.Action
           ~~~~~
BC30242: 'ByRef' is not valid on a method declaration.
    ByRef Sub Reset()
    ~~~~~
BC30242: 'ByRef' is not valid on a method declaration.
    ByRef Sub New()
    ~~~~~
BC30244: 'ByRef' is not valid on a Declare.
    ByRef Declare Function GetTickCount Lib "kernel32" () As Integer
    ~~~~~
BC33013: Operators cannot be declared 'ByRef'.
    ByRef Shared Operator +(a As C, b As C) As C
    ~~~~~
BC30247: 'ByRef' is not valid on a local variable declaration.
        Dim ByRef first As Integer = values(0)
            ~~~~~
BC30247: 'ByRef' is not valid on a local variable declaration.
        Static ByRef count As Integer
               ~~~~~
BC30247: 'ByRef' is not valid on a local variable declaration.
        Const ByRef limit As Integer = 3
              ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub ByRefOnALocalDeclarationOn17_13()
            Dim compilation = CreateCompilation("
Class C
    Sub M(values As Integer())
        Dim ByRef first As Integer = values(0)
    End Sub
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13))

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30183: Keyword is not valid as an identifier.
        Dim ByRef first As Integer = values(0)
            ~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
