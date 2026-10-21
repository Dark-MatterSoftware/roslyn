' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class LambdaParameterAttributeTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Const Attributes As String = "
Imports System

<AttributeUsage(AttributeTargets.Parameter)>
Public Class TagAttribute
    Inherits Attribute

    Public ReadOnly Name As String

    Public Sub New(name As String)
        Me.Name = name
    End Sub
End Class

<AttributeUsage(AttributeTargets.Method)>
Public Class MethodOnlyAttribute
    Inherits Attribute
End Class
"

        <Fact>
        Public Sub LambdaParameterAttributes_AreEmitted()
            Dim compilation = CreateCompilation({Attributes, "
Imports System
Imports System.Reflection

Module Program
    Sub Describe(method As MethodInfo)
        For Each p In method.GetParameters()
            Dim tag = DirectCast(Attribute.GetCustomAttribute(p, GetType(TagAttribute)), TagAttribute)
            Console.Write(p.Name & ""="" & If(tag Is Nothing, ""none"", tag.Name) & "" "")
        Next
    End Sub

    Sub Main()
        Dim join As Func(Of Integer, String, String) = Function(<Tag(""count"")> n As Integer, <Tag(""label"")> s As String) s & n.ToString()
        Describe(join.Method)
        Console.Write(join(2, ""x"") & "" "")

        Dim prefix = ""p""
        Dim inferred As Func(Of String, String) = Function(<Tag(""text"")> t) prefix & t
        Describe(inferred.Method)
        Console.Write(inferred(""q"") & "" "")

        Dim act As Action(Of Integer) = Sub(<Tag(""value"")> v As Integer) Console.Write(v.ToString() & "" "")
        Describe(act.Method)
        act(7)
    End Sub
End Module
"}, options:=TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="n=count s=label x2 t=text pq v=value 7").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub LambdaParameterAttributes_InSemanticModel()
            Dim compilation = CreateCompilation({Attributes, "
Imports System

Module Program
    Sub Main()
        Dim f As Func(Of Integer, Integer) = Function(<Tag(""n"")> n As Integer) n
    End Sub
End Module
"}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees(1)
            Dim model = compilation.GetSemanticModel(tree)
            Dim parameterSyntax = tree.GetRoot().DescendantNodes().OfType(Of Syntax.ParameterSyntax)().Single()
            Dim parameter = model.GetDeclaredSymbol(parameterSyntax)
            Dim attribute = parameter.GetAttributes().Single()
            Assert.Equal("TagAttribute", attribute.AttributeClass.Name)
            Assert.Equal("n", attribute.ConstructorArguments.Single().Value)
        End Sub

        <Fact>
        Public Sub LambdaParameterAttributes_Errors()
            Dim compilation = CreateCompilation({Attributes, "
Imports System

Module Program
    Sub Main()
        Dim a As Func(Of Integer, Integer) = Function(<MethodOnly> n As Integer) n
        Dim b As Func(Of Integer, Integer) = Function(<Missing> n As Integer) n
    End Sub
End Module
"}, options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30662: Attribute 'MethodOnlyAttribute' cannot be applied to 'n' because the attribute is not valid on this declaration type.
        Dim a As Func(Of Integer, Integer) = Function(<MethodOnly> n As Integer) n
                                                       ~~~~~~~~~~
BC30002: Type 'Missing' is not defined.
        Dim b As Func(Of Integer, Integer) = Function(<Missing> n As Integer) n
                                                       ~~~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub LambdaParameterAttributes_RejectedOn17_13()
            Dim compilation = CreateCompilation({Attributes, "
Imports System

Module Program
    Sub Main()
        Dim f As Func(Of Integer, Integer) = Function(<Tag(""n"")> n As Integer) n
    End Sub
End Module
"}, options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36634: Attributes cannot be applied to parameters of lambda expressions.
        Dim f As Func(Of Integer, Integer) = Function(<Tag("n")> n As Integer) n
                                                      ~~~~~~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
