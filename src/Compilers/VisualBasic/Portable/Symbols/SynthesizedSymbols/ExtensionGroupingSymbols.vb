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
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    Partial Friend MustInherit Class SourceMemberContainerTypeSymbol

        Private ReadOnly _extensionGroupingGate As New Object()
        Private _extensionGroupings As ImmutableArray(Of NamedTypeSymbol)
        Private _extensionGroupingsBuilt As Boolean
        Private _buildingExtensionGroupings As Boolean

        Friend Overrides Function GetExtensionGroupingTypes() As ImmutableArray(Of NamedTypeSymbol)
            SyncLock _extensionGroupingGate
                If _extensionGroupingsBuilt Then
                    Return _extensionGroupings
                End If

                If _buildingExtensionGroupings Then
                    Return ImmutableArray(Of NamedTypeSymbol).Empty
                End If

                _buildingExtensionGroupings = True
            End SyncLock

            Dim built = ImmutableArray(Of NamedTypeSymbol).Empty
            Try
                built = ExtensionGroupingBuilder.Build(Me)
            Finally
                SyncLock _extensionGroupingGate
                    If Not _extensionGroupingsBuilt Then
                        _extensionGroupings = built
                        _extensionGroupingsBuilt = True
                    End If

                    _buildingExtensionGroupings = False
                End SyncLock
            End Try

            Return _extensionGroupings
        End Function

        Friend Overrides Iterator Function GetPropertiesToEmit() As IEnumerable(Of PropertySymbol)
            For Each [property] In MyBase.GetPropertiesToEmit()
                ' The module property includes the receiver in its parameter list. Consumers read the
                ' grouping property instead. The static implementation accessors are still emitted.
                If Not [property].IsExtensionProperty Then
                    Yield [property]
                End If
            Next
        End Function

    End Class

    Friend Module ExtensionGroupingBuilder

        Friend Function Build(container As SourceMemberContainerTypeSymbol) As ImmutableArray(Of NamedTypeSymbol)
            If container.TypeKind <> TypeKind.Module Then
                Return ImmutableArray(Of NamedTypeSymbol).Empty
            End If

            Dim groups = New Dictionary(Of String, ArrayBuilder(Of SourcePropertySymbol))(StringComparer.Ordinal)
            Dim order = ArrayBuilder(Of String).GetInstance()
            For Each member In container.GetMembers()
                Dim [property] = TryCast(member, SourcePropertySymbol)
                If [property] Is Nothing OrElse Not [property].IsExtensionProperty Then
                    Continue For
                End If

                Dim parameters = [property].Parameters
                If parameters.IsDefault OrElse parameters.Length = 0 OrElse parameters(0).Type.IsErrorType() Then
                    Continue For
                End If

                Dim key = parameters(0).Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                Dim builder As ArrayBuilder(Of SourcePropertySymbol) = Nothing
                If Not groups.TryGetValue(key, builder) Then
                    builder = ArrayBuilder(Of SourcePropertySymbol).GetInstance()
                    groups.Add(key, builder)
                    order.Add(key)
                End If

                builder.Add([property])
            Next

            If order.Count = 0 Then
                order.Free()
                Return ImmutableArray(Of NamedTypeSymbol).Empty
            End If

            Dim usedNames = New HashSet(Of String)(StringComparer.Ordinal)
            Dim result = ArrayBuilder(Of NamedTypeSymbol).GetInstance()
            For Each key In order
                Dim properties = groups(key).ToImmutableAndFree()
                result.Add(ExtensionGroupingTypeSymbol.Create(
                    container,
                    AllocateName(WellKnownMemberNames.ExtensionGroupingTypePrefix, key, usedNames),
                    AllocateName(WellKnownMemberNames.ExtensionMarkerTypePrefix, key, usedNames),
                    properties))
            Next

            order.Free()
            Return result.ToImmutableAndFree()
        End Function

        Private Function AllocateName(prefix As String, key As String, used As HashSet(Of String)) As String
            Dim name = prefix & StableHash(prefix & "|" & key)
            Dim suffix = 1
            While Not used.Add(name)
                suffix += 1
                name = prefix & StableHash(prefix & "|" & key) & "_" & suffix.ToString(Globalization.CultureInfo.InvariantCulture)
            End While

            Return name
        End Function

        Private Function StableHash(text As String) As String
            Dim first As UInteger = 2166136261UI
            Dim second As UInteger = 2166136261UI
            For Each ch In text
                Dim value = CUInt(AscW(ch) And &HFFFF)
                first = (first Xor value) * 16777619UI
                second = (second Xor (value + 97UI)) * 16777619UI
            Next

            Return first.ToString("X8") & second.ToString("X8")
        End Function

    End Module

    Friend MustInherit Class ExtensionMetadataTypeSymbol
        Inherits InstanceTypeSymbol

        Private ReadOnly _containing As Symbol
        Private ReadOnly _baseType As NamedTypeSymbol
        Private ReadOnly _name As String
        Private ReadOnly _isMustInherit As Boolean
        Private _members As ImmutableArray(Of Symbol)
        Private _nestedTypes As ImmutableArray(Of NamedTypeSymbol)
        Private _defaultMemberName As String

        Protected Sub New(containing As Symbol, name As String, baseType As NamedTypeSymbol, isMustInherit As Boolean)
            _containing = containing
            _name = name
            _baseType = baseType
            _isMustInherit = isMustInherit
            _members = ImmutableArray(Of Symbol).Empty
            _nestedTypes = ImmutableArray(Of NamedTypeSymbol).Empty
        End Sub

        Protected Sub Seal(members As ImmutableArray(Of Symbol), nestedTypes As ImmutableArray(Of NamedTypeSymbol), defaultMemberName As String)
            _members = members
            _nestedTypes = If(nestedTypes.IsDefault, ImmutableArray(Of NamedTypeSymbol).Empty, nestedTypes)
            _defaultMemberName = defaultMemberName
        End Sub

        Public Overrides Function GetMembers() As ImmutableArray(Of Symbol)
            Return _members
        End Function

        Public Overrides Function GetMembers(name As String) As ImmutableArray(Of Symbol)
            Dim builder = ArrayBuilder(Of Symbol).GetInstance()
            For Each member In _members
                If IdentifierComparison.Equals(member.Name, name) Then
                    builder.Add(member)
                End If
            Next

            Return builder.ToImmutableAndFree()
        End Function

        Public Overrides Function GetTypeMembers() As ImmutableArray(Of NamedTypeSymbol)
            Return _nestedTypes
        End Function

        Public Overrides Function GetTypeMembers(name As String) As ImmutableArray(Of NamedTypeSymbol)
            Return GetTypeMembers(name, 0)
        End Function

        Public Overrides Function GetTypeMembers(name As String, arity As Integer) As ImmutableArray(Of NamedTypeSymbol)
            If arity <> 0 Then
                Return ImmutableArray(Of NamedTypeSymbol).Empty
            End If

            Dim builder = ArrayBuilder(Of NamedTypeSymbol).GetInstance()
            For Each nested In _nestedTypes
                If IdentifierComparison.Equals(nested.Name, name) Then
                    builder.Add(nested)
                End If
            Next

            Return builder.ToImmutableAndFree()
        End Function

        Public Overrides ReadOnly Property MemberNames As IEnumerable(Of String)
            Get
                Dim names = New HashSet(Of String)(StringComparer.Ordinal)
                For Each member In _members
                    names.Add(member.Name)
                Next

                For Each nested In _nestedTypes
                    names.Add(nested.Name)
                Next

                Return names
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
                Return _containing
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return TryCast(_containing, NamedTypeSymbol)
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return Accessibility.Public
            End Get
        End Property

        Friend Overrides ReadOnly Property DefaultPropertyName As String
            Get
                Return _defaultMemberName
            End Get
        End Property

        Public Overrides ReadOnly Property IsMustInherit As Boolean
            Get
                Return _isMustInherit
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
                Dim containingType = TryCast(_containing, NamedTypeSymbol)
                If containingType IsNot Nothing Then
                    Return containingType.Locations
                End If

                Return ImmutableArray(Of Location).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return ImmutableArray(Of SyntaxReference).Empty
            End Get
        End Property

        Friend Overrides Function MakeAcyclicBaseType(diagnostics As BindingDiagnosticBag) As NamedTypeSymbol
            Return _baseType
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
            Return Nothing
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
                Return _name
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return True
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

    End Class

    Friend NotInheritable Class ExtensionGroupingTypeSymbol
        Inherits ExtensionMetadataTypeSymbol

        Private Sub New(container As SourceMemberContainerTypeSymbol, name As String, baseType As NamedTypeSymbol)
            MyBase.New(container, name, baseType, isMustInherit:=False)
        End Sub

        Friend Shared Function Create(container As SourceMemberContainerTypeSymbol,
                                      name As String,
                                      markerName As String,
                                      properties As ImmutableArray(Of SourcePropertySymbol)) As ExtensionGroupingTypeSymbol
            Dim compilation = container.DeclaringCompilation
            Dim objectType = compilation.GetSpecialType(SpecialType.System_Object)
            Dim grouping = New ExtensionGroupingTypeSymbol(container, name, objectType)
            Dim marker = New ExtensionMarkerTypeSymbol(grouping, markerName, objectType, properties(0).Parameters(0))
            Dim members = ArrayBuilder(Of Symbol).GetInstance()
            Dim defaultMemberName As String = Nothing
            members.Add(New ExtensionGroupingConstructorSymbol(grouping))

            For Each sourceProperty In properties
                ' A parameterized extension property is the receiver's indexer. The Default keyword is
                ' rejected on a module member, so the indexer is recognized by its extra parameters.
                If defaultMemberName Is Nothing AndAlso sourceProperty.ParameterCount > 1 Then
                    defaultMemberName = sourceProperty.MetadataName
                End If

                Dim getter = CreateAccessor(grouping, sourceProperty.GetMethod, MethodKind.PropertyGet, markerName)
                Dim setter = CreateAccessor(grouping, sourceProperty.SetMethod, MethodKind.PropertySet, markerName)
                Dim emitted = New ExtensionGroupingPropertySymbol(grouping, sourceProperty, getter, setter, markerName)
                If getter IsNot Nothing Then
                    getter.SetAssociatedProperty(emitted)
                End If

                If setter IsNot Nothing Then
                    setter.SetAssociatedProperty(emitted)
                End If

                members.Add(emitted)
                If getter IsNot Nothing Then
                    members.Add(getter)
                End If

                If setter IsNot Nothing Then
                    members.Add(setter)
                End If
            Next

            grouping.Seal(members.ToImmutableAndFree(), ImmutableArray.Create(Of NamedTypeSymbol)(marker), defaultMemberName)
            Return grouping
        End Function

        Private Shared Function CreateAccessor(container As ExtensionGroupingTypeSymbol,
                                               implementation As MethodSymbol,
                                               kind As MethodKind,
                                               markerName As String) As ExtensionGroupingMethodSymbol
            If implementation Is Nothing Then
                Return Nothing
            End If

            Return New ExtensionGroupingMethodSymbol(container, implementation, kind, markerName)
        End Function

        Friend Overrides Sub AddSynthesizedAttributes(moduleBuilder As PEModuleBuilder, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData))
            MyBase.AddSynthesizedAttributes(moduleBuilder, attributes)
            Dim compilation = DeclaringCompilation
            AddSynthesizedAttribute(attributes, compilation.TrySynthesizeAttribute(
                WellKnownMember.System_Runtime_CompilerServices_ExtensionAttribute__ctor,
                isOptionalUse:=True))
            If Not String.IsNullOrEmpty(DefaultPropertyName) Then
                Dim stringType = compilation.GetSpecialType(SpecialType.System_String)
                AddSynthesizedAttribute(attributes, compilation.TrySynthesizeAttribute(
                    WellKnownMember.System_Reflection_DefaultMemberAttribute__ctor,
                    ImmutableArray.Create(New TypedConstant(stringType, TypedConstantKind.Primitive, DefaultPropertyName)),
                    isOptionalUse:=True))
            End If
        End Sub

    End Class

    Friend NotInheritable Class ExtensionMarkerTypeSymbol
        Inherits ExtensionMetadataTypeSymbol

        Friend Sub New(grouping As ExtensionGroupingTypeSymbol, name As String, baseType As NamedTypeSymbol, receiver As ParameterSymbol)
            MyBase.New(grouping, name, baseType, isMustInherit:=True)
            Dim method = New ExtensionGroupingMethodSymbol(Me, receiver)
            Seal(ImmutableArray.Create(Of Symbol)(method), ImmutableArray(Of NamedTypeSymbol).Empty, defaultMemberName:=Nothing)
        End Sub

    End Class

    Friend NotInheritable Class ExtensionGroupingPropertySymbol
        Inherits SynthesizedPropertyBase

        Private ReadOnly _container As NamedTypeSymbol
        Private ReadOnly _name As String
        Private ReadOnly _type As TypeSymbol
        Private ReadOnly _typeModifiers As ImmutableArray(Of CustomModifier)
        Private ReadOnly _getter As MethodSymbol
        Private ReadOnly _setter As MethodSymbol
        Private ReadOnly _isDefault As Boolean
        Private ReadOnly _accessibility As Accessibility
        Private ReadOnly _markerName As String

        Friend Sub New(container As NamedTypeSymbol,
                       source As SourcePropertySymbol,
                       getter As MethodSymbol,
                       setter As MethodSymbol,
                       markerName As String)
            _container = container
            _name = source.MetadataName
            _type = source.Type
            _typeModifiers = source.TypeCustomModifiers
            If _typeModifiers.IsDefault Then
                _typeModifiers = ImmutableArray(Of CustomModifier).Empty
            End If

            _getter = getter
            _setter = setter
            _isDefault = source.IsDefault
            _accessibility = source.DeclaredAccessibility
            _markerName = markerName
        End Sub

        Public Overrides ReadOnly Property Name As String
            Get
                Return _name
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return _container
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return _container
            End Get
        End Property

        Public Overrides ReadOnly Property Type As TypeSymbol
            Get
                Return _type
            End Get
        End Property

        Public Overrides ReadOnly Property TypeCustomModifiers As ImmutableArray(Of CustomModifier)
            Get
                Return _typeModifiers
            End Get
        End Property

        Public Overrides ReadOnly Property GetMethod As MethodSymbol
            Get
                Return _getter
            End Get
        End Property

        Public Overrides ReadOnly Property SetMethod As MethodSymbol
            Get
                Return _setter
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                If _getter IsNot Nothing Then
                    Return _getter.Parameters
                End If

                Dim setterParameters = _setter.Parameters
                Return setterParameters.RemoveAt(setterParameters.Length - 1)
            End Get
        End Property

        Public Overrides ReadOnly Property IsDefault As Boolean
            Get
                Return _isDefault
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return _accessibility
            End Get
        End Property

        Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return _container.Locations
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return ImmutableArray(Of SyntaxReference).Empty
            End Get
        End Property

        Friend Overrides Sub AddSynthesizedAttributes(moduleBuilder As PEModuleBuilder, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData))
            MyBase.AddSynthesizedAttributes(moduleBuilder, attributes)
            AddSynthesizedAttribute(attributes, moduleBuilder.TrySynthesizeExtensionMarkerAttribute(_markerName))
        End Sub

    End Class

    Friend NotInheritable Class ExtensionGroupingMethodSymbol
        Inherits SynthesizedRegularMethodBase

        Private ReadOnly _parameters As ImmutableArray(Of ParameterSymbol)
        Private ReadOnly _returnType As TypeSymbol
        Private ReadOnly _methodKind As MethodKind
        Private ReadOnly _accessibility As Accessibility
        Private ReadOnly _markerName As String
        Private ReadOnly _throw As Boolean
        Private ReadOnly _specialName As Boolean
        Private _associated As Symbol

        Friend Sub New(container As NamedTypeSymbol, implementation As MethodSymbol, kind As MethodKind, markerName As String)
            MyBase.New(DummySyntax(), container, implementation.Name, isShared:=False)
            _parameters = CopyParameters(Me, implementation.Parameters, start:=1)
            _returnType = implementation.ReturnType
            _methodKind = kind
            _accessibility = implementation.DeclaredAccessibility
            _markerName = markerName
            _throw = True
            _specialName = True
        End Sub

        Friend Sub New(container As NamedTypeSymbol, receiver As ParameterSymbol)
            MyBase.New(DummySyntax(), container, WellKnownMemberNames.ExtensionMarkerMethodName, isShared:=True)
            _parameters = ImmutableArray.Create(Of ParameterSymbol)(CopyParameter(Me, receiver, ordinal:=0))
            _returnType = container.ContainingAssembly.GetSpecialType(SpecialType.System_Void)
            _methodKind = MethodKind.Ordinary
            _accessibility = Accessibility.Public
            _throw = False
            _specialName = True
        End Sub

        Friend Sub SetAssociatedProperty([property] As PropertySymbol)
            _associated = [property]
        End Sub

        Public Overrides ReadOnly Property MethodKind As MethodKind
            Get
                Return _methodKind
            End Get
        End Property

        Public Overrides ReadOnly Property AssociatedSymbol As Symbol
            Get
                Return _associated
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return _accessibility
            End Get
        End Property

        Public Overrides ReadOnly Property IsSub As Boolean
            Get
                Return _returnType.SpecialType = SpecialType.System_Void
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnType As TypeSymbol
            Get
                Return _returnType
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return _parameters
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return _specialName
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
            If Not _throw Then
                Return factory.Block(factory.Return())
            End If

            Dim constructor = factory.WellKnownMember(Of MethodSymbol)(WellKnownMember.System_NotSupportedException__ctor, isOptional:=True)
            If constructor Is Nothing Then
                Return factory.Block(factory.Return())
            End If

            Return factory.Block(factory.Throw(factory.[New](constructor)))
        End Function

        Friend Overrides Sub AddSynthesizedAttributes(moduleBuilder As PEModuleBuilder, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData))
            MyBase.AddSynthesizedAttributes(moduleBuilder, attributes)
            If _markerName IsNot Nothing Then
                AddSynthesizedAttribute(attributes, moduleBuilder.TrySynthesizeExtensionMarkerAttribute(_markerName))
            Else
                AddSynthesizedAttribute(attributes, DeclaringCompilation.TrySynthesizeAttribute(
                    WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor,
                    isOptionalUse:=True))
            End If
        End Sub

        Private Shared Function DummySyntax() As VisualBasicSyntaxNode
            Return DirectCast(VisualBasicSyntaxTree.Dummy.GetRoot(), VisualBasicSyntaxNode)
        End Function

        Private Shared Function CopyParameters(method As MethodSymbol, source As ImmutableArray(Of ParameterSymbol), start As Integer) As ImmutableArray(Of ParameterSymbol)
            If source.IsDefault OrElse source.Length <= start Then
                Return ImmutableArray(Of ParameterSymbol).Empty
            End If

            Dim builder = ArrayBuilder(Of ParameterSymbol).GetInstance(source.Length - start)
            For i = 0 To source.Length - start - 1
                builder.Add(CopyParameter(method, source(start + i), i))
            Next

            Return builder.ToImmutableAndFree()
        End Function

        Private Shared Function CopyParameter(method As MethodSymbol, source As ParameterSymbol, ordinal As Integer) As SynthesizedParameterSymbol
            Dim modifiers = source.CustomModifiers
            If modifiers.IsDefault Then
                modifiers = ImmutableArray(Of CustomModifier).Empty
            End If

            Dim refModifiers = source.RefCustomModifiers
            If refModifiers.IsDefault Then
                refModifiers = ImmutableArray(Of CustomModifier).Empty
            End If

            Return SynthesizedParameterSymbol.Create(method, source.Type, ordinal, source.IsByRef, source.Name, modifiers, refModifiers)
        End Function

    End Class

    Friend NotInheritable Class ExtensionGroupingConstructorSymbol
        Inherits SynthesizedConstructorBase

        Friend Sub New(container As NamedTypeSymbol)
            MyBase.New(VisualBasicSyntaxTree.DummyReference, container, isShared:=False, binder:=Nothing, diagnostics:=Nothing)
        End Sub

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return Accessibility.Private
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
                Return factory.Block(factory.Return())
            End If

            Return factory.Block(
                factory.ExpressionStatement(factory.Call(factory.Me(), baseConstructor)),
                factory.Return())
        End Function

    End Class

End Namespace
