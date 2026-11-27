' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Globalization
Imports System.Threading
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Roslyn.Utilities
Imports Display = Microsoft.CodeAnalysis.VisualBasic.SymbolDisplay

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Public representation of one annotated use of an existing compiler type.
    ''' The projection is not a <see cref="TypeSymbol"/> and is never stored in compiler caches.
    ''' </summary>
    Friend MustInherit Partial Class PublicTypeProjection
        Implements ITypeSymbol

        Private ReadOnly _underlying As TypeSymbol
        Private ReadOnly _annotation As NullableAnnotation

        Private Sub New(underlying As TypeSymbol, annotation As NullableAnnotation, Optional allowOblivious As Boolean = False)
            Debug.Assert(underlying IsNot Nothing)
            Debug.Assert(allowOblivious OrElse annotation <> NullableAnnotation.None)
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

        Friend Shared Function Project(type As TypeSymbol, annotation As NullableAnnotation) As ITypeSymbol
            If type Is Nothing Then
                Return Nothing
            End If

            Return Create(type, annotation)
        End Function

        Friend Shared Function ProjectAll(types As ImmutableArray(Of TypeSymbol), annotations As ImmutableArray(Of NullableAnnotation)) As ImmutableArray(Of ITypeSymbol)
            If types.IsDefault Then
                Return Nothing
            End If

            If types.IsEmpty Then
                Return ImmutableArray(Of ITypeSymbol).Empty
            End If

            Dim useAnnotations = Not annotations.IsDefault AndAlso annotations.Length = types.Length
            Dim builder = ArrayBuilder(Of ITypeSymbol).GetInstance(types.Length)
            For i = 0 To types.Length - 1
                Dim annotation = If(useAnnotations, annotations(i), NullableAnnotation.None)
                builder.Add(Project(types(i), annotation))
            Next

            Return builder.ToImmutableAndFree()
        End Function

        Friend Shared Function Unwrap(symbol As ISymbol) As ISymbol
            Dim projection = TryCast(symbol, PublicTypeProjection)
            If projection IsNot Nothing Then
                Return projection.Underlying
            End If

            Dim projected = TryCast(symbol, ProjectedSymbol)
            If projected IsNot Nothing Then
                Return projected.CompilerSymbol
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

        Private Structure TypePair
            Implements IEquatable(Of TypePair)

            Public ReadOnly Left As ITypeSymbol
            Public ReadOnly Right As ITypeSymbol

            Public Sub New(left As ITypeSymbol, right As ITypeSymbol)
                Me.Left = left
                Me.Right = right
            End Sub

            Public Overloads Function Equals(other As TypePair) As Boolean Implements IEquatable(Of TypePair).Equals
                Return Left Is other.Left AndAlso Right Is other.Right
            End Function

            Public Overrides Function Equals(obj As Object) As Boolean
                Return TypeOf obj Is TypePair AndAlso Equals(DirectCast(obj, TypePair))
            End Function

            Public Overrides Function GetHashCode() As Integer
                Return Hash.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Left), System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Right))
            End Function
        End Structure

        Friend Shared Function NestedAnnotationsEqual(left As ITypeSymbol, right As ITypeSymbol, compareKind As TypeCompareKind) As Boolean
            Return NestedAnnotationsEqual(left, right, compareKind, Nothing)
        End Function

        Private Shared Function NestedAnnotationsEqual(left As ITypeSymbol, right As ITypeSymbol, compareKind As TypeCompareKind, seen As HashSet(Of TypePair)) As Boolean
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

                Return NestedAnnotationsEqual(leftArray.ElementType, rightArray.ElementType, compareKind, seen)
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
                    If Not NestedAnnotationsEqual(leftArguments(i), rightArguments(i), compareKind, seen) Then
                        Return False
                    End If
                Next

                If leftNamed.IsAnonymousType AndAlso rightNamed.IsAnonymousType AndAlso Not AnonymousMembersEqual(leftNamed, rightNamed, compareKind, seen) Then
                    Return False
                End If

                Return NestedAnnotationsEqual(leftNamed.ContainingType, rightNamed.ContainingType, compareKind, seen)
            End If

            Dim leftParameter = TryCast(left, ITypeParameterSymbol)
            If leftParameter IsNot Nothing Then
                Dim rightParameter = TryCast(right, ITypeParameterSymbol)
                If rightParameter Is Nothing OrElse
                   Not AnnotationsEqual(leftParameter.ReferenceTypeConstraintNullableAnnotation, rightParameter.ReferenceTypeConstraintNullableAnnotation, compareKind, isSignificant:=True) OrElse
                   Not AnnotationListsEqual(leftParameter.ConstraintNullableAnnotations, rightParameter.ConstraintNullableAnnotations, leftParameter.ConstraintTypes, rightParameter.ConstraintTypes, compareKind) Then
                    Return False
                End If

                ' Type-parameter identity follows the shared contract: original definition and containing
                ' symbol. Constraint types are not traversed. Their public nullable annotations are the
                ' arrays compared above, and nested annotations are compared when those types are compared
                ' as types. C# type-parameter equality does not walk constraints either.
            End If

            Return True
        End Function

        Private Shared Function AnonymousMembersEqual(left As INamedTypeSymbol, right As INamedTypeSymbol, compareKind As TypeCompareKind, seen As HashSet(Of TypePair)) As Boolean
            For Each leftMember In left.GetMembers()
                Dim leftProperty = TryCast(leftMember, IPropertySymbol)
                If leftProperty Is Nothing OrElse leftProperty.IsImplicitlyDeclared Then
                    Continue For
                End If

                Dim rightProperty As IPropertySymbol = Nothing
                For Each rightMember In right.GetMembers(leftProperty.Name)
                    rightProperty = TryCast(rightMember, IPropertySymbol)
                    If rightProperty IsNot Nothing Then
                        Exit For
                    End If
                Next

                If rightProperty Is Nothing OrElse Not NestedAnnotationsEqual(leftProperty.Type, rightProperty.Type, compareKind, seen) Then
                    Return False
                End If
            Next

            Return True
        End Function

        Friend Shared Function AnyIndependentState(typeArguments As ImmutableArray(Of ITypeSymbol)) As Boolean
            If typeArguments.IsDefaultOrEmpty Then
                Return False
            End If

            For Each argument In typeArguments
                If HasIndependentState(argument) Then
                    Return True
                End If
            Next

            Return False
        End Function

        Friend Shared Function HasIndependentState(symbol As ISymbol) As Boolean
            Dim named = TryCast(symbol, NamedTypeProjection)
            If named IsNot Nothing Then
                Return Not named.ArgumentAnnotations.IsDefault OrElse named.ContainingOverride IsNot Nothing OrElse Not named.PublicArguments.IsDefault OrElse Not named.MemberTypes.IsDefault
            End If

            Dim array = TryCast(symbol, ArrayTypeProjection)
            Return array IsNot Nothing AndAlso array.HasElementOverride
        End Function

        Friend Shared Function PresentWithArguments(underlying As NamedTypeSymbol, arguments As ImmutableArray(Of ITypeSymbol)) As INamedTypeSymbol
            If underlying Is Nothing OrElse arguments.IsDefault Then
                Return underlying
            End If

            Dim annotations(arguments.Length - 1) As NullableAnnotation
            For i = 0 To arguments.Length - 1
                annotations(i) = If(arguments(i) Is Nothing, NullableAnnotation.None, arguments(i).NullableAnnotation)
            Next

            Return New NamedTypeProjection(underlying, NullableAnnotation.None, annotations.AsImmutableOrNull(), Nothing, arguments)
        End Function

        Friend Shared Function PresentArray(arrayType As ArrayTypeSymbol, annotation As NullableAnnotation, element As ITypeSymbol) As ITypeSymbol
            Return New ArrayTypeProjection(arrayType, annotation, element)
        End Function

        Friend Shared Function PresentAnonymous(underlying As NamedTypeSymbol, memberNames As ImmutableArray(Of String), memberTypes As ImmutableArray(Of ITypeSymbol)) As INamedTypeSymbol
            If underlying Is Nothing OrElse memberTypes.IsDefaultOrEmpty Then
                Return underlying
            End If

            Return New NamedTypeProjection(underlying, NullableAnnotation.None, Nothing, Nothing, Nothing, memberNames, memberTypes)
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
            Return ApplyNullableAnnotation(nullableAnnotation)
        End Function

        Protected Overridable Function ApplyNullableAnnotation(nullableAnnotation As NullableAnnotation) As ITypeSymbol
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
                Return GetProjectedContainingType()
            End Get
        End Property

        Protected Overridable Function GetProjectedContainingType() As INamedTypeSymbol
            Return PublicType.ContainingType
        End Function

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
            Return Display.ToDisplayString(Me, format)
        End Function

        Private Function ISymbol_ToDisplayParts(Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ISymbol.ToDisplayParts
            Return Display.ToDisplayParts(Me, format)
        End Function

        Private Function ISymbol_ToMinimalDisplayString(semanticModel As SemanticModel, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As String Implements ISymbol.ToMinimalDisplayString
            Return Display.ToMinimalDisplayString(Me, semanticModel, position, format)
        End Function

        Private Function ISymbol_ToMinimalDisplayParts(semanticModel As SemanticModel, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ISymbol.ToMinimalDisplayParts
            Return Display.ToMinimalDisplayParts(Me, semanticModel, position, format)
        End Function

        Private ReadOnly Property ISymbol_HasUnsupportedMetadata As Boolean Implements ISymbol.HasUnsupportedMetadata
            Get
                Return PublicType.HasUnsupportedMetadata
            End Get
        End Property

        Private Function INamespaceOrTypeSymbol_GetMembers() As ImmutableArray(Of ISymbol) Implements INamespaceOrTypeSymbol.GetMembers
            Return GetProjectedMembers()
        End Function

        Private Function INamespaceOrTypeSymbol_GetMembers(name As String) As ImmutableArray(Of ISymbol) Implements INamespaceOrTypeSymbol.GetMembers
            Return GetProjectedMembers(name)
        End Function

        Private Function INamespaceOrTypeSymbol_GetTypeMembers() As ImmutableArray(Of INamedTypeSymbol) Implements INamespaceOrTypeSymbol.GetTypeMembers
            Return GetProjectedTypeMembers()
        End Function

        Private Function INamespaceOrTypeSymbol_GetTypeMembers(name As String) As ImmutableArray(Of INamedTypeSymbol) Implements INamespaceOrTypeSymbol.GetTypeMembers
            Return GetProjectedTypeMembers(name)
        End Function

        Private Function INamespaceOrTypeSymbol_GetTypeMembers(name As String, arity As Integer) As ImmutableArray(Of INamedTypeSymbol) Implements INamespaceOrTypeSymbol.GetTypeMembers
            Return GetProjectedTypeMembers(name, arity)
        End Function

        Protected Overridable Function GetProjectedMembers() As ImmutableArray(Of ISymbol)
            Return PublicType.GetMembers()
        End Function

        Protected Overridable Function GetProjectedMembers(name As String) As ImmutableArray(Of ISymbol)
            Return PublicType.GetMembers(name)
        End Function

        Protected Overridable Function GetProjectedTypeMembers() As ImmutableArray(Of INamedTypeSymbol)
            Return PublicType.GetTypeMembers()
        End Function

        Protected Overridable Function GetProjectedTypeMembers(name As String) As ImmutableArray(Of INamedTypeSymbol)
            Return PublicType.GetTypeMembers(name)
        End Function

        Protected Overridable Function GetProjectedTypeMembers(name As String, arity As Integer) As ImmutableArray(Of INamedTypeSymbol)
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
            Return Display.ToDisplayString(Me, format)
        End Function

        Private Function ITypeSymbol_ToDisplayParts(topLevelNullability As NullableFlowState, Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ITypeSymbol.ToDisplayParts
            Return Display.ToDisplayParts(Me, format)
        End Function

        Private Function ITypeSymbol_ToMinimalDisplayString(semanticModel As SemanticModel, topLevelNullability As NullableFlowState, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As String Implements ITypeSymbol.ToMinimalDisplayString
            Return Display.ToMinimalDisplayString(Me, semanticModel, position, format)
        End Function

        Private Function ITypeSymbol_ToMinimalDisplayParts(semanticModel As SemanticModel, topLevelNullability As NullableFlowState, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ITypeSymbol.ToMinimalDisplayParts
            Return Display.ToMinimalDisplayParts(Me, semanticModel, position, format)
        End Function

        Private Class NamedTypeProjection
            Inherits PublicTypeProjection
            Implements INamedTypeSymbol

            Private ReadOnly _named As INamedTypeSymbol
            Private ReadOnly _argumentAnnotations As ImmutableArray(Of NullableAnnotation)
            Private ReadOnly _containingOverride As INamedTypeSymbol
            Private ReadOnly _publicArguments As ImmutableArray(Of ITypeSymbol)
            Private ReadOnly _memberNames As ImmutableArray(Of String)
            Private ReadOnly _memberTypes As ImmutableArray(Of ITypeSymbol)

            Public Sub New(underlying As NamedTypeSymbol,
                           annotation As NullableAnnotation,
                           Optional argumentAnnotations As ImmutableArray(Of NullableAnnotation) = Nothing,
                           Optional containingOverride As INamedTypeSymbol = Nothing,
                           Optional publicArguments As ImmutableArray(Of ITypeSymbol) = Nothing,
                           Optional memberNames As ImmutableArray(Of String) = Nothing,
                           Optional memberTypes As ImmutableArray(Of ITypeSymbol) = Nothing)
                MyBase.New(underlying, annotation, allowOblivious:=Not argumentAnnotations.IsDefault OrElse containingOverride IsNot Nothing OrElse Not publicArguments.IsDefault OrElse Not memberTypes.IsDefault)
                _named = underlying
                _argumentAnnotations = argumentAnnotations
                _containingOverride = containingOverride
                _publicArguments = publicArguments
                _memberNames = memberNames
                _memberTypes = memberTypes
            End Sub

            Friend ReadOnly Property ArgumentAnnotations As ImmutableArray(Of NullableAnnotation)
                Get
                    Return _argumentAnnotations
                End Get
            End Property

            Friend ReadOnly Property ContainingOverride As INamedTypeSymbol
                Get
                    Return _containingOverride
                End Get
            End Property

            Friend ReadOnly Property PublicArguments As ImmutableArray(Of ITypeSymbol)
                Get
                    Return _publicArguments
                End Get
            End Property

            Friend ReadOnly Property MemberTypes As ImmutableArray(Of ITypeSymbol)
                Get
                    Return _memberTypes
                End Get
            End Property

            Private Function NeedsMemberViews() As Boolean
                Return Not _argumentAnnotations.IsDefault OrElse TypeOf _containingOverride Is NamedTypeProjection OrElse Not _publicArguments.IsDefault OrElse Not _memberTypes.IsDefault
            End Function

            Private Function ExplicitMemberType(name As String) As ITypeSymbol
                If _memberNames.IsDefault OrElse _memberTypes.IsDefault Then
                    Return Nothing
                End If

                For i = 0 To _memberNames.Length - 1
                    If String.Equals(_memberNames(i), name, StringComparison.Ordinal) Then
                        Return _memberTypes(i)
                    End If
                Next

                Return Nothing
            End Function

            Protected Overrides Function GetProjectedContainingType() As INamedTypeSymbol
                If _containingOverride IsNot Nothing Then
                    Return _containingOverride
                End If

                Return MyBase.GetProjectedContainingType()
            End Function

            Protected Overrides Function ApplyNullableAnnotation(nullableAnnotation As NullableAnnotation) As ITypeSymbol
                If nullableAnnotation = Annotation Then
                    Return Me
                End If

                If nullableAnnotation = NullableAnnotation.None AndAlso _argumentAnnotations.IsDefault AndAlso _containingOverride Is Nothing AndAlso _publicArguments.IsDefault AndAlso _memberTypes.IsDefault Then
                    Return Underlying
                End If

                Return New NamedTypeProjection(DirectCast(Underlying, NamedTypeSymbol), nullableAnnotation, _argumentAnnotations, _containingOverride, _publicArguments, _memberNames, _memberTypes)
            End Function

            Protected Overrides Function GetProjectedMembers() As ImmutableArray(Of ISymbol)
                Return WrapMembers(MyBase.GetProjectedMembers())
            End Function

            Protected Overrides Function GetProjectedMembers(name As String) As ImmutableArray(Of ISymbol)
                Return WrapMembers(MyBase.GetProjectedMembers(name))
            End Function

            Protected Overrides Function GetProjectedTypeMembers() As ImmutableArray(Of INamedTypeSymbol)
                Return WrapTypes(MyBase.GetProjectedTypeMembers())
            End Function

            Protected Overrides Function GetProjectedTypeMembers(name As String) As ImmutableArray(Of INamedTypeSymbol)
                Return WrapTypes(MyBase.GetProjectedTypeMembers(name))
            End Function

            Protected Overrides Function GetProjectedTypeMembers(name As String, arity As Integer) As ImmutableArray(Of INamedTypeSymbol)
                Return WrapTypes(MyBase.GetProjectedTypeMembers(name, arity))
            End Function

            Private Function WrapMembers(members As ImmutableArray(Of ISymbol)) As ImmutableArray(Of ISymbol)
                If Not NeedsMemberViews() OrElse members.IsDefaultOrEmpty Then
                    Return members
                End If

                Dim builder = ArrayBuilder(Of ISymbol).GetInstance(members.Length)
                For Each member In members
                    builder.Add(WrapMember(member))
                Next

                Return builder.ToImmutableAndFree()
            End Function

            Private Function WrapMember(member As ISymbol) As ISymbol
                Dim explicitType = ExplicitMemberType(member.Name)
                Dim field = TryCast(member, IFieldSymbol)
                If field IsNot Nothing Then
                    Return New IdentityFieldView(field, Me, explicitType)
                End If

                Dim propertySymbol = TryCast(member, IPropertySymbol)
                If propertySymbol IsNot Nothing Then
                    Return New IdentityPropertyView(propertySymbol, Me, explicitType)
                End If

                Dim method = TryCast(member, IMethodSymbol)
                If method IsNot Nothing Then
                    Return New IdentityMethodView(method, Nothing, Me)
                End If

                Dim eventSymbol = TryCast(member, IEventSymbol)
                If eventSymbol IsNot Nothing Then
                    Return New IdentityEventView(eventSymbol, Me)
                End If

                Return member
            End Function

            Private Function WrapTypes(types As ImmutableArray(Of INamedTypeSymbol)) As ImmutableArray(Of INamedTypeSymbol)
                If Not NeedsMemberViews() OrElse types.IsDefaultOrEmpty Then
                    Return types
                End If

                Dim builder = ArrayBuilder(Of INamedTypeSymbol).GetInstance(types.Length)
                For Each nested In types
                    Dim compiler = TryCast(Unwrap(nested), NamedTypeSymbol)
                    If compiler Is Nothing Then
                        builder.Add(nested)
                    Else
                        builder.Add(New NamedTypeProjection(compiler, nested.NullableAnnotation, Nothing, Me))
                    End If
                Next

                Return builder.ToImmutableAndFree()
            End Function

            Private Function FinishConstruct(built As INamedTypeSymbol, requested As ImmutableArray(Of NullableAnnotation)) As INamedTypeSymbol
                Dim compiler = TryCast(Unwrap(built), NamedTypeSymbol)
                If compiler Is Nothing Then
                    Return built
                End If

                Dim overlay = requested
                If overlay.IsDefault OrElse Not AnnotationsDiffer(compiler.GetTypeArgumentNullableAnnotations(), overlay) OrElse compiler IsNot DirectCast(Underlying, NamedTypeSymbol) Then
                    overlay = Nothing
                End If

                Dim containing = _containingOverride
                If overlay.IsDefault AndAlso containing Is Nothing Then
                    Return built
                End If

                Dim top = Annotation
                If top = NullableAnnotation.None Then
                    top = built.NullableAnnotation
                End If

                Return New NamedTypeProjection(compiler, top, overlay, containing, _publicArguments, _memberNames, _memberTypes)
            End Function

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
                    If Not _publicArguments.IsDefault Then
                        Return _publicArguments
                    End If

                    If Not _argumentAnnotations.IsDefault Then
                        Return ProjectAll(DirectCast(Underlying, NamedTypeSymbol).TypeArgumentsNoUseSiteDiagnostics, _argumentAnnotations)
                    End If

                    Return _named.TypeArguments
                End Get
            End Property

            Private ReadOnly Property INamedTypeSymbol_TypeArgumentNullableAnnotations As ImmutableArray(Of NullableAnnotation) Implements INamedTypeSymbol.TypeArgumentNullableAnnotations
                Get
                    If Not _argumentAnnotations.IsDefault Then
                        Return _argumentAnnotations
                    End If

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
                Return FinishConstruct(_named.Construct(typeArguments), CaptureAnnotations(typeArguments.AsImmutableOrNull()))
            End Function

            Private Function INamedTypeSymbol_Construct(typeArguments As ImmutableArray(Of ITypeSymbol), typeArgumentNullableAnnotations As ImmutableArray(Of NullableAnnotation)) As INamedTypeSymbol Implements INamedTypeSymbol.Construct
                Return FinishConstruct(_named.Construct(typeArguments, typeArgumentNullableAnnotations), typeArgumentNullableAnnotations)
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
                    Dim elements = _named.TupleElements
                    If _memberTypes.IsDefault OrElse elements.IsDefaultOrEmpty Then
                        Return elements
                    End If

                    Dim builder = ArrayBuilder(Of IFieldSymbol).GetInstance(elements.Length)
                    For i = 0 To elements.Length - 1
                        Dim explicitType = If(i < _memberTypes.Length, _memberTypes(i), Nothing)
                        builder.Add(New IdentityFieldView(elements(i), Me, explicitType))
                    Next

                    Return builder.ToImmutableAndFree()
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
            Private ReadOnly _elementOverride As ITypeSymbol

            Public Sub New(underlying As ArrayTypeSymbol, annotation As NullableAnnotation, Optional elementOverride As ITypeSymbol = Nothing)
                MyBase.New(underlying, annotation, allowOblivious:=elementOverride IsNot Nothing)
                _array = underlying
                _underlyingArray = underlying
                _elementOverride = elementOverride
            End Sub

            Friend ReadOnly Property HasElementOverride As Boolean
                Get
                    Return _elementOverride IsNot Nothing
                End Get
            End Property

            Protected Overrides Function ApplyNullableAnnotation(nullableAnnotation As NullableAnnotation) As ITypeSymbol
                If nullableAnnotation = Annotation Then
                    Return Me
                End If

                If nullableAnnotation = NullableAnnotation.None AndAlso _elementOverride Is Nothing Then
                    Return Underlying
                End If

                Return New ArrayTypeProjection(_underlyingArray, nullableAnnotation, _elementOverride)
            End Function

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
                    If _elementOverride IsNot Nothing Then
                        Return _elementOverride
                    End If

                    Return _array.ElementType
                End Get
            End Property

            Private ReadOnly Property IArrayTypeSymbol_ElementNullableAnnotation As NullableAnnotation Implements IArrayTypeSymbol.ElementNullableAnnotation
                Get
                    If _elementOverride IsNot Nothing Then
                        Return _elementOverride.NullableAnnotation
                    End If

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
