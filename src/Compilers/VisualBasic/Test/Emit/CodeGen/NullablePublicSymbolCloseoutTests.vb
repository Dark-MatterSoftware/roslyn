' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis.CSharp
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols.Retargeting
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class NullablePublicSymbolCloseoutTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)

        <Fact>
        Public Sub IdentityEventPreservesDelegateArgument()
            Dim compilation = CompileVisualBasic("
Imports System
Public Class Box(Of T)
    Public Event Changed As Action(Of T)
End Class
")
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim [event] = DirectCast(identity.GetMembers("Changed").Single(), IEventSymbol)
            Dim definitionEvent = DirectCast(box.GetMembers("Changed").Single(), IEventSymbol)
            Assert.Equal(NullableAnnotation.None, [event].NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast([event].Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.None, definitionEvent.Type.NullableAnnotation)
            Assert.True([event].ContainingType.Equals(identity, SymbolEqualityComparer.IncludeNullability))
            Assert.NotNull([event].AddMethod)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast([event].AddMethod.Parameters.Single().Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub AnonymousMemberAnnotationIsPreserved()
            Dim compilation = CompileVisualBasic("Public Class Marker
End Class")
            Dim text = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim annotated = text.WithNullableAnnotation(NullableAnnotation.Annotated)
            Dim created = compilation.CreateAnonymousTypeSymbol(
                ImmutableArray.Create(annotated),
                ImmutableArray.Create("Name"),
                ImmutableArray.Create(True),
                memberNullableAnnotations:=ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim member = DirectCast(created.GetMembers("Name").Single(), IPropertySymbol)
            Assert.Equal(NullableAnnotation.Annotated, member.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, member.Type.NullableAnnotation)
            Assert.True(created.Equals(compilation.CreateAnonymousTypeSymbol(ImmutableArray.Create(text), ImmutableArray.Create("Name"), ImmutableArray.Create(True)), SymbolEqualityComparer.Default))
            Assert.False(member.Type.Equals(text, SymbolEqualityComparer.IncludeNullability))
        End Sub

        <Fact>
        Public Sub ReducedExtensionMapsRemainingPublicArgument()
            Dim compilation = CompileVisualBasic("
Imports System.Runtime.CompilerServices
Public Class Pair(Of T)
    Public Item As T
End Class
Public Module Extensions
    <Extension>
    Public Function Take(Of T, U)(value As Pair(Of T), other As U) As U
        Return other
    End Function
End Module
")
            Dim pair = DirectCast(compilation.GetTypeByMetadataName("Pair`1"), INamedTypeSymbol)
            Dim text = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim receiver = pair.Construct(text.WithNullableAnnotation(NullableAnnotation.Annotated))
            Dim definition = DirectCast(compilation.GetTypeByMetadataName("Extensions").GetMembers("Take").Single(), IMethodSymbol)
            Dim reduced = definition.ReduceExtensionMethod(receiver)
            Assert.NotNull(reduced)
            Assert.Equal(1, reduced.Arity)
            Dim constructed = reduced.Construct(ImmutableArray.Create(Of ITypeSymbol)(reduced.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.Equal(1, constructed.TypeArgumentNullableAnnotations.Length)
            Assert.Equal(NullableAnnotation.Annotated, constructed.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, constructed.ReturnType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, definition.TypeArgumentNullableAnnotations(0))
        End Sub

        <Fact>
        Public Sub NestedIdentityProjectionSurvivesConstructionArrayAndTuple()
            Dim compilation = CompileVisualBasic("
Public Class Box(Of T)
    Public Value As T
End Class
Public Class Wrapper(Of T)
    Public Item As T
End Class
")
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim wrapper = DirectCast(compilation.GetTypeByMetadataName("Wrapper`1"), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArgumentNullableAnnotations.Single())
            Dim constructed = wrapper.Construct(identity)
            Dim argument = constructed.TypeArguments.Single()
            Assert.True(argument.Equals(identity, SymbolEqualityComparer.IncludeNullability), argument.GetType().FullName & " ann=" & argument.NullableAnnotation.ToString() & " args=" & String.Join(",", DirectCast(argument, INamedTypeSymbol).TypeArgumentNullableAnnotations))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(constructed.GetMembers("Item").Single(), IFieldSymbol).Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())

            Dim publicCompilation = DirectCast(compilation, Compilation)
            Dim array = publicCompilation.CreateArrayTypeSymbol(identity, 1)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(array.ElementType, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Dim text = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim tuple = publicCompilation.CreateTupleTypeSymbol(ImmutableArray.Create(Of ITypeSymbol)(identity, text))
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(tuple.TupleElements(0).Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub TupleLengthsPreserveLogicalElementAnnotations()
            Dim compilation = CompileVisualBasic("Public Class Marker : End Class")
            Dim text = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim number = DirectCast(compilation.GetSpecialType(SpecialType.System_Int32), ITypeSymbol)
            Dim nullableNumber = DirectCast(compilation.GetSpecialType(SpecialType.System_Nullable_T), INamedTypeSymbol).Construct(number)
            For Each length In {2, 7, 8, 9, 15}
                Dim elements(length - 1) As ITypeSymbol
                Dim annotations(length - 1) As NullableAnnotation
                For i = 0 To length - 1
                    If i = 0 Then
                        elements(i) = text.WithNullableAnnotation(NullableAnnotation.Annotated)
                        annotations(i) = NullableAnnotation.Annotated
                    ElseIf i Mod 3 = 1 Then
                        elements(i) = number
                        annotations(i) = NullableAnnotation.None
                    ElseIf i Mod 3 = 2 Then
                        elements(i) = nullableNumber
                        annotations(i) = NullableAnnotation.None
                    Else
                        elements(i) = text
                        annotations(i) = NullableAnnotation.NotAnnotated
                    End If
                Next

                Dim tuple = DirectCast(compilation, Compilation).CreateTupleTypeSymbol(elements.AsImmutableOrNull(), elementNullableAnnotations:=annotations.AsImmutableOrNull())
                Assert.Equal(length, tuple.TupleElements.Length)
                For i = 0 To length - 1
                    Assert.Equal(annotations(i), tuple.TupleElements(i).NullableAnnotation)
                    Assert.Equal(annotations(i), tuple.TupleElements(i).Type.NullableAnnotation)
                Next
            Next
        End Sub

        <Fact>
        Public Sub ValueTypeAnnotationFollowsTheSharedContract()
            Dim compilation = CompileVisualBasic("Public Structure Point : Public X As Integer : End Structure
Public Enum Color : Red : End Enum
Public Class Box(Of T) : Public Value As T : End Class")
            Dim number = DirectCast(compilation.GetSpecialType(SpecialType.System_Int32), ITypeSymbol)
            Dim point = DirectCast(compilation.GetTypeByMetadataName("Point"), ITypeSymbol)
            Dim color = DirectCast(compilation.GetTypeByMetadataName("Color"), ITypeSymbol)
            Dim csharp = CSharpCompilation.Create("c", references:={MetadataReference.CreateFromFile(GetType(Integer).Assembly.Location)})
            Dim csharpNumber = csharp.GetSpecialType(SpecialType.System_Int32)
            Assert.Equal(csharpNumber.WithNullableAnnotation(NullableAnnotation.Annotated).NullableAnnotation, number.WithNullableAnnotation(NullableAnnotation.Annotated).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, point.WithNullableAnnotation(NullableAnnotation.Annotated).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, color.WithNullableAnnotation(NullableAnnotation.Annotated).NullableAnnotation)
            Assert.True(point.WithNullableAnnotation(NullableAnnotation.Annotated).Equals(point, SymbolEqualityComparer.IncludeNullability))
            Assert.True(number.WithNullableAnnotation(NullableAnnotation.Annotated).Equals(number, SymbolEqualityComparer.IncludeNullability))
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim constructed = box.Construct(number)
            Assert.Equal(NullableAnnotation.None, DirectCast(constructed.GetMembers("Value").Single(), IFieldSymbol).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub PropertyAccessorsStayWithTheAnnotatedProperty()
            Dim compilation = CompileVisualBasic("
Public Class Box(Of T)
    Public ReadOnly Property Item As T
        Get
            Return Nothing
        End Get
    End Property
    Public WriteOnly Property Slot As T
        Set(value As T)
        End Set
    End Property
    Default Public Property At(index As Integer) As T
        Get
            Return Nothing
        End Get
        Set(value As T)
        End Set
    End Property
End Class")
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim item = DirectCast(identity.GetMembers("Item").Single(), IPropertySymbol)
            Dim slot = DirectCast(identity.GetMembers("Slot").Single(), IPropertySymbol)
            Dim at = DirectCast(identity.GetMembers("At").Single(), IPropertySymbol)
            Assert.Equal(NullableAnnotation.Annotated, item.Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, item.GetMethod.ReturnType.NullableAnnotation)
            Assert.True(item.GetMethod.AssociatedSymbol.Equals(item, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(NullableAnnotation.Annotated, slot.SetMethod.Parameters.Single().Type.NullableAnnotation)
            Assert.True(slot.SetMethod.AssociatedSymbol.Equals(slot, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(NullableAnnotation.Annotated, at.GetMethod.ReturnType.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, at.SetMethod.Parameters.Single(Function(p) p.Ordinal = 1).Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, at.Parameters.Single().NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ParameterShapesKeepDeclarationAnnotations()
            Dim compilation = CompileVisualBasic("
Public Class Box(Of T)
    Public Sub Take(ByVal value As T, ByRef other As T, Optional ready As T = Nothing, ParamArray items As T())
    End Sub
End Class")
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim parameters = DirectCast(identity.GetMembers("Take").Single(), IMethodSymbol).Parameters
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.None, parameters(0).RefKind)
            Assert.Equal(Microsoft.CodeAnalysis.RefKind.Ref, parameters(1).RefKind)
            Assert.True(parameters(2).IsOptional)
            Assert.True(parameters(3).IsParams)
            Assert.Equal(NullableAnnotation.Annotated, parameters(0).Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, parameters(1).Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, parameters(2).Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(parameters(3).Type, IArrayTypeSymbol).ElementType.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ReceiversFollowInstanceSharedAndReducedContracts()
            Dim compilation = CompileVisualBasic("
Imports System.Runtime.CompilerServices
Public Class Box
    Public Sub Instance()
    End Sub
    Public Shared Sub SharedMethod()
    End Sub
End Class
Public Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module")
            Dim box = compilation.GetTypeByMetadataName("Box")
            Dim instance = DirectCast(box.GetMembers("Instance").Single(), IMethodSymbol)
            Dim sharedMethod = DirectCast(box.GetMembers("SharedMethod").Single(), IMethodSymbol)
            Assert.Equal(NullableAnnotation.NotAnnotated, instance.ReceiverNullableAnnotation)
            Assert.True(instance.ReceiverType.Equals(box, SymbolEqualityComparer.Default))
            Assert.Equal(NullableAnnotation.None, sharedMethod.ReceiverNullableAnnotation)
            Assert.True(sharedMethod.ReceiverType.Equals(box, SymbolEqualityComparer.Default))
            Dim text = DirectCast(compilation.GetSpecialType(SpecialType.System_String), ITypeSymbol)
            Dim definition = DirectCast(compilation.GetTypeByMetadataName("Extensions").GetMembers("Echo").Single(), IMethodSymbol)
            Dim reduced = definition.ReduceExtensionMethod(text.WithNullableAnnotation(NullableAnnotation.Annotated))
            Assert.Equal(0, reduced.TypeArgumentNullableAnnotations.Length)
            Assert.Equal(1, DirectCast(reduced, MethodSymbol).GetTypeArgumentNullableAnnotations().Length)
            Assert.Equal(NullableAnnotation.Annotated, reduced.ReceiverNullableAnnotation)
        End Sub

        <Fact>
        Public Sub ImportedConstraintsExposePublicAnnotations()
            Dim csharp = CSharpCompilation.Create("lib",
                {CSharpSyntaxTree.ParseText("
#nullable enable
public interface IFace<T> { }
public class ClassConstraint<T> where T : class { }
public class NullableClassConstraint<T> where T : class? { }
public class InterfaceConstraint<T> where T : IFace<string?> { }
public class Self<T> where T : Self<T> { }
", options:=CSharpParseOptions.Default.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview))},
                {MetadataReference.CreateFromFile(GetType(Integer).Assembly.Location)},
                New CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            Dim image = csharp.EmitToArray()
            Dim compilation = CreateCompilation("", references:={MetadataReference.CreateFromImage(image)}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim nullableClass = DirectCast(compilation.GetTypeByMetadataName("NullableClassConstraint`1"), INamedTypeSymbol)
            Dim plainClass = DirectCast(compilation.GetTypeByMetadataName("ClassConstraint`1"), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, nullableClass.TypeParameters.Single().ReferenceTypeConstraintNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, plainClass.TypeParameters.Single().ReferenceTypeConstraintNullableAnnotation)
            Dim iface = DirectCast(compilation.GetTypeByMetadataName("InterfaceConstraint`1").TypeParameters.Single().ConstraintTypes.Single(), INamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, iface.TypeArgumentNullableAnnotations.Single())
            Dim self = DirectCast(compilation.GetTypeByMetadataName("Self`1"), INamedTypeSymbol).TypeParameters.Single()
            Assert.True(self.Equals(self, SymbolEqualityComparer.IncludeNullability), self.GetType().ToString())
            Assert.False(self.Equals(nullableClass.TypeParameters.Single(), SymbolEqualityComparer.Default))
        End Sub

        <Fact>
        Public Sub RetargetedSymbolsExposePublicAnnotations()
            Dim compilation = CompileVisualBasic("
Public Class Pair(Of T)
    Public Item As T
End Class
Public Class Box
    Public Name As String?
    Public Property Title As String?
    Public Event Changed As System.Action(Of String?)
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Class")
            Dim sourceAssembly = DirectCast(compilation.SourceModule.ContainingAssembly, SourceAssemblySymbol)
            Dim retargeting = New RetargetingAssemblySymbol(sourceAssembly, isLinked:=False)
            retargeting.SetCorLibrary(sourceAssembly.CorLibrary)
            Dim box = DirectCast(retargeting.GlobalNamespace.GetTypeMembers("Box").Single(), INamedTypeSymbol)
            Dim name = DirectCast(box.GetMembers("Name").Single(), IFieldSymbol)
            Dim title = DirectCast(box.GetMembers("Title").Single(), IPropertySymbol)
            Dim changed = DirectCast(box.GetMembers("Changed").Single(), IEventSymbol)
            Dim echo = DirectCast(box.GetMembers("Echo").Single(), IMethodSymbol)
            Assert.IsAssignableFrom(Of RetargetingNamedTypeSymbol)(box)
            Assert.Equal(NullableAnnotation.Annotated, name.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, name.Type.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, title.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, changed.NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(changed.Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.None, echo.TypeArgumentNullableAnnotations.Single())
            Dim pair = DirectCast(retargeting.GlobalNamespace.GetTypeMembers("Pair").Single(), INamedTypeSymbol)
            Dim identity = pair.Construct(ImmutableArray.Create(Of ITypeSymbol)(pair.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.Equal(NullableAnnotation.Annotated, identity.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.None, pair.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(identity.GetMembers("Item").Single(), IFieldSymbol).Type.NullableAnnotation)
        End Sub

        <Fact>
        Public Sub CrossLanguagePublicAnnotationsRoundTrip()
            Dim compilation = CompileVisualBasic("
Public Class Box(Of T)
    Public Value As T
    Public Event Changed As System.Action(Of T)
End Class")
            Dim image = compilation.EmitToArray()
            Dim reference = MetadataReference.CreateFromImage(image)
            Dim corlib = MetadataReference.CreateFromFile(GetType(Integer).Assembly.Location)
            Dim visualBasic = CreateCompilation("", references:={reference, corlib}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim csharp = CSharpCompilation.Create("consumer", references:={reference, corlib})
            Dim vbBox = DirectCast(visualBasic.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim csBox = DirectCast(csharp.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim vbIdentity = vbBox.Construct(ImmutableArray.Create(Of ITypeSymbol)(vbBox.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim csIdentity = csBox.Construct(ImmutableArray.Create(Of ITypeSymbol)(csBox.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.Equal(csIdentity.TypeArgumentNullableAnnotations.Single(), vbIdentity.TypeArgumentNullableAnnotations.Single())
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(vbIdentity.GetMembers("Value").Single(), IFieldSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, DirectCast(vbIdentity.GetMembers("Changed").Single(), IEventSymbol).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(DirectCast(vbIdentity.GetMembers("Changed").Single(), IEventSymbol).Type, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub EqualityHashAndIsolationStayStable()
            Dim compilation = CompileVisualBasic("Public Class Box(Of T) : Public Value As T : End Class")
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim parameter = box.TypeParameters.Single()
            Dim annotated = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            Dim plain = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.NotAnnotated))
            Dim field = DirectCast(annotated.GetMembers("Value").Single(), IFieldSymbol)
            Dim another = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.Equal(NullableAnnotation.Annotated, field.NullableAnnotation)
            Assert.Equal(NullableAnnotation.None, box.TypeArgumentNullableAnnotations.Single())
            Assert.True(box.Equals(annotated, SymbolEqualityComparer.Default))
            Assert.True(annotated.Equals(box, SymbolEqualityComparer.Default))
            Assert.False(box.Equals(annotated, SymbolEqualityComparer.IncludeNullability))
            Assert.False(annotated.Equals(plain, SymbolEqualityComparer.IncludeNullability))
            Assert.Equal(SymbolEqualityComparer.Default.GetHashCode(box), SymbolEqualityComparer.Default.GetHashCode(annotated))
            Dim defaults As New HashSet(Of ISymbol)(SymbolEqualityComparer.Default) From {box, annotated, plain}
            Assert.Single(defaults)
            Dim sensitive As New HashSet(Of ISymbol)(SymbolEqualityComparer.IncludeNullability) From {box, annotated, plain}
            Assert.Equal(3, sensitive.Count)
            Assert.Contains(annotated, sensitive)
        End Sub

        <Fact>
        Public Sub CompletedProjectionWorkIsConcurrentAndMeasurable()
            Dim compilation = CompileVisualBasic("Public Class Box(Of T) : Public Value As T : Public Function Echo(Of U)(value As U) As T : Return Nothing : End Function : End Class")
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim parameter = box.TypeParameters.Single()
            Dim mismatches = 0
            Parallel.For(0, 4, Sub(worker)
                                   For i = 1 To 20
                                       Dim annotation = If(i Mod 2 = 0, NullableAnnotation.Annotated, NullableAnnotation.NotAnnotated)
                                       Dim constructed = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(annotation))
                                       Dim method = DirectCast(constructed.GetMembers("Echo").Single(), IMethodSymbol)
                                       Dim methodIdentity = method.Construct(ImmutableArray.Create(Of ITypeSymbol)(method.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
                                       Dim array = DirectCast(compilation, Compilation).CreateArrayTypeSymbol(constructed)
                                       If constructed.TypeArgumentNullableAnnotations.Single() <> annotation OrElse
                                          DirectCast(constructed.GetMembers("Value").Single(), IFieldSymbol).NullableAnnotation <> annotation OrElse
                                          methodIdentity.TypeArgumentNullableAnnotations.Single() <> NullableAnnotation.Annotated OrElse
                                          DirectCast(array.ElementType, INamedTypeSymbol).TypeArgumentNullableAnnotations.Single() <> annotation OrElse
                                          box.TypeArgumentNullableAnnotations.Single() <> NullableAnnotation.None Then
                                           Interlocked.Increment(mismatches)
                                       End If
                                   Next
                               End Sub)
            Assert.Equal(0, mismatches)
            Const iterations = 200
            Dim watch = Stopwatch.StartNew()
            For i = 1 To iterations
                box.GetMembers()
                box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            Next
            watch.Stop()
#If Not NET472 Then
            Dim before = GC.GetAllocatedBytesForCurrentThread()
            For i = 1 To iterations
                box.GetMembers()
                box.Construct(ImmutableArray.Create(Of ITypeSymbol)(parameter), ImmutableArray.Create(NullableAnnotation.Annotated))
            Next
            Dim allocated = GC.GetAllocatedBytesForCurrentThread() - before
            Console.WriteLine("LC-1G.4b.2 closeout iterations=" & iterations & " elapsedMs=" & watch.Elapsed.TotalMilliseconds.ToString("0.000") & " allocatedBytes=" & allocated)
#Else
            Console.WriteLine("LC-1G.4b.2 closeout iterations=" & iterations & " elapsedMs=" & watch.Elapsed.TotalMilliseconds.ToString("0.000"))
#End If
            Assert.True(watch.Elapsed.TotalMilliseconds >= 0)
        End Sub

        <Fact>
        Public Sub LegacyLanguageRejectsSyntaxButPublicConstructionRemainsAvailable()
            Const source = "Public Class Box(Of T) : Public Value As T : End Class"
            For Each version In {LanguageVersion.VisualBasic17_13, LanguageVersion.Default, LanguageVersion.Latest}
                Dim legacy = CreateCompilation("Public Class Program : Public Value As String? : End Class", options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(version))
                Assert.Contains(legacy.GetDiagnostics(), Function(d) d.Severity = DiagnosticSeverity.Error)
                Dim compilation = CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(version))
                Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
                Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
                Assert.Equal(NullableAnnotation.Annotated, identity.TypeArgumentNullableAnnotations.Single())
            Next
        End Sub

        <Fact>
        Public Sub AnnotationFreeTwinsEmitTheSameMethodBody()
            Const source = "
Public Class Box(Of T)
    Public Value As T
    Public Property Item As T
    Public Event Changed As System.Action(Of T)
    Public Function Echo(Of U)(value As U) As U
        Return value
    End Function
    Public Function Read() As T
        Return Value
    End Function
End Class
Public Module Program
    Public Function Go(box As Box(Of String)) As String
        Return box.Echo(Of String)(box.Item)
    End Function
End Module"
            Dim legacy = CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)))
            Dim modern = CompileAndVerify(CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext))
            Assert.Equal(legacy.VisualizeIL("Program.Go"), modern.VisualizeIL("Program.Go"))
            Assert.Equal(legacy.VisualizeIL("Box(Of T).Echo(Of U)(U)"), modern.VisualizeIL("Box(Of T).Echo(Of U)(U)"))
            Assert.Equal(legacy.VisualizeIL("Box(Of T).Read()"), modern.VisualizeIL("Box(Of T).Read()"))
            Assert.Equal(legacy.VisualizeIL("Box(Of T).get_Item()"), modern.VisualizeIL("Box(Of T).get_Item()"))
            Assert.Equal(legacy.VisualizeIL("Box(Of T).add_Changed(System.Action(Of T))"), modern.VisualizeIL("Box(Of T).add_Changed(System.Action(Of T))"))
        End Sub

        <Fact>
        Public Sub SpecializedInterfacesStayAvailable()
            Dim compilation = CompileVisualBasic("Public Class Box(Of T) : End Class")
            Dim box = DirectCast(compilation.GetTypeByMetadataName("Box`1"), INamedTypeSymbol)
            Dim identity = box.Construct(ImmutableArray.Create(Of ITypeSymbol)(box.TypeParameters.Single()), ImmutableArray.Create(NullableAnnotation.Annotated))
            Assert.IsAssignableFrom(Of INamedTypeSymbol)(identity)
            Assert.IsAssignableFrom(Of ITypeParameterSymbol)(identity.TypeArguments.Single())
            Dim unbound = box.ConstructUnboundGenericType()
            Assert.True(unbound.IsUnboundGenericType)
            Dim array = DirectCast(compilation, Compilation).CreateArrayTypeSymbol(identity)
            Assert.IsAssignableFrom(Of IArrayTypeSymbol)(array)
            Dim missing = compilation.CreateErrorTypeSymbol(box, "Missing", 0)
            Assert.Equal(TypeKind.Error, missing.TypeKind)
        End Sub

        Private Function CompileVisualBasic(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function
    End Class
End Namespace
