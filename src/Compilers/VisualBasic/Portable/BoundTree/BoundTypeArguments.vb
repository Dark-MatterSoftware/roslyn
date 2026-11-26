' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class BoundTypeArguments
        Private _typeArgumentAnnotations As ImmutableArray(Of NullableAnnotation)

        Friend ReadOnly Property TypeArgumentAnnotations As ImmutableArray(Of NullableAnnotation)
            Get
                Return _typeArgumentAnnotations
            End Get
        End Property

        Friend Sub SetTypeArgumentAnnotations(annotations As ImmutableArray(Of NullableAnnotation))
            _typeArgumentAnnotations = annotations
        End Sub
    End Class
End Namespace
