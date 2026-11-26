' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    ''' <summary>
    ''' Compares nullable contracts of an existing override or Implements relationship.
    ''' Annotations and analysis attributes are not part of CLR signature identity.
    ''' An output may be stronger. An input may be weaker. ByRef is checked in both
    ''' directions unless metadata marks the parameter as in-only or out-only.
    ''' A nullable annotation of None (oblivious) does not force a top-level mismatch.
    ''' MemberNotNull, DoesNotReturnIf, and NotNullIfNotNull name sets are not
    ''' override incompatibilities. NotNull wins when both NotNull and MaybeNull are present.
    ''' </summary>
    Friend Module NullableContractCompatibility
        Private Enum ContractDirection
            Output
            Input
            Invariant
        End Enum

        Friend Sub CheckMethod(method As MethodSymbol, diagnostics As BindingDiagnosticBag)
            If method Is Nothing OrElse method.IsImplicitlyDeclared AndAlso method.AssociatedSymbol Is Nothing Then
                Return
            End If

            If Not WarningsOn(method) Then
                Return
            End If

            Dim overridden = method.OverriddenMethod
            If overridden IsNot Nothing Then
                CompareMethods(method, overridden, isImplementation:=False, diagnostics)
            End If

            Dim implementations = method.ExplicitInterfaceImplementations
            If Not implementations.IsDefault Then
                For Each implemented In implementations
                    CompareMethods(method, implemented, isImplementation:=True, diagnostics)
                Next
            End If
        End Sub

        Friend Sub CheckProperty([property] As PropertySymbol, diagnostics As BindingDiagnosticBag)
            If [property] Is Nothing OrElse Not WarningsOn([property]) Then
                Return
            End If

            Dim setter = [property].SetMethod
            If setter Is Nothing OrElse setter.ParameterCount = 0 Then
                Return
            End If

            Dim valueParameter = setter.Parameters(setter.ParameterCount - 1)
            Dim propertyAnnotation = [property].GetNullableAnnotation()
            Dim parameterAnnotation = valueParameter.GetDeclaredNullableAnnotation()
            If propertyAnnotation = NullableAnnotation.None OrElse parameterAnnotation = NullableAnnotation.None Then
                Return
            End If

            If propertyAnnotation = parameterAnnotation Then
                Return
            End If

            Dim flags = [property].FlowAnalysisAnnotations Or valueParameter.FlowAnalysisAnnotations
            If Has(flags, FlowAnalysisAnnotations.AllowNull) OrElse Has(flags, FlowAnalysisAnnotations.DisallowNull) Then
                Return
            End If

            Report(diagnostics, valueParameter, ERRID.WRN_NullableSetterAnnotationMismatch, valueParameter.Name, [property].Name)
        End Sub

        Private Sub CompareMethods(method As MethodSymbol, baseMethod As MethodSymbol, isImplementation As Boolean, diagnostics As BindingDiagnosticBag)
            If baseMethod Is Nothing OrElse method.IsAccessor <> baseMethod.IsAccessor Then
                Return
            End If

            If Has(baseMethod.FlowAnalysisAnnotations, FlowAnalysisAnnotations.DoesNotReturn) AndAlso
               Not Has(method.FlowAnalysisAnnotations, FlowAnalysisAnnotations.DoesNotReturn) Then
                Report(diagnostics, method, ERRID.WRN_DoesNotReturnMismatch, method.Name)
            End If

            Dim returnCode = If(isImplementation, ERRID.WRN_NullabilityMismatchInReturnTypeOnImplementation, ERRID.WRN_NullabilityMismatchInReturnTypeOnOverride)
            If method.ReturnType IsNot Nothing AndAlso method.ReturnType.IsReferenceType AndAlso baseMethod.ReturnType IsNot Nothing AndAlso Not baseMethod.ReturnType.IsErrorType() Then
                If Not Compatible(
                    baseMethod.ReturnType,
                    OutputAnnotation(DeclaredReturnAnnotation(baseMethod), baseMethod.ReturnTypeFlowAnalysisAnnotations),
                    method.ReturnType,
                    OutputAnnotation(DeclaredReturnAnnotation(method), method.ReturnTypeFlowAnalysisAnnotations),
                    ContractDirection.Output) Then
                    Report(diagnostics, method, returnCode, method.Name)
                End If
            End If

            If method.ParameterCount <> baseMethod.ParameterCount Then
                Return
            End If

            Dim parameterCode = If(isImplementation, ERRID.WRN_NullabilityMismatchInParameterTypeOnImplementation, ERRID.WRN_NullabilityMismatchInParameterTypeOnOverride)
            For index = 0 To method.ParameterCount - 1
                Dim derivedParameter = method.Parameters(index)
                Dim baseParameter = baseMethod.Parameters(index)
                If derivedParameter.IsByRef <> baseParameter.IsByRef Then
                    Continue For
                End If

                If Not ParameterCompatible(baseParameter, derivedParameter) Then
                    Report(diagnostics, derivedParameter, parameterCode, derivedParameter.Name)
                End If
            Next
        End Sub

        Private Function ParameterCompatible(baseParameter As ParameterSymbol, derivedParameter As ParameterSymbol) As Boolean
            Dim direction = ParameterDirection(derivedParameter)
            Dim baseInput = InputAnnotation(DeclaredParameterAnnotation(baseParameter), baseParameter.FlowAnalysisAnnotations)
            Dim derivedInput = InputAnnotation(DeclaredParameterAnnotation(derivedParameter), derivedParameter.FlowAnalysisAnnotations)
            Dim baseOutput = OutputAnnotation(DeclaredParameterAnnotation(baseParameter), baseParameter.FlowAnalysisAnnotations)
            Dim derivedOutput = OutputAnnotation(DeclaredParameterAnnotation(derivedParameter), derivedParameter.FlowAnalysisAnnotations)
            If direction = ContractDirection.Invariant Then
                Return Compatible(baseParameter.Type, baseInput, derivedParameter.Type, derivedInput, ContractDirection.Invariant) AndAlso
                    Compatible(baseParameter.Type, baseOutput, derivedParameter.Type, derivedOutput, ContractDirection.Invariant) AndAlso
                    Not ConditionalOutputWeakened(baseParameter.FlowAnalysisAnnotations, derivedParameter.FlowAnalysisAnnotations)
            End If

            If direction <> ContractDirection.Output Then
                If Not Compatible(baseParameter.Type, baseInput, derivedParameter.Type, derivedInput, ContractDirection.Input) Then
                    Return False
                End If
            End If

            If direction <> ContractDirection.Input Then
                If Not Compatible(baseParameter.Type, baseOutput, derivedParameter.Type, derivedOutput, ContractDirection.Output) Then
                    Return False
                End If

                If derivedParameter.IsByRef AndAlso ConditionalOutputWeakened(baseParameter.FlowAnalysisAnnotations, derivedParameter.FlowAnalysisAnnotations) Then
                    Return False
                End If
            ElseIf Has(baseParameter.FlowAnalysisAnnotations, FlowAnalysisAnnotations.NotNull) OrElse
                Has(baseParameter.FlowAnalysisAnnotations, FlowAnalysisAnnotations.MaybeNull) OrElse
                Has(derivedParameter.FlowAnalysisAnnotations, FlowAnalysisAnnotations.NotNull) OrElse
                Has(derivedParameter.FlowAnalysisAnnotations, FlowAnalysisAnnotations.MaybeNull) Then
                If Not Compatible(baseParameter.Type, baseOutput, derivedParameter.Type, derivedOutput, ContractDirection.Output) Then
                    Return False
                End If
            End If

            Return True
        End Function

        Private Function ParameterDirection(parameter As ParameterSymbol) As ContractDirection
            If Not parameter.IsByRef Then
                Return ContractDirection.Input
            End If

            If parameter.IsOut Then
                Return ContractDirection.Output
            End If

            If parameter.IsMetadataIn AndAlso Not parameter.IsMetadataOut Then
                Return ContractDirection.Input
            End If

            Return ContractDirection.Invariant
        End Function

        Private Function ConditionalOutputWeakened(baseFlags As FlowAnalysisAnnotations, derivedFlags As FlowAnalysisAnnotations) As Boolean
            If Has(baseFlags, FlowAnalysisAnnotations.NotNullWhenTrue) AndAlso Not Has(derivedFlags, FlowAnalysisAnnotations.NotNullWhenTrue) AndAlso Not Has(derivedFlags, FlowAnalysisAnnotations.NotNull) Then
                Return True
            End If

            If Has(baseFlags, FlowAnalysisAnnotations.NotNullWhenFalse) AndAlso Not Has(derivedFlags, FlowAnalysisAnnotations.NotNullWhenFalse) AndAlso Not Has(derivedFlags, FlowAnalysisAnnotations.NotNull) Then
                Return True
            End If

            If Has(derivedFlags, FlowAnalysisAnnotations.MaybeNullWhenTrue) AndAlso Not Has(baseFlags, FlowAnalysisAnnotations.MaybeNullWhenTrue) AndAlso Not Has(baseFlags, FlowAnalysisAnnotations.MaybeNull) Then
                Return True
            End If

            If Has(derivedFlags, FlowAnalysisAnnotations.MaybeNullWhenFalse) AndAlso Not Has(baseFlags, FlowAnalysisAnnotations.MaybeNullWhenFalse) AndAlso Not Has(baseFlags, FlowAnalysisAnnotations.MaybeNull) Then
                Return True
            End If

            Return False
        End Function

        Private Function Compatible(baseType As TypeSymbol, baseAnnotation As NullableAnnotation, derivedType As TypeSymbol, derivedAnnotation As NullableAnnotation, direction As ContractDirection) As Boolean
            If Mismatch(baseAnnotation, derivedAnnotation, direction) Then
                Return False
            End If

            Return NestedCompatible(baseType, derivedType, direction)
        End Function

        Private Function NestedCompatible(baseType As TypeSymbol, derivedType As TypeSymbol, direction As ContractDirection) As Boolean
            If baseType Is Nothing OrElse derivedType Is Nothing OrElse baseType.IsErrorType() OrElse derivedType.IsErrorType() Then
                Return True
            End If

            Dim baseArray = TryCast(baseType, ArrayTypeSymbol)
            Dim derivedArray = TryCast(derivedType, ArrayTypeSymbol)
            If baseArray IsNot Nothing AndAlso derivedArray IsNot Nothing Then
                Dim elementDirection = If(direction = ContractDirection.Invariant, ContractDirection.Invariant, direction)
                Return Compatible(baseArray.ElementType, baseArray.ElementNullableAnnotation, derivedArray.ElementType, derivedArray.ElementNullableAnnotation, elementDirection)
            End If

            Dim baseNamed = TryCast(baseType, NamedTypeSymbol)
            Dim derivedNamed = TryCast(derivedType, NamedTypeSymbol)
            If baseNamed Is Nothing OrElse derivedNamed Is Nothing OrElse Not baseNamed.OriginalDefinition.Equals(derivedNamed.OriginalDefinition) Then
                Return True
            End If

            Dim baseArguments = baseNamed.TypeArgumentsNoUseSiteDiagnostics
            Dim derivedArguments = derivedNamed.TypeArgumentsNoUseSiteDiagnostics
            If baseArguments.Length <> derivedArguments.Length Then
                Return True
            End If

            Dim baseAnnotations = baseNamed.GetTypeArgumentNullableAnnotations()
            Dim derivedAnnotations = derivedNamed.GetTypeArgumentNullableAnnotations()
            Dim parameters = baseNamed.OriginalDefinition.TypeParameters
            For index = 0 To baseArguments.Length - 1
                Dim child = direction
                If index < parameters.Length Then
                    If parameters(index).Variance = VarianceKind.In Then
                        child = Flip(direction)
                    ElseIf parameters(index).Variance = VarianceKind.None Then
                        child = ContractDirection.Invariant
                    End If
                End If

                Dim baseArgumentAnnotation = If(baseAnnotations.IsDefault OrElse index >= baseAnnotations.Length, NullableAnnotation.None, baseAnnotations(index))
                Dim derivedArgumentAnnotation = If(derivedAnnotations.IsDefault OrElse index >= derivedAnnotations.Length, NullableAnnotation.None, derivedAnnotations(index))
                If Not Compatible(baseArguments(index), baseArgumentAnnotation, derivedArguments(index), derivedArgumentAnnotation, child) Then
                    Return False
                End If
            Next

            Return True
        End Function

        Private Function Flip(direction As ContractDirection) As ContractDirection
            Select Case direction
                Case ContractDirection.Output
                    Return ContractDirection.Input
                Case ContractDirection.Input
                    Return ContractDirection.Output
                Case Else
                    Return ContractDirection.Invariant
            End Select
        End Function

        Private Function Mismatch(baseAnnotation As NullableAnnotation, derivedAnnotation As NullableAnnotation, direction As ContractDirection) As Boolean
            If baseAnnotation = NullableAnnotation.None OrElse derivedAnnotation = NullableAnnotation.None OrElse baseAnnotation = derivedAnnotation Then
                Return False
            End If

            Select Case direction
                Case ContractDirection.Output
                    Return baseAnnotation = NullableAnnotation.NotAnnotated AndAlso derivedAnnotation = NullableAnnotation.Annotated
                Case ContractDirection.Input
                    Return derivedAnnotation = NullableAnnotation.NotAnnotated AndAlso baseAnnotation = NullableAnnotation.Annotated
                Case Else
                    Return True
            End Select
        End Function

        Private Function DeclaredParameterAnnotation(parameter As ParameterSymbol) As NullableAnnotation
            Return PromoteSubstitutedReference(parameter.Type, parameter.OriginalDefinition.Type, parameter.GetDeclaredNullableAnnotation())
        End Function

        Private Function DeclaredReturnAnnotation(method As MethodSymbol) As NullableAnnotation
            Return PromoteSubstitutedReference(method.ReturnType, method.OriginalDefinition.ReturnType, method.GetReturnNullableAnnotation())
        End Function

        Private Function PromoteSubstitutedReference(current As TypeSymbol, original As TypeSymbol, annotation As NullableAnnotation) As NullableAnnotation
            If annotation <> NullableAnnotation.None OrElse current Is Nothing OrElse original Is Nothing Then
                Return annotation
            End If

            If original.TypeKind = TypeKind.TypeParameter AndAlso current.IsReferenceType AndAlso current.TypeKind <> TypeKind.TypeParameter Then
                Return NullableAnnotation.NotAnnotated
            End If

            Return annotation
        End Function

        Private Function OutputAnnotation(annotation As NullableAnnotation, flags As FlowAnalysisAnnotations) As NullableAnnotation
            If Has(flags, FlowAnalysisAnnotations.NotNull) Then
                Return NullableAnnotation.NotAnnotated
            End If

            If Has(flags, FlowAnalysisAnnotations.MaybeNull) Then
                Return NullableAnnotation.Annotated
            End If

            Return annotation
        End Function

        Private Function InputAnnotation(annotation As NullableAnnotation, flags As FlowAnalysisAnnotations) As NullableAnnotation
            If Has(flags, FlowAnalysisAnnotations.DisallowNull) Then
                Return NullableAnnotation.NotAnnotated
            End If

            If Has(flags, FlowAnalysisAnnotations.AllowNull) Then
                Return NullableAnnotation.Annotated
            End If

            Return annotation
        End Function

        Private Function Has(flags As FlowAnalysisAnnotations, contract As FlowAnalysisAnnotations) As Boolean
            Return (flags And contract) = contract
        End Function

        Private Function WarningsOn(symbol As Symbol) As Boolean
            Dim compilation = symbol.DeclaringCompilation
            If compilation Is Nothing OrElse compilation.LanguageVersion < LanguageVersion.VbNext Then
                Return False
            End If

            Dim options = TryCast(compilation.Options, VisualBasicCompilationOptions)
            If options Is Nothing Then
                Return False
            End If

            Dim location = symbol.Locations.FirstOrDefault()
            If location Is Nothing OrElse location.SourceTree Is Nothing Then
                Return (options.NullableContext And NullableContextOptions.Warnings) <> 0
            End If

            Dim context = NullableContextFacts.ForTree(location.SourceTree, options.NullableContext).GetContext(location.SourceSpan.Start)
            Return (context And NullableContextOptions.Warnings) <> 0
        End Function

        Private Sub Report(diagnostics As BindingDiagnosticBag, symbol As Symbol, code As ERRID, ParamArray args As Object())
            Dim location = symbol.Locations.FirstOrDefault()
            If location Is Nothing Then
                Return
            End If

            diagnostics.Add(New VBDiagnostic(ErrorFactory.ErrorInfo(code, args), location))
        End Sub
    End Module
End Namespace
