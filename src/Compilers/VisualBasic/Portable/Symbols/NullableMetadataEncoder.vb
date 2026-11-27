' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' Encodes one source type tree using the standard nullable-metadata byte walk.
    ''' A byte is recorded for every metadata type node that is not a non-generic value type
    ''' or <see cref="System.Nullable(Of T)"/>. Containing types contribute their type arguments
    ''' without a byte for the container. Arrays contribute one byte and then the element type.
    ''' </summary>
    Friend Module NullableMetadataEncoder

        Private Const MaxDepth As Integer = 512

        <ThreadStatic>
        Private reportingPredefinedNullableAttribute As Boolean

        Friend Sub NoteRequired(symbol As Symbol, type As TypeSymbol, annotation As NullableAnnotation, Optional diagnostics As BindingDiagnosticBag = Nothing)
            Dim compilation = symbol.DeclaringCompilation
            If compilation Is Nothing OrElse compilation.LanguageVersion < LanguageVersion.VbNext Then
                Return
            End If

            If IsEmbeddedRuntimeImplementation(symbol) Then
                Return
            End If

            If HasNullableFlags(Encode(type, annotation)) Then
                compilation.MarkNullableMetadataRequired()
                If diagnostics IsNot Nothing Then
                    ReportPredefinedNullableAttribute(compilation, symbol, diagnostics)
                End If
            End If
        End Sub

        ''' <summary>
        ''' When <c>System.Runtime.CompilerServices.NullableAttribute</c> is present, both standard
        ''' constructors are required. A missing or unusable constructor uses the existing missing
        ''' runtime-helper diagnostic. A missing type is left for the embedded definition.
        ''' </summary>
        Private Sub ReportPredefinedNullableAttribute(compilation As VisualBasicCompilation, symbol As Symbol, diagnostics As BindingDiagnosticBag)
            ' Resolving the predefined type can bind the declaration that requested the report.
            If reportingPredefinedNullableAttribute Then
                Return
            End If

            reportingPredefinedNullableAttribute = True
            Try
                ReportPredefinedNullableAttributeCore(compilation, symbol, diagnostics)
            Finally
                reportingPredefinedNullableAttribute = False
            End Try
        End Sub

        Private Sub ReportPredefinedNullableAttributeCore(compilation As VisualBasicCompilation, symbol As Symbol, diagnostics As BindingDiagnosticBag)
            Dim existing = compilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_NullableAttribute)
            If TypeOf existing Is MissingMetadataTypeSymbol Then
                Return
            End If

            Dim location = symbol.Locations.FirstOrDefault()
            If location Is Nothing Then
                location = NoLocation.Singleton
            End If

            Binder.ReportUseSiteInfoForSynthesizedAttribute(WellKnownMember.System_Runtime_CompilerServices_NullableAttribute__ctorByte, compilation, location, diagnostics)
            Binder.ReportUseSiteInfoForSynthesizedAttribute(WellKnownMember.System_Runtime_CompilerServices_NullableAttribute__ctorTransformFlags, compilation, location, diagnostics)
        End Sub

        Friend Function IsEmbeddedRuntimeImplementation(symbol As Symbol) As Boolean
            Dim kind = symbol.EmbeddedSymbolKind
            Return (kind And (EmbeddedSymbolKind.VbCore Or EmbeddedSymbolKind.XmlHelper)) <> 0
        End Function

        Friend Function Encode(type As TypeSymbol, annotation As NullableAnnotation) As ImmutableArray(Of Byte)
            Dim builder = ArrayBuilder(Of Byte).GetInstance()
            Dim depth = 0
            AddType(type, annotation, builder, depth)
            Return builder.ToImmutableAndFree()
        End Function

        Friend Function HasNullableFlags(transforms As ImmutableArray(Of Byte)) As Boolean
            If transforms.IsDefaultOrEmpty Then
                Return False
            End If

            For Each flag In transforms
                If flag <> 0 Then
                    Return True
                End If
            Next

            Return False
        End Function

        Friend Function IsUniform(transforms As ImmutableArray(Of Byte)) As Boolean
            If transforms.IsDefaultOrEmpty Then
                Return True
            End If

            Dim first = transforms(0)
            For i = 1 To transforms.Length - 1
                If transforms(i) <> first Then
                    Return False
                End If
            Next

            Return True
        End Function

        Private Sub AddType(type As TypeSymbol, annotation As NullableAnnotation, builder As ArrayBuilder(Of Byte), ByRef depth As Integer)
            If type Is Nothing Then
                Return
            End If

            depth += 1
            If depth > MaxDepth Then
                Throw ExceptionUtilities.Unreachable
            End If

            If Not IsNonGenericValueType(type) Then
                builder.Add(MetadataFlag(type, annotation))
            End If

            If Not type.IsErrorType() Then
                If type.TypeKind = TypeKind.Array Then
                    Dim arrayType = DirectCast(type, ArrayTypeSymbol)
                    AddType(arrayType.ElementType, arrayType.ElementNullableAnnotation, builder, depth)
                ElseIf type.TypeKind = TypeKind.Pointer Then
                    Dim pointerType = TryCast(type, PointerTypeSymbol)
                    If pointerType IsNot Nothing Then
                        AddType(pointerType.PointedAtType, NullableAnnotation.None, builder, depth)
                    End If
                Else
                    Dim named = TryCast(type, NamedTypeSymbol)
                    If named IsNot Nothing Then
                        Dim children = If(named.IsTupleType, named.TupleUnderlyingType, named)
                        If children IsNot Nothing Then
                            AddNamedChildren(children, builder, depth)
                        End If
                    End If
                End If
            End If

            depth -= 1
        End Sub

        Private Sub AddNamedChildren(named As NamedTypeSymbol, builder As ArrayBuilder(Of Byte), ByRef depth As Integer)
            If named Is Nothing OrElse named.IsErrorType() OrElse named.IsUnboundGenericType Then
                Return
            End If

            Dim containing = named.ContainingType
            If containing IsNot Nothing Then
                AddNamedChildren(containing, builder, depth)
            End If

            If named.Arity = 0 Then
                Return
            End If

            Dim arguments = named.TypeArgumentsNoUseSiteDiagnostics
            Dim annotations = named.GetTypeArgumentNullableAnnotations()
            For i = 0 To arguments.Length - 1
                Dim argumentAnnotation = NullableAnnotation.None
                If Not annotations.IsDefault AndAlso i < annotations.Length Then
                    argumentAnnotation = annotations(i)
                End If

                AddType(arguments(i), argumentAnnotation, builder, depth)
            Next
        End Sub

        Private Function IsNonGenericValueType(type As TypeSymbol) As Boolean
            If type.IsTupleType Then
                Return False
            End If

            If type.IsNullableType() Then
                Return True
            End If

            Dim named = TryCast(type, NamedTypeSymbol)
            Return named IsNot Nothing AndAlso named.IsValueType AndAlso named.Arity = 0
        End Function

        Private Function MetadataFlag(type As TypeSymbol, annotation As NullableAnnotation) As Byte
            If annotation = NullableAnnotation.None OrElse type.IsValueType Then
                Return 0
            ElseIf annotation = NullableAnnotation.Annotated Then
                Return 2
            Else
                Return 1
            End If
        End Function

    End Module

End Namespace
