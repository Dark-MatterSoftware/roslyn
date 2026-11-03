' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.AddOrRemoveAccessibilityModifiers

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.AddOrRemoveAccessibilityModifiers
    ''' <summary>
    ''' Adding accessibility keeps the Vb.Next modifiers and the modifiers that declaration modifiers cannot represent.
    ''' </summary>
    <Trait(Traits.Feature, Traits.Features.CodeActionsAddOrRemoveAccessibilityModifiers)>
    Public NotInheritable Class VbNextAddAccessibilityModifiersTests
        Inherits AbstractVisualBasicDiagnosticProviderBasedUserDiagnosticTest_NoEditor

        Friend Overrides Function CreateDiagnosticProviderAndFixer(workspace As Workspace) As (DiagnosticAnalyzer, CodeFixProvider)
            Dim fixer = DirectCast(Activator.CreateInstance(GetType(VisualBasicAddOrRemoveAccessibilityModifiersCodeFixProvider)), CodeFixProvider)
            Return (New VisualBasicAddOrRemoveAccessibilityModifiersDiagnosticAnalyzer(), fixer)
        End Function

        Private Shared Function InVbNext(code As String) As String
            Return "<Workspace><Project Language=""Visual Basic"" CommonReferencesNet9=""true"" LanguageVersion=""vbnext""><Document><![CDATA[" & code & "]]></Document></Project></Workspace>"
        End Function

        <Fact>
        Public Async Function TestRecordClass() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Record Class [|Point|](X As Integer, Y As Integer)
End Class"),
"Friend Record Class Point(X As Integer, Y As Integer)
End Class")
        End Function

        <Fact>
        Public Async Function TestByRefStructure() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"ByRef Structure [|Counter|]
    Public Value As Integer
End Structure"),
"Friend ByRef Structure Counter
    Public Value As Integer
End Structure")
        End Function

        <Fact>
        Public Async Function TestByRefReadOnlyFunction() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Public Class Totals
    Private ReadOnly _first As Integer = 3

    ByRef ReadOnly Function [|First|]() As Integer
        Return _first
    End Function
End Class"),
"Public Class Totals
    Private ReadOnly _first As Integer = 3

    Public ByRef ReadOnly Function First() As Integer
        Return _first
    End Function
End Class")
        End Function

        <Fact>
        Public Async Function TestAsyncIterator() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Imports System.Collections.Generic
Imports System.Threading.Tasks

Public Module M
    Async Iterator Function [|Countdown|]() As IAsyncEnumerable(Of Integer)
        Await Task.Yield()
        Yield 1
    End Function
End Module"),
"Imports System.Collections.Generic
Imports System.Threading.Tasks

Public Module M
    Public Async Iterator Function Countdown() As IAsyncEnumerable(Of Integer)
        Await Task.Yield()
        Yield 1
    End Function
End Module")
        End Function
    End Class
End Namespace
