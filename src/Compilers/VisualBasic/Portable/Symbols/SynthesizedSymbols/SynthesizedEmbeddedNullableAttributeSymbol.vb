' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Emit

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' Embedded System.Runtime.CompilerServices.NullableAttribute used when the target framework
    ''' does not define the standard nullable metadata attribute.
    ''' </summary>
    Friend NotInheritable Class SynthesizedEmbeddedNullableAttributeSymbol
        Inherits InstanceTypeSymbol

        Friend Const NamespaceName As String = "System.Runtime.CompilerServices"
        Friend Const TypeName As String = "NullableAttribute"

        Private Shared ReadOnly AttributeUsageTargets As AttributeTargets =
            AttributeTargets.Class Or AttributeTargets.Event Or AttributeTargets.Field Or
            AttributeTargets.GenericParameter Or AttributeTargets.Parameter Or
            AttributeTargets.Property Or AttributeTargets.ReturnValue

        Private ReadOnly _baseType As NamedTypeSymbol
        Private ReadOnly _namespace As NamespaceSymbol
        Private ReadOnly _members As ImmutableArray(Of Symbol)

        Friend Sub New(containingNamespace As NamespaceSymbol, baseType As NamedTypeSymbol, byteType As TypeSymbol, byteArrayType As TypeSymbol)
            _baseType = baseType
            _namespace = containingNamespace
            _members = ImmutableArray.Create(Of Symbol)(
                New SynthesizedEmbeddedNullableAttributeConstructorSymbol(Me, byteType),
                New SynthesizedEmbeddedNullableAttributeConstructorSymbol(Me, byteArrayType))
        End Sub

        Friend ReadOnly Property ByteConstructor As MethodSymbol
            Get
                Return DirectCast(_members(0), MethodSymbol)
            End Get
        End Property

        Friend ReadOnly Property ByteArrayConstructor As MethodSymbol
            Get
                Return DirectCast(_members(1), MethodSymbol)
            End Get
        End Property

        Public Overrides Function GetMembers() As ImmutableArray(Of Symbol)
            Return _members
        End Function

        Public Overrides Function GetMembers(name As String) As ImmutableArray(Of Symbol)
            Return (From member In GetMembers() Where IdentifierComparison.Equals(member.Name, name)).AsImmutable
        End Function

        Public Overrides Function GetTypeMembers() As ImmutableArray(Of NamedTypeSymbol)
            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Public Overrides Function GetTypeMembers(name As String) As ImmutableArray(Of NamedTypeSymbol)
            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Public Overrides Function GetTypeMembers(name As String, arity As Integer) As ImmutableArray(Of NamedTypeSymbol)
            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Public Overrides ReadOnly Property MemberNames As IEnumerable(Of String)
            Get
                Return New HashSet(Of String)(From member In GetMembers() Select member.Name)
            End Get
        End Property

        Friend Overrides Function GetFieldsToEmit() As IEnumerable(Of FieldSymbol)
            Return SpecializedCollections.EmptyEnumerable(Of FieldSymbol)()
        End Function

        Public Overrides ReadOnly Property AssociatedSymbol As Symbol
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property Arity As Integer
            Get
                Return 0
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return _namespace
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return Accessibility.Friend
            End Get
        End Property

        Friend Overrides ReadOnly Property DefaultPropertyName As String
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property IsMustInherit As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsNotInheritable As Boolean
            Get
                Return True
            End Get
        End Property

        Friend Overrides ReadOnly Property ShadowsExplicitly As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return ImmutableArray(Of Location).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return ImmutableArray(Of SyntaxReference).Empty
            End Get
        End Property

        Friend Overrides Function MakeAcyclicBaseType(diagnostics As BindingDiagnosticBag) As NamedTypeSymbol
            Return MakeDeclaredBase(Nothing, diagnostics)
        End Function

        Friend Overrides Function MakeAcyclicInterfaces(diagnostics As BindingDiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Friend Overrides Function MakeDeclaredBase(basesBeingResolved As BasesBeingResolved, diagnostics As BindingDiagnosticBag) As NamedTypeSymbol
            Return _baseType
        End Function

        Friend Overrides Function MakeDeclaredInterfaces(basesBeingResolved As BasesBeingResolved, diagnostics As BindingDiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Friend Overrides ReadOnly Property MangleName As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property MightContainExtensionMethods As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property HasCodeAnalysisEmbeddedAttribute As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property HasVisualBasicEmbeddedAttribute As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property HasCompilerLoweringPreserveAttribute As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property IsExtensibleInterfaceNoUseSiteDiagnostics As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property IsWindowsRuntimeImport As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property ShouldAddWinRTMembers As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property IsComImport As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property CoClassType As TypeSymbol
            Get
                Return Nothing
            End Get
        End Property

        Friend Overrides Function GetAppliedConditionalSymbols() As ImmutableArray(Of String)
            Return ImmutableArray(Of String).Empty
        End Function

        Friend Overrides Function GetAttributeUsageInfo() As AttributeUsageInfo
            Return New AttributeUsageInfo(AttributeUsageTargets, allowMultiple:=False, inherited:=False)
        End Function

        Friend Overrides ReadOnly Property HasDeclarativeSecurity As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides Function GetGuidString(ByRef guidString As String) As Boolean
            guidString = Nothing
            Return False
        End Function

        Friend Overrides Function GetSecurityInformation() As IEnumerable(Of Microsoft.Cci.SecurityAttribute)
            Return Nothing
        End Function

        Public Overrides ReadOnly Property Name As String
            Get
                Return TypeName
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsSerializable As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property Layout As TypeLayout
            Get
                Return Nothing
            End Get
        End Property

        Friend Overrides ReadOnly Property MarshallingCharSet As CharSet
            Get
                Return DefaultMarshallingCharSet
            End Get
        End Property

        Public Overrides ReadOnly Property TypeKind As TypeKind
            Get
                Return TypeKind.Class
            End Get
        End Property

        Friend Overrides ReadOnly Property IsInterface As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property TypeParameters As ImmutableArray(Of TypeParameterSymbol)
            Get
                Return ImmutableArray(Of TypeParameterSymbol).Empty
            End Get
        End Property

        Friend Overrides Sub GenerateDeclarationErrors(cancellationToken As CancellationToken)
        End Sub

        Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
            Get
                Return True
            End Get
        End Property

        Friend Overrides ReadOnly Property EmbeddedSymbolKind As EmbeddedSymbolKind
            Get
                Return EmbeddedSymbolKind.None
            End Get
        End Property

        Friend Overrides ReadOnly Property ObsoleteAttributeData As ObsoleteAttributeData
            Get
                Return Nothing
            End Get
        End Property

        Friend Overrides Function GetSynthesizedWithEventsOverrides() As IEnumerable(Of PropertySymbol)
            Return SpecializedCollections.EmptyEnumerable(Of PropertySymbol)()
        End Function

        Friend Overrides ReadOnly Property HasAnyDeclaredRequiredMembers As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides Sub AddSynthesizedAttributes(moduleBuilder As PEModuleBuilder, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData))
            MyBase.AddSynthesizedAttributes(moduleBuilder, attributes)

            Dim compilation = DeclaringCompilation
            AddSynthesizedAttribute(attributes, compilation.TrySynthesizeAttribute(WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor, isOptionalUse:=True))
            AddSynthesizedAttribute(attributes, compilation.SynthesizeAttributeUsageAttribute(AttributeUsageTargets, allowMultiple:=False, inherited:=False))
        End Sub

    End Class

    Friend NotInheritable Class SynthesizedEmbeddedNullableAttributeConstructorSymbol
        Inherits SynthesizedConstructorBase

        Private ReadOnly _parameters As ImmutableArray(Of ParameterSymbol)

        Public Sub New(container As NamedTypeSymbol, parameterType As TypeSymbol)
            MyBase.New(VisualBasicSyntaxTree.DummyReference, container, isShared:=False, binder:=Nothing, diagnostics:=Nothing)
            _parameters = ImmutableArray.Create(Of ParameterSymbol)(
                New SynthesizedParameterSymbol(Me, parameterType, ordinal:=0, isByRef:=False, name:="value"))
        End Sub

        Friend Overrides ReadOnly Property ParameterCount As Integer
            Get
                Return _parameters.Length
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return _parameters
            End Get
        End Property

        Friend Overrides ReadOnly Property GenerateDebugInfoImpl As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides Function CalculateLocalSyntaxOffset(localPosition As Integer, localTree As SyntaxTree) As Integer
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetBoundMethodBody(compilationState As TypeCompilationState, diagnostics As BindingDiagnosticBag, <Out> ByRef Optional methodBodyBinder As Binder = Nothing) As BoundBlock
            Dim factory As New SyntheticBoundNodeFactory(Me, Me, Syntax, compilationState, diagnostics)
            Dim baseType = ContainingType.BaseTypeNoUseSiteDiagnostics
            Dim baseConstructor As MethodSymbol = Nothing
            If baseType IsNot Nothing Then
                For Each member In baseType.GetMembers(WellKnownMemberNames.InstanceConstructorName)
                    Dim candidate = TryCast(member, MethodSymbol)
                    If candidate IsNot Nothing AndAlso candidate.ParameterCount = 0 Then
                        baseConstructor = candidate
                        Exit For
                    End If
                Next
            End If

            If baseConstructor Is Nothing Then
                Return factory.Block()
            End If

            Return factory.Block(
                factory.ExpressionStatement(factory.Call(factory.Me(), baseConstructor)),
                factory.Return())
        End Function

    End Class

End Namespace
