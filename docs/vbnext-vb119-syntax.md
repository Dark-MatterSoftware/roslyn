# VB-119 Type parameters that take ref structures

This note locks the form for slice 86b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

A VB type parameter cannot take a ref structure, and VB has no form for C# 13's `allows ref struct`, so generic VB code cannot take spans or `ByRef Structure` values:

```vb
Function Echo(Of T)(value As T) As T
    Return value
End Function

Dim span As Span(Of Integer) = New Integer() {1, 2}
Echo(span) ' BC37376 today: T does not allow a ByRef structure
```

## Locked form: `ByRef` before the type parameter

```vb
Function Echo(Of ByRef T)(value As T) As T
    Return value
End Function

Function Length(Of ByRef T As IHasLength)(value As T) As Integer
    Return value.Length
End Function

Structure Holder(Of ByRef T)
End Structure
```

- `ByRef` before a type parameter's name says that its type arguments may be ref structures, as `ByRef Structure` declares one. It reads "T may be a ByRef structure"; it does not require one, so `Echo(5)` and `Echo("text")` still apply.
- It goes in any type parameter list: methods, classes, structures, interfaces, and delegates, with or without an `As` clause.
- It combines with an interface constraint, which a ref structure meets by implementing the interface, as phase 85 checks for C# type parameters, and with `Structure` and `New`. It does not combine with `Class` or a class constraint, which no ref structure meets.
- Inside the generic code, a value of type `T` follows the rules for ref structures: it is not converted to `Object` or to an interface, it is not an array element type or a field of a class, and it is not captured by a lambda or kept across an `Await` or `Yield`. Interface members are called on it constrained to `T`, without boxing.
- A type argument of a type parameter that takes ref structures may be a ref structure, which a type parameter without `ByRef` still reports with BC37376.

A type parameter declared this way is emitted as C# 13 emits `allows ref struct`, with the `AllowByRefLike` generic parameter attribute, and C# reads it as `allows ref struct`.

Visual Basic 17.13 still reports a syntax error for `ByRef` in a type parameter list.

## Rejected forms

- A keyword in the constraint list, as in `(Of T As {IDisposable, ByRef})`. VB's constraint list says what a type argument must be, and this says what it may be.
- `(Of T As ByRef Structure)`. It reads as a constraint that the type argument must be a ref structure, as `As Structure` requires a structure.

## Not locked

Combining `ByRef` with the `In` and `Out` variance modifiers is not covered by this note.
