' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Extensions
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.VisualStudio.LanguageServices.UnitTests.CodeModel
Imports Roslyn.Test.Utilities

Namespace Microsoft.VisualStudio.LanguageServices.UnitTests.CodeModel.VisualBasic

    ''' <summary>
    ''' D10-A: targeted Code Model coverage for ordinary designer contracts under
    ''' VisualBasic17_13 and VbNext, plus Vb.Next declaration coexistence.
    ''' Uses the existing Code Model test host (CreateCodeModelTestState); not parser-only.
    ''' </summary>
    <[UseExportProvider]>
    Public Class VbNextDesignerCodeModelTests

        Private Shared Function CreateDesignerWorkspace(languageVersion As String, document As XElement) As XElement
            Return <Workspace>
                       <Project Language="Visual Basic" CommonReferences="true" LanguageVersion=<%= languageVersion %>>
                           <CompilationOptions RootNamespace="WindowsApplication1"/>
                           <MetadataReference><%= SystemWindowsFormsPath %></MetadataReference>
                           <MetadataReference><%= SystemDrawingPath %></MetadataReference>
                           <%= document %>
                       </Project>
                   </Workspace>
        End Function

        Private Shared Function CreateSimpleWorkspace(languageVersion As String, code As XElement) As XElement
            Return <Workspace>
                       <Project Language="Visual Basic" CommonReferences="true" LanguageVersion=<%= languageVersion %>>
                           <Document><%= code.Value.Trim() %></Document>
                       </Project>
                   </Workspace>
        End Function

        Private Shared Sub AssertDesignerFormMembers(codeClass As EnvDTE80.CodeClass2, Optional expectedCount As Integer = 5)
            Assert.Equal("Form1", codeClass.Name)
            Assert.Equal(EnvDTE.vsCMElement.vsCMElementClass, codeClass.Kind)

            Dim members = codeClass.Members
            Assert.Equal(expectedCount, members.Count)

            Dim names = Enumerable.Range(1, members.Count).Select(Function(i) members.Item(i).Name).ToArray()
            Assert.Contains("InitializeComponent", names)
            Assert.Contains("Button1", names)

            Dim button = Enumerable.Range(1, members.Count).
                Select(Function(i) members.Item(i)).
                First(Function(m) m.Name = "Button1")
            Assert.Equal(EnvDTE.vsCMElement.vsCMElementVariable, button.Kind)

            Dim init = Enumerable.Range(1, members.Count).
                Select(Function(i) members.Item(i)).
                First(Function(m) m.Name = "InitializeComponent")
            Assert.Equal(EnvDTE.vsCMElement.vsCMElementFunction, init.Kind)

            Dim startPoint = codeClass.GetStartPoint(EnvDTE.vsCMPart.vsCMPartWholeWithAttributes)
            Assert.NotNull(startPoint)
        End Sub

#Region "Ordinary designer contracts (17.13 and vbnext)"

        <WpfTheory, CombinatorialData, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Sub OrdinaryDesignerForm_MemberEnumeration(useVbNext As Boolean)
            Dim version = If(useVbNext, "vbnext", "17.13")
            Dim definition = CreateDesignerWorkspace(version,
                <Document>&lt;Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()&gt; _
Partial Class $$Form1
    Inherits System.Windows.Forms.Form

    &lt;System.Diagnostics.DebuggerNonUserCode()&gt; _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    &lt;System.Diagnostics.DebuggerStepThrough()&gt; _
    Private Sub InitializeComponent()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        Me.Button1.Name = "Button1"
        Me.Controls.Add(Me.Button1)
        Me.Name = "Form1"
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents Button1 As System.Windows.Forms.Button

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
    End Sub
End Class</Document>)

            Using state = CreateCodeModelTestState(definition)
                Dim codeClass = state.GetCodeElementAtCursor(Of EnvDTE80.CodeClass2)()
                AssertDesignerFormMembers(codeClass, expectedCount:=5)
                Assert.Equal("Dispose", codeClass.Members.Item(1).Name)
                Assert.Equal("components", codeClass.Members.Item(2).Name)
                Assert.Equal("InitializeComponent", codeClass.Members.Item(3).Name)
                Assert.Equal("Button1", codeClass.Members.Item(4).Name)
                Assert.Equal("Button1_Click", codeClass.Members.Item(5).Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementFunction, codeClass.Members.Item(5).Kind)
            End Using
        End Sub

        <WpfTheory, CombinatorialData, Trait(Traits.Feature, Traits.Features.CodeModelMethodXml)>
        Public Sub OrdinaryDesignerForm_InitializeComponent_MethodXML(useVbNext As Boolean)
            Dim version = If(useVbNext, "vbnext", "17.13")
            Dim definition = CreateDesignerWorkspace(version,
                <Document>&lt;Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()&gt; _
Partial Class Form1
    Inherits System.Windows.Forms.Form

    &lt;System.Diagnostics.DebuggerNonUserCode()&gt; _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    &lt;System.Diagnostics.DebuggerStepThrough()&gt; _
    $$Private Sub InitializeComponent()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        Me.Button1.Name = "Button1"
        Me.Controls.Add(Me.Button1)
        Me.Name = "Form1"
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents Button1 As System.Windows.Forms.Button
End Class</Document>)

            Using state = CreateCodeModelTestState(definition)
                Dim func = state.GetCodeElementAtCursor(Of EnvDTE.CodeFunction)()
                Dim actual = func.GetMethodXML()

                Assert.Equal("InitializeComponent", func.Name)
                Assert.NotNull(actual)
                Assert.Equal("Block", actual.Name.LocalName)
                Assert.True(actual.Elements().Any(), "MethodXML should contain InitializeComponent statements")
            End Using
        End Sub

#End Region

#Region "Vb.Next declaration coexistence"

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Sub RecordClass_AppearsAsClass()
            Dim code =
<Code>
Record Class $$Stage(Name As String)
End Class
</Code>

            Using state = CreateCodeModelTestState(CreateSimpleWorkspace("vbnext", code))
                Dim codeClass = state.GetCodeElementAtCursor(Of EnvDTE80.CodeClass2)()
                Assert.Equal("Stage", codeClass.Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementClass, codeClass.Kind)

                ' Positional / synthesized members are not required EnvDTE elements.
                Assert.Equal(0, codeClass.Members.Count)
            End Using
        End Sub

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Sub RecordStructure_AppearsAsStructure()
            Dim code =
<Code>
Record Structure $$Point(X As Integer, Y As Integer)
End Structure
</Code>

            Using state = CreateCodeModelTestState(CreateSimpleWorkspace("vbnext", code))
                Dim codeStruct = state.GetCodeElementAtCursor(Of EnvDTE80.CodeStruct2)()
                Assert.Equal("Point", codeStruct.Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementStruct, codeStruct.Kind)
                Assert.Equal(0, codeStruct.Members.Count)
            End Using
        End Sub

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Sub ExtensionBlock_Omitted_NeighborsDiscoverable()
            Dim code =
<Code>
Module $$Helpers
    Sub Before()
    End Sub

    Extension(s As String)
        Public ReadOnly Property Twice As String
            Get
                Return s &amp; s
            End Get
        End Property
    End Extension

    Sub After()
    End Sub
End Module
</Code>

            Using state = CreateCodeModelTestState(CreateSimpleWorkspace("vbnext", code))
                Dim moduleElement = state.GetCodeElementAtCursor(Of EnvDTE80.CodeClass2)()
                Assert.Equal("Helpers", moduleElement.Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementModule, moduleElement.Kind)

                Dim members = moduleElement.Members
                Assert.Equal(2, members.Count)
                Assert.Equal("Before", members.Item(1).Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementFunction, members.Item(1).Kind)
                Assert.Equal("After", members.Item(2).Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementFunction, members.Item(2).Kind)

                For Each member As EnvDTE.CodeElement In members
                    Assert.NotEqual("Twice", member.Name)
                    Assert.NotEqual("Extension", member.Name)
                Next
            End Using
        End Sub

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Sub DesignerForm_CoexistsWithRecordAndExtensionBlock()
            Dim definition = CreateDesignerWorkspace("vbnext",
                <Document>&lt;Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()&gt; _
Partial Class $$Form1
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer

    &lt;System.Diagnostics.DebuggerStepThrough()&gt; _
    Private Sub InitializeComponent()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.Controls.Add(Me.Button1)
    End Sub

    Friend WithEvents Button1 As System.Windows.Forms.Button

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
    End Sub
End Class

Record Class Stage(Name As String)
End Class

Module StringExtensions
    Extension(s As String)
        Public ReadOnly Property Twice As String
            Get
                Return s &amp; s
            End Get
        End Property
    End Extension

    Public Function Identity(value As String) As String
        Return value
    End Function
End Module</Document>)

            Using state = CreateCodeModelTestState(definition)
                Dim formClass = state.GetCodeElementAtCursor(Of EnvDTE80.CodeClass2)()
                Assert.Equal("Form1", formClass.Name)

                Dim formMembers = formClass.Members
                Assert.Equal(4, formMembers.Count)
                Assert.Equal("components", formMembers.Item(1).Name)
                Assert.Equal("InitializeComponent", formMembers.Item(2).Name)
                Assert.Equal("Button1", formMembers.Item(3).Name)
                Assert.Equal("Button1_Click", formMembers.Item(4).Name)

                Dim fileElements = state.FileCodeModel.CodeElements
                Assert.Equal(3, fileElements.Count)

                Dim stage = TryCast(fileElements.Item(2), EnvDTE80.CodeClass2)
                Assert.NotNull(stage)
                Assert.Equal("Stage", stage.Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementClass, stage.Kind)

                Dim extensions = TryCast(fileElements.Item(3), EnvDTE80.CodeClass2)
                Assert.NotNull(extensions)
                Assert.Equal("StringExtensions", extensions.Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementModule, extensions.Kind)
                Assert.Equal(1, extensions.Members.Count)
                Assert.Equal("Identity", extensions.Members.Item(1).Name)
            End Using
        End Sub

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Sub IndexFromEndInHandler_DoesNotDisruptMemberDiscovery()
            Dim definition = CreateDesignerWorkspace("vbnext",
                <Document>&lt;Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()&gt; _
Partial Class $$Form1
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer
    Private ReadOnly _stages As New System.Collections.Generic.List(Of String) From {"a", "b", "c"}

    &lt;System.Diagnostics.DebuggerStepThrough()&gt; _
    Private Sub InitializeComponent()
    End Sub

    Friend WithEvents Button1 As System.Windows.Forms.Button

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Me.Text = _stages(^1)
    End Sub
End Class</Document>)

            Using state = CreateCodeModelTestState(definition)
                Dim codeClass = state.GetCodeElementAtCursor(Of EnvDTE80.CodeClass2)()
                Dim members = codeClass.Members
                Assert.Equal(5, members.Count)
                Assert.Equal("components", members.Item(1).Name)
                Assert.Equal("_stages", members.Item(2).Name)
                Assert.Equal("InitializeComponent", members.Item(3).Name)
                Assert.Equal("Button1", members.Item(4).Name)
                Assert.Equal("Button1_Click", members.Item(5).Name)
            End Using
        End Sub

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Sub PrimaryConstructor_CompactMember_AndField_CoexistAsOrdinaryMembers()
            Dim code =
<Code>
Class $$Customer(name As String)
    Public ReadOnly Property Name As String = name

    Public Property Title As String
        Get
            Return Field
        End Get
        Set
            Field = Value
        End Set
    End Property

    Function Label() As String = Return Name &amp; ":" &amp; Title
End Class
</Code>

            Using state = CreateCodeModelTestState(CreateSimpleWorkspace("vbnext", code))
                Dim codeClass = state.GetCodeElementAtCursor(Of EnvDTE80.CodeClass2)()
                Assert.Equal("Customer", codeClass.Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementClass, codeClass.Kind)

                Dim members = codeClass.Members
                Assert.Equal(3, members.Count)
                Assert.Equal("Name", members.Item(1).Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementProperty, members.Item(1).Kind)
                Assert.Equal("Title", members.Item(2).Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementProperty, members.Item(2).Kind)
                Assert.Equal("Label", members.Item(3).Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementFunction, members.Item(3).Kind)

                ' Primary-constructor parameters are not required EnvDTE elements.
                For Each member As EnvDTE.CodeElement In members
                    Assert.NotEqual("name", member.Name)
                Next
            End Using
        End Sub

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Async Function AddFunction_DoesNotCorruptNeighboringVbNextDeclarations() As Task
            Dim code =
<Code>
Module $$Helpers
    Sub Before()
    End Sub

    Extension(s As String)
        Public ReadOnly Property Twice As String
            Get
                Return s &amp; s
            End Get
        End Property
    End Extension

    Sub After()
    End Sub
End Module

Record Class Stage(Name As String)
End Class
</Code>

            Using state = CreateCodeModelTestState(CreateSimpleWorkspace("vbnext", code))
                Dim moduleElement = state.GetCodeElementAtCursor(Of EnvDTE80.CodeClass2)()
                Dim added = moduleElement.AddFunction("Inserted", EnvDTE.vsCMFunction.vsCMFunctionSub, Nothing, -1, EnvDTE.vsCMAccess.vsCMAccessPublic)
                Assert.Equal("Inserted", added.Name)

                Dim text = (Await state.GetDocumentAtCursor().GetTextAsync()).ToString()
                Assert.Contains("Extension(s As String)", text)
                Assert.Contains("End Extension", text)
                Assert.Contains("Record Class Stage(Name As String)", text)
                Assert.Contains("Sub Inserted()", text)
                Assert.Contains("Sub Before()", text)
                Assert.Contains("Sub After()", text)

                Dim members = moduleElement.Members
                Dim names = Enumerable.Range(1, members.Count).Select(Function(i) members.Item(i).Name).ToArray()
                Assert.Contains("Before", names)
                Assert.Contains("After", names)
                Assert.Contains("Inserted", names)
                Assert.DoesNotContain("Twice", names)

                Dim fileElements = state.FileCodeModel.CodeElements
                Assert.Equal(2, fileElements.Count)
                Assert.Equal("Helpers", fileElements.Item(1).Name)
                Assert.Equal("Stage", fileElements.Item(2).Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementClass, fileElements.Item(2).Kind)
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeModel)>
        Public Async Function AddVariable_OnDesignerForm_PreservesSiblingVbNextTypes() As Task
            Dim definition = CreateDesignerWorkspace("vbnext",
                <Document>&lt;Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()&gt; _
Partial Class $$Form1
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer

    &lt;System.Diagnostics.DebuggerStepThrough()&gt; _
    Private Sub InitializeComponent()
    End Sub

    Friend WithEvents Button1 As System.Windows.Forms.Button
End Class

Record Class Stage(Name As String)
End Class</Document>)

            Using state = CreateCodeModelTestState(definition)
                Dim formClass = state.GetCodeElementAtCursor(Of EnvDTE80.CodeClass2)()
                Dim added = formClass.AddVariable("ExtraField", "Integer", -1, EnvDTE.vsCMAccess.vsCMAccessPrivate)
                Assert.Equal("ExtraField", added.Name)

                Dim text = (Await state.GetDocumentAtCursor().GetTextAsync()).ToString()
                Assert.Contains("Friend WithEvents Button1", text)
                Assert.Contains("Private ExtraField As Integer", text)
                Assert.Contains("Record Class Stage(Name As String)", text)

                Dim stage = TryCast(state.FileCodeModel.CodeElements.Item(2), EnvDTE80.CodeClass2)
                Assert.NotNull(stage)
                Assert.Equal("Stage", stage.Name)
                Assert.Equal(EnvDTE.vsCMElement.vsCMElementClass, stage.Kind)
            End Using
        End Function

#End Region

    End Class
End Namespace
