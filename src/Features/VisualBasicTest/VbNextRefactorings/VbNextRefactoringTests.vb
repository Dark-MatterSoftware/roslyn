' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions
Imports Microsoft.CodeAnalysis.ReplaceMethodWithProperty
Imports Microsoft.CodeAnalysis.VisualBasic.ConvertTupleToStruct
Imports Microsoft.CodeAnalysis.VisualBasic.InvertIf
Imports Microsoft.CodeAnalysis.VisualBasic.UseNamedArguments

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.CodeRefactorings.VbNext
    ''' <summary>
    ''' Refactorings that a Vb.Next form would break are not offered on it.
    ''' </summary>
    Public MustInherit Class VbNextRefactoringTest
        Inherits AbstractVisualBasicCodeActionTest_NoEditor

        Protected Shared Function InVbNext(code As String) As String
            Return "<Workspace><Project Language=""Visual Basic"" CommonReferencesNet9=""true"" LanguageVersion=""vbnext""><Document><![CDATA[" & code & "]]></Document></Project></Workspace>"
        End Function
    End Class

    Public NotInheritable Class VbNextUseNamedArgumentsTests
        Inherits VbNextRefactoringTest

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As TestWorkspace, parameters As TestParameters) As CodeRefactoringProvider
            Return New VisualBasicUseNamedArgumentsCodeRefactoringProvider()
        End Function

        <Fact>
        Public Async Function TestNotOnAnIndexFromTheEnd() As Task
            Await TestMissingAsync(InVbNext(
"Imports System.Collections.Generic

Class C
    Function M(items As List(Of Integer)) As Integer
        Return items([||]^1)
    End Function
End Class"))
        End Function
    End Class

    Public NotInheritable Class VbNextInvertIfTests
        Inherits VbNextRefactoringTest

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As TestWorkspace, parameters As TestParameters) As CodeRefactoringProvider
            Return DirectCast(Activator.CreateInstance(GetType(VisualBasicInvertSingleLineIfCodeRefactoringProvider)), CodeRefactoringProvider)
        End Function

        <Fact>
        Public Async Function TestNotOnATypeTestThatNamesItsResult() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function M(value As Object) As String
        [||]If TypeOf value Is String Into text Then Return text Else Return """"
    End Function
End Class"))
        End Function
    End Class

    Public NotInheritable Class VbNextConvertTupleToStructTests
        Inherits VbNextRefactoringTest

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As TestWorkspace, parameters As TestParameters) As CodeRefactoringProvider
            Return New VisualBasicConvertTupleToStructCodeRefactoringProvider()
        End Function

        <Fact>
        Public Async Function TestNotOnADeconstructionTarget() As Task
            Await TestMissingAsync(InVbNext(
"Class Shelf
    Public Label As String
    Public Count As Integer
End Class

Class C
    Sub M(shelf As Shelf)
        [||](shelf.Label, shelf.Count) = (""bolts"", 40)
    End Sub
End Class"))
        End Function

        <Fact>
        Public Async Function TestNotOnATupleComparison() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function M(x As Integer, y As Integer) As Boolean
        Return [||](x, y) = (1, 2)
    End Function
End Class"))
        End Function
    End Class

    Public NotInheritable Class VbNextReplaceMethodWithPropertyTests
        Inherits VbNextRefactoringTest

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As TestWorkspace, parameters As TestParameters) As CodeRefactoringProvider
            Return New ReplaceMethodWithPropertyCodeRefactoringProvider()
        End Function

        <Fact>
        Public Async Function TestNotOnAReadOnlyStructureFunction() As Task
            Await TestMissingAsync(InVbNext(
"Structure Length
    Public Value As Double

    Public ReadOnly Function [||]GetDescription() As String
        Return Value.ToString()
    End Function
End Structure"))
        End Function
    End Class
End Namespace
