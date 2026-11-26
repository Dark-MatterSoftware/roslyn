' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' Represents a compiler generated backing field for an automatically implemented property.
    ''' </summary>
    Friend Class SynthesizedPropertyBackingFieldSymbol
        Inherits SynthesizedBackingFieldBase(Of SourcePropertySymbol)

        Public Sub New([property] As SourcePropertySymbol, name As String, isShared As Boolean)
            MyBase.New([property], name, isShared)
        End Sub

        Public Overrides ReadOnly Property Type As TypeSymbol
            Get
                Return _propertyOrEvent.Type
            End Get
        End Property

        Friend Overrides Function GetNullableAnnotation() As NullableAnnotation
            Return _propertyOrEvent.GetNullableAnnotation()
        End Function

        Friend Overrides Sub AddSynthesizedAttributes(moduleBuilder As PEModuleBuilder, ByRef attributes As ArrayBuilder(Of VisualBasicAttributeData))
            MyBase.AddSynthesizedAttributes(moduleBuilder, attributes)
            moduleBuilder.AddNullableMetadataAttribute(Me, attributes, Type, GetNullableAnnotation())
        End Sub
    End Class
End Namespace
