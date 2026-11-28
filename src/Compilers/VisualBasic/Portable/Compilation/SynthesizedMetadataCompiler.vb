' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.VisualBasic.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' When compiling in metadata-only mode, <see cref="MethodCompiler"/> is not run. This is problematic because 
    ''' <see cref="MethodCompiler"/> adds synthesized explicit implementations to the list of synthesized definitions. 
    ''' In lieu of running <see cref="MethodCompiler"/>, this class performs a quick 
    ''' traversal of the symbol table and performs processing of synthesized symbols if necessary.
    ''' </summary>
    Friend Class SynthesizedMetadataCompiler
        Inherits VisualBasicSymbolVisitor

        Private ReadOnly _moduleBeingBuilt As PEModuleBuilder
        Private ReadOnly _diagnostics As BindingDiagnosticBag
        Private ReadOnly _cancellationToken As CancellationToken

        Private Sub New(moduleBeingBuilt As PEModuleBuilder, diagnostics As BindingDiagnosticBag, cancellationToken As CancellationToken)
            Me._moduleBeingBuilt = moduleBeingBuilt
            Me._diagnostics = diagnostics
            Me._cancellationToken = cancellationToken
        End Sub

        ''' <summary>
        ''' Traverse the symbol table and properly add/process synthesized extra metadata if needed.
        ''' </summary>
        Friend Shared Sub ProcessSynthesizedMembers(compilation As VisualBasicCompilation,
                                                        moduleBeingBuilt As PEModuleBuilder,
                                                        diagnostics As BindingDiagnosticBag,
                                                        Optional cancellationToken As CancellationToken = Nothing)

            Debug.Assert(moduleBeingBuilt IsNot Nothing)
            Dim compiler = New SynthesizedMetadataCompiler(moduleBeingBuilt:=moduleBeingBuilt, diagnostics:=diagnostics, cancellationToken:=cancellationToken)
            compilation.SourceModule.GlobalNamespace.Accept(compiler)
        End Sub

        Public Overrides Sub VisitNamespace(symbol As NamespaceSymbol)
            Me._cancellationToken.ThrowIfCancellationRequested()
            For Each member In symbol.GetMembers()
                member.Accept(Me)
            Next
        End Sub

        Public Overrides Sub VisitNamedType(symbol As NamedTypeSymbol)
            Me._cancellationToken.ThrowIfCancellationRequested()

            ' Metadata-only emit does not compile method bodies, which is where the embedded
            ' marker attribute is otherwise created. Create it before metadata indexing so the
            ' constructor referenced by ExtensionMarkerAttribute is part of the image.
            For Each member In symbol.GetMembers()
                Dim propertySymbol = TryCast(member, PropertySymbol)
                If propertySymbol IsNot Nothing AndAlso propertySymbol.IsExtensionProperty Then
                    PEModuleBuilder.ReportRequiredExtensionMarkerContract(_moduleBeingBuilt.Compilation, propertySymbol, _diagnostics)
                    _moduleBeingBuilt.EnsureExtensionMarkerAttribute()
                    Exit For
                End If
            Next

            For Each member In symbol.GetMembers()
                Select Case member.Kind
                    Case SymbolKind.NamedType
                        member.Accept(Me)
                End Select
            Next
        End Sub

#If DEBUG Then
        Public Overrides Sub VisitProperty(symbol As PropertySymbol)
            Throw ExceptionUtilities.Unreachable
        End Sub

        Public Overrides Sub VisitMethod(symbol As MethodSymbol)
            Throw ExceptionUtilities.Unreachable
        End Sub
#End If

    End Class
End Namespace
