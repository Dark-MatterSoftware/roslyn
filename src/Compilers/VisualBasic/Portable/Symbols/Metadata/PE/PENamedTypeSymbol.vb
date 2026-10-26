' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Globalization
Imports System.Reflection.Metadata
Imports System.Reflection.Metadata.Ecma335
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports FieldAttributes = System.Reflection.FieldAttributes
Imports TypeAttributes = System.Reflection.TypeAttributes

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE

    ''' <summary>
    ''' The class to represent all types imported from a PE/module.
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class PENamedTypeSymbol
        Inherits InstanceTypeSymbol

        Private Shared ReadOnly s_emptyNestedTypes As Dictionary(Of String, ImmutableArray(Of PENamedTypeSymbol)) =
            New Dictionary(Of String, ImmutableArray(Of PENamedTypeSymbol))(IdentifierComparison.Comparer)

        Private ReadOnly _container As NamespaceOrTypeSymbol

#Region "Metadata"
        Private ReadOnly _handle As TypeDefinitionHandle
        Private ReadOnly _genericParameterHandles As GenericParameterHandleCollection
        Private ReadOnly _name As String
        Private ReadOnly _flags As TypeAttributes
        Private ReadOnly _arity As UShort
        Private ReadOnly _mangleName As Boolean ' CONSIDER: combine with flags
        Private _lazyIsReadOnly As ThreeState = ThreeState.Unknown
        Private _lazyIsRefLike As ThreeState = ThreeState.Unknown
#End Region

        ''' <summary>
        ''' A map of types immediately contained within this type 
        ''' grouped by their name (case-insensitively).
        ''' </summary>
        Private _lazyNestedTypes As Dictionary(Of String, ImmutableArray(Of PENamedTypeSymbol))

        ''' <summary>
        ''' A set of all the names of the members in this type.
        ''' </summary>
        Private _lazyMemberNames As ICollection(Of String)

        ''' <summary>
        ''' A map of members immediately contained within this type 
        ''' grouped by their name (case-insensitively).
        ''' </summary>
        ''' <remarks></remarks>
        Private _lazyMembers As Dictionary(Of String, ImmutableArray(Of Symbol))

        Private _lazyTypeParameters As ImmutableArray(Of TypeParameterSymbol)

        Private _lazyEnumUnderlyingType As NamedTypeSymbol

        Private _lazyCustomAttributes As ImmutableArray(Of VisualBasicAttributeData)
        Private _lazyConditionalAttributeSymbols As ImmutableArray(Of String)
        Private _lazyAttributeUsageInfo As AttributeUsageInfo = AttributeUsageInfo.Null

        Private _lazyCoClassType As TypeSymbol = ErrorTypeSymbol.UnknownResultType

        ''' <summary>
        ''' Lazily initialized by TypeKind property.
        ''' Using Integer type to make sure read/write operations are atomic.
        ''' </summary>
        ''' <remarks></remarks>
        Private _lazyTypeKind As Integer

        Private _lazyDocComment As Tuple(Of CultureInfo, String)

        Private _lazyDefaultPropertyName As String

        Private _lazyCachedUseSiteInfo As CachedUseSiteInfo(Of AssemblySymbol) = CachedUseSiteInfo(Of AssemblySymbol).Uninitialized ' Indicates unknown state. 

        Private _lazyMightContainExtensionMethods As Byte = ThreeState.Unknown

        Private _lazyHasCodeAnalysisEmbeddedAttribute As Integer = ThreeState.Unknown

        Private _lazyHasVisualBasicEmbeddedAttribute As Integer = ThreeState.Unknown

        Private _lazyHasCompilerLoweringPreserveAttribute As Integer = ThreeState.Unknown

        Private _lazyObsoleteAttributeData As ObsoleteAttributeData = ObsoleteAttributeData.Uninitialized

        Private _lazyIsExtensibleInterface As ThreeState = ThreeState.Unknown

        Friend Sub New(
            moduleSymbol As PEModuleSymbol,
            containingNamespace As PENamespaceSymbol,
            handle As TypeDefinitionHandle
        )
            Me.New(moduleSymbol, containingNamespace, 0, handle)
        End Sub

        Friend Sub New(
            moduleSymbol As PEModuleSymbol,
            containingType As PENamedTypeSymbol,
            handle As TypeDefinitionHandle
        )
            Me.New(moduleSymbol, containingType, CUShort(containingType.MetadataArity), handle)
        End Sub

        Private Sub New(
            moduleSymbol As PEModuleSymbol,
            container As NamespaceOrTypeSymbol,
            containerMetadataArity As UShort,
            handle As TypeDefinitionHandle
        )
            Debug.Assert(Not handle.IsNil)
            Debug.Assert(container IsNot Nothing)

            _handle = handle
            _container = container

            Dim makeBad As Boolean = False

            Dim name As String

            Try
                name = moduleSymbol.Module.GetTypeDefNameOrThrow(handle)
            Catch mrEx As BadImageFormatException
                name = String.Empty
                makeBad = True
            End Try

            Try
                _flags = moduleSymbol.Module.GetTypeDefFlagsOrThrow(handle)
            Catch mrEx As BadImageFormatException
                makeBad = True
            End Try

            Dim metadataArity As Integer

            Try
                _genericParameterHandles = moduleSymbol.Module.GetTypeDefGenericParamsOrThrow(handle)
                metadataArity = _genericParameterHandles.Count
            Catch mrEx As BadImageFormatException
                _genericParameterHandles = Nothing
                metadataArity = 0
                makeBad = True
            End Try

            ' Figure out arity from the language point of view
            If metadataArity > containerMetadataArity Then
                _arity = CType(metadataArity - containerMetadataArity, UShort)
            End If

            If _arity = 0 Then
                _lazyTypeParameters = ImmutableArray(Of TypeParameterSymbol).Empty
                _name = name
                _mangleName = False
            Else
                ' Unmangle name for a generic type.
                _name = MetadataHelpers.UnmangleMetadataNameForArity(name, _arity)
                _mangleName = (_name IsNot name)
            End If

            If makeBad OrElse metadataArity < containerMetadataArity Then
                _lazyCachedUseSiteInfo.Initialize(If(DeriveCompilerFeatureRequiredDiagnostic(), ErrorFactory.ErrorInfo(ERRID.ERR_UnsupportedType1, Me)))
            End If

            Debug.Assert(Not _mangleName OrElse _name.Length < name.Length)
        End Sub

        Friend ReadOnly Property ContainingPEModule As PEModuleSymbol
            Get
                Dim s As Symbol = _container

                While s.Kind <> SymbolKind.Namespace
                    s = s.ContainingSymbol
                End While

                Return DirectCast(s, PENamespaceSymbol).ContainingPEModule
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingModule As ModuleSymbol
            Get
                Return ContainingPEModule
            End Get
        End Property

        Public Overrides ReadOnly Property Arity As Integer
            Get
                Return _arity
            End Get
        End Property

        Friend Overrides ReadOnly Property MangleName As Boolean
            Get
                Return _mangleName
            End Get
        End Property

        Friend Overrides ReadOnly Property Layout As TypeLayout
            Get
                Return Me.ContainingPEModule.Module.GetTypeLayout(_handle)
            End Get
        End Property

        Friend Overrides ReadOnly Property MarshallingCharSet As CharSet
            Get
                Dim result As CharSet = _flags.ToCharSet()
                If result = 0 Then
                    Return CharSet.Ansi
                End If

                Return result
            End Get
        End Property

        Public Overrides ReadOnly Property IsSerializable As Boolean
            Get
#Disable Warning SYSLIB0050 ' 'TypeAttributes.Serializable' is obsolete
                Return (_flags And TypeAttributes.Serializable) <> 0
#Enable Warning SYSLIB0050
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return (_flags And TypeAttributes.SpecialName) <> 0
            End Get
        End Property

        Friend ReadOnly Property MetadataArity As Integer
            Get
                Return _genericParameterHandles.Count
            End Get
        End Property

        Friend ReadOnly Property Handle As TypeDefinitionHandle
            Get
                Return _handle
            End Get
        End Property

        Public Overrides ReadOnly Property MetadataToken As Integer
            Get
                Return MetadataTokens.GetToken(_handle)
            End Get
        End Property

        Friend Overrides Function GetInterfacesToEmit() As IEnumerable(Of NamedTypeSymbol)
            Return InterfacesNoUseSiteDiagnostics
        End Function

        Friend Overrides Function MakeDeclaredBase(basesBeingResolved As BasesBeingResolved, diagnostics As BindingDiagnosticBag) As NamedTypeSymbol
            If (Me._flags And TypeAttributes.Interface) = 0 Then
                Dim moduleSymbol As PEModuleSymbol = Me.ContainingPEModule

                Try
                    Dim token As EntityHandle = moduleSymbol.Module.GetBaseTypeOfTypeOrThrow(Me._handle)
                    If Not token.IsNil Then
                        Dim decodedType = New MetadataDecoder(moduleSymbol, Me).GetTypeOfToken(token)
                        Return DirectCast(TupleTypeDecoder.DecodeTupleTypesIfApplicable(decodedType, _handle, moduleSymbol), NamedTypeSymbol)

                    End If
                Catch mrEx As BadImageFormatException
                    Return New UnsupportedMetadataTypeSymbol(mrEx)
                End Try
            End If
            Return Nothing
        End Function

        Friend Overrides Function MakeDeclaredInterfaces(basesBeingResolved As BasesBeingResolved, diagnostics As BindingDiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            Try
                Dim moduleSymbol As PEModuleSymbol = Me.ContainingPEModule
                Dim interfaceImpls = moduleSymbol.Module.GetInterfaceImplementationsOrThrow(Me._handle)

                If interfaceImpls.Count = 0 Then
                    Return ImmutableArray(Of NamedTypeSymbol).Empty
                End If

                Dim symbols As NamedTypeSymbol() = New NamedTypeSymbol(interfaceImpls.Count - 1) {}
                Dim tokenDecoder As New MetadataDecoder(moduleSymbol, Me)
                Dim i = 0
                For Each interfaceImpl In interfaceImpls
                    Dim interfaceHandle As EntityHandle = moduleSymbol.Module.MetadataReader.GetInterfaceImplementation(interfaceImpl).Interface
                    Dim typeSymbol As TypeSymbol = tokenDecoder.GetTypeOfToken(interfaceHandle)

                    typeSymbol = DirectCast(TupleTypeDecoder.DecodeTupleTypesIfApplicable(typeSymbol, interfaceImpl, moduleSymbol), NamedTypeSymbol)

                    'TODO: how to pass reason to unsupported
                    Dim namedTypeSymbol As NamedTypeSymbol = TryCast(typeSymbol, NamedTypeSymbol)
                    symbols(i) = If(namedTypeSymbol IsNot Nothing, namedTypeSymbol, New UnsupportedMetadataTypeSymbol()) ' "interface tmpList contains a bad type"
                    i = i + 1
                Next

                Return symbols.AsImmutableOrNull

            Catch mrEx As BadImageFormatException
                Return ImmutableArray.Create(Of NamedTypeSymbol)(New UnsupportedMetadataTypeSymbol(mrEx))
            End Try
        End Function

        Private Shared Function CyclicInheritanceError(diag As DiagnosticInfo) As ErrorTypeSymbol
            Return New ExtendedErrorTypeSymbol(diag, True)
        End Function

        Friend Overrides Function MakeAcyclicBaseType(diagnostics As BindingDiagnosticBag) As NamedTypeSymbol
            Dim diag = BaseTypeAnalysis.GetDependencyDiagnosticsForImportedClass(Me)
            If diag IsNot Nothing Then
                Return CyclicInheritanceError(diag)
            End If

            Return GetDeclaredBase(Nothing)
        End Function

        Friend Overrides Function MakeAcyclicInterfaces(diagnostics As BindingDiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            Dim declaredInterfaces As ImmutableArray(Of NamedTypeSymbol) = GetDeclaredInterfacesNoUseSiteDiagnostics(Nothing)
            If (Not Me.IsInterface) Then
                ' only interfaces needs to check for inheritance cycles via interfaces.
                Return declaredInterfaces
            End If

            Return (From t In declaredInterfaces
                    Let diag = BaseTypeAnalysis.GetDependencyDiagnosticsForImportedBaseInterface(Me, t)
                    Select If(diag Is Nothing, t, CyclicInheritanceError(diag))).AsImmutable

        End Function

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return _container
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return TryCast(_container, NamedTypeSymbol)
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Dim access As Accessibility = Accessibility.Private

                Select Case _flags And TypeAttributes.VisibilityMask
                    Case TypeAttributes.NestedAssembly
                        access = Accessibility.Friend

                    Case TypeAttributes.NestedFamORAssem
                        access = Accessibility.ProtectedOrFriend

                    Case TypeAttributes.NestedFamANDAssem
                        access = Accessibility.ProtectedAndFriend

                    Case TypeAttributes.NestedPrivate
                        access = Accessibility.Private

                    Case TypeAttributes.Public,
                         TypeAttributes.NestedPublic
                        access = Accessibility.Public

                    Case TypeAttributes.NestedFamily
                        access = Accessibility.Protected

                    Case TypeAttributes.NotPublic
                        access = Accessibility.Friend

                    Case Else
                        Debug.Assert(False, "Unexpected!!!")
                End Select

                Return access
            End Get
        End Property

        Public Overrides ReadOnly Property EnumUnderlyingType As NamedTypeSymbol
            Get
                If _lazyEnumUnderlyingType Is Nothing AndAlso TypeKind = TypeKind.Enum Then
                    ' From §8.5.2
                    ' An enum is considerably more restricted than a true type, as
                    ' follows:
                    ' • It shall have exactly one instance field, and the type of that field defines the underlying type of
                    ' the enumeration.
                    ' • It shall not have any static fields unless they are literal. (see §8.6.1.2)

                    ' The underlying type shall be a built-in integer type. Enums shall derive from System.Enum, hence they are
                    ' value types. Like all value types, they shall be sealed (see §8.9.9).

                    Dim underlyingType As NamedTypeSymbol = Nothing
                    For Each member In GetMembers()
                        If (Not member.IsShared AndAlso member.Kind = SymbolKind.Field) Then
                            Dim type = DirectCast(member, FieldSymbol).Type

                            If (type.SpecialType.IsClrInteger()) Then
                                If (underlyingType Is Nothing) Then
                                    underlyingType = DirectCast(type, NamedTypeSymbol)
                                Else
                                    underlyingType = New UnsupportedMetadataTypeSymbol()
                                    Exit For
                                End If
                            End If
                        End If
                    Next

                    Interlocked.CompareExchange(_lazyEnumUnderlyingType,
                        If(underlyingType, New UnsupportedMetadataTypeSymbol()),
                        Nothing)
                End If

                Return _lazyEnumUnderlyingType
            End Get
        End Property

        Public Overloads Overrides Function GetAttributes() As ImmutableArray(Of VisualBasicAttributeData)
            If _lazyCustomAttributes.IsDefault Then
                If (_lazyTypeKind = TypeKind.Unknown AndAlso
                    ((_flags And TypeAttributes.Interface) <> 0 OrElse Me.Arity <> 0 OrElse Me.ContainingType IsNot Nothing)) OrElse
                   Me.TypeKind <> TypeKind.Module Then
                    ContainingPEModule.LoadCustomAttributes(_handle, _lazyCustomAttributes)
                Else
                    Dim stdModuleAttribute As CustomAttributeHandle
                    Dim attributes = ContainingPEModule.GetCustomAttributesForToken(
                        _handle,
                        stdModuleAttribute,
                        filterOut1:=AttributeDescription.StandardModuleAttribute)

                    Debug.Assert(Not stdModuleAttribute.IsNil)
                    ImmutableInterlocked.InterlockedInitialize(_lazyCustomAttributes, attributes)
                End If
            End If

            Return _lazyCustomAttributes
        End Function

        Friend Overrides Iterator Function GetCustomAttributesToEmit(moduleBuilder As PEModuleBuilder) As IEnumerable(Of VisualBasicAttributeData)
            For Each attribute In GetAttributes()
                Yield attribute
            Next

            If Me.TypeKind = TypeKind.Module Then
                Yield New PEAttributeData(ContainingPEModule,
                                          ContainingPEModule.Module.GetAttributeHandle(Me._handle, AttributeDescription.StandardModuleAttribute))
            End If
        End Function

        Public Overrides ReadOnly Property MemberNames As IEnumerable(Of String)
            Get
                EnsureNonTypeMemberNamesAreLoaded()
                Return _lazyMemberNames
            End Get
        End Property

        Private Sub EnsureNonTypeMemberNamesAreLoaded()
            If _lazyMemberNames Is Nothing Then

                Dim peModule = ContainingPEModule.Module
                Dim names = New HashSet(Of String)()

                Try
                    For Each methodDef In peModule.GetMethodsOfTypeOrThrow(_handle)
                        Try
                            names.Add(peModule.GetMethodDefNameOrThrow(methodDef))
                        Catch mrEx As BadImageFormatException
                        End Try
                    Next
                Catch mrEx As BadImageFormatException
                End Try

                Try
                    For Each propertyDef In peModule.GetPropertiesOfTypeOrThrow(_handle)
                        Try
                            names.Add(peModule.GetPropertyDefNameOrThrow(propertyDef))
                        Catch mrEx As BadImageFormatException
                        End Try
                    Next
                Catch mrEx As BadImageFormatException
                End Try

                Try
                    For Each eventDef In peModule.GetEventsOfTypeOrThrow(_handle)
                        Try
                            names.Add(peModule.GetEventDefNameOrThrow(eventDef))
                        Catch mrEx As BadImageFormatException
                        End Try
                    Next
                Catch mrEx As BadImageFormatException
                End Try

                Try
                    For Each fieldDef In peModule.GetFieldsOfTypeOrThrow(_handle)
                        Try
                            names.Add(peModule.GetFieldDefNameOrThrow(fieldDef))
                        Catch mrEx As BadImageFormatException
                        End Try
                    Next
                Catch mrEx As BadImageFormatException
                End Try

                Interlocked.CompareExchange(Of ICollection(Of String))(
                    _lazyMemberNames,
                    SpecializedCollections.ReadOnlySet(names),
                    Nothing)
            End If
        End Sub

        Public Overloads Overrides Function GetMembers() As ImmutableArray(Of Symbol)
            EnsureNestedTypesAreLoaded()
            EnsureNonTypeMembersAreLoaded()

            Return _lazyMembers.Flatten(DeclarationOrderSymbolComparer.Instance)
        End Function

        Friend Overrides Function GetMembersUnordered() As ImmutableArray(Of Symbol)
            EnsureNestedTypesAreLoaded()
            EnsureNonTypeMembersAreLoaded()

            Return _lazyMembers.Flatten().ConditionallyDeOrder()
        End Function

        Friend Overrides Function GetFieldsToEmit() As IEnumerable(Of FieldSymbol)
            ' If there are any fields, they are at the very beginning.
            Return GetMembers(Of FieldSymbol)(GetMembers(), SymbolKind.Field, offset:=0)
        End Function

        Friend Overrides Iterator Function GetMethodsToEmit() As IEnumerable(Of MethodSymbol)
            Dim members = GetMembers()

            ' Get to methods.
            Dim index = GetIndexOfFirstMember(members, SymbolKind.Method)

            If Not Me.IsInterfaceType() Then
                While index < members.Length
                    Dim member = members(index)
                    If member.Kind <> SymbolKind.Method Then
                        Exit While
                    End If

                    Dim method = DirectCast(member, MethodSymbol)

                    ' Don't emit the default value type constructor - the runtime handles that
                    If Not method.IsDefaultValueTypeConstructor() Then
                        Yield method
                    End If

                    index += 1
                End While

            Else
                ' We do not create symbols for v-table gap methods, let's figure out where the gaps go.
                If index >= members.Length OrElse members(index).Kind <> SymbolKind.Method Then
                    ' We didn't import any methods, it is Ok to return an empty set.
                    Return
                End If

                Dim method = DirectCast(members(index), PEMethodSymbol)
                Dim [module] = ContainingPEModule.Module
                Dim methodDefs = ArrayBuilder(Of MethodDefinitionHandle).GetInstance()

                Try
                    For Each methodDef In [module].GetMethodsOfTypeOrThrow(_handle)
                        methodDefs.Add(methodDef)
                    Next
                Catch mrEx As BadImageFormatException
                End Try

                For Each methodDef In methodDefs
                    If method.Handle = methodDef Then
                        Yield method
                        index += 1

                        If index = members.Length OrElse members(index).Kind <> SymbolKind.Method Then
                            ' no need to return any gaps at the end.
                            methodDefs.Free()
                            Return
                        End If

                        method = DirectCast(members(index), PEMethodSymbol)

                    Else
                        ' Encountered a gap.
                        Dim gapSize As Integer

                        Try
                            gapSize = ModuleExtensions.GetVTableGapSize([module].GetMethodDefNameOrThrow(methodDef))
                        Catch mrEx As BadImageFormatException
                            gapSize = 1
                        End Try

                        ' We don't have a symbol to return, so, even if the name doesn't represent a gap, we still have a gap.
                        Do
                            Yield Nothing
                            gapSize -= 1
                        Loop While gapSize > 0
                    End If
                Next

                ' Ensure we explicitly returned from inside loop.
                Throw ExceptionUtilities.Unreachable
            End If
        End Function

        Friend Overrides Function GetPropertiesToEmit() As IEnumerable(Of PropertySymbol)
            Return GetMembers(Of PropertySymbol)(GetMembers(), SymbolKind.Property)
        End Function

        Friend Overrides Function GetEventsToEmit() As IEnumerable(Of EventSymbol)
            Return GetMembers(Of EventSymbol)(GetMembers(), SymbolKind.Event)
        End Function

        Private Class DeclarationOrderSymbolComparer
            Implements IComparer(Of ISymbol)

            Public Shared ReadOnly Instance As New DeclarationOrderSymbolComparer()

            Private Sub New()
            End Sub

            Public Function Compare(x As ISymbol, y As ISymbol) As Integer Implements IComparer(Of ISymbol).Compare
                If x Is y Then
                    Return 0
                End If

                Dim cmp As Integer = x.Kind.ToSortOrder - y.Kind.ToSortOrder

                If cmp <> 0 Then
                    Return cmp
                End If

                Select Case x.Kind
                    Case SymbolKind.Field
                        Return HandleComparer.Default.Compare(DirectCast(x, PEFieldSymbol).Handle, DirectCast(y, PEFieldSymbol).Handle)
                    Case SymbolKind.Method
                        If DirectCast(x, MethodSymbol).IsDefaultValueTypeConstructor() Then
                            Return -1
                        ElseIf DirectCast(y, MethodSymbol).IsDefaultValueTypeConstructor() Then
                            Return 1
                        End If

                        Return HandleComparer.Default.Compare(DirectCast(x, PEMethodSymbol).Handle, DirectCast(y, PEMethodSymbol).Handle)
                    Case SymbolKind.Property
                        Return HandleComparer.Default.Compare(DirectCast(x, PEPropertySymbol).Handle, DirectCast(y, PEPropertySymbol).Handle)
                    Case SymbolKind.Event
                        Return HandleComparer.Default.Compare(DirectCast(x, PEEventSymbol).Handle, DirectCast(y, PEEventSymbol).Handle)
                    Case SymbolKind.NamedType
                        Return HandleComparer.Default.Compare(DirectCast(x, PENamedTypeSymbol).Handle, DirectCast(y, PENamedTypeSymbol).Handle)
                    Case Else
                        Throw ExceptionUtilities.UnexpectedValue(x.Kind)
                End Select
            End Function
        End Class

        Private Sub EnsureNonTypeMembersAreLoaded()

            If Volatile.Read(_lazyMembers) Is Nothing Then
                ' A method may be referenced as an accessor by one or more properties. And,
                ' any of those properties may be "bogus" if one of the property accessors
                ' does not match the property signature. If the method is referenced by at
                ' least one non-bogus property, then the method is created as an accessor,
                ' and (for purposes of error reporting if the method is referenced directly) the
                ' associated property is set (arbitrarily) to the first non-bogus property found
                ' in metadata. If the method is not referenced by any non-bogus properties,
                ' then the method is created as a normal method rather than an accessor.
                ' Create a dictionary of method symbols indexed by metadata row id
                ' (to allow efficient lookup when matching property accessors).
                Dim methodHandleToSymbol As Dictionary(Of MethodDefinitionHandle, PEMethodSymbol) = CreateMethods()
                Dim members = ArrayBuilder(Of Symbol).GetInstance()

                Dim ensureParameterlessConstructor As Boolean = (TypeKind = TypeKind.Structure OrElse TypeKind = TypeKind.Enum) AndAlso Not IsShared

                For Each member In methodHandleToSymbol.Values
                    members.Add(member)

                    If ensureParameterlessConstructor Then
                        ensureParameterlessConstructor = Not member.IsParameterlessConstructor()
                    End If
                Next

                If ensureParameterlessConstructor Then
                    members.Add(New SynthesizedConstructorSymbol(Nothing, Me, Me.IsShared, False, Nothing, Nothing))
                End If

                ' CreateFields will add withEvent names here if there are any.
                ' Otherwise stays Nothing
                Dim withEventNames As HashSet(Of String) = Nothing

                CreateProperties(methodHandleToSymbol, members)
                CreateFields(members, withEventNames)
                CreateEvents(methodHandleToSymbol, members)

                Dim membersDict As New Dictionary(Of String, ImmutableArray(Of Symbol))(CaseInsensitiveComparison.Comparer)
                Dim groupedMembers = members.GroupBy(Function(m) m.Name, CaseInsensitiveComparison.Comparer)

                For Each g In groupedMembers
                    membersDict.Add(g.Key, ImmutableArray.CreateRange(g))
                Next

                members.Free()

                ' tell WithEvents properties that they are WithEvents properties
                If withEventNames IsNot Nothing Then
                    For Each withEventName In withEventNames
                        Dim weMembers As ImmutableArray(Of Symbol) = Nothing
                        If membersDict.TryGetValue(withEventName, weMembers) Then
                            ' there must be only a single match for a given WithEvents name
                            If weMembers.Length <> 1 Then
                                Continue For
                            End If

                            ' it must be a valid property
                            Dim asProperty = TryCast(weMembers(0), PEPropertySymbol)
                            If asProperty IsNot Nothing AndAlso IsValidWithEventsProperty(asProperty) Then
                                asProperty.SetIsWithEvents(True)
                            End If
                        End If
                    Next
                End If

                ' Merge types into members
                For Each typeSymbols In _lazyNestedTypes.Values
                    Dim name = typeSymbols(0).Name

                    Dim symbols As ImmutableArray(Of Symbol) = Nothing
                    If Not membersDict.TryGetValue(name, symbols) Then
                        membersDict.Add(name, StaticCast(Of Symbol).From(typeSymbols))
                    Else
                        membersDict(name) = symbols.Concat(StaticCast(Of Symbol).From(typeSymbols))
                    End If
                Next

                Dim exchangeResult = Interlocked.CompareExchange(_lazyMembers, membersDict, Nothing)

                If exchangeResult Is Nothing Then
                    Dim memberNames = SpecializedCollections.ReadOnlyCollection(membersDict.Keys)
                    Interlocked.Exchange(Of ICollection(Of String))(_lazyMemberNames, memberNames)
                End If
            End If
        End Sub

        ''' <summary>
        ''' Some simple sanity checks if a property can actually be a withevents property
        ''' </summary>
        Private Function IsValidWithEventsProperty(prop As PEPropertySymbol) As Boolean
            ' NOTE: Dev10 does not make any checks. Just has comment that it could be a good idea to do in Whidbey.
            ' We will check, just to make stuff a bit more robust.
            ' It will be extremely rare that this function would fail though.

            If prop.IsReadOnly Or prop.IsWriteOnly Then
                Return False
            End If

            If Not prop.IsOverridable Then
                Return False
            End If

            Return True
        End Function

        Public Overloads Overrides Function GetMembers(name As String) As ImmutableArray(Of Symbol)
            EnsureNestedTypesAreLoaded()
            EnsureNonTypeMembersAreLoaded()

            Dim m As ImmutableArray(Of Symbol) = Nothing

            If _lazyMembers.TryGetValue(name, m) Then
                Return m
            End If

            Return ImmutableArray(Of Symbol).Empty
        End Function

        Friend Overrides Function GetTypeMembersUnordered() As ImmutableArray(Of NamedTypeSymbol)
            EnsureNestedTypesAreLoaded()

            Return StaticCast(Of NamedTypeSymbol).From(_lazyNestedTypes.Flatten())
        End Function

        Public Overloads Overrides Function GetTypeMembers() As ImmutableArray(Of NamedTypeSymbol)
            EnsureNestedTypesAreLoaded()

            Return StaticCast(Of NamedTypeSymbol).From(_lazyNestedTypes.Flatten(DeclarationOrderSymbolComparer.Instance))
        End Function

        Private Sub EnsureNestedTypesAreLoaded()

            If _lazyNestedTypes Is Nothing Then

                Dim types = ArrayBuilder(Of PENamedTypeSymbol).GetInstance()
                CreateNestedTypes(types)
                Dim typesDict = GroupByName(types)

                Interlocked.CompareExchange(_lazyNestedTypes, typesDict, Nothing)

                If _lazyNestedTypes Is typesDict Then
                    ' Build cache of TypeDef Tokens
                    ' Potentially this can be done in the background.
                    ContainingPEModule.OnNewTypeDeclarationsLoaded(typesDict)
                End If

                types.Free()
            End If

        End Sub

        Public Overloads Overrides Function GetTypeMembers(name As String) As ImmutableArray(Of NamedTypeSymbol)
            EnsureNestedTypesAreLoaded()

            Dim t As ImmutableArray(Of PENamedTypeSymbol) = Nothing

            If _lazyNestedTypes.TryGetValue(name, t) Then
                Return StaticCast(Of NamedTypeSymbol).From(t)
            End If

            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Public Overloads Overrides Function GetTypeMembers(name As String, arity As Integer) As ImmutableArray(Of NamedTypeSymbol)
            Return GetTypeMembers(name).WhereAsArray(Function(type, arity_) type.Arity = arity_, arity)
        End Function

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return StaticCast(Of Location).From(ContainingPEModule.MetadataLocation)
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return ImmutableArray(Of SyntaxReference).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return _name
            End Get
        End Property

        Friend ReadOnly Property TypeDefFlags As TypeAttributes
            Get
                Return _flags
            End Get
        End Property

        Public Overrides ReadOnly Property TypeParameters As ImmutableArray(Of TypeParameterSymbol)
            Get
                EnsureTypeParametersAreLoaded()
                Return _lazyTypeParameters
            End Get
        End Property

        Private Sub EnsureTypeParametersAreLoaded()

            If _lazyTypeParameters.IsDefault Then

                Debug.Assert(_arity > 0)

                Dim ownedParams(_arity - 1) As PETypeParameterSymbol

                Dim moduleSymbol = ContainingPEModule

                ' If this is a nested type generic parameters in metadata include generic parameters of the outer types.
                Dim firstIndex = _genericParameterHandles.Count - Arity

                For i = 0 To ownedParams.Length - 1
                    ownedParams(i) = New PETypeParameterSymbol(moduleSymbol, Me, CUShort(i), _genericParameterHandles(firstIndex + i))
                Next

                ImmutableInterlocked.InterlockedCompareExchange(_lazyTypeParameters,
                                            StaticCast(Of TypeParameterSymbol).From(ownedParams.AsImmutableOrNull),
                                            Nothing)
            End If

        End Sub

        Public Overrides ReadOnly Property IsMustInherit As Boolean
            Get
                Return (_flags And TypeAttributes.Abstract) <> 0 AndAlso
                    (_flags And TypeAttributes.Sealed) = 0
            End Get
        End Property

        Friend Overrides ReadOnly Property IsMetadataAbstract As Boolean
            Get
                Return (_flags And TypeAttributes.Abstract) <> 0
            End Get
        End Property

        Public Overrides ReadOnly Property IsNotInheritable As Boolean
            Get
                Return (_flags And TypeAttributes.Sealed) <> 0
            End Get
        End Property

        Public Overrides ReadOnly Property IsReadOnly As Boolean
            Get
                If Not _lazyIsReadOnly.HasValue() Then
                    Dim hasAttribute = DirectCast(ContainingModule, PEModuleSymbol).Module.HasIsReadOnlyAttribute(_handle)
                    _lazyIsReadOnly = hasAttribute.ToThreeState()
                End If

                Return _lazyIsReadOnly.Value()
            End Get
        End Property

        Public Overrides ReadOnly Property IsRefLikeType As Boolean
            Get
                If Not _lazyIsRefLike.HasValue() Then
                    Dim hasAttribute = DirectCast(ContainingModule, PEModuleSymbol).Module.HasIsByRefLikeAttribute(_handle)
                    _lazyIsRefLike = hasAttribute.ToThreeState()
                End If

                Return _lazyIsRefLike.Value()
            End Get
        End Property

        Friend Overrides ReadOnly Property IsMetadataSealed As Boolean
            Get
                Return (_flags And TypeAttributes.Sealed) <> 0
            End Get
        End Property

        Friend Overrides ReadOnly Property IsWindowsRuntimeImport As Boolean
            Get
                Return (_flags And TypeAttributes.WindowsRuntime) <> 0
            End Get
        End Property

        Friend Overrides ReadOnly Property ShouldAddWinRTMembers As Boolean
            Get
                Return IsWindowsRuntimeImport
            End Get
        End Property

        Friend Overrides Function GetGuidString(ByRef guidString As String) As Boolean
            Return ContainingPEModule.Module.HasGuidAttribute(_handle, guidString)
        End Function

        Public NotOverridable Overrides ReadOnly Property MightContainExtensionMethods As Boolean
            Get
                If _lazyMightContainExtensionMethods = ThreeState.Unknown Then

                    ' Only top level non-generic types with an Extension attribute are
                    ' valid containers of extension methods.
                    Dim result As Boolean = False

                    If _container.Kind = SymbolKind.Namespace AndAlso _arity = 0 Then
                        Dim containingModuleSymbol = Me.ContainingPEModule

                        If containingModuleSymbol.MightContainExtensionMethods AndAlso
                           containingModuleSymbol.Module.HasExtensionAttribute(Me._handle, ignoreCase:=True) Then
                            result = True
                        End If
                    End If

                    If result Then
                        _lazyMightContainExtensionMethods = ThreeState.True
                    Else
                        _lazyMightContainExtensionMethods = ThreeState.False
                    End If
                End If

                Return _lazyMightContainExtensionMethods = ThreeState.True
            End Get
        End Property

        Friend Overrides ReadOnly Property HasCodeAnalysisEmbeddedAttribute As Boolean
            Get
                If Me._lazyHasCodeAnalysisEmbeddedAttribute = ThreeState.Unknown Then
                    Interlocked.CompareExchange(
                        Me._lazyHasCodeAnalysisEmbeddedAttribute,
                        Me.ContainingPEModule.Module.HasCodeAnalysisEmbeddedAttribute(Me._handle).ToThreeState(),
                        ThreeState.Unknown)
                End If
                Return Me._lazyHasCodeAnalysisEmbeddedAttribute = ThreeState.True
            End Get
        End Property

        Friend Overrides ReadOnly Property HasVisualBasicEmbeddedAttribute As Boolean
            Get
                If Me._lazyHasVisualBasicEmbeddedAttribute = ThreeState.Unknown Then
                    Interlocked.CompareExchange(
                        Me._lazyHasVisualBasicEmbeddedAttribute,
                        Me.ContainingPEModule.Module.HasVisualBasicEmbeddedAttribute(Me._handle).ToThreeState(),
                        ThreeState.Unknown)
                End If
                Return Me._lazyHasVisualBasicEmbeddedAttribute = ThreeState.True
            End Get
        End Property

        Friend Overrides ReadOnly Property HasCompilerLoweringPreserveAttribute As Boolean
            Get
                If Me._lazyHasCompilerLoweringPreserveAttribute = ThreeState.Unknown Then
                    Interlocked.CompareExchange(
                        Me._lazyHasCompilerLoweringPreserveAttribute,
                        Me.ContainingPEModule.Module.HasCompilerLoweringPreserveAttribute(Me._handle).ToThreeState(),
                        ThreeState.Unknown)
                End If
                Return Me._lazyHasCompilerLoweringPreserveAttribute = ThreeState.True
            End Get
        End Property

        Friend Overrides Sub BuildExtensionMethodsMap(
            map As Dictionary(Of String, ArrayBuilder(Of MethodSymbol)),
            appendThrough As NamespaceSymbol
        )
            If Me.MightContainExtensionMethods Then
                EnsureNestedTypesAreLoaded()
                EnsureNonTypeMembersAreLoaded()

                If Not appendThrough.BuildExtensionMethodsMap(map, _lazyMembers) Then
                    ' Didn't find any extension methods, record the fact.
                    _lazyMightContainExtensionMethods = ThreeState.False
                End If
            End If
        End Sub

        Friend Overrides Sub AddExtensionMethodLookupSymbolsInfo(nameSet As LookupSymbolsInfo,
                                                                  options As LookupOptions,
                                                                  originalBinder As Binder,
                                                                  appendThrough As NamedTypeSymbol)
            If Me.MightContainExtensionMethods Then
                EnsureNestedTypesAreLoaded()
                EnsureNonTypeMembersAreLoaded()

                If Not appendThrough.AddExtensionMethodLookupSymbolsInfo(nameSet, options, originalBinder, _lazyMembers) Then
                    ' Didn't find any extension methods, record the fact.
                    _lazyMightContainExtensionMethods = ThreeState.False
                End If
            End If
        End Sub

        Public Overrides ReadOnly Property TypeKind As TypeKind
            Get
                If _lazyTypeKind = TypeKind.Unknown Then

                    Dim result As TypeKind

                    If (_flags And TypeAttributes.Interface) <> 0 Then
                        result = TypeKind.Interface
                    Else
                        Dim base As TypeSymbol = GetDeclaredBase(Nothing)

                        result = TypeKind.Class

                        If base IsNot Nothing Then
                            ' Code is cloned from MetaImport::DoImportBaseAndImplements()
                            Dim baseCorTypeId As SpecialType = base.SpecialType

                            If baseCorTypeId = SpecialType.System_Enum Then
                                ' Enum
                                result = TypeKind.Enum
                            ElseIf baseCorTypeId = SpecialType.System_MulticastDelegate OrElse
                                   (baseCorTypeId = SpecialType.System_Delegate AndAlso Me.SpecialType <> SpecialType.System_MulticastDelegate) Then
                                ' Delegate
                                result = TypeKind.Delegate
                            ElseIf (baseCorTypeId = SpecialType.System_ValueType AndAlso
                                     Me.SpecialType <> SpecialType.System_Enum) Then
                                ' Struct
                                result = TypeKind.Structure
                            ElseIf Me.Arity = 0 AndAlso
                                Me.ContainingType Is Nothing AndAlso
                                ContainingPEModule.Module.HasAttribute(Me._handle, AttributeDescription.StandardModuleAttribute) Then
                                result = TypeKind.Module
                            End If
                        End If
                    End If

                    _lazyTypeKind = result
                End If

                Return CType(_lazyTypeKind, TypeKind)
            End Get
        End Property

        Friend Overrides ReadOnly Property IsInterface As Boolean
            Get
                Return (_flags And TypeAttributes.Interface) <> 0
            End Get
        End Property

        Public Overrides Function GetDocumentationCommentXml(Optional preferredCulture As CultureInfo = Nothing, Optional expandIncludes As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As String
            ' Note: m_lazyDocComment is passed ByRef
            Return PEDocumentationCommentUtils.GetDocumentationComment(
                Me, ContainingPEModule, preferredCulture, cancellationToken, _lazyDocComment)
        End Function

        Friend Overrides ReadOnly Property IsComImport As Boolean
            Get
                Return (_flags And TypeAttributes.Import) <> 0
            End Get
        End Property

        Friend Overrides ReadOnly Property CoClassType As TypeSymbol
            Get
                If _lazyCoClassType Is ErrorTypeSymbol.UnknownResultType Then
                    Interlocked.CompareExchange(_lazyCoClassType,
                                                MakeComImportCoClassType(),
                                                DirectCast(ErrorTypeSymbol.UnknownResultType, TypeSymbol))
                End If

                Return _lazyCoClassType
            End Get
        End Property

        Private Function MakeComImportCoClassType() As TypeSymbol
            If Not Me.IsInterface Then
                Return Nothing
            End If

            Dim coClassTypeName As String = Nothing
            If Not Me.ContainingPEModule.Module.HasStringValuedAttribute(Me._handle, AttributeDescription.CoClassAttribute, coClassTypeName) Then
                Return Nothing
            End If

            Dim decoder As New MetadataDecoder(Me.ContainingPEModule)
            Return decoder.GetTypeSymbolForSerializedType(coClassTypeName)
        End Function

        Friend Overrides ReadOnly Property DefaultPropertyName As String
            Get
                ' Unset value is Nothing. No default member is String.Empty.
                If _lazyDefaultPropertyName Is Nothing Then
                    Dim memberName = GetDefaultPropertyName()
                    Interlocked.CompareExchange(_lazyDefaultPropertyName, If(memberName, String.Empty), Nothing)
                End If

                ' Return Nothing rather than String.Empty for no default member.
                Return If(String.IsNullOrEmpty(_lazyDefaultPropertyName), Nothing, _lazyDefaultPropertyName)
            End Get
        End Property

        Private Function GetDefaultPropertyName() As String
            Dim memberName As String = Nothing
            ContainingPEModule.Module.HasDefaultMemberAttribute(Me._handle, memberName)

            If memberName IsNot Nothing Then
                For Each member In GetMembers(memberName)
                    ' Allow Default Shared properties for consistency with Dev10.
                    If member.Kind = SymbolKind.Property Then
                        Return memberName
                    End If
                Next
            End If

            Return Nothing
        End Function

        Private Sub CreateNestedTypes(members As ArrayBuilder(Of PENamedTypeSymbol))
            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each nestedTypeDef In [module].GetNestedTypeDefsOrThrow(_handle)
                    members.Add(New PENamedTypeSymbol(moduleSymbol, Me, nestedTypeDef))
                Next
            Catch mrEx As BadImageFormatException
            End Try
        End Sub

        Private Sub CreateFields(members As ArrayBuilder(Of Symbol),
                                 <Out()> ByRef witheventPropertyNames As HashSet(Of String))

            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each fieldDef In [module].GetFieldsOfTypeOrThrow(_handle)
                    Dim import As Boolean

                    Try
                        import = [module].ShouldImportField(fieldDef, moduleSymbol.ImportOptions)

                        If Not import Then
                            Select Case Me.TypeKind
                                Case TypeKind.Structure
                                    Select Case Me.SpecialType
                                        Case SpecialType.System_Void,
                                             SpecialType.System_Boolean,
                                             SpecialType.System_Char,
                                             SpecialType.System_Byte,
                                             SpecialType.System_SByte,
                                             SpecialType.System_Int16,
                                             SpecialType.System_UInt16,
                                             SpecialType.System_Int32,
                                             SpecialType.System_UInt32,
                                             SpecialType.System_Int64,
                                             SpecialType.System_UInt64,
                                             SpecialType.System_Single,
                                             SpecialType.System_Double,
                                             SpecialType.System_Decimal,
                                             SpecialType.System_IntPtr,
                                             SpecialType.System_UIntPtr,
                                             SpecialType.System_DateTime,
                                             SpecialType.System_TypedReference,
                                             SpecialType.System_ArgIterator,
                                             SpecialType.System_RuntimeArgumentHandle,
                                             SpecialType.System_RuntimeFieldHandle,
                                             SpecialType.System_RuntimeMethodHandle,
                                             SpecialType.System_RuntimeTypeHandle
                                        Case Else
                                            ' This is an ordinary struct
                                            If ([module].GetFieldDefFlagsOrThrow(fieldDef) And FieldAttributes.Static) = 0 Then
                                                import = True
                                            End If
                                    End Select

                                Case TypeKind.Enum
                                    If ([module].GetFieldDefFlagsOrThrow(fieldDef) And FieldAttributes.Static) = 0 Then
                                        import = True
                                    End If
                            End Select
                        End If
                    Catch mrEx As BadImageFormatException
                    End Try

                    If import Then
                        members.Add(New PEFieldSymbol(moduleSymbol, Me, fieldDef))
                    End If

                    Dim witheventPropertyName As String = Nothing
                    If [module].HasAccessedThroughPropertyAttribute(fieldDef, witheventPropertyName) Then
                        '       Dev10 does not check if names are duplicated, but it does check
                        '       that withevents names match some property name using identifier match
                        '       So if names are duplicated they would refer to same property.
                        '       We will just put names in a set.
                        If witheventPropertyNames Is Nothing Then
                            witheventPropertyNames = New HashSet(Of String)(IdentifierComparison.Comparer)
                        End If

                        witheventPropertyNames.Add(witheventPropertyName)
                    End If

                Next
            Catch mrEx As BadImageFormatException
            End Try
        End Sub

        Private Function CreateMethods() As Dictionary(Of MethodDefinitionHandle, PEMethodSymbol)
            Dim methods = New Dictionary(Of MethodDefinitionHandle, PEMethodSymbol)()
            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each methodDef In [module].GetMethodsOfTypeOrThrow(_handle)
                    If [module].ShouldImportMethod(_handle, methodDef, moduleSymbol.ImportOptions) Then
                        methods.Add(methodDef, New PEMethodSymbol(moduleSymbol, Me, methodDef))
                    End If
                Next
            Catch mrEx As BadImageFormatException
            End Try

            Return methods
        End Function

        Private Sub CreateProperties(methodHandleToSymbol As Dictionary(Of MethodDefinitionHandle, PEMethodSymbol), members As ArrayBuilder(Of Symbol))
            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each propertyDef In [module].GetPropertiesOfTypeOrThrow(_handle)
                    Try
                        Dim methods = [module].GetPropertyMethodsOrThrow(propertyDef)

                        Dim getMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, _handle, methods.Getter)
                        Dim setMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, _handle, methods.Setter)

                        If (getMethod IsNot Nothing) OrElse (setMethod IsNot Nothing) Then
                            members.Add(PEPropertySymbol.Create(moduleSymbol, Me, propertyDef, getMethod, setMethod))
                        End If
                    Catch mrEx As BadImageFormatException
                    End Try
                Next
            Catch mrEx As BadImageFormatException
            End Try
        End Sub

        Private Sub CreateEvents(methodHandleToSymbol As Dictionary(Of MethodDefinitionHandle, PEMethodSymbol), members As ArrayBuilder(Of Symbol))
            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each eventRid In [module].GetEventsOfTypeOrThrow(_handle)
                    Try
                        Dim methods = [module].GetEventMethodsOrThrow(eventRid)

                        ' NOTE: C# ignores all other accessors (most notably, raise/fire).
                        Dim addMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, _handle, methods.Adder)
                        Dim removeMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, _handle, methods.Remover)
                        Dim raiseMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, _handle, methods.Raiser)

                        ' VB ignores events that do not have both Add and Remove.
                        If (addMethod IsNot Nothing) AndAlso (removeMethod IsNot Nothing) Then
                            members.Add(New PEEventSymbol(moduleSymbol, Me, eventRid, addMethod, removeMethod, raiseMethod))
                        End If
                    Catch mrEx As BadImageFormatException
                    End Try
                Next
            Catch mrEx As BadImageFormatException
            End Try
        End Sub

        Private Shared Function GetAccessorMethod(moduleSymbol As PEModuleSymbol, methodHandleToSymbol As Dictionary(Of MethodDefinitionHandle, PEMethodSymbol), typeDef As TypeDefinitionHandle, methodDef As MethodDefinitionHandle) As PEMethodSymbol
            If methodDef.IsNil Then
                Return Nothing
            End If

            Dim method As PEMethodSymbol = Nothing
            Dim found As Boolean = methodHandleToSymbol.TryGetValue(methodDef, method)
            Debug.Assert(found OrElse Not moduleSymbol.Module.ShouldImportMethod(typeDef, methodDef, moduleSymbol.ImportOptions))
            Return method
        End Function

        Private Shared Function GroupByName(symbols As ArrayBuilder(Of PENamedTypeSymbol)) As Dictionary(Of String, ImmutableArray(Of PENamedTypeSymbol))
            If symbols.Count = 0 Then
                Return s_emptyNestedTypes
            End If

            Return symbols.ToDictionary(Function(s) s.Name, IdentifierComparison.Comparer)
        End Function

        Friend Overrides Function GetUseSiteInfo() As UseSiteInfo(Of AssemblySymbol)
            Dim primaryDependency As AssemblySymbol = Me.PrimaryDependency

            If Not _lazyCachedUseSiteInfo.IsInitialized Then
                _lazyCachedUseSiteInfo.Initialize(primaryDependency, CalculateUseSiteInfoImpl())
            End If

            Return _lazyCachedUseSiteInfo.ToUseSiteInfo(primaryDependency)
        End Function

        Private Function CalculateUseSiteInfoImpl() As UseSiteInfo(Of AssemblySymbol)
            ' GetCompilerFeatureRequiredDiagnostic depends on this being the highest priority use-site diagnostic. If another
            ' diagnostic was calculated first and cached, it will return incorrect results and assert in Debug mode.
            Dim compilerFeatureRequiredDiagnostic = DeriveCompilerFeatureRequiredDiagnostic()
            If compilerFeatureRequiredDiagnostic IsNot Nothing Then
                Return New UseSiteInfo(Of AssemblySymbol)(compilerFeatureRequiredDiagnostic)
            End If

            Dim useSiteInfo = CalculateUseSiteInfo()

            If useSiteInfo.DiagnosticInfo Is Nothing Then

                ' Check if this type Is marked by RequiredAttribute attribute.
                ' If so mark the type as bad, because it relies upon semantics that are not understood by the VB compiler.
                If Me.ContainingPEModule.Module.HasRequiredAttributeAttribute(Me.Handle) Then
                    Return New UseSiteInfo(Of AssemblySymbol)(ErrorFactory.ErrorInfo(ERRID.ERR_UnsupportedType1, Me))
                End If

                Dim typeKind = Me.TypeKind
                Dim specialtype = Me.SpecialType
                If (typeKind = TypeKind.Class OrElse typeKind = TypeKind.Module) AndAlso
                   specialtype <> SpecialType.System_Enum AndAlso specialtype <> SpecialType.System_MulticastDelegate Then
                    Dim base As TypeSymbol = GetDeclaredBase(Nothing)

                    If base IsNot Nothing AndAlso base.SpecialType = SpecialType.None AndAlso base.ContainingAssembly?.IsMissing Then
                        Dim missingType = TryCast(base, MissingMetadataTypeSymbol.TopLevel)

                        If missingType IsNot Nothing AndAlso missingType.Arity = 0 Then
                            Dim emittedName As String = MetadataHelpers.BuildQualifiedName(missingType.NamespaceName, missingType.MetadataName)

                            Select Case SpecialTypes.GetTypeFromMetadataName(emittedName)
                                Case SpecialType.System_Enum,
                                     SpecialType.System_Delegate,
                                     SpecialType.System_MulticastDelegate,
                                     SpecialType.System_ValueType
                                    ' This might be a structure, an enum, or a delegate
                                    Return missingType.GetUseSiteInfo()
                            End Select
                        End If
                    End If
                End If

                ' Verify type parameters for containing types
                ' match those on the containing types.
                If Not MatchesContainingTypeParameters() Then
                    Return New UseSiteInfo(Of AssemblySymbol)(ErrorFactory.ErrorInfo(ERRID.ERR_NestingViolatesCLS1, Me))
                End If
            End If

            Return useSiteInfo
        End Function

        Friend Function GetCompilerFeatureRequiredDiagnostic() As DiagnosticInfo
            Dim typeUseSiteInfo = GetUseSiteInfo()
            If typeUseSiteInfo.DiagnosticInfo?.Code = ERRID.ERR_UnsupportedCompilerFeature Then
                Return typeUseSiteInfo.DiagnosticInfo
            End If

            Debug.Assert(DeriveCompilerFeatureRequiredDiagnostic() Is Nothing)

            Return Nothing
        End Function

        Private Function DeriveCompilerFeatureRequiredDiagnostic() As DiagnosticInfo
            Dim decoder = New MetadataDecoder(ContainingPEModule, Me)

            Dim diagnostic = DeriveCompilerFeatureRequiredAttributeDiagnostic(Me, ContainingPEModule, Handle, CompilerFeatureRequiredFeatures.RefStructs, decoder)

            If diagnostic IsNot Nothing Then
                Return diagnostic
            End If

            For Each typeParameter In TypeParameters
                diagnostic = DirectCast(typeParameter, PETypeParameterSymbol).DeriveCompilerFeatureRequiredDiagnostic(decoder)

                If diagnostic IsNot Nothing Then
                    Return diagnostic
                End If
            Next

            Dim containingPEType = TryCast(ContainingType, PENamedTypeSymbol)

            Return If(containingPEType IsNot Nothing, containingPEType.GetCompilerFeatureRequiredDiagnostic(), ContainingPEModule.GetCompilerFeatureRequiredDiagnostic())
        End Function

        ''' <summary>
        ''' Return true if the type parameters specified on the nested type (Me),
        ''' that represent the corresponding type parameters on the containing
        ''' types, in fact match the actual type parameters on the containing types.
        ''' </summary>
        Private Function MatchesContainingTypeParameters() As Boolean
            If _genericParameterHandles.Count = 0 Then
                Return True
            End If

            Dim container = ContainingType
            If container Is Nothing Then
                Return True
            End If

            Dim containingTypeParameters = container.GetAllTypeParameters()
            Dim n = containingTypeParameters.Length

            If n = 0 Then
                Return True
            End If

            ' Create an instance of PENamedTypeSymbol for the nested type, but
            ' with all type parameters, from the nested type and all containing
            ' types. The type parameters on this temporary type instance are used
            ' for comparison with those on the actual containing types. The
            ' containing symbol for the temporary type is the namespace directly.
            Dim nestedType = New PENamedTypeSymbol(ContainingPEModule, DirectCast(ContainingNamespace, PENamespaceSymbol), _handle)
            Dim nestedTypeParameters = nestedType.TypeParameters
            Dim containingTypeMap = TypeSubstitution.Create(
                container,
                containingTypeParameters,
                IndexedTypeParameterSymbol.Take(n).As(Of TypeSymbol))
            Dim nestedTypeMap = TypeSubstitution.Create(
                nestedType,
                nestedTypeParameters,
                IndexedTypeParameterSymbol.Take(nestedTypeParameters.Length).As(Of TypeSymbol))

            For i = 0 To n - 1
                Dim containingTypeParameter = containingTypeParameters(i)
                Dim nestedTypeParameter = nestedTypeParameters(i)
                If Not MethodSignatureComparer.HaveSameConstraints(
                    containingTypeParameter,
                    containingTypeMap,
                    nestedTypeParameter,
                    nestedTypeMap) Then
                    Return False
                End If
            Next

            Return True
        End Function

        ''' <summary>
        ''' Force all declaration errors to be generated.
        ''' </summary>
        Friend Overrides Sub GenerateDeclarationErrors(cancellationToken As CancellationToken)
            Throw ExceptionUtilities.Unreachable
        End Sub

        Friend Overrides ReadOnly Property ObsoleteAttributeData As ObsoleteAttributeData
            Get
                ObsoleteAttributeHelpers.InitializeObsoleteDataFromMetadata(_lazyObsoleteAttributeData, _handle, ContainingPEModule)
                Return _lazyObsoleteAttributeData
            End Get
        End Property

        Friend Overrides Function GetAppliedConditionalSymbols() As ImmutableArray(Of String)
            If Me._lazyConditionalAttributeSymbols.IsDefault Then
                Dim conditionalSymbols As ImmutableArray(Of String) = ContainingPEModule.Module.GetConditionalAttributeValues(_handle)
                Debug.Assert(Not conditionalSymbols.IsDefault)
                ImmutableInterlocked.InterlockedCompareExchange(_lazyConditionalAttributeSymbols, conditionalSymbols, Nothing)
            End If

            Return Me._lazyConditionalAttributeSymbols
        End Function

        Friend Overrides Function GetAttributeUsageInfo() As AttributeUsageInfo
            If _lazyAttributeUsageInfo.IsNull Then
                _lazyAttributeUsageInfo = DecodeAttributeUsageInfo()
            End If

            Debug.Assert(Not _lazyAttributeUsageInfo.IsNull)
            Return _lazyAttributeUsageInfo
        End Function

        Private Function DecodeAttributeUsageInfo() As AttributeUsageInfo
            Dim result As AttributeUsageInfo = Nothing

            If Me.ContainingPEModule.Module.HasAttributeUsageAttribute(_handle, New MetadataDecoder(ContainingPEModule), result) Then
                Return result
            End If

            Dim baseType = Me.BaseTypeNoUseSiteDiagnostics
            Return If(baseType IsNot Nothing, baseType.GetAttributeUsageInfo(), AttributeUsageInfo.Default)
        End Function

        Friend Overrides ReadOnly Property HasDeclarativeSecurity As Boolean
            Get
                Throw ExceptionUtilities.Unreachable
            End Get
        End Property

        Friend Overrides Function GetSecurityInformation() As IEnumerable(Of Microsoft.Cci.SecurityAttribute)
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides ReadOnly Property IsExtensibleInterfaceNoUseSiteDiagnostics As Boolean
            Get
                If _lazyIsExtensibleInterface = ThreeState.Unknown Then
                    _lazyIsExtensibleInterface = DecodeIsExtensibleInterface().ToThreeState()
                End If

                Return _lazyIsExtensibleInterface.Value
            End Get
        End Property

        Private Function DecodeIsExtensibleInterface() As Boolean
            If Me.IsInterfaceType() Then
                If Me.HasAttributeForExtensibleInterface() Then
                    Return True
                End If

                For Each [interface] In Me.AllInterfacesNoUseSiteDiagnostics
                    If [interface].IsExtensibleInterfaceNoUseSiteDiagnostics Then
                        Return True
                    End If
                Next
            End If

            Return False
        End Function

        Private Function HasAttributeForExtensibleInterface() As Boolean
            Dim metadataModule = Me.ContainingPEModule.Module

            ' Is interface marked with 'TypeLibTypeAttribute( flags w/o TypeLibTypeFlags.FNonExtensible )' attribute
            Dim flags As Cci.TypeLibTypeFlags = Nothing
            If metadataModule.HasTypeLibTypeAttribute(Me._handle, flags) AndAlso
                (flags And Cci.TypeLibTypeFlags.FNonExtensible) = 0 Then
                Return True
            End If

            ' Is interface marked with 'InterfaceTypeAttribute( flags with ComInterfaceType.InterfaceIsIDispatch )' attribute
            Dim interfaceType As ComInterfaceType = Nothing
            If metadataModule.HasInterfaceTypeAttribute(Me._handle, interfaceType) AndAlso
                (interfaceType And Cci.Constants.ComInterfaceType_InterfaceIsIDispatch) <> 0 Then
                Return True
            End If

            Return False
        End Function

        ''' <remarks>
        ''' This is for perf, not for correctness.
        ''' </remarks>
        Friend NotOverridable Overrides ReadOnly Property DeclaringCompilation As VisualBasicCompilation
            Get
                Return Nothing
            End Get
        End Property

        ''' <summary>
        ''' Returns the index of the first member of the specific kind.
        ''' Returns the number of members if not found.
        ''' </summary>
        Private Shared Function GetIndexOfFirstMember(members As ImmutableArray(Of Symbol), kind As SymbolKind) As Integer
            Dim n = members.Length
            For i = 0 To n - 1
                If members(i).Kind = kind Then
                    Return i
                End If
            Next
            Return n
        End Function

        ''' <summary>
        ''' Returns all members of the specific kind, starting at the optional offset.
        ''' Members of the same kind are assumed to be contiguous.
        ''' </summary>
        Private Overloads Shared Iterator Function GetMembers(Of TSymbol As Symbol)(members As ImmutableArray(Of Symbol), kind As SymbolKind, Optional offset As Integer = -1) As IEnumerable(Of TSymbol)
            If offset < 0 Then
                offset = GetIndexOfFirstMember(members, kind)
            End If
            Dim n = members.Length
            For i = offset To n - 1
                Dim member = members(i)
                If member.Kind <> kind Then
                    Return
                End If
                Yield DirectCast(member, TSymbol)
            Next
        End Function

        Friend NotOverridable Overrides Function GetSynthesizedWithEventsOverrides() As IEnumerable(Of PropertySymbol)
            Return SpecializedCollections.EmptyEnumerable(Of PropertySymbol)()
        End Function

        Friend Overrides ReadOnly Property HasAnyDeclaredRequiredMembers As Boolean
            Get
                Return ContainingPEModule.Module.HasAttribute(Handle, AttributeDescription.RequiredMemberAttribute)
            End Get
        End Property

#Region "C# 14 extension members"
        ''' <summary>
        ''' The members that C# 14 extension blocks of this type declare, other than instance extension methods, which C#
        ''' also emits as classic extension methods.
        ''' </summary>
        Private NotInheritable Class ExtensionMembers
            Public ReadOnly InstanceProperties As New List(Of PropertySymbol)()
            Public ReadOnly StaticProperties As New List(Of PropertySymbol)()
            Public ReadOnly StaticMethods As New Dictionary(Of MethodSymbol, TypeSymbol)()
            Public ReadOnly Operators As New Dictionary(Of MethodSymbol, TypeSymbol)()
        End Class

        Private _lazyExtensionMembers As ExtensionMembers

        Private Function GetExtensionMembers() As ExtensionMembers
            If _lazyExtensionMembers Is Nothing Then
                Interlocked.CompareExchange(_lazyExtensionMembers, BuildExtensionMembers(), Nothing)
            End If

            Return _lazyExtensionMembers
        End Function

        ''' <summary>
        ''' C# 14 emits an extension block as a nested grouping type whose members are declarations marked with
        ''' ExtensionMarkerAttribute, naming a nested marker type whose &lt;Extension&gt;$ method takes the receiver.
        ''' The members are implemented by static methods of this type, which take the receiver first unless static.
        ''' </summary>
        Private Function BuildExtensionMembers() As ExtensionMembers
            Dim result As New ExtensionMembers()
            If Not Me.MightContainExtensionMethods Then
                Return result
            End If

            For Each grouping In GetTypeMembersUnordered()
                If Not grouping.Name.StartsWith("<G>$", StringComparison.Ordinal) Then
                    Continue For
                End If

                Dim receivers As New Dictionary(Of String, ParameterSymbol)(StringComparer.Ordinal)
                For Each marker In grouping.GetTypeMembersUnordered()
                    For Each markerMethod In marker.GetMembers("<Extension>$").OfType(Of MethodSymbol)()
                        If markerMethod.ParameterCount = 1 Then
                            receivers(marker.Name) = markerMethod.Parameters(0)
                        End If
                    Next
                Next

                For Each declaration In grouping.GetMembersUnordered()
                    Dim markerName = GetExtensionMarkerName(declaration)
                    Dim receiver As ParameterSymbol = Nothing
                    If markerName Is Nothing OrElse Not receivers.TryGetValue(markerName, receiver) Then
                        Continue For
                    End If

                    Select Case declaration.Kind
                        Case SymbolKind.Property
                            Dim [property] = DirectCast(declaration, PropertySymbol)
                            Dim getter = FindExtensionImplementation([property].GetMethod, receiver, grouping)
                            Dim setter = FindExtensionImplementation([property].SetMethod, receiver, grouping)
                            If getter IsNot Nothing OrElse setter IsNot Nothing Then
                                Dim extendedType = SubstituteGroupingTypeParameters(receiver.Type, grouping, If(getter, setter))
                                Dim implemented = New PEExtensionPropertySymbol(Me, [property], getter, setter, extendedType)
                                If [property].IsShared Then
                                    result.StaticProperties.Add(implemented)
                                Else
                                    result.InstanceProperties.Add(implemented)
                                End If
                            End If

                        Case SymbolKind.Method
                            Dim method = DirectCast(declaration, MethodSymbol)
                            If Not method.IsShared Then
                                Continue For
                            End If

                            Dim isOperator = method.HasSpecialName AndAlso method.Name.StartsWith("op_", StringComparison.Ordinal)
                            If isOperator AndAlso grouping.Arity <> 0 Then
                                Continue For
                            End If

                            If isOperator OrElse method.MethodKind = MethodKind.Ordinary Then
                                Dim implementation = FindExtensionImplementation(method, receiver, grouping)
                                If implementation IsNot Nothing Then
                                    Dim extendedType = SubstituteGroupingTypeParameters(receiver.Type, grouping, implementation)
                                    If isOperator Then
                                        result.Operators(implementation) = extendedType
                                    Else
                                        result.StaticMethods(implementation) = extendedType
                                    End If
                                End If
                            End If
                    End Select
                Next
            Next

            Return result
        End Function

        Private Shared Function GetExtensionMarkerName(declaration As Symbol) As String
            For Each attribute In declaration.GetAttributes()
                If attribute.AttributeClass?.Name = "ExtensionMarkerAttribute" AndAlso
                   attribute.CommonConstructorArguments.Length = 1 Then
                    Return TryCast(attribute.CommonConstructorArguments(0).ValueInternal, String)
                End If
            Next

            Return Nothing
        End Function

        ''' <summary>
        ''' Finds the static method of this type that implements a declaration: the same name, and the declaration's
        ''' parameters after the receiver unless the declaration is static. The implementation of a member of a generic
        ''' block takes the block's type parameters as its own first type parameters; generic members of generic blocks
        ''' are not read.
        ''' </summary>
        Private Function FindExtensionImplementation(declaration As MethodSymbol, receiver As ParameterSymbol, grouping As NamedTypeSymbol) As MethodSymbol
            If declaration Is Nothing OrElse (grouping.Arity <> 0 AndAlso declaration.Arity <> 0) Then
                Return Nothing
            End If

            Dim offset = If(declaration.IsShared, 0, 1)
            For Each candidate In GetMembers(declaration.Name).OfType(Of MethodSymbol)()
                If Not candidate.IsShared OrElse candidate.ParameterCount <> declaration.ParameterCount + offset OrElse
                   candidate.Arity <> declaration.Arity + grouping.Arity OrElse
                   Not candidate.ReturnType.IsSameType(SubstituteGroupingTypeParameters(declaration.ReturnType, grouping, candidate), TypeCompareKind.AllIgnoreOptionsForVB) Then
                    Continue For
                End If

                If offset = 1 AndAlso Not candidate.Parameters(0).Type.IsSameType(SubstituteGroupingTypeParameters(receiver.Type, grouping, candidate), TypeCompareKind.AllIgnoreOptionsForVB) Then
                    Continue For
                End If

                Dim matches = True
                For i = 0 To declaration.ParameterCount - 1
                    If Not candidate.Parameters(i + offset).Type.IsSameType(SubstituteGroupingTypeParameters(declaration.Parameters(i).Type, grouping, candidate), TypeCompareKind.AllIgnoreOptionsForVB) Then
                        matches = False
                        Exit For
                    End If
                Next

                If matches Then
                    Return candidate
                End If
            Next

            Return Nothing
        End Function

        ''' <summary>
        ''' A type of a generic extension block's declaration, in terms of the implementation method's type parameters.
        ''' </summary>
        Private Shared Function SubstituteGroupingTypeParameters(type As TypeSymbol, grouping As NamedTypeSymbol, implementation As MethodSymbol) As TypeSymbol
            If grouping.Arity = 0 OrElse implementation.Arity < grouping.Arity Then
                Return type
            End If

            Dim substitution = TypeSubstitution.Create(grouping,
                                                       grouping.TypeParameters,
                                                       implementation.TypeParameters.Take(grouping.Arity).Cast(Of TypeSymbol)().ToImmutableArray(),
                                                       allowAlphaRenamedTypeParametersAsArguments:=True)
            Return type.InternalSubstituteTypeParameters(substitution).Type
        End Function

        ''' <summary>
        ''' The receiver type of a static method that implements a static member of a C# 14 extension block.
        ''' </summary>
        Friend Function GetStaticExtensionType(method As MethodSymbol) As TypeSymbol
            Dim extendedType As TypeSymbol = Nothing
            Return If(GetExtensionMembers().StaticMethods.TryGetValue(method, extendedType), extendedType, Nothing)
        End Function

        Friend Overrides Sub AppendProbableExtensionProperties(name As String, properties As ArrayBuilder(Of PropertySymbol))
            MyBase.AppendProbableExtensionProperties(name, properties)
            For Each [property] In GetExtensionMembers().InstanceProperties
                If IdentifierComparison.Equals([property].Name, name) Then
                    properties.Add([property])
                End If
            Next
        End Sub

        ''' <summary>
        ''' The receiver type of a static method that implements an operator of a C# 14 extension block.
        ''' </summary>
        Friend Function GetExtensionOperatorType(method As MethodSymbol) As TypeSymbol
            Dim extendedType As TypeSymbol = Nothing
            Return If(GetExtensionMembers().Operators.TryGetValue(method, extendedType), extendedType, Nothing)
        End Function

        ''' <summary>
        ''' Whether a method, read from metadata as an ordinary method, implements an operator of a C# 14 extension block.
        ''' </summary>
        Friend Shared Function IsExtensionOperator(method As MethodSymbol) As Boolean
            Return TryCast(method.ContainingType, PENamedTypeSymbol)?.GetExtensionOperatorType(method) IsNot Nothing
        End Function

        Friend Overrides Sub AppendProbableStaticExtensionMembers(name As String, members As ArrayBuilder(Of Symbol))
            MyBase.AppendProbableStaticExtensionMembers(name, members)
            For Each [property] In GetExtensionMembers().StaticProperties
                If IdentifierComparison.Equals([property].Name, name) Then
                    members.Add([property])
                End If
            Next

            ' Operators are collected here too, and have no StaticExtensionType, so member access does not find them.
            For Each [operator] In GetExtensionMembers().Operators.Keys
                If String.Equals([operator].Name, name, StringComparison.Ordinal) Then
                    members.Add([operator])
                End If
            Next
        End Sub
#End Region
    End Class

End Namespace
