# VB-166 Nested deconstruction

This note records the form for VB-166. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

## The form nests the deconstruction list

A slot of a deconstruction list may itself be a parenthesized deconstruction list, as C# 7 nests `var ((x, y), label) = value;`. The three forms of earlier waves take it:

```vb
Function Nested() As ((Integer, Integer), String)
    Return ((2, 3), "pair")
End Function

Dim ((x, y), label) = Nested()                  ' slices 5a and 149b: declares x, y, and label
((x, y), label) = Nested()                      ' slices 5b, 148a, and 149b: assigns existing targets
((box.Width, cells(1)), label) = Nested()       ' slice 148a targets nest too

For Each ((x, y), label) In {Nested()}          ' phase 39 and slice 149c
Next

For Each ((x As Long, y), label As String) In {Nested()}
Next

Dim ((first, ), ) = Nested()                    ' empty slots, as slice 5c spells a discard
```

- A nested list is deconstructed the way a list at the top is: by the elements of a tuple of the same length, or else by an accessible `Deconstruct` method whose `ByRef` parameters match the slots, as slice 5d finds it.
- The value is evaluated once. Each nested element is taken apart after the level that contains it, and the elements are assigned left to right, depth first.
- An empty slot may appear at any level, and a nested list may be empty only in the sense that each of its slots is.
- In the assignment form, a slot holds anything an assignment can target, as slice 148a allows, or a nested list or an empty slot. The receivers and indexes of all targets are evaluated once, left to right, before the value.
- In `For Each` and `Await Each`, an `As` clause may follow any name at any level, as phase 39 allows at the top. `Dim` infers each type, as slice 5a does.
- A nested list in a `Dim` deconstruction declares no name for the list itself; only names declare locals.
- Visual Basic 17.13 still rejects every nested form.

## What does not change

- `Dim (x, y) = value`, `(x, y) = value`, `(x, , z) = value`, and `For Each (a, b) In values` keep the meaning of slices 5a to 5d and phase 39.
- `(a, b) = (c, d)` in an expression stays the tuple comparison of slice 96a; only a statement deconstructs.
- `Dim x((1, 2))` and other array bounds are not deconstruction lists: a deconstruction list starts right after `Dim`, or at the start of the statement, or after `For Each`.
