' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class CompactMemberTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub Compact_AllThreeMemberKinds()
            Dim compilation = CreateCompilation("
Class Customer
    Private ReadOnly _name As String = ""Ada""

    ReadOnly Property Name As String = Get _name

    Function Twice(x As Integer) As Integer = Return x * 2

    Sub Log(message As String) = Call System.Console.Write(message)

    Shared Sub Main()
        Dim c = New Customer()
        c.Log(c.Name)
        c.Log(c.Twice(21).ToString())
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Ada42").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Compact_MembersMixWithBlockMembers()
            Dim compilation = CreateCompilation("
Interface IShape
    Function Area() As Integer
End Interface
Class Square
    Implements IShape
    Private ReadOnly _side As Integer = 3
    Function Area() As Integer Implements IShape.Area = Return _side * _side
    Shared Function Describe(shape As IShape) As String
        Return ""Area "" & shape.Area()
    End Function
    Shared ReadOnly Property Unit As String = Get ""cm""
    Shared Sub Main() = Call System.Console.Write(Describe(New Square()) & Unit)
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Area 9cm").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Compact_AsyncFunctionAwaits()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks
Class Program
    Shared Async Function GetValueAsync() As Task(Of Integer) = Return Await Task.FromResult(5)
    Shared Sub Main() = Call System.Console.Write(GetValueAsync().Result)
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Compact_DebugBuildEmits()
            Dim compilation = CreateCompilation("
Imports System.Threading.Tasks
Class Program
    Shared ReadOnly Property Name As String = Get ""Ada""
    Shared Function Twice(x As Integer) As Integer = Return x * 2
    Shared Async Function GetValueAsync() As Task(Of Integer) = Return Await Task.FromResult(Twice(3))
    Shared Sub Log(message As String) = Call System.Console.Write(message)
    Shared Sub Main()
        Log(Name & GetValueAsync().Result)
    End Sub
End Class
", options:=TestOptions.DebugExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="Ada6").VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Compact_SyntaxShape()
            Dim source = "
Class C
    Function F() As Integer = Return 1
    Sub S() = Call F()
    ReadOnly Property P As Integer = Get 2
End Class
"
            Dim tree = VisualBasicSyntaxTree.ParseText(source, VbNext)
            Assert.Empty(tree.GetDiagnostics())
            Assert.Equal(source, tree.GetRoot().ToFullString())

            Dim members = DirectCast(tree.GetRoot(), CompilationUnitSyntax).Members.OfType(Of ClassBlockSyntax)().Single().Members

            Dim f = DirectCast(members(0), MethodBlockSyntax)
            Assert.Equal(SyntaxKind.FunctionBlock, f.Kind())
            Assert.False(f.SubOrFunctionStatement.EqualsToken.IsMissing)
            Assert.Equal(SyntaxKind.ReturnStatement, f.Statements.Single().Kind())
            Assert.True(f.EndBlockStatement.IsMissing)

            Dim s = DirectCast(members(1), MethodBlockSyntax)
            Assert.Equal(SyntaxKind.SubBlock, s.Kind())
            Assert.Equal(SyntaxKind.CallStatement, s.Statements.Single().Kind())
            Assert.True(s.EndBlockStatement.IsMissing)

            Dim p = DirectCast(members(2), PropertyBlockSyntax)
            Assert.False(p.PropertyStatement.EqualsToken.IsMissing)
            Assert.Null(p.PropertyStatement.Initializer)
            Dim getter = p.Accessors.Single()
            Assert.Equal(SyntaxKind.GetAccessorBlock, getter.Kind())
            Assert.Equal("Get", getter.AccessorStatement.AccessorKeyword.Text)
            Assert.Equal(SyntaxKind.ReturnStatement, getter.Statements.Single().Kind())
            Assert.True(p.EndPropertyStatement.IsMissing)
        End Sub

        <Fact>
        Public Sub Compact_AutoPropertyInitializerKeepsItsMeaning()
            Dim compilation = CreateCompilation("
Class Program
    Shared ReadOnly Property Count As Integer = 5
    Shared Sub Main()
        System.Console.Write(Count)
    End Sub
End Class
", options:=TestOptions.ReleaseExe, parseOptions:=VbNext)

            CompileAndVerify(compilation, expectedOutput:="5").VerifyDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim prop = tree.GetRoot().DescendantNodes().OfType(Of PropertyStatementSyntax)().Single()
            Assert.Equal(SyntaxKind.None, prop.EqualsToken.Kind())
            Assert.NotNull(prop.Initializer)
            Assert.IsNotType(Of PropertyBlockSyntax)(prop.Parent)
        End Sub

        <Fact>
        Public Sub Compact_WrongKeywordIsAnError()
            Dim compilation = CreateCompilation("
Class C
    Function F() As Integer = Call G()
    Function G() As Integer = Return 1
    Sub S() = Return F()
    ReadOnly Property P As Integer = Return 1
End Class
", parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected>
BC37386: A compact property body starts with '= Get', a compact Function body with '= Return', and a compact Sub body with '= Call'.
    Function F() As Integer = Call G()
                              ~~~~
BC37386: A compact property body starts with '= Get', a compact Function body with '= Return', and a compact Sub body with '= Call'.
    Sub S() = Return F()
              ~~~~~~
BC37386: A compact property body starts with '= Get', a compact Function body with '= Return', and a compact Sub body with '= Call'.
    ReadOnly Property P As Integer = Return 1
                                     ~~~~~~
</expected>)
        End Sub

        <Fact>
        Public Sub Compact_RejectedOn17_13()
            Dim compilation = CreateCompilation("
Class C
    Function F() As Integer = Return 1
End Class
", parseOptions:=Vb17_13)

            Assert.Contains(compilation.GetDiagnostics(), Function(d) d.Code = ERRID.ERR_ExpectedEOS)
            Dim tree = compilation.SyntaxTrees.Single()
            Assert.Empty(tree.GetRoot().DescendantNodes().OfType(Of MethodStatementSyntax)().Where(Function(m) m.EqualsToken.Kind() <> SyntaxKind.None))
        End Sub

        <Fact>
        Public Sub Compact_IncrementalEditMatchesFullParse()
            Dim source = "
Class C
    Function F() As Integer = Return 1
    Sub S() = Call F()
    ReadOnly Property P As Integer = Get 2
    Sub M()
    End Sub
End Class
"
            Dim tree = VisualBasicSyntaxTree.ParseText(source, VbNext)
            Dim position = source.IndexOf("Return 1", StringComparison.Ordinal) + "Return ".Length
            Dim changedText = tree.GetText().WithChanges(New TextChange(New TextSpan(position, 1), "41"))

            Dim incremental = tree.WithChangedText(changedText)
            Dim full = VisualBasicSyntaxTree.ParseText(changedText, VbNext)

            Assert.True(incremental.GetRoot().IsEquivalentTo(full.GetRoot()))
            Assert.Equal(full.GetRoot().ToFullString(), incremental.GetRoot().ToFullString())
            Assert.Empty(incremental.GetDiagnostics())
        End Sub

    End Class

End Namespace
