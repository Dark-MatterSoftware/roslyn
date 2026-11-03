' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod
Imports Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.CodeRefactorings.ExtractMethod
    ''' <summary>
    ''' Extract method on the Vb.Next forms.
    ''' </summary>
    <Trait(Traits.Feature, Traits.Features.CodeActionsExtractMethod)>
    Public NotInheritable Class VbNextExtractMethodTests
        Inherits AbstractVisualBasicCodeActionTest_NoEditor

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As TestWorkspace, parameters As TestParameters) As CodeRefactoringProvider
            Return New ExtractMethodCodeRefactoringProvider()
        End Function

        Private Shared Function InVbNext(code As String) As String
            Return "<Workspace><Project Language=""Visual Basic"" CommonReferencesNet9=""true"" LanguageVersion=""vbnext""><Document><![CDATA[" & code & "]]></Document></Project></Workspace>"
        End Function

        <Fact>
        Public Async Function TestInInterfaceMember() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Interface IGreeter
    Function Greet(name As String) As String
        Return [|""hello "" & name|]
    End Function
End Interface"),
"Interface IGreeter
    Function Greet(name As String) As String
        Return {|Rename:NewMethod|}(name)
    End Function

    Private Shared Function NewMethod(name As String) As String
        Return ""hello "" & name
    End Function
End Interface")
        End Function

        <Fact>
        Public Async Function TestInSharedInterfaceMember() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Interface INamed
    Shared Function DefaultName(prefix As String) As String
        Return [|prefix & ""unnamed""|]
    End Function
End Interface"),
"Interface INamed
    Shared Function DefaultName(prefix As String) As String
        Return {|Rename:NewMethod|}(prefix)
    End Function

    Private Shared Function NewMethod(prefix As String) As String
        Return prefix & ""unnamed""
    End Function
End Interface")
        End Function

        <Fact>
        Public Async Function TestInExtensionBlock() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return [|s.Split("" ""c).Length|]
            End Get
        End Property
    End Extension
End Module"),
"Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property WordCount As Integer
            Get
                Return {|Rename:GetLength|}(s)
            End Get
        End Property
    End Extension

    Private Function GetLength(s As String) As Integer
        Return s.Split("" ""c).Length
    End Function
End Module")
        End Function

        <Fact>
        Public Async Function TestNotOnForEachDeconstructionNames() As Task
            Await TestMissingAsync(InVbNext(
"Imports System.Collections.Generic

Class C
    Sub M(prices As Dictionary(Of String, Decimal))
        For Each ([|item|], price) In prices
            System.Console.WriteLine(item)
        Next
    End Sub
End Class"))
        End Function

        <Fact>
        Public Async Function TestForEachDeconstructionLoop() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Imports System.Collections.Generic

Class C
    Sub M(prices As Dictionary(Of String, Decimal))
        [|For Each (item, price) In prices
            System.Console.WriteLine(item & price)
        Next|]
    End Sub
End Class"),
"Imports System.Collections.Generic

Class C
    Sub M(prices As Dictionary(Of String, Decimal))
        {|Rename:NewMethod|}(prices)
    End Sub

    Private Shared Sub NewMethod(prices As Dictionary(Of String, Decimal))
        For Each (item, price) In prices
            System.Console.WriteLine(item & price)
        Next
    End Sub
End Class")
        End Function

        <Fact>
        Public Async Function TestIndexFromTheEnd() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Imports System.Collections.Generic

Class C
    Function M(items As List(Of Integer), depth As Integer) As Integer
        Return items([|^depth|])
    End Function
End Class"),
"Imports System.Collections.Generic

Class C
    Function M(items As List(Of Integer), depth As Integer) As Integer
        Return items({|Rename:NewMethod|}(depth))
    End Function

    Private Shared Function NewMethod(depth As Integer) As Index
        Return ^depth
    End Function
End Class")
        End Function

        <Fact>
        Public Async Function TestCollectionLiteral() As Task
            Await TestInRegularAndScriptAsync(InVbNext(
"Imports System.Collections.Generic

Class C
    Function M(head As Integer()) As List(Of Integer)
        Dim all As List(Of Integer) = [|{..head, 3}|]
        Return all
    End Function
End Class"),
"Imports System.Collections.Generic

Class C
    Function M(head As Integer()) As List(Of Integer)
        Dim all As List(Of Integer) = {|Rename:GetAll|}(head)
        Return all
    End Function

    Private Shared Function GetAll(head() As Integer) As List(Of Integer)
        Return {..head, 3}
    End Function
End Class")
        End Function

        Private Const XmlItems As String = "Imports System.Xml.Linq

Class C
    Function Items(names As String()) As XElement
        Return <ul>
                   <%= {0} %>
               </ul>
    End Function
End Class"

        <Fact>
        Public Async Function TestNotOnTheForEachOfAnXmlEmbeddedBlock() As Task
            Await TestMissingAsync(InVbNext(String.Format(XmlItems,
"[|For Each name As String In names
                           Yield <li><%= name %></li>
                       Next|]")))
        End Function

        <Fact>
        Public Async Function TestNotOnAYieldInAnXmlEmbeddedBlock() As Task
            Await TestMissingAsync(InVbNext(String.Format(XmlItems,
"For Each name As String In names
                           [|Yield <li><%= name %></li>|]
                       Next")))
        End Function

        <Fact>
        Public Async Function TestNotOnAnIfWhoseTypeOfIntoNameIsUsedAfter() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        [|If TypeOf value IsNot String Into text Then Return ""not text""|]

        Return $""text of length {text.Length}""
    End Function
End Class"))
        End Function

        <Fact>
        Public Async Function TestNotOnATypeOfIntoWhoseNameIsUsedAfter() As Task
            Await TestMissingAsync(InVbNext(
"Class C
    Function Label(value As Object) As String
        If [|TypeOf value IsNot String Into text|] Then Return ""not text""

        Return $""text of length {text.Length}""
    End Function
End Class"))
        End Function

        <Fact>
        Public Async Function TestNotOnAReturnOfAByRefStructure() As Task
            Await TestMissingAsync(InVbNext(
"ByRef Structure Tally
    Private ByRef _total As Integer

    Sub New(ByRef total As Integer)
        _total = ByRef total
    End Sub
End Structure

Class C
    Function Track(ByRef total As Integer) As Tally
        [|Return New Tally(total)|]
    End Function
End Class"))
        End Function
    End Class
End Namespace
