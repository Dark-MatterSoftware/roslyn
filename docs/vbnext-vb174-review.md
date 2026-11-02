# VB-174 Review of adopted symbolic spellings

This note reviews four spellings for VB-174, as [vbnext-mission.md](vbnext-mission.md#adopted-spellings-under-review) asks. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Classification: design review; no code change. Each spelling below was adopted before the mission preferred keyword forms, and each is implemented on `vbnext`. The review records, for each, whether it stays, gains a keyword alternative, or changes. It does not choose a new spelling: a keyword alternative or a change would be its own proposal, in the form the mission sets out, decided before any implementation.

The cost of a change is limited to this fork. Nothing is released, and `vbnext` is opt-in, so no published code depends on these spellings; the tests, the samples, and the notes that use them would change with them.

## `^n`, an index from the end (VB-006)

```vb
Dim last = items(^1)
Dim fromEnd As Index = ^n
```

- Adopted in slices 4a and 7i for indexes, and in slice 140a as a value of type `System.Index`, a [compiler contract](vbnext-mission.md#language-and-framework-api-boundary).
- For keeping it: it is short, it is what .NET documentation and C# code write, and it is used only inside an index or as an `Index` value, so it rarely appears.
- Against it: `^` is exponentiation in Visual Basic. The unary form reads as a power with no base, and it is the least VB-like of the four.
- Alternatives a proposal could weigh: a keyword before the offset, or `Index.FromEnd(1)`, which works today without new syntax.

## `a..b`, a range (VB-006)

```vb
Dim middle = items(1..4)
Dim span As Range = ..n
```

- Adopted in slices 4b and 7i for slices, and in slice 140a as a value of type `System.Range`, also a compiler contract.
- For keeping it: the end is exclusive, as `System.Range` defines it, and `..` matches what .NET documentation writes.
- Against it: it is a symbol where VB writes ranges with `To`, in `For i = 1 To 4`, `Case 1 To 4`, and now `Case Is (1 To 9, "draft")`.
- The important difference: `To` is inclusive everywhere in VB, and a `Range` excludes its end. `items(1 To 3)` that excluded index 3 would contradict every other `To` in the language, and one that included it would contradict `System.Range`. A proposal for a keyword form has to resolve that first.

## `x ?= y`, nothing-coalescing assignment (VB-010)

```vb
name ?= "unknown"
box?.Name ?= "x"
```

- Adopted in slice 2d, and extended through `?.` in slice 104a.
- For keeping it: it belongs to VB's own family of compound assignments, `+=`, `-=`, `&=`, and `^=`, and it pairs with VB's `?.`. It reads as "assign if Nothing" in the same way that `&=` reads as "append".
- Against it: the `?` is symbolic, and the long form `name = If(name, "unknown")` already exists.
- Alternatives a proposal could weigh: none needed beyond the long form, which keeps working.

## `..items`, a spread in collection and array literals (VB-035)

```vb
Dim all As List(Of Integer) = (1, .. extras, 4)
Dim more As Integer() = {..values, 3}
```

- Adopted in slice 7c for collection expressions, and in slice 102a for array literals.
- For keeping it: it is short, it is what C# 12 writes, and a spread is rare in a literal.
- Against it: it is a symbol with no VB precedent, and `..` already means a range, so `(..items)` and `(..n)` differ only by what follows; slice 7c parenthesizes a range inside a collection to tell them apart.
- Alternatives a proposal could weigh: a keyword before the spread collection, or calling `AddRange` after the literal, which works today.

## Recommendation

- `^n`: keep, flagged as the least VB-like; consider a keyword alternative in a later proposal.
- `a..b`: keep. A keyword form would have to settle the inclusive `To` against the exclusive `Range` first, and no form does that without surprise.
- `x ?= y`: keep. It follows VB's own compound assignments.
- `..items`: keep, flagged; consider a keyword alternative in a later proposal, because `..` is also the range.

## Decision

Decided in slice 159a, as recommended:

- `^n` stays, flagged as a candidate for a keyword alternative in a later proposal.
- `a..b` stays, with no further review: `To` stays inclusive, so a range keeps `..`.
- `x ?= y` stays, with no further review: it follows VB's compound assignments.
- `..items` stays, flagged as a candidate for a keyword alternative in a later proposal.

No spelling changes, and no alternative is planned yet.
