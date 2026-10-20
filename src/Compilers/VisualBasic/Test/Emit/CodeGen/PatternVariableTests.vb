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
        Public Sub If_IntoInIfElseIfAndAndAlso()
            CompileAndRun("
Module Program
    Function Describe(value As Object) As String
        If TypeOf value Is Square Into s AndAlso s.Side > 10 Then
            Return ""big square "" & s.Side.ToString()
        ElseIf TypeOf value Is Circle Into c Then
            Return ""circle "" & c.Radius.ToString()
        ElseIf TypeOf value Is Square Into s Then
            Return ""square "" & s.Side.ToString()
        Else
            Return ""other""
        End If
    End Function

    Sub Main()
        System.Console.Write(Describe(New Square(20)) & "", "")
        System.Console.Write(Describe(New Square(5)) & "", "")
        System.Console.Write(Describe(New Circle(1)) & "", "")
        System.Console.Write(Describe(""text""))
    End Sub
End Module
", "big square 20, square 5, circle 1, other")
        End Sub

        <Fact>
        Public Sub If_IntoOnASingleLineAndInLoops()
            CompileAndRun("
Module Program
    Sub Main()
        Dim values As Object() = {New Circle(1), ""x"", New Circle(3)}
        For Each value As Object In values
            If TypeOf value Is Circle Into c Then System.Console.Write(c.Radius.ToString() & "" "") Else System.Console.Write(""- "")
        Next
    End Sub
End Module
", "1 - 3 ")
        End Sub

        <Fact>
        Public Sub If_IntoEvaluatesTheValueOnceAndUnboxes()
            CompileAndRun("
Module Program
    Private _calls As Integer

    Function NextValue() As Object
        _calls += 1
        Return 41
    End Function

    Sub Main()
        If TypeOf NextValue() Is Integer Into n AndAlso n > 40 Then
            System.Console.Write((n + 1).ToString() & "" "" & _calls.ToString())
        End If
    End Sub
End Module
", "42 1")
        End Sub

        <Fact>
        Public Sub If_IntoCapturedByALambdaAndQueryIntoKeepsItsMeaning()
            CompileAndRun("
Module Program
    Sub Main()
        Dim shapes As Object() = {New Circle(2), New Square(1), New Circle(5)}
        Dim value As Object = shapes(2)
        If TypeOf value Is Circle Into c AndAlso (Aggregate o In shapes Where TypeOf o Is Circle Into Count()) = 2 Then
            Dim area As System.Func(Of Double) = Function() c.Radius * c.Radius
            System.Console.Write(area().ToString())
        End If
    End Sub
End Module
", "25", StrictOptions.WithGlobalImports(GlobalImport.Parse("System.Linq")))
        End Sub

        <Fact>
        Public Sub If_IntoErrors()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Sub M(value As Object, other As Object)
        If TypeOf value Is Circle Into c Then
        Else
            System.Console.Write(c)
        End If
        System.Console.Write(c)

        If TypeOf value IsNot Circle Into a Then
        End If
        If TypeOf value Is {Circle, Square} Into b Then
        End If
        If Not TypeOf value Is Circle Into d Then
        End If
        If TypeOf value Is Circle Into e OrElse True Then
        End If
        If TypeOf value Is Circle Into x AndAlso TypeOf other Is Square Into x Then
        End If
        If y.Radius > 0 AndAlso TypeOf value Is Circle Into y Then
        End If
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30451: 'c' is not declared. It may be inaccessible due to its protection level.
            System.Console.Write(c)
                                 ~
BC30451: 'c' is not declared. It may be inaccessible due to its protection level.
        System.Console.Write(c)
                             ~
BC37396: 'Into' cannot name the result of this type test here. Use it in 'Case Is TypeOf', or in the condition of an 'If' or 'ElseIf'.
        If TypeOf value IsNot Circle Into a Then
                                          ~
BC37396: 'Into' cannot name the result of this type test here. Use it in 'Case Is TypeOf', or in the condition of an 'If' or 'ElseIf'.
        If TypeOf value Is {Circle, Square} Into b Then
                                                 ~
BC37396: 'Into' cannot name the result of this type test here. Use it in 'Case Is TypeOf', or in the condition of an 'If' or 'ElseIf'.
        If Not TypeOf value Is Circle Into d Then
                                           ~
BC37396: 'Into' cannot name the result of this type test here. Use it in 'Case Is TypeOf', or in the condition of an 'If' or 'ElseIf'.
        If TypeOf value Is Circle Into e OrElse True Then
                                       ~
BC30288: Local variable 'x' is already declared in the current block.
        If TypeOf value Is Circle Into x AndAlso TypeOf other Is Square Into x Then
                                                                             ~
BC32000: Local variable 'y' cannot be referred to before it is declared.
        If y.Radius > 0 AndAlso TypeOf value Is Circle Into y Then
           ~
]]></expected>)
        End Sub

        <Fact>
        Public Sub If_IntoSemanticModel()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Function Radius(value As Object) As Double
        If TypeOf value Is Circle Into c AndAlso c.Radius > 0 Then
            Return c.Radius
        End If
        Return 0
    End Function
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim usages = tree.GetRoot().DescendantNodes().OfType(Of Syntax.IdentifierNameSyntax)().Where(Function(n) n.Identifier.ValueText = "c").ToArray()
            Assert.Equal(2, usages.Length)
            Dim declaration = tree.GetRoot().DescendantNodes().OfType(Of Syntax.TypeOfIntoExpressionSyntax)().Single()
            For Each usage In usages
                Dim local = DirectCast(model.GetSymbolInfo(usage).Symbol, ILocalSymbol)
                Assert.Equal("Circle", local.Type.ToTestDisplayString())
                Assert.Equal(declaration.Identifier.SpanStart, local.Locations.Single().SourceSpan.Start)
            Next

            Assert.Equal("System.Boolean", model.GetTypeInfo(declaration).Type.ToTestDisplayString())
        End Sub

        <Fact>
        Public Sub If_IntoNotAvailableInVisualBasic17_13()
            Dim compilation = CreateCompilation(Shapes & "
Module Program
    Sub M(value As Object)
        If TypeOf value Is Circle Into c Then
        End If
    End Sub
End Module
", options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC36716: Visual Basic 17.13 does not support 'Into' after a type test.
        If TypeOf value Is Circle Into c Then
                                  ~~~~
]]></expected>)
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
