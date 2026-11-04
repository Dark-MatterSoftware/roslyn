' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.CodeActions
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

    Public NotInheritable Class VbNextIntroduceParameterTests
        Inherits VbNextRefactoringTest

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As TestWorkspace, parameters As TestParameters) As CodeRefactoringProvider
            Return DirectCast(Activator.CreateInstance(GetType(Microsoft.CodeAnalysis.VisualBasic.IntroduceParameter.VisualBasicIntroduceParameterCodeRefactoringProvider), nonPublic:=True), CodeRefactoringProvider)
        End Function

        Protected Overrides Function MassageActions(actions As ImmutableArray(Of CodeAction)) As ImmutableArray(Of CodeAction)
            Return GetNestedActions(actions)
        End Function

        <Fact>
        Public Async Function TestNotOnAGuardTestWhoseNameIsUsedAfter() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String Into text|] Then Return ""no""

        Return text
    End Function
End Class"))
        End Function

        <Fact>
        Public Async Function TestNotOnATypeTestWhoseNameIsNotUsed() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value Is String Into text|] Then Return ""yes""

        Return ""no""
    End Function
End Class"))
        End Function

        <Fact>
        Public Async Function TestNotOnTheTestInsideOne() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String|] Into text Then Return ""no""

        Return text
    End Function
End Class"))
        End Function

        <Fact>
        Public Async Function TestNotOnAConditionThatHoldsOne() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value Is String Into text AndAlso text.Length > 1|] Then Return text

        Return ""no""
    End Function
End Class"))
        End Function

        <Fact>
        Public Async Function TestNotOnATypeTestInAnElseIf() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If value Is Nothing Then
            Return ""none""
        ElseIf [|TypeOf value Is String Into text|] Then
            Return text
        End If

        Return ""other""
    End Function
End Class"))
        End Function

        <Fact>
        Public Async Function TestNotOnAGuardTestWithACaller() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String Into text|] Then Return ""no""

        Return text
    End Function

    Function Caller(o As Object) As String
        Return Label(o)
    End Function
End Class"))
        End Function

        <Fact>
        Public Async Function TestChoicesOnAPlainTypeTest() As Task
            Await TestExactActionSetOfferedAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String|] Then Return ""no""

        Return ""yes""
    End Function

    Function Caller(o As Object) As String
        Return Label(o)
    End Function
End Class"),
            {
                FeaturesResources.and_update_call_sites_directly,
                FeaturesResources.into_extracted_method_to_invoke_at_call_sites,
                FeaturesResources.into_new_overload,
                FeaturesResources.and_update_call_sites_directly,
                FeaturesResources.into_extracted_method_to_invoke_at_call_sites,
                FeaturesResources.into_new_overload
            })
        End Function

        <Fact>
        Public Async Function TestOnAPlainTypeTestWithACaller() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String|] Then Return ""no""

        Return ""yes""
    End Function

    Function Caller(o As Object) As String
        Return Label(o)
    End Function
End Class"),
"Class C
    Function Label(value As Object, v As Boolean) As String
        If {|Rename:v|} Then Return ""no""

        Return ""yes""
    End Function

    Function Caller(o As Object) As String
        Return Label(o, TypeOf o IsNot String)
    End Function
End Class")
        End Function

        <Fact>
        Public Async Function TestOnAPlainTypeTestWithACallerTrampoline() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String|] Then Return ""no""

        Return ""yes""
    End Function

    Function Caller(o As Object) As String
        Return Label(o)
    End Function
End Class"),
"Class C
    Public Function GetV(value As Object) As Boolean
        Return TypeOf value IsNot String
    End Function

    Function Label(value As Object, v As Boolean) As String
        If {|Rename:v|} Then Return ""no""

        Return ""yes""
    End Function

    Function Caller(o As Object) As String
        Return Label(o, GetV(o))
    End Function
End Class", index:=1)
        End Function

        <Fact>
        Public Async Function TestOnAPlainTypeTestWithACallerOverload() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String|] Then Return ""no""

        Return ""yes""
    End Function

    Function Caller(o As Object) As String
        Return Label(o)
    End Function
End Class"),
"Class C
    Public Function Label(value As Object) As String
        Return Label(value, TypeOf value IsNot String)
    End Function

    Function Label(value As Object, v As Boolean) As String
        If {|Rename:v|} Then Return ""no""

        Return ""yes""
    End Function

    Function Caller(o As Object) As String
        Return Label(o)
    End Function
End Class", index:=2)
        End Function

        <Fact>
        Public Async Function TestOnAPlainTypeTest() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String|] Then Return ""no""

        Return ""yes""
    End Function
End Class"),
"Class C
    Function Label(value As Object, v As Boolean) As String
        If {|Rename:v|} Then Return ""no""

        Return ""yes""
    End Function
End Class")
        End Function

        <Fact>
        Public Async Function TestOnTheOperandOfOne() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Class C
    Function Make() As Object
        Return ""x""
    End Function

    Function Label() As String
        If TypeOf [|Make()|] IsNot String Into text Then Return ""no""

        Return text
    End Function
End Class"),
"Class C
    Function Make() As Object
        Return ""x""
    End Function

    Public Function GetV() As Object
        Return Make()
    End Function

    Function Label(v As Object) As String
        If TypeOf {|Rename:v|} IsNot String Into text Then Return ""no""

        Return text
    End Function
End Class")
        End Function
    End Class

    Public NotInheritable Class VbNextIntroduceVariableTests
        Inherits VbNextRefactoringTest

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As TestWorkspace, parameters As TestParameters) As CodeRefactoringProvider
            Return New Microsoft.CodeAnalysis.IntroduceVariable.IntroduceVariableCodeRefactoringProvider()
        End Function

        <Fact>
        Public Async Function TestNotOnTheForEachOfAnXmlEmbeddedBlock() As Task
            Await TestMissingAsync(InVbNext(
"Imports System.Xml.Linq

Class C
    Function Items(names As String()) As XElement
        Return <ul>
                   <%= [|For Each name As String In names
                           Yield <li><%= name %></li>
                       Next|] %>
               </ul>
    End Function
End Class"))
        End Function
    End Class
End Namespace
