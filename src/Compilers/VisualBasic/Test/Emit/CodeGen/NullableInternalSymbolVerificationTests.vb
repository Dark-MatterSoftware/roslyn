' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols.Retargeting
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGen

    ''' <summary>
    ''' LC-1G.1a verification of class constraints, NotNull contracts,
    ''' reduced-definition isolation, event capture, and retargeting.
    ''' </summary>
    Public Class NullableInternalSymbolVerificationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub ClassConstraint_DoesNotHideANullableTypeArgument()
            Dim compilation = Compile("
Imports System
Imports System.Diagnostics.CodeAnalysis
Imports System.Runtime.CompilerServices
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
End Namespace
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
    <Extension>
    Public Function Constrained(Of T As Class)(value As T) As T
        Return value
    End Function
    <Extension>
    Public Function Promised(Of T As Class)(value As T) As <NotNull> T
        Return value
    End Function
    <Extension>
    Public Function Marked(Of T As Class)(value As T) As T?
        Return value
    End Function
End Module
Module Program
    Sub Test(value As String?, ready As String)
        Dim loose = Echo(value)
        Dim looseReduced = value.Echo()
        Dim readyCall = Echo(ready)
        Dim readyReduced = ready.Echo()
        Dim classResult = Constrained(value)
        Dim classReduced = value.Constrained()
        Dim classReady = Constrained(ready)
        Dim classReadyReduced = ready.Constrained()
        Dim promisedResult = Promised(value)
        Dim promisedReduced = value.Promised()
        Dim markedResult = Marked(ready)
        Dim markedReduced = ready.Marked()
        Console.WriteLine(loose.Length)
        Console.WriteLine(looseReduced.Length)
        Console.WriteLine(readyCall.Length)
        Console.WriteLine(readyReduced.Length)
        Console.WriteLine(classResult.Length)
        Console.WriteLine(classReduced.Length)
        Console.WriteLine(classReady.Length)
        Console.WriteLine(classReadyReduced.Length)
        Console.WriteLine(promisedResult.Length)
        Console.WriteLine(promisedReduced.Length)
        Console.WriteLine(markedResult.Length)
        Console.WriteLine(markedReduced.Length)
    End Sub
End Module
")
            AssertSameReturn(compilation, "Echo(value)", "value.Echo()", NullableAnnotation.Annotated)
            AssertSameReturn(compilation, "Echo(ready)", "ready.Echo()", NullableAnnotation.NotAnnotated)
            AssertSameReturn(compilation, "Constrained(value)", "value.Constrained()", NullableAnnotation.Annotated)
            AssertSameReturn(compilation, "Constrained(ready)", "ready.Constrained()", NullableAnnotation.NotAnnotated)
            AssertSameReturn(compilation, "Promised(value)", "value.Promised()", NullableAnnotation.Annotated)
            AssertSameReturn(compilation, "Marked(ready)", "ready.Marked()", NullableAnnotation.Annotated)

            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:loose"), seen)
            Assert.True(seen.Contains("42512:looseReduced"), seen)
            Assert.True(Not seen.Contains("42512:readyCall"), seen)
            Assert.True(Not seen.Contains("42512:readyReduced"), seen)
            Assert.True(seen.Contains("42512:classResult"), seen)
            Assert.True(seen.Contains("42512:classReduced"), seen)
            Assert.True(Not seen.Contains("42512:classReady"), seen)
            Assert.True(Not seen.Contains("42512:classReadyReduced"), seen)
            Assert.True(Not seen.Contains("42512:promisedResult"), seen)
            Assert.True(Not seen.Contains("42512:promisedReduced"), seen)
            Assert.True(seen.Contains("42512:markedResult"), seen)
            Assert.True(seen.Contains("42512:markedReduced"), seen)
            Assert.Equal(NullableAnnotation.Annotated, BoundMethod(compilation, "value.Constrained()").GetTypeArgumentNullableAnnotations().Single())
            Assert.True(DirectCast(BoundMethod(compilation, "value.Promised()"), IMethodSymbol).TypeArgumentNullableAnnotations.IsEmpty)
        End Sub

        <Fact>
        Public Sub ObliviousAndImportedConstraints_MatchTheSourceCall()
            Dim library = Compile("
Imports System
Imports System.Diagnostics.CodeAnalysis
Imports System.Runtime.CompilerServices
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
End Namespace
Public Module Extensions
    <Extension>
    Public Function Constrained(Of T As Class)(value As T) As T
        Return value
    End Function
    <Extension>
    Public Function Promised(Of T As Class)(value As T) As <NotNull> T
        Return value
    End Function
End Module
")
            Dim consumer = Compile("
Imports System
Module Program
    Sub Test(value As String?, ready As String)
        Dim classResult = value.Constrained()
        Dim promisedResult = value.Promised()
        Dim readyResult = ready.Constrained()
        Console.WriteLine(classResult.Length)
        Console.WriteLine(promisedResult.Length)
        Console.WriteLine(readyResult.Length)
    End Sub
End Module
", reference:=library.EmitToImageReference())
            Assert.Equal(NullableAnnotation.Annotated, BoundMethod(consumer, "value.Constrained()").GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, BoundMethod(consumer, "value.Promised()").GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.NotAnnotated, BoundMethod(consumer, "ready.Constrained()").GetReturnNullableAnnotation())
            Dim seen = Picture(consumer)
            Assert.True(seen.Contains("42512:classResult"), seen)
            Assert.True(Not seen.Contains("42512:promisedResult"), seen)
            Assert.True(Not seen.Contains("42512:readyResult"), seen)

            Dim oblivious = Compile("
Imports System.Runtime.CompilerServices
Module Extensions
    <Extension>
    Public Function Constrained(Of T As Class)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Sub Test(value As String)
        Dim reduced = value.Constrained()
    End Sub
End Module
", nullable:=False)
            Assert.Equal(NullableAnnotation.None, BoundMethod(oblivious, "value.Constrained()").GetReturnNullableAnnotation())
        End Sub

        <Fact>
        Public Sub DistinctReducedCalls_DoNotReplaceEachOthersAnnotations()
            Dim compilation = Compile("
Imports System.Runtime.CompilerServices
Public Class Pair(Of T)
    Public Item As T
End Class
Module Extensions
    <Extension>
    Public Function Pick(Of T, U)(value As T, other As U) As Pair(Of U)
        Return Nothing
    End Function
    <Extension>
    Public Function Choose(Of T, U, V)(value As Pair(Of T), left As U, right As V) As Pair(Of V)
        Return Nothing
    End Function
End Module
Module Program
    Sub Test(value As String?, ready As String, pair As Pair(Of String?))
        Dim fromValue = value.Pick(ready)
        Dim fromReady = ready.Pick(value)
        Dim nested = pair.Choose(ready, value)
        Dim repeated = value.Pick(ready)
    End Sub
End Module
")
            Dim fromValue = BoundMethod(compilation, "value.Pick(ready)")
            Dim fromReady = BoundMethod(compilation, "ready.Pick(value)")
            Dim nested = BoundMethod(compilation, "pair.Choose(ready, value)")
            Dim repeated = BoundMethod(compilation, "value.Pick(ready)")
            Assert.False(Object.ReferenceEquals(fromValue.OriginalDefinition, fromReady.OriginalDefinition))
            Assert.False(Object.ReferenceEquals(fromValue, repeated))
            AssertOriginal(fromValue, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            AssertOriginal(fromReady, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
            Assert.Equal(NullableAnnotation.NotAnnotated, fromValue.GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(fromValue.ReturnType, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, fromReady.GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(fromReady.ReturnType, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            AssertOriginal(nested, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(nested.ReturnType, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            AssertOriginal(fromValue, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
            Assert.Equal(NullableAnnotation.NotAnnotated, fromValue.Parameters.Single().GetDeclaredNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, fromReady.Parameters.Single().GetDeclaredNullableAnnotation())
        End Sub

        <Fact>
        Public Sub OneReducedDefinition_KeepsSeparateConstructions()
            Dim compilation = Compile("
Imports System.Runtime.CompilerServices
Public Class Pair(Of T)
    Public Item As T
End Class
Module Extensions
    <Extension>
    Public Function Pick(Of T, U)(value As T, other As U) As Pair(Of U)
        Return Nothing
    End Function
End Module
Module Program
    Sub Test(value As String?)
        Dim first = value.Pick(value)
    End Sub
End Module
")
            Dim definition = BoundMethod(compilation, "value.Pick(value)").OriginalDefinition
            Dim stringType = compilation.GetSpecialType(SpecialType.System_String)
            Dim annotated = definition.Construct(ImmutableArray.Create(Of TypeSymbol)(stringType))
            Dim nonNull = definition.Construct(ImmutableArray.Create(Of TypeSymbol)(stringType))
            annotated.SetTypeArgumentNullableAnnotations(ImmutableArray.Create(NullableAnnotation.Annotated))
            nonNull.SetTypeArgumentNullableAnnotations(ImmutableArray.Create(NullableAnnotation.NotAnnotated))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(annotated.ReturnType, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(nonNull.ReturnType, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            definition.SetTypeArgumentNullableAnnotations(ImmutableArray.Create(NullableAnnotation.None, NullableAnnotation.None))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(annotated.ReturnType, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(nonNull.ReturnType, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.Annotated, annotated.Parameters.Single().GetDeclaredNullableAnnotation())
            Assert.Equal(NullableAnnotation.NotAnnotated, nonNull.Parameters.Single().GetDeclaredNullableAnnotation())
        End Sub

        <Fact>
        Public Sub EventType_CapturesTheConstructedDelegateAnnotation()
            Dim compilation = Compile("
Public Delegate Sub MessageHandler(Of T)(message As T)
Public Class Box
    Public Event MessageReceived As MessageHandler(Of String?)
    Public Event Ready As MessageHandler(Of String)
    Public Received As MessageHandler(Of String?)
    Public Plain As MessageHandler(Of String)
End Class
")
            Dim box = compilation.GlobalNamespace.GetTypeMembers("Box").Single()
            Dim received = box.GetMember(Of EventSymbol)("MessageReceived")
            Dim ready = box.GetMember(Of EventSymbol)("Ready")
            Dim receivedField = box.GetMember(Of FieldSymbol)("Received")
            Dim plainField = box.GetMember(Of FieldSymbol)("Plain")
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(receivedField.Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(plainField.Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(DirectCast(receivedField.Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single(), DirectCast(received.Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(DirectCast(plainField.Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single(), DirectCast(ready.Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.None, DirectCast(received, IEventSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(received.Type, NamedTypeSymbol).DelegateInvokeMethod.Parameters.Single().GetDeclaredNullableAnnotation())

            Dim sourceAssembly = DirectCast(compilation.SourceModule.ContainingAssembly, SourceAssemblySymbol)
            Dim retargeting = New RetargetingAssemblySymbol(sourceAssembly, isLinked:=False)
            retargeting.SetCorLibrary(sourceAssembly.CorLibrary)
            Dim retargeted = DirectCast(retargeting.GlobalNamespace.GetTypeMembers("Box").Single(), RetargetingNamedTypeSymbol)
            Dim retargetedEvent = retargeted.GetMember(Of EventSymbol)("MessageReceived")
            Assert.IsType(Of RetargetingEventSymbol)(retargetedEvent)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(retargetedEvent.Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.None, DirectCast(retargetedEvent, IEventSymbol).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub RetargetedMethods_KeepNestedAnnotationsAndConstraints()
            Dim compilation = Compile("
Public Class Pair(Of T)
    Public Item As T
End Class
Public Class Box
    Public Function Make(Of T As Class)(value As Pair(Of T())) As Pair(Of Pair(Of T))
        Return Nothing
    End Function
End Class
")
            Dim sourceAssembly = DirectCast(compilation.SourceModule.ContainingAssembly, SourceAssemblySymbol)
            Dim retargeting = New RetargetingAssemblySymbol(sourceAssembly, isLinked:=False)
            retargeting.SetCorLibrary(sourceAssembly.CorLibrary)
            Dim sourceMethod = compilation.GlobalNamespace.GetTypeMembers("Box").Single().GetMember(Of MethodSymbol)("Make")
            Dim retargetedMethod = retargeting.GlobalNamespace.GetTypeMembers("Box").Single().GetMember(Of MethodSymbol)("Make")
            Assert.IsType(Of RetargetingMethodSymbol)(retargetedMethod)
            Assert.Equal(sourceMethod.TypeParameters.Single().GetReferenceTypeConstraintNullableAnnotation(), retargetedMethod.TypeParameters.Single().GetReferenceTypeConstraintNullableAnnotation())
            Assert.Equal(sourceMethod.Parameters.Single().GetDeclaredNullableAnnotation(), retargetedMethod.Parameters.Single().GetDeclaredNullableAnnotation())
            Dim sourceParameter = DirectCast(sourceMethod.Parameters.Single().Type, NamedTypeSymbol)
            Dim retargetedParameter = DirectCast(retargetedMethod.Parameters.Single().Type, NamedTypeSymbol)
            Assert.Equal(DirectCast(sourceParameter.TypeArgumentsNoUseSiteDiagnostics.Single(), ArrayTypeSymbol).ElementNullableAnnotation, DirectCast(retargetedParameter.TypeArgumentsNoUseSiteDiagnostics.Single(), ArrayTypeSymbol).ElementNullableAnnotation)

            Dim sourceReturn = DirectCast(sourceMethod.ReturnType, NamedTypeSymbol)
            Dim retargetedReturn = DirectCast(retargetedMethod.ReturnType, NamedTypeSymbol)
            Assert.Equal(sourceReturn.GetTypeArgumentNullableAnnotations().Single(), retargetedReturn.GetTypeArgumentNullableAnnotations().Single())
            Dim substituted = retargetedMethod.Construct(ImmutableArray.Create(Of TypeSymbol)(compilation.GetSpecialType(SpecialType.System_String)))
            substituted.SetTypeArgumentNullableAnnotations(ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim substitutedReturn = DirectCast(substituted.ReturnType, NamedTypeSymbol)
            Dim inner = DirectCast(substitutedReturn.TypeArgumentsNoUseSiteDiagnostics.Single(), NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, inner.GetTypeArgumentNullableAnnotations().Single())
            Dim other = retargetedMethod.Construct(ImmutableArray.Create(Of TypeSymbol)(compilation.GetSpecialType(SpecialType.System_String)))
            other.SetTypeArgumentNullableAnnotations(ImmutableArray.Create(NullableAnnotation.NotAnnotated))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(substituted.ReturnType, NamedTypeSymbol).TypeArgumentsNoUseSiteDiagnostics.Single(), NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(DirectCast(other.ReturnType, NamedTypeSymbol).TypeArgumentsNoUseSiteDiagnostics.Single(), NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
        End Sub

        <Fact>
        Public Sub ReducedExtension_FromRetargetedMethod_KeepsTheForwardedAnnotation()
            Dim compilation = Compile("
Imports System.Runtime.CompilerServices
Public Module Extensions
    <Extension>
    Public Function Echo(Of T, U)(value As T, other As U) As U
        Return other
    End Function
End Module
")
            Dim sourceAssembly = DirectCast(compilation.SourceModule.ContainingAssembly, SourceAssemblySymbol)
            Dim retargeting = New RetargetingAssemblySymbol(sourceAssembly, isLinked:=False)
            retargeting.SetCorLibrary(sourceAssembly.CorLibrary)
            Dim retargeted = retargeting.GlobalNamespace.GetModuleMembers("Extensions").Single().GetMember(Of MethodSymbol)("Echo")
            Assert.IsType(Of RetargetingMethodSymbol)(retargeted)
            Dim useSite = CompoundUseSiteInfo(Of AssemblySymbol).Discarded
            Dim receiver = compilation.GetSpecialType(SpecialType.System_String)
            Dim first = retargeted.ReduceExtensionMethod(receiver, useSite, LanguageVersion.VbNext)
            Dim second = retargeted.ReduceExtensionMethod(receiver, useSite, LanguageVersion.VbNext)
            Assert.NotNull(first)
            Assert.True(first.IsReducedExtensionMethod)
            Assert.IsType(Of RetargetingMethodSymbol)(first.ReducedFrom)
            Assert.False(Object.ReferenceEquals(first, second))
            first.SetTypeArgumentNullableAnnotations(ImmutableArray.Create(NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated))
            second.SetTypeArgumentNullableAnnotations(ImmutableArray.Create(NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated))
            Assert.Equal(NullableAnnotation.NotAnnotated, first.GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, second.GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.NotAnnotated, first.GetReturnNullableAnnotation())
        End Sub

        Private Sub AssertSameReturn(compilation As VisualBasicCompilation, unreducedText As String, reducedText As String, expected As NullableAnnotation)
            Dim unreduced = BoundMethod(compilation, unreducedText)
            Dim reduced = BoundMethod(compilation, reducedText)
            Assert.Equal(expected, unreduced.GetReturnNullableAnnotation())
            Assert.Equal(unreduced.GetReturnNullableAnnotation(), reduced.GetReturnNullableAnnotation())
        End Sub

        Private Shared Function Picture(compilation As VisualBasicCompilation) As String
            Return String.Join("|", compilation.GetDiagnostics().
                Where(Function(diagnostic) diagnostic.Code >= 42510 AndAlso diagnostic.Code <= 42532).
                Select(Function(diagnostic) CInt(diagnostic.Code).ToString() & ":" & SpanText(diagnostic)).
                OrderBy(Function(text) text, StringComparer.Ordinal))
        End Function

        Private Shared Function SpanText(diagnostic As Diagnostic) As String
            If diagnostic.Location Is Nothing OrElse diagnostic.Location.SourceTree Is Nothing Then
                Return ""
            End If

            Return diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan)
        End Function

        Private Function Compile(source As String, Optional nullable As Boolean = True, Optional reference As MetadataReference = Nothing) As VisualBasicCompilation
            Dim options = TestOptions.ReleaseDll
            If nullable Then
                options = options.WithNullableContext(NullableContextOptions.Enable)
            End If

            Dim references = If(reference Is Nothing, Nothing, {reference})
            Return CreateCompilation(source, references:=references, options:=options, parseOptions:=VbNext)
        End Function

        Private Function BoundMethod(compilation As VisualBasicCompilation, text As String) As MethodSymbol
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            For Each callSyntax In tree.GetRoot().DescendantNodes().OfType(Of Syntax.InvocationExpressionSyntax)()
                If callSyntax.ToString() <> text Then
                    Continue For
                End If

                Dim symbol = TryCast(model.GetSymbolInfo(callSyntax).Symbol, MethodSymbol)
                If symbol Is Nothing Then
                    Throw New InvalidOperationException(text & " " & String.Join("; ", compilation.GetDiagnostics().Select(Function(d) d.ToString())))
                End If

                Return symbol
            Next

            Throw New InvalidOperationException(text)
        End Function

        Private Shared Sub AssertOriginal(method As MethodSymbol, ParamArray expected() As NullableAnnotation)
            Dim owner = method.OriginalDefinition
            Dim actual = owner.GetTypeArgumentNullableAnnotations()
            Assert.Equal(expected.Length, actual.Length)
            For index = 0 To expected.Length - 1
                Assert.Equal(expected(index), actual(index))
            Next
        End Sub

    End Class

End Namespace
