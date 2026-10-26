# VB-119 Type parameters that take ref structures

This note records the form for VB-119. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

## The form is slice 7m's `ByRef` constraint

Slice 7m already declares type parameters that take ref structures, as C# 13's `allows ref struct` does, with `ByRef` in the constraint:

```vb
Function Echo(Of T As ByRef)(value As T) As T
    Return value
End Function

Sub Close(Of T As {IDisposable, ByRef})(value As T)
    value.Dispose()
End Sub

Dim span As Span(Of Integer) = New Integer() {1, 2}
Echo(span)                ' a ref structure
Echo(5)                   ' and any other type
Close(New Wrapper())      ' a ByRef Structure that implements IDisposable, as phase 85 checks
```

- `ByRef` in a type parameter's constraint lets its type arguments be ref structures. It does not require one.
- It combines with the other constraints in a `{...}` list. With phase 85, a ref structure meets an interface constraint by implementing the interface.
- Inside the generic code, a value of type `T` cannot be boxed or stored as a field of a class, as for other ref structures.
- It is emitted with the `AllowByRefLike` generic parameter attribute, and C# reads it as `allows ref struct`.
- Visual Basic 17.13 still rejects `ByRef` in the constraint.

The wave 14 probe that listed VB-119 as a gap declared `Function Echo(Of T)` without the constraint, so it reported BC37376 as designed.

## Withdrawn form

Slice 86a first locked `ByRef` before the type parameter's name, as in `(Of ByRef T)`, before slice 7m's form was found. It was withdrawn so that VB has one spelling, and no code for it was committed.
