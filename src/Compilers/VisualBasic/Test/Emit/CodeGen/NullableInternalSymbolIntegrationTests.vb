' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols.Retargeting
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGen

    ''' <summary>
    ''' LC-1G.1 internal annotation preservation. Public method type-argument
    ''' arrays stay None; friend getters and existing return and parameter
    ''' getters are the internal surface these tests read.
    ''' </summary>
    Public Class NullableInternalSymbolIntegrationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ReducedReceiver_StoresTheFixedAnnotation()
            Dim compilation = Compile("
Imports System.Runtime.CompilerServices
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Sub Test(value As String?, ready As String)
        Dim reduced = value.Echo()
        Dim reducedReady = ready.Echo()
        Dim ordinary = Echo(value)
    End Sub
End Module
")
            Dim reduced = BoundMethod(compilation, "value.Echo()")
            Dim ready = BoundMethod(compilation, "ready.Echo()")
            Dim ordinary = BoundMethod(compilation, "Echo(value)")

            Assert.True(reduced.IsReducedExtensionMethod)
            Assert.Equal(0, reduced.Arity)
            Assert.Equal(NullableAnnotation.Annotated, reduced.GetReturnNullableAnnotation())
            AssertOriginal(reduced, NullableAnnotation.Annotated)

            Assert.Equal(NullableAnnotation.NotAnnotated, ready.GetReturnNullableAnnotation())
            AssertOriginal(ready, NullableAnnotation.NotAnnotated)

            Assert.Equal(NullableAnnotation.Annotated, ordinary.GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, ordinary.GetTypeArgumentNullableAnnotations().Single())
            AssertPublicTypeArgumentsAreNone(ordinary)
        End Sub

        <Fact>
        Public Sub ReducedReceiver_LaterCallDoesNotReplaceAnEarlierAnnotation()
            Dim compilation = Compile("
Imports System.Runtime.CompilerServices
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Sub AnnotatedFirst(value As String?, ready As String)
        Dim first = value.Echo()
        Dim second = ready.Echo()
    End Sub
    Sub NonNullFirst(value As String?, ready As String)
        Dim first = ready.Echo()
        Dim second = value.Echo()
    End Sub
End Module
")
            Dim annotatedFirst = BoundMethod(compilation, "value.Echo()")
            Dim nonNullSecond = BoundMethod(compilation, "ready.Echo()")
            Assert.Equal(NullableAnnotation.Annotated, annotatedFirst.GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.NotAnnotated, nonNullSecond.GetReturnNullableAnnotation())

            Dim nonNullFirst = BoundMethodIn(compilation, "NonNullFirst", "ready.Echo()")
            Dim annotatedSecond = BoundMethodIn(compilation, "NonNullFirst", "value.Echo()")
            Assert.Equal(NullableAnnotation.NotAnnotated, nonNullFirst.GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, annotatedSecond.GetReturnNullableAnnotation())
        End Sub

        <Fact>
        Public Sub ReducedReceiver_CoversNestedFreeAndObliviousArguments()
            Dim compilation = Compile("
Imports System.Runtime.CompilerServices
Public Class Pair(Of T)
    Public Item As T
End Class
Module Extensions
    <Extension>
    Public Function Take(Of T, U)(value As Pair(Of T), other As U) As Pair(Of U)
        Return Nothing
    End Function
    <Extension>
    Public Function Element(Of T)(value As T()) As T
        Return Nothing
    End Function
    <Extension>
    Public Function Wrap(Of T)(value As T) As Pair(Of Pair(Of T))
        Return Nothing
    End Function
End Module
Module Program
    Sub Test(value As Pair(Of String?), other As String?, items As String?(), text As String?)
        Dim nested = value.Take(other)
        Dim element = items.Element()
        Dim wrapped = text.Wrap()
    End Sub
End Module
")
            Dim nested = BoundMethod(compilation, "value.Take(other)")
            Dim reduced = nested.OriginalDefinition
            Assert.Equal(NullableAnnotation.NotAnnotated, nested.GetReturnNullableAnnotation())
            Dim returnType = DirectCast(nested.ReturnType, NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, returnType.GetTypeArgumentNullableAnnotations().Single())
            AssertOriginal(reduced, NullableAnnotation.Annotated, NullableAnnotation.Annotated)
            Assert.Equal(NullableAnnotation.Annotated, reduced.Parameters(0).GetDeclaredNullableAnnotation())

            Dim element = BoundMethod(compilation, "items.Element()")
            Assert.Equal(NullableAnnotation.Annotated, element.GetReturnNullableAnnotation())
            AssertOriginal(element, NullableAnnotation.Annotated)

            Dim wrapped = BoundMethod(compilation, "text.Wrap()")
            Dim outer = DirectCast(wrapped.ReturnType, NamedTypeSymbol)
            Dim inner = DirectCast(outer.TypeArgumentsNoUseSiteDiagnostics(0), NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, inner.GetTypeArgumentNullableAnnotations().Single())

            Dim oblivious = Compile("
Imports System.Runtime.CompilerServices
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Sub Test(value As String)
        Dim reduced = value.Echo()
    End Sub
End Module
", nullable:=False)
            Dim obliviousCall = BoundMethod(oblivious, "value.Echo()")
            Assert.Equal(NullableAnnotation.None, obliviousCall.GetReturnNullableAnnotation())
            AssertOriginal(obliviousCall, NullableAnnotation.None)
        End Sub

        <Fact>
        Public Sub ImportedExtension_PreservesTheReceiverAnnotation()
            Dim library = Compile("
Imports System.Runtime.CompilerServices
Public Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
")
            Dim reference = library.EmitToImageReference()
            Dim consumer = CreateCompilation("
Module Program
    Sub Test(value As String?, ready As String)
        Dim reduced = value.Echo()
        Dim readyCall = ready.Echo()
    End Sub
End Module
", references:={reference}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim reduced = BoundMethod(consumer, "value.Echo()")
            Dim ready = BoundMethod(consumer, "ready.Echo()")
            Assert.Equal(NullableAnnotation.Annotated, reduced.GetReturnNullableAnnotation())
            Assert.Equal(NullableAnnotation.NotAnnotated, ready.GetReturnNullableAnnotation())
            AssertOriginal(reduced, NullableAnnotation.Annotated)
        End Sub

        <Fact>
        Public Sub RetargetedSymbols_PreserveStoredAnnotations()
            Dim compilation = Compile("
Public Class Pair(Of T)
    Public Item As T
End Class
Public Class Box
    Public Name As String?
    Public Ready As String
    Public Items As String?()
    Public Values As Pair(Of String?)
    Public Property Title As String?
    Public Event Changed As Pair(Of String?)
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
    Public Sub Take(value As String?)
    End Sub
End Class
")
            Dim sourceAssembly = DirectCast(compilation.SourceModule.ContainingAssembly, SourceAssemblySymbol)
            Dim retargeting = New RetargetingAssemblySymbol(sourceAssembly, isLinked:=False)
            retargeting.SetCorLibrary(sourceAssembly.CorLibrary)
            Dim sourceBox = sourceAssembly.GlobalNamespace.GetTypeMember("Box")
            Dim retargetedBox = DirectCast(retargeting.GlobalNamespace.GetTypeMember("Box"), RetargetingNamedTypeSymbol)
            Assert.NotNull(retargetedBox)

            Assert.Equal(sourceBox.GetMember(Of FieldSymbol)("Name").GetNullableAnnotation(),
                         retargetedBox.GetMember(Of FieldSymbol)("Name").GetNullableAnnotation())
            Assert.Equal(NullableAnnotation.Annotated, retargetedBox.GetMember(Of FieldSymbol)("Name").GetNullableAnnotation())
            Assert.Equal(NullableAnnotation.NotAnnotated, retargetedBox.GetMember(Of FieldSymbol)("Ready").GetNullableAnnotation())

            Dim sourceItems = DirectCast(sourceBox.GetMember(Of FieldSymbol)("Items").Type, ArrayTypeSymbol)
            Dim retargetedItems = DirectCast(retargetedBox.GetMember(Of FieldSymbol)("Items").Type, ArrayTypeSymbol)
            Assert.Equal(sourceItems.ElementNullableAnnotation, retargetedItems.ElementNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, retargetedItems.ElementNullableAnnotation)

            Dim sourceValues = DirectCast(sourceBox.GetMember(Of FieldSymbol)("Values").Type, NamedTypeSymbol)
            Dim retargetedValues = DirectCast(retargetedBox.GetMember(Of FieldSymbol)("Values").Type, NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, retargetedValues.GetTypeArgumentNullableAnnotations().Single())

            Assert.Equal(NullableAnnotation.Annotated, retargetedBox.GetMember(Of PropertySymbol)("Title").GetNullableAnnotation())

            Dim sourceMethod = sourceBox.GetMember(Of MethodSymbol)("Echo")
            Dim retargetedMethod = retargetedBox.GetMember(Of MethodSymbol)("Echo")
            Assert.IsType(Of RetargetingMethodSymbol)(retargetedMethod)
            Assert.Equal(sourceMethod.GetReturnNullableAnnotation(), retargetedMethod.GetReturnNullableAnnotation())

            Dim sourceParameter = sourceBox.GetMember(Of MethodSymbol)("Take").Parameters(0)
            Dim retargetedParameter = retargetedBox.GetMember(Of MethodSymbol)("Take").Parameters(0)
            Assert.IsAssignableFrom(Of RetargetingParameterSymbol)(retargetedParameter)
            Assert.Equal(NullableAnnotation.Annotated, retargetedParameter.GetDeclaredNullableAnnotation())
            Assert.Equal(sourceParameter.GetDeclaredNullableAnnotation(), retargetedParameter.GetDeclaredNullableAnnotation())

            Dim sourceEvent = sourceBox.GetMember(Of EventSymbol)("Changed")
            Dim retargetedEvent = retargetedBox.GetMember(Of EventSymbol)("Changed")
            Assert.IsType(Of RetargetingEventSymbol)(retargetedEvent)
            Dim sourceEventType = DirectCast(sourceEvent.Type, NamedTypeSymbol)
            Dim eventType = DirectCast(retargetedEvent.Type, NamedTypeSymbol)
            ' The event has no annotation of its own. The constructed delegate or type argument does.
            Assert.Equal(NullableAnnotation.Annotated, sourceEventType.GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.Annotated, eventType.GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.None, DirectCast(retargetedEvent, IEventSymbol).NullableAnnotation)
        End Sub

        <Fact>
        Public Sub ConstructedSymbols_KeepDistinctAnnotations()
            Dim compilation = Compile("
Public Class Pair(Of T)
    Public Item As T
End Class
Module Program
    Function Annotated(value As Pair(Of String?)) As Pair(Of String?)
        Return value
    End Function
    Function NonNull(value As Pair(Of String)) As Pair(Of String)
        Return value
    End Function
    Function Both(value As Pair(Of String?), ready As Pair(Of String)) As Pair(Of String?)
        Return value
    End Function
End Module
")
            Dim annotated = DirectCast(SourceMethod(compilation, "Program", "Annotated").Parameters(0).Type, NamedTypeSymbol)
            Dim nonNull = DirectCast(SourceMethod(compilation, "Program", "NonNull").Parameters(0).Type, NamedTypeSymbol)
            Assert.Equal(NullableAnnotation.Annotated, annotated.GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, nonNull.GetTypeArgumentNullableAnnotations().Single())

            Dim both = SourceMethod(compilation, "Program", "Both")
            Assert.Equal(NullableAnnotation.Annotated, DirectCast(both.Parameters(0).Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
            Assert.Equal(NullableAnnotation.NotAnnotated, DirectCast(both.Parameters(1).Type, NamedTypeSymbol).GetTypeArgumentNullableAnnotations().Single())
        End Sub

        <Fact>
        Public Sub RepeatedConstruction_IsStableAcrossOrder()
            Dim results(1) As NullableAnnotation
            Parallel.For(0, 2, Sub(index As Integer)
                                    Dim source = If(index = 0,
                                        "Public Class Pair(Of T)" & vbLf & "Public Item As T" & vbLf & "End Class" & vbLf &
                                        "Module Program" & vbLf & "Function A(value As Pair(Of String?)) As Pair(Of String?)" & vbLf & "Return value" & vbLf & "End Function" & vbLf & "End Module",
                                        "Public Class Pair(Of T)" & vbLf & "Public Item As T" & vbLf & "End Class" & vbLf &
                                        "Module Program" & vbLf & "Function A(value As Pair(Of String)) As Pair(Of String)" & vbLf & "Return value" & vbLf & "End Function" & vbLf & "End Module")
                                    Dim compilation = Compile(source)
                                    Dim parameter = DirectCast(SourceMethod(compilation, "Program", "A").Parameters(0).Type, NamedTypeSymbol)
                                    results(index) = parameter.GetTypeArgumentNullableAnnotations().Single()
                                End Sub)
            Assert.Equal(NullableAnnotation.Annotated, results(0))
            Assert.Equal(NullableAnnotation.NotAnnotated, results(1))
        End Sub

        <Fact>
        Public Sub ExplicitReducedTypeArgument_RemainsBC36907()
            Dim compilation = Compile("
Imports System.Runtime.CompilerServices
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Sub Test(value As String?)
        Dim explicitType = value.Echo(Of String?)()
    End Sub
End Module
")
            Assert.Contains(compilation.GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.ERR_TypeOrMemberNotGeneric2)
        End Sub

        <Fact>
        Public Sub LegacyLanguage_DoesNotStoreVbNextAnnotations()
            Dim source = "
Imports System.Runtime.CompilerServices
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Public Function Go(value As String) As String
        Return value.Echo()
    End Function
    Public Function CallIdentity(value As String) As String
        Return Identity(value)
    End Function
    Public Function Identity(Of T)(value As T) As T
        Return value
    End Function
End Module
"
            Dim legacy = CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=Vb17)
            legacy.VerifyDiagnostics()
            Dim modern = Compile(source)
            Dim legacyIl = CompileAndVerify(legacy)
            Dim modernIl = CompileAndVerify(modern)
            Assert.Equal(legacyIl.VisualizeIL("Program.Go"), modernIl.VisualizeIL("Program.Go"))
            Assert.Equal(legacyIl.VisualizeIL("Extensions.Echo"), modernIl.VisualizeIL("Extensions.Echo"))
            Assert.Equal(legacyIl.VisualizeIL("Program.CallIdentity"), modernIl.VisualizeIL("Program.CallIdentity"))
            Assert.Equal(legacyIl.VisualizeIL("Program.Identity"), modernIl.VisualizeIL("Program.Identity"))
        End Sub

        Private Function Compile(source As String, Optional nullable As Boolean = True) As VisualBasicCompilation
            Dim options = TestOptions.ReleaseDll
            If nullable Then
                options = options.WithNullableContext(NullableContextOptions.Enable)
            End If

            Return CreateCompilation(source, options:=options, parseOptions:=VbNext)
        End Function

        Private Function SourceMethod(compilation As VisualBasicCompilation, typeName As String, methodName As String) As MethodSymbol
            Dim container = compilation.GlobalNamespace.GetModuleMembers(typeName).Single()
            Return container.GetMember(Of MethodSymbol)(methodName)
        End Function

        Private Function BoundMethod(compilation As VisualBasicCompilation, text As String) As MethodSymbol
            Return BoundMethodIn(compilation, Nothing, text)
        End Function

        Private Function BoundMethodIn(compilation As VisualBasicCompilation, containingName As String, text As String) As MethodSymbol
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            For Each callSyntax In tree.GetRoot().DescendantNodes().OfType(Of Syntax.InvocationExpressionSyntax)()
                If callSyntax.ToString() <> text Then
                    Continue For
                End If

                If containingName IsNot Nothing Then
                    Dim containing = callSyntax.FirstAncestorOrSelf(Of Syntax.MethodBlockBaseSyntax)()
                    If containing Is Nothing OrElse Not containing.ToString().Contains("Sub " & containingName) Then
                        Continue For
                    End If
                End If

                Dim symbol = TryCast(model.GetSymbolInfo(callSyntax).Symbol, MethodSymbol)
                If symbol Is Nothing Then
                    Throw New InvalidOperationException(text)
                End If

                Return symbol
            Next

            Throw New InvalidOperationException(text)
        End Function

        Private Shared Sub AssertOriginal(method As MethodSymbol, ParamArray expected() As NullableAnnotation)
            Dim owner = method.OriginalDefinition
            If Not owner.IsReducedExtensionMethod Then
                owner = method
            End If

            Dim actual = owner.GetTypeArgumentNullableAnnotations()
            Assert.Equal(expected.Length, actual.Length)
            For index = 0 To expected.Length - 1
                Assert.Equal(expected(index), actual(index))
            Next

            AssertPublicTypeArgumentsAreNone(method)
        End Sub

        Private Shared Sub AssertPublicTypeArgumentsAreNone(method As MethodSymbol)
            For Each annotation In DirectCast(method, IMethodSymbol).TypeArgumentNullableAnnotations
                Assert.Equal(NullableAnnotation.None, annotation)
            Next
        End Sub

    End Class

End Namespace
