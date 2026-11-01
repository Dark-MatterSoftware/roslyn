' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class TupleDeconstructionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub TupleDeconstruction_DeclaresLocals()
            Dim compilation = CreateCompilation("
Class Program
    Shared n As Integer
    Shared Function NextPoint() As (Integer, Integer)
        n += 1
        Return (n, n + 10)
    End Function
    Shared Sub Main()
        Dim point = (X:=10, Y:=20)
        Dim (x, y) = point
        Dim (a, b, c) = (1, 2, 3)
        Dim (p, q) = NextPoint()
        System.Console.Write(x)
        System.Console.Write(y)
        System.Console.Write(a)
        System.Console.Write(b)
        System.Console.Write(c)
        System.Console.Write(p)
        System.Console.Write(q)
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="10201231111").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleDeconstruction_AssignsExistingLocals()
            Dim compilation = CreateCompilation("
Class Program
    Shared n As Integer
    Shared Function NextPoint() As (Integer, Integer)
        n += 1
        Return (n, n + 10)
    End Function
    Shared Sub Main()
        Dim x = 0
        Dim y = 0
        Dim a = 0
        Dim b = 0
        Dim c = 0
        (x, y) = NextPoint()
        (a, b, c) = (1, 2, 3)
        System.Console.Write(x)
        System.Console.Write(y)
        System.Console.Write(a)
        System.Console.Write(b)
        System.Console.Write(c)
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1111231").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_RequiresATuple()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim x = 0
        Dim y = 0
        (x, y) = 1
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DeconstructionRequiresTuple, "= 1").WithArguments("2").WithLocation(6, 16))
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim x = 0
        Dim y = 0
        (x, y) = (10, 20)
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_Syntax, "(").WithLocation(6, 9))
        End Sub

        <Fact>
        Public Sub TupleDeconstruction_RequiresATuple()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim (x, y) = 1
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DeconstructionRequiresTuple, "= 1").WithArguments("2").WithLocation(4, 20))
        End Sub

        <Theory>
        <InlineData(OptionStrict.Off)>
        <InlineData(OptionStrict.On)>
        Public Sub TupleDeconstruction_RequiresOptionInfer(strict As OptionStrict)
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim point As (Integer, Integer) = (10, 20)
        Dim (x, y) = point
    End Sub
End Class
", options:=TestOptions.ReleaseDll.WithOptionInfer(False).WithOptionStrict(strict), parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DeconstructionRequiresOptionInfer, "(x, y)").WithLocation(5, 13))
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_WorksUnderOptionInferOff()
            Dim compilation = CreateCompilation("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point(X As Integer, Y As Integer)
End Class

Class Program
    Shared Sub Main()
        Dim pair As (Integer, String) = (1, ""one"")
        Dim number As Integer, name As String
        (number, name) = pair
        Dim x As Integer, y As Integer
        (x, y) = New Point(2, 3)
        System.Console.Write(number & name & x & y)
    End Sub
End Class
", options:=TestOptions.ReleaseExe.WithOptionInfer(False).WithOptionStrict(OptionStrict.On).WithOptionExplicit(True), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1one23").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleDeconstruction_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Sub Main()
        Dim point = (10, 20)
        Dim (x, y) = point
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedIdentifier, "").WithLocation(5, 13),
                Diagnostic(ERRID.ERR_InferringNonArrayType1, "").WithArguments("(Integer, Integer)").WithLocation(5, 13),
                Diagnostic(ERRID.ERR_InitWithExplicitArraySizes, "(x, y)").WithLocation(5, 13),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "x").WithArguments("x").WithLocation(5, 14),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "y").WithArguments("y").WithLocation(5, 17),
                Diagnostic(ERRID.ERR_TypeMismatch2, "point").WithArguments("(Integer, Integer)", "Object(*,*)").WithLocation(5, 22))
        End Sub

        <Fact>
        Public Sub TupleDeconstruction_DiscardSkipsAnElement()
            Dim compilation = CreateCompilation("
Class Program
    Shared n As Integer
    Shared Function NextTriple() As (Integer, Integer, Integer)
        n += 1
        Return (n, n + 1, n + 2)
    End Function
    Shared Sub Main()
        Dim (x, , z) = NextTriple()
        Dim a = 0
        Dim c = 0
        (a, , c) = (4, 5, 6)
        System.Console.Write(x)
        System.Console.Write(z)
        System.Console.Write(a)
        System.Console.Write(c)
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="13461").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub DeconstructMethod_AssignsByRefParameters()
            Dim compilation = CreateCompilation("
Class Point
    Public X As Integer
    Public Y As Integer
    Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub
    Sub Deconstruct(ByRef a As Integer, ByRef b As Integer)
        a = X
        b = Y
    End Sub
End Class
Class Program
    Shared n As Integer
    Shared Function NextPoint() As Point
        n += 1
        Return New Point(n, n + 1)
    End Function
    Shared Sub Main()
        Dim (x, y) = NextPoint()
        Dim (a, ) = NextPoint()
        Dim q = 0
        Dim r = 0
        (q, r) = NextPoint()
        System.Console.Write(x)
        System.Console.Write(y)
        System.Console.Write(a)
        System.Console.Write(q)
        System.Console.Write(r)
        System.Console.Write(n)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="122343").VerifyDiagnostics()
        End Sub

        <Theory>
        <InlineData(OptimizationLevel.Release)>
        <InlineData(OptimizationLevel.Debug)>
        Public Sub Deconstruction_InAsyncMethod(optimization As OptimizationLevel)
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks
Class Point
    Public X As Integer
    Public Y As Integer
    Sub New(x As Integer, y As Integer)
        Me.X = x
        Me.Y = y
    End Sub
    Sub Deconstruct(ByRef a As Integer, ByRef b As Integer)
        a = X
        b = Y
    End Sub
End Class
Module Program
    Async Function Run() As Task
        For i = 1 To 2
            Await Task.Yield()
            Dim (x, y) = New Point(i, i + 1)
            Dim (s, t) = (i.ToString(), i * 10)
            Dim q As Long = 0
            Dim r = 0
            (q, r) = New Point(i, i)
            Await Task.Yield()
            System.Console.Write(x & y & s & t & q & r & "" "")
        Next
    End Function
    Sub Main()
        Run().GetAwaiter().GetResult()
    End Sub
End Module
", options:=TestOptions.ReleaseExe.WithOptimizationLevel(optimization), parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="1211011 2322022 ").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub DeconstructMethod_Extension()
            Dim compilation = CreateCompilation("
Imports System.Runtime.CompilerServices
Class Point
    Public X As Integer
    Public Y As Integer
End Class
Module PointExtensions
    <Extension>
    Sub Deconstruct(value As Point, ByRef a As Integer, ByRef b As Integer)
        a = value.X
        b = value.Y
    End Sub
End Module
Class Program
    Shared Sub Main()
        Dim point As New Point()
        point.X = 7
        point.Y = 8
        Dim (x, y) = point
        System.Console.Write(x)
        System.Console.Write(y)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="78").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_PropertiesFieldsAndElements()
            Dim compilation = CreateCompilation("
Imports System
Imports System.Collections.Generic

Class Box
    Public Width As Integer
    Public Property Height As Integer
End Class

Structure Spot
    Public X As Integer
    Public Y As Integer
End Structure

Class Program
    Shared Log As String = """"

    Shared Function Pick(label As String, value As Integer) As Integer
        Log &= label
        Return value
    End Function

    Shared Sub Main()
        Dim box As New Box()
        Dim cells(2) As Integer
        Dim spot As Spot
        Dim spots(1) As Spot
        Dim list As New List(Of String) From {""a"", ""b""}
        (box.Width, box.Height, cells(1), spot.X, spots(1).Y, list(0)) = (1, 2, 3, 4, 5, ""z"")
        Console.Write(box.Width & box.Height & cells(1) & spot.X & spots(1).Y & list(0) & "" "")
        (cells(Pick(""i"", 0)), cells(Pick(""j"", 2))) = (Pick(""v"", 7), Pick(""w"", 9))
        Console.Write(cells(0) & cells(2) & "" "" & Log)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="12345z 79 ijvw").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_DeconstructIntoProperties()
            Dim compilation = CreateCompilation("
Class Point
    Public Sub Deconstruct(ByRef x As Integer, ByRef y As Integer)
        x = 3
        y = 4
    End Sub
End Class

Class Box
    Public Width As Integer
    Public Property Height As Integer
End Class

Class Program
    Shared Sub Main()
        Dim box As New Box()
        (box.Width, box.Height) = New Point()
        System.Console.Write(box.Width & box.Height)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="34").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_TargetsThatCannotBeAssigned()
            Dim compilation = CreateCompilation("
Class Box
    Public ReadOnly Property Size As Integer
    Public Width As Integer
End Class

Class Program
    Shared Function Value() As Integer
        Return 0
    End Function

    Shared Sub Main()
        Dim box As New Box()
        (box.Size, box.Width) = (1, 2)
        (Value(), box.Width) = (1, 2)
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NoSetProperty1, "box.Size").WithArguments("Size").WithLocation(14, 10),
                Diagnostic(ERRID.ERR_LValueRequired, "Value()").WithLocation(15, 10))
        End Sub

        Private Const NestedSource As String = "
Option Strict On
Imports System

Class Point
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

Class Box
    Public Width As Integer
    Public Property Height As Integer
    Public Property Label As String
End Class
"

        <Fact>
        Public Sub NestedDeconstruction_Dim()
            Dim compilation = CreateCompilation(NestedSource & "
Class Program
    Shared Function Nested() As ((Integer, Integer), String)
        Return ((2, 3), ""pair"")
    End Function

    Shared Sub Main()
        Dim ((x, y), label) = Nested()
        Dim ((first, ), ) = Nested()
        Dim (p, (q, r)) = (1, New Point(4, 5))
        Dim ((a, b), (c, d)) = ((1, 2), (3, 4))
        Dim text As String = label
        Dim total As Integer = x + y
        Console.Write(total & text & first & p & q & r & a & b & c & d)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5pair21451234").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NestedDeconstruction_AssignmentAndEmptySlotsWithMembers()
            Dim compilation = CreateCompilation(NestedSource & "
Class Program
    Shared Function Nested() As ((Integer, Integer), String)
        Return ((2, 3), ""pair"")
    End Function

    Shared Sub Main()
        Dim box As New Box()
        Dim m As Integer
        Dim n As Integer
        ((m, n), box.Label) = Nested()
        (box.Width, ) = (9, ""skip"")
        ((box.Height, ), ) = Nested()
        Console.Write(m & n & box.Label & box.Width & box.Height)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="23pair92").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub NestedDeconstruction_NestedListOfAnotherLength()
            Dim compilation = CreateCompilation(NestedSource & "
Class Program
    Shared Function Nested() As ((Integer, Integer), String)
        Return ((2, 3), ""pair"")
    End Function

    Shared Sub Main()
        Dim ((x, y, z), label) = Nested()
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DeconstructionRequiresTuple, "(x, y, z)").WithArguments("3").WithLocation(32, 14))
        End Sub

        <Fact>
        Public Sub NestedDeconstruction_SemanticModel()
            Dim compilation = CreateCompilation(NestedSource & "
Class Program
    Shared Function Nested() As ((Integer, Integer), String)
        Return ((2, 3), ""pair"")
    End Function

    Shared Sub Main()
        Dim ((x, y), label) = Nested()
        Console.Write(x)
        Console.Write(label)
    End Sub
End Class
", parseOptions:=VbNext)

            compilation.VerifyDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim names = tree.GetRoot().DescendantNodes().OfType(Of Syntax.DeconstructionSlotSyntax)().Where(Function(s) s.Name IsNot Nothing).Select(Function(s) s.Name).ToArray()
            Assert.Equal({"x As System.Int32", "y As System.Int32", "label As System.String"},
                         names.Select(Function(n) model.GetDeclaredSymbol(n).ToTestDisplayString()).ToArray())
            Dim main = tree.GetRoot().DescendantNodes().OfType(Of Syntax.MethodBlockSyntax)().Last()
            Dim usages = main.DescendantNodes().OfType(Of Syntax.IdentifierNameSyntax)().Where(Function(i) i.Identifier.ValueText = "x" OrElse i.Identifier.ValueText = "label").ToArray()
            Assert.Equal({"x As System.Int32", "label As System.String"}, usages.Select(Function(u) model.GetSymbolInfo(u).Symbol.ToTestDisplayString()).ToArray())
        End Sub

        <Fact>
        Public Sub NestedDeconstruction_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Program
    Shared Function Nested() As ((Integer, Integer), String)
        Return ((2, 3), ""pair"")
    End Function

    Shared Sub Main()
        Dim ((x, y), label) = Nested()
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_ExpectedIdentifier, "").WithLocation(8, 13),
                Diagnostic(ERRID.ERR_InferringNonArrayType1, "").WithArguments("((Integer, Integer), String)").WithLocation(8, 13),
                Diagnostic(ERRID.ERR_InitWithExplicitArraySizes, "((x, y), label)").WithLocation(8, 13),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "x").WithArguments("x").WithLocation(8, 15),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "y").WithArguments("y").WithLocation(8, 18),
                Diagnostic(ERRID.ERR_NameNotDeclared1, "label").WithArguments("label").WithLocation(8, 22),
                Diagnostic(ERRID.ERR_TypeMismatch2, "Nested()").WithArguments("((Integer, Integer), String)", "Object(*,*)").WithLocation(8, 31))
        End Sub

        <Fact>
        Public Sub TupleDeconstructionAssignment_MembersRejectedOn17_13()
            Dim compilation = CreateCompilation("
Class Box
    Public Width As Integer
End Class

Class Program
    Shared Sub Main()
        Dim box As New Box()
        Dim x As Integer
        (box.Width, x) = (1, 2)
    End Sub
End Class
", parseOptions:=Vb17_13)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.WRN_UnusedLocal, "x").WithArguments("x").WithLocation(9, 13),
                Diagnostic(ERRID.ERR_Syntax, "(").WithLocation(10, 9))
        End Sub
    End Class
End Namespace
