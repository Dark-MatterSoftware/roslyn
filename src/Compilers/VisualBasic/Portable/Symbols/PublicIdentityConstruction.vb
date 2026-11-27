' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Globalization
Imports System.Threading
Imports Microsoft.CodeAnalysis.PooledObjects

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    Partial Friend MustInherit Class PublicTypeProjection
        Friend Shared Function AnnotationsDiffer(left As ImmutableArray(Of NullableAnnotation), right As ImmutableArray(Of NullableAnnotation)) As Boolean
            If right.IsDefault Then
                Return False
            End If

            If left.IsDefault OrElse left.Length <> right.Length Then
                Return True
            End If

            For i = 0 To left.Length - 1
                If left(i) <> right(i) Then
                    Return True
                End If
            Next

            Return False
        End Function

        Friend Shared Function PresentNamedIdentity(definition As NamedTypeSymbol, annotations As ImmutableArray(Of NullableAnnotation)) As INamedTypeSymbol
            If definition Is Nothing Then
                Return Nothing
            End If

            If annotations.IsDefault OrElse Not AnnotationsDiffer(definition.GetTypeArgumentNullableAnnotations(), annotations) Then
                Return definition
            End If

            Return New NamedTypeProjection(definition, NullableAnnotation.None, annotations, Nothing)
        End Function

        Friend Shared Function PresentMethodIdentity(definition As MethodSymbol, annotations As ImmutableArray(Of NullableAnnotation)) As IMethodSymbol
            Return PresentMethodIdentity(definition, annotations, owner:=Nothing)
        End Function

        Private Shared Function PresentMethodIdentity(definition As MethodSymbol, annotations As ImmutableArray(Of NullableAnnotation), owner As NamedTypeProjection) As IMethodSymbol
            If definition Is Nothing Then
                Return Nothing
            End If

            Dim current = DirectCast(definition, IMethodSymbol).TypeArgumentNullableAnnotations
            If annotations.IsDefault OrElse Not AnnotationsDiffer(current, annotations) Then
                If owner Is Nothing Then
                    Return definition
                End If

                Return New IdentityMethodView(definition, Nothing, owner)
            End If

            Return New IdentityMethodView(definition, annotations, owner)
        End Function

        Private Shared Function SubstituteMemberType(type As ITypeSymbol,
                                                     declaredAnnotation As NullableAnnotation,
                                                     owner As NamedTypeProjection,
                                                     methodDefinition As MethodSymbol,
                                                     methodAnnotations As ImmutableArray(Of NullableAnnotation)) As ITypeSymbol
            Return SubstituteMemberType(type, declaredAnnotation, owner, methodDefinition, methodAnnotations, depth:=0)
        End Function

        Private Shared Function SubstituteMemberType(type As ITypeSymbol,
                                                      declaredAnnotation As NullableAnnotation,
                                                      owner As NamedTypeProjection,
                                                      methodDefinition As MethodSymbol,
                                                      methodAnnotations As ImmutableArray(Of NullableAnnotation),
                                                      depth As Integer) As ITypeSymbol
            If type Is Nothing OrElse depth > 8 Then
                Return type
            End If

            Dim compiler = TryCast(Unwrap(type), TypeSymbol)
            If compiler Is Nothing Then
                Return type
            End If

            Dim parameter = TryCast(compiler, TypeParameterSymbol)
            If parameter IsNot Nothing Then
                If owner IsNot Nothing AndAlso Not owner.PublicArguments.IsDefault AndAlso OwnsNamedParameter(parameter, owner) AndAlso parameter.Ordinal < owner.PublicArguments.Length Then
                    Dim argument = owner.PublicArguments(parameter.Ordinal)
                    If declaredAnnotation = NullableAnnotation.Annotated AndAlso argument IsNot Nothing AndAlso argument.NullableAnnotation <> NullableAnnotation.Annotated AndAlso Not parameter.IsValueType Then
                        Return argument.WithNullableAnnotation(NullableAnnotation.Annotated)
                    End If

                    Return argument
                End If

                Dim annotation = ResolveParameterAnnotation(parameter, declaredAnnotation, owner, methodDefinition, methodAnnotations)
                If annotation = type.NullableAnnotation Then
                    Return type
                End If

                Return Project(parameter, annotation)
            End If

            Dim replaced = ReplaceWithPublicArgument(compiler, declaredAnnotation, owner)
            If replaced IsNot Nothing Then
                Return replaced
            End If

            Dim arrayType = TryCast(compiler, ArrayTypeSymbol)
            If arrayType IsNot Nothing Then
                Dim publicArray = DirectCast(type, IArrayTypeSymbol)
                Dim element = SubstituteMemberType(publicArray.ElementType, publicArray.ElementNullableAnnotation, owner, methodDefinition, methodAnnotations, depth + 1)
                If element Is publicArray.ElementType AndAlso type.NullableAnnotation = NullableAnnotation.None Then
                    Return type
                End If

                If element Is publicArray.ElementType Then
                    Return Create(arrayType, type.NullableAnnotation)
                End If

                Return New ArrayTypeProjection(arrayType, type.NullableAnnotation, element)
            End If

            Dim named = TryCast(compiler, NamedTypeSymbol)
            If named Is Nothing OrElse named.Arity = 0 Then
                Return type
            End If

            Dim publicNamed = DirectCast(type, INamedTypeSymbol)
            Dim arguments = publicNamed.TypeArguments
            Dim argumentAnnotations = publicNamed.TypeArgumentNullableAnnotations
            Dim changed = False
            Dim anyIndependent = False
            Dim rebuilt(arguments.Length - 1) As NullableAnnotation
            Dim rebuiltArguments(arguments.Length - 1) As ITypeSymbol
            For i = 0 To arguments.Length - 1
                Dim substituted = SubstituteMemberType(arguments(i), argumentAnnotations(i), owner, methodDefinition, methodAnnotations, depth + 1)
                rebuiltArguments(i) = substituted
                rebuilt(i) = substituted.NullableAnnotation
                If HasIndependentState(substituted) Then
                    anyIndependent = True
                End If

                If substituted IsNot arguments(i) OrElse rebuilt(i) <> argumentAnnotations(i) Then
                    changed = True
                End If
            Next

            Dim containingOverride As INamedTypeSymbol = Nothing
            If publicNamed.ContainingType IsNot Nothing Then
                Dim substitutedContaining = SubstituteMemberType(publicNamed.ContainingType, publicNamed.ContainingType.NullableAnnotation, owner, methodDefinition, methodAnnotations, depth + 1)
                containingOverride = TryCast(substitutedContaining, NamedTypeProjection)
                If substitutedContaining IsNot publicNamed.ContainingType Then
                    changed = True
                End If
            End If

            If Not changed Then
                Return type
            End If

            Dim publicArguments = If(anyIndependent, rebuiltArguments.AsImmutableOrNull(), Nothing)
            Return New NamedTypeProjection(named, type.NullableAnnotation, rebuilt.AsImmutableOrNull(), containingOverride, publicArguments)
        End Function

        Private Shared Function ReplaceWithPublicArgument(compiler As TypeSymbol, declaredAnnotation As NullableAnnotation, owner As NamedTypeProjection) As ITypeSymbol
            If compiler Is Nothing OrElse owner Is Nothing OrElse owner.PublicArguments.IsDefault Then
                Return Nothing
            End If

            For Each argument In owner.PublicArguments
                If argument Is Nothing OrElse Not HasIndependentState(argument) OrElse Unwrap(argument) IsNot compiler Then
                    Continue For
                End If

                If declaredAnnotation = NullableAnnotation.Annotated AndAlso argument.NullableAnnotation <> NullableAnnotation.Annotated AndAlso Not compiler.IsValueType Then
                    Return argument.WithNullableAnnotation(NullableAnnotation.Annotated)
                End If

                Return argument
            Next

            Return Nothing
        End Function

        Private Shared Function ResolveParameterAnnotation(parameter As TypeParameterSymbol,
                                                            declaredAnnotation As NullableAnnotation,
                                                            owner As NamedTypeProjection,
                                                            methodDefinition As MethodSymbol,
                                                            methodAnnotations As ImmutableArray(Of NullableAnnotation)) As NullableAnnotation
            If parameter.IsValueType Then
                Return NullableAnnotation.None
            End If

            If declaredAnnotation = NullableAnnotation.Annotated Then
                Return NullableAnnotation.Annotated
            End If

            If methodDefinition IsNot Nothing AndAlso OwnsMethodParameter(parameter, methodDefinition) AndAlso
               Not methodAnnotations.IsDefault AndAlso parameter.Ordinal < methodAnnotations.Length AndAlso
               methodAnnotations(parameter.Ordinal) <> NullableAnnotation.None Then
                Return methodAnnotations(parameter.Ordinal)
            End If

            If owner IsNot Nothing Then
                Dim requested = RequestedFromOwner(parameter, owner)
                If requested <> NullableAnnotation.None Then
                    Return requested
                End If
            End If

            Return declaredAnnotation
        End Function

        Private Shared Function RequestedFromOwner(parameter As TypeParameterSymbol, owner As NamedTypeProjection) As NullableAnnotation
            Dim current = owner
            Dim guard = 0
            While current IsNot Nothing AndAlso guard < 8
                guard += 1
                If OwnsNamedParameter(parameter, current) Then
                    Dim annotations = current.ArgumentAnnotations
                    If Not annotations.IsDefault AndAlso parameter.Ordinal < annotations.Length AndAlso annotations(parameter.Ordinal) <> NullableAnnotation.None Then
                        Return annotations(parameter.Ordinal)
                    End If
                End If

                current = TryCast(current.ContainingOverride, NamedTypeProjection)
            End While

            Return NullableAnnotation.None
        End Function

        Private Shared Function OwnsNamedParameter(parameter As TypeParameterSymbol, projection As NamedTypeProjection) As Boolean
            Dim parameterOwner = TryCast(parameter.ContainingSymbol, NamedTypeSymbol)
            Dim definition = TryCast(projection.Underlying, NamedTypeSymbol)
            Return parameterOwner IsNot Nothing AndAlso definition IsNot Nothing AndAlso parameterOwner.OriginalDefinition Is definition.OriginalDefinition
        End Function

        Private Shared Function OwnsMethodParameter(parameter As TypeParameterSymbol, method As MethodSymbol) As Boolean
            Dim parameterOwner = TryCast(parameter.ContainingSymbol, MethodSymbol)
            Return parameterOwner IsNot Nothing AndAlso parameterOwner.OriginalDefinition Is method.OriginalDefinition
        End Function

        Private MustInherit Class ProjectedSymbol
            Implements ISymbol

            Private ReadOnly _symbol As ISymbol
            Private ReadOnly _owner As NamedTypeProjection

            Protected Sub New(symbol As ISymbol, owner As NamedTypeProjection)
                _symbol = symbol
                _owner = owner
            End Sub

            Friend ReadOnly Property CompilerSymbol As ISymbol
                Get
                    Return _symbol
                End Get
            End Property

            Protected ReadOnly Property Owner As NamedTypeProjection
                Get
                    Return _owner
                End Get
            End Property

            Public Overrides Function Equals(obj As Object) As Boolean
                Return Equals(TryCast(obj, ISymbol), SymbolEqualityComparer.Default)
            End Function

            Public Overrides Function GetHashCode() As Integer
                Return _symbol.GetHashCode()
            End Function

            Protected Overridable Function EqualsCore(other As ISymbol, comparer As SymbolEqualityComparer) As Boolean
                Return False
            End Function

            Protected Overridable Function GetContainingSymbol() As ISymbol
                If _owner IsNot Nothing Then
                    Return _owner
                End If

                Return _symbol.ContainingSymbol
            End Function

            Private Function IEquatable_Equals(other As ISymbol) As Boolean Implements IEquatable(Of ISymbol).Equals
                Return Equals(other, SymbolEqualityComparer.Default)
            End Function

            Private Function ISymbol_Equals(other As ISymbol, equalityComparer As SymbolEqualityComparer) As Boolean Implements ISymbol.Equals
                If other Is Me Then
                    Return True
                End If

                Return EqualsCore(other, equalityComparer)
            End Function

            Private ReadOnly Property ISymbol_Kind As SymbolKind Implements ISymbol.Kind
                Get
                    Return _symbol.Kind
                End Get
            End Property

            Private ReadOnly Property ISymbol_Language As String Implements ISymbol.Language
                Get
                    Return _symbol.Language
                End Get
            End Property

            Private ReadOnly Property ISymbol_Name As String Implements ISymbol.Name
                Get
                    Return _symbol.Name
                End Get
            End Property

            Private ReadOnly Property ISymbol_MetadataName As String Implements ISymbol.MetadataName
                Get
                    Return _symbol.MetadataName
                End Get
            End Property

            Private ReadOnly Property ISymbol_MetadataToken As Integer Implements ISymbol.MetadataToken
                Get
                    Return _symbol.MetadataToken
                End Get
            End Property

            Private ReadOnly Property ISymbol_ContainingSymbol As ISymbol Implements ISymbol.ContainingSymbol
                Get
                    Return GetContainingSymbol()
                End Get
            End Property

            Private ReadOnly Property ISymbol_ContainingAssembly As IAssemblySymbol Implements ISymbol.ContainingAssembly
                Get
                    Return _symbol.ContainingAssembly
                End Get
            End Property

            Private ReadOnly Property ISymbol_ContainingModule As IModuleSymbol Implements ISymbol.ContainingModule
                Get
                    Return _symbol.ContainingModule
                End Get
            End Property

            Private ReadOnly Property ISymbol_ContainingType As INamedTypeSymbol Implements ISymbol.ContainingType
                Get
                    If _owner IsNot Nothing Then
                        Return _owner
                    End If

                    Return _symbol.ContainingType
                End Get
            End Property

            Private ReadOnly Property ISymbol_ContainingNamespace As INamespaceSymbol Implements ISymbol.ContainingNamespace
                Get
                    Return _symbol.ContainingNamespace
                End Get
            End Property

            Private ReadOnly Property ISymbol_IsDefinition As Boolean Implements ISymbol.IsDefinition
                Get
                    Return False
                End Get
            End Property

            Private ReadOnly Property ISymbol_IsStatic As Boolean Implements ISymbol.IsStatic
                Get
                    Return _symbol.IsStatic
                End Get
            End Property

            Private ReadOnly Property ISymbol_IsVirtual As Boolean Implements ISymbol.IsVirtual
                Get
                    Return _symbol.IsVirtual
                End Get
            End Property

            Private ReadOnly Property ISymbol_IsOverride As Boolean Implements ISymbol.IsOverride
                Get
                    Return _symbol.IsOverride
                End Get
            End Property

            Private ReadOnly Property ISymbol_IsAbstract As Boolean Implements ISymbol.IsAbstract
                Get
                    Return _symbol.IsAbstract
                End Get
            End Property

            Private ReadOnly Property ISymbol_IsSealed As Boolean Implements ISymbol.IsSealed
                Get
                    Return _symbol.IsSealed
                End Get
            End Property

            Private ReadOnly Property ISymbol_IsExtern As Boolean Implements ISymbol.IsExtern
                Get
                    Return _symbol.IsExtern
                End Get
            End Property

            Private ReadOnly Property ISymbol_IsImplicitlyDeclared As Boolean Implements ISymbol.IsImplicitlyDeclared
                Get
                    Return _symbol.IsImplicitlyDeclared
                End Get
            End Property

            Private ReadOnly Property ISymbol_RequiresUnsafeContext As Boolean Implements ISymbol.RequiresUnsafeContext
                Get
                    Return _symbol.RequiresUnsafeContext
                End Get
            End Property

            Private ReadOnly Property ISymbol_CanBeReferencedByName As Boolean Implements ISymbol.CanBeReferencedByName
                Get
                    Return _symbol.CanBeReferencedByName
                End Get
            End Property

            Private ReadOnly Property ISymbol_Locations As ImmutableArray(Of Location) Implements ISymbol.Locations
                Get
                    Return _symbol.Locations
                End Get
            End Property

            Private ReadOnly Property ISymbol_DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference) Implements ISymbol.DeclaringSyntaxReferences
                Get
                    Return _symbol.DeclaringSyntaxReferences
                End Get
            End Property

            Private Function ISymbol_GetAttributes() As ImmutableArray(Of AttributeData) Implements ISymbol.GetAttributes
                Return _symbol.GetAttributes()
            End Function

            Private ReadOnly Property ISymbol_DeclaredAccessibility As Accessibility Implements ISymbol.DeclaredAccessibility
                Get
                    Return _symbol.DeclaredAccessibility
                End Get
            End Property

            Private ReadOnly Property ISymbol_OriginalDefinition As ISymbol Implements ISymbol.OriginalDefinition
                Get
                    Return _symbol.OriginalDefinition
                End Get
            End Property

            Private ReadOnly Property ISymbol_HasUnsupportedMetadata As Boolean Implements ISymbol.HasUnsupportedMetadata
                Get
                    Return _symbol.HasUnsupportedMetadata
                End Get
            End Property

            Private Function ISymbol_GetDocumentationCommentId() As String Implements ISymbol.GetDocumentationCommentId
                Return _symbol.GetDocumentationCommentId()
            End Function

            Private Function ISymbol_GetDocumentationCommentXml(Optional preferredCulture As CultureInfo = Nothing, Optional expandIncludes As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As String Implements ISymbol.GetDocumentationCommentXml
                Return _symbol.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken)
            End Function

            Private Function ISymbol_ToDisplayString(Optional format As SymbolDisplayFormat = Nothing) As String Implements ISymbol.ToDisplayString
                Return _symbol.ToDisplayString(format)
            End Function

            Private Function ISymbol_ToDisplayParts(Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ISymbol.ToDisplayParts
                Return _symbol.ToDisplayParts(format)
            End Function

            Private Function ISymbol_ToMinimalDisplayString(semanticModel As SemanticModel, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As String Implements ISymbol.ToMinimalDisplayString
                Return _symbol.ToMinimalDisplayString(semanticModel, position, format)
            End Function

            Private Function ISymbol_ToMinimalDisplayParts(semanticModel As SemanticModel, position As Integer, Optional format As SymbolDisplayFormat = Nothing) As ImmutableArray(Of SymbolDisplayPart) Implements ISymbol.ToMinimalDisplayParts
                Return _symbol.ToMinimalDisplayParts(semanticModel, position, format)
            End Function

            Private Sub ISymbol_Accept(visitor As SymbolVisitor) Implements ISymbol.Accept
                AcceptCore(visitor)
            End Sub

            Private Function ISymbol_Accept(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult Implements ISymbol.Accept
                Return AcceptCore(visitor)
            End Function

            Private Function ISymbol_Accept(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult Implements ISymbol.Accept
                Return AcceptCore(visitor, argument)
            End Function

            Protected Overridable Sub AcceptCore(visitor As SymbolVisitor)
                visitor.DefaultVisit(Me)
            End Sub

            Protected Overridable Function AcceptCore(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
                Return visitor.DefaultVisit(Me)
            End Function

            Protected Overridable Function AcceptCore(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
                Return visitor.DefaultVisit(Me, argument)
            End Function
        End Class

        Private NotInheritable Class IdentityFieldView
            Inherits ProjectedSymbol
            Implements IFieldSymbol

            Private ReadOnly _field As IFieldSymbol
            Private ReadOnly _type As ITypeSymbol

            Friend Sub New(field As IFieldSymbol, owner As NamedTypeProjection, Optional explicitType As ITypeSymbol = Nothing)
                MyBase.New(field, owner)
                _field = field
                _type = If(explicitType, SubstituteMemberType(field.Type, field.NullableAnnotation, owner, Nothing, Nothing))
            End Sub

            Private ReadOnly Property IFieldSymbol_Type As ITypeSymbol Implements IFieldSymbol.Type
                Get
                    Return _type
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_NullableAnnotation As NullableAnnotation Implements IFieldSymbol.NullableAnnotation
                Get
                    Return _type.NullableAnnotation
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_AssociatedSymbol As ISymbol Implements IFieldSymbol.AssociatedSymbol
                Get
                    Return _field.AssociatedSymbol
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_IsConst As Boolean Implements IFieldSymbol.IsConst
                Get
                    Return _field.IsConst
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_IsReadOnly As Boolean Implements IFieldSymbol.IsReadOnly
                Get
                    Return _field.IsReadOnly
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_IsVolatile As Boolean Implements IFieldSymbol.IsVolatile
                Get
                    Return _field.IsVolatile
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_IsRequired As Boolean Implements IFieldSymbol.IsRequired
                Get
                    Return _field.IsRequired
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_IsFixedSizeBuffer As Boolean Implements IFieldSymbol.IsFixedSizeBuffer
                Get
                    Return _field.IsFixedSizeBuffer
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_FixedSize As Integer Implements IFieldSymbol.FixedSize
                Get
                    Return _field.FixedSize
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_RefKind As RefKind Implements IFieldSymbol.RefKind
                Get
                    Return _field.RefKind
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_RefCustomModifiers As ImmutableArray(Of CustomModifier) Implements IFieldSymbol.RefCustomModifiers
                Get
                    Return _field.RefCustomModifiers
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_HasConstantValue As Boolean Implements IFieldSymbol.HasConstantValue
                Get
                    Return _field.HasConstantValue
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_ConstantValue As Object Implements IFieldSymbol.ConstantValue
                Get
                    Return _field.ConstantValue
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_CustomModifiers As ImmutableArray(Of CustomModifier) Implements IFieldSymbol.CustomModifiers
                Get
                    Return _field.CustomModifiers
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_OriginalDefinition As IFieldSymbol Implements IFieldSymbol.OriginalDefinition
                Get
                    Return _field.OriginalDefinition
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_CorrespondingTupleField As IFieldSymbol Implements IFieldSymbol.CorrespondingTupleField
                Get
                    Return _field.CorrespondingTupleField
                End Get
            End Property

            Private ReadOnly Property IFieldSymbol_IsExplicitlyNamedTupleElement As Boolean Implements IFieldSymbol.IsExplicitlyNamedTupleElement
                Get
                    Return _field.IsExplicitlyNamedTupleElement
                End Get
            End Property

            Protected Overrides Sub AcceptCore(visitor As SymbolVisitor)
                visitor.VisitField(Me)
            End Sub

            Protected Overrides Function AcceptCore(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
                Return visitor.VisitField(Me)
            End Function

            Protected Overrides Function AcceptCore(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
                Return visitor.VisitField(Me, argument)
            End Function
        End Class

        Private NotInheritable Class IdentityParameterView
            Inherits ProjectedSymbol
            Implements IParameterSymbol

            Private ReadOnly _parameter As IParameterSymbol
            Private ReadOnly _containing As ISymbol
            Private ReadOnly _type As ITypeSymbol

            Friend Sub New(parameter As IParameterSymbol, containing As ISymbol, owner As NamedTypeProjection, methodDefinition As MethodSymbol, methodAnnotations As ImmutableArray(Of NullableAnnotation))
                MyBase.New(parameter, owner)
                _parameter = parameter
                _containing = containing
                _type = SubstituteMemberType(parameter.Type, parameter.NullableAnnotation, owner, methodDefinition, methodAnnotations)
            End Sub

            Protected Overrides Function GetContainingSymbol() As ISymbol
                If _containing IsNot Nothing Then
                    Return _containing
                End If

                Return MyBase.GetContainingSymbol()
            End Function

            Private ReadOnly Property IParameterSymbol_Type As ITypeSymbol Implements IParameterSymbol.Type
                Get
                    Return _type
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_NullableAnnotation As NullableAnnotation Implements IParameterSymbol.NullableAnnotation
                Get
                    Return _type.NullableAnnotation
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_RefKind As RefKind Implements IParameterSymbol.RefKind
                Get
                    Return _parameter.RefKind
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_ScopedKind As ScopedKind Implements IParameterSymbol.ScopedKind
                Get
                    Return _parameter.ScopedKind
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_IsParams As Boolean Implements IParameterSymbol.IsParams
                Get
                    Return _parameter.IsParams
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_IsParamsArray As Boolean Implements IParameterSymbol.IsParamsArray
                Get
                    Return _parameter.IsParamsArray
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_IsParamsCollection As Boolean Implements IParameterSymbol.IsParamsCollection
                Get
                    Return _parameter.IsParamsCollection
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_IsOptional As Boolean Implements IParameterSymbol.IsOptional
                Get
                    Return _parameter.IsOptional
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_IsThis As Boolean Implements IParameterSymbol.IsThis
                Get
                    Return _parameter.IsThis
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_IsDiscard As Boolean Implements IParameterSymbol.IsDiscard
                Get
                    Return _parameter.IsDiscard
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_CustomModifiers As ImmutableArray(Of CustomModifier) Implements IParameterSymbol.CustomModifiers
                Get
                    Return _parameter.CustomModifiers
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_RefCustomModifiers As ImmutableArray(Of CustomModifier) Implements IParameterSymbol.RefCustomModifiers
                Get
                    Return _parameter.RefCustomModifiers
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_Ordinal As Integer Implements IParameterSymbol.Ordinal
                Get
                    Return _parameter.Ordinal
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_HasExplicitDefaultValue As Boolean Implements IParameterSymbol.HasExplicitDefaultValue
                Get
                    Return _parameter.HasExplicitDefaultValue
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_ExplicitDefaultValue As Object Implements IParameterSymbol.ExplicitDefaultValue
                Get
                    Return _parameter.ExplicitDefaultValue
                End Get
            End Property

            Private ReadOnly Property IParameterSymbol_OriginalDefinition As IParameterSymbol Implements IParameterSymbol.OriginalDefinition
                Get
                    Return _parameter.OriginalDefinition
                End Get
            End Property

            Protected Overrides Sub AcceptCore(visitor As SymbolVisitor)
                visitor.VisitParameter(Me)
            End Sub

            Protected Overrides Function AcceptCore(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
                Return visitor.VisitParameter(Me)
            End Function

            Protected Overrides Function AcceptCore(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
                Return visitor.VisitParameter(Me, argument)
            End Function
        End Class

        Private NotInheritable Class IdentityMethodView
            Inherits ProjectedSymbol
            Implements IMethodSymbol

            Private ReadOnly _method As IMethodSymbol
            Private ReadOnly _annotations As ImmutableArray(Of NullableAnnotation)
            Private ReadOnly _returnType As ITypeSymbol
            Private ReadOnly _associated As ISymbol

            Friend Sub New(method As IMethodSymbol, annotations As ImmutableArray(Of NullableAnnotation), owner As NamedTypeProjection, Optional associated As ISymbol = Nothing)
                MyBase.New(method, owner)
                _method = method
                _annotations = annotations
                _associated = associated
                Dim definition = TryCast(Unwrap(method), MethodSymbol)
                _returnType = SubstituteMemberType(method.ReturnType, method.ReturnNullableAnnotation, owner, If(_annotations.IsDefault, Nothing, definition), _annotations)
            End Sub

            Protected Overrides Function EqualsCore(other As ISymbol, comparer As SymbolEqualityComparer) As Boolean
                Dim otherView = TryCast(other, IdentityMethodView)
                If otherView Is Nothing OrElse Not _method.Equals(otherView._method, comparer) Then
                    Return False
                End If

                If (comparer.CompareKind And TypeCompareKind.IgnoreNullableModifiersForReferenceTypes) <> 0 Then
                    Return True
                End If

                If AnnotationsDiffer(_annotations, otherView._annotations) OrElse AnnotationsDiffer(otherView._annotations, _annotations) Then
                    Return False
                End If

                If Owner Is otherView.Owner Then
                    Return True
                End If

                If Owner Is Nothing OrElse otherView.Owner Is Nothing Then
                    Return False
                End If

                Return AreEqual(Owner, otherView.Owner, comparer)
            End Function

            Private Function FinishConstruct(built As IMethodSymbol, requested As ImmutableArray(Of NullableAnnotation)) As IMethodSymbol
                Dim compiler = TryCast(Unwrap(built), MethodSymbol)
                Dim definition = TryCast(Unwrap(_method), MethodSymbol)
                If compiler IsNot Nothing AndAlso compiler Is definition Then
                    Return PresentMethodIdentity(compiler, requested, Owner)
                End If

                Return built
            End Function

            Private ReadOnly Property IMethodSymbol_ReturnType As ITypeSymbol Implements IMethodSymbol.ReturnType
                Get
                    Return _returnType
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ReturnNullableAnnotation As NullableAnnotation Implements IMethodSymbol.ReturnNullableAnnotation
                Get
                    Return _returnType.NullableAnnotation
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_TypeArguments As ImmutableArray(Of ITypeSymbol) Implements IMethodSymbol.TypeArguments
                Get
                    If _annotations.IsDefault Then
                        Return _method.TypeArguments
                    End If

                    Return ProjectAll(DirectCast(Unwrap(_method), MethodSymbol).TypeArguments, _annotations)
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_TypeArgumentNullableAnnotations As ImmutableArray(Of NullableAnnotation) Implements IMethodSymbol.TypeArgumentNullableAnnotations
                Get
                    If _annotations.IsDefault Then
                        Return _method.TypeArgumentNullableAnnotations
                    End If

                    Return _annotations
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_Parameters As ImmutableArray(Of IParameterSymbol) Implements IMethodSymbol.Parameters
                Get
                    Dim parameters = _method.Parameters
                    If parameters.IsDefaultOrEmpty Then
                        Return parameters
                    End If

                    Dim definition = If(_annotations.IsDefault, Nothing, TryCast(Unwrap(_method), MethodSymbol))
                    Dim builder = ArrayBuilder(Of IParameterSymbol).GetInstance(parameters.Length)
                    For Each parameter In parameters
                        builder.Add(New IdentityParameterView(parameter, Me, Owner, definition, _annotations))
                    Next

                    Return builder.ToImmutableAndFree()
                End Get
            End Property

            Private Function IMethodSymbol_Construct(ParamArray typeArguments() As ITypeSymbol) As IMethodSymbol Implements IMethodSymbol.Construct
                Return FinishConstruct(_method.Construct(typeArguments), CaptureAnnotations(typeArguments.AsImmutableOrNull()))
            End Function

            Private Function IMethodSymbol_Construct(typeArguments As ImmutableArray(Of ITypeSymbol), typeArgumentNullableAnnotations As ImmutableArray(Of NullableAnnotation)) As IMethodSymbol Implements IMethodSymbol.Construct
                Return FinishConstruct(_method.Construct(typeArguments, typeArgumentNullableAnnotations), typeArgumentNullableAnnotations)
            End Function

            Private ReadOnly Property IMethodSymbol_MethodKind As MethodKind Implements IMethodSymbol.MethodKind
                Get
                    Return _method.MethodKind
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_Arity As Integer Implements IMethodSymbol.Arity
                Get
                    Return _method.Arity
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsGenericMethod As Boolean Implements IMethodSymbol.IsGenericMethod
                Get
                    Return _method.IsGenericMethod
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsExtensionMethod As Boolean Implements IMethodSymbol.IsExtensionMethod
                Get
                    Return _method.IsExtensionMethod
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsAsync As Boolean Implements IMethodSymbol.IsAsync
                Get
                    Return _method.IsAsync
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsVararg As Boolean Implements IMethodSymbol.IsVararg
                Get
                    Return _method.IsVararg
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsCheckedBuiltin As Boolean Implements IMethodSymbol.IsCheckedBuiltin
                Get
                    Return _method.IsCheckedBuiltin
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_HidesBaseMethodsByName As Boolean Implements IMethodSymbol.HidesBaseMethodsByName
                Get
                    Return _method.HidesBaseMethodsByName
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ReturnsVoid As Boolean Implements IMethodSymbol.ReturnsVoid
                Get
                    Return _method.ReturnsVoid
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ReturnsByRef As Boolean Implements IMethodSymbol.ReturnsByRef
                Get
                    Return _method.ReturnsByRef
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ReturnsByRefReadonly As Boolean Implements IMethodSymbol.ReturnsByRefReadonly
                Get
                    Return _method.ReturnsByRefReadonly
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_RefKind As RefKind Implements IMethodSymbol.RefKind
                Get
                    Return _method.RefKind
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_TypeParameters As ImmutableArray(Of ITypeParameterSymbol) Implements IMethodSymbol.TypeParameters
                Get
                    Return _method.TypeParameters
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ConstructedFrom As IMethodSymbol Implements IMethodSymbol.ConstructedFrom
                Get
                    Return _method.ConstructedFrom
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsReadOnly As Boolean Implements IMethodSymbol.IsReadOnly
                Get
                    Return _method.IsReadOnly
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsInitOnly As Boolean Implements IMethodSymbol.IsInitOnly
                Get
                    Return _method.IsInitOnly
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_OriginalDefinition As IMethodSymbol Implements IMethodSymbol.OriginalDefinition
                Get
                    Return _method.OriginalDefinition
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_OverriddenMethod As IMethodSymbol Implements IMethodSymbol.OverriddenMethod
                Get
                    Return _method.OverriddenMethod
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ReceiverType As ITypeSymbol Implements IMethodSymbol.ReceiverType
                Get
                    If _method.ReceiverType Is Nothing Then
                        Return Nothing
                    End If

                    Return SubstituteMemberType(_method.ReceiverType, _method.ReceiverNullableAnnotation, Owner, Nothing, Nothing)
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ReceiverNullableAnnotation As NullableAnnotation Implements IMethodSymbol.ReceiverNullableAnnotation
                Get
                    Dim receiver = IMethodSymbol_ReceiverType
                    If receiver Is Nothing Then
                        Return _method.ReceiverNullableAnnotation
                    End If

                    Return receiver.NullableAnnotation
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ReducedFrom As IMethodSymbol Implements IMethodSymbol.ReducedFrom
                Get
                    Return _method.ReducedFrom
                End Get
            End Property

            Private Function IMethodSymbol_GetTypeInferredDuringReduction(reducedFromTypeParameter As ITypeParameterSymbol) As ITypeSymbol Implements IMethodSymbol.GetTypeInferredDuringReduction
                Return _method.GetTypeInferredDuringReduction(reducedFromTypeParameter)
            End Function

            Private Function IMethodSymbol_ReduceExtensionMethod(receiverType As ITypeSymbol) As IMethodSymbol Implements IMethodSymbol.ReduceExtensionMethod
                Return _method.ReduceExtensionMethod(receiverType)
            End Function

            Private Function IMethodSymbol_ReduceExtensionMember(receiverType As ITypeSymbol) As IMethodSymbol Implements IMethodSymbol.ReduceExtensionMember
                Return _method.ReduceExtensionMember(receiverType)
            End Function

            Private ReadOnly Property IMethodSymbol_ExplicitInterfaceImplementations As ImmutableArray(Of IMethodSymbol) Implements IMethodSymbol.ExplicitInterfaceImplementations
                Get
                    Return _method.ExplicitInterfaceImplementations
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_ReturnTypeCustomModifiers As ImmutableArray(Of CustomModifier) Implements IMethodSymbol.ReturnTypeCustomModifiers
                Get
                    Return _method.ReturnTypeCustomModifiers
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_RefCustomModifiers As ImmutableArray(Of CustomModifier) Implements IMethodSymbol.RefCustomModifiers
                Get
                    Return _method.RefCustomModifiers
                End Get
            End Property

            Private Function IMethodSymbol_GetReturnTypeAttributes() As ImmutableArray(Of AttributeData) Implements IMethodSymbol.GetReturnTypeAttributes
                Return _method.GetReturnTypeAttributes()
            End Function

            Private Function IMethodSymbol_GetDllImportData() As DllImportData Implements IMethodSymbol.GetDllImportData
                Return _method.GetDllImportData()
            End Function

            Private ReadOnly Property IMethodSymbol_CallingConvention As Reflection.Metadata.SignatureCallingConvention Implements IMethodSymbol.CallingConvention
                Get
                    Return _method.CallingConvention
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_UnmanagedCallingConventionTypes As ImmutableArray(Of INamedTypeSymbol) Implements IMethodSymbol.UnmanagedCallingConventionTypes
                Get
                    Return _method.UnmanagedCallingConventionTypes
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_AssociatedSymbol As ISymbol Implements IMethodSymbol.AssociatedSymbol
                Get
                    If _associated IsNot Nothing Then
                        Return _associated
                    End If

                    Return _method.AssociatedSymbol
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_PartialDefinitionPart As IMethodSymbol Implements IMethodSymbol.PartialDefinitionPart
                Get
                    Return _method.PartialDefinitionPart
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_PartialImplementationPart As IMethodSymbol Implements IMethodSymbol.PartialImplementationPart
                Get
                    Return _method.PartialImplementationPart
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_MethodImplementationFlags As Reflection.MethodImplAttributes Implements IMethodSymbol.MethodImplementationFlags
                Get
                    Return _method.MethodImplementationFlags
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsPartialDefinition As Boolean Implements IMethodSymbol.IsPartialDefinition
                Get
                    Return _method.IsPartialDefinition
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_AssociatedAnonymousDelegate As INamedTypeSymbol Implements IMethodSymbol.AssociatedAnonymousDelegate
                Get
                    Return _method.AssociatedAnonymousDelegate
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsConditional As Boolean Implements IMethodSymbol.IsConditional
                Get
                    Return _method.IsConditional
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_IsIterator As Boolean Implements IMethodSymbol.IsIterator
                Get
                    Return _method.IsIterator
                End Get
            End Property

            Private ReadOnly Property IMethodSymbol_AssociatedExtensionImplementation As IMethodSymbol Implements IMethodSymbol.AssociatedExtensionImplementation
                Get
                    Return _method.AssociatedExtensionImplementation
                End Get
            End Property

            Protected Overrides Sub AcceptCore(visitor As SymbolVisitor)
                visitor.VisitMethod(Me)
            End Sub

            Protected Overrides Function AcceptCore(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
                Return visitor.VisitMethod(Me)
            End Function

            Protected Overrides Function AcceptCore(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
                Return visitor.VisitMethod(Me, argument)
            End Function
        End Class

        Private NotInheritable Class IdentityPropertyView
            Inherits ProjectedSymbol
            Implements IPropertySymbol

            Private ReadOnly _property As IPropertySymbol
            Private ReadOnly _type As ITypeSymbol

            Friend Sub New(propertySymbol As IPropertySymbol, owner As NamedTypeProjection, Optional explicitType As ITypeSymbol = Nothing)
                MyBase.New(propertySymbol, owner)
                _property = propertySymbol
                _type = If(explicitType, SubstituteMemberType(propertySymbol.Type, propertySymbol.NullableAnnotation, owner, Nothing, Nothing))
            End Sub

            Private ReadOnly Property IPropertySymbol_Type As ITypeSymbol Implements IPropertySymbol.Type
                Get
                    Return _type
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_NullableAnnotation As NullableAnnotation Implements IPropertySymbol.NullableAnnotation
                Get
                    Return _type.NullableAnnotation
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_Parameters As ImmutableArray(Of IParameterSymbol) Implements IPropertySymbol.Parameters
                Get
                    Dim parameters = _property.Parameters
                    If parameters.IsDefaultOrEmpty Then
                        Return parameters
                    End If

                    Dim builder = ArrayBuilder(Of IParameterSymbol).GetInstance(parameters.Length)
                    For Each parameter In parameters
                        builder.Add(New IdentityParameterView(parameter, Me, Owner, Nothing, Nothing))
                    Next

                    Return builder.ToImmutableAndFree()
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_GetMethod As IMethodSymbol Implements IPropertySymbol.GetMethod
                Get
                    If _property.GetMethod Is Nothing Then
                        Return Nothing
                    End If

                    Return New IdentityMethodView(_property.GetMethod, Nothing, Owner, Me)
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_SetMethod As IMethodSymbol Implements IPropertySymbol.SetMethod
                Get
                    If _property.SetMethod Is Nothing Then
                        Return Nothing
                    End If

                    Return New IdentityMethodView(_property.SetMethod, Nothing, Owner, Me)
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_IsIndexer As Boolean Implements IPropertySymbol.IsIndexer
                Get
                    Return _property.IsIndexer
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_IsReadOnly As Boolean Implements IPropertySymbol.IsReadOnly
                Get
                    Return _property.IsReadOnly
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_IsWriteOnly As Boolean Implements IPropertySymbol.IsWriteOnly
                Get
                    Return _property.IsWriteOnly
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_IsRequired As Boolean Implements IPropertySymbol.IsRequired
                Get
                    Return _property.IsRequired
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_IsWithEvents As Boolean Implements IPropertySymbol.IsWithEvents
                Get
                    Return _property.IsWithEvents
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_ReturnsByRef As Boolean Implements IPropertySymbol.ReturnsByRef
                Get
                    Return _property.ReturnsByRef
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_ReturnsByRefReadonly As Boolean Implements IPropertySymbol.ReturnsByRefReadonly
                Get
                    Return _property.ReturnsByRefReadonly
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_RefKind As RefKind Implements IPropertySymbol.RefKind
                Get
                    Return _property.RefKind
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_OriginalDefinition As IPropertySymbol Implements IPropertySymbol.OriginalDefinition
                Get
                    Return _property.OriginalDefinition
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_OverriddenProperty As IPropertySymbol Implements IPropertySymbol.OverriddenProperty
                Get
                    Return _property.OverriddenProperty
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_ExplicitInterfaceImplementations As ImmutableArray(Of IPropertySymbol) Implements IPropertySymbol.ExplicitInterfaceImplementations
                Get
                    Return _property.ExplicitInterfaceImplementations
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_RefCustomModifiers As ImmutableArray(Of CustomModifier) Implements IPropertySymbol.RefCustomModifiers
                Get
                    Return _property.RefCustomModifiers
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_TypeCustomModifiers As ImmutableArray(Of CustomModifier) Implements IPropertySymbol.TypeCustomModifiers
                Get
                    Return _property.TypeCustomModifiers
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_PartialDefinitionPart As IPropertySymbol Implements IPropertySymbol.PartialDefinitionPart
                Get
                    Return _property.PartialDefinitionPart
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_PartialImplementationPart As IPropertySymbol Implements IPropertySymbol.PartialImplementationPart
                Get
                    Return _property.PartialImplementationPart
                End Get
            End Property

            Private ReadOnly Property IPropertySymbol_IsPartialDefinition As Boolean Implements IPropertySymbol.IsPartialDefinition
                Get
                    Return _property.IsPartialDefinition
                End Get
            End Property

            Private Function IPropertySymbol_ReduceExtensionMember(receiverType As ITypeSymbol) As IPropertySymbol Implements IPropertySymbol.ReduceExtensionMember
                Return _property.ReduceExtensionMember(receiverType)
            End Function

            Protected Overrides Sub AcceptCore(visitor As SymbolVisitor)
                visitor.VisitProperty(Me)
            End Sub

            Protected Overrides Function AcceptCore(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
                Return visitor.VisitProperty(Me)
            End Function

            Protected Overrides Function AcceptCore(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
                Return visitor.VisitProperty(Me, argument)
            End Function
        End Class

        Private NotInheritable Class IdentityEventView
            Inherits ProjectedSymbol
            Implements IEventSymbol

            Private ReadOnly _event As IEventSymbol
            Private ReadOnly _type As ITypeSymbol

            Friend Sub New([event] As IEventSymbol, owner As NamedTypeProjection)
                MyBase.New([event], owner)
                _event = [event]
                _type = SubstituteMemberType([event].Type, NullableAnnotation.None, owner, Nothing, Nothing)
            End Sub

            Private ReadOnly Property IEventSymbol_Type As ITypeSymbol Implements IEventSymbol.Type
                Get
                    Return _type
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_NullableAnnotation As NullableAnnotation Implements IEventSymbol.NullableAnnotation
                Get
                    Return NullableAnnotation.None
                End Get
            End Property

            Private Function Accessor(method As IMethodSymbol) As IMethodSymbol
                If method Is Nothing Then
                    Return Nothing
                End If

                Return New IdentityMethodView(method, Nothing, Owner, Me)
            End Function

            Private ReadOnly Property IEventSymbol_AddMethod As IMethodSymbol Implements IEventSymbol.AddMethod
                Get
                    Return Accessor(_event.AddMethod)
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_RemoveMethod As IMethodSymbol Implements IEventSymbol.RemoveMethod
                Get
                    Return Accessor(_event.RemoveMethod)
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_RaiseMethod As IMethodSymbol Implements IEventSymbol.RaiseMethod
                Get
                    Return Accessor(_event.RaiseMethod)
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_IsWindowsRuntimeEvent As Boolean Implements IEventSymbol.IsWindowsRuntimeEvent
                Get
                    Return _event.IsWindowsRuntimeEvent
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_OriginalDefinition As IEventSymbol Implements IEventSymbol.OriginalDefinition
                Get
                    Return _event.OriginalDefinition
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_OverriddenEvent As IEventSymbol Implements IEventSymbol.OverriddenEvent
                Get
                    Return _event.OverriddenEvent
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_ExplicitInterfaceImplementations As ImmutableArray(Of IEventSymbol) Implements IEventSymbol.ExplicitInterfaceImplementations
                Get
                    Return _event.ExplicitInterfaceImplementations
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_PartialDefinitionPart As IEventSymbol Implements IEventSymbol.PartialDefinitionPart
                Get
                    Return _event.PartialDefinitionPart
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_PartialImplementationPart As IEventSymbol Implements IEventSymbol.PartialImplementationPart
                Get
                    Return _event.PartialImplementationPart
                End Get
            End Property

            Private ReadOnly Property IEventSymbol_IsPartialDefinition As Boolean Implements IEventSymbol.IsPartialDefinition
                Get
                    Return _event.IsPartialDefinition
                End Get
            End Property

            Protected Overrides Sub AcceptCore(visitor As SymbolVisitor)
                visitor.VisitEvent(Me)
            End Sub

            Protected Overrides Function AcceptCore(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
                Return visitor.VisitEvent(Me)
            End Function

            Protected Overrides Function AcceptCore(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
                Return visitor.VisitEvent(Me, argument)
            End Function
        End Class
    End Class
End Namespace
