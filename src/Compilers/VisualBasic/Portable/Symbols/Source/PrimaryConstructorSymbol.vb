' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' The constructor synthesized from a primary constructor parameter list.
    ''' </summary>
    Friend NotInheritable Class PrimaryConstructorSymbol
        Inherits SynthesizedConstructorBase

        Private _parameters As ImmutableArray(Of ParameterSymbol)
        Private ReadOnly _debuggable As Boolean

        Friend Sub New(
            syntaxReference As SyntaxReference,
            container As NamedTypeSymbol,
            isDebuggable As Boolean,
            binder As Binder,
            diagnostics As BindingDiagnosticBag,
            parameterList As ParameterListSyntax
        )
            MyBase.New(syntaxReference, container, isShared:=False, binder, diagnostics)
            _debuggable = isDebuggable
            _parameters = ImmutableArray(Of ParameterSymbol).Empty

            Dim signatureBinder = New LocationSpecificBinder(BindingLocation.MethodSignature, Me, binder)
            _parameters = signatureBinder.DecodeParameterList(Me, False, SourceMemberFlags.None, parameterList, diagnostics)
        End Sub

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return _parameters
            End Get
        End Property

        Friend Overrides Function GetBoundMethodBody(compilationState As TypeCompilationState, diagnostics As BindingDiagnosticBag, Optional ByRef methodBodyBinder As Binder = Nothing) As BoundBlock
            methodBodyBinder = Nothing
            Dim returnStmt = New BoundReturnStatement(Me.Syntax, Nothing, Nothing, Nothing)
            returnStmt.SetWasCompilerGenerated()
            Return New BoundBlock(Me.Syntax, Nothing, ImmutableArray(Of LocalSymbol).Empty, ImmutableArray.Create(Of BoundStatement)(returnStmt))
        End Function

        Friend Overrides ReadOnly Property GenerateDebugInfoImpl As Boolean
            Get
                Return _debuggable
            End Get
        End Property

        Friend Overrides Function CalculateLocalSyntaxOffset(localPosition As Integer, localTree As SyntaxTree) As Integer
            Dim containingType = DirectCast(Me.ContainingType, SourceMemberContainerTypeSymbol)
            Return containingType.CalculateSyntaxOffsetInSynthesizedConstructor(localPosition, localTree, IsShared)
        End Function
    End Class
End Namespace
