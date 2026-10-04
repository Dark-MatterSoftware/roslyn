# VB-050 Compact members

This note records the spelling for compact members. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

## Locked spelling

A compact member replaces its block body with `=` followed by the keyword the block would have used:

```vb
ReadOnly Property Kind As SyntaxKind = Get CType(Me.RawKind, SyntaxKind)

Function Area(width As Integer, height As Integer) As Integer = Return width * height

Sub Log(message As String) = Call Console.WriteLine(message)
```

- A read-only property takes `= Get expression`. It means a `Get` block that returns that expression. The property has no `End Property`, no `Set`, and no backing field.
- A `Function` takes `= Return expression`. It means a body that returns that expression. The function has no `End Function`.
- A `Sub` takes `= Call invocation`. It means a body with that one `Call` statement. The sub has no `End Sub`.

Each member kind has one spelling. `= Get` is rejected on a `Function` or a `Sub`, `= Return` is rejected on a property or a `Sub`, and `= Call` is rejected on a property or a `Function`.

`ReadOnly Property Kind As SyntaxKind = expr` is already an auto-property with an initial value. That form keeps its meaning, because the keyword after `=` is what makes a member compact.

## Rejected spellings

The issues ([#61](https://github.com/dotnet/vblang/issues/61), [#403](https://github.com/dotnet/vblang/issues/403), [#567](https://github.com/dotnet/vblang/issues/567)) also proposed `=>`, a trailing `Get` with no `=`, and a trailing `On`, `With`, or `Using`. `=>` is a symbolic C# form, and the others read as a second clause on the declaration rather than a body. They are not adopted.

## Not locked

A writable property, a single-line `Get` and `Set` pair without `End Get`, a `MustOverride` or interface member, and a compact operator or event accessor are not covered by this note.
