' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' The constructor synthesized from a primary constructor parameter list.
    ''' </summary>
    Friend NotInheritable Class PrimaryConstructorSymbol
        Inherits SynthesizedConstructorBase

        Private _parameters As ImmutableArray(Of ParameterSymbol)
        Private ReadOnly _debuggable As Boolean
        Private ReadOnly _parameterListPosition As Integer

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
            _parameterListPosition = parameterList.SpanStart
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
            Dim syntax = DirectCast(Me.Syntax, VisualBasicSyntaxNode)
            Dim factory As New SyntheticBoundNodeFactory(Me, Me, syntax, compilationState, diagnostics)
            Dim statements = ArrayBuilder(Of BoundStatement).GetInstance()

            Dim argumentList = GetBaseConstructorArguments()
            If argumentList IsNot Nothing Then
                Dim binder = BinderBuilder.CreateBinderForType(DirectCast(ContainingModule, SourceModuleSymbol), argumentList.SyntaxTree, DirectCast(ContainingType, SourceMemberContainerTypeSymbol))
                binder = New PrimaryConstructorParameterBinder(binder, Parameters)
                Dim baseType = ContainingType.BaseTypeNoUseSiteDiagnostics
                Dim callExpression = binder.BindPrimaryBaseConstructorCall(argumentList, argumentList, baseType, diagnostics)
                statements.Add(New BoundExpressionStatement(argumentList, callExpression))
            End If

            For Each parameter In Parameters
                For Each member In ContainingType.GetMembers(parameter.Name)
                    Dim positional = TryCast(member, SynthesizedRecordPositionalPropertySymbol)
                    If positional Is Nothing OrElse positional.AssociatedField Is Nothing Then
                        Continue For
                    End If

                    statements.Add(factory.Assignment(
                        factory.Field(factory.Me(), positional.AssociatedField, isLValue:=True),
                        factory.Parameter(parameter, isLValue:=False)))
                Next
            Next

            statements.Add(factory.Return())
            Return factory.Block(statements.ToImmutableAndFree())
        End Function

        Private Function GetBaseConstructorArguments() As ArgumentListSyntax
            Dim container = DirectCast(ContainingType, SourceMemberContainerTypeSymbol)
            For Each reference In container.SyntaxReferences
                Dim typeBlock = TryCast(reference.GetSyntax(), TypeBlockSyntax)
                If typeBlock Is Nothing Then
                    Continue For
                End If

                For Each inheritsStatement In typeBlock.[Inherits]
                    If inheritsStatement.ArgumentList IsNot Nothing Then
                        Return inheritsStatement.ArgumentList
                    End If
                Next
            Next

            Return Nothing
        End Function

        Friend Overrides Function GetLexicalSortKey() As LexicalSortKey
            ' The synthesized parameterless structure constructor sorts with the type.
            ' This constructor sorts at the parameter list so both constructors can be ordered.
            Return New LexicalSortKey(m_syntaxReference.SyntaxTree, _parameterListPosition, DirectCast(DeclaringCompilation, VisualBasicCompilation))
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
