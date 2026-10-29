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
    End Class
End Namespace
