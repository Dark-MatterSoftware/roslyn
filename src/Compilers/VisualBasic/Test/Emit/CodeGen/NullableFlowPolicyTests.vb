' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGen

    ''' <summary>
    ''' LC-1F.6c approved nullable policies: inferred generic annotations, declared
    ''' contracts at ordinary call sites, conservative On Error flow, and NotNull precedence.
    ''' </summary>
    Public Class NullableFlowPolicyTests
        Inherits BasicTestBase

        Private Shared ReadOnly VbNext As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VbNext)
        Private Shared ReadOnly Vb17 As VisualBasicParseOptions = TestOptions.Regular.WithLanguageVersion(LanguageVersion.VisualBasic17_13)

        <Fact>
        Public Sub InferredIdentity_WarnsForNullableAndNotForLiteral()
            Dim compilation = Compile("
Imports System
Module Program
    Function Identity(Of T)(value As T) As T
        Return value
    End Function
    Sub Test(value As String?)
        Dim inferred = Identity(value)
        Console.WriteLine(inferred.Length)
        Dim literal = Identity(""Hello"")
        Console.WriteLine(literal.Length)
    End Sub
End Module
")
            Assert.Equal("42512:inferred", Picture(compilation))
            Assert.Equal(NullableAnnotation.Annotated, Invocation(compilation, "Identity(value)").ReturnNullableAnnotation)
            Assert.Equal(NullableAnnotation.None, Invocation(compilation, "Identity(""Hello"")").ReturnNullableAnnotation)
        End Sub

        <Fact>
        Public Sub InferredAnnotations_CoverTheApprovedMatrix()
            Dim compilation = Compile("
Imports System
Imports System.Runtime.CompilerServices
Class Pair(Of T)
    Public Item As T
End Class
Class Container(Of T)
    Public Function Pass(Of U)(value As U) As U
        Return value
    End Function
End Class
Interface IFace
    Function Echo(Of T)(value As T) As T
End Interface
Class Face
    Implements IFace
    Public Function Echo(Of T)(value As T) As T Implements IFace.Echo
        Return value
    End Function
End Class
Module Extensions
    <Extension>
    Public Function Echo(Of T)(value As T) As T
        Return value
    End Function
End Module
Module Program
    Function Identity(Of T)(value As T) As T
        Return value
    End Function
    Function Pick(Of T)(left As T, right As T) As T
        Return right
    End Function
    Function Two(Of T, U)(left As T, right As U) As U
        Return right
    End Function
    Function Wrap(Of T)(value As T) As Pair(Of T)
        Return New Pair(Of T)()
    End Function
    Function Constrained(Of T As Class)(value As T) As T
        Return value
    End Function
    Async Function Delayed(Of T)(value As T) As System.Threading.Tasks.Task(Of T)
        Return value
    End Function
    Sub ByRefEcho(Of T)(ByRef value As T)
    End Sub
    Function OptionalEcho(Of T)(Optional value As T = Nothing) As T
        Return value
    End Function
    Sub Named(value As String?)
        Dim ready As String = ""Hello""
        Dim current As String? = ""kept""
        Dim cleared As String? = Nothing
        Dim fromReady = Identity(ready)
        Dim fromCurrent = Identity(current)
        Dim fromCleared = Identity(cleared)
        If value IsNot Nothing Then
            Dim narrowed = Identity(value)
            Console.WriteLine(narrowed.Length)
        End If
        Dim mixed = Pick(value, ready)
        Dim reversed = Pick(ready, value)
        Dim second = Two(ready, value)
        Dim first = Two(value, ready)
        Dim nested = Wrap(value)
        Dim box As New Container(Of String)()
        Dim methodArg = box.Pass(value)
        Dim extended = value.Echo()
        Dim face As IFace = New Face()
        Dim through = face.Echo(value)
        Dim explicitNull = Identity(Of String?)(value)
        Dim explicitReady = Identity(Of String)(ready)
        Dim again = Identity(value)
        Dim byRefValue As String = ""x""
        ByRefEcho(byRefValue)
        Dim optionalReady = OptionalEcho(ready)
        Console.WriteLine(fromReady.Length)
        Console.WriteLine(fromCurrent.Length)
        Console.WriteLine(fromCleared.Length)
        Console.WriteLine(mixed.Length)
        Console.WriteLine(reversed.Length)
        Console.WriteLine(second.Length)
        Console.WriteLine(first.Length)
        Console.WriteLine(nested.Item.Length)
        Console.WriteLine(methodArg.Length)
        Console.WriteLine(extended.Length)
        Console.WriteLine(through.Length)
        Console.WriteLine(explicitNull.Length)
        Console.WriteLine(explicitReady.Length)
        Console.WriteLine(again.Length)
        Console.WriteLine(optionalReady.Length)
    End Sub
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:fromCurrent"), seen)
            Assert.True(seen.Contains("42512:fromCleared"), seen)
            Assert.True(seen.Contains("42512:mixed"), seen)
            Assert.True(seen.Contains("42512:reversed"), seen)
            Assert.True(seen.Contains("42512:second"), seen)
            Assert.True(Not seen.Contains("42512:first"), seen)
            Assert.True(seen.Contains("42512:nested.Item"), seen)
            Assert.True(seen.Contains("42512:methodArg"), seen)
            Assert.True(seen.Contains("42512:extended"), seen)
            Assert.True(seen.Contains("42512:through"), seen)
            Assert.True(seen.Contains("42512:explicitNull"), seen)
            Assert.True(seen.Contains("42512:again"), seen)
            Assert.True(Not seen.Contains("42512:fromReady"), seen)
            Assert.True(seen.Contains("42512:narrowed"), seen)
            Assert.True(Not seen.Contains("42512:explicitReady"), seen)
            Assert.True(Not seen.Contains("42512:optionalReady"), seen)
        End Sub

        <Fact>
        Public Sub OrdinaryCallersUseDeclaredContracts()
            Dim compilation = Compile(Attributes() & "
Module Program
    Function GetName() As String
        Return Nothing
    End Function
    Function UseName() As Integer
        Dim name = GetName()
        Return name.Length
    End Function
    Function Left(depth As Integer) As String
        If depth <= 0 Then Return ""ready""
        Return Right(depth)
    End Function
    Function Right(depth As Integer) As String
        Return Left(depth - 1)
    End Function
    Function UseLeft() As Integer
        Dim mutual = Left(1)
        Return mutual.Length
    End Function
    Function Bad() As String
        Return Nothing
    End Function
    Function UseBad() As Integer
        Dim bad = Bad()
        Return bad.Length
    End Function
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42511:Nothing"), seen)
            Assert.True(Not seen.Contains("42512:name"), seen)
            Assert.True(Not seen.Contains("42512:mutual"), seen)
            Assert.True(Not seen.Contains("42512:bad"), seen)
        End Sub

        <Fact>
        Public Sub HandlerResumeNextWarnsAfterNullAssignment()
            Dim compilation = Compile("
Imports System
Module Program
    Sub Go()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = Nothing
        Resume Next
    End Sub
    Sub Straight()
        Dim text As String = ""Hello""
        On Error Resume Next
        Console.WriteLine(text.Length)
    End Sub
    Sub SafeHandler()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        Console.WriteLine(text.Length)
        Exit Sub
Handler:
        text = ""ready""
        Resume Next
    End Sub
    Sub Cleared()
        Dim text As String = ""Hello""
        On Error GoTo Handler
        Error 5
        Exit Sub
Handler:
        text = Nothing
        Resume Next
        On Error GoTo 0
        text = ""safe""
        Console.WriteLine(text.Length)
    End Sub
End Module
")
            Assert.Equal(1, Count(compilation, 42512, "text"))
            Assert.Equal(2, Count(compilation, 42510, "Nothing"))
        End Sub

        <Fact>
        Public Sub NotNullWinsOverMaybeNullInEitherOrder()
            Dim compilation = Compile(Attributes() & "
Module Program
    <MaybeNull>
    <NotNull>
    Function First() As String
        Return Nothing
    End Function
    <NotNull>
    <MaybeNull>
    Function Second() As String
        Return Nothing
    End Function
    Function UseBoth() As Integer
        Dim left = First()
        Dim right = Second()
        Return left.Length + right.Length
    End Function
End Module
")
            Dim seen = Picture(compilation)
            Assert.Equal(2, Count(compilation, 42511, "Nothing"))
            Assert.True(Not seen.Contains("42512:"), seen)
        End Sub

        <Fact>
        Public Sub GenericNotNullReturnRefinesTheCaller()
            Dim compilation = Compile(Attributes() & "
Module Program
    Function RequireValue(Of T As Class)(value As T) As <NotNull> T
        If value Is Nothing Then
            Throw New System.ArgumentNullException(NameOf(value))
        End If
        Return value
    End Function
    Function Unchecked(Of T As Class)(value As T) As T
        Return value
    End Function
    Sub Test(value As String?)
        Dim result = RequireValue(Of String?)(value)
        Console.WriteLine(result.Length)
        Dim loose = Unchecked(Of String?)(value)
        Console.WriteLine(loose.Length)
    End Sub
    Function Broken(Of T As Class)(value As T) As <NotNull> T
        Return Nothing
    End Function
End Module
")
            Dim seen = Picture(compilation)
            Assert.True(seen.Contains("42512:loose"), seen)
            Assert.True(seen.Contains("42511:Nothing"), seen)
            Assert.True(Not seen.Contains("42512:result"), seen)
        End Sub

        <Fact>
        Public Sub LegacyOnErrorKeepsTheSameIL()
            Dim source = "
Module Program
    Sub Go()
        On Error Resume Next
        Dim text As String = ""Hello""
        System.Console.WriteLine(text)
    End Sub
End Module
"
            Dim modern = CreateCompilation(source, options:=TestOptions.ReleaseDll.WithNullableContext(NullableContextOptions.Enable), parseOptions:=VbNext)
            Dim legacy = CreateCompilation(source, options:=TestOptions.ReleaseDll, parseOptions:=Vb17)
            Assert.Equal(0, modern.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code >= 42510 AndAlso diagnostic.Code <= 42532).Count())
            Assert.Equal(0, legacy.GetDiagnostics().Where(Function(diagnostic) diagnostic.Code >= 42510 AndAlso diagnostic.Code <= 42532).Count())
        End Sub

        Private Shared Function Attributes() As String
            Return "
Imports System
Imports System.Diagnostics.CodeAnalysis
Namespace System.Diagnostics.CodeAnalysis
    <AttributeUsage(AttributeTargets.Method Or AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.Field Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class NotNullAttribute
        Inherits Attribute
    End Class
    <AttributeUsage(AttributeTargets.Method Or AttributeTargets.Parameter Or AttributeTargets.Property Or AttributeTargets.Field Or AttributeTargets.ReturnValue)>
    Public NotInheritable Class MaybeNullAttribute
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

        Private Shared Function Count(compilation As VisualBasicCompilation, code As Integer, span As String) As Integer
            Return compilation.GetDiagnostics().Count(Function(diagnostic) diagnostic.Code = code AndAlso SpanText(diagnostic) = span)
        End Function

        Private Shared Function Picture(compilation As VisualBasicCompilation) As String
            Return String.Join("|", compilation.GetDiagnostics().
                Where(Function(diagnostic) diagnostic.Severity <> DiagnosticSeverity.Hidden AndAlso diagnostic.Code >= 42510 AndAlso diagnostic.Code <= 42532).
                Select(Function(diagnostic) CInt(diagnostic.Code).ToString() & ":" & SpanText(diagnostic)).
                OrderBy(Function(text) text, StringComparer.Ordinal))
        End Function

        Private Shared Function SpanText(diagnostic As Diagnostic) As String
            If diagnostic.Location Is Nothing OrElse diagnostic.Location.SourceTree Is Nothing Then
                Return ""
            End If

            Return diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan)
        End Function

    End Class

End Namespace
