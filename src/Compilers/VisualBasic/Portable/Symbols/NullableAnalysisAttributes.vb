' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Reflection.Metadata
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Decodes the standard nullable analysis attributes into contract flags.
    ''' The flags describe declared contracts. They do not prove that a method body satisfies them.
    ''' </summary>
    Friend Module NullableAnalysisAttributes
        Private ReadOnly s_outputMask As FlowAnalysisAnnotations = FlowAnalysisAnnotations.MaybeNull Or FlowAnalysisAnnotations.NotNull
        Private ReadOnly s_inputMask As FlowAnalysisAnnotations = FlowAnalysisAnnotations.AllowNull Or FlowAnalysisAnnotations.DisallowNull

        Friend Function FromParameterData(data As ParameterWellKnownAttributeData) As FlowAnalysisAnnotations
            If data Is Nothing Then
                Return FlowAnalysisAnnotations.None
            End If

            Dim annotations = FlowAnalysisAnnotations.None
            If data.HasAllowNullAttribute Then
                annotations = annotations Or FlowAnalysisAnnotations.AllowNull
            End If
            If data.HasDisallowNullAttribute Then
                annotations = annotations Or FlowAnalysisAnnotations.DisallowNull
            End If

            If data.HasMaybeNullAttribute Then
                annotations = annotations Or FlowAnalysisAnnotations.MaybeNull
            ElseIf data.MaybeNullWhenAttribute.HasValue Then
                annotations = annotations Or If(data.MaybeNullWhenAttribute.Value, FlowAnalysisAnnotations.MaybeNullWhenTrue, FlowAnalysisAnnotations.MaybeNullWhenFalse)
            End If

            If data.HasNotNullAttribute Then
                annotations = annotations Or FlowAnalysisAnnotations.NotNull
            ElseIf data.NotNullWhenAttribute.HasValue Then
                annotations = annotations Or If(data.NotNullWhenAttribute.Value, FlowAnalysisAnnotations.NotNullWhenTrue, FlowAnalysisAnnotations.NotNullWhenFalse)
            End If

            If data.DoesNotReturnIfAttribute.HasValue Then
                annotations = annotations Or If(data.DoesNotReturnIfAttribute.Value, FlowAnalysisAnnotations.DoesNotReturnIfTrue, FlowAnalysisAnnotations.DoesNotReturnIfFalse)
            End If

            Return annotations
        End Function

        Friend Function FromFieldData(data As FieldWellKnownAttributeData) As FlowAnalysisAnnotations
            If data Is Nothing Then
                Return FlowAnalysisAnnotations.None
            End If

            Return UnconditionalAnnotations(data.HasAllowNullAttribute, data.HasDisallowNullAttribute, data.HasMaybeNullAttribute, data.HasNotNullAttribute)
        End Function

        Friend Function FromReturnData(data As ReturnTypeWellKnownAttributeData) As FlowAnalysisAnnotations
            If data Is Nothing Then
                Return FlowAnalysisAnnotations.None
            End If

            Dim annotations = FlowAnalysisAnnotations.None
            If data.HasMaybeNullAttribute Then
                annotations = annotations Or FlowAnalysisAnnotations.MaybeNull
            End If
            If data.HasNotNullAttribute Then
                annotations = annotations Or FlowAnalysisAnnotations.NotNull
            End If

            Return annotations
        End Function

        Friend Function FromPropertyData(data As PropertyWellKnownAttributeData, returnData As ReturnTypeWellKnownAttributeData) As FlowAnalysisAnnotations
            Dim annotations = FromReturnData(returnData)
            If data Is Nothing Then
                Return annotations
            End If

            Return annotations Or UnconditionalAnnotations(data.HasAllowNullAttribute, data.HasDisallowNullAttribute, data.HasMaybeNullAttribute, data.HasNotNullAttribute)
        End Function

        Friend Function OutputAnnotations(annotations As FlowAnalysisAnnotations) As FlowAnalysisAnnotations
            Return annotations And s_outputMask
        End Function

        Friend Function InputAnnotations(annotations As FlowAnalysisAnnotations) As FlowAnalysisAnnotations
            Return annotations And s_inputMask
        End Function

        Friend Function TryDecodeParameterAttribute(attribute As VisualBasicAttributeData, ByRef arguments As DecodeWellKnownAttributeArguments(Of AttributeSyntax, VisualBasicAttributeData, AttributeLocation)) As Boolean
            If attribute.IsTargetAttribute(AttributeDescription.AllowNullAttribute) Then
                arguments.GetOrCreateData(Of ParameterWellKnownAttributeData)().HasAllowNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.DisallowNullAttribute) Then
                arguments.GetOrCreateData(Of ParameterWellKnownAttributeData)().HasDisallowNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.MaybeNullAttribute) Then
                arguments.GetOrCreateData(Of ParameterWellKnownAttributeData)().HasMaybeNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.MaybeNullWhenAttribute) Then
                arguments.GetOrCreateData(Of ParameterWellKnownAttributeData)().MaybeNullWhenAttribute = attribute.DecodeBooleanConstructorArgument()
            ElseIf attribute.IsTargetAttribute(AttributeDescription.NotNullAttribute) Then
                arguments.GetOrCreateData(Of ParameterWellKnownAttributeData)().HasNotNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.NotNullWhenAttribute) Then
                arguments.GetOrCreateData(Of ParameterWellKnownAttributeData)().NotNullWhenAttribute = attribute.DecodeBooleanConstructorArgument()
            ElseIf attribute.IsTargetAttribute(AttributeDescription.DoesNotReturnIfAttribute) Then
                arguments.GetOrCreateData(Of ParameterWellKnownAttributeData)().DoesNotReturnIfAttribute = attribute.DecodeBooleanConstructorArgument()
            ElseIf attribute.IsTargetAttribute(AttributeDescription.NotNullIfNotNullAttribute) Then
                Dim parameterName = attribute.DecodeNotNullIfNotNullArgument()
                If parameterName IsNot Nothing Then
                    arguments.GetOrCreateData(Of ParameterWellKnownAttributeData)().AddNotNullIfParameterNotNull(parameterName)
                End If
            Else
                Return False
            End If

            Return True
        End Function

        Friend Function TryDecodeFieldAttribute(attribute As VisualBasicAttributeData, ByRef arguments As DecodeWellKnownAttributeArguments(Of AttributeSyntax, VisualBasicAttributeData, AttributeLocation)) As Boolean
            If attribute.IsTargetAttribute(AttributeDescription.AllowNullAttribute) Then
                arguments.GetOrCreateData(Of FieldWellKnownAttributeData)().HasAllowNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.DisallowNullAttribute) Then
                arguments.GetOrCreateData(Of FieldWellKnownAttributeData)().HasDisallowNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.MaybeNullAttribute) Then
                arguments.GetOrCreateData(Of FieldWellKnownAttributeData)().HasMaybeNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.NotNullAttribute) Then
                arguments.GetOrCreateData(Of FieldWellKnownAttributeData)().HasNotNullAttribute = True
            Else
                Return False
            End If

            Return True
        End Function

        Friend Function TryDecodeMethodAttribute(attribute As VisualBasicAttributeData, ByRef arguments As DecodeWellKnownAttributeArguments(Of AttributeSyntax, VisualBasicAttributeData, AttributeLocation)) As Boolean
            If attribute.IsTargetAttribute(AttributeDescription.DoesNotReturnAttribute) Then
                arguments.GetOrCreateData(Of MethodWellKnownAttributeData)().HasDoesNotReturnAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.MemberNotNullAttribute) Then
                Dim names = attribute.DecodeMemberNotNullNames()
                If Not names.IsDefaultOrEmpty Then
                    Dim data = arguments.GetOrCreateData(Of MethodWellKnownAttributeData)()
                    AddMemberNames(names, Sub(name) data.AddNotNullMember(name))
                End If
            ElseIf attribute.IsTargetAttribute(AttributeDescription.MemberNotNullWhenAttribute) Then
                Dim sense As Boolean
                Dim names = attribute.DecodeMemberNotNullWhenNames(sense)
                If Not names.IsDefaultOrEmpty Then
                    Dim data = arguments.GetOrCreateData(Of MethodWellKnownAttributeData)()
                    AddMemberNames(names, Sub(name) data.AddNotNullWhenMember(sense, name))
                End If
            Else
                Return False
            End If

            Return True
        End Function

        Friend Function TryDecodeReturnAttribute(attribute As VisualBasicAttributeData, ByRef arguments As DecodeWellKnownAttributeArguments(Of AttributeSyntax, VisualBasicAttributeData, AttributeLocation)) As Boolean
            If attribute.IsTargetAttribute(AttributeDescription.MaybeNullAttribute) Then
                arguments.GetOrCreateData(Of ReturnTypeWellKnownAttributeData)().HasMaybeNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.NotNullAttribute) Then
                arguments.GetOrCreateData(Of ReturnTypeWellKnownAttributeData)().HasNotNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.NotNullIfNotNullAttribute) Then
                Dim parameterName = attribute.DecodeNotNullIfNotNullArgument()
                If parameterName IsNot Nothing Then
                    arguments.GetOrCreateData(Of ReturnTypeWellKnownAttributeData)().AddNotNullIfParameterNotNull(parameterName)
                End If
            Else
                Return False
            End If

            Return True
        End Function

        Friend Function TryDecodePropertyAttribute(attribute As VisualBasicAttributeData, ByRef arguments As DecodeWellKnownAttributeArguments(Of AttributeSyntax, VisualBasicAttributeData, AttributeLocation)) As Boolean
            If arguments.SymbolPart = AttributeLocation.Return Then
                Return TryDecodeReturnAttribute(attribute, arguments)
            End If

            If attribute.IsTargetAttribute(AttributeDescription.AllowNullAttribute) Then
                arguments.GetOrCreateData(Of PropertyWellKnownAttributeData)().HasAllowNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.DisallowNullAttribute) Then
                arguments.GetOrCreateData(Of PropertyWellKnownAttributeData)().HasDisallowNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.MaybeNullAttribute) Then
                arguments.GetOrCreateData(Of PropertyWellKnownAttributeData)().HasMaybeNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.NotNullAttribute) Then
                arguments.GetOrCreateData(Of PropertyWellKnownAttributeData)().HasNotNullAttribute = True
            ElseIf attribute.IsTargetAttribute(AttributeDescription.MemberNotNullAttribute) Then
                Dim names = attribute.DecodeMemberNotNullNames()
                If Not names.IsDefaultOrEmpty Then
                    Dim data = arguments.GetOrCreateData(Of PropertyWellKnownAttributeData)()
                    AddMemberNames(names, Sub(name) data.AddNotNullMember(name))
                End If
            ElseIf attribute.IsTargetAttribute(AttributeDescription.MemberNotNullWhenAttribute) Then
                Dim sense As Boolean
                Dim names = attribute.DecodeMemberNotNullWhenNames(sense)
                If Not names.IsDefaultOrEmpty Then
                    Dim data = arguments.GetOrCreateData(Of PropertyWellKnownAttributeData)()
                    AddMemberNames(names, Sub(name) data.AddNotNullWhenMember(sense, name))
                End If
            Else
                Return False
            End If

            Return True
        End Function

        Friend Function DecodeUnconditionalMetadataAnnotations([module] As PEModule, handle As EntityHandle) As FlowAnalysisAnnotations
            If [module] Is Nothing OrElse handle.IsNil Then
                Return FlowAnalysisAnnotations.None
            End If

            Try
                Return UnconditionalAnnotations(
                    [module].HasAttribute(handle, AttributeDescription.AllowNullAttribute),
                    [module].HasAttribute(handle, AttributeDescription.DisallowNullAttribute),
                    [module].HasAttribute(handle, AttributeDescription.MaybeNullAttribute),
                    [module].HasAttribute(handle, AttributeDescription.NotNullAttribute))
            Catch ex As Exception When IsBadMetadata(ex)
                Return FlowAnalysisAnnotations.None
            End Try
        End Function

        Friend Function DecodeMetadataAnnotations([module] As PEModule, handle As EntityHandle) As FlowAnalysisAnnotations
            If [module] Is Nothing OrElse handle.IsNil Then
                Return FlowAnalysisAnnotations.None
            End If

            Try
                Dim annotations = FlowAnalysisAnnotations.None
                If [module].HasAttribute(handle, AttributeDescription.AllowNullAttribute) Then
                    annotations = annotations Or FlowAnalysisAnnotations.AllowNull
                End If
                If [module].HasAttribute(handle, AttributeDescription.DisallowNullAttribute) Then
                    annotations = annotations Or FlowAnalysisAnnotations.DisallowNull
                End If

                If [module].HasAttribute(handle, AttributeDescription.MaybeNullAttribute) Then
                    annotations = annotations Or FlowAnalysisAnnotations.MaybeNull
                Else
                    Dim sense As Boolean
                    If [module].HasMaybeNullWhenOrNotNullWhenOrDoesNotReturnIfAttribute(handle, AttributeDescription.MaybeNullWhenAttribute, sense) Then
                        annotations = annotations Or If(sense, FlowAnalysisAnnotations.MaybeNullWhenTrue, FlowAnalysisAnnotations.MaybeNullWhenFalse)
                    End If
                End If

                If [module].HasAttribute(handle, AttributeDescription.NotNullAttribute) Then
                    annotations = annotations Or FlowAnalysisAnnotations.NotNull
                Else
                    Dim sense As Boolean
                    If [module].HasMaybeNullWhenOrNotNullWhenOrDoesNotReturnIfAttribute(handle, AttributeDescription.NotNullWhenAttribute, sense) Then
                        annotations = annotations Or If(sense, FlowAnalysisAnnotations.NotNullWhenTrue, FlowAnalysisAnnotations.NotNullWhenFalse)
                    End If
                End If

                Dim condition As Boolean
                If [module].HasMaybeNullWhenOrNotNullWhenOrDoesNotReturnIfAttribute(handle, AttributeDescription.DoesNotReturnIfAttribute, condition) Then
                    annotations = annotations Or If(condition, FlowAnalysisAnnotations.DoesNotReturnIfTrue, FlowAnalysisAnnotations.DoesNotReturnIfFalse)
                End If

                Return annotations
            Catch ex As Exception When IsBadMetadata(ex)
                Return FlowAnalysisAnnotations.None
            End Try
        End Function

        Friend Function ReadNotNullIfParameterNotNull([module] As PEModule, handle As EntityHandle) As ImmutableHashSet(Of String)
            If [module] Is Nothing OrElse handle.IsNil Then
                Return ImmutableHashSet(Of String).Empty
            End If

            Try
                Dim values = [module].GetStringValuesOfNotNullIfNotNullAttribute(handle)
                If values Is Nothing OrElse values.IsEmpty Then
                    Return ImmutableHashSet(Of String).Empty
                End If

                Dim result = ImmutableHashSet(Of String).Empty
                For Each name In values
                    If name IsNot Nothing Then
                        result = result.Add(name)
                    End If
                Next

                Return result
            Catch ex As Exception When IsBadMetadata(ex)
                Return ImmutableHashSet(Of String).Empty
            End Try
        End Function

        Friend Function ReadMemberNotNull([module] As PEModule, handle As EntityHandle) As NullableMemberNotNullData
            Dim data = New NullableMemberNotNullData()
            If [module] Is Nothing OrElse handle.IsNil Then
                Return data
            End If

            Try
                Dim members = [module].GetMemberNotNullAttributeValues(handle)
                If Not members.IsDefault Then
                    For Each name In members
                        If name IsNot Nothing Then
                            data.AddMember(name)
                        End If
                    Next
                End If

                Dim conditionalMembers = [module].GetMemberNotNullWhenAttributeValues(handle)
                AddNames(data, conditionalMembers.whenTrue, sense:=True)
                AddNames(data, conditionalMembers.whenFalse, sense:=False)
            Catch ex As Exception When IsBadMetadata(ex)
            End Try

            Return data
        End Function

        Friend Function HasDoesNotReturn([module] As PEModule, handle As EntityHandle) As Boolean
            If [module] Is Nothing OrElse handle.IsNil Then
                Return False
            End If

            Try
                Return [module].HasDoesNotReturnAttribute(handle)
            Catch ex As Exception When IsBadMetadata(ex)
                Return False
            End Try
        End Function

        Friend Function PropertySetterInputAnnotations(parameter As ParameterSymbol) As FlowAnalysisAnnotations
            Dim accessor = TryCast(parameter.ContainingSymbol, SourcePropertyAccessorSymbol)
            If accessor Is Nothing OrElse accessor.MethodKind <> MethodKind.PropertySet OrElse parameter.Ordinal <> accessor.ParameterCount - 1 Then
                Return FlowAnalysisAnnotations.None
            End If

            Dim propertySymbol = TryCast(accessor.AssociatedSymbol, SourcePropertySymbol)
            If propertySymbol Is Nothing Then
                Return FlowAnalysisAnnotations.None
            End If

            Return InputAnnotations(propertySymbol.FlowAnalysisAnnotations)
        End Function

        Friend Sub AddPropertySetterInputAttributes(parameter As ParameterSymbol, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData))
            Dim compilation = parameter.DeclaringCompilation
            If compilation Is Nothing OrElse compilation.LanguageVersion < LanguageVersion.VbNext Then
                Return
            End If

            Dim accessor = TryCast(parameter.ContainingSymbol, SourcePropertyAccessorSymbol)
            If accessor Is Nothing OrElse accessor.MethodKind <> MethodKind.PropertySet OrElse parameter.Ordinal <> accessor.ParameterCount - 1 Then
                Return
            End If

            Dim propertySymbol = TryCast(accessor.AssociatedSymbol, SourcePropertySymbol)
            If propertySymbol Is Nothing Then
                Return
            End If

            CopyAttributes(propertySymbol.GetAttributes(), compilation, attributes, AttributeDescription.AllowNullAttribute, AttributeDescription.DisallowNullAttribute, parameter)
        End Sub

        Friend Sub AddPropertyAccessorContracts(accessor As SourcePropertyAccessorSymbol, [property] As SourcePropertySymbol, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData))
            Dim compilation = accessor.DeclaringCompilation
            If compilation Is Nothing OrElse compilation.LanguageVersion < LanguageVersion.VbNext OrElse [property] Is Nothing Then
                Return
            End If

            CopyAttributes([property].GetAttributes(), compilation, attributes, AttributeDescription.MemberNotNullAttribute, AttributeDescription.MemberNotNullWhenAttribute)
        End Sub

        Friend Sub AddPropertyGetterReturnContracts(accessor As SourcePropertyAccessorSymbol, [property] As SourcePropertySymbol, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData))
            If accessor.MethodKind <> MethodKind.PropertyGet OrElse [property] Is Nothing Then
                Return
            End If

            Dim compilation = accessor.DeclaringCompilation
            If compilation Is Nothing OrElse compilation.LanguageVersion < LanguageVersion.VbNext Then
                Return
            End If

            CopyAttributes([property].GetAttributes(), compilation, attributes, AttributeDescription.MaybeNullAttribute, AttributeDescription.NotNullAttribute)
        End Sub

        Friend Function Combine(first As ImmutableArray(Of String), second As ImmutableArray(Of String)) As ImmutableArray(Of String)
            If first.IsDefaultOrEmpty Then
                Return If(second.IsDefault, ImmutableArray(Of String).Empty, second)
            End If
            If second.IsDefaultOrEmpty Then
                Return first
            End If

            Return first.AddRange(second)
        End Function

        Private Function UnconditionalAnnotations(allowNull As Boolean, disallowNull As Boolean, maybeNull As Boolean, notNull As Boolean) As FlowAnalysisAnnotations
            Dim annotations = FlowAnalysisAnnotations.None
            If allowNull Then
                annotations = annotations Or FlowAnalysisAnnotations.AllowNull
            End If
            If disallowNull Then
                annotations = annotations Or FlowAnalysisAnnotations.DisallowNull
            End If
            If maybeNull Then
                annotations = annotations Or FlowAnalysisAnnotations.MaybeNull
            End If
            If notNull Then
                annotations = annotations Or FlowAnalysisAnnotations.NotNull
            End If

            Return annotations
        End Function

        Private Sub AddNames(data As NullableMemberNotNullData, names As ImmutableArray(Of String), sense As Boolean)
            If names.IsDefaultOrEmpty Then
                Return
            End If

            For Each name In names
                If name IsNot Nothing Then
                    data.AddWhenMember(sense, name)
                End If
            Next
        End Sub

        Private Sub AddMemberNames(names As ImmutableArray(Of String), add As Action(Of String))
            If names.IsDefaultOrEmpty OrElse add Is Nothing Then
                Return
            End If

            For Each name In names
                If name IsNot Nothing Then
                    add(name)
                End If
            Next
        End Sub

        Private Sub CopyAttributes(source As ImmutableArray(Of VisualBasicAttributeData), compilation As VisualBasicCompilation, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData), first As AttributeDescription, second As AttributeDescription, Optional skip As Symbol = Nothing)
            If source.IsDefaultOrEmpty Then
                Return
            End If

            For Each attribute In source
                Dim description As AttributeDescription = Nothing
                If attribute.IsTargetAttribute(first) Then
                    description = first
                ElseIf attribute.IsTargetAttribute(second) Then
                    description = second
                Else
                    Continue For
                End If

                If skip IsNot Nothing AndAlso HasDeclaredAttribute(skip, description) Then
                    Continue For
                End If

                Symbol.AddSynthesizedAttribute(attributes, attribute.ToSynthesizedAttribute(compilation))
            Next
        End Sub

        Private Function HasDeclaredAttribute(symbol As Symbol, description As AttributeDescription) As Boolean
            Dim attributes = symbol.GetAttributes()
            If attributes.IsDefaultOrEmpty Then
                Return False
            End If

            For Each attribute In attributes
                If attribute.IsTargetAttribute(description) Then
                    Return True
                End If
            Next

            Return False
        End Function

        Private Function IsBadMetadata(ex As Exception) As Boolean
            Return TypeOf ex Is BadImageFormatException OrElse
                TypeOf ex Is ArgumentNullException OrElse
                TypeOf ex Is InvalidOperationException
        End Function
    End Module
End Namespace
