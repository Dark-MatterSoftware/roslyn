' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Reflection.Metadata

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE

    ''' <summary>
    ''' Applies the standard .NET nullable metadata contract
    ''' (<c>NullableAttribute</c> and <c>NullableContextAttribute</c>) to a VB type tree.
    ''' Bytes are 0 oblivious, 1 not annotated, and 2 annotated. A single byte, or a context
    ''' byte, applies to every annotated position. A byte array is a preorder walk and must
    ''' be consumed exactly. Malformed metadata leaves the type oblivious.
    ''' </summary>
    Friend NotInheritable Class NullableMetadataDecoder

        Private Const MaxDepth As Integer = 512

        Private Sub New()
        End Sub

        Friend Shared Function ContextValue(symbol As Symbol) As Byte?
            Dim current = symbol
            While current IsNot Nothing
                Dim method = TryCast(current, PEMethodSymbol)
                If method IsNot Nothing Then
                    Return method.GetNullableContextValue()
                End If

                Dim type = TryCast(current, PENamedTypeSymbol)
                If type IsNot Nothing Then
                    Return type.GetNullableContextValue()
                End If

                current = current.ContainingType
            End While

            Return Nothing
        End Function

        Friend Shared Function Transform(
            metadataType As TypeSymbol,
            target As EntityHandle,
            moduleSymbol As PEModuleSymbol,
            accessSymbol As Symbol,
            contextSymbol As Symbol) As DecodedNullableType

            If metadataType Is Nothing OrElse moduleSymbol Is Nothing Then
                Return New DecodedNullableType(metadataType, NullableAnnotation.None)
            End If

            Dim defaultFlag As Byte = 0
            Dim transforms As ImmutableArray(Of Byte) = Nothing
            Dim hasAttribute As Boolean = False

            If Not target.IsNil Then
                hasAttribute = moduleSymbol.[Module].HasNullableAttribute(target, defaultFlag, transforms)
            End If

            If Not hasAttribute Then
                Dim context = ContextValue(contextSymbol)
                If Not context.HasValue Then
                    Return New DecodedNullableType(metadataType, NullableAnnotation.None)
                End If

                defaultFlag = context.Value
                transforms = Nothing
            End If

            If accessSymbol IsNot Nothing AndAlso Not moduleSymbol.ShouldDecodeNullableAttributes(accessSymbol) Then
                Return New DecodedNullableType(metadataType, NullableAnnotation.None)
            End If

            Dim position As Integer = 0
            Dim node = TryBuild(metadataType, defaultFlag, transforms, position, depth:=0)
            If node Is Nothing Then
                Return New DecodedNullableType(metadataType, NullableAnnotation.None)
            End If

            If Not transforms.IsDefault AndAlso position <> transforms.Length Then
                Return New DecodedNullableType(metadataType, NullableAnnotation.None)
            End If

            Commit(node)
            Return New DecodedNullableType(metadataType, node.Annotation)
        End Function

        Private Shared Function TryBuild(
            type As TypeSymbol,
            defaultFlag As Byte,
            transforms As ImmutableArray(Of Byte),
            ByRef position As Integer,
            depth As Integer) As NullableTypeNode

            If type Is Nothing OrElse depth > MaxDepth Then
                Return Nothing
            End If

            ' Nullable(Of T) and other non-generic value types occupy no byte and have no annotated children.
            If IsNonGenericValueType(type) Then
                Return New NullableTypeNode(type, NullableAnnotation.None)
            End If

            Dim annotation As NullableAnnotation
            If Not TryTakeFlag(type, defaultFlag, transforms, position, annotation) Then
                Return Nothing
            End If

            Dim node As New NullableTypeNode(type, annotation)
            Dim current = node
            Dim currentType = type

            Do
                Dim pointer = TryCast(currentType, PointerTypeSymbol)
                If pointer IsNot Nothing Then
                    current.Element = TryBuild(pointer.PointedAtType, defaultFlag, transforms, position, depth + 1)
                    If current.Element Is Nothing Then
                        Return Nothing
                    End If

                    Return node
                End If

                Dim array = TryCast(currentType, ArrayTypeSymbol)
                If array Is Nothing Then
                    Exit Do
                End If

                Dim elementAnnotation As NullableAnnotation
                If Not TryTakeFlag(array.ElementType, defaultFlag, transforms, position, elementAnnotation) Then
                    Return Nothing
                End If

                Dim elementNode As New NullableTypeNode(array.ElementType, elementAnnotation)
                current.Element = elementNode
                current = elementNode
                currentType = array.ElementType
                depth += 1
                If depth > MaxDepth Then
                    Return Nothing
                End If
            Loop

            Dim named = TryCast(currentType, NamedTypeSymbol)
            If named IsNot Nothing Then
                If Not TryAttachNamedChildren(current, named, defaultFlag, transforms, position, depth) Then
                    Return Nothing
                End If
            End If

            Return node
        End Function

        Private Shared Function TryTakeFlag(
            type As TypeSymbol,
            defaultFlag As Byte,
            transforms As ImmutableArray(Of Byte),
            ByRef position As Integer,
            ByRef annotation As NullableAnnotation) As Boolean

            Dim flag As Byte
            If IsNonGenericValueType(type) Then
                flag = 0
            ElseIf transforms.IsDefault Then
                flag = defaultFlag
            ElseIf position < transforms.Length Then
                flag = transforms(position)
                position += 1
            Else
                annotation = NullableAnnotation.None
                Return False
            End If

            If flag > 2 Then
                annotation = NullableAnnotation.None
                Return False
            End If

            annotation = CType(flag, NullableAnnotation)
            Return True
        End Function

        ''' <summary>
        ''' A non-generic value type, including <c>Nullable(Of T)</c>, does not occupy a byte.
        ''' Type parameters, arrays, pointers, and other generic types do.
        ''' </summary>
        Private Shared Function IsNonGenericValueType(type As TypeSymbol) As Boolean
            If type Is Nothing OrElse TypeOf type Is PointerTypeSymbol OrElse type.TypeKind = TypeKind.Array OrElse type.IsTupleType Then
                Return False
            End If

            Dim named = TryCast(type, NamedTypeSymbol)
            If named Is Nothing Then
                Return False
            End If

            If named.IsGenericType Then
                Return type.IsNullableType()
            End If

            Return type.IsValueType
        End Function

        Private Shared Function TryAttachNamedChildren(
            node As NullableTypeNode,
            named As NamedTypeSymbol,
            defaultFlag As Byte,
            transforms As ImmutableArray(Of Byte),
            ByRef position As Integer,
            depth As Integer) As Boolean

            Dim shape = named
            If named.IsTupleType AndAlso named.TupleUnderlyingType IsNot Nothing Then
                shape = named.TupleUnderlyingType
            End If

            Dim containing As NullableTypeNode = Nothing
            If shape.ContainingType IsNot Nothing Then
                If Not TryBuildContainer(shape.ContainingType, defaultFlag, transforms, position, depth + 1, containing) Then
                    Return False
                End If
            End If

            node.Containing = containing
            Return TryBuildArguments(shape, defaultFlag, transforms, position, depth + 1, node.Arguments)
        End Function

        ''' <summary>
        ''' Containing types contribute their type arguments, not a byte for the container itself.
        ''' </summary>
        Private Shared Function TryBuildContainer(
            container As NamedTypeSymbol,
            defaultFlag As Byte,
            transforms As ImmutableArray(Of Byte),
            ByRef position As Integer,
            depth As Integer,
            ByRef node As NullableTypeNode) As Boolean

            node = Nothing
            If container Is Nothing OrElse depth > MaxDepth Then
                Return container Is Nothing
            End If

            Dim containing As NullableTypeNode = Nothing
            If container.ContainingType IsNot Nothing Then
                If Not TryBuildContainer(container.ContainingType, defaultFlag, transforms, position, depth + 1, containing) Then
                    Return False
                End If
            End If

            Dim arguments As ImmutableArray(Of NullableTypeNode) = Nothing
            If Not TryBuildArguments(container, defaultFlag, transforms, position, depth + 1, arguments) Then
                Return False
            End If

            If containing Is Nothing AndAlso arguments.IsDefault Then
                Return True
            End If

            node = New NullableTypeNode(container, NullableAnnotation.None)
            node.Containing = containing
            node.Arguments = arguments
            Return True
        End Function

        Private Shared Function TryBuildArguments(
            named As NamedTypeSymbol,
            defaultFlag As Byte,
            transforms As ImmutableArray(Of Byte),
            ByRef position As Integer,
            depth As Integer,
            ByRef arguments As ImmutableArray(Of NullableTypeNode)) As Boolean

            arguments = Nothing
            If named Is Nothing OrElse named.Arity = 0 Then
                Return True
            End If

            Dim typeArguments = named.TypeArgumentsNoUseSiteDiagnostics
            If typeArguments.Length <> named.Arity Then
                Return False
            End If

            Dim built(typeArguments.Length - 1) As NullableTypeNode
            For i = 0 To typeArguments.Length - 1
                built(i) = TryBuild(typeArguments(i), defaultFlag, transforms, position, depth)
                If built(i) Is Nothing Then
                    Return False
                End If
            Next

            arguments = built.AsImmutable()
            Return True
        End Function

        Private Shared Sub Commit(node As NullableTypeNode)
            If node Is Nothing Then
                Return
            End If

            If node.Containing IsNot Nothing Then
                Commit(node.Containing)
            End If

            Dim array = TryCast(node.Type, ArrayTypeSymbol)
            If array IsNot Nothing AndAlso node.Element IsNot Nothing Then
                array.SetElementNullableAnnotation(node.Element.Annotation)
                Commit(node.Element)
                Return
            End If

            If node.Element IsNot Nothing Then
                Commit(node.Element)
            End If

            If node.Arguments.IsDefault Then
                Return
            End If

            Dim named = TryCast(node.Type, NamedTypeSymbol)
            If named Is Nothing Then
                Return
            End If

            If named.IsTupleType AndAlso named.TupleUnderlyingType IsNot Nothing Then
                named = named.TupleUnderlyingType
            End If

            If Not named.IsDefinition AndAlso node.Arguments.Length = named.Arity Then
                Dim annotations(node.Arguments.Length - 1) As NullableAnnotation
                For i = 0 To node.Arguments.Length - 1
                    annotations(i) = node.Arguments(i).Annotation
                Next

                named.SetTypeArgumentNullableAnnotations(annotations.AsImmutable())
            End If

            For Each argument In node.Arguments
                Commit(argument)
            Next
        End Sub

        Private NotInheritable Class NullableTypeNode
            Public ReadOnly Type As TypeSymbol
            Public ReadOnly Annotation As NullableAnnotation
            Public Element As NullableTypeNode
            Public Containing As NullableTypeNode
            Public Arguments As ImmutableArray(Of NullableTypeNode)

            Public Sub New(type As TypeSymbol, annotation As NullableAnnotation)
                Me.Type = type
                Me.Annotation = annotation
            End Sub
        End Class

    End Class

    Friend Structure DecodedNullableType
        Public ReadOnly Type As TypeSymbol
        Public ReadOnly Annotation As NullableAnnotation

        Public Sub New(type As TypeSymbol, annotation As NullableAnnotation)
            Me.Type = type
            Me.Annotation = annotation
        End Sub
    End Structure

End Namespace
