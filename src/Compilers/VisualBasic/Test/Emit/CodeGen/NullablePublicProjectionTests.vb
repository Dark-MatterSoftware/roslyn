' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullablePublicProjectionTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub Projection_PreservesAnnotationIdentityAndSpecializedInterfaces()
            Dim compilation = CreateCompilation("
Class Box(Of T)
    Public Items As T()
End Class
Class Ref(Of U As Class)
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)

            Dim canonical = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim annotated = canonical.WithNullableAnnotation(NullableAnnotation.Annotated)
            Dim notAnnotated = canonical.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            Dim oblivious = annotated.WithNullableAnnotation(NullableAnnotation.None)
            Dim annotatedAgain = annotated.WithNullableAnnotation(NullableAnnotation.Annotated)

            Assert.Equal(NullableAnnotation.None, canonical.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, annotated.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, notAnnotated.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, oblivious.NullableAnnotation)
            Assert.Same(annotated, annotatedAgain)
            Assert.Same(canonical, oblivious)
            Assert.Equal(NullableAnnotation.None, canonical.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, annotated.NullableAnnotation)

            Assert.Equal(SpecialType.System_String, annotated.SpecialType)
            Assert.Equal(TypeKind.Class, annotated.TypeKind)
            Assert.Equal(SymbolKind.NamedType, annotated.Kind)
            Assert.Equal("String", annotated.Name)
            Assert.Equal("String", annotated.MetadataName)
            Dim named = TryCast(annotated, INamedTypeSymbol)
            Assert.NotNull(named)
            Assert.Equal(0, named.Arity)
            Assert.Same(canonical, named.OriginalDefinition)
            Assert.Same(canonical, named.ConstructedFrom)
            Assert.NotNull(named.BaseType)
            Assert.True(named.GetMembers().Length > 0)

            Dim list = DirectCast(compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"), INamedTypeSymbol)
            Dim constructed = list.Construct(ImmutableArray.Create(annotated), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.Equal(NullableAnnotation.Annotated, constructed.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(SpecialType.System_String, constructed.TypeArguments.Single().SpecialType)
            Assert.Equal(NullableAnnotation.Annotated, annotated.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, canonical.NullableAnnotation)

            Dim fromProjection = list.Construct(annotated)
            Assert.Equal(NullableAnnotation.Annotated, fromProjection.TypeArgumentNullableAnnotations.Single())

            Dim plain = list.Construct(canonical)
            Assert.NotEqual(NullableAnnotation.Annotated, plain.TypeArgumentNullableAnnotations.Single())
            Assert.Throws(Of ArgumentException)(Function() list.Construct(ImmutableArray(Of ITypeSymbol).Empty, ImmutableArray(Of NullableAnnotation).Empty))
            Assert.Throws(Of ArgumentException)(Function() list.Construct(ImmutableArray.Create(canonical), ImmutableArray.Create(NullableAnnotation.Annotated, NullableAnnotation.Annotated)))

            Dim publicCompilation As Compilation = compilation
            Dim arrayAnnotated = DirectCast(publicCompilation.CreateArrayTypeSymbol(canonical).WithNullableAnnotation(NullableAnnotation.Annotated), IArrayTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, arrayAnnotated.NullableAnnotation)
            Assert.Equal(1, arrayAnnotated.Rank)
            Assert.True(arrayAnnotated.IsSZArray)
            Assert.Equal(SpecialType.System_String, arrayAnnotated.ElementType.SpecialType)

            Dim sourceArray = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Box").Single().GetMembers("Items").Single(), IFieldSymbol)
            Dim items = sourceArray.Type
            ' Items is T(), not String?(). Element annotation stays with the array symbol.
            Assert.IsAssignableFrom(Of IArrayTypeSymbol)(items)

            Dim definition = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Box").Single(), INamedTypeSymbol)
            Dim parameter = definition.TypeParameters.Single()
            Dim parameterAnnotated = parameter.WithNullableAnnotation(NullableAnnotation.Annotated)
            Dim typeParameter = TryCast(parameterAnnotated, ITypeParameterSymbol)
            Assert.NotNull(typeParameter)
            Assert.Equal(NullableAnnotation.Annotated, typeParameter.NullableAnnotation)
            Assert.Equal(0, typeParameter.Ordinal)
            Assert.Equal("T", typeParameter.Name)
            Assert.Same(definition, typeParameter.DeclaringType)
            Assert.Equal(NullableAnnotation.None, parameter.NullableAnnotation)

            Dim refParameter = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Ref").Single(), INamedTypeSymbol).TypeParameters.Single()
            Dim refAnnotated = refParameter.WithNullableAnnotation(NullableAnnotation.Annotated)
            Assert.NotNull(TryCast(refAnnotated, ITypeParameterSymbol))
            Assert.Equal(NullableAnnotation.Annotated, refAnnotated.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, refParameter.NullableAnnotation)
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(refParameter, refAnnotated))

            Dim missing = publicCompilation.CreateErrorTypeSymbol(compilation.GlobalNamespace, "Missing", 0)
            Dim missingAnnotated = missing.WithNullableAnnotation(NullableAnnotation.Annotated)
            Assert.NotNull(TryCast(missingAnnotated, IErrorTypeSymbol))
            Assert.Equal(NullableAnnotation.Annotated, missingAnnotated.NullableAnnotation)
            Assert.Equal(TypeKind.Error, missingAnnotated.TypeKind)
            Assert.Equal(NullableAnnotation.None, missing.NullableAnnotation)

            Dim conversion = compilation.ClassifyConversion(annotated, canonical)
            Assert.Equal(ConversionKind.Identity, conversion.Kind)
        End Sub

        <Fact>
        Public Sub Projection_DefaultEqualityIsSymmetricAndIncludeNullabilityObservesTopLevel()
            Dim compilation = CreateCompilation("Class Box : End Class", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            Dim canonical = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim annotated = canonical.WithNullableAnnotation(NullableAnnotation.Annotated)
            Dim notAnnotated = canonical.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            Dim otherAnnotated = canonical.WithNullableAnnotation(NullableAnnotation.Annotated)
            Dim intType = DirectCast(compilation.GetSpecialType(SpecialType.System_Int32), ITypeSymbol)
            Dim intAnnotated = intType.WithNullableAnnotation(NullableAnnotation.Annotated)

            Assert.True(SymbolEqualityComparer.Default.Equals(canonical, annotated))
            Assert.True(SymbolEqualityComparer.Default.Equals(annotated, canonical))
            Assert.True(SymbolEqualityComparer.Default.Equals(annotated, notAnnotated))
            Assert.True(SymbolEqualityComparer.Default.Equals(annotated, otherAnnotated))
            Assert.True(canonical.Equals(annotated))
            Assert.True(annotated.Equals(canonical))
            Assert.False(SymbolEqualityComparer.Default.Equals(annotated, intType))
            Assert.Equal(NullableAnnotation.Annotated, intAnnotated.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, intType.NullableAnnotation)
            Assert.True(SymbolEqualityComparer.IncludeNullability.Equals(intType, intAnnotated))
            Dim canonicalObject As Object = canonical
            Dim annotatedObject As Object = annotated
            Dim notAnnotatedObject As Object = notAnnotated
            Assert.True(canonicalObject.Equals(annotated))
            Assert.True(annotatedObject.Equals(canonical))
            Assert.True(annotatedObject.Equals(notAnnotatedObject))

            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(annotated, notAnnotated))
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(notAnnotated, annotated))
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(canonical, annotated))
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(annotated, canonical))
            Assert.True(SymbolEqualityComparer.IncludeNullability.Equals(annotated, otherAnnotated))
            Assert.Equal(canonical.GetHashCode(), annotated.GetHashCode())
            Assert.Equal(annotated.GetHashCode(), notAnnotated.GetHashCode())

            Dim forward = New Dictionary(Of ISymbol, Integer)(SymbolEqualityComparer.Default)
            forward.Add(canonical, 1)
            Assert.Equal(1, forward(annotated))
            Dim reverse = New HashSet(Of ISymbol)(SymbolEqualityComparer.Default)
            reverse.Add(annotated)
            Assert.Contains(canonical, reverse)

            Dim distinct = New HashSet(Of ISymbol)(SymbolEqualityComparer.IncludeNullability)
            distinct.Add(annotated)
            distinct.Add(notAnnotated)
            Assert.Equal(2, distinct.Count)
            Assert.DoesNotContain(notAnnotated, New HashSet(Of ISymbol)(SymbolEqualityComparer.IncludeNullability) From {annotated})
            distinct.Remove(annotated)
            Assert.Contains(notAnnotated, distinct)
        End Sub

        <Fact>
        Public Sub Projection_NestedAnnotationsAndArrays()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Class Box
    Public Plain As List(Of String)
    Public Marked As List(Of String?)
    Public Nested As List(Of List(Of String?))
    Public Letters As String()
    Public Maybe As String?()
    Public Pair As Dictionary(Of String, String?)
    Public PlainPair As Dictionary(Of String, String)
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            Dim box = compilation.GlobalNamespace.GetTypeMembers("Box").Single()
            Dim plain = DirectCast(box.GetMembers("Plain").Single(), IFieldSymbol).Type
            Dim marked = DirectCast(box.GetMembers("Marked").Single(), IFieldSymbol).Type
            Dim nested = DirectCast(DirectCast(box.GetMembers("Nested").Single(), IFieldSymbol).Type, INamedTypeSymbol)
            Dim letters = DirectCast(DirectCast(box.GetMembers("Letters").Single(), IFieldSymbol).Type, IArrayTypeSymbol)
            Dim maybe = DirectCast(DirectCast(box.GetMembers("Maybe").Single(), IFieldSymbol).Type, IArrayTypeSymbol)

            Assert.True(SymbolEqualityComparer.Default.Equals(plain, marked))
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(plain, marked))
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(marked, plain))
            Assert.Equal(NullableAnnotation.Annotated, nested.TypeArguments.Single().GetTypeArgumentNullableAnnotationsOrNone())
            Assert.Equal(NullableAnnotation.Annotated, maybe.ElementNullableAnnotation)
            Assert.NotEqual(NullableAnnotation.Annotated, letters.ElementNullableAnnotation)
            Assert.True(SymbolEqualityComparer.Default.Equals(letters, maybe))
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(letters, maybe))

            Dim pair = DirectCast(box.GetMembers("Pair").Single(), IFieldSymbol).Type
            Dim plainPair = DirectCast(box.GetMembers("PlainPair").Single(), IFieldSymbol).Type
            Assert.True(SymbolEqualityComparer.Default.Equals(pair, plainPair))
            Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(pair, plainPair))

            Dim markedReference = marked.WithNullableAnnotation(NullableAnnotation.Annotated)
            Assert.Equal(NullableAnnotation.Annotated, markedReference.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(markedReference, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, marked.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub Projection_VisitorsDispatchByKind()
            Dim compilation = CreateCompilation("
Class Box(Of T)
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            Dim stringType = DirectCast(compilation.GetSpecialType(SpecialType.System_String), INamedTypeSymbol)
            Dim annotated = stringType.WithNullableAnnotation(NullableAnnotation.Annotated)
            Dim visitor = New KindVisitor()
            annotated.Accept(visitor)
            Assert.Equal(SymbolKind.NamedType, visitor.Kind)
            Assert.Equal(NullableAnnotation.Annotated, visitor.Annotation)

            Dim publicCompilation As Compilation = compilation
            Dim arrayType = DirectCast(publicCompilation.CreateArrayTypeSymbol(stringType).WithNullableAnnotation(NullableAnnotation.NotAnnotated), IArrayTypeSymbol)
            arrayType.Accept(visitor)
            Assert.Equal(SymbolKind.ArrayType, visitor.Kind)

            Dim parameter = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Box").Single(), INamedTypeSymbol).TypeParameters.Single()
            parameter.WithNullableAnnotation(NullableAnnotation.Annotated).Accept(visitor)
            Assert.Equal(SymbolKind.TypeParameter, visitor.Kind)

            Assert.Contains("String", annotated.ToDisplayString())
            Assert.NotNull(annotated.GetDocumentationCommentId())
        End Sub

        <Fact>
        Public Sub Projection_RejectsForeignSymbolAndDoesNotMutateUnderConcurrency()
            Dim compilation = CreateCompilation("Class Box : End Class", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            Dim canonical = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim csharp = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("c")
            Dim foreign = csharp.GetSpecialType(SpecialType.System_String)
            Assert.Throws(Of ArgumentException)(Function() compilation.ClassifyConversion(foreign, canonical))

            Dim results = New ConcurrentBag(Of NullableAnnotation)()
            System.Threading.Tasks.Parallel.For(0, 32,
                Sub(index As Integer)
                    Dim annotation = If(index Mod 2 = 0, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
                    Dim projected = canonical.WithNullableAnnotation(annotation)
                    results.Add(projected.NullableAnnotation)
                    Dim named = DirectCast(projected, INamedTypeSymbol)
                    Dim unused = named.Name
                    Assert.Equal(annotation, projected.NullableAnnotation)
                    Assert.True(SymbolEqualityComparer.Default.Equals(canonical, projected))
                    If annotation = NullableAnnotation.Annotated Then
                        Assert.False(SymbolEqualityComparer.IncludeNullability.Equals(canonical, projected))
                    End If
                End Sub)
            Assert.Equal(32, results.Count)
            Assert.Equal(NullableAnnotation.None, canonical.NullableAnnotation)
            Assert.Contains(NullableAnnotation.Annotated, results)
            Assert.Contains(NullableAnnotation.NotAnnotated, results)
        End Sub

        <Fact>
        Public Sub Projection_RepeatedTransformationIsBounded()
            Dim compilation = CreateCompilation("Class Box : End Class", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            Dim canonical = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
#If Not NET472 Then
            Dim before = GC.GetAllocatedBytesForCurrentThread()
#End If
            Dim watch = System.Diagnostics.Stopwatch.StartNew()
            Dim last As ITypeSymbol = canonical
            For i = 1 To 2000
                last = last.WithNullableAnnotation(If(i Mod 2 = 0, NullableAnnotation.NotAnnotated, NullableAnnotation.Annotated))
            Next
            watch.Stop()
            Assert.Equal(NullableAnnotation.NotAnnotated, last.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, canonical.NullableAnnotation)
            Assert.True(watch.ElapsedMilliseconds < 5000, "elapsed=" & watch.ElapsedMilliseconds.ToString())
#If Not NET472 Then
            Dim allocated = GC.GetAllocatedBytesForCurrentThread() - before
            Assert.True(allocated < 50L * 1024L * 1024L, "allocated=" & allocated.ToString())
#End If
        End Sub

        Private NotInheritable Class KindVisitor
            Inherits SymbolVisitor

            Public Kind As SymbolKind
            Public Annotation As NullableAnnotation

            Public Overrides Sub VisitNamedType(symbol As INamedTypeSymbol)
                Kind = symbol.Kind
                Annotation = symbol.NullableAnnotation
            End Sub

            Public Overrides Sub VisitArrayType(symbol As IArrayTypeSymbol)
                Kind = symbol.Kind
                Annotation = symbol.NullableAnnotation
            End Sub

            Public Overrides Sub VisitTypeParameter(symbol As ITypeParameterSymbol)
                Kind = symbol.Kind
                Annotation = symbol.NullableAnnotation
            End Sub
        End Class

    End Class

    Friend Module NullableProjectionTestExtensions
        <System.Runtime.CompilerServices.Extension>
        Friend Function GetTypeArgumentNullableAnnotationsOrNone(type As ITypeSymbol) As NullableAnnotation
            Dim named = TryCast(type, INamedTypeSymbol)
            If named Is Nothing OrElse named.TypeArgumentNullableAnnotations.IsDefaultOrEmpty Then
                Return NullableAnnotation.None
            End If
            Return named.TypeArgumentNullableAnnotations.Single()
        End Function
    End Module

End Namespace
