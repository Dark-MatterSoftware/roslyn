' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class PropertyFieldTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17_13 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        Private Shared ReadOnly StrictOptions As VisualBasicCompilationOptions =
            TestOptions.ReleaseExe.WithOptionStrict(OptionStrict.On).WithOptionInfer(False).WithOptionExplicit(True)

        Private Function CompileAndRun(source As String, expectedOutput As String, Optional options As VisualBasicCompilationOptions = Nothing) As CompilationVerifier
            Dim compilation = CreateCompilation(source, options:=If(options, StrictOptions), parseOptions:=VbNext)
            Return CompileAndVerify(compilation, expectedOutput:=expectedOutput).VerifyDiagnostics()
        End Function

        <Fact>
        Public Sub Field_StoresAValidatedValue()
            CompileAndRun("
Public Class Customer
    Public Property Name As String
        Get
            Return Field
        End Get
        Set
            If String.IsNullOrWhiteSpace(Value) Then Throw New System.ArgumentException(""A name is required."")
            Field = Value.Trim()
        End Set
    End Property
End Class

Module Program
    Sub Main()
        Dim c As New Customer()
        c.Name = ""  Ada  ""
        System.Console.Write(c.Name & "" "")
        Try
            c.Name = "" ""
        Catch ex As System.ArgumentException
            System.Console.Write(ex.Message & "" "" & c.Name)
        End Try
    End Sub
End Module
", "Ada A name is required. Ada")
        End Sub

        <Fact>
        Public Sub Field_InReadOnlyWriteOnlySharedAndStructureProperties()
            CompileAndRun("
Public Class Settings
    Private Shared _loads As Integer

    Public ReadOnly Property Values As System.Collections.Generic.List(Of String)
        Get
            If Field Is Nothing Then
                _loads += 1
                Field = New System.Collections.Generic.List(Of String)()
            End If
            Return Field
        End Get
    End Property

    Public WriteOnly Property Secret As String
        Set
            Field = Value
            Values.Add(Field.Length.ToString())
        End Set
    End Property

    Public Shared Property Count As Integer
        Get
            Return Field
        End Get
        Set
            Field = Value * 2
        End Set
    End Property

    Public Shared ReadOnly Property Loads As Integer
        Get
            Return _loads
        End Get
    End Property
End Class

Public Structure Counter
    Public Property Value As Integer
        Get
            Return Field
        End Get
        Set
            Field = Value + 1
        End Set
    End Property
End Structure

Module Program
    Sub Main()
        Dim s As New Settings()
        s.Secret = ""abc""
        s.Values.Add(""x"")
        Settings.Count = 5
        Dim c As New Counter()
        c.Value = 1
        System.Console.Write(String.Join("","", s.Values) & "" "" & Settings.Loads.ToString() & "" "" & Settings.Count.ToString() & "" "" & c.Value.ToString())
    End Sub
End Module
", "3,x 1 10 2")
        End Sub

        <Fact>
        Public Sub Field_InALambdaAndUnderOptionExplicitOff()
            CompileAndRun("
Public Class Box
    Public Property Item As String
        Get
            Dim read As System.Func(Of String) = Function() Field
            Return read()
        End Get
        Set
            Dim write As System.Action(Of String) = Sub(v) Field = v & ""!""
            write(Value)
        End Set
    End Property
End Class

Module Program
    Sub Main()
        Dim b As New Box()
        b.Item = ""hi""
        System.Console.Write(b.Item)
    End Sub
End Module
", "hi!", TestOptions.ReleaseExe.WithOptionExplicit(False))
        End Sub

        <Fact>
        Public Sub Field_AnExistingFieldKeepsItsMeaning()
            CompileAndRun("
Public Class Base
    Protected Field As String = ""base""
End Class

Public Class Derived
    Inherits Base

    Public ReadOnly Property FromBase As String
        Get
            Return Field
        End Get
    End Property

    Public ReadOnly Property FromLocal As String
        Get
            Dim Field As String = ""local""
            Return Field
        End Get
    End Property
End Class

Public Class Own
    Private Field As Integer = 7

    Public ReadOnly Property Value As Integer
        Get
            Return Field
        End Get
    End Property
End Class

Module Program
    Sub Main()
        Dim d As New Derived()
        System.Console.Write(d.FromBase & "" "" & d.FromLocal & "" "" & New Own().Value.ToString())
    End Sub
End Module
", "base local 7")
        End Sub

        <Fact>
        Public Sub Field_StorageIsHiddenAndBesideAnUnderscoreField()
            Dim compilation = CreateCompilation("
Public Class Customer
    Private _Name As String = ""mine""

    Public Property Name As String
        Get
            Return Field
        End Get
        Set
            Field = Value
        End Set
    End Property

    Public ReadOnly Property Own As String
        Get
            Return _Name
        End Get
    End Property
End Class

Public Class Own
    Private Field As Integer

    Public ReadOnly Property Value As Integer
        Get
            Return Field
        End Get
    End Property
End Class
", options:=TestOptions.ReleaseDll.WithMetadataImportOptions(MetadataImportOptions.All), parseOptions:=VbNext)

            CompileAndVerify(compilation, symbolValidator:=
                Sub([module])
                    Dim customer = [module].GlobalNamespace.GetTypeMember("Customer")
                    Dim storage = customer.GetMembers().OfType(Of FieldSymbol)().Single(Function(f) f.Name = "<Name>k__BackingField")
                    Assert.Equal(Accessibility.Private, storage.DeclaredAccessibility)
                    Assert.Equal("System.String", storage.Type.ToTestDisplayString())
                    Assert.NotNull(customer.GetMember(Of FieldSymbol)("_Name"))
                    Dim own = [module].GlobalNamespace.GetTypeMember("Own")
                    Assert.Equal({"Field"}, own.GetMembers().OfType(Of FieldSymbol)().Select(Function(f) f.Name).ToArray())
                End Sub).VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub Field_SemanticModel()
            Dim compilation = CreateCompilation("
Public Class Customer
    Public Property Name As String
        Get
            Return Field
        End Get
        Set
            Field = Value
        End Set
    End Property
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertNoDiagnostics()
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            For Each usage In tree.GetRoot().DescendantNodes().OfType(Of Syntax.IdentifierNameSyntax)().Where(Function(n) n.Identifier.ValueText = "Field")
                Dim storage = DirectCast(model.GetSymbolInfo(usage).Symbol, IFieldSymbol)
                Assert.True(storage.IsImplicitlyDeclared)
                Assert.Equal("Name", storage.AssociatedSymbol.Name)
                Assert.Equal("System.String", storage.Type.ToTestDisplayString())
            Next
        End Sub

        <Fact>
        Public Sub Field_Errors()
            Dim compilation = CreateCompilation("
Public Class C
    Public Property Item(index As Integer) As String
        Get
            Return Field
        End Get
        Set
            Field = Value
        End Set
    End Property

    Public ReadOnly Property Escaped As String
        Get
            Return [Field]
        End Get
    End Property

    Public Function NotAProperty() As String
        Return Field
    End Function
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC37397: 'Field' cannot be used in a property with parameters, because the property would need storage for each argument.
            Return Field
                   ~~~~~
BC37397: 'Field' cannot be used in a property with parameters, because the property would need storage for each argument.
            Field = Value
            ~~~~~
BC30451: 'Field' is not declared. It may be inaccessible due to its protection level.
            Return [Field]
                   ~~~~~~~
BC30451: 'Field' is not declared. It may be inaccessible due to its protection level.
        Return Field
               ~~~~~
]]></expected>)
        End Sub

        <Fact>
        Public Sub Field_NotAvailableInVisualBasic17_13()
            Dim compilation = CreateCompilation("
Public Class C
    Public ReadOnly Property Name As String
        Get
            Return Field
        End Get
    End Property
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=Vb17_13)

            compilation.AssertTheseDiagnostics(<expected><![CDATA[
BC30451: 'Field' is not declared. It may be inaccessible due to its protection level.
            Return Field
                   ~~~~~
]]></expected>)
        End Sub
    End Class
End Namespace
