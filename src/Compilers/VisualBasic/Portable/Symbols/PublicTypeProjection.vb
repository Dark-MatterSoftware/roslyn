' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Globalization
Imports System.Threading
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Roslyn.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Public representation of one annotated use of an existing compiler type.
    ''' The projection is not a <see cref="TypeSymbol"/> and is never stored in compiler caches.
    ''' </summary>
    Friend MustInherit Class PublicTypeProjection
        Implements ITypeSymbol

        Private ReadOnly _underlying As TypeSymbol
        Private ReadOnly _annotation As NullableAnnotation

        Private Sub New(underlying As TypeSymbol, annotation As NullableAnnotation)
            Debug.Assert(underlying IsNot Nothing)
            Debug.Assert(annotation <> NullableAnnotation.None)
            _underlying = underlying
            _annotation = annotation
        End Sub

        Friend ReadOnly Property Underlying As TypeSymbol
            Get
                Return _underlying
            End Get
        End Property

        Friend ReadOnly Property Annotation As NullableAnnotation
            Get
                Return _annotation
            End Get
        End Property

        Private ReadOnly Property PublicType As ITypeSymbol
            Get
                Return _underlying
            End Get
        End Property

        Friend Shared Function Create(underlying As TypeSymbol, annotation As NullableAnnotation) As ITypeSymbol
            If underlying Is Nothing Then
                Return Nothing
            End If

            If annotation = NullableAnnotation.None Then
                Return underlying
            End If

            If TypeOf underlying Is ErrorTypeSymbol Then
                Return New ErrorTypeProjection(DirectCast(underlying, ErrorTypeSymbol), annotation)
            End If

            If TypeOf underlying Is NamedTypeSymbol Then
                Return New NamedTypeProjection(DirectCast(underlying, NamedTypeSymbol), annotation)
            End If

            If TypeOf underlying Is ArrayTypeSymbol Then
                Return New ArrayTypeProjection(DirectCast(underlying, ArrayTypeSymbol), annotation)
            End If

            If TypeOf underlying Is TypeParameterSymbol Then
                Return New TypeParameterProjection(DirectCast(underlying, TypeParameterSymbol), annotation)
            End If

            Throw ExceptionUtilities.UnexpectedValue(underlying.GetType())
        End Function

        Friend Shared Function CaptureAnnotations(typeArguments As ImmutableArray(Of ITypeSymbol)) As ImmutableArray(Of NullableAnnotation)
            If typeArguments.IsDefaultOrEmpty Then
                Return Nothing
            End If

            Dim any = False
            Dim builder = ArrayBuilder(Of NullableAnnotation).GetInstance(typeArguments.Length)
            For Each typeArgument In typeArguments
                Dim annotation = GetAnnotation(typeArgument)
                If annotation <> NullableAnnotation.None Then
                    any = True
                End If

                builder.Add(annotation)
            Next

            If Not any Then
                builder.Free()
                Return Nothing
            End If

            Return builder.ToImmutableAndFree()
        End Function

        Friend Shared Function GetAnnotation(symbol As ISymbol) As NullableAnnotation
            Dim projection = TryCast(symbol, PublicTypeProjection)
            If projection IsNot Nothing Then
                Return projection.Annotation
            End If

            Return NullableAnnotation.None
        End Function

        Friend Shared Function Unwrap(symbol As ISymbol) As ISymbol
            Dim projection = TryCast(symbol, PublicTypeProjection)
            If projection IsNot Nothing Then
                Return projection.Underlying
            End If

            Return symbol
        End Function

        Friend Shared Function AreEqual(left As ISymbol, right As ISymbol, comparer As SymbolEqualityComparer) As Boolean
            If left Is right Then
                Return True
            End If

            If left Is Nothing OrElse right Is Nothing Then
                Return False
            End If

            Dim leftUnderlying As TypeSymbol = Nothing
            Dim leftAnnotation As NullableAnnotation
            Dim rightUnderlying As TypeSymbol = Nothing
            Dim rightAnnotation As NullableAnnotation
            If Not TryGetCompilerType(left, leftUnderlying, leftAnnotation) OrElse
               Not TryGetCompilerType(right, rightUnderlying, rightAnnotation) Then
                Return False
            End If

            Dim compareKind = comparer.CompareKind
            If Not AnnotationsEqual(leftAnnotation, rightAnnotation, compareKind, AnnotationIsSignificant(leftUnderlying)) Then
                Return False
            End If

            If Not leftUnderlying.Equals(rightUnderlying, compareKind) Then
                Return False
            End If

            If (compareKind And TypeCompareKind.IgnoreNullableModifiersForReferenceTypes) <> 0 Then
                Return True
            End If

            Return NestedAnnotationsEqual(DirectCast(left, ITypeSymbol), DirectCast(right, ITypeSymbol), compareKind)
        End Function

        Friend Shared Function NestedAnnotationsEqual(left As ITypeSymbol, right As ITypeSymbol, compareKind As TypeCompareKind) As Boolean
            If left Is right Then
                Return True
            End If

            If left Is Nothing OrElse right Is Nothing Then
                Return False
            End If

            Dim leftArray = TryCast(left, IArrayTypeSymbol)
            If leftArray IsNot Nothing Then
                Dim rightArray = TryCast(right, IArrayTypeSymbol)
                If rightArray Is Nothing OrElse
                   Not AnnotationsEqual(leftArray.ElementNullableAnnotation, rightArray.ElementNullableAnnotation, compareKind, AnnotationIsSignificant(leftArray.ElementType)) Then
                    Return False
                End If

                Return NestedAnnotationsEqual(leftArray.ElementType, rightArray.ElementType, compareKind)
            End If

            Dim leftNamed = TryCast(left, INamedTypeSymbol)
            If leftNamed IsNot Nothing Then
                Dim rightNamed = TryCast(right, INamedTypeSymbol)
                If rightNamed Is Nothing OrElse
                   Not AnnotationListsEqual(leftNamed.TypeArgumentNullableAnnotations, rightNamed.TypeArgumentNullableAnnotations, leftNamed.TypeArguments, rightNamed.TypeArguments, compareKind) Then
                    Return False
                End If

                Dim leftArguments = leftNamed.TypeArguments
                Dim rightArguments = rightNamed.TypeArguments
                If leftArguments.Length <> rightArguments.Length Then
                    Return False
                End If

                For i = 0 To leftArguments.Length - 1
                    If Not NestedAnnotationsEqual(leftArguments(i), rightArguments(i), compareKind) Then
                        Return False
                    End If
                Next

                Return NestedAnnotationsEqual(leftNamed.ContainingType, rightNamed.ContainingType, compareKind)
            End If

            Dim leftParameter = TryCast(left, ITypeParameterSymbol)
            If leftParameter IsNot Nothing Then
                Dim rightParameter = TryCast(right, ITypeParameterSymbol)
                If rightParameter Is Nothing OrElse
                   Not AnnotationsEqual(leftParameter.ReferenceTypeConstraintNullableAnnotation, rightParameter.ReferenceTypeConstraintNullableAnnotation, compareKind, isSignificant:=True) OrElse
                   Not AnnotationListsEqual(leftParameter.ConstraintNullableAnnotations, rightParameter.ConstraintNullableAnnotations, leftParameter.ConstraintTypes, rightParameter.ConstraintTypes, compareKind) Then
                    Return False
                End If

                Dim leftConstraints = leftParameter.ConstraintTypes
                Dim rightConstraints = rightParameter.ConstraintTypes
                If leftConstraints.Length <> rightConstraints.Length Then
                    Return False
                End If

                For i = 0 To leftConstraints.Length - 1
                    If Not NestedAnnotationsEqual(leftConstraints(i), rightConstraints(i), compareKind) Then
                        Return False
                    End If
                Next
            End If

            Return True
        End Function

        Private Shared Function TryGetCompilerType(symbol As ISymbol, ByRef underlying As TypeSymbol, ByRef annotation As NullableAnnotation) As Boolean
            Dim projection = TryCast(symbol, PublicTypeProjection)
            If projection IsNot Nothing Then
                underlying = projection.Underlying
                annotation = projection.Annotation
                Return True
            End If

            underlying = TryCast(symbol, TypeSymbol)
            If underlying Is Nothing Then
                annotation = NullableAnnotation.None
                Return False
            End If

            annotation = NullableAnnotation.None
            Return True
        End Function

        Private Shared Function AnnotationsEqual(left As NullableAnnotation, right As NullableAnnotation, compareKind As TypeCompareKind, isSignificant As Boolean) As Boolean
            If left = right OrElse (compareKind And TypeCompareKind.IgnoreNullableModifiersForReferenceTypes) <> 0 OrElse Not isSignificant Then
                Return True
            End If

            Return (compareKind And TypeCompareKind.ObliviousNullableModifierMatchesAny) <> 0 AndAlso
                   (left = NullableAnnotation.None OrElse right = NullableAnnotation.None)
        End Function

        Private Shared Function AnnotationListsEqual(leftAnnotations As ImmutableArray(Of NullableAnnotation),
                                                      rightAnnotations As ImmutableArray(Of NullableAnnotation),
                                                      leftTypes As ImmutableArray(Of ITypeSymbol),
                                                      rightTypes As ImmutableArray(Of ITypeSymbol),
                                                      compareKind As TypeCompareKind) As Boolean
            Dim leftLength = If(leftAnnotations.IsDefault, 0, leftAnnotations.Length)
            Dim rightLength = If(rightAnnotations.IsDefault, 0, rightAnnotations.Length)
            If leftLength <> rightLength Then
                Return False
            End If

            For i = 0 To leftLength - 1
                Dim significant = True
                If Not leftTypes.IsDefault AndAlso i < leftTypes.Length Then
                    significant = AnnotationIsSignificant(leftTypes(i))
                ElseIf Not rightTypes.IsDefault AndAlso i < rightTypes.Length Then
                    significant = AnnotationIsSignificant(rightTypes(i))
                End If

                If Not AnnotationsEqual(leftAnnotations(i), rightAnnotations(i), compareKind, significant) Then
                    Return False
                End If
            Next

            Return True
        End Function

        Private Shared Function AnnotationIsSignificant(type As ITypeSymbol) As Boolean
            If type Is Nothing OrElse Not type.IsValueType Then
                Return True
            End If

            Dim named = TryCast(type, INamedTypeSymbol)
            Return named IsNot Nothing AndAlso named.OriginalDefinition.SpecialType = SpecialType.System_Nullable_T
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Dim other = TryCast(Unwrap(TryCast(obj, ISymbol)), TypeSymbol)
            If other Is Nothing AndAlso obj IsNot Nothing Then
                Return False
            End If

            Return _underlying.Equals(other, TypeCompareKind.ConsiderEverything)
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return _underlying.GetHashCode()
        End Function

        Private Function IEquatable_Equals(other As ISymbol) As Boolean Implements IEquatable(Of ISymbol).Equals
            Return AreEqual(Me, other, SymbolEqualityComparer.Default)
        End Function

        Private Function ISymbol_Equals(other As ISymbol, equalityComparer As SymbolEqualityComparer) As Boolean Implements ISymbol.Equals
            Return AreEqual(Me, other, equalityComparer)
        End Function

        Private ReadOnly Property ITypeSymbol_NullableAnnotation As NullableAnnotation Implements ITypeSymbol.NullableAnnotation
            Get
                Return _annotation
            End Get
        End Property

        Private Function ITypeSymbol_WithNullableAnnotation(nullableAnnotation As NullableAnnotation) As ITypeSymbol Implements ITypeSymbol.WithNullableAnnotation
            If nullableAnnotation = _annotation Then
                Return Me
            End If

            Return Create(_underlying, nullableAnnotation)
        End Function

        Private Sub ISymbol_Accept(visitor As SymbolVisitor) Implements ISymbol.Accept
            Dispatch(visitor)
        End Sub

        Private Function ISymbol_Accept(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult Implements ISymbol.Accept
            Return Dispatch(visitor)
        End Function

        Private Function ISymbol_Accept(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult Implements ISymbol.Accept
            Return Dispatch(visitor, argument)
        End Function

        Private Sub Dispatch(visitor As SymbolVisitor)
            If TypeOf Me Is ArrayTypeProjection Then
                visitor.VisitArrayType(DirectCast(Me, IArrayTypeSymbol))
            ElseIf TypeOf Me Is TypeParameterProjection Then
                visitor.VisitTypeParameter(DirectCast(Me, ITypeParameterSymbol))
            Else
                visitor.VisitNamedType(DirectCast(Me, INamedTypeSymbol))
            End If
        End Sub

        Private Function Dispatch(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
            If TypeOf Me Is ArrayTypeProjection Then
                Return visitor.VisitArrayType(DirectCast(Me, IArrayTypeSymbol))
            ElseIf TypeOf Me Is TypeParameterProjection Then
                Return visitor.VisitTypeParameter(DirectCast(Me, ITypeParameterSymbol))
            Else
                Return visitor.VisitNamedType(DirectCast(Me, INamedTypeSymbol))
            End If
        End Function

        Private Function Dispatch(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
            If TypeOf Me Is ArrayTypeProjection Then
                Return visitor.VisitArrayType(DirectCast(Me, IArrayTypeSymbol), argument)
            ElseIf TypeOf Me Is TypeParameterProjection Then
                Return visitor.VisitTypeParameter(DirectCast(Me, ITypeParameterSymbol), argument)
            Else
                Return visitor.VisitNamedType(DirectCast(Me, INamedTypeSymbol), argument)
            End If
        End Function

        Private ReadOnly Property ISymbol_Kind As SymbolKind Implements ISymbol.Kind
            Get
                Return PublicType.Kind
            End Get
        End Property

        Private ReadOnly Property ISymbol_Language As String Implements ISymbol.Language
            Get
                Return PublicType.Language
            End Get
        End Property

        Private ReadOnly Property ISymbol_Name As String Implements ISymbol.Name
            Get
                Return PublicType.Name
            End Get
        End Property

        Private ReadOnly Property ISymbol_MetadataName As String Implements ISymbol.MetadataName
            Get
                Return PublicType.MetadataName
            End Get
        End Property

        Private ReadOnly Property ISymbol_MetadataToken As Integer Implements ISymbol.MetadataToken
            Get
                Return PublicType.MetadataToken
            End Get
        End Property

        Private ReadOnly Property ISymbol_ContainingSymbol As ISymbol Implements ISymbol.ContainingSymbol
            Get
                Return PublicType.ContainingSymbol
            End Get
        End Property

        Private ReadOnly Property ISymbol_ContainingAssembly As IAssemblySymbol Implements ISymbol.ContainingAssembly
            Get
                Return PublicType.ContainingAssembly
            End Get
        End Property

        Private ReadOnly Property ISymbol_ContainingModule As IModuleSymbol Implements ISymbol.ContainingModule
            Get
                Return PublicType.ContainingModule
            End Get
        End Property

        Private ReadOnly Property ISymbol_ContainingType As INamedTypeSymbol Implements ISymbol.ContainingType
            Get
                Return PublicType.ContainingType
            End Get
        End Property

        Private ReadOnly Property ISymbol_ContainingNamespace As INamespaceSymbol Implements ISymbol.ContainingNamespace
            Get
                Return PublicType.ContainingNamespace
            End Get
        End Property

        Private ReadOnly Property ISymbol_IsDefinition As Boolean Implements ISymbol.IsDefinition
            Get
                Return PublicType.IsDefinition
            End Get
        End Property

        Private ReadOnly Property ISymbol_IsStatic As Boolean Implements ISymbol.IsStatic
            Get
                Return PublicType.IsStatic
            End Get
        End Property

        Private ReadOnly Property ISymbol_IsVirtual As Boolean Implements ISymbol.IsVirtual
            Get
                Return PublicType.IsVirtual
            End Get
        End Property

        Private ReadOnly Property ISymbol_IsOverride As Boolean Implements ISymbol.IsOverride
            Get
                Return PublicType.IsOverride
            End Get
        End Property

        Private ReadOnly Property ISymbol_IsAbstract As Boolean Implements ISymbol.IsAbstract
            Get
                Return PublicType.IsAbstract
            End Get
        End Property

        Private ReadOnly Property ISymbol_IsSealed As Boolean Implements ISymbol.IsSealed
            Get
                Return PublicType.IsSealed
            End Get
        End Property

        Private ReadOnly Property ISymbol_IsExtern As Boolean Implements ISymbol.IsExtern
            Get
                Return PublicType.IsExtern
            End Get
        End Property

        Private ReadOnly Property ISymbol_IsImplicitlyDeclared As Boolean Implements ISymbol.IsImplicitlyDeclared
            Get
                Return PublicType.IsImplicitlyDeclared
            End Get
        End Property

        Private ReadOnly Property ISymbol_RequiresUnsafeContext As Boolean Implements ISymbol.RequiresUnsafeContext
            Get
                Return PublicType.RequiresUnsafeContext
            End Get
        End Property

        Private ReadOnly Property ISymbol_CanBeReferencedByName As Boolean Implements ISymbol.CanBeReferencedByName
            Get
                Return PublicType.CanBeReferencedByName
            End Get
        End Property

        Private ReadOnly Property ISymbol_Locations As ImmutableArray(Of Location) Implements ISymbol.Locations
            Get
                Return PublicType.Locations
            End Get
        End Property

        Private ReadOnly Property ISymbol_DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference) Implements ISymbol.DeclaringSyntaxReferences
            Get
                Return PublicType.DeclaringSyntaxReferences
            End Get
        End Property

        Private Function ISymbol_GetAttributes() As ImmutableArray(Of AttributeData) Implements ISymbol.GetAttributes
            Return PublicType.GetAttributes()
        End Function

        Private ReadOnly Property ISymbol_DeclaredAccessibility As Accessibility Implements ISymbol.DeclaredAccessibility
            Get
                Return PublicType.DeclaredAccessibility
            End Get
        End Property

        Private ReadOnly Property ISymbol_OriginalDefinition As ISymbol Implements ISymbol.OriginalDefinition
            Get
                Return PublicType.OriginalDefinition
            End Get
        End Property

        Private Function ISymbol_GetDocumentationCommentId() As String Implements ISymbol.GetDocumentationCommentId
            Return PublicType.GetDocumentationCommentId()
        End Function

        Private Function ISymbol_GetDocumentationCommentXml(Optional preferredCulture As CultureInfo = Nothing, Optional expandIncludes As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As String Implements ISymbol.GetDocumentationCommentXml
            Return PublicType.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken)
        End Function

        Private Function ISymbol_ToDisplayString(Optional format As SymbolDisplayFormat = Nothing) As String Implements ISymbol.ToDisplayString
            Return PublicType.ToDisplayString(format)
        End Function

        Private Function ISymbol_ToDisplayParts(Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ISymbol.ToDisplayParts
            Return PublicType.ToDisplayParts(format)
        End Function

        Private Function ISymbol_ToMinimalDisplayString(semanticModel As SemanticModel, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As String Implements ISymbol.ToMinimalDisplayString
            Return PublicType.ToMinimalDisplayString(semanticModel, position, format)
        End Function

        Private Function ISymbol_ToMinimalDisplayParts(semanticModel As SemanticModel, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ISymbol.ToMinimalDisplayParts
            Return PublicType.ToMinimalDisplayParts(semanticModel, position, format)
        End Function

        Private ReadOnly Property ISymbol_HasUnsupportedMetadata As Boolean Implements ISymbol.HasUnsupportedMetadata
            Get
                Return PublicType.HasUnsupportedMetadata
            End Get
        End Property

        Private Function INamespaceOrTypeSymbol_GetMembers() As ImmutableArray(Of ISymbol) Implements INamespaceOrTypeSymbol.GetMembers
            Return PublicType.GetMembers()
        End Function

        Private Function INamespaceOrTypeSymbol_GetMembers(name As String) As ImmutableArray(Of ISymbol) Implements INamespaceOrTypeSymbol.GetMembers
            Return PublicType.GetMembers(name)
        End Function

        Private Function INamespaceOrTypeSymbol_GetTypeMembers() As ImmutableArray(Of INamedTypeSymbol) Implements INamespaceOrTypeSymbol.GetTypeMembers
            Return PublicType.GetTypeMembers()
        End Function

        Private Function INamespaceOrTypeSymbol_GetTypeMembers(name As String) As ImmutableArray(Of INamedTypeSymbol) Implements INamespaceOrTypeSymbol.GetTypeMembers
            Return PublicType.GetTypeMembers(name)
        End Function

        Private Function INamespaceOrTypeSymbol_GetTypeMembers(name As String, arity As Integer) As ImmutableArray(Of INamedTypeSymbol) Implements INamespaceOrTypeSymbol.GetTypeMembers
            Return PublicType.GetTypeMembers(name, arity)
        End Function

        Private ReadOnly Property INamespaceOrTypeSymbol_IsNamespace As Boolean Implements INamespaceOrTypeSymbol.IsNamespace
            Get
                Return PublicType.IsNamespace
            End Get
        End Property

        Private ReadOnly Property INamespaceOrTypeSymbol_IsType As Boolean Implements INamespaceOrTypeSymbol.IsType
            Get
                Return PublicType.IsType
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_TypeKind As TypeKind Implements ITypeSymbol.TypeKind
            Get
                Return PublicType.TypeKind
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_BaseType As INamedTypeSymbol Implements ITypeSymbol.BaseType
            Get
                Return PublicType.BaseType
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_Interfaces As ImmutableArray(Of INamedTypeSymbol) Implements ITypeSymbol.Interfaces
            Get
                Return PublicType.Interfaces
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_AllInterfaces As ImmutableArray(Of INamedTypeSymbol) Implements ITypeSymbol.AllInterfaces
            Get
                Return PublicType.AllInterfaces
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsReferenceType As Boolean Implements ITypeSymbol.IsReferenceType
            Get
                Return PublicType.IsReferenceType
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsValueType As Boolean Implements ITypeSymbol.IsValueType
            Get
                Return PublicType.IsValueType
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsAnonymousType As Boolean Implements ITypeSymbol.IsAnonymousType
            Get
                Return PublicType.IsAnonymousType
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsTupleType As Boolean Implements ITypeSymbol.IsTupleType
            Get
                Return PublicType.IsTupleType
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsNativeIntegerType As Boolean Implements ITypeSymbol.IsNativeIntegerType
            Get
                Return PublicType.IsNativeIntegerType
            End Get
        End Property

        <Obsolete>
        Private ReadOnly Property ITypeSymbol_IsExtension As Boolean Implements ITypeSymbol.IsExtension
            Get
                Return PublicType.IsExtension
            End Get
        End Property

        <Obsolete>
        Private ReadOnly Property ITypeSymbol_ExtensionParameter As IParameterSymbol Implements ITypeSymbol.ExtensionParameter
            Get
                Return PublicType.ExtensionParameter
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_OriginalDefinition As ITypeSymbol Implements ITypeSymbol.OriginalDefinition
            Get
                Return PublicType.OriginalDefinition
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_SpecialType As SpecialType Implements ITypeSymbol.SpecialType
            Get
                Return PublicType.SpecialType
            End Get
        End Property

        Private Function ITypeSymbol_FindImplementationForInterfaceMember(interfaceMember As ISymbol) As ISymbol Implements ITypeSymbol.FindImplementationForInterfaceMember
            Return PublicType.FindImplementationForInterfaceMember(Unwrap(interfaceMember))
        End Function

        Private ReadOnly Property ITypeSymbol_IsRefLikeType As Boolean Implements ITypeSymbol.IsRefLikeType
            Get
                Return PublicType.IsRefLikeType
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsUnmanagedType As Boolean Implements ITypeSymbol.IsUnmanagedType
            Get
                Return PublicType.IsUnmanagedType
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsReadOnly As Boolean Implements ITypeSymbol.IsReadOnly
            Get
                Return PublicType.IsReadOnly
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsRecord As Boolean Implements ITypeSymbol.IsRecord
            Get
                Return PublicType.IsRecord
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsUnion As Boolean Implements ITypeSymbol.IsUnion
            Get
                Return PublicType.IsUnion
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_UnionCaseTypes As ImmutableArray(Of ITypeSymbol) Implements ITypeSymbol.UnionCaseTypes
            Get
                Return PublicType.UnionCaseTypes
            End Get
        End Property

        Private ReadOnly Property ITypeSymbol_IsClosed As Boolean Implements ITypeSymbol.IsClosed
            Get
                Return PublicType.IsClosed
            End Get
        End Property

        Private Function ITypeSymbol_GetClosedDerivedTypeInfo(cancellationToken As CancellationToken) As ClosedDerivedTypeInfo Implements ITypeSymbol.GetClosedDerivedTypeInfo
            Return PublicType.GetClosedDerivedTypeInfo(cancellationToken)
        End Function

        Private Function ITypeSymbol_ToDisplayString(topLevelNullability As NullableFlowState, Optional format As SymbolDisplayFormat = Nothing) As String Implements ITypeSymbol.ToDisplayString
            Return PublicType.ToDisplayString(topLevelNullability, format)
        End Function

        Private Function ITypeSymbol_ToDisplayParts(topLevelNullability As NullableFlowState, Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ITypeSymbol.ToDisplayParts
            Return PublicType.ToDisplayParts(topLevelNullability, format)
        End Function

        Private Function ITypeSymbol_ToMinimalDisplayString(semanticModel As SemanticModel, topLevelNullability As NullableFlowState, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As String Implements ITypeSymbol.ToMinimalDisplayString
            Return PublicType.ToMinimalDisplayString(semanticModel, topLevelNullability, position, format)
        End Function

        Private Function ITypeSymbol_ToMinimalDisplayParts(semanticModel As SemanticModel, topLevelNullability As NullableFlowState, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ITypeSymbol.ToMinimalDisplayParts
            Return PublicType.ToMinimalDisplayParts(semanticModel, topLevelNullability, position, format)
        End Function

        Private Class NamedTypeProjection
            Inherits PublicTypeProjection
            Implements INamedTypeSymbol

            Private ReadOnly _named As INamedTypeSymbol

            Public Sub New(underlying As NamedTypeSymbol, annotation As NullableAnnotation)
                MyBase.New(underlying, annotation)
                _named = underlying
            End Sub

            Private ReadOnly Property INamedTypeSymbol_Arity As Integer Implements INamedTypeSymbol.Arity
                Get
                    Return _named.Arity
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_IsGenericType As Boolean Implements INamedTypeSymbol.IsGenericType
                Get
                    Return _named.IsGenericType
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_IsUnboundGenericType As Boolean Implements INamedTypeSymbol.IsUnboundGenericType
                Get
                    Return _named.IsUnboundGenericType
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_IsScriptClass As Boolean Implements INamedTypeSymbol.IsScriptClass
                Get
                    Return _named.IsScriptClass
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_IsImplicitClass As Boolean Implements INamedTypeSymbol.IsImplicitClass
                Get
                    Return _named.IsImplicitClass
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_IsComImport As Boolean Implements INamedTypeSymbol.IsComImport
                Get
                    Return _named.IsComImport
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_IsFileLocal As Boolean Implements INamedTypeSymbol.IsFileLocal
                Get
                    Return _named.IsFileLocal
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_MemberNames As IEnumerable(Of String) Implements INamedTypeSymbol.MemberNames
                Get
                    Return _named.MemberNames
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_TypeParameters As ImmutableArray(Of ITypeParameterSymbol) Implements INamedTypeSymbol.TypeParameters
                Get
                    Return _named.TypeParameters
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_TypeArguments As ImmutableArray(Of ITypeSymbol) Implements INamedTypeSymbol.TypeArguments
                Get
                    Return _named.TypeArguments
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_TypeArgumentNullableAnnotations As ImmutableArray(Of NullableAnnotation) Implements INamedTypeSymbol.TypeArgumentNullableAnnotations
                Get
                    Return _named.TypeArgumentNullableAnnotations
                End Get
            End Property

            Private Function INamedTypeSymbol_GetTypeArgumentCustomModifiers(ordinal As Integer) As ImmutableArray(Of CustomModifier) Implements INamedTypeSymbol.GetTypeArgumentCustomModifiers
                Return _named.GetTypeArgumentCustomModifiers(ordinal)
            End Function

            Private ReadOnly Property INamedTypeSymbol_OriginalDefinition As INamedTypeSymbol Implements INamedTypeSymbol.OriginalDefinition
                Get
                    Return _named.OriginalDefinition
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_DelegateInvokeMethod As IMethodSymbol Implements INamedTypeSymbol.DelegateInvokeMethod
                Get
                    Return _named.DelegateInvokeMethod
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_EnumUnderlyingType As INamedTypeSymbol Implements INamedTypeSymbol.EnumUnderlyingType
                Get
                    Return _named.EnumUnderlyingType
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_ConstructedFrom As INamedTypeSymbol Implements INamedTypeSymbol.ConstructedFrom
                Get
                    Return _named.ConstructedFrom
                End Get
            End Property

            Private Function INamedTypeSymbol_Construct(ParamArray typeArguments() As ITypeSymbol) As INamedTypeSymbol Implements INamedTypeSymbol.Construct
                Return _named.Construct(typeArguments)
            End Function

            Private Function INamedTypeSymbol_Construct(typeArguments As ImmutableArray(Of ITypeSymbol), typeArgumentNullableAnnotations As ImmutableArray(Of NullableAnnotation)) As INamedTypeSymbol Implements INamedTypeSymbol.Construct
                Return _named.Construct(typeArguments, typeArgumentNullableAnnotations)
            End Function

            Private Function INamedTypeSymbol_ConstructUnboundGenericType() As INamedTypeSymbol Implements INamedTypeSymbol.ConstructUnboundGenericType
                Return _named.ConstructUnboundGenericType()
            End Function

            Private ReadOnly Property INamedTypeSymbol_InstanceConstructors As ImmutableArray(Of IMethodSymbol) Implements INamedTypeSymbol.InstanceConstructors
                Get
                    Return _named.InstanceConstructors
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_StaticConstructors As ImmutableArray(Of IMethodSymbol) Implements INamedTypeSymbol.StaticConstructors
                Get
                    Return _named.StaticConstructors
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_Constructors As ImmutableArray(Of IMethodSymbol) Implements INamedTypeSymbol.Constructors
                Get
                    Return _named.Constructors
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_AssociatedSymbol As ISymbol Implements INamedTypeSymbol.AssociatedSymbol
                Get
                    Return _named.AssociatedSymbol
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_MightContainExtensionMethods As Boolean Implements INamedTypeSymbol.MightContainExtensionMethods
                Get
                    Return _named.MightContainExtensionMethods
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_TupleUnderlyingType As INamedTypeSymbol Implements INamedTypeSymbol.TupleUnderlyingType
                Get
                    Return _named.TupleUnderlyingType
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_TupleElements As ImmutableArray(Of IFieldSymbol) Implements INamedTypeSymbol.TupleElements
                Get
                    Return _named.TupleElements
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_IsSerializable As Boolean Implements INamedTypeSymbol.IsSerializable
                Get
                    Return _named.IsSerializable
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_NativeIntegerUnderlyingType As INamedTypeSymbol Implements INamedTypeSymbol.NativeIntegerUnderlyingType
                Get
                    Return _named.NativeIntegerUnderlyingType
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_IsExtension As Boolean Implements INamedTypeSymbol.IsExtension
                Get
                    Return _named.IsExtension
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_ExtensionParameter As IParameterSymbol Implements INamedTypeSymbol.ExtensionParameter
                Get
                    Return _named.ExtensionParameter
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_ExtensionGroupingName As String Implements INamedTypeSymbol.ExtensionGroupingName
                Get
                    Return _named.ExtensionGroupingName
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_ExtensionMarkerName As String Implements INamedTypeSymbol.ExtensionMarkerName
                Get
                    Return _named.ExtensionMarkerName
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_TypeLayout As TypeLayout Implements INamedTypeSymbol.TypeLayout
                Get
                    Return _named.TypeLayout
                End Get
            End Property
        End Class

        Private NotInheritable Class ErrorTypeProjection
            Inherits NamedTypeProjection
            Implements IErrorTypeSymbol

            Private ReadOnly _error As IErrorTypeSymbol

            Public Sub New(underlying As ErrorTypeSymbol, annotation As NullableAnnotation)
                MyBase.New(underlying, annotation)
                _error = underlying
            End Sub

            Private ReadOnly Property IErrorTypeSymbol_CandidateSymbols As ImmutableArray(Of ISymbol) Implements IErrorTypeSymbol.CandidateSymbols
                Get
                    Return _error.CandidateSymbols
                End Get
            End Property

            Private ReadOnly Property IErrorTypeSymbol_CandidateReason As CandidateReason Implements IErrorTypeSymbol.CandidateReason
                Get
                    Return _error.CandidateReason
                End Get
            End Property
        End Class

        Private NotInheritable Class ArrayTypeProjection
            Inherits PublicTypeProjection
            Implements IArrayTypeSymbol

            Private ReadOnly _array As IArrayTypeSymbol
            Private ReadOnly _underlyingArray As ArrayTypeSymbol

            Public Sub New(underlying As ArrayTypeSymbol, annotation As NullableAnnotation)
                MyBase.New(underlying, annotation)
                _array = underlying
                _underlyingArray = underlying
            End Sub

            Private ReadOnly Property IArrayTypeSymbol_Rank As Integer Implements IArrayTypeSymbol.Rank
                Get
                    Return _array.Rank
                End Get
            End Property

            Private ReadOnly Property IArrayTypeSymbol_IsSZArray As Boolean Implements IArrayTypeSymbol.IsSZArray
                Get
                    Return _array.IsSZArray
                End Get
            End Property

            Private ReadOnly Property IArrayTypeSymbol_LowerBounds As ImmutableArray(Of Integer) Implements IArrayTypeSymbol.LowerBounds
                Get
                    Return _array.LowerBounds
                End Get
            End Property

            Private ReadOnly Property IArrayTypeSymbol_Sizes As ImmutableArray(Of Integer) Implements IArrayTypeSymbol.Sizes
                Get
                    Return _array.Sizes
                End Get
            End Property

            Private ReadOnly Property IArrayTypeSymbol_ElementType As ITypeSymbol Implements IArrayTypeSymbol.ElementType
                Get
                    Return _array.ElementType
                End Get
            End Property

            Private ReadOnly Property IArrayTypeSymbol_ElementNullableAnnotation As NullableAnnotation Implements IArrayTypeSymbol.ElementNullableAnnotation
                Get
                    Return _array.ElementNullableAnnotation
                End Get
            End Property

            Private ReadOnly Property IArrayTypeSymbol_CustomModifiers As ImmutableArray(Of CustomModifier) Implements IArrayTypeSymbol.CustomModifiers
                Get
                    Return _array.CustomModifiers
                End Get
            End Property

            Private Function IArrayTypeSymbol_Equals(other As IArrayTypeSymbol) As Boolean Implements IArrayTypeSymbol.Equals
                Return _underlyingArray.Equals(TryCast(Unwrap(other), ArrayTypeSymbol), TypeCompareKind.ConsiderEverything)
            End Function
        End Class

        Private NotInheritable Class TypeParameterProjection
            Inherits PublicTypeProjection
            Implements ITypeParameterSymbol

            Private ReadOnly _parameter As ITypeParameterSymbol

            Public Sub New(underlying As TypeParameterSymbol, annotation As NullableAnnotation)
                MyBase.New(underlying, annotation)
                _parameter = underlying
            End Sub

            Private ReadOnly Property ITypeParameterSymbol_Ordinal As Integer Implements ITypeParameterSymbol.Ordinal
                Get
                    Return _parameter.Ordinal
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_Variance As VarianceKind Implements ITypeParameterSymbol.Variance
                Get
                    Return _parameter.Variance
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_TypeParameterKind As TypeParameterKind Implements ITypeParameterSymbol.TypeParameterKind
                Get
                    Return _parameter.TypeParameterKind
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_DeclaringMethod As IMethodSymbol Implements ITypeParameterSymbol.DeclaringMethod
                Get
                    Return _parameter.DeclaringMethod
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_DeclaringType As INamedTypeSymbol Implements ITypeParameterSymbol.DeclaringType
                Get
                    Return _parameter.DeclaringType
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_HasReferenceTypeConstraint As Boolean Implements ITypeParameterSymbol.HasReferenceTypeConstraint
                Get
                    Return _parameter.HasReferenceTypeConstraint
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_ReferenceTypeConstraintNullableAnnotation As NullableAnnotation Implements ITypeParameterSymbol.ReferenceTypeConstraintNullableAnnotation
                Get
                    Return _parameter.ReferenceTypeConstraintNullableAnnotation
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_HasValueTypeConstraint As Boolean Implements ITypeParameterSymbol.HasValueTypeConstraint
                Get
                    Return _parameter.HasValueTypeConstraint
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_AllowsRefLikeType As Boolean Implements ITypeParameterSymbol.AllowsRefLikeType
                Get
                    Return _parameter.AllowsRefLikeType
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_HasUnmanagedTypeConstraint As Boolean Implements ITypeParameterSymbol.HasUnmanagedTypeConstraint
                Get
                    Return _parameter.HasUnmanagedTypeConstraint
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_HasNotNullConstraint As Boolean Implements ITypeParameterSymbol.HasNotNullConstraint
                Get
                    Return _parameter.HasNotNullConstraint
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_HasConstructorConstraint As Boolean Implements ITypeParameterSymbol.HasConstructorConstraint
                Get
                    Return _parameter.HasConstructorConstraint
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_ConstraintTypes As ImmutableArray(Of ITypeSymbol) Implements ITypeParameterSymbol.ConstraintTypes
                Get
                    Return _parameter.ConstraintTypes
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_ConstraintNullableAnnotations As ImmutableArray(Of NullableAnnotation) Implements ITypeParameterSymbol.ConstraintNullableAnnotations
                Get
                    Return _parameter.ConstraintNullableAnnotations
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_OriginalDefinition As ITypeParameterSymbol Implements ITypeParameterSymbol.OriginalDefinition
                Get
                    Return _parameter.OriginalDefinition
                End Get
            End Property

            Private ReadOnly Property ITypeParameterSymbol_ReducedFrom As ITypeParameterSymbol Implements ITypeParameterSymbol.ReducedFrom
                Get
                    Return _parameter.ReducedFrom
                End Get
            End Property
        End Class
    End Class
End Namespace
