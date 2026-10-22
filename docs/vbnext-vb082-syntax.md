# VB-082 ByRef locals

This note locks the form for slices 45c to 45e. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Slices 7e and 7f added `ByRef Function` and `ByRef ReadOnly Property`, which return a reference, and slice 44a lets code assign through one even when its receiver is a value. A reference that several statements use still needs a call per use:

```vb
Dim exists As Boolean
CollectionsMarshal.GetValueRefOrAddDefault(counts, word, exists) += 1
If CollectionsMarshal.GetValueRefOrAddDefault(counts, word, exists) > limit Then
    CollectionsMarshal.GetValueRefOrAddDefault(counts, word, exists) = limit
End If
```

Before slice 45a, `Dim ByRef slot As Integer = ...` compiled on `vbnext` and silently declared a copy. Slice 45a made it an error until this form is implemented.

## Locked form: `Dim ByRef name [As type] = reference`

```vb
Dim ByRef slot As Integer = CollectionsMarshal.GetValueRefOrAddDefault(counts, word, exists)
slot += 1
If slot > limit Then slot = limit

Dim ByRef first = values(0)
first = 9

Dim ByRef ReadOnly letter = text.AsSpan()(0)
Console.WriteLine(letter)
```

`ByRef` before `Dim` declares a local that refers to a variable instead of holding its own value. Reading the local reads that variable, and assigning, compound assigning, or passing the local `ByRef` writes to it. The spelling is the modifier that `ByRef Function` and `ByRef ReadOnly Property` already use, and the `vbnext` parser already accepts it.

### The initializer is required and must be a reference

A `ByRef` local must have an initializer, and the initializer must be a variable or a reference:

- a local, a parameter, a field, or an array element;
- the result of a `ByRef Function`, a `ByRef` property, or a `ByRef` indexer, from source or metadata, such as `CollectionsMarshal.GetValueRefOrAddDefault`, `CollectionsMarshal.AsSpan(list)(i)`, or `span(i)` on a `Span(Of T)`.

A value that is not a variable, such as a constant, an arithmetic result, or the result of an ordinary `Function`, is an error. A property with a setter is not a reference, so it is an error too.

The type comes from the `As` clause, which must be the type of the referenced variable, or under `Option Infer On` from the initializer. There is no conversion: a reference to an `Integer` cannot be held as a `ByRef` local of type `Long`.

### Assignment always writes through

After the declaration, assigning the local writes to the referenced variable. The local cannot be made to refer to another variable. A form for that, like C# 7.3 ref reassignment, is not adopted.

### `ByRef ReadOnly`

`Dim ByRef ReadOnly name = reference` refers to a variable without allowing writes through it. Its initializer may also be a `ReadOnly` field, another `ByRef ReadOnly` local, or a `ref readonly` result from phase 35, such as an element of a `ReadOnlySpan(Of T)`. Assigning or compound assigning such a local is an error. Passing it `ByRef` passes a copy, as passing a `ReadOnly` field `ByRef` does, so the callee cannot write through it. A plain `Dim ByRef` local initialized from any of these is an error, because it would allow writes.

### Lambdas, `Async` methods, and iterators

A lambda cannot capture a `ByRef` local. In an `Async` or `Iterator` method or lambda, a `ByRef` local follows the phase 37 rule for span locals: it is an error only where its reference is live across an `Await` or `Yield`, and BC37405 reports it there.

### Where it may appear

`ByRef` applies to `Dim` locals only. `Static ByRef`, `Const ByRef`, and `ByRef` on a field keep the errors from slice 45a. One declaration may declare several `ByRef` locals, each with its own initializer.

Visual Basic 17.13 does not have the form, and its parser rejects `Dim ByRef`.

### Sources

C# 7 has `ref` locals, `ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(...)`, and C# 7.2 has `ref readonly` locals. C# marks the initializer with `ref` too. Visual Basic does not mark an argument passed `ByRef` at the call site, so the initializer is not marked here either.

## Rejected forms

- `Dim slot As ByRef Integer = ...`, with `ByRef` on the type. `ByRef` is not part of a type anywhere else in the language.
- `ByRef slot = ...`, with `ByRef` in place of `Dim`. It would read like a parameter list and break the rule that a local declaration starts with `Dim`, `Static`, or `Const`.
- An initializer that may be only a `ByRef Function`, property, or indexer result. Referring to an array element or a field is as common.
- Reassigning the reference. It needs a second assignment form, and C# needed one too.

## Not locked

`ByRef` loop control variables beyond the array form of slice 17, `ByRef` locals in `Using` and `With` statements, and returning a `ByRef` local from a `ByRef Function` are not covered by this note.
