' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Binds the argument of a NameOf, so that a binder outside it can tell that a lookup is for NameOf.
    ''' </summary>
    Friend NotInheritable Class NameOfArgumentBinder
        Inherits Binder

        Public Sub New(containingBinder As Binder)
            MyBase.New(containingBinder)
        End Sub
    End Class

    ''' <summary>
    ''' On vbnext, the parameters of a method are in scope for NameOf in the attributes of the method, of its
    ''' parameters, and of its return value, as C# 11 puts them in scope for nameof. Outside NameOf they are not.
    ''' </summary>
    Friend NotInheritable Class AttributeParametersBinder
        Inherits Binder

        Private ReadOnly _method As MethodSymbol

        Public Sub New(containingBinder As Binder, method As MethodSymbol)
            MyBase.New(containingBinder)
            _method = method
        End Sub

        Friend Overrides Sub LookupInSingleBinder(lookupResult As LookupResult,
                                                  name As String,
                                                  arity As Integer,
                                                  options As LookupOptions,
                                                  originalBinder As Binder,
                                                  <[In], Out> ByRef useSiteInfo As CompoundUseSiteInfo(Of AssemblySymbol))
            Debug.Assert(lookupResult.IsClear)

            If arity <> 0 OrElse Not IsInNameOf(originalBinder) Then
                Return
            End If

            For Each parameter In _method.Parameters
                If IdentifierComparison.Equals(parameter.Name, name) Then
                    lookupResult.SetFrom(CheckViability(parameter, arity, options, Nothing, useSiteInfo))
                    Return
                End If
            Next
        End Sub

        Private Function IsInNameOf(originalBinder As Binder) As Boolean
            Dim current = originalBinder
            While current IsNot Nothing AndAlso current IsNot Me
                If TypeOf current Is NameOfArgumentBinder Then
                    Return True
                End If

                current = current.ContainingBinder
            End While

            Return False
        End Function
    End Class

End Namespace
