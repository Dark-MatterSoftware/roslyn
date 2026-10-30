' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
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
    ''' On vbnext, the parameters of a method, a property, or a delegate are in scope for NameOf in the attributes
    ''' of the member, of its parameters, and of its return value, as C# 11 puts them in scope for nameof. Outside
    ''' NameOf they are not.
    ''' </summary>
    Friend NotInheritable Class AttributeParametersBinder
        Inherits Binder

        Private ReadOnly _member As Symbol
        Private ReadOnly _attributedParameterOpt As ParameterSymbol

        <ThreadStatic>
        Private Shared s_propertiesBuildingParameters As HashSet(Of Symbol)

        ''' <summary>
        ''' The member is a method or a property, and the attributed parameter is the parameter that carries the
        ''' attribute, if any. The member's parameters are read only for a lookup inside NameOf.
        ''' </summary>
        Public Sub New(containingBinder As Binder, member As Symbol, Optional attributedParameterOpt As ParameterSymbol = Nothing)
            MyBase.New(containingBinder)
            Debug.Assert(member.Kind = SymbolKind.Method OrElse member.Kind = SymbolKind.Property)
            _member = member
            _attributedParameterOpt = attributedParameterOpt
        End Sub

        ''' <summary>
        ''' A default property checks its parameters while it builds them, which binds their attributes. A lookup
        ''' made during that build finds only the attributed parameter.
        ''' </summary>
        Private ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                If _member.Kind = SymbolKind.Method Then
                    Return DirectCast(_member, MethodSymbol).Parameters
                End If

                If s_propertiesBuildingParameters Is Nothing Then
                    s_propertiesBuildingParameters = New HashSet(Of Symbol)()
                End If

                If Not s_propertiesBuildingParameters.Add(_member) Then
                    Return If(_attributedParameterOpt Is Nothing, ImmutableArray(Of ParameterSymbol).Empty, ImmutableArray.Create(_attributedParameterOpt))
                End If

                Try
                    Return DirectCast(_member, PropertySymbol).Parameters
                Finally
                    s_propertiesBuildingParameters.Remove(_member)
                End Try
            End Get
        End Property

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

            For Each parameter In Parameters
                If IdentifierComparison.Equals(parameter.Name, name) Then
                    lookupResult.SetFrom(CheckViability(parameter, arity, options, Nothing, useSiteInfo))
                    Return
                End If
            Next
        End Sub

        Friend Overrides Sub AddLookupSymbolsInfoInSingleBinder(nameSet As LookupSymbolsInfo,
                                                                options As LookupOptions,
                                                                originalBinder As Binder)
            If (options And (LookupOptions.NamespacesOrTypesOnly Or LookupOptions.LabelsOnly)) <> 0 OrElse Not IsInNameOf(originalBinder) Then
                Return
            End If

            For Each parameter In Parameters
                If originalBinder.CanAddLookupSymbolInfo(parameter, options, nameSet, Nothing) Then
                    nameSet.AddSymbol(parameter, parameter.Name, 0)
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
