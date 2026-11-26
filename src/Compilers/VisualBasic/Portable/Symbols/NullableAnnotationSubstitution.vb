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

        Friend Function Preserve(original As TypeSymbol, substituted As TypeSymbol, Optional containing As NamedTypeSymbol = Nothing, Optional method As MethodSymbol = Nothing) As TypeSymbol
            If original Is Nothing OrElse substituted Is Nothing OrElse original Is substituted Then
                Return substituted
            End If

            PreserveCore(original, substituted, containing, method)
            Return substituted
        End Function

        Private Sub PreserveCore(original As TypeSymbol, substituted As TypeSymbol, containing As NamedTypeSymbol, method As MethodSymbol)
            If original Is Nothing OrElse substituted Is Nothing OrElse original Is substituted Then
                Return
            End If

            Dim originalArray = TryCast(original, ArrayTypeSymbol)
            Dim substitutedArray = TryCast(substituted, ArrayTypeSymbol)
            If originalArray IsNot Nothing AndAlso substitutedArray IsNot Nothing Then
                substitutedArray.SetElementNullableAnnotation(SubstituteMemberAnnotation(originalArray.ElementType, substitutedArray.ElementType, originalArray.ElementNullableAnnotation, containing, method))
                PreserveCore(originalArray.ElementType, substitutedArray.ElementType, containing, method)
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
                PreserveCore(originalNamed.ContainingType, substitutedNamed.ContainingType, containing, method)
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
                adjusted(i) = SubstituteMemberAnnotation(originalArguments(i), substitutedArguments(i), originalAnnotations(i), containing, method)
                PreserveCore(originalArguments(i), substitutedArguments(i), containing, method)
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

        Friend Function SubstituteMemberAnnotation(originalType As TypeSymbol, substitutedType As TypeSymbol, annotation As NullableAnnotation, containingType As NamedTypeSymbol, Optional method As MethodSymbol = Nothing) As NullableAnnotation
            Dim adjusted = AdjustTopLevel(originalType, substitutedType, annotation)
            If annotation = NullableAnnotation.Annotated AndAlso adjusted <> NullableAnnotation.None Then
                Return NullableAnnotation.Annotated
            End If

            Dim parameter = TryCast(originalType, TypeParameterSymbol)
            If parameter IsNot Nothing AndAlso method IsNot Nothing Then
                Dim methodOwner = TryCast(parameter.ContainingSymbol, MethodSymbol)
                If methodOwner IsNot Nothing AndAlso method.OriginalDefinition Is methodOwner.OriginalDefinition Then
                    Dim methodAnnotations = method.GetTypeArgumentNullableAnnotations()
                    If Not methodAnnotations.IsDefault AndAlso
                       parameter.Ordinal >= 0 AndAlso
                       parameter.Ordinal < methodAnnotations.Length AndAlso
                       methodAnnotations(parameter.Ordinal) <> NullableAnnotation.None Then
                        If substitutedType IsNot Nothing AndAlso substitutedType.IsValueType Then
                            Return NullableAnnotation.None
                        End If

                        Return methodAnnotations(parameter.Ordinal)
                    End If
                End If
            End If

            Dim owner = TryCast(If(parameter Is Nothing, Nothing, parameter.ContainingSymbol), NamedTypeSymbol)
            Dim current = containingType
            Dim guard = 0
            While parameter IsNot Nothing AndAlso owner IsNot Nothing AndAlso current IsNot Nothing AndAlso guard < 16
                guard += 1
                If current.OriginalDefinition Is owner Then
                    Dim annotations = current.GetTypeArgumentNullableAnnotations()
                    If parameter.Ordinal >= 0 AndAlso parameter.Ordinal < annotations.Length AndAlso annotations(parameter.Ordinal) <> NullableAnnotation.None Then
                        Return annotations(parameter.Ordinal)
                    End If

                    Exit While
                End If

                Dim baseType = current.BaseTypeNoUseSiteDiagnostics
                If baseType IsNot Nothing AndAlso baseType.SpecialType <> SpecialType.System_Object Then
                    current = baseType
                Else
                    current = current.ContainingType
                End If
            End While

            Return adjusted
        End Function

    End Module

End Namespace
