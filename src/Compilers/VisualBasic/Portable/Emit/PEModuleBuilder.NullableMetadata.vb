' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.Emit

    Partial Friend MustInherit Class PEModuleBuilder

        Private _lazyNullableAttribute As SynthesizedEmbeddedNullableAttributeSymbol

        Friend Function PrepareEmbeddedNullableAttributes() As ImmutableArray(Of NamedTypeSymbol)
            Dim attribute = GetOrCreateEmbeddedNullableAttribute()
            If attribute Is Nothing Then
                Return ImmutableArray(Of NamedTypeSymbol).Empty
            End If

            Return ImmutableArray.Create(Of NamedTypeSymbol)(attribute)
        End Function

        Public Overrides Function GetEmbeddedTypes(diagnostics As DiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            If _lazyNullableAttribute Is Nothing Then
                Return ImmutableArray(Of NamedTypeSymbol).Empty
            End If

            Return ImmutableArray.Create(Of NamedTypeSymbol)(_lazyNullableAttribute)
        End Function

        Friend Sub AddNullableMetadataAttribute(symbol As Symbol, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData), type As TypeSymbol, annotation As NullableAnnotation)
            If Compilation.LanguageVersion < LanguageVersion.VbNext Then
                Return
            End If

            If NullableMetadataEncoder.IsEmbeddedRuntimeImplementation(symbol) Then
                Return
            End If

            Dim transforms = NullableMetadataEncoder.Encode(type, annotation)
            If Not NullableMetadataEncoder.HasNullableFlags(transforms) Then
                Return
            End If

            Dim synthesized = TrySynthesizeNullableAttribute(transforms)
            If synthesized Is Nothing Then
                Return
            End If

            Symbol.AddSynthesizedAttribute(attributes, synthesized)
        End Sub

        Private Function TrySynthesizeNullableAttribute(transforms As ImmutableArray(Of Byte)) As SynthesizedAttributeData
            Dim byteArray = Not NullableMetadataEncoder.IsUniform(transforms)
            Dim constructor As MethodSymbol
            Dim arguments As ImmutableArray(Of TypedConstant)
            Dim byteType = Compilation.GetSpecialType(SpecialType.System_Byte)
            Dim frameworkConstructor = TryGetFrameworkNullableConstructor(byteArray)
            If frameworkConstructor IsNot Nothing Then
                constructor = frameworkConstructor
            Else
                Dim embedded = GetOrCreateEmbeddedNullableAttribute()
                If embedded Is Nothing Then
                    ' A predefined NullableAttribute without the standard constructor is not replaced
                    ' by an embedded definition. Emitting against it would write an invalid attribute.
                    Return Nothing
                End If

                constructor = If(byteArray, embedded.ByteArrayConstructor, embedded.ByteConstructor)
            End If

            If byteArray Then
                Dim arrayType = Compilation.CreateArrayTypeSymbol(byteType)
                Dim constants(transforms.Length - 1) As TypedConstant
                For i = 0 To transforms.Length - 1
                    constants(i) = New TypedConstant(byteType, TypedConstantKind.Primitive, transforms(i))
                Next

                arguments = ImmutableArray.Create(New TypedConstant(arrayType, constants.AsImmutableOrNull()))
            Else
                arguments = ImmutableArray.Create(New TypedConstant(byteType, TypedConstantKind.Primitive, transforms(0)))
            End If

            Return New SynthesizedAttributeData(Compilation, constructor, arguments, ImmutableArray(Of KeyValuePair(Of String, TypedConstant)).Empty)
        End Function

        Private Function TryGetFrameworkNullableConstructor(byteArray As Boolean) As MethodSymbol
            Dim existing = Compilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_NullableAttribute)
            If TypeOf existing Is MissingMetadataTypeSymbol Then
                Return Nothing
            End If

            Dim member = If(byteArray,
                             WellKnownMember.System_Runtime_CompilerServices_NullableAttribute__ctorTransformFlags,
                             WellKnownMember.System_Runtime_CompilerServices_NullableAttribute__ctorByte)
            Dim constructor = TryCast(Compilation.GetWellKnownTypeMember(member), MethodSymbol)
            If constructor Is Nothing OrElse
               Binder.GetUseSiteInfoForWellKnownTypeMember(constructor, member, False).DiagnosticInfo IsNot Nothing Then
                ' The framework type is present but does not have the standard constructor.
                ' Embedding a second definition would change the metadata identity.
                Return Nothing
            End If

            Return constructor
        End Function

        Private Function GetOrCreateEmbeddedNullableAttribute() As SynthesizedEmbeddedNullableAttributeSymbol
            Dim existing = _lazyNullableAttribute
            If existing IsNot Nothing Then
                Return existing
            End If

            If Not Compilation.IsNullableMetadataRequired Then
                Return Nothing
            End If

            If Not TypeOf Compilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_NullableAttribute) Is MissingMetadataTypeSymbol Then
                Return Nothing
            End If

            Dim byteType = Compilation.GetSpecialType(SpecialType.System_Byte)
            Dim attributeType = Compilation.GetWellKnownType(WellKnownType.System_Attribute)
            If byteType.IsErrorType() OrElse attributeType.IsErrorType() Then
                Throw ExceptionUtilities.Unreachable
            End If

            Dim created = New SynthesizedEmbeddedNullableAttributeSymbol(
                GetOrCreateNullableAttributeNamespace(),
                attributeType,
                byteType,
                Compilation.CreateArrayTypeSymbol(byteType))
            Dim prior = Interlocked.CompareExchange(_lazyNullableAttribute, created, Nothing)
            Return If(prior, created)
        End Function

        Private Function GetOrCreateNullableAttributeNamespace() As NamespaceSymbol
            Dim result As NamespaceSymbol = SourceModule.GlobalNamespace
            For Each partName In SynthesizedEmbeddedNullableAttributeSymbol.NamespaceName.Split("."c)
                Dim subnamespace As NamespaceSymbol = Nothing
                For Each member In result.GetMembers(partName)
                    If member.Kind = SymbolKind.Namespace Then
                        subnamespace = DirectCast(member, NamespaceSymbol)
                        Exit For
                    End If
                Next

                If subnamespace Is Nothing Then
                    subnamespace = New SynthesizedNamespaceSymbol(result, partName)
                    AddSynthesizedDefinition(result, subnamespace)
                End If

                result = subnamespace
            Next

            Return result
        End Function

    End Class

End Namespace
