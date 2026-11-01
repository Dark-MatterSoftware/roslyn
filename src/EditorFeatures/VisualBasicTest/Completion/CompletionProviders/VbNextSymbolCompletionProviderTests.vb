' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.VisualBasic.Completion.Providers

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Completion.CompletionProviders
    <Trait(Traits.Feature, Traits.Features.Completion)>
    Public Class VbNextSymbolCompletionProviderTests
        Inherits AbstractVisualBasicCompletionProviderTests

        Friend Overrides Function GetCompletionProviderType() As Type
            Return GetType(SymbolCompletionProvider)
        End Function

        Private Shared Function Shapes(languageVersion As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""" & languageVersion & """>
        <Document>
Interface IShape(Of TSelf As IShape(Of TSelf))
    Shared MustOverride Function Create() As TSelf
    Function Name() As String
End Interface

Module M
    Function Make(Of T As IShape(Of T))() As T
        Return T.$$
    End Function
End Module
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function SharedMustOverrideMemberAfterTypeParameter() As Task
            Await VerifyItemExistsAsync(Shapes("vbnext"), "Create")
        End Function

        <Fact>
        Public Async Function InstanceMemberMissingAfterTypeParameter() As Task
            Await VerifyItemIsAbsentAsync(Shapes("vbnext"), "Name")
        End Function

        <Fact>
        Public Async Function SharedMemberMissingAfterTypeParameterOn17_13() As Task
            Await VerifyItemIsAbsentAsync(Shapes("17.13"), "Create")
        End Function

        Private Shared Function SpanExtensions(languageVersion As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""" & languageVersion & """>
        <MetadataReferenceFromSource Language=""C#"" CommonReferences=""true"">
            <Document><![CDATA[
namespace System { public struct ReadOnlySpan<T> { public static implicit operator ReadOnlySpan<T>(T[] array) => default; } }
public static class SpanExtensions
{
    public static int CountOf<T>(this System.ReadOnlySpan<T> span, T value) where T : System.IEquatable<T> => 0;
}]]></Document>
        </MetadataReferenceFromSource>
        <Document>
Module M
    Sub Use()
        Dim a As Integer() = {1, 2}
        a.$$
    End Sub
End Module
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function SpanExtensionAfterArray() As Task
            Await VerifyItemExistsAsync(SpanExtensions("vbnext"), "CountOf")
        End Function

        <Fact>
        Public Async Function SpanExtensionMissingAfterArrayOn17_13() As Task
            Await VerifyItemIsAbsentAsync(SpanExtensions("17.13"), "CountOf")
        End Function

        Private Shared Function Records(body As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""vbnext"">
        <Document>
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class Animal(Name As String)
End Class

Record Class Dog(Name As String, Breed As String)
    Inherits Animal(Name)
" & body & "
End Class
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function EqualityContractInsideDerivedRecord() As Task
            Await VerifyItemExistsAsync(Records("
    Function Contract() As System.Type
        Return Me.$$
    End Function"), "EqualityContract")
        End Function

        <Fact>
        Public Async Function EqualityContractMissingOutsideRecord() As Task
            Await VerifyItemIsAbsentAsync(Records("
End Class

Module M
    Sub Use(d As Dog)
        d.$$
    End Sub
End Module

Class Unused"), "EqualityContract")
        End Function

        <Fact>
        Public Async Function InheritedPropertyOnceAfterDerivedRecord() As Task
            Await VerifyItemExistsAsync(Records("
End Class

Module M
    Sub Use(d As Dog)
        d.$$
    End Sub
End Module

Class Unused"), "Name")
        End Function

        <Fact>
        Public Async Function PrintMembersInsideDerivedRecord() As Task
            Await VerifyItemExistsAsync(Records("
    Function Describe(builder As System.Text.StringBuilder) As Boolean
        Return Me.$$
    End Function"), "PrintMembers")
        End Function

        <Fact>
        Public Async Function PrintMembersInsideRecordUnderSealedToString() As Task
            Await VerifyItemExistsAsync("<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""vbnext"">
        <Document>
Namespace System.Runtime.CompilerServices
    Public Class IsExternalInit
    End Class
End Namespace

Record Class SealedBase(Id As Integer)
    Public NotOverridable Overrides Function ToString() As String
        Return ""SealedBase""
    End Function
End Class

Record Class Derived(Id As Integer, Extra As String)
    Inherits SealedBase(Id)

    Function Describe(builder As System.Text.StringBuilder) As Boolean
        Return Me.$$
    End Function
End Class
        </Document>
    </Project>
</Workspace>", "PrintMembers")
        End Function

        <Fact>
        Public Async Function PrintMembersMissingOutsideRecord() As Task
            Await VerifyItemIsAbsentAsync(Records("
End Class

Module M
    Sub Use(d As Dog)
        d.$$
    End Sub
End Module

Class Unused"), "PrintMembers")
        End Function

        Private Shared Function AddressOfLocal(languageVersion As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""" & languageVersion & """>
        <Document>
Module M
    Function Twice(x As Integer) As Integer
        Return x * 2
    End Function

    Sub Use()
        Dim t = AddressOf Twice
        t.$$
    End Sub
End Module
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function InvokeAfterLambdaReturningAddressOf() As Task
            Await VerifyItemExistsAsync("<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""vbnext"">
        <Document>
Module M
    Sub Hello()
    End Sub

    Sub Use()
        Dim factory = Function() AddressOf Hello
        factory().$$
    End Sub
End Module
        </Document>
    </Project>
</Workspace>", "Invoke")
        End Function

        Private Shared Function DelegateConstrained(languageVersion As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""" & languageVersion & """>
        <Document>
Module M
    Function Describe(Of T As System.Delegate)(d As T) As Object
        Return d.$$
    End Function
End Module
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function DelegateMembersThroughDelegateConstraint() As Task
            Await VerifyItemExistsAsync(DelegateConstrained("vbnext"), "DynamicInvoke")
        End Function

        <Fact>
        Public Async Function InvokeAfterAddressOfLocal() As Task
            Await VerifyItemExistsAsync(AddressOfLocal("vbnext"), "Invoke")
        End Function

        <Fact>
        Public Async Function InvokeMissingAfterAddressOfLocalOn17_13() As Task
            Await VerifyItemIsAbsentAsync(AddressOfLocal("17.13"), "Invoke")
        End Function

        Private Shared Function NameOfInAttribute(languageVersion As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""" & languageVersion & """>
        <Document>
Class NoteAttribute
    Inherits System.Attribute
    Sub New(name As String)
    End Sub
End Class

Module M
    &lt;Note(NameOf($$))&gt;
    Sub F(count As Integer)
    End Sub
End Module
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function ParameterInNameOfInAttribute() As Task
            Await VerifyItemExistsAsync(NameOfInAttribute("vbnext"), "count")
        End Function

        <Fact>
        Public Async Function ParameterMissingInNameOfInAttributeOn17_13() As Task
            Await VerifyItemIsAbsentAsync(NameOfInAttribute("17.13"), "count")
        End Function

        Private Shared Function WaveTwentySeven(body As String) As String
            Return "<Workspace>
    <Project Language=""Visual Basic"" CommonReferencesNet6=""true"" LanguageVersion=""vbnext"">
        <Document>
Imports System

Class NoteAttribute
    Inherits Attribute
    Sub New(name As String)
    End Sub
End Class
" & body & "
        </Document>
    </Project>
</Workspace>"
        End Function

        <Fact>
        Public Async Function IndexMembersAfterIndexValue() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Module M
    Sub F()
        Dim fromEnd = ^1
        fromEnd.$$
    End Sub
End Module"), "IsFromEnd")
        End Function

        <Fact>
        Public Async Function StringMembersAfterNestedDeconstructionLocal() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Module M
    Sub F()
        Dim ((x, y), label) = ((1, 2), ""pair"")
        label.$$
    End Sub
End Module"), "Length")
        End Function

        <Fact>
        Public Async Function StringMembersAfterNestedForEachLocal() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Module M
    Sub F()
        For Each ((x, y), label) In {((1, 2), ""a"")}
            label.$$
        Next
    End Sub
End Module"), "Length")
        End Function

        <Fact>
        Public Async Function NullableMembersAfterLiftedIndex() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Module M
    Sub F(n As Integer?)
        Dim maybe = ^n
        maybe.$$
    End Sub
End Module"), "HasValue")
        End Function

        <Fact>
        Public Async Function NullableMembersAfterLiftedRange() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Module M
    Sub F(n As Integer?)
        Dim r = ..n
        r.$$
    End Sub
End Module"), "HasValue")
        End Function

        <Fact>
        Public Async Function RangeMembersAfterRangeValue() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Module M
    Sub F()
        Dim r = 1..^1
        r.$$
    End Sub
End Module"), "Start")
        End Function

        <Fact>
        Public Async Function MemoryMembersAfterMemorySlice() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Module M
    Sub F(mem As Memory(Of Integer))
        mem(1..3).$$
    End Sub
End Module"), "Span")
        End Function

        <Fact>
        Public Async Function PropertyParameterInNameOfInAttribute() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Class Table
    Default ReadOnly Property Item(&lt;Note(NameOf($$))&gt; key As String) As Integer
        Get
            Return 0
        End Get
    End Property
End Class"), "key")
        End Function

        <Fact>
        Public Async Function DelegateParameterInNameOfInAttribute() As Task
            Await VerifyItemExistsAsync(WaveTwentySeven("
Delegate Function Mapper(&lt;Note(NameOf($$))&gt; value As String) As String"), "value")
        End Function

        <Fact>
        Public Async Function StringMembersAfterStringRange() As Task
            Await VerifyItemExistsAsync("<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""vbnext"">
        <Document>
Module M
    Sub F(s As String)
        s(1..3).$$
    End Sub
End Module
        </Document>
    </Project>
</Workspace>", "Length")
        End Function

        <Fact>
        Public Async Function ElementMembersAfterListIndexFromEnd() As Task
            Await VerifyItemExistsAsync("<Workspace>
    <Project Language=""Visual Basic"" CommonReferences=""true"" LanguageVersion=""vbnext"">
        <Document>
Module M
    Sub F(values As System.Collections.Generic.List(Of String))
        values(^1).$$
    End Sub
End Module
        </Document>
    </Project>
</Workspace>", "Length")
        End Function
    End Class
End Namespace
