' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic

    Friend NotInheritable Class AsyncStateMachine
        Inherits StateMachineTypeSymbol

        Private ReadOnly _typeKind As TypeKind
        Private ReadOnly _constructor As SynthesizedSimpleConstructorSymbol

        Protected Friend Sub New(slotAllocatorOpt As VariableSlotAllocator, compilationState As TypeCompilationState, asyncMethod As MethodSymbol, asyncMethodOrdinal As Integer, typeKind As TypeKind)
            Me.New(slotAllocatorOpt, compilationState, asyncMethod, asyncMethodOrdinal, typeKind, asyncIteratorElementType:=Nothing, isEnumerable:=False)
        End Sub

        ''' <summary>
        ''' An async iterator's state machine is a class that implements the async enumerator interfaces and
        ''' IValueTaskSource, and its constructor takes the initial state.
        ''' </summary>
        Protected Friend Sub New(slotAllocatorOpt As VariableSlotAllocator,
                                 compilationState As TypeCompilationState,
                                 asyncMethod As MethodSymbol,
                                 asyncMethodOrdinal As Integer,
                                 typeKind As TypeKind,
                                 asyncIteratorElementType As TypeSymbol,
                                 isEnumerable As Boolean)
            MyBase.New(slotAllocatorOpt,
                       compilationState,
                       asyncMethod,
                       asyncMethodOrdinal,
                       asyncMethod.ContainingAssembly.GetSpecialType(If(typeKind = TypeKind.Struct, SpecialType.System_ValueType, SpecialType.System_Object)),
                       GetInterfaces(asyncMethod.DeclaringCompilation, asyncIteratorElementType, isEnumerable))

            Me._constructor = New SynthesizedSimpleConstructorSymbol(Me)
            If asyncIteratorElementType Is Nothing Then
                Me._constructor.SetParameters(ImmutableArray(Of ParameterSymbol).Empty)
            Else
                Debug.Assert(typeKind = TypeKind.Class)
                Dim intType = DeclaringCompilation.GetSpecialType(SpecialType.System_Int32)
                Me._constructor.SetParameters(ImmutableArray.Create(Of ParameterSymbol)(
                    New SynthesizedParameterSymbol(Me._constructor, intType, 0, False, GeneratedNames.MakeStateMachineStateFieldName())))
            End If

            Me._typeKind = typeKind
        End Sub

        Private Shared Function GetInterfaces(compilation As VisualBasicCompilation, asyncIteratorElementType As TypeSymbol, isEnumerable As Boolean) As ImmutableArray(Of NamedTypeSymbol)
            Dim asyncStateMachine = compilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_IAsyncStateMachine)
            If asyncIteratorElementType Is Nothing Then
                Return ImmutableArray.Create(asyncStateMachine)
            End If

            Dim interfaces = ArrayBuilder(Of NamedTypeSymbol).GetInstance()
            interfaces.Add(asyncStateMachine)
            If isEnumerable Then
                interfaces.Add(compilation.GetWellKnownType(WellKnownType.System_Collections_Generic_IAsyncEnumerable_T).Construct(asyncIteratorElementType))
            End If

            interfaces.Add(compilation.GetWellKnownType(WellKnownType.System_Collections_Generic_IAsyncEnumerator_T).Construct(asyncIteratorElementType))
            interfaces.Add(compilation.GetWellKnownType(WellKnownType.System_IAsyncDisposable))
            interfaces.Add(compilation.GetWellKnownType(WellKnownType.System_Threading_Tasks_Sources_IValueTaskSource_T).Construct(compilation.GetSpecialType(SpecialType.System_Boolean)))
            interfaces.Add(compilation.GetWellKnownType(WellKnownType.System_Threading_Tasks_Sources_IValueTaskSource))
            Return interfaces.ToImmutableAndFree()
        End Function

        Public Overrides ReadOnly Property TypeKind As TypeKind
            Get
                Return Me._typeKind
            End Get
        End Property

        Protected Friend Overrides ReadOnly Property Constructor As MethodSymbol
            Get
                Return Me._constructor
            End Get
        End Property
    End Class
End Namespace
