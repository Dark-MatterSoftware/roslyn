' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.Emit

    Partial Friend MustInherit Class PEModuleBuilder

        Private _lazyExtensionMarkerAttribute As SynthesizedEmbeddedExtensionMarkerAttributeSymbol
        Private _synthesizedNamespaces As Dictionary(Of NamespaceSymbol, Dictionary(Of String, NamespaceSymbol))
        Private ReadOnly _synthesizedNamespaceGate As New Object()

        Friend ReadOnly Property EmbeddedExtensionMarkerAttribute As NamedTypeSymbol
            Get
                Return _lazyExtensionMarkerAttribute
            End Get
        End Property

        ''' <summary>
        ''' Reports the required marker constructor when an extension property is emitted and an
        ''' existing attribute definition cannot supply it. A missing attribute is left for embedding.
        ''' </summary>
        Friend Shared Sub ReportRequiredExtensionMarkerContract(compilation As VisualBasicCompilation, symbol As Symbol, diagnostics As BindingDiagnosticBag)
            If diagnostics Is Nothing OrElse symbol Is Nothing Then
                Return
            End If

            Dim existing = compilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_ExtensionMarkerAttribute)
            If TypeOf existing Is MissingMetadataTypeSymbol Then
                Return
            End If

            Dim location = symbol.Locations.FirstOrDefault()
            If location Is Nothing Then
                location = NoLocation.Singleton
            End If

            Binder.ReportUseSiteInfoForSynthesizedAttribute(
                WellKnownMember.System_Runtime_CompilerServices_ExtensionMarkerAttribute__ctor,
                compilation,
                location,
                diagnostics)
        End Sub

        ''' <summary>
        ''' Creates the embedded marker attribute when this module emits extension properties
        ''' and the target reference set does not define the framework contract.
        ''' An existing type with an unusable constructor is not replaced.
        ''' </summary>
        Friend Sub EnsureExtensionMarkerAttribute()
            Dim existing = Compilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_ExtensionMarkerAttribute)
            If TypeOf existing Is MissingMetadataTypeSymbol Then
                GetOrCreateEmbeddedExtensionMarkerAttribute()
            End If
        End Sub

        Friend Function TrySynthesizeExtensionMarkerAttribute(markerName As String) As SynthesizedAttributeData
            Dim stringType = Compilation.GetSpecialType(SpecialType.System_String)
            Dim arguments = ImmutableArray.Create(New TypedConstant(stringType, TypedConstantKind.Primitive, markerName))
            Dim existing = Compilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_ExtensionMarkerAttribute)
            If Not TypeOf existing Is MissingMetadataTypeSymbol Then
                ' Present, including a malformed constructor. Do not embed a second definition.
                Return Compilation.TrySynthesizeAttribute(
                    WellKnownMember.System_Runtime_CompilerServices_ExtensionMarkerAttribute__ctor,
                    arguments,
                    isOptionalUse:=True)
            End If

            Dim embedded = GetOrCreateEmbeddedExtensionMarkerAttribute()
            If embedded Is Nothing Then
                Return Nothing
            End If

            Return New SynthesizedAttributeData(
                Compilation,
                embedded.StringConstructor,
                arguments,
                ImmutableArray(Of KeyValuePair(Of String, TypedConstant)).Empty)
        End Function

        Private Function GetOrCreateEmbeddedExtensionMarkerAttribute() As SynthesizedEmbeddedExtensionMarkerAttributeSymbol
            Dim existing = _lazyExtensionMarkerAttribute
            If existing IsNot Nothing Then
                Return existing
            End If

            Dim stringType = Compilation.GetSpecialType(SpecialType.System_String)
            Dim attributeType = Compilation.GetWellKnownType(WellKnownType.System_Attribute)
            If stringType.IsErrorType() OrElse attributeType.IsErrorType() Then
                Return Nothing
            End If

            Dim created = New SynthesizedEmbeddedExtensionMarkerAttributeSymbol(
                GetOrSynthesizeCompilerNamespace(SynthesizedEmbeddedExtensionMarkerAttributeSymbol.NamespaceName),
                attributeType,
                stringType)
            Dim prior = Interlocked.CompareExchange(_lazyExtensionMarkerAttribute, created, Nothing)
            Return If(prior, created)
        End Function

        Friend Function GetOrSynthesizeCompilerNamespace(namespaceFullName As String) As NamespaceSymbol
            SyncLock _synthesizedNamespaceGate
                Dim result As NamespaceSymbol = SourceModule.GlobalNamespace
                For Each partName In namespaceFullName.Split("."c)
                    Dim subnamespace As NamespaceSymbol = Nothing
                    For Each member In result.GetMembers(partName)
                        If member.Kind = SymbolKind.Namespace Then
                            subnamespace = DirectCast(member, NamespaceSymbol)
                            Exit For
                        End If
                    Next

                    If subnamespace Is Nothing Then
                        If _synthesizedNamespaces Is Nothing Then
                            _synthesizedNamespaces = New Dictionary(Of NamespaceSymbol, Dictionary(Of String, NamespaceSymbol))()
                        End If

                        Dim synthesized As Dictionary(Of String, NamespaceSymbol) = Nothing
                        If Not _synthesizedNamespaces.TryGetValue(result, synthesized) Then
                            synthesized = New Dictionary(Of String, NamespaceSymbol)(StringComparer.Ordinal)
                            _synthesizedNamespaces.Add(result, synthesized)
                        End If

                        If Not synthesized.TryGetValue(partName, subnamespace) Then
                            subnamespace = New SynthesizedNamespaceSymbol(result, partName)
                            synthesized.Add(partName, subnamespace)
                            AddSynthesizedDefinition(result, subnamespace)
                        End If
                    End If

                    result = subnamespace
                Next

                Return result
            End SyncLock
        End Function

    End Class

End Namespace
