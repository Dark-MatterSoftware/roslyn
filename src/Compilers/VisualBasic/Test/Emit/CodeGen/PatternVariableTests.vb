' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class PatternVariableTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Shared ReadOnly StrictOptions As VisualBasicCompilationOptions =
            TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(False).WithOptionExplicit(True)

        Private Const Shapes As String = "
Public MustInherit Class Shape
End Class

Public Class Circle
    Inherits Shape
    Public Radius As Double
    Public Sub New(radius As Double)
        Me.Radius = radius
    End Sub
End Class

Public Class Square
    Inherits Shape
    Public Property Side As Double
    Public Sub New(side As Double)
        Me.Side = side
    End Sub
End Class

Public Class Point
    Public ReadOnly X As Integer
    Public ReadOnly Y As Integer
    Public Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub
    Public Sub Deconstruct(ByRef x As Integer, ByRef y As Integer)
        x = Me.X
        y = Me.Y
    End Sub
End Class
"

        Private Function CompileAndRun(source As String, expectedOutput As String, Optional options As VisualBasicCompilationOptions = Nothing) As CompilationVerifier
            Dim compilation = CreateCompilation(Shapes & source, options:=If(options, StrictOptions), parseOptions:=VbNext)
            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput).VerifyDiagnostics()
        End Function

        <Fact>
        Public Sub Case_IntoNamesTheTestedValue()
            CompileAndRun("
Module Program
    Function Describe(shape As Shape) As String
        Select Case shape
            Case Is TypeOf Circle Into c
                Return ""circle "" & c.Radius.ToString()
            Case Is TypeOf Square Into s
                Return ""square "" & s.Side.ToString()
            Case Else
                Return ""other""
        End Select
    End Function

    Sub Main()
        System.Console.Write(Describe(New Circle(2)) & "", "")
        System.Console.Write(Describe(New Square(3)) & "", "")
        System.Console.Write(Describe(Nothing))
    End Sub
End Module
", "circle 2, square 3, other")
        End Sub

        <Fact>
        Public Sub Case_IntoUnboxesAValueType()
            CompileAndRun("
Module Program
    Function Describe(value As Object) As String
        Select Case value
            Case Is TypeOf Integer Into n
                Return (n + 1).ToString()
            Case Is TypeOf String Into s
                Return s.ToUpperInvariant()
        End Select
        Return ""none""
    End Function

    Sub Main()
        System.Console.Write(Describe(41) & "" "" & Describe(""abc"") & "" "" & Describe(1.5))
    End Sub
End Module
", "42 ABC none")
        End Sub

        <Fact>
        Public Sub Case_IntoWithFiltersAndPositionalTest()
            CompileAndRun("
Module Program
    Function Describe(value As Object) As String
        Select Case value
            Case Is TypeOf Square Into s AndAlso .Side Is > 10
                Return ""big square "" & s.Side.ToString()
            Case Is TypeOf Point(0, 0) Into origin
                Return ""origin "" & origin.X.ToString()
            Case Is TypeOf Point Into p
                Return ""point "" & p.Y.ToString()
        End Select
        Return ""none""
    End Function

    Sub Main()
        System.Console.Write(Describe(New Square(20)) & "", "")
        System.Console.Write(Describe(New Square(5)) & "", "")
        System.Console.Write(Describe(New Point(0, 0)) & "", "")
        System.Console.Write(Describe(New Point(1, 7)))
    End Sub
End Module
", "big square 20, none, origin 0, point 7")
        End Sub

        <Fact>
        Public Sub Case_IntoLocalIsAssignableAndScopedToItsCase()
            CompileAndRun("
Module Program
    Sub Main()
        Dim shapes As Shape() = {New Circle(1), New Circle(2)}
        For Each shape As Shape In shapes
            Select Case shape
                Case Is TypeOf Circle Into c
                    c = New Circle(c.Radius * 10)
                    System.Console.Write(c.Radius.ToString() & "" "")
                Case Is TypeOf Square Into c
                    System.Console.Write(c.Side.ToString())
            End Select
        Next
    End Sub
End Module
", "10 20 ")
        End Sub

        <Fact>
        Public Sub Case_IntoInDebugBuild()
            CompileAndRun("
Module Program
    Sub Main()
        Dim value As Object = New Circle(4)
        Select Case value
            Case Is TypeOf Circle Into c
                System.Console.Write(c.Radius)
        End Select
    End Sub
End Module
", "4", TestOptions.DebugExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(False))
        End Sub

        <Fact>
        Public Sub Case_IntoErrors()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Function F(shape As Shape, p As Integer) As Integer
        Dim existing As Integer = 0
        Select Case shape
            Case Is TypeOf Circle Into c, Is TypeOf Square
            Case Is TypeOf Circle Into existing
            Case Is TypeOf Circle Into p
            Case Is TypeOf Circle Into F
        End Select

        Dim label As String = Select Case shape
                                  Case Is TypeOf Circle Into c
                                      ""circle""
                                  Case Else
                                      ""other""
                              End Select
        Return 0
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37395: A 'Case' whose type test names its result with 'Into' cannot have other clauses.
            Case Is TypeOf Circle Into c, Is TypeOf Square
                 ~~~~~~~~~~~~~~~~~~~~~~~
BC30616: Variable 'existing' hides a variable in an enclosing block.
            Case Is TypeOf Circle Into existing
                                       ~~~~~~~~
BC30734: 'p' is already declared as a parameter of this method.
            Case Is TypeOf Circle Into p
                                       ~
BC30290: Local variable cannot have the same name as the function containing it.
            Case Is TypeOf Circle Into F
                                       ~
BC37396: 'Into' cannot name the result of this type test here. Use it in 'Case Is TypeOf', or in the condition of an 'If' or 'ElseIf'.
                                  Case Is TypeOf Circle Into c
                                                             ~
]]></expected>)
        End Sub

        <Fact>
        Public Sub Case_IntoSemanticModel()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Function Radius(shape As Shape) As Double
        Select Case shape
            Case Is TypeOf Circle Into c
                Return c.Radius
        End Select
        Return 0
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim usage = tree.GetRoot().DescendantNodes().OfType(Of Syntax.IdentifierNameSyntax)().Single(Function(n) n.Identifier.ValueText = "c")
            Dim local = DirectCast(model.GetSymbolInfo(usage).Symbol, ILocalSymbol)
            Assert.Equal("c", local.Name)
            Assert.Equal("Circle", local.Type.ToTestDisplayString())
            Assert.Equal(tree.GetRoot().DescendantNodes().OfType(Of Syntax.TypeCaseClauseSyntax)().Single().Identifier.SpanStart, local.Locations.Single().SourceSpan.Start)
        End Sub

        <Fact>
        Public Sub Case_IntoNotAvailableInVisualBasic17_13()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Sub M(shape As Shape)
        Select Case shape
            Case Is TypeOf Circle Into c
        End Select
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            ' Case Is TypeOf itself is a vbnext form, so Visual Basic 17.13 stops before Into.
            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30239: Relational operator expected.
            Case Is TypeOf Circle Into c
                    ~
]]></expected>)
        End Sub
    End Class
End Namespace
