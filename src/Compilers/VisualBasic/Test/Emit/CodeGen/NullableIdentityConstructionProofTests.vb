' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Diagnostics
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullableIdentityConstructionProofTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub CSharp_NamedAndMethodIdentityContract()
            Dim box = CSharpType("Box`1")
            Dim parameter = box.TypeParameters.Single()
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim unchanged = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.None))
            Assert.NotSame(box, identity)
            Assert.Same(box, unchanged)
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArguments.Single().NullableAnnotation)
            Assert.True(identity.ConstructedFrom.Equals(box, SymbolEqualityComparer.Default))
            Assert.True(identity.OriginalDefinition.Equals(box, SymbolEqualityComparer.Default))
            Assert.True(box.Equals(identity, SymbolEqualityComparer.Default))
            Assert.False(box.Equals(identity, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(SymbolEqualityComparer.Default.GetHashCode(box), SymbolEqualityComparer.Default.GetHashCode(identity))

            Dim field = DirectCast(identity.GetMembers("Value").Single(), IFieldSymbol)
            Dim definitionField = DirectCast(box.GetMembers("Value").Single(), IFieldSymbol)
            Assert.Equal(NullableAnnotation.Annotated, field.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, field.Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, definitionField.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, definitionField.Type.NullableAnnotation)
            Assert.True(field.ContainingType.Equals(identity, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(identity.GetMembers("GetValue").Single(), IMethodSymbol).ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(identity.GetMembers("SetValue").Single(), IMethodSymbol).Parameters.Single().NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(identity.GetMembers("GetItems").Single(), IMethodSymbol).ReturnType, IArrayTypeSymbol).ElementNullableAnnotation)

            Dim echo = DirectCast(CSharpType("Factory").GetMembers("Echo").Single(), IMethodSymbol)
            Dim echoIdentity = echo.Construct(ImmutableArray.Create(Of ITypeSymbol)(echo.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.NotSame(echo, echoIdentity)
            Assert.Equal(NullableAnnotation.None, echo.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.ReturnType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.Parameters.Single().Type.NullableAnnotation)
            Assert.False(echo.Equals(echoIdentity, SymbolEqualityComparer.Default))
            Assert.False(echo.Equals(echoIdentity, SymbolEqualityComparer.IncludeNullability))
            Dim echoAgain = echo.Construct(ImmutableArray.Create(Of ITypeSymbol)(echo.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.Equal(True, echoIdentity.Equals(echoAgain, SymbolEqualityComparer.Default))
            Assert.Equal(True, echoIdentity.Equals(echoAgain, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(SymbolEqualityComparer.Default.GetHashCode(echoIdentity), SymbolEqualityComparer.Default.GetHashCode(echoAgain))
            Dim echoNotAnnotated = echo.Construct(ImmutableArray.Create(Of ITypeSymbol)(echo.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.NotAnnotated))
            Assert.True(echoIdentity.Equals(echoNotAnnotated, SymbolEqualityComparer.Default))
            Assert.False(echoIdentity.Equals(echoNotAnnotated, SymbolEqualityComparer.IncludeNullability))
        End Sub

        <Fact>
        Public Sub VisualBasic_NamedIdentityRetainsAnnotationAndMembers()
            Dim compilation = CompileVisualBasic()
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim parameter = box.TypeParameters.Single()
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim unchanged = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.None))

            Assert.NotSame(box, identity)
            Assert.Same(box, unchanged)
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArguments.Single().NullableAnnotation)
            Assert.True(box.Equals(identity, SymbolEqualityComparer.Default))
            Assert.False(box.Equals(identity, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(SymbolEqualityComparer.Default.GetHashCode(box), SymbolEqualityComparer.Default.GetHashCode(identity))

            Dim field = DirectCast(identity.GetMembers("Value").Single(), IFieldSymbol)
            Dim definitionField = DirectCast(box.GetMembers("Value").Single(), IFieldSymbol)
            Assert.Equal(NullableAnnotation.Annotated, field.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, field.Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, definitionField.NullableAnnotation)
            Assert.True(field.ContainingType.Equals(identity, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(identity.GetMembers("Item").Single(), IPropertySymbol).Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(identity.GetMembers("GetValue").Single(), IMethodSymbol).ReturnType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(identity.GetMembers("SetValue").Single(), IMethodSymbol).Parameters.Single().Type.NullableAnnotation)
            Dim items = DirectCast(DirectCast(identity.GetMembers("GetItems").Single(), IMethodSymbol).ReturnType, IArrayTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, items.ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, items.ElementType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, definitionField.Type.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub VisualBasic_IdentityAnnotationSurvivesTopLevelChange()
            Dim compilation = CompileVisualBasic()
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim annotated = identity.WithNullableAnnotation(NullableAnnotation.Annotated)
            Dim cleared = annotated.WithNullableAnnotation(NullableAnnotation.None)
            Assert.Equal(NullableAnnotation.Annotated, annotated.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(annotated, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.None, cleared.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(cleared, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(cleared, INamedTypeSymbol).GetMembers("Value").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub VisualBasic_RepeatedIdentityConstructionsStayIsolated()
            Dim compilation = CompileVisualBasic()
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim parameter = box.TypeParameters.Single()
            Dim annotated = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim annotatedField = DirectCast(annotated.GetMembers("Value").Single(), IFieldSymbol)
            Dim plain = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.NotAnnotated))
            Assert.Equal(NullableAnnotation.Annotated, annotated.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, annotatedField.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, plain.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(plain.GetMembers("Value").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())

            Dim map = New Dictionary(Of ISymbol, String)(SymbolEqualityComparer.Default)
            map(box) = "definition"
            Assert.Equal("definition", map(annotated))
            Assert.Equal("definition", map(plain))
            Dim distinct = New HashSet(Of ISymbol)(SymbolEqualityComparer.IncludeNullability) From {box, annotated, plain}
            Assert.Equal(3, distinct.Count)
        End Sub

        <Fact>
        Public Sub VisualBasic_MethodIdentityInsideAnnotatedContainer()
            Dim compilation = CompileVisualBasic()
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim annotatedBox = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim echo = DirectCast(annotatedBox.GetMembers("EchoItem").Single(), IMethodSymbol)
            Dim constructed = echo.Construct(ImmutableArray.Create(Of ITypeSymbol)(echo.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.NotAnnotated))
            Assert.Equal(NullableAnnotation.NotAnnotated, constructed.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, constructed.ReturnType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, constructed.Parameters.Single().Type.NullableAnnotation)
            Assert.True(annotatedBox.Equals(constructed.ContainingType, SymbolEqualityComparer.IncludeNullability))
        End Sub

        <Fact>
        Public Sub VisualBasic_IdentityConstructionReentersAsUnderlyingType()
            Dim compilation = CompileVisualBasic()
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim conversion = compilation.ClassifyConversion(identity, box)
            Assert.Equal(ConversionKind.Identity, conversion.Kind)
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArgumentNullableAnnotations.Single())
            Dim foreign = CSharpType("Box`1")
            Assert.Throws(Of ArgumentException)(Function() compilation.ClassifyConversion(foreign, box))
        End Sub

        <Fact>
        Public Sub VisualBasic_ConcurrentIdentityConstructionDoesNotMutateDefinition()
            Dim compilation = CompileVisualBasic()
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim parameter = box.TypeParameters.Single()
            Dim mismatches = 0
            Parallel.For(0, 8, Sub(worker)
                                   For i = 1 To 40
                                       Dim annotation = If(i Mod 2 = 0, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
                                       Dim constructed = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(annotation))
                                       If constructed.TypeArgumentNullableAnnotations.Single() <> annotation OrElse
                                          DirectCast(constructed.GetMembers("Value").Single(), IFieldSymbol).NullableAnnotation <> annotation OrElse
                                          box.TypeArgumentNullableAnnotations.Single() <> NullableAnnotation.None Then
                                           Interlocked.Increment(mismatches)
                                       End If
                                   Next
                               End Sub)
            Assert.Equal(0, mismatches)
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub VisualBasic_IdentityConstructionCostIsBounded()
            Dim compilation = CompileVisualBasic()
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim parameter = box.TypeParameters.Single()
            Dim [string] = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Const iterations = 300
            Dim identity = TimeConstruction(iterations, Function() box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated)))
            Dim nonIdentity = TimeConstruction(iterations, Function() box.Construct(ImmutableArray.Create(Of ITypeSymbol)([string]), ImmutableArray.Create(NullableAnnotation.Annotated)))
            Dim unchanged = TimeConstruction(iterations, Function() box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.None)))
            Dim identityBytes = AllocatedBytes(iterations, Function() box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated)))
            Dim evidence = $"iterations={iterations}; identityMs={identity.TotalMilliseconds:F3}; nonIdentityMs={nonIdentity.TotalMilliseconds:F3}; unchangedMs={unchanged.TotalMilliseconds:F3}; identityAllocatedBytes={identityBytes}; measurement=GC.GetAllocatedBytesForCurrentThread; runtime={Global.System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}"
            Directory.CreateDirectory("D:\Development\VBNext\Evidence\LanguageCompletion")
            File.WriteAllText("D:\Development\VBNext\Evidence\LanguageCompletion\LC-1G.4b.1-performance.txt", evidence)
            Assert.True(identity.TotalSeconds < 30)
            Assert.True(nonIdentity.TotalSeconds < 30)
            Assert.True(unchanged.TotalSeconds < 30)
        End Sub

        <Fact>
        Public Sub VisualBasic_MethodIdentityRetainsReturnAndParameter()
            Dim compilation = CompileVisualBasic()
            Dim echo = DirectCast(compilation.GetTypeByMetadataName("Factory").GetMembers("Echo").Single(), IMethodSymbol)
            Dim echoIdentity = echo.Construct(ImmutableArray.Create(Of ITypeSymbol)(echo.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.NotSame(echo, echoIdentity)
            Assert.Equal(NullableAnnotation.None, echo.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.TypeArguments.Single().NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.ReturnType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.Parameters.Single().NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, echoIdentity.Parameters.Single().Type.NullableAnnotation)
            Assert.False(echo.Equals(echoIdentity, SymbolEqualityComparer.Default))
            Assert.Equal(NullableAnnotation.None, echo.ReturnNullableAnnotation)
        End Sub

        <Fact>
        Public Sub VisualBasic_ContainingIdentityAnnotationsStayDistinct()
            Dim compilation = CompileVisualBasic()
            Dim outer = DirectCast(compilation.GetTypeByMetadataName("Outer`1"), INamedTypeSymbol)
            Dim outerAnnotated = outer.Construct(ImmutableArray.Create(Of ITypeSymbol)(outer.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim inner = outerAnnotated.GetTypeMembers("Inner").Single()
            Dim innerIdentity = inner.Construct(ImmutableArray.Create(Of ITypeSymbol)(inner.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.NotAnnotated))
            Assert.Equal(NullableAnnotation.Annotated, innerIdentity.ContainingType.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, innerIdentity.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(innerIdentity.GetMembers("OuterValue").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(innerIdentity.GetMembers("InnerValue").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, outer.TypeArgumentNullableAnnotations.Single())
        End Sub

        Private Shared Function CSharpType(metadataName As String) As INamedTypeSymbol
            Dim compilation = CSharpCompilation.Create(
                "identity",
                {CSharpSyntaxTree.ParseText(CSharpSource)},
                {MetadataReference.CreateFromFile(GetType(Object).Assembly.Location)},
                New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            Return compilation.GetTypeByMetadataName(metadataName)
        End Function

        Private Shared Function AllocatedBytes(iterations As Integer, construct As Func(Of ISymbol)) As Long
#If NET472 Then
            construct()
            Return -1
#Else
            construct()
            Dim start = GC.GetAllocatedBytesForCurrentThread()
            For i = 1 To iterations
                construct()
            Next

            Return GC.GetAllocatedBytesForCurrentThread() - start
#End If
        End Function

        Private Shared Function TimeConstruction(iterations As Integer, construct As Func(Of ISymbol)) As TimeSpan
            construct()
            Dim watch = Stopwatch.StartNew()
            For i = 1 To iterations
                construct()
            Next

            watch.Stop()
            Return watch.Elapsed
        End Function

        Private Function CompileVisualBasic() As VisualBasicCompilation
            Return CreateCompilation(VisualBasicSource, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function

        Private Const CSharpSource As String = "
#nullable enable
public class Box<T>
{
    public T Value;
    public T Item { get; set; }
    public T GetValue() => Value;
    public void SetValue(T value) { }
    public T[] GetItems() => null;
}
public class Outer<TOuter>
{
    public class Inner<TInner>
    {
        public TOuter OuterValue;
        public TInner InnerValue;
    }
}
public class Factory
{
    public T Echo<T>(T value) => value;
}
"

        Private Const VisualBasicSource As String = "
Public Class Box(Of T)
    Public Value As T
    Public Property Item As T
    Public Function GetValue() As T
        Return Value
    End Function
    Public Sub SetValue(value As T)
    End Sub
    Public Function GetItems() As T()
        Return Nothing
    End Function
    Public Function EchoItem(Of U)(value As U) As T
        Return Value
    End Function
End Class
Public Class Outer(Of TOuter)
    Public Class Inner(Of TInner)
        Public OuterValue As TOuter
        Public InnerValue As TInner
    End Class
End Class
Public Class Factory
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Class
"
    End Class
End Namespace
