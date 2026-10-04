# VB-050 Compact members

This note records the spelling for compact members. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

The issues proposed several spellings for a property whose body is one expression:

```vb
ReadOnly Property Kind As SyntaxKind => CType(Me.RawKind, SyntaxKind)
ReadOnly Property Kind As SyntaxKind = Get CType(Me.RawKind, SyntaxKind)
ReadOnly Property Kind As SyntaxKind = Return CType(Me.RawKind, SyntaxKind)
ReadOnly Property Kind As SyntaxKind Get CType(Me.RawKind, SyntaxKind)
Property IsReadOnly As Boolean On Me.TextBox.IsReadOnly
```

They also proposed a property with a single-line `Get` and `Set` and no `End Get`, and a `ReadOnly Property` body with no `Get` block.

`ReadOnly Property Kind As SyntaxKind = expr` is already an auto-property with an initial value. That form keeps its meaning. The issues did not choose among `=>`, `= Get`, `= Return`, a trailing `Get`, or a trailing `On`, `With`, or `Using`. There is no implementation slice until one of those spellings is chosen.
