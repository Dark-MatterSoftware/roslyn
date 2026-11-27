' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Runtime.CompilerServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Extensions

    Friend Module INamespaceOrTypeSymbolExtensions
        <Extension>
        Public Function GenerateTypeSyntax(symbol As INamespaceOrTypeSymbol, Optional addGlobal As Boolean = True, Optional compilation As Compilation = Nothing) As TypeSyntax
            Dim effective = If(compilation, SourceCompilationOf(symbol))
            Return symbol.Accept(TypeSyntaxGeneratorVisitor.Create(addGlobal, effective)).WithAdditionalAnnotations(Simplifier.Annotation)
        End Function

        Friend Function SourceCompilationOf(symbol As ISymbol) As Compilation
            Dim current = symbol
            While current IsNot Nothing
                Dim source = TryCast(current.ContainingAssembly, ISourceAssemblySymbol)
                If source IsNot Nothing AndAlso source.Compilation IsNot Nothing Then
                    Return source.Compilation
                End If

                current = current.ContainingSymbol
            End While

            Return Nothing
        End Function

        ''' <summary>
        ''' Compilation that decides whether generated signature syntax may include a nullable reference marker.
        ''' A code-generation symbol is often constructed without a containing type, so the original source
        ''' method, property, event, or parameter is the compilation that still owns the annotation.
        ''' </summary>
        Friend Function SignatureCompilationOf(symbol As ISymbol) As Compilation
            Dim compilation = SourceCompilationOf(symbol)
            If compilation IsNot Nothing Then
                Return compilation
            End If

            Dim method = TryCast(symbol, IMethodSymbol)
            If method IsNot Nothing Then
                Return FirstSignatureCompilation(method.ExplicitInterfaceImplementations, method.Parameters, method.ReturnType)
            End If

            Dim propertySymbol = TryCast(symbol, IPropertySymbol)
            If propertySymbol IsNot Nothing Then
                Return FirstSignatureCompilation(propertySymbol.ExplicitInterfaceImplementations, propertySymbol.Parameters, propertySymbol.Type)
            End If

            Dim eventSymbol = TryCast(symbol, IEventSymbol)
            If eventSymbol IsNot Nothing Then
                Return FirstSignatureCompilation(eventSymbol.ExplicitInterfaceImplementations, ImmutableArray(Of IParameterSymbol).Empty, eventSymbol.Type)
            End If

            Dim field = TryCast(symbol, IFieldSymbol)
            If field IsNot Nothing Then
                Return SourceCompilationOf(field.Type)
            End If

            Return Nothing
        End Function

        Private Function FirstSignatureCompilation(Of TImplementation As ISymbol)(implementations As ImmutableArray(Of TImplementation), parameters As ImmutableArray(Of IParameterSymbol), type As ITypeSymbol) As Compilation
            Dim compilation = FirstSourceCompilation(implementations)
            If compilation IsNot Nothing Then
                Return compilation
            End If

            compilation = FirstSourceCompilation(parameters)
            If compilation IsNot Nothing Then
                Return compilation
            End If

            Return SourceCompilationOf(type)
        End Function

        Private Function FirstSourceCompilation(Of TSymbol As ISymbol)(symbols As ImmutableArray(Of TSymbol)) As Compilation
            If symbols.IsDefaultOrEmpty Then
                Return Nothing
            End If

            For Each symbol In symbols
                Dim compilation = SourceCompilationOf(symbol)
                If compilation IsNot Nothing Then
                    Return compilation
                End If
            Next

            Return Nothing
        End Function

        <Extension>
        Public Function GetAliasForSymbol(symbol As INamespaceOrTypeSymbol, node As SyntaxNode, semanticModel As SemanticModel) As IAliasSymbol
            ' NOTE(cyrusn): If we're in an imports clause, we can't use aliases.
            Dim clause = node.AncestorsAndSelf().OfType(Of ImportsClauseSyntax).FirstOrDefault()
            If clause IsNot Nothing Then
                Return Nothing
            End If

            Dim originalSemanticModel = semanticModel.GetOriginalSemanticModel()
            If Not originalSemanticModel.SyntaxTree.HasCompilationUnitRoot Then
                Return Nothing
            End If

            Dim aliasSymbol As IAliasSymbol = Nothing
            If Not AliasSymbolCache.TryGetAliasSymbol(originalSemanticModel, 0, symbol, aliasSymbol) Then
                ' build cache first
                AliasSymbolCache.AddAliasSymbols(originalSemanticModel, 0, originalSemanticModel.GetAliasSymbols())

                ' retry
                AliasSymbolCache.TryGetAliasSymbol(originalSemanticModel, 0, symbol, aliasSymbol)
            End If

            Return aliasSymbol
        End Function
    End Module

End Namespace
