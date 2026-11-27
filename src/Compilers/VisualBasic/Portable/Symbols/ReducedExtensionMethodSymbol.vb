' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' Represents a curried extension method definition - first parameter and fixed 
    ''' type parameters removed.
    ''' </summary>
    Friend NotInheritable Class ReducedExtensionMethodSymbol
        Inherits MethodSymbol

        Private ReadOnly _receiverType As TypeSymbol
        Private ReadOnly _curriedFromMethod As MethodSymbol
        Private ReadOnly _fixedTypeParameters As ImmutableArray(Of KeyValuePair(Of TypeParameterSymbol, TypeSymbol))
        Private ReadOnly _proximity As Integer
        Private ReadOnly _curryTypeSubstitution As TypeSubstitution
        Private ReadOnly _curriedTypeParameters As ImmutableArray(Of ReducedTypeParameterSymbol)
        Private _originalTypeArgumentAnnotations As ImmutableArray(Of NullableAnnotation)
        Private _lazyReturnType As TypeSymbol
        Private _lazyParameters As ImmutableArray(Of ReducedParameterSymbol)

        ''' <summary>
        ''' If this is an extension method that can be applied to an instance of the given type,
        ''' returns the curried method symbol thus formed. Otherwise, returns Nothing.
        ''' </summary>
        Public Shared Function Create(
                                     instanceType As TypeSymbol,
                                     possiblyExtensionMethod As MethodSymbol,
                                     proximity As Integer,
                                     ByRef useSiteInfo As CompoundUseSiteInfo(Of AssemblySymbol),
                                     languageVersion As LanguageVersion
        ) As MethodSymbol

            Debug.Assert(instanceType IsNot Nothing)
            Debug.Assert(possiblyExtensionMethod IsNot Nothing)
            Debug.Assert(proximity >= 0)

            If Not (possiblyExtensionMethod.IsDefinition AndAlso
                    possiblyExtensionMethod.MayBeReducibleExtensionMethod AndAlso
                    possiblyExtensionMethod.MethodKind <> MethodKind.ReducedExtension) Then
                Return Nothing
            End If

            ' Note, we have only checked IsProbableExtensionMethod at this point, not IsExtensionMethod. For performance reasons
            ' (fully binding the Extension attribute and verifying all the rules around extension methods is expensive), we only 
            ' check IsExtensionMethod after checking whether the extension method is applicable to the given type.

            Debug.Assert(Not possiblyExtensionMethod.ContainingType.IsGenericType)

            If possiblyExtensionMethod.ParameterCount = 0 Then
                Return Nothing
            End If

            Dim receiverType As TypeSymbol = possiblyExtensionMethod.Parameters(0).Type
            Dim hashSetOfTypeParametersToFix As New HashSet(Of TypeParameterSymbol)

            receiverType.CollectReferencedTypeParameters(hashSetOfTypeParametersToFix)

            Dim typeParametersToFixArray As ImmutableArray(Of TypeParameterSymbol) = Nothing
            Dim fixWithArray As ImmutableArray(Of TypeSymbol) = Nothing
            Dim reducedUseSiteInfo = If(useSiteInfo.AccumulatesDependencies, New CompoundUseSiteInfo(Of AssemblySymbol)(useSiteInfo.AssemblyBeingBuilt), CompoundUseSiteInfo(Of AssemblySymbol).DiscardedDependencies)

            If hashSetOfTypeParametersToFix.Count > 0 Then
                ' Try to infer type parameters from the supplied instanceType.

                Dim parameterToArgumentMap = ArrayBuilder(Of Integer).GetInstance(possiblyExtensionMethod.ParameterCount, -1)
                parameterToArgumentMap(0) = 0

                Dim typeArguments As ImmutableArray(Of TypeSymbol) = Nothing
                Dim inferenceLevel As TypeArgumentInference.InferenceLevel = TypeArgumentInference.InferenceLevel.None
                Dim allFailedInferenceIsDueToObject As Boolean = False
                Dim someInferenceFailed As Boolean = False
                Dim inferenceErrorReasons As InferenceErrorReasons = InferenceErrorReasons.Other

                Dim fixTheseTypeParameters = BitVector.Create(possiblyExtensionMethod.Arity)

                For Each typeParameter As TypeParameterSymbol In hashSetOfTypeParametersToFix
                    fixTheseTypeParameters(typeParameter.Ordinal) = True
                Next

                Dim inferenceDiagnostic = If(reducedUseSiteInfo.AccumulatesDependencies,
                                             BindingDiagnosticBag.GetInstance(withDiagnostics:=False, withDependencies:=True),
                                             BindingDiagnosticBag.Discarded)

                Dim success As Boolean = TypeArgumentInference.Infer(possiblyExtensionMethod,
                                               arguments:=ImmutableArray.Create(Of BoundExpression)(
                                                   New BoundRValuePlaceholder(VisualBasic.VisualBasicSyntaxTree.Dummy.GetRoot(Nothing),
                                                                             instanceType)),
                                               parameterToArgumentMap:=parameterToArgumentMap,
                                               paramArrayItems:=Nothing,
                                               delegateReturnType:=Nothing,
                                               delegateReturnTypeReferenceBoundNode:=Nothing,
                                               typeArguments:=typeArguments,
                                               inferenceLevel:=inferenceLevel,
                                               someInferenceFailed:=someInferenceFailed,
                                               allFailedInferenceIsDueToObject:=allFailedInferenceIsDueToObject,
                                               inferenceErrorReasons:=inferenceErrorReasons,
                                               inferredTypeByAssumption:=Nothing,
                                               typeArgumentsLocation:=Nothing,
                                               asyncLambdaSubToFunctionMismatch:=Nothing,
                                               useSiteInfo:=reducedUseSiteInfo,
                                               diagnostic:=inferenceDiagnostic,
                                               inferTheseTypeParameters:=fixTheseTypeParameters)

                parameterToArgumentMap.Free()

                If Not success OrElse Not reducedUseSiteInfo.Diagnostics.IsNullOrEmpty() Then
                    inferenceDiagnostic.Free()
                    Return Nothing
                End If

                reducedUseSiteInfo.AddDependencies(inferenceDiagnostic.DependenciesBag)
                inferenceDiagnostic.Free()

                Dim toFixCount = hashSetOfTypeParametersToFix.Count
                Dim typeParametersToFix = ArrayBuilder(Of TypeParameterSymbol).GetInstance(toFixCount)
                Dim fixWith = ArrayBuilder(Of TypeSymbol).GetInstance(toFixCount)

                ' Adjust the receiver type accordingly.
                For i As Integer = 0 To possiblyExtensionMethod.Arity - 1
                    If fixTheseTypeParameters(i) Then
                        typeParametersToFix.Add(possiblyExtensionMethod.TypeParameters(i))
                        fixWith.Add(typeArguments(i))
                        Debug.Assert(fixWith.Last() IsNot Nothing)

                        If typeParametersToFix.Count = toFixCount Then
                            Exit For
                        End If
                    End If
                Next

                typeParametersToFixArray = typeParametersToFix.ToImmutableAndFree()
                fixWithArray = fixWith.ToImmutableAndFree()

                Dim partialSubstitution = TypeSubstitution.Create(possiblyExtensionMethod, typeParametersToFixArray, fixWithArray)

                If partialSubstitution IsNot Nothing Then
                    ' Check constraints.
                    Dim diagnosticsBuilder = ArrayBuilder(Of TypeParameterDiagnosticInfo).GetInstance()
                    Dim useSiteDiagnosticsBuilder As ArrayBuilder(Of TypeParameterDiagnosticInfo) = Nothing
                    success = possiblyExtensionMethod.CheckConstraints(languageVersion,
                                                                       partialSubstitution,
                                                                       typeParametersToFixArray,
                                                                       fixWithArray,
                                                                       diagnosticsBuilder,
                                                                       useSiteDiagnosticsBuilder,
                                                                       template:=New CompoundUseSiteInfo(Of AssemblySymbol)(reducedUseSiteInfo))

                    If Not success Then
                        diagnosticsBuilder.Free()
                        Return Nothing
                    End If

                    If useSiteDiagnosticsBuilder IsNot Nothing Then
                        diagnosticsBuilder.AddRange(useSiteDiagnosticsBuilder)
                    End If

                    For Each pair In diagnosticsBuilder
                        reducedUseSiteInfo.AddDependencies(pair.UseSiteInfo)
                    Next

                    diagnosticsBuilder.Free()

                    receiverType = receiverType.InternalSubstituteTypeParameters(partialSubstitution).Type
                End If
            End If

            If Not OverloadResolution.DoesReceiverMatchInstance(instanceType, receiverType, reducedUseSiteInfo) OrElse
               Not reducedUseSiteInfo.Diagnostics.IsNullOrEmpty() Then
                Return Nothing
            End If

            ' Checking IsExtensionMethod on source symbols can be expensive (we use IsProbableExtensionMethod to quickly
            ' check). We delay the actual check whether it is an extension method until after the determination that the method
            ' would be applicable to the given receiver type.
            If Not possiblyExtensionMethod.IsExtensionMethod OrElse possiblyExtensionMethod.MethodKind = MethodKind.ReducedExtension Then
                Return Nothing
            End If

            Dim fixedTypeParameters = ImmutableArray(Of KeyValuePair(Of TypeParameterSymbol, TypeSymbol)).Empty

            If Not typeParametersToFixArray.IsDefault Then
                Dim fixed = ArrayBuilder(Of KeyValuePair(Of TypeParameterSymbol, TypeSymbol)).GetInstance(typeParametersToFixArray.Length)

                For i As Integer = 0 To typeParametersToFixArray.Length - 1
                    fixed.Add(New KeyValuePair(Of TypeParameterSymbol, TypeSymbol)(typeParametersToFixArray(i), fixWithArray(i)))
                Next

                fixedTypeParameters = fixed.ToImmutableAndFree()
            End If

            useSiteInfo.AddDependencies(reducedUseSiteInfo)

            Return New ReducedExtensionMethodSymbol(receiverType, possiblyExtensionMethod, fixedTypeParameters, proximity)
        End Function

        Private Sub New(
            receiverType As TypeSymbol,
            curriedFromMethod As MethodSymbol,
            fixedTypeParameters As ImmutableArray(Of KeyValuePair(Of TypeParameterSymbol, TypeSymbol)),
            proximity As Integer
        )
            _curriedFromMethod = curriedFromMethod
            _receiverType = receiverType
            _fixedTypeParameters = fixedTypeParameters
            _proximity = proximity
            If curriedFromMethod.Arity = 0 Then
                _originalTypeArgumentAnnotations = ImmutableArray(Of NullableAnnotation).Empty
            Else
                Dim blanks(curriedFromMethod.Arity - 1) As NullableAnnotation
                _originalTypeArgumentAnnotations = blanks.AsImmutableOrNull()
            End If

            If _curriedFromMethod.Arity = 0 Then
                _curryTypeSubstitution = Nothing
                _curriedTypeParameters = ImmutableArray(Of ReducedTypeParameterSymbol).Empty
                Return
            End If

            ' Build curried type parameters and the type substitution for the currying.
            Dim curriedTypeParameters() As ReducedTypeParameterSymbol = Nothing

            If fixedTypeParameters.Length < curriedFromMethod.Arity Then
                curriedTypeParameters = New ReducedTypeParameterSymbol(curriedFromMethod.Arity - fixedTypeParameters.Length - 1) {}
            End If

            Dim curryTypeArguments(curriedFromMethod.Arity - 1) As TypeSymbol

            Dim i As Integer

            ' First take care of fixed type parameters.
            For i = 0 To fixedTypeParameters.Length - 1
                Dim fixed As KeyValuePair(Of TypeParameterSymbol, TypeSymbol) = fixedTypeParameters(i)
                curryTypeArguments(fixed.Key.Ordinal) = fixed.Value
            Next

            ' Now deal with the curried ones.
            If curriedTypeParameters Is Nothing Then
                _curriedTypeParameters = ImmutableArray(Of ReducedTypeParameterSymbol).Empty
            Else
                Dim j As Integer = 0
                For i = 0 To curryTypeArguments.Length - 1
                    If curryTypeArguments(i) Is Nothing Then
                        Dim curried = New ReducedTypeParameterSymbol(Me, curriedFromMethod.TypeParameters(i), j)
                        curriedTypeParameters(j) = curried
                        curryTypeArguments(i) = curried
                        j += 1

                        If j = curriedTypeParameters.Length Then
                            Exit For
                        End If
                    End If
                Next

                _curriedTypeParameters = curriedTypeParameters.AsImmutableOrNull()
            End If

            _curryTypeSubstitution = TypeSubstitution.Create(curriedFromMethod, curriedFromMethod.TypeParameters, curryTypeArguments.AsImmutableOrNull())
        End Sub

        Public Overrides ReadOnly Property ReceiverType As TypeSymbol
            Get
                Return _receiverType
            End Get
        End Property

        Friend Sub NotePublicReceiverAnnotation(annotation As NullableAnnotation)
            If annotation = NullableAnnotation.None OrElse _curriedFromMethod.Parameters.Length = 0 Then
                Return
            End If

            Dim typeParameter = TryCast(_curriedFromMethod.Parameters(0).Type, TypeParameterSymbol)
            If typeParameter Is Nothing OrElse typeParameter.ContainingSymbol.OriginalDefinition IsNot _curriedFromMethod.OriginalDefinition Then
                Return
            End If

            Dim stored = _originalTypeArgumentAnnotations
            If stored.IsDefault OrElse typeParameter.Ordinal < 0 OrElse typeParameter.Ordinal >= stored.Length OrElse stored(typeParameter.Ordinal) <> NullableAnnotation.None Then
                Return
            End If

            Dim updated(stored.Length - 1) As NullableAnnotation
            stored.CopyTo(updated)
            updated(typeParameter.Ordinal) = annotation
            _originalTypeArgumentAnnotations = updated.AsImmutableOrNull()
            _lazyReturnType = Nothing
            _lazyParameters = Nothing
        End Sub

        Friend Overrides Function GetReceiverNullableAnnotation() As NullableAnnotation
            Dim parameter = _curriedFromMethod.Parameters(0)
            Dim declared = parameter.GetDeclaredNullableAnnotation()
            If declared = NullableAnnotation.Annotated Then
                Return declared
            End If

            Dim carried = AnnotationOfMethodType(parameter.Type)
            If carried = NullableAnnotation.None Then
                Return declared
            End If

            Return carried
        End Function

        Friend Overrides ReadOnly Property FixedTypeParameters As ImmutableArray(Of KeyValuePair(Of TypeParameterSymbol, TypeSymbol))
            Get
                Return _fixedTypeParameters
            End Get
        End Property

        Friend Overrides Sub SetTypeArgumentNullableAnnotations(annotations As ImmutableArray(Of NullableAnnotation))
            If annotations.IsDefault Then
                Return
            End If

            If annotations.Length = _curriedFromMethod.Arity Then
                _originalTypeArgumentAnnotations = annotations
                _lazyReturnType = Nothing
                _lazyParameters = Nothing
                Return
            End If

            Dim parameters = Me.TypeParameters
            If parameters.IsDefault OrElse annotations.Length <> parameters.Length OrElse _curriedFromMethod.Arity = 0 Then
                Return
            End If

            Dim updated(_curriedFromMethod.Arity - 1) As NullableAnnotation
            Dim stored = _originalTypeArgumentAnnotations
            If Not stored.IsDefault AndAlso stored.Length = updated.Length Then
                stored.CopyTo(updated)
            End If

            For i = 0 To annotations.Length - 1
                Dim original = parameters(i).ReducedFrom
                Dim ordinal = If(original Is Nothing, parameters(i).Ordinal, original.Ordinal)
                If ordinal >= 0 AndAlso ordinal < updated.Length Then
                    updated(ordinal) = annotations(i)
                End If
            Next

            _originalTypeArgumentAnnotations = updated.AsImmutableOrNull()
            _lazyReturnType = Nothing
            _lazyParameters = Nothing
        End Sub

        Friend Overrides Function GetTypeArgumentNullableAnnotations() As ImmutableArray(Of NullableAnnotation)
            Return _originalTypeArgumentAnnotations
        End Function

        Friend Overrides Function GetPublicTypeArgumentNullableAnnotations() As ImmutableArray(Of NullableAnnotation)
            Dim stored = _originalTypeArgumentAnnotations
            Dim parameters = Me.TypeParameters
            If parameters.IsDefaultOrEmpty Then
                Return ImmutableArray(Of NullableAnnotation).Empty
            End If

            If stored.IsDefault OrElse stored.Length = parameters.Length Then
                Return stored
            End If

            If stored.Length <> _curriedFromMethod.Arity Then
                Return Nothing
            End If

            Dim mapped(parameters.Length - 1) As NullableAnnotation
            For i = 0 To parameters.Length - 1
                Dim original = parameters(i).ReducedFrom
                Dim ordinal = If(original Is Nothing, parameters(i).Ordinal, original.Ordinal)
                mapped(i) = If(ordinal >= 0 AndAlso ordinal < stored.Length, stored(ordinal), NullableAnnotation.None)
            Next

            Return mapped.AsImmutableOrNull()
        End Function

        Friend Overrides ReadOnly Property ReturnTypeFlowAnalysisAnnotations As FlowAnalysisAnnotations
            Get
                Return _curriedFromMethod.ReturnTypeFlowAnalysisAnnotations
            End Get
        End Property

        Friend Overrides Function GetReturnNullableAnnotation() As NullableAnnotation
            Dim declared = _curriedFromMethod.GetReturnNullableAnnotation()
            ' An explicit T? stays annotated. Inference does not remove it.
            If declared = NullableAnnotation.Annotated Then
                Return declared
            End If

            Dim carried = AnnotationOfMethodType(_curriedFromMethod.ReturnType)
            If carried = NullableAnnotation.None Then
                Return declared
            End If

            Return carried
        End Function

        Private Function AnnotationOfMethodType(type As TypeSymbol) As NullableAnnotation
            Dim typeParameter = TryCast(type, TypeParameterSymbol)
            If typeParameter Is Nothing Then
                Return NullableAnnotation.None
            End If

            Dim original = If(typeParameter.ReducedFrom, typeParameter)
            Dim owner = TryCast(original.ContainingSymbol, MethodSymbol)
            If owner Is Nothing OrElse owner.OriginalDefinition IsNot _curriedFromMethod.OriginalDefinition Then
                Return NullableAnnotation.None
            End If

            If original.Ordinal < 0 OrElse original.Ordinal >= _originalTypeArgumentAnnotations.Length Then
                Return NullableAnnotation.None
            End If

            Return _originalTypeArgumentAnnotations(original.Ordinal)
        End Function

        Private Sub ApplyStoredAnnotations(original As TypeSymbol, substituted As TypeSymbol)
            If substituted Is Nothing OrElse substituted.IsDefinition Then
                Return
            End If

            If original Is Nothing Then
                original = substituted
            End If

            Dim originalNamed = TryCast(original, NamedTypeSymbol)
            Dim named = TryCast(substituted, NamedTypeSymbol)
            If originalNamed IsNot Nothing AndAlso named IsNot Nothing AndAlso named.Arity > 0 AndAlso Not named.IsDefinition AndAlso originalNamed.Arity = named.Arity Then
                Dim originalArguments = originalNamed.TypeArgumentsNoUseSiteDiagnostics
                Dim arguments = named.TypeArgumentsNoUseSiteDiagnostics
                Dim current = named.GetTypeArgumentNullableAnnotations()
                Dim updated(arguments.Length - 1) As NullableAnnotation
                Dim changed = False
                For index = 0 To arguments.Length - 1
                    Dim sourceArgument = If(index < originalArguments.Length, originalArguments(index), arguments(index))
                    Dim carried = AnnotationOfMethodType(sourceArgument)
                    Dim existing = NullableAnnotation.None
                    If Not current.IsDefault AndAlso index < current.Length Then
                        existing = current(index)
                    End If

                    updated(index) = If(carried <> NullableAnnotation.None, carried, existing)
                    If updated(index) <> existing Then
                        changed = True
                    End If

                    Dim substitutedArgument = If(index < arguments.Length, arguments(index), Nothing)
                    ApplyStoredAnnotations(sourceArgument, substitutedArgument)
                Next

                If changed Then
                    named.SetTypeArgumentNullableAnnotations(updated.AsImmutableOrNull())
                End If
            End If

            Dim originalArray = TryCast(original, ArrayTypeSymbol)
            Dim arrayType = TryCast(substituted, ArrayTypeSymbol)
            If originalArray IsNot Nothing AndAlso arrayType IsNot Nothing Then
                Dim carried = AnnotationOfMethodType(originalArray.ElementType)
                If carried <> NullableAnnotation.None AndAlso arrayType.ElementNullableAnnotation <> carried Then
                    arrayType.SetElementNullableAnnotation(carried)
                End If

                ApplyStoredAnnotations(originalArray.ElementType, arrayType.ElementType)
            End If
        End Sub

        Public Overrides Function GetTypeInferredDuringReduction(reducedFromTypeParameter As TypeParameterSymbol) As TypeSymbol
            If reducedFromTypeParameter Is Nothing Then
                Throw New ArgumentNullException()
            End If

            If reducedFromTypeParameter.ContainingSymbol <> _curriedFromMethod Then
                Throw New ArgumentException()
            End If

            For Each pair As KeyValuePair(Of TypeParameterSymbol, TypeSymbol) In _fixedTypeParameters
                If TypeSymbol.Equals(pair.Key, reducedFromTypeParameter, TypeCompareKind.ConsiderEverything) Then
                    Return pair.Value
                End If
            Next

            Return Nothing
        End Function

        Public Overrides ReadOnly Property ReducedFrom As MethodSymbol
            Get
                Return _curriedFromMethod
            End Get
        End Property

        Friend Overrides ReadOnly Property CallsiteReducedFromMethod As MethodSymbol
            Get
                If _curryTypeSubstitution Is Nothing Then
                    Return _curriedFromMethod
                End If

                If _curriedFromMethod.Arity = Me.Arity Then
                    Return New SubstitutedMethodSymbol.ConstructedNotSpecializedGenericMethod(_curryTypeSubstitution, Me.TypeArguments)
                End If

                Dim resultTypeArguments(_curriedFromMethod.Arity - 1) As TypeSymbol

                For Each pair As KeyValuePair(Of TypeParameterSymbol, TypeSymbol) In _fixedTypeParameters
                    resultTypeArguments(pair.Key.Ordinal) = pair.Value
                Next

                For Each typeParameter As ReducedTypeParameterSymbol In _curriedTypeParameters
                    resultTypeArguments(typeParameter.ReducedFrom.Ordinal) = typeParameter
                Next

                Return New SubstitutedMethodSymbol.ConstructedNotSpecializedGenericMethod(_curryTypeSubstitution, resultTypeArguments.AsImmutableOrNull())

            End Get
        End Property

        Friend Overrides ReadOnly Property MayBeReducibleExtensionMethod As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property Proximity As Integer
            Get
                Return _proximity
            End Get
        End Property

        Friend Overrides Function GetUseSiteInfo() As UseSiteInfo(Of AssemblySymbol)
            Return _curriedFromMethod.GetUseSiteInfo()
        End Function

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return _curriedFromMethod.ContainingSymbol
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return _curriedFromMethod.ContainingType
            End Get
        End Property

        Public Overrides ReadOnly Property MethodKind As MethodKind
            Get
                Return MethodKind.ReducedExtension
            End Get
        End Property

        Friend Overrides ReadOnly Property IsMethodKindBasedOnSyntax As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property Arity As Integer
            Get
                Return _curriedTypeParameters.Length
            End Get
        End Property

        Public Overrides ReadOnly Property TypeParameters As ImmutableArray(Of TypeParameterSymbol)
            Get
                Return StaticCast(Of TypeParameterSymbol).From(_curriedTypeParameters)
            End Get
        End Property

        Public Overrides ReadOnly Property TypeArguments As ImmutableArray(Of TypeSymbol)
            Get
                Return StaticCast(Of TypeSymbol).From(_curriedTypeParameters)
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnsByRef As Boolean
            Get
                Return _curriedFromMethod.ReturnsByRef
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnType As TypeSymbol
            Get
                If _lazyReturnType Is Nothing Then
                    Dim originalType As TypeSymbol = _curriedFromMethod.ReturnType
                    Dim type = originalType

                    If _curryTypeSubstitution IsNot Nothing Then
                        type = type.InternalSubstituteTypeParameters(_curryTypeSubstitution).Type
                    End If

                    ' A partial reduction is shared by every later construction of this definition.
                    ' Call-specific annotations stay on those constructions. A fully reduced
                    ' method is itself the call symbol, so its return type can carry them.
                    If _curriedTypeParameters.IsEmpty Then
                        ApplyStoredAnnotations(originalType, type)
                    End If
                    Interlocked.CompareExchange(_lazyReturnType, type, Nothing)
                End If

                Return _lazyReturnType
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                If _lazyParameters.IsDefault Then
                    Dim fromParams As ImmutableArray(Of ParameterSymbol) = _curriedFromMethod.Parameters

                    If fromParams.Length = 1 Then
                        _lazyParameters = ImmutableArray(Of ReducedParameterSymbol).Empty
                    Else
                        Dim newParams(fromParams.Length - 2) As ReducedParameterSymbol

                        For i As Integer = 1 To fromParams.Length - 1
                            newParams(i - 1) = New ReducedParameterSymbol(Me, fromParams(i))
                        Next

                        ImmutableInterlocked.InterlockedCompareExchange(_lazyParameters,
                                                            newParams.AsImmutableOrNull(),
                                                            Nothing)
                    End If
                End If

                Return StaticCast(Of ParameterSymbol).From(_lazyParameters)
            End Get
        End Property

        Friend Overrides ReadOnly Property ParameterCount As Integer
            Get
                Return _curriedFromMethod.ParameterCount - 1
            End Get
        End Property

        Public Overrides ReadOnly Property IsExtensionMethod As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverloads As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function GetOverloadResolutionPriority() As Integer
            Return _curriedFromMethod.GetOverloadResolutionPriority()
        End Function

        Public Overrides ReadOnly Property IsShared As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverridable As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsOverrides As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsMustOverride As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsNotOverridable As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property OverriddenMethod As MethodSymbol
            Get
                Return Nothing
            End Get
        End Property

        Friend Overrides ReadOnly Property ShadowsExplicitly As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsSub As Boolean
            Get
                Return _curriedFromMethod.IsSub
            End Get
        End Property

        Public Overrides ReadOnly Property IsAsync As Boolean
            Get
                Return _curriedFromMethod.IsAsync
            End Get
        End Property

        Public Overrides ReadOnly Property IsIterator As Boolean
            Get
                Return _curriedFromMethod.IsIterator
            End Get
        End Property

        Public Overrides ReadOnly Property IsInitOnly As Boolean
            Get
                Return _curriedFromMethod.IsInitOnly
            End Get
        End Property

        Public Overrides ReadOnly Property IsVararg As Boolean
            Get
                Return _curriedFromMethod.IsVararg
            End Get
        End Property

        Public Overrides Function GetReturnTypeAttributes() As ImmutableArray(Of VisualBasicAttributeData)
            Return _curriedFromMethod.GetReturnTypeAttributes()
        End Function

        Public Overrides ReadOnly Property ReturnTypeCustomModifiers As ImmutableArray(Of CustomModifier)
            Get
                Return _curriedFromMethod.ReturnTypeCustomModifiers
            End Get
        End Property

        Public Overrides ReadOnly Property RefCustomModifiers As ImmutableArray(Of CustomModifier)
            Get
                Return _curriedFromMethod.RefCustomModifiers
            End Get
        End Property

        Friend Overrides ReadOnly Property Syntax As SyntaxNode
            Get
                Return _curriedFromMethod.Syntax
            End Get
        End Property

        Public Overrides ReadOnly Property AssociatedSymbol As Symbol
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property ExplicitInterfaceImplementations As ImmutableArray(Of MethodSymbol)
            Get
                Return ImmutableArray(Of MethodSymbol).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property IsExternalMethod As Boolean
            Get
                Return _curriedFromMethod.IsExternalMethod
            End Get
        End Property

        Public Overrides Function GetDllImportData() As DllImportData
            Return _curriedFromMethod.GetDllImportData()
        End Function

        Friend Overrides ReadOnly Property ReturnTypeMarshallingInformation As MarshalPseudoCustomAttributeData
            Get
                Return _curriedFromMethod.ReturnTypeMarshallingInformation
            End Get
        End Property

        Friend Overrides ReadOnly Property ImplementationAttributes As Reflection.MethodImplAttributes
            Get
                Return _curriedFromMethod.ImplementationAttributes
            End Get
        End Property

        Friend Overrides ReadOnly Property HasDeclarativeSecurity As Boolean
            Get
                Return _curriedFromMethod.HasDeclarativeSecurity
            End Get
        End Property

        Friend Overrides Function GetSecurityInformation() As IEnumerable(Of Microsoft.Cci.SecurityAttribute)
            Return _curriedFromMethod.GetSecurityInformation()
        End Function

        Friend Overrides ReadOnly Property CallingConvention As Microsoft.Cci.CallingConvention
            Get
                Return _curriedFromMethod.CallingConvention
            End Get
        End Property

        Friend Overrides ReadOnly Property ObsoleteAttributeData As ObsoleteAttributeData
            Get
                Return _curriedFromMethod.ObsoleteAttributeData
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return _curriedFromMethod.Locations
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return _curriedFromMethod.DeclaringSyntaxReferences
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return _curriedFromMethod.DeclaredAccessibility
            End Get
        End Property

        Public Overrides Function GetAttributes() As ImmutableArray(Of VisualBasicAttributeData)
            Return _curriedFromMethod.GetAttributes()
        End Function

        Public Overrides Function GetDocumentationCommentXml(Optional preferredCulture As Globalization.CultureInfo = Nothing, Optional expandIncludes As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As String
            Return _curriedFromMethod.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken)
        End Function

        Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
            Get
                Return _curriedFromMethod.IsImplicitlyDeclared
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return _curriedFromMethod.Name
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return _curriedFromMethod.HasSpecialName
            End Get
        End Property

        Friend Overrides Function GetAppliedConditionalSymbols() As ImmutableArray(Of String)
            Return _curriedFromMethod.GetAppliedConditionalSymbols()
        End Function

        Public Overrides ReadOnly Property MetadataName As String
            Get
                Return _curriedFromMethod.MetadataName
            End Get
        End Property

        Friend Overrides Function IsMetadataNewSlot() As Boolean
            Return False
        End Function

        Friend Overrides ReadOnly Property GenerateDebugInfoImpl As Boolean
            Get
                Return _curriedFromMethod.GenerateDebugInfo
            End Get
        End Property

        Friend Overrides Function CalculateLocalSyntaxOffset(localPosition As Integer, localTree As SyntaxTree) As Integer
            Throw ExceptionUtilities.Unreachable
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return Hash.Combine(_receiverType.GetHashCode(), _curriedFromMethod.GetHashCode)
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            If obj Is Me Then
                Return True
            End If

            Dim other = TryCast(obj, ReducedExtensionMethodSymbol)

            Return other IsNot Nothing AndAlso
                   other._curriedFromMethod.Equals(_curriedFromMethod) AndAlso
                   other._receiverType.Equals(_receiverType)
        End Function

        ''' <summary>
        ''' Represents type parameter of a curried extension method definition.
        ''' </summary>
        Private NotInheritable Class ReducedTypeParameterSymbol
            Inherits TypeParameterSymbol

            Private ReadOnly _curriedMethod As ReducedExtensionMethodSymbol
            Private ReadOnly _curriedFromTypeParameter As TypeParameterSymbol
            Private ReadOnly _ordinal As Integer

            Public Sub New(curriedMethod As ReducedExtensionMethodSymbol, curriedFromTypeParameter As TypeParameterSymbol, ordinal As Integer)
                _curriedMethod = curriedMethod
                _curriedFromTypeParameter = curriedFromTypeParameter
                _ordinal = ordinal
            End Sub

            Public Overrides ReadOnly Property TypeParameterKind As TypeParameterKind
                Get
                    Return TypeParameterKind.Method
                End Get
            End Property

            Public Overrides ReadOnly Property Name As String
                Get
                    Return _curriedFromTypeParameter.Name
                End Get
            End Property

            Public Overrides ReadOnly Property MetadataName As String
                Get
                    Return _curriedFromTypeParameter.MetadataName
                End Get
            End Property

            Public Overrides ReadOnly Property ReducedFrom As TypeParameterSymbol
                Get
                    Return _curriedFromTypeParameter
                End Get
            End Property

            Friend Overrides ReadOnly Property ConstraintTypesNoUseSiteDiagnostics As ImmutableArray(Of TypeSymbol)
                Get
                    Dim types = _curriedFromTypeParameter.ConstraintTypesNoUseSiteDiagnostics
                    Dim substitution = _curriedMethod._curryTypeSubstitution
                    If substitution IsNot Nothing Then
                        types = InternalSubstituteTypeParametersDistinct(substitution, types)
                    End If
                    Return types
                End Get
            End Property

            Friend Overrides ReadOnly Property HasUnmanagedTypeConstraint As Boolean
                Get
                    Return _curriedFromTypeParameter.HasUnmanagedTypeConstraint
                End Get
            End Property

            Public Overrides ReadOnly Property ContainingSymbol As Symbol
                Get
                    Return _curriedMethod
                End Get
            End Property

            Public Overloads Overrides Function GetAttributes() As ImmutableArray(Of VisualBasicAttributeData)
                Return _curriedFromTypeParameter.GetAttributes()
            End Function

            Public Overrides ReadOnly Property HasConstructorConstraint As Boolean
                Get
                    Return _curriedFromTypeParameter.HasConstructorConstraint
                End Get
            End Property

            Public Overrides ReadOnly Property HasReferenceTypeConstraint As Boolean
                Get
                    Return _curriedFromTypeParameter.HasReferenceTypeConstraint
                End Get
            End Property

            Public Overrides ReadOnly Property HasValueTypeConstraint As Boolean
                Get
                    Return _curriedFromTypeParameter.HasValueTypeConstraint
                End Get
            End Property

            Public Overrides ReadOnly Property AllowsRefLikeType As Boolean
                Get
                    Return _curriedFromTypeParameter.AllowsRefLikeType
                End Get
            End Property

            Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
                Get
                    Return _curriedFromTypeParameter.Locations
                End Get
            End Property

            Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
                Get
                    Return _curriedFromTypeParameter.DeclaringSyntaxReferences
                End Get
            End Property

            Public Overrides ReadOnly Property Ordinal As Integer
                Get
                    Return _ordinal
                End Get
            End Property

            Public Overrides ReadOnly Property Variance As VarianceKind
                Get
                    Return _curriedFromTypeParameter.Variance
                End Get
            End Property

            Public Overrides Function GetDocumentationCommentXml(Optional preferredCulture As Globalization.CultureInfo = Nothing, Optional expandIncludes As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As String
                Return _curriedFromTypeParameter.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken)
            End Function

            Friend Overrides Function GetUseSiteInfo() As UseSiteInfo(Of AssemblySymbol)
                Return _curriedFromTypeParameter.GetUseSiteInfo()
            End Function

            Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
                Get
                    Return _curriedFromTypeParameter.IsImplicitlyDeclared
                End Get
            End Property

            Friend Overrides Sub EnsureAllConstraintsAreResolved()
                _curriedFromTypeParameter.EnsureAllConstraintsAreResolved()
            End Sub

            Public Overrides Function GetHashCode() As Integer
                Return Hash.Combine(_ordinal.GetHashCode(), Me.ContainingSymbol.GetHashCode())
            End Function

            Public Overrides Function Equals(other As TypeSymbol, comparison As TypeCompareKind) As Boolean
                Return Equals(TryCast(other, ReducedTypeParameterSymbol), comparison)
            End Function

            Public Overloads Function Equals(other As ReducedTypeParameterSymbol, comparison As TypeCompareKind) As Boolean

                If Me Is other Then
                    Return True
                End If

                Return other IsNot Nothing AndAlso Me._ordinal = other._ordinal AndAlso Me.ContainingSymbol.Equals(other.ContainingSymbol, comparison)
            End Function

        End Class

        ''' <summary>
        ''' Represents parameter of a curried extension method definition.
        ''' </summary>
        Private Class ReducedParameterSymbol
            Inherits ReducedParameterSymbolBase

            Private ReadOnly _curriedMethod As ReducedExtensionMethodSymbol
            Private _lazyType As TypeSymbol

            Public Sub New(curriedMethod As ReducedExtensionMethodSymbol, curriedFromParameter As ParameterSymbol)
                MyBase.New(curriedFromParameter)
                _curriedMethod = curriedMethod
            End Sub

            Public Overrides ReadOnly Property ContainingSymbol As Symbol
                Get
                    Return _curriedMethod
                End Get
            End Property

            Friend Overrides Function GetDeclaredNullableAnnotation() As NullableAnnotation
                Dim declared = m_CurriedFromParameter.GetDeclaredNullableAnnotation()
                If declared <> NullableAnnotation.None Then
                    Return declared
                End If

                Return _curriedMethod.AnnotationOfMethodType(m_CurriedFromParameter.Type)
            End Function

            Public Overrides ReadOnly Property Type As TypeSymbol
                Get
                    If _lazyType Is Nothing Then

                        Dim originalType As TypeSymbol = m_CurriedFromParameter.Type
                        Dim paramType = originalType

                        If _curriedMethod._curryTypeSubstitution IsNot Nothing Then
                            paramType = paramType.InternalSubstituteTypeParameters(_curriedMethod._curryTypeSubstitution).Type
                        End If

                        _curriedMethod.ApplyStoredAnnotations(originalType, paramType)
                        Interlocked.CompareExchange(_lazyType, paramType, Nothing)
                    End If

                    Return _lazyType
                End Get
            End Property
        End Class

        Friend Overrides ReadOnly Property HasSetsRequiredMembers As Boolean
            Get
                Return False
            End Get
        End Property
    End Class

    Friend MustInherit Class ReducedParameterSymbolBase
        Inherits ParameterSymbol

        Protected ReadOnly m_CurriedFromParameter As ParameterSymbol

        Protected Sub New(curriedFromParameter As ParameterSymbol)
            m_CurriedFromParameter = curriedFromParameter
        End Sub

        Public MustOverride Overrides ReadOnly Property ContainingSymbol As Symbol

        Public MustOverride Overrides ReadOnly Property Type As TypeSymbol

        Public Overrides ReadOnly Property IsByRef As Boolean
            Get
                Return m_CurriedFromParameter.IsByRef
            End Get
        End Property

        Friend Overrides ReadOnly Property IsExplicitByRef As Boolean
            Get
                Return m_CurriedFromParameter.IsExplicitByRef
            End Get
        End Property

        Public Overrides ReadOnly Property CustomModifiers As ImmutableArray(Of CustomModifier)
            Get
                Return m_CurriedFromParameter.CustomModifiers
            End Get
        End Property

        Public Overrides ReadOnly Property RefCustomModifiers As ImmutableArray(Of CustomModifier)
            Get
                Return m_CurriedFromParameter.RefCustomModifiers
            End Get
        End Property

        Public Overrides ReadOnly Property Ordinal As Integer
            Get
                Return m_CurriedFromParameter.Ordinal - 1
            End Get
        End Property

        Public Overrides ReadOnly Property IsParamArray As Boolean
            Get
                Return m_CurriedFromParameter.IsParamArray
            End Get
        End Property

        Public Overrides ReadOnly Property IsOptional As Boolean
            Get
                Return m_CurriedFromParameter.IsOptional
            End Get
        End Property

        Friend Overrides ReadOnly Property ExplicitDefaultConstantValue(inProgress As SymbolsInProgress(Of ParameterSymbol)) As ConstantValue
            Get
                Return m_CurriedFromParameter.ExplicitDefaultConstantValue(inProgress)
            End Get
        End Property

        Friend Overrides ReadOnly Property HasOptionCompare As Boolean
            Get
                Return m_CurriedFromParameter.HasOptionCompare
            End Get
        End Property

        Friend Overrides ReadOnly Property IsIDispatchConstant As Boolean
            Get
                Return m_CurriedFromParameter.IsIDispatchConstant
            End Get
        End Property

        Friend Overrides ReadOnly Property IsIUnknownConstant As Boolean
            Get
                Return m_CurriedFromParameter.IsIUnknownConstant
            End Get
        End Property

        Friend Overrides ReadOnly Property IsCallerLineNumber As Boolean
            Get
                Return m_CurriedFromParameter.IsCallerLineNumber
            End Get
        End Property

        Friend Overrides ReadOnly Property IsCallerMemberName As Boolean
            Get
                Return m_CurriedFromParameter.IsCallerMemberName
            End Get
        End Property

        Friend Overrides ReadOnly Property IsCallerFilePath As Boolean
            Get
                Return m_CurriedFromParameter.IsCallerFilePath
            End Get
        End Property

        Friend NotOverridable Overrides ReadOnly Property CallerArgumentExpressionParameterIndex As Integer
            Get
                Return m_CurriedFromParameter.CallerArgumentExpressionParameterIndex - 1
            End Get
        End Property

        Public Overrides ReadOnly Property HasExplicitDefaultValue As Boolean
            Get
                Return m_CurriedFromParameter.HasExplicitDefaultValue
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return m_CurriedFromParameter.Locations
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return m_CurriedFromParameter.DeclaringSyntaxReferences
            End Get
        End Property

        Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
            Get
                Return m_CurriedFromParameter.IsImplicitlyDeclared
            End Get
        End Property

        Public Overrides Function GetAttributes() As ImmutableArray(Of VisualBasicAttributeData)
            Return m_CurriedFromParameter.GetAttributes()
        End Function

        Friend NotOverridable Overrides ReadOnly Property IsMetadataOut As Boolean
            Get
                Return m_CurriedFromParameter.IsMetadataOut
            End Get
        End Property

        Friend NotOverridable Overrides ReadOnly Property IsMetadataIn As Boolean
            Get
                Return m_CurriedFromParameter.IsMetadataIn
            End Get
        End Property

        Friend NotOverridable Overrides ReadOnly Property IsReadOnlyByRef As Boolean
            Get
                Return m_CurriedFromParameter.IsReadOnlyByRef
            End Get
        End Property

        Friend NotOverridable Overrides ReadOnly Property MarshallingInformation As MarshalPseudoCustomAttributeData
            Get
                Return m_CurriedFromParameter.MarshallingInformation
            End Get
        End Property

        Public Overrides Function GetDocumentationCommentXml(Optional preferredCulture As Globalization.CultureInfo = Nothing, Optional expandIncludes As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As String
            Return m_CurriedFromParameter.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken)
        End Function

        Friend Overrides Function GetUseSiteInfo() As UseSiteInfo(Of AssemblySymbol)
            Return m_CurriedFromParameter.GetUseSiteInfo()
        End Function

        Public Overrides ReadOnly Property Name As String
            Get
                Return m_CurriedFromParameter.Name
            End Get
        End Property

        Public Overrides ReadOnly Property MetadataName As String
            Get
                Return m_CurriedFromParameter.MetadataName
            End Get
        End Property

        Public Overrides Function GetHashCode() As Integer
            Return Hash.Combine(m_CurriedFromParameter.GetHashCode(), ContainingSymbol.GetHashCode())
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            If Me Is obj Then
                Return True
            End If

            Dim other = TryCast(obj, ReducedParameterSymbolBase)

            Return other IsNot Nothing AndAlso
                   other.m_CurriedFromParameter.Equals(m_CurriedFromParameter) AndAlso
                   other.ContainingSymbol.Equals(ContainingSymbol)
        End Function
    End Class

End Namespace

