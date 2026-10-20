' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.PooledObjects

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' The copy constructor of a record class, Sub New(original As T). It calls the base copy constructor,
    ''' or the base constructor without parameters, and copies every instance field of the record.
    ''' Field initializers do not run, so a With copy keeps the values of the original.
    ''' </summary>
    Friend NotInheritable Class SynthesizedRecordCopyConstructorSymbol
        Inherits SynthesizedConstructorBase

        Private ReadOnly _parameters As ImmutableArray(Of ParameterSymbol)
        Private ReadOnly _baseConstructor As MethodSymbol

        Public Sub New(syntaxReference As SyntaxReference, container As NamedTypeSymbol, baseConstructor As MethodSymbol, binder As Binder, diagnostics As BindingDiagnosticBag)
            MyBase.New(syntaxReference, container, isShared:=False, binder, diagnostics)
            _baseConstructor = baseConstructor
            _parameters = ImmutableArray.Create(Of ParameterSymbol)(New SynthesizedParameterSymbol(Me, container, 0, isByRef:=False, "original"))
        End Sub

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return If(ContainingType.IsNotInheritable, Accessibility.Private, Accessibility.Protected)
            End Get
        End Property

        Friend Overrides ReadOnly Property ParameterCount As Integer
            Get
                Return _parameters.Length
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return _parameters
            End Get
        End Property

        Friend Overrides Function GetLexicalSortKey() As LexicalSortKey
            Return New LexicalSortKey(Syntax.SyntaxTree, Syntax.SpanStart + 2, DirectCast(DeclaringCompilation, VisualBasicCompilation))
        End Function

        Friend Overrides ReadOnly Property GenerateDebugInfoImpl As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides Function CalculateLocalSyntaxOffset(localPosition As Integer, localTree As SyntaxTree) As Integer
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetBoundMethodBody(compilationState As TypeCompilationState, diagnostics As BindingDiagnosticBag, <Out> ByRef Optional methodBodyBinder As Binder = Nothing) As BoundBlock
            methodBodyBinder = Nothing
            Dim factory = New SyntheticBoundNodeFactory(Me, Me, Syntax, compilationState, diagnostics)
            Dim original As BoundExpression = factory.Parameter(_parameters(0), isLValue:=False)
            Dim statements = ArrayBuilder(Of BoundStatement).GetInstance()

            If _baseConstructor.ParameterCount = 1 Then
                Dim baseOriginal = New BoundConversion(Syntax, original, ConversionKind.WideningReference, False, False, Nothing,
                                                       _baseConstructor.Parameters(0).Type, Nothing).MakeCompilerGenerated()
                statements.Add(factory.ExpressionStatement(factory.Call(factory.Me(), _baseConstructor, baseOriginal)))
            Else
                statements.Add(factory.ExpressionStatement(factory.Call(factory.Me(), _baseConstructor)))
            End If

            For Each member In ContainingType.GetMembers()
                Dim field = TryCast(member, FieldSymbol)
                If field Is Nothing OrElse field.IsShared OrElse field.IsConst Then
                    Continue For
                End If

                statements.Add(factory.Assignment(factory.Field(factory.Me(), field, isLValue:=True),
                                                  factory.Field(factory.Parameter(_parameters(0), isLValue:=False), field, isLValue:=False)))
            Next

            statements.Add(factory.Return())
            Return factory.Block(statements.ToImmutableAndFree())
        End Function
    End Class
End Namespace
