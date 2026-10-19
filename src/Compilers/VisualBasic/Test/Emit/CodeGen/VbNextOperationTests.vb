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

        <Fact(Skip:="vbnext binders reuse syntax for synthesized nodes, so the operation tree has duplicate nodes. See Phase 24 in docs/vbnext-phases.md.")>
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

        <Fact(Skip:="vbnext binders reuse syntax for synthesized nodes, so the operation tree has duplicate nodes. See Phase 24 in docs/vbnext-phases.md.")>
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

        <Fact(Skip:="vbnext binders reuse syntax for synthesized nodes, so the operation tree has duplicate nodes. See Phase 24 in docs/vbnext-phases.md.")>
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

        <Fact(Skip:="vbnext binders reuse syntax for synthesized nodes, so the operation tree has duplicate nodes. See Phase 24 in docs/vbnext-phases.md.")>
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

        <Fact(Skip:="vbnext binders reuse syntax for synthesized nodes, so the operation tree has duplicate nodes. See Phase 24 in docs/vbnext-phases.md.")>
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

        <Fact(Skip:="vbnext binders reuse syntax for synthesized nodes, so the operation tree has duplicate nodes. See Phase 24 in docs/vbnext-phases.md.")>
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
                Next
            Next
        End Sub
    End Class
End Namespace
