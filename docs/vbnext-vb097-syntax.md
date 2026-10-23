# VB-097 `ByRef ReadOnly` functions

This note locks the form for slice 61b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

On `vbnext` a `ByRef Function` returns a reference that callers may write through, and slice 35a reads `ref readonly` results from other assemblies. A VB function cannot return a reference that callers may only read:

```vb
Private ReadOnly _values As Integer() = {1, 2}

ByRef ReadOnly Function FirstValue() As Integer ' BC30242 today
    Return _values(0)
End Function
```

## Locked form: `ByRef ReadOnly Function`

`ByRef ReadOnly` before `Function` declares a function that returns a read-only reference, which C# 7.2 calls `ref readonly`. It is the pair of `ByRef` and `ReadOnly` that `Dim ByRef ReadOnly` locals (slice 45d) and `ByRef ReadOnly` fields (slice 50c) already use.

- `Return` names the variable the function refers to, under the rules for a `ByRef Function`. It may also name a variable that cannot be written: a `ReadOnly` field, a `Dim ByRef ReadOnly` local, a `ByRef ReadOnly` field, or a `ref readonly` result.
- A caller reads the result as slice 35a reads a `ref readonly` result from another assembly. It may refer to it with `Dim ByRef ReadOnly`, but assigning it, compound assigning it, or referring to it with `Dim ByRef` is an error, and passing it `ByRef` passes a copy.
- A `ByRef ReadOnly Function` overrides or implements only a member that returns `ref readonly`, and a `ByRef Function` only one that returns `ref`.
- The function is emitted as C# emits `ref readonly` returns: a by-reference return marked `IsReadOnlyAttribute`, with the `InAttribute` required modifier on interface and virtual members.

The modifiers may come in either order, as for any VB modifiers.

### Properties

`ByRef ReadOnly Property` keeps its meaning: a getter-only property that returns `ByRef`. This note does not declare `ref readonly` properties.

Visual Basic 17.13 has neither form.

## Rejected forms

- `Function FirstValue() As ByRef ReadOnly Integer`, with the reference kind on the return type. `ByRef` is not part of a type anywhere else in the language.
- Changing `ByRef ReadOnly Property` to mean a `ref readonly` property. It would change the meaning of code that compiles today.
- `ByRef ReadOnly ReadOnly Property`, with `ReadOnly` twice. It reads as a typing mistake.

## Not locked

`ref readonly` properties and indexers, and `ByRef ReadOnly` lambdas and delegates, are not covered by this note.
