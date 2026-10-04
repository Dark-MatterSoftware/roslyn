' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Binds the operand of an <c>Unchecked(expression)</c> operator without integer overflow checks.
    ''' </summary>
    Friend NotInheritable Class UncheckedBinder
        Inherits Binder

        Public Sub New(containingBinder As Binder)
            MyBase.New(containingBinder)
        End Sub

        Public Overrides ReadOnly Property CheckOverflow As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property IsInUncheckedExpression As Boolean
            Get
                Return True
            End Get
        End Property

    End Class

End Namespace
