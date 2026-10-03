' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Puts primary constructor parameters in scope for instance initializers.
    ''' </summary>
    Friend NotInheritable Class PrimaryConstructorParameterBinder
        Inherits Binder

        Private ReadOnly _parameterMap As Dictionary(Of String, Symbol)

        Public Sub New(containingBinder As Binder, parameters As ImmutableArray(Of ParameterSymbol))
            MyBase.New(containingBinder)

            _parameterMap = New Dictionary(Of String, Symbol)(parameters.Length, IdentifierComparison.Comparer)
            For Each parameterSymbol In parameters
                If Not _parameterMap.ContainsKey(parameterSymbol.Name) Then
                    _parameterMap(parameterSymbol.Name) = parameterSymbol
                End If
            Next
        End Sub

        Friend Overrides Sub LookupInSingleBinder(lookupResult As LookupResult,
                                                   name As String,
                                                   arity As Integer,
                                                   options As LookupOptions,
                                                   originalBinder As Binder,
                                                   <[In], Out> ByRef useSiteInfo As CompoundUseSiteInfo(Of AssemblySymbol))
            Debug.Assert(lookupResult.IsClear)

            If (options And (LookupOptions.NamespacesOrTypesOnly Or LookupOptions.LabelsOnly Or LookupOptions.MustNotBeLocalOrParameter)) = 0 Then
                Dim parameterSymbol As Symbol = Nothing
                If _parameterMap.TryGetValue(name, parameterSymbol) Then
                    lookupResult.SetFrom(CheckViability(parameterSymbol, arity, options, Nothing, useSiteInfo))
                End If
            End If
        End Sub

        Friend Overrides Sub AddLookupSymbolsInfoInSingleBinder(nameSet As LookupSymbolsInfo,
                                                                 options As LookupOptions,
                                                                 originalBinder As Binder)
            If (options And (LookupOptions.NamespacesOrTypesOnly Or LookupOptions.LabelsOnly)) = 0 Then
                For Each param In _parameterMap.Values
                    If originalBinder.CanAddLookupSymbolInfo(param, options, nameSet, Nothing) Then
                        nameSet.AddSymbol(param, param.Name, 0)
                    End If
                Next
            End If
        End Sub

        Public Overrides ReadOnly Property ContainingMember As Symbol
            Get
                Return m_containingBinder.ContainingMember
            End Get
        End Property

        Public Overrides ReadOnly Property AdditionalContainingMembers As ImmutableArray(Of Symbol)
            Get
                Return m_containingBinder.AdditionalContainingMembers
            End Get
        End Property
    End Class

End Namespace
