' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullablePublicSymbolApiTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub DeclarationTypesAgreeWithStoredAnnotations()
            Dim compilation = CreateCompilation("
Public Class Customer
End Class
Public Class Repository
    Public Current As Customer?
    Public Property Selected As Customer?
    Public Function Find(name As String?) As Customer?
        Return Nothing
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            Dim repository = compilation.GlobalNamespace.GetTypeMembers("Repository").Single()
            Dim field = DirectCast(repository.GetMembers("Current").Single(), IFieldSymbol)
            Dim [property] = DirectCast(repository.GetMembers("Selected").Single(), IPropertySymbol)
            Dim method = DirectCast(repository.GetMembers("Find").Single(), IMethodSymbol)
            Dim parameter = method.Parameters.Single()

            Assert.Equal(NullableAnnotation.Annotated, field.NullableAnnotation)
            Assert.Equal(field.NullableAnnotation, field.Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, [property].NullableAnnotation)
            Assert.Equal([property].NullableAnnotation, [property].Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, parameter.NullableAnnotation)
            Assert.Equal(parameter.NullableAnnotation, parameter.Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, method.ReturnNullableAnnotation)
            Assert.Equal(method.ReturnNullableAnnotation, method.ReturnType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, method.ReceiverNullableAnnotation)
            Assert.Equal(method.ReceiverNullableAnnotation, method.ReceiverType.NullableAnnotation)

            Dim customer = compilation.GlobalNamespace.GetTypeMembers("Customer").Single()
            Assert.Equal(customer, field.Type, SymbolEqualityComparer.Default)
            Assert.NotEqual(customer, field.Type, SymbolEqualityComparer.IncludeNullability)
            Assert.Equal(SpecialType.System_String, parameter.Type.SpecialType)
        End Sub

        <Fact>
        Public Sub FlowAttributesDoNotChangeDeclaredAnnotations()
            Dim compilation = CreateCompilation("
Imports System.Diagnostics.CodeAnalysis
Public Class Repository
    Public Function Find(Of T)(value As T) As <NotNull> T
        Return value
    End Function
    Public Function Plain(Of T)(value As T) As T
        Return value
    End Function
    Public Function Maybe(value As String) As <MaybeNull> String
        Return value
    End Function
    Public Function Allowed(value As <AllowNull> String) As String
        Return value
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            Dim repository = compilation.GlobalNamespace.GetTypeMembers("Repository").Single()
            Dim find = DirectCast(repository.GetMembers("Find").Single(), IMethodSymbol)
            Dim plain = DirectCast(repository.GetMembers("Plain").Single(), IMethodSymbol)
            Dim maybe = DirectCast(repository.GetMembers("Maybe").Single(), IMethodSymbol)
            Dim allowed = DirectCast(repository.GetMembers("Allowed").Single(), IMethodSymbol)

            Assert.Equal(plain.ReturnNullableAnnotation, find.ReturnNullableAnnotation)
            Assert.Equal(find.ReturnNullableAnnotation, find.ReturnType.NullableAnnotation)
            Assert.Equal(maybe.ReturnNullableAnnotation, maybe.ReturnType.NullableAnnotation)
            Assert.NotEqual(NullableAnnotation.Annotated, maybe.ReturnNullableAnnotation)
            Assert.Equal(allowed.Parameters.Single().NullableAnnotation, allowed.Parameters.Single().Type.NullableAnnotation)
            Assert.NotEqual(NullableAnnotation.Annotated, allowed.Parameters.Single().NullableAnnotation)
        End Sub

        <Fact>
        Public Sub NamedTypeArgumentsExposeStoredAnnotations()
            Dim compilation = CreateCompilation("
Imports System.Collections.Generic
Public Class Holder
    Public Names As List(Of String?)
    Public Nested As List(Of List(Of String?))
    Public Pairs As Dictionary(Of String, String?)
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            Dim holder = compilation.GlobalNamespace.GetTypeMembers("Holder").Single()
            Dim names = DirectCast(DirectCast(holder.GetMembers("Names").Single(), IFieldSymbol).Type, INamedTypeSymbol)
            Assert.Equal(1, names.TypeArguments.Length)
            Assert.Equal(1, names.TypeArgumentNullableAnnotations.Length)
            Assert.Equal(NullableAnnotation.Annotated, names.TypeArgumentNullableAnnotations(0))
            Assert.Equal(NullableAnnotation.Annotated, names.TypeArguments(0).NullableAnnotation)

            Dim nested = DirectCast(DirectCast(holder.GetMembers("Nested").Single(), IFieldSymbol).Type, INamedTypeSymbol)
            Dim inner = DirectCast(nested.TypeArguments(0), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, inner.TypeArguments(0).NullableAnnotation)

            Dim pairs = DirectCast(DirectCast(holder.GetMembers("Pairs").Single(), IFieldSymbol).Type, INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, pairs.TypeArgumentNullableAnnotations(0))
            Assert.Equal(NullableAnnotation.NotAnnotated, pairs.TypeArguments(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, pairs.TypeArguments(1).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, names.OriginalDefinition.TypeArgumentNullableAnnotations(0))
        End Sub

        <Fact>
        Public Sub MethodConstructionPreservesAnnotationsWithoutMutatingDefinition()
            Dim compilation = CreateCompilation("
Public Class Box(Of T)
End Class
Public Class Factory
    Public Function Make(Of T)(value As T) As T
        Return value
    End Function
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)

            Dim definition = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Factory").Single().GetMembers("Make").Single(), IMethodSymbol)
            Dim [string] = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim annotated = [string].WithNullableAnnotation(NullableAnnotation.Annotated)
            Dim first = definition.Construct(annotated)
            Dim second = definition.Construct([string].WithNullableAnnotation(NullableAnnotation.NotAnnotated))

            Assert.Equal(NullableAnnotation.Annotated, first.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, first.TypeArguments.Single().NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, second.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.None, definition.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, first.TypeArgumentNullableAnnotations.Single())

            Dim box = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Box").Single(), INamedTypeSymbol)
            Dim parameter = box.TypeParameters.Single()
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            ' An explicit annotation on an identity substitution is a public projection. The shared definition stays oblivious.
            Assert.NotSame(box, identity)
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArgumentNullableAnnotations.Single())
            Assert.True(box.Equals(identity, SymbolEqualityComparer.Default))
        End Sub

        <Fact>
        Public Sub ArrayAndTupleConstructionKeepIndependentAnnotations()
            Dim compilation = CreateCompilation("
Public Class Marker
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim publicCompilation As Compilation = compilation
            Dim [string] = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim annotated = publicCompilation.CreateArrayTypeSymbol([string], 1, NullableAnnotation.Annotated)
            Dim plain = publicCompilation.CreateArrayTypeSymbol([string], 1, NullableAnnotation.NotAnnotated)
            Assert.Equal(NullableAnnotation.Annotated, annotated.ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, annotated.ElementType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, plain.ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, annotated.ElementNullableAnnotation)

            Dim tuple = publicCompilation.CreateTupleTypeSymbol(
                ImmutableArray.Create([string], [string]),
                elementNames:=Nothing,
                elementLocations:=Nothing,
                elementNullableAnnotations:=ImmutableArray.Create(NullableAnnotation.None, NullableAnnotation.Annotated))
            Dim underlying = tuple.TupleUnderlyingType
            Assert.Equal(NullableAnnotation.Annotated, underlying.TypeArguments(1).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, tuple.TupleElements(1).NullableAnnotation)
            Assert.Equal(tuple.TupleElements(1).NullableAnnotation, tuple.TupleElements(1).Type.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub RecursiveConstraintComparisonTerminates()
            Dim compilation = CreateCompilation("
Public Class Node(Of T As Node(Of T))
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim node = DirectCast(compilation.GlobalNamespace.GetTypeMembers("Node").Single(), INamedTypeSymbol)
            Dim parameter = node.TypeParameters.Single()
            Assert.True(parameter.Equals(parameter, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(parameter.GetHashCode(), parameter.GetHashCode())
            Dim constraint = parameter.ConstraintTypes.Single()
            Assert.Equal(NullableAnnotation.None, DirectCast(constraint, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub CSharpIdentityConstructionReturnsTheDefinition()
            Dim compilation = CSharpCompilation.Create("c",
                {CSharpSyntaxTree.ParseText("public class Box<T> {}")},
                {MetadataReference.CreateFromFile(GetType(Object).Assembly.Location)})
            Dim box = compilation.GetTypeByMetadataName("Box`1")
            Dim parameter = box.TypeParameters.Single()
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.NotSame(box, identity)
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub EventsDoNotInventATopLevelAnnotation()
            Dim compilation = CreateCompilation("
Public Delegate Sub Handler(Of T)(value As T)
Public Class Publisher
    Public Event Changed As Handler(Of String?)
End Class
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim publisher = compilation.GlobalNamespace.GetTypeMembers("Publisher").Single()
            Dim [event] = DirectCast(publisher.GetMembers("Changed").Single(), IEventSymbol)
            Assert.Equal(NullableAnnotation.None, [event].NullableAnnotation)
            Dim handler = DirectCast([event].Type, INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, handler.TypeArguments.Single().NullableAnnotation)
        End Sub
    End Class
End Namespace
