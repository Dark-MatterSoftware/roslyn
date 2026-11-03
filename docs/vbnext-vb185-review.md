# VB-185 review: the name of a call whose result is indexed from the end

This note records slice 170a, the investigation of VB-185: what the semantic model returns for `Make()(^3)` against `Make()(2)`, why, and the fixes that the project can choose for slice 170b. It is a note for this independent fork. It is not a Microsoft proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Classification: Tooling/diagnostics, a fix to VB-006. No code change in this slice.

## Reproduction

A compiler test, kept outside the tests, bound this module and class with `vbnext` and compared the bound tree of each statement, the bound nodes that the semantic model records for each name, and `GetSymbolInfo`, `GetTypeInfo`, and `GetOperation` for the name, the call, and the whole access.

```vb
Module A
    Function Make() As List(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function

    Sub Run()
        Dim f = Make()(^3)
        Dim g = Make()(2)
        Make()(^1) = 4
    End Sub
End Module

Class C
    Private items As New List(Of Integer) From {1, 2, 3}

    ReadOnly Property Depth As Integer
        Get
            Return 1
        End Get
    End Property

    Sub Run()
        Dim h = items(^Depth)
        Dim k = items(Depth)
    End Sub
End Class
```

| Syntax | Bound nodes recorded for it | `GetSymbolInfo` | `GetTypeInfo` | `GetOperation` |
| --- | --- | --- | --- | --- |
| `Make` in `Make()(2)` | method group | the method `Make` | none | none |
| `Make` in `Make()(^3)`, and in `Make()(^1) = 4` | method group, `Me` reference (compiler generated) | none, one candidate | the module `A` | none |
| `Make()` in both | call | the method `Make` | `List(Of Integer)` | invocation |
| `Make()(2)` | property access | `Item` | `Integer` | property reference |
| `Make()(^3)` | property access | `Item` | `Integer` | implicit indexer reference |
| `items` in `items(Depth)` | field access | the field `items` | `List(Of Integer)` | |
| `items` in `items(^Depth)` | field access, field access, `Me` reference (compiler generated) | the parameter `Me` | the class `C` | |
| `Depth` in `items(Depth)` | property access | the property `Depth` | `Integer` | |
| `Depth` in `^Depth` | property access, `Me` reference (compiler generated) | the parameter `Me` | the class `C` | |

The call, the whole access, and their operations are right; the difference in the operation of the whole access is the implicit indexer reference that VB-006 reports by design. The names are wrong. VB-185 is wider than a call: a field, a property, or a method that the receiver or the operand of `^n` reaches through the implicit `Me`, or through the module, has the wrong symbol and type.

## Cause

The bound trees agree. `Make()(^3)` binds to a sequence that stores `Make()` in a temporary and reads `Item` of the temporary, with an index offset that keeps the source call and `^3` for operations; the call keeps its method group, and the method group keeps its receiver, a `Me` reference that the binder marks compiler generated, with the syntax of the name `Make`. In `Make()(2)` the call has the same method group and receiver.

The difference is in the map from syntax to bound nodes that `MemberSemanticModel` builds. Its builder, `SemanticModelMapsBuilder`, skips compiler generated nodes. Slice 164a made it record the receiver and the argument that an index offset or a pattern slice keeps, through `RecordConsumedNodes`, which visits them with `_recordingConsumedNodes` set; and `Visit` then passes `allowCompilerGenerated:=True` to `RecordNode` for every node under them, not only for the receiver or the argument itself. `VisitCall` visits the method group, and the method group its receiver, so the `Me` reference is recorded for the name `Make`, after the method group. `GetSymbolInfo` and `GetTypeInfo` read the lower bound node for the name, which is now the `Me` reference: no symbol, the method group as the one candidate, and the type of the module. A field access and a property access visit their implicit `Me` the same way.

`MakeTreeCompilerGenerated` leaves the nodes of the source trees that the stored value shares as the user wrote them, also since slice 164a. So the receiver and the argument that an index offset keeps are not compiler generated unless the binder made them so, and the nodes under them need no exception.

## Fixes for slice 170b

| Option | Change | What else it changes |
| --- | --- | --- |
| A. Record only the consumed root when it is compiler generated | `RecordConsumedNodes` allows a compiler generated node for the receiver or the argument itself, and the nodes under it follow the rule of the rest of the tree. It keeps the check that records each node once. | The names under a receiver or an operand of `^n`, `a..b`, or a range value get the nodes, and so the symbols and types, that they get without the index. A receiver or argument that the binder marked compiler generated is still recorded. The semantic model only; the bound tree, flow analysis, IOperation, and emitted code do not change. |
| B. Drop the exception | `RecordConsumedNodes` records with the rule of the rest of the tree. | As A, and a consumed root that the binder marked compiler generated is no longer recorded, so its syntax may lose the node that the operations of VB-006 and the region analysis of slice 164a read. |
| C. Skip implicit receivers | `RecordNode` refuses a compiler generated `Me`, `MyBase`, `MyClass`, or type expression while it records consumed nodes. | Fixes the names of this review, and leaves the exception for every other compiler generated node under a consumed root, which could map another name the same way. |

The review proposes option A. It is the narrowest change that gives the names under a consumed receiver or argument the same nodes as without the index, and it keeps what slice 164a needed the exception for, the consumed root. Its risk is in the tests that slice 164a added for the semantic model and the region analysis of indexes, slices, and collection literals, and the operation tests of VB-006, which slice 170b reruns.

## Tests for slice 170b

- The semantic model: `GetSymbolInfo` and `GetTypeInfo` of the names in `Make()(^3)`, `Make()(^2) += 5`, `Items(^1)` on a property, `items(^Depth)` on a field with a property operand, and `items(1..Depth)` and `s(1..^Depth)` slices, equal to their forms without `^` or `..`; and the operations of these forms unchanged.
- Rename, find references, go to definition, and quick info of `Make`, `Items`, `items`, and `Depth` from and to those uses.
- Extract method, introduce variable, and introduce parameter on those names, which write code that compiles, or are not offered where they are not offered on `Make()(2)`.
