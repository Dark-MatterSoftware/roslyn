' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    ''' <summary>
    ''' Analyzers ask for the IOperation tree of every method body. Some vbnext expressions bind to
    ''' nodes that only lowering produced before, so these tests build that tree for each of them.
    ''' </summary>
    Public Class VbNextOperationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub SelectCaseExpressionAndPatterns()
            VerifyOperationTrees("
Class Customer
    Public Balance As Integer
End Class
Class Program
    Shared Function Describe(order As Integer) As String
        Dim label = Select Case order
                        Case 1
                            ""one""
                        Case Else
                            ""other""
                    End Select
        Return label
    End Function
    Shared Function Shape(values As Integer(), value As Object) As String
        Select Case values
            Case Is {1, 2}
                Return ""pair""
        End Select
        Select Case value
            Case Is TypeOf Customer AndAlso .Balance Is > 0
                Return ""customer""
        End Select
        Return ""other""
    End Function
End Class
")
        End Sub

        <Fact>
        Public Sub IndexFromEndAndRange()
            VerifyOperationTrees("
Class Program
    Shared Function Last(items As Integer()) As Integer
        Return items(^1)
    End Function
    Shared Function Middle(items As Integer()) As Integer()
        Return items(1..^1)
    End Function
End Class
")
        End Sub

        <Fact>
        Public Sub CollectionExpressionWithSpread()
            VerifyOperationTrees("
Imports System.Collections.Generic
Module Program
    Function Values(extras As List(Of Integer)) As List(Of Integer)
        Dim result As List(Of Integer) = (1, .. extras, 4)
        Return result
    End Function
End Module
")
        End Sub

        <Fact>
        Public Sub ForEachWithIndex()
            VerifyOperationTrees("
Class Program
    Shared Sub Main()
        For Each item With index In {""a"", ""b""}
            System.Console.Write(index)
            System.Console.Write(item)
        Next
    End Sub
End Class
")
        End Sub

        <Fact>
        Public Sub CoalescingAndConditionalAssignment()
            VerifyOperationTrees("
Class Box
    Public Field As Integer
End Class
Class Program
    Shared Sub Main(box As Box)
        Dim text As String = Nothing
        text ?= ""a""
        box?.Field = 2
    End Sub
End Class
")
        End Sub

        <Fact>
        Public Sub RecordWith()
            VerifyOperationTrees("
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Point
    Public Property X As Integer
    Public InitOnly Property Y As Integer
End Class

Module Program
    Function Moved(a As Point) As Point
        Dim b = a With {.Y = 3}
        Return b
    End Function
End Module
")
        End Sub

        <Fact>
        Public Sub WithDimAndTupleDeconstruction()
            VerifyOperationTrees("
Class Customer
    Public Name As String
End Class
Class Program
    Shared Function NextPoint() As (Integer, Integer)
        Return (1, 2)
    End Function
    Shared Sub Main()
        With Dim customer = New Customer()
            .Name = ""Ada""
        End With
        Dim (x, y) = NextPoint()
        (x, y) = NextPoint()
        System.Console.Write(x + y)
    End Sub
End Class
")
        End Sub

        <Fact>
        Public Sub PositionalPatternAndTypeOfList()
            VerifyOperationTrees("
Class Point
    Public X As Integer
    Public Y As Integer
    Public Sub Deconstruct(ByRef x As Integer, ByRef y As Integer)
        x = Me.X
        y = Me.Y
    End Sub
End Class
Class Program
    Shared Function Describe(value As Object) As String
        If TypeOf value Is {Point, String} Then
            Select Case value
                Case Is TypeOf Point(1, 2)
                    Return ""origin""
            End Select
        End If
        Return ""other""
    End Function
End Class
")
        End Sub

        <Fact>
        Public Sub RecentFormsHaveOperationsForEveryNode()
            Dim source = "
Imports System.Collections.Generic
Imports Pair = (Integer, String)

Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Structure Spot
    Public X As Integer
    Public Y As Integer
End Structure

Class Box
    Public Count As Integer
    Public Name As String
End Class

Record Class Animal(Name As String)
End Class

Record Class Dog(Name As String, Breed As String)
    Inherits Animal(Name)
End Class

Module Program
    Sub Hello()
    End Sub

    Function Twice(x As Integer) As Integer
        Return x * 2
    End Function

    Function Count(values As List(Of Integer)) As Integer
        Return values.Count
    End Function

    Function Generic(Of T)(values As List(Of T)) As Integer
        Return values.Count
    End Function

    Function Join(separator As String, values As List(Of String)) As String
        Return String.Join(separator, values)
    End Function

    Sub Forms(box As Box, a As Integer(), flag As Boolean, t As (Integer, Integer), n As (Integer, Integer)?, d As Dog, other As Dog, spot As Spot)
        Dim pair As Pair = (1, ""one"")
        Dim sameTuple = t = (1, 2)
        Dim differentTuple = t <> (3, 4)
        Dim sameNullable = n = (1, 2)
        Dim moved = spot With {.X = 5}
        Dim anon = New With {.A = 1, .B = 2}
        Dim anonCopy = anon With {.B = 3}
        box?.Count += 1
        box?.Name ?= ""x""
        Dim list As List(Of Integer) = {1, 2}
        Dim spread As List(Of Integer) = {..a, 3}
        Dim setSpread As HashSet(Of Integer) = {..a, 4}
        Dim counted = Count({1, 2})
        Dim joined = Join(values:={""a"", ""b""}, separator:=""-"")
        Dim chosen As List(Of Integer) = If(flag, {1}, {2, 3})
        Dim inferred = Generic({1, 2})
        Dim lambda = Function(Optional x As Integer = 3) x
        Dim sameRecord = d = other
        Dim differentRecord = d <> other
        Dim copy = d With {.Breed = ""Pug""}
        Dim handler = AddressOf Hello
        Dim boxed As Object = AddressOf Twice
        Dim picked = If(flag, AddressOf Hello, AddressOf Hello)
        Dim handlers = {AddressOf Twice, AddressOf Twice}
        Dim factory = Function() AddressOf Hello
    End Sub
End Module
"
            Dim compilation = CreateCompilation(source, parseOptions:=VbNext)
            compilation.VerifyDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim missing As New List(Of String)
            Dim tupleBinaries As New List(Of String)
            For Each block In tree.GetRoot().DescendantNodes().OfType(Of MethodBlockBaseSyntax)()
                Dim operation = model.GetOperation(block)
                Assert.NotNull(operation)
                For Each descendant In operation.DescendantsAndSelf()
                    If descendant.Kind = OperationKind.None Then
                        missing.Add(descendant.Syntax.ToString())
                    End If

                    Dim tupleBinary = TryCast(descendant, ITupleBinaryOperation)
                    If tupleBinary IsNot Nothing Then
                        tupleBinaries.Add(tupleBinary.OperatorKind.ToString() & " " & tupleBinary.LeftOperand.Syntax.ToString() & " " & tupleBinary.RightOperand.Syntax.ToString())
                    End If
                Next

                Assert.NotNull(FlowAnalysis.ControlFlowGraph.Create(block, model))
            Next

            Assert.Empty(missing)
            Assert.Equal("Equals t (1, 2) | NotEquals t (3, 4) | Equals n (1, 2)", String.Join(" | ", tupleBinaries))
        End Sub

        <Fact>
        Public Sub WithCopiesOfStructuresAndAnonymousTypesAreWithOperations()
            Dim source = "
Structure Spot
    Public X As Integer
    Public Y As Integer
End Structure

Module Program
    Sub Copies(spot As Spot)
        Dim moved = spot With {.X = .Y + 1}
        Dim anon = New With {.A = 1, .B = 2}
        Dim anonCopy = anon With {.B = 3}
        Dim readsOriginal = anon With {.B = .A + 10}
    End Sub
End Module
"
            Dim compilation = CreateCompilation(source, parseOptions:=VbNext)
            compilation.VerifyDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim copies = tree.GetRoot().DescendantNodes().OfType(Of WithExpressionSyntax)().ToArray()
            Assert.Equal(3, copies.Length)
            ' Reading the original in an anonymous type's With has no with operation; C# cannot write it.
            Dim methodOperation = model.GetOperation(tree.GetRoot().DescendantNodes().OfType(Of MethodBlockBaseSyntax)().Single())
            Assert.Contains(methodOperation.DescendantsAndSelf(), Function(o) o.Syntax Is copies(2) AndAlso o.Kind = OperationKind.None)
            Dim shapes As New List(Of String)
            For Each copy In copies.Take(2)
                Dim operation = Assert.IsAssignableFrom(Of IWithOperation)(model.GetOperation(copy))
                Assert.Null(operation.CloneMethod)
                Assert.Empty(operation.DescendantsAndSelf().OfType(Of ILocalReferenceOperation)().Where(Function(l) l.Local.IsImplicitlyDeclared))
                Dim assignment = DirectCast(operation.Initializer.Initializers.Single(), ISimpleAssignmentOperation)
                Dim target = DirectCast(assignment.Target, IMemberReferenceOperation)
                Assert.Equal(InstanceReferenceKind.ImplicitReceiver, DirectCast(target.Instance, IInstanceReferenceOperation).ReferenceKind)
                shapes.Add(operation.Operand.Syntax.ToString() & ":" & target.Member.Name & "=" & assignment.Value.Syntax.ToString())
            Next

            Assert.Equal("spot:X=.Y + 1 | anon:B=3", String.Join(" | ", shapes))
            Dim block = tree.GetRoot().DescendantNodes().OfType(Of MethodBlockBaseSyntax)().Single()
            Assert.NotNull(FlowAnalysis.ControlFlowGraph.Create(block, model))
        End Sub

        <Fact>
        Public Sub IndexAndRangeValuesAreHatAndRangeOperations()
            Dim source = "
Module Program
    Sub Values(n As Integer, maybe As Integer?)
        Dim a = ^1
        Dim b = 1..^n
        Dim c = ..2
        Dim d = (3..)
        Dim e = ^maybe
        Dim f = ..maybe
    End Sub
End Module
"
            Dim compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, parseOptions:=VbNext)
            compilation.VerifyDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim hats = tree.GetRoot().DescendantNodes().OfType(Of UnaryExpressionSyntax)().Select(
                Function(syntax)
                    Dim operation = Assert.IsAssignableFrom(Of IUnaryOperation)(model.GetOperation(syntax))
                    Assert.Equal(Operations.UnaryOperatorKind.Hat, operation.OperatorKind)
                    Return syntax.ToString() & ":" & operation.IsLifted & ":" & operation.Type.ToTestDisplayString()
                End Function)
            Assert.Equal("^1:False:System.Index | ^n:False:System.Index | ^maybe:True:System.Nullable(Of System.Index)", String.Join(" | ", hats))

            Dim ranges = tree.GetRoot().DescendantNodes().OfType(Of RangeExpressionSyntax)().Select(
                Function(syntax)
                    Dim operation = Assert.IsAssignableFrom(Of IRangeOperation)(model.GetOperation(syntax))
                    Return syntax.ToString() & ":" & operation.Method.Name & ":" & operation.IsLifted & ":" & operation.Type.ToTestDisplayString()
                End Function)
            Assert.Equal("1..^n:.ctor:False:System.Range | ..2:EndAt:False:System.Range | 3..:StartAt:False:System.Range | ..maybe:EndAt:True:System.Nullable(Of System.Range)", String.Join(" | ", ranges))

            Dim block = tree.GetRoot().DescendantNodes().OfType(Of MethodBlockBaseSyntax)().Single()
            Assert.NotNull(FlowAnalysis.ControlFlowGraph.Create(block, model))
        End Sub

        <Fact>
        Public Sub IndexAndSliceAccessesAreImplicitIndexerReferences()
            Dim source = "
Imports System
Imports System.Collections.Generic

Module Program
    Function Make() As List(Of Integer)
        Return New List(Of Integer)
    End Function

    Sub Uses(list As List(Of Integer), s As String, span As ReadOnlySpan(Of Integer), arr As Integer(), i As Index, r As Range, mem As Memory(Of Integer))
        Dim a = list(^1)
        Dim b = list(i)
        Dim c = s(1..3)
        Dim d = span(^2)
        Dim e = span(r)
        Dim f = Make()(^1)
        Dim g = arr(^1)
        Dim h = arr(i)
        Dim k = mem(1..2)
        list(^1) = 4
    End Sub
End Module
"
            Dim compilation = CreateCompilation(source, targetFramework:=TargetFramework.Net90, parseOptions:=VbNext)
            compilation.VerifyDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim describe = Function(operation As IOperation) As String
                               If TypeOf operation Is IUnaryOperation Then
                                   Return DirectCast(operation, IUnaryOperation).OperatorKind.ToString()
                               ElseIf TypeOf operation Is IRangeOperation Then
                                   Return "Range"
                               ElseIf TypeOf operation Is IParameterReferenceOperation Then
                                   Return "Parameter"
                               End If

                               Return operation.GetType().Name
                           End Function

            Dim accesses = tree.GetRoot().DescendantNodes().OfType(Of InvocationExpressionSyntax)().
                Where(Function(invocation) invocation.ArgumentList IsNot Nothing AndAlso invocation.ArgumentList.Arguments.Count = 1).
                Select(Function(invocation)
                           Dim operation = model.GetOperation(invocation)
                           If TypeOf operation Is IArrayElementReferenceOperation Then
                               Return invocation.ToString() & ":Array|" & describe(DirectCast(operation, IArrayElementReferenceOperation).Indices.Single())
                           End If

                           Dim indexer = Assert.IsAssignableFrom(Of IImplicitIndexerReferenceOperation)(operation)
                           Return invocation.ToString() & ":" & indexer.Instance.Syntax.ToString() & "|" & describe(indexer.Argument) & "|" & indexer.LengthSymbol.Name & "|" & indexer.IndexerSymbol.Name
                       End Function)

            Assert.Equal({
                "list(^1):list|Hat|Count|Item",
                "list(i):list|Parameter|Count|Item",
                "s(1..3):s|Range|Length|Substring",
                "span(^2):span|Hat|Length|Item",
                "span(r):span|Parameter|Length|Slice",
                "Make()(^1):Make()|Hat|Count|Item",
                "arr(^1):Array|Hat",
                "arr(i):Array|Parameter",
                "mem(1..2):mem|Range|Length|Slice",
                "list(^1):list|Hat|Count|Item"}, accesses.ToArray())

            Dim block = tree.GetRoot().DescendantNodes().OfType(Of MethodBlockBaseSyntax)().Last()
            Assert.NotNull(FlowAnalysis.ControlFlowGraph.Create(block, model))
        End Sub

        <Fact>
        Public Sub UnboundTargetTypedNewIsInvalid()
            Dim source = "
Class C
    Function ToMissing() As Missing
        Return New(1)
    End Function

    Sub AssignMissing()
        Dim x As Undefined = New(2)
    End Sub

    Function BadTypeName(value As Short) As Integer
        Return New à–¾(value)
    End Function
End Class
"
            Dim compilation = CreateCompilation(source, parseOptions:=VbNext)
            Assert.NotEmpty(compilation.GetDiagnostics().Where(Function(d) d.Severity = DiagnosticSeverity.Error))

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim creations = tree.GetRoot().DescendantNodes().OfType(Of ObjectCreationExpressionSyntax)().ToArray()
            Assert.Equal(3, creations.Length)
            For Each creation In creations
                Assert.Equal(OperationKind.Invalid, model.GetOperation(creation).Kind)
            Next

            For Each block In tree.GetRoot().DescendantNodes().OfType(Of MethodBlockBaseSyntax)()
                Assert.NotNull(FlowAnalysis.ControlFlowGraph.Create(block, model))
            Next
        End Sub

        Private Shared Sub VerifyOperationTrees(source As String)
            Dim compilation = CreateCompilation(source, parseOptions:=VbNext)
            compilation.VerifyDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            Dim blocks = tree.GetRoot().DescendantNodes().OfType(Of MethodBlockBaseSyntax)().ToArray()
            Assert.NotEmpty(blocks)

            For Each block In blocks
                Dim operation = model.GetOperation(block)
                Assert.NotNull(operation)
                For Each descendant In operation.DescendantsAndSelf()
                    Assert.NotNull(descendant.Syntax)
                    Assert.Same(tree, descendant.Syntax.SyntaxTree)
                Next

                Assert.NotNull(FlowAnalysis.ControlFlowGraph.Create(block, model))
            Next
        End Sub
    End Class
End Namespace
