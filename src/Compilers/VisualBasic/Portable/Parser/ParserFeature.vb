' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Runtime.CompilerServices

Namespace Microsoft.CodeAnalysis.VisualBasic.Syntax.InternalSyntax

    Friend Enum Feature
        AutoProperties
        LineContinuation
        StatementLambdas
        CoContraVariance
        CollectionInitializers
        SubLambdas
        ArrayLiterals
        AsyncExpressions
        Iterators
        GlobalNamespace
        NullPropagatingOperator
        NameOfExpressions
        InterpolatedStrings
        ReadonlyAutoProperties
        RegionsEverywhere
        MultilineStringLiterals
        CObjInAttributeArguments
        LineContinuationComments
        TypeOfIsNot
        YearFirstDateLiterals
        WarningDirectives
        PartialModules
        PartialInterfaces
        ImplementingReadonlyOrWriteonlyPropertyWithReadwrite
        DigitSeparators
        BinaryLiterals
        Tuples
        InferredTupleNames
        LeadingDigitSeparator
        NonTrailingNamedArguments
        PrivateProtected
        UnconstrainedTypeParameterInConditional
        CommentsAfterLineContinuation
        InitOnlySettersUsage
        UnmanagedConstraint
        OverloadResolutionPriority
        EnumConstraint
        AsyncMain
        ModuleInitializers
        SkipLocalsInit
        UnmanagedCallersOnly
        TargetTypedNew
        TargetTypedDefault
        NothingCoalescingAssignment
        UsingDeclaration
        ConstantInterpolatedString
        NullConditionalAssignment
        InitOnlyProperty
        RequiredProperty
        SetsRequiredMembers
        GenericAttributes
        ReadOnlyStructures
        NativeIntegers
        IndexFromEnd
        ArrayRange
        CovariantReturns
        RawStringLiteral
        Utf8StringLiteral
        TupleDeconstruction
        SelectCaseTypePattern
        PartialProperty
        PartialEvent
        PartialConstructor
        PrimaryConstructor
        CollectionExpression
        AsyncUsing
        AwaitEach
        PatternVariables
        ForEachDeconstruction
        RefFields
    End Enum

    Friend Module FeatureExtensions
        <Extension>
        Friend Function GetFeatureFlag(feature As Feature) As String
            Select Case feature
                Case Else
                    Return Nothing
            End Select
        End Function

        <Extension>
        Friend Function GetLanguageVersion(feature As Feature) As LanguageVersion

            Select Case feature
                Case Feature.AutoProperties,
                     Feature.LineContinuation,
                     Feature.StatementLambdas,
                     Feature.CoContraVariance,
                     Feature.CollectionInitializers,
                     Feature.SubLambdas,
                     Feature.ArrayLiterals
                    Return LanguageVersion.VisualBasic10

                Case Feature.AsyncExpressions,
                     Feature.Iterators,
                     Feature.GlobalNamespace
                    Return LanguageVersion.VisualBasic11

                Case Feature.NullPropagatingOperator,
                     Feature.NameOfExpressions,
                     Feature.InterpolatedStrings,
                     Feature.ReadonlyAutoProperties,
                     Feature.RegionsEverywhere,
                     Feature.MultilineStringLiterals,
                     Feature.CObjInAttributeArguments,
                     Feature.LineContinuationComments,
                     Feature.TypeOfIsNot,
                     Feature.YearFirstDateLiterals,
                     Feature.WarningDirectives,
                     Feature.PartialModules,
                     Feature.PartialInterfaces,
                     Feature.ImplementingReadonlyOrWriteonlyPropertyWithReadwrite
                    Return LanguageVersion.VisualBasic14

                Case Feature.Tuples,
                    Feature.BinaryLiterals,
                    Feature.DigitSeparators
                    Return LanguageVersion.VisualBasic15

                Case Feature.InferredTupleNames
                    Return LanguageVersion.VisualBasic15_3

                Case Feature.LeadingDigitSeparator,
                    Feature.NonTrailingNamedArguments,
                    Feature.PrivateProtected
                    Return LanguageVersion.VisualBasic15_5

                Case Feature.UnconstrainedTypeParameterInConditional,
                    Feature.CommentsAfterLineContinuation
                    Return LanguageVersion.VisualBasic16

                Case Feature.InitOnlySettersUsage
                    Return LanguageVersion.VisualBasic16_9

                Case Feature.UnmanagedConstraint,
                     Feature.OverloadResolutionPriority
                    Return LanguageVersion.VisualBasic17_13

                Case Feature.EnumConstraint,
                     Feature.AsyncMain,
                     Feature.ModuleInitializers,
                     Feature.SkipLocalsInit,
                     Feature.UnmanagedCallersOnly,
                     Feature.TargetTypedNew,
                     Feature.TargetTypedDefault,
                     Feature.NothingCoalescingAssignment,
                     Feature.UsingDeclaration,
                     Feature.ConstantInterpolatedString,
                     Feature.NullConditionalAssignment,
                     Feature.InitOnlyProperty,
                     Feature.RequiredProperty,
                     Feature.SetsRequiredMembers,
                     Feature.GenericAttributes,
                     Feature.ReadOnlyStructures,
                     Feature.NativeIntegers,
                     Feature.IndexFromEnd,
                     Feature.ArrayRange,
                     Feature.CovariantReturns,
                     Feature.RawStringLiteral,
                     Feature.Utf8StringLiteral,
                     Feature.TupleDeconstruction,
                     Feature.SelectCaseTypePattern,
                     Feature.PartialProperty,
                     Feature.PartialEvent,
                     Feature.PartialConstructor,
                     Feature.PrimaryConstructor,
                     Feature.CollectionExpression,
                     Feature.AsyncUsing,
                     Feature.AwaitEach,
                     Feature.PatternVariables,
                     Feature.ForEachDeconstruction,
                     Feature.RefFields
                    Return LanguageVersion.VbNext

                Case Else
                    Throw ExceptionUtilities.UnexpectedValue(feature)
            End Select

        End Function

        <Extension>
        Friend Function GetResourceId(feature As Feature) As ERRID
            Select Case feature
                Case Feature.AutoProperties
                    Return ERRID.FEATURE_AutoProperties
                Case Feature.ReadonlyAutoProperties
                    Return ERRID.FEATURE_ReadonlyAutoProperties
                Case Feature.LineContinuation
                    Return ERRID.FEATURE_LineContinuation
                Case Feature.StatementLambdas
                    Return ERRID.FEATURE_StatementLambdas
                Case Feature.CoContraVariance
                    Return ERRID.FEATURE_CoContraVariance
                Case Feature.CollectionInitializers
                    Return ERRID.FEATURE_CollectionInitializers
                Case Feature.SubLambdas
                    Return ERRID.FEATURE_SubLambdas
                Case Feature.ArrayLiterals
                    Return ERRID.FEATURE_ArrayLiterals
                Case Feature.AsyncExpressions
                    Return ERRID.FEATURE_AsyncExpressions
                Case Feature.Iterators
                    Return ERRID.FEATURE_Iterators
                Case Feature.GlobalNamespace
                    Return ERRID.FEATURE_GlobalNamespace
                Case Feature.NullPropagatingOperator
                    Return ERRID.FEATURE_NullPropagatingOperator
                Case Feature.NameOfExpressions
                    Return ERRID.FEATURE_NameOfExpressions
                Case Feature.RegionsEverywhere
                    Return ERRID.FEATURE_RegionsEverywhere
                Case Feature.MultilineStringLiterals
                    Return ERRID.FEATURE_MultilineStringLiterals
                Case Feature.CObjInAttributeArguments
                    Return ERRID.FEATURE_CObjInAttributeArguments
                Case Feature.LineContinuationComments
                    Return ERRID.FEATURE_LineContinuationComments
                Case Feature.TypeOfIsNot
                    Return ERRID.FEATURE_TypeOfIsNot
                Case Feature.YearFirstDateLiterals
                    Return ERRID.FEATURE_YearFirstDateLiterals
                Case Feature.WarningDirectives
                    Return ERRID.FEATURE_WarningDirectives
                Case Feature.PartialModules
                    Return ERRID.FEATURE_PartialModules
                Case Feature.PartialInterfaces
                    Return ERRID.FEATURE_PartialInterfaces
                Case Feature.ImplementingReadonlyOrWriteonlyPropertyWithReadwrite
                    Return ERRID.FEATURE_ImplementingReadonlyOrWriteonlyPropertyWithReadwrite
                Case Feature.DigitSeparators
                    Return ERRID.FEATURE_DigitSeparators
                Case Feature.BinaryLiterals
                    Return ERRID.FEATURE_BinaryLiterals
                Case Feature.Tuples
                    Return ERRID.FEATURE_Tuples
                Case Feature.LeadingDigitSeparator
                    Return ERRID.FEATURE_LeadingDigitSeparator
                Case Feature.PrivateProtected
                    Return ERRID.FEATURE_PrivateProtected
                Case Feature.InterpolatedStrings
                    Return ERRID.FEATURE_InterpolatedStrings
                Case Feature.UnconstrainedTypeParameterInConditional
                    Return ERRID.FEATURE_UnconstrainedTypeParameterInConditional
                Case Feature.CommentsAfterLineContinuation
                    Return ERRID.FEATURE_CommentsAfterLineContinuation
                Case Feature.InitOnlySettersUsage
                    Return ERRID.FEATURE_InitOnlySettersUsage
                Case Feature.UnmanagedConstraint
                    Return ERRID.FEATURE_UnmanagedConstraint
                Case Feature.OverloadResolutionPriority
                    Return ERRID.FEATURE_OverloadResolutionPriority
                Case Feature.EnumConstraint
                    Return ERRID.FEATURE_EnumConstraint
                Case Feature.AsyncMain
                    Return ERRID.FEATURE_AsyncMain
                Case Feature.ModuleInitializers
                    Return ERRID.FEATURE_ModuleInitializers
                Case Feature.SkipLocalsInit
                    Return ERRID.FEATURE_SkipLocalsInit
                Case Feature.UnmanagedCallersOnly
                    Return ERRID.FEATURE_UnmanagedCallersOnly
                Case Feature.TargetTypedNew
                    Return ERRID.FEATURE_TargetTypedNew
                Case Feature.TargetTypedDefault
                    Return ERRID.FEATURE_TargetTypedDefault
                Case Feature.NothingCoalescingAssignment
                    Return ERRID.FEATURE_NothingCoalescingAssignment
                Case Feature.UsingDeclaration
                    Return ERRID.FEATURE_UsingDeclaration
                Case Feature.ConstantInterpolatedString
                    Return ERRID.FEATURE_ConstantInterpolatedString
                Case Feature.NullConditionalAssignment
                    Return ERRID.FEATURE_NullConditionalAssignment
                Case Feature.InitOnlyProperty
                    Return ERRID.FEATURE_InitOnlyProperty
                Case Feature.RequiredProperty
                    Return ERRID.FEATURE_RequiredProperty
                Case Feature.SetsRequiredMembers
                    Return ERRID.FEATURE_SetsRequiredMembers
                Case Feature.GenericAttributes
                    Return ERRID.FEATURE_GenericAttributes
                Case Feature.ReadOnlyStructures
                    Return ERRID.FEATURE_ReadOnlyStructures
                Case Feature.NativeIntegers
                    Return ERRID.FEATURE_NativeIntegers
                Case Feature.IndexFromEnd
                    Return ERRID.FEATURE_IndexFromEnd
                Case Feature.ArrayRange
                    Return ERRID.FEATURE_ArrayRange
                Case Feature.CovariantReturns
                    Return ERRID.FEATURE_CovariantReturns
                Case Feature.RawStringLiteral
                    Return ERRID.FEATURE_RawStringLiteral
                Case Feature.Utf8StringLiteral
                    Return ERRID.FEATURE_Utf8StringLiteral
                Case Feature.TupleDeconstruction
                    Return ERRID.FEATURE_TupleDeconstruction
                Case Feature.SelectCaseTypePattern
                    Return ERRID.FEATURE_SelectCaseTypePattern
                Case Feature.PartialProperty
                    Return ERRID.FEATURE_PartialProperty
                Case Feature.PartialEvent
                    Return ERRID.FEATURE_PartialEvent
                Case Feature.PartialConstructor
                    Return ERRID.FEATURE_PartialConstructor
                Case Feature.PrimaryConstructor
                    Return ERRID.FEATURE_PrimaryConstructor
                Case Feature.CollectionExpression
                    Return ERRID.FEATURE_CollectionExpression
                Case Feature.AsyncUsing
                    Return ERRID.FEATURE_AsyncUsing
                Case Feature.AwaitEach
                    Return ERRID.FEATURE_AwaitEach
                Case Feature.PatternVariables
                    Return ERRID.FEATURE_PatternVariables
                Case Feature.ForEachDeconstruction
                    Return ERRID.FEATURE_ForEachDeconstruction
                Case Feature.RefFields
                    Return ERRID.FEATURE_RefFields
                Case Else
                    Throw ExceptionUtilities.UnexpectedValue(feature)
            End Select
        End Function
    End Module
End Namespace
