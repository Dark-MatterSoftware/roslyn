' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Nullable analysis contracts carried by the standard
    ''' <c>System.Diagnostics.CodeAnalysis</c> attributes.
    ''' Flag values match the C# compiler's internal contract flags.
    ''' They do not change the declared nullable annotation or the CLR type.
    ''' </summary>
    <Flags>
    Friend Enum FlowAnalysisAnnotations
        None = 0
        AllowNull = 1
        DisallowNull = 2
        MaybeNullWhenTrue = 4
        MaybeNullWhenFalse = 8
        MaybeNull = 12
        NotNullWhenTrue = 16
        NotNullWhenFalse = 32
        NotNull = 48
        DoesNotReturnIfFalse = 64
        DoesNotReturnIfTrue = 128
        DoesNotReturn = 192
    End Enum
End Namespace
