' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGen

    ''' <summary>
    ''' LC-1F.6b generic method nullable annotation preservation.
    ''' Inferred type arguments keep the compiler's existing inference result.
    ''' </summary>
    Public Class NullableGenericMethodAnnotationTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub ExplicitMethodTypeArgumentPreservesNullableAnnotation()
            Dim compilation = Compile("
Imports System
Module Program
    Function Identity(Of T)(value As T) As T
        Return value
    End Function
    Sub Main()
        Dim ready = Identity(Of String)(""Hello"")
        Dim loose = Identity(Of String?)(Nothing)
        Console.WriteLine(ready.Length)
        Console.WriteLine(loose.Length)
    End Sub
End Module
")
            Assert.Equal("42512:loose", Picture(compilation))
            Dim looseCall = Invocation(compilation, "Identity(Of String?)(Nothing)")
            Assert.Equal(NullableAnnotation.Annotated, looseCall.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, looseCall.Parameters(0).NullableAnnotation)
            Assert.Equal(NullableAnnotation.Annotated, looseCall.TypeArgumentNullableAnnotations.Single())
            Dim readyCall = Invocation(compilation, "Identity(Of String)(""Hello"")")
            Assert.Equal(NullableAnnotation.NotAnnotated, readyCall.ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.NotAnnotated, readyCall.TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub ExplicitMethodTypeArguments_ConstraintsMultiplesAndNested()
            Dim compilation = Compile("
Class Pair(Of T)
    Public Item As T
End Class
Module Program
    Function Constrained(Of T As Class)(value As T) As T
        Return value
    End Function
    Function Pick(Of T, U)(left As T, right As U) As U
        Return right
    End Function
    Function Wrap(Of T)(value As T) As Pair(Of T)
        Return New Pair(Of T) With {.Item = value}
    End Function
    Function Marked(Of T)(value As T?) As T?
        Return value
    End Function
    Sub Go()
        Dim constrainedResult = Constrained(Of String?)(Nothing)
        Dim constrainedLength = constrainedResult.Length
        Dim fromSecond = Pick(Of String?, String)(Nothing, ""x"")
        Dim fromSecondLength = fromSecond.Length
        Dim loose = Pick(Of String, String?)(""x"", Nothing)
        Dim looseLength = loose.Length
        Dim wrapped = Wrap(Of String?)(Nothing)
        Dim nestedLength = wrapped.Item.Length
        Dim markedResult = Marked(Of String)(""Hello"")
        Dim markedLength = markedResult.Length
    End Sub
End Module
")
            Assert.Equal("42512:constrainedResult|42512:loose|42512:markedResult|42512:wrapped.Item", Picture(compilation))
        End Sub

        <Fact>
        Public Sub InferredMethodTypeArguments_KeepExistingInference()
            Dim compilation = Compile("
Module Program
    Function Identity(Of T)(value As T) As T
        Return value
    End Function
    Function FromAnnotated(value As String?) As Integer
        Dim inferred = Identity(value)
        Return inferred.Length
    End Function
    Function FromLiteral() As Integer
        Dim inferred = Identity(""Hello"")
        Return inferred.Length
    End Function
End Module
")
            Assert.Equal("42512:inferred", Picture(compilation))
            Assert.Equal(NullableAnnotation.Annotated, Invocation(compilation, "Identity(value)").ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, Invocation(compilation, "Identity(""Hello"")").ReturnNullableAnnotation)
        End Sub

        <Fact>
        Public Sub ContainingTypeAndMethodTypeParametersStayDistinct()
            Dim compilation = Compile("
Class Container(Of T)
    Public Function First(Of U)(value As T) As T
        Return value
    End Function
    Public Function Second(Of U)(value As U) As U
        Return value
    End Function
End Class
Module Program
    Sub Go()
        Dim annotated As New Container(Of String?)()
        Dim fromType = annotated.First(Of String)(Nothing)
        Dim fromTypeLength = fromType.Length
        Dim fromMethod = annotated.Second(Of String)(""x"")
        Dim fromMethodLength = fromMethod.Length
        Dim plain As New Container(Of String)()
        Dim plainType = plain.First(Of String?)(""x"")
        Dim plainTypeLength = plainType.Length
        Dim plainMethod = plain.Second(Of String?)(Nothing)
        Dim plainMethodLength = plainMethod.Length
    End Sub
End Module
")
            Assert.Equal("42512:fromType|42512:plainMethod", Picture(compilation))
        End Sub

        <Fact>
        Public Sub MethodShapes_ExtensionInterfaceOverrideNestedAsyncByRefAndContract()
            Dim compilation = Compile("
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks
Public Interface IIdentity
    Function Identity(Of T)(value As T) As T
End Interface
Public Class BaseIdentity
    Public Overridable Function Identity(Of T)(value As T) As T
        Return value
    End Function
End Class
Public Class DerivedIdentity
    Inherits BaseIdentity
    Public Overrides Function Identity(Of T)(value As T) As T
        Return value
    End Function
End Class
Public Class Implementation
    Implements IIdentity
    Public Function Identity(Of T)(value As T) As T Implements IIdentity.Identity
        Return value
    End Function
End Class
Public Module Extensions
    <Extension>
    Public Function Extended(Of T)(value As T) As T
        Return value
    End Function
End Module
Public Class Pair(Of T)
    Public Item As T
End Class
Module Program
    Function Nest(Of T)(value As T) As Pair(Of T)
        Return New Pair(Of T) With {.Item = value}
    End Function
    Async Function Load(Of T)(value As T) As Task(Of T)
        Return value
    End Function
    Sub Fill(Of T)(ByRef target As T, value As T)
        target = value
    End Sub
    Async Function Go() As Task
        Dim extended = Extensions.Extended(Of String?)(Nothing)
        Dim extendedLength = extended.Length
        Dim face As IIdentity = New Implementation()
        Dim throughInterface = face.Identity(Of String?)(Nothing)
        Dim interfaceLength = throughInterface.Length
        Dim derived As New DerivedIdentity()
        Dim throughOverride = derived.Identity(Of String?)(Nothing)
        Dim overrideLength = throughOverride.Length
        Dim nested = Nest(Of String?)(Nothing)
        Dim nestedLength = nested.Item.Length
        Dim awaited = Await Load(Of String?)(Nothing)
        Dim awaitedLength = awaited.Length
        Dim target As String = ""x""
        Fill(Of String?)(target, Nothing)
        Dim filledLength = target.Length
    End Function
End Module
")
            Assert.Equal("42510:target|42512:awaited|42512:extended|42512:nested.Item|42512:target|42512:throughInterface|42512:throughOverride", Picture(compilation))
        End Sub

        <Fact>
        Public Sub NotNullMethodAttribute_DoesNotReplaceSubstitutedAnnotation()
            Dim compilation = Compile(Attributes() & "
Module Program
    <NotNull>
    Function Forced(Of T)(value As T) As T
        Return value
    End Function
    Function Open(Of T)(value As T) As T
        Return value
    End Function
    Sub Go()
        Dim forcedResult = Forced(Of String?)(Nothing)
        Dim forcedLength = forcedResult.Length
        Dim openResult = Open(Of String?)(Nothing)
        Dim openLength = openResult.Length
    End Sub
End Module
")
            Assert.Equal("42512:forcedResult|42512:openResult", Picture(compilation))
        End Sub

        <Fact>
        Public Sub ConstructionOrder_NullableAndNotNullDoNotContaminate()
            Const body = "
Function Identity(Of T)(value As T) As T
    Return value
End Function
"
            Dim nullableFirst = Compile("
Module Program
" & body & "
    Sub Go()
        Dim loose = Identity(Of String?)(Nothing)
        Dim ready = Identity(Of String)(""Hello"")
        Dim repeated = Identity(Of String?)(Nothing)
        Dim looseLength = loose.Length
        Dim readyLength = ready.Length
        Dim repeatedLength = repeated.Length
    End Sub
End Module
")
            Dim nullableSecond = Compile("
Module Program
" & body & "
    Sub Go()
        Dim ready = Identity(Of String)(""Hello"")
        Dim loose = Identity(Of String?)(Nothing)
        Dim repeated = Identity(Of String)(""Hello"")
        Dim readyLength = ready.Length
        Dim looseLength = loose.Length
        Dim repeatedLength = repeated.Length
    End Sub
End Module
")
            Assert.Equal("42512:loose|42512:repeated", Picture(nullableFirst))
            Assert.Equal("42512:loose", Picture(nullableSecond))
            Assert.Equal(CompileAndVerify(Compile("
Module Program
" & body & "
    Public Function Ready() As String
        Return Identity(Of String)(""Hello"")
    End Function
End Module
")).VisualizeIL("Program.Ready"),
                         CompileAndVerify(Compile("
Module Program
" & body & "
    Public Function Other() As String
        Return Identity(Of String?)(Nothing)
    End Function
    Public Function Ready() As String
        Return Identity(Of String)(""Hello"")
    End Function
End Module
")).VisualizeIL("Program.Ready"))
        End Sub

        <Fact>
        Public Sub ImportedGenericMethod_PreservesExplicitTypeArgumentAnnotation()
            Dim library = CreateCompilation("
Public Class Catalog
    Public Shared Function Identity(Of T)(value As T) As T
        Return value
    End Function
End Class
", options:=TestOptions.ReleaseDll, parseOptions:=VbNext)
            Dim compilation = CreateCompilation("
Module Program
    Public Sub Go()
        Dim readyResult = Catalog.Identity(Of String)(""Hello"")
        Dim looseResult = Catalog.Identity(Of String?)(Nothing)
        Dim readyLength = readyResult.Length
        Dim looseLength = looseResult.Length
    End Sub
End Module
", references:={library.EmitToImageReference()}, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Assert.Equal("42512:looseResult", Picture(compilation))
            Assert.Equal(NullableAnnotation.Annotated, Invocation(compilation, "Catalog.Identity(Of String?)(Nothing)").TypeArgumentNullableAnnotations.Single())
        End Sub

        <Fact>
        Public Sub LegacyLanguage_RejectsMethodNullableReferenceSyntax()
            Dim legacy = CreateCompilation("
Module Program
    Function Identity(Of T)(value As T) As T
        Return value
    End Function
    Sub Go()
        Dim loose = Identity(Of String?)(Nothing)
        Dim length = loose.Length
    End Sub
End Module
", options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=Vb17)
            Assert.Contains(legacy.GetDiagnostics(), Function(diagnostic) diagnostic.Code = ERRID.ERR_BadTypeArgForStructConstraintNull)
            Assert.DoesNotContain(legacy.GetDiagnostics(), Function(diagnostic) diagnostic.Code >= 42510 AndAlso diagnostic.Code <= 42532)
        End Sub

        <Fact>
        Public Sub DirectAndMutualRecursion_CallerTreatmentStaysDistinct()
            Dim direct = Compile("
Module Program
    Public Function Self(depth As Integer) As String
        If depth <= 0 Then Return ""ready""
        Return Self(depth - 1)
    End Function
    Public Function UseSelf() As Integer
        Dim direct = Self(1)
        Return direct.Length
    End Function
    Public Function SelfNull(depth As Integer) As String
        If depth <= 0 Then Return Nothing
        Return SelfNull(depth - 1)
    End Function
    Public Function UseSelfNull() As Integer
        Dim directNull = SelfNull(1)
        Return directNull.Length
    End Function
    Public Function NotNullDeclared(depth As Integer) As String
        If depth <= 0 Then Return Nothing
        Return NotNullDeclared(depth - 1)
    End Function
End Module
")
            Assert.Equal(1, Count(direct, 42511, "Self(depth - 1)"))
            Assert.Equal(0, Count(direct, 42512, "direct"))
            Assert.Equal(2, Count(direct, 42511, "Nothing"))
            Assert.Equal(1, Count(direct, 42511, "SelfNull(depth - 1)"))
            Assert.Equal(0, Count(direct, 42512, "directNull"))
            Assert.Equal(1, Count(direct, 42511, "NotNullDeclared(depth - 1)"))

            Dim mutual = Compile(MutualSource(reversed:=False))
            Dim reversed = Compile(MutualSource(reversed:=True))
            Assert.Equal(Picture(mutual), Picture(reversed))
            Assert.Equal("42511:ContinueMutual(depth)|42511:Left(depth - 1)|42511:Nothing|42511:NullLeft(depth - 1)|42511:NullRight(depth)|42511:Right(depth)|42511:StopMutual(depth - 1)|42513:carried|42528:Return Again(value)|42528:Return Other(value)|42532:Return ""ready""|42532:Return ContinueMutual(depth)", Picture(mutual))
        End Sub

        <Fact>
        Public Sub OnError_DoesNotSuppressAStraightLineNullAssignment()
            Dim straight = Compile("
Module Program
    Sub Go()
        Dim s As String = ""x""
        On Error Resume Next
        s = Nothing
        Dim length = s.Length
    End Sub
End Module
")
            Assert.Equal(1, Count(straight, 42510, "Nothing"))
            Assert.Equal(1, Count(straight, 42512, "s"))

            Dim handler = Compile("
Module Program
    Sub Go()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        System.Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = Nothing
        Resume Next
    End Sub
End Module
")
            Assert.Equal("42510:Nothing|42512:text", Picture(handler))
        End Sub

        Private Shared Function MutualSource(reversed As Boolean) As String
            Dim left = "
    Public Function Left(depth As Integer) As String
        If depth <= 0 Then Return ""ready""
        Return Right(depth)
    End Function
"
            Dim right = "
    Public Function Right(depth As Integer) As String
        Return Left(depth - 1)
    End Function
"
            Dim nullLeft = "
    Public Function NullLeft(depth As Integer) As String
        If depth <= 0 Then Return Nothing
        Return NullRight(depth)
    End Function
"
            Dim nullRight = "
    Public Function NullRight(depth As Integer) As String
        Return NullLeft(depth - 1)
    End Function
"
            Dim order = If(reversed, right & left & nullRight & nullLeft, left & right & nullLeft & nullRight)
            Return Attributes() & "
Module Program
" & order & "
    Public Function UseMutual() As Integer
        Dim mutual = Left(1)
        Return mutual.Length
    End Function
    Public Function UseNull() As Integer
        Dim mutualNull = NullLeft(1)
        Return mutualNull.Length
    End Function
    Public Function Again(<NotNullWhen(True)> ByRef value As String?) As Boolean
        Return Other(value)
    End Function
    Public Function Other(<NotNullWhen(True)> ByRef value As String?) As Boolean
        Return Again(value)
    End Function
    Public Sub Clear(ByRef value As String)
        Wave(value)
    End Sub
    Public Sub Wave(ByRef value As String)
        Clear(value)
    End Sub
    Public Sub UseOutput(carried As String?)
        Dim output As String = ""x""
        Clear(output)
        Dim length = output.Length
        Clear(carried)
    End Sub
    <DoesNotReturn>
    Public Function StopMutual(depth As Integer) As String
        If depth <= 0 Then Return ""ready""
        Return ContinueMutual(depth)
    End Function
    Public Function ContinueMutual(depth As Integer) As String
        Return StopMutual(depth - 1)
    End Function
    Public Function UseStop() As Integer
        Dim stopped = StopMutual(1)
        Return stopped.Length
    End Function
End Module
"
        End Function

        Private Shared Function Attributes() As String
            Return "
Imports System
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Parameter Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullWhenAttribute
        Inherits Attribute
        Public Sub New(returnValue As Boolean)
        End Sub
    End Class
    <AttributeUsage(AttributeTargets.Method)>
    Public NotInheritable Class DoesNotReturnAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Method Or AttributeTargets.Parameter Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
End Namespace
"
        End Function

        Private Function Compile(source As String) As VisualBasicCompilation
            Return CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
        End Function

        Private Function Invocation(compilation As VisualBasicCompilation, text As String) As IMethodSymbol
            Dim tree = compilation.SyntaxTrees.Single()
            Dim model = compilation.GetSemanticModel(tree)
            For Each callSyntax In tree.GetRoot().DescendantNodes().OfType(Of Microsoft.CodeAnalysis.VisualBasic.Syntax.InvocationExpressionSyntax)()
                If callSyntax.ToString() = text Then
                    Return DirectCast(model.GetSymbolInfo(DirectCast(callSyntax, Microsoft.CodeAnalysis.VisualBasic.Syntax.ExpressionSyntax)).Symbol, IMethodSymbol)
                End If
            Next

            Throw New InvalidOperationException(text)
        End Function

        Private Shared Function DiagnosticText(compilation As VisualBasicCompilation) As String
            Return String.Join(Environment.NewLine, compilation.GetDiagnostics().
                Where(Function(diagnostic) diagnostic.Severity <> DiagnosticSeverity.Hidden).
                Select(Function(diagnostic) diagnostic.ToString()))
        End Function

        Private Shared Function Picture(compilation As VisualBasicCompilation) As String
            Return String.Join("|", compilation.GetDiagnostics().
                Where(Function(diagnostic) diagnostic.Severity <> DiagnosticSeverity.Hidden AndAlso diagnostic.Code >= 42510 AndAlso diagnostic.Code <= 42532).
                Select(Function(diagnostic) CInt(diagnostic.Code).ToString() & ":" & SpanText(diagnostic)).
                OrderBy(Function(text) text, StringComparer.Ordinal))
        End Function

        Private Shared Function Count(compilation As VisualBasicCompilation, code As Integer, span As String) As Integer
            Return compilation.GetDiagnostics().Count(Function(diagnostic) diagnostic.Code = code AndAlso SpanText(diagnostic) = span)
        End Function

        Private Shared Function SpanText(diagnostic As Diagnostic) As String
            If diagnostic.Location Is Nothing OrElse diagnostic.Location.SourceTree Is Nothing Then
                Return ""
            End If

            Return diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan)
        End Function

    End Class

End Namespace
