' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' Copies nullable annotations onto a type produced by substitution.
    ''' Annotations are not part of CLR type identity, so substitution builds a new
    ''' type that would otherwise drop them.
    ''' </summary>
    Friend Module NullableAnnotationSubstitution

        Friend Function Preserve(original As TypeSymbol, substituted As TypeSymbol) As TypeSymbol
            If original Is Nothing OrElse substituted Is Nothing OrElse original Is substituted Then
                Return substituted
            End If

            PreserveCore(original, substituted)
            Return substituted
        End Function

        Private Sub PreserveCore(original As TypeSymbol, substituted As TypeSymbol)
            If original Is Nothing OrElse substituted Is Nothing OrElse original Is substituted Then
                Return
            End If

            Dim originalArray = TryCast(original, ArrayTypeSymbol)
            Dim substitutedArray = TryCast(substituted, ArrayTypeSymbol)
            If originalArray IsNot Nothing AndAlso substitutedArray IsNot Nothing Then
                substitutedArray.SetElementNullableAnnotation(AdjustTopLevel(originalArray.ElementType, substitutedArray.ElementType, originalArray.ElementNullableAnnotation))
                PreserveCore(originalArray.ElementType, substitutedArray.ElementType)
                Return
            End If

            Dim originalNamed = TryCast(original, NamedTypeSymbol)
            Dim substitutedNamed = TryCast(substituted, NamedTypeSymbol)
            If originalNamed Is Nothing OrElse substitutedNamed Is Nothing Then
                Return
            End If

            If originalNamed.ContainingType IsNot Nothing AndAlso
               substitutedNamed.ContainingType IsNot Nothing AndAlso
               substitutedNamed.ContainingType IsNot originalNamed.ContainingType Then
                PreserveCore(originalNamed.ContainingType, substitutedNamed.ContainingType)
            End If

            If originalNamed.Arity = 0 OrElse substitutedNamed.Arity <> originalNamed.Arity OrElse substitutedNamed.IsDefinition Then
                Return
            End If

            Dim originalArguments = originalNamed.TypeArgumentsNoUseSiteDiagnostics
            Dim substitutedArguments = substitutedNamed.TypeArgumentsNoUseSiteDiagnostics
            Dim originalAnnotations = originalNamed.GetTypeArgumentNullableAnnotations()
            If originalArguments.Length <> substitutedArguments.Length OrElse
               originalAnnotations.IsDefault OrElse
               originalAnnotations.Length <> substitutedArguments.Length Then
                Return
            End If

            Dim adjusted(substitutedArguments.Length - 1) As NullableAnnotation
            For i = 0 To substitutedArguments.Length - 1
                adjusted(i) = AdjustTopLevel(originalArguments(i), substitutedArguments(i), originalAnnotations(i))
                PreserveCore(originalArguments(i), substitutedArguments(i))
            Next

            substitutedNamed.SetTypeArgumentNullableAnnotations(adjusted.AsImmutableOrNull())
        End Sub

        Friend Function AdjustTopLevel(originalType As TypeSymbol, substitutedType As TypeSymbol, annotation As NullableAnnotation) As NullableAnnotation
            ' T? is an annotation on T. Substituting a value type, including Nullable(Of T),
            ' drops that annotation and does not produce another Nullable(Of T).
            If originalType IsNot Nothing AndAlso
               originalType.TypeKind = TypeKind.TypeParameter AndAlso
               substitutedType IsNot Nothing AndAlso
               substitutedType.IsValueType Then
                Return NullableAnnotation.None
            End If

            Return annotation
        End Function

    End Module

End Namespace
