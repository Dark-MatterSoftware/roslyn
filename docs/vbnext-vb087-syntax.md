# VB-087 Ref fields

This note locks the form for slices 50b to 50d. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Slice 7g added `ByRef Structure`, a ref-like structure, and phase 45 added `Dim ByRef` locals that refer to a variable. A `ByRef Structure` still cannot hold a reference to one variable, as `Span(Of T)` does internally, because a field cannot be `ByRef`:

```vb
ByRef Structure Cursor
    Private ByRef _value As Integer ' BC30235 since slice 45a
End Structure
```

## Locked form: `ByRef` fields, set with `= ByRef`

```vb
ByRef Structure Cursor
    Private ByRef _value As Integer

    Public Sub New(ByRef value As Integer)
        _value = ByRef value
    End Sub

    Public Sub Increment()
        _value += 1
    End Sub

    Public ReadOnly Property Current As Integer
        Get
            Return _value
        End Get
    End Property
End Structure

Dim total = 0
Dim cursor As New Cursor(total)
cursor.Increment()
' total is 1
```

### Declaring

`ByRef` before a field of a `ByRef Structure` declares a ref field: a field that refers to a variable instead of holding its own value. `ByRef` is the modifier that `ByRef Function`, `ByRef ReadOnly Property`, and `Dim ByRef` already use. A ref field in a class, a module, or a structure that is not `ByRef` is an error, and `Shared ByRef` is an error. Slice 45a's BC30235 still applies there.

### Setting the reference: `field = ByRef reference`

An assignment of the form `field = ByRef reference` makes the field refer to the variable `reference` names. A plain assignment, `field = value`, always writes through to the variable the field already refers to, as an assignment to a `Dim ByRef` local does.

- `= ByRef` may appear only in a `Sub New` of the structure that declares the field, with that field, or `Me.field`, on the left.
- The reference must be a `ByRef` parameter of that `Sub New`, an element of an array, a field of a class, or the result of a `ByRef` function, property, or indexer. A local, a `ByVal` parameter, and a value that is not a variable are errors. Its type must be the field's type, with no conversion.
- Reading, assigning, compound assigning, or passing a ref field `ByRef` before it is set is not checked by the compiler, and fails at run time as reading a null reference does.

### `ByRef ReadOnly`

`Private ByRef ReadOnly _value As Integer` refers to a variable without allowing writes through it, as `Dim ByRef ReadOnly` does. Its `= ByRef` source may also be a `ReadOnly` field or a `ref readonly` result. Assigning or compound assigning it is an error, and passing it `ByRef` passes a copy.

### Lifetime

A structure holds a ref field when it declares one, or when it has a field of a structure that holds one. Such a structure must not outlive the variables its ref fields refer to, so:

- it cannot be the return type of a `Function`, a property, or a `ByRef Function`;
- it cannot be assigned to a `ByRef` parameter, or to a field of a structure that is reached through a `ByRef` parameter or `Me`;
- the rules for every `ByRef Structure` from slice 7g still apply, so it cannot be boxed, stored in a class, captured by a lambda, or kept across an `Await` or `Yield`.

Within those rules a ref field's source outlives the structure: a `ByRef` parameter of `Sub New` belongs to the caller that creates the structure and keeps it as a local, and an array element, a class field, or a `ByRef` result lives on the heap or in a variable that the caller already holds.

### Runtime

Ref fields need a runtime that supports them, which .NET 7 and later do. On another runtime, declaring a ref field is an error.

Visual Basic 17.13 does not have the form.

### Sources

C# 11 added `ref` and `ref readonly` fields in `ref struct` types, set with `field = ref value`, with escape analysis and `scoped` parameters to keep them safe. This note takes the C# spelling of the assignment in Visual Basic form and uses fixed rules in place of escape analysis.

## Rejected forms

- `Private _value As ByRef Integer`, with `ByRef` on the type. `ByRef` is not part of a type anywhere else in the language.
- Setting the reference by a plain assignment in `Sub New`. The same statement would mean two different things inside and outside `Sub New`.
- C#'s escape analysis with `scoped` parameters. It needs a new parameter modifier and a flow analysis, and the fixed rules cover a ref structure that its creator keeps as a local.

## Not locked

Setting a ref field outside `Sub New`, returning a structure that holds a ref field, `scoped` parameters, and ref fields that refer to other ref structures are not covered by this note.
