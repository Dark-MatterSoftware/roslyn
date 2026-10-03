# VB-035 Collection expressions

This note locks the spelling for slices 7a through 7c. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

A collection expression is a bracketed list of elements. The target type decides what is built. The expression has no type of its own. Array literals `{1, 2}` and `From {1, 2}` stay as they are.

## Brackets

```vb
Dim values As Integer() = [1, 2, 3]
```

Elements are expressions, separated by commas. A trailing comma is allowed. A newline is allowed after `[`, after a comma, and before `]`.

`[Name]` remains an escaped identifier. The brackets start a collection expression only when they are not that identifier. A one-element collection whose element is an identifier is written with parentheses:

```vb
Dim values As String() = [(name)]
```

`[]` is an empty collection. `[name, other]` is a collection, because the comma means the brackets are not an escaped identifier.

Visual Basic 17.13 still treats `[` as the start of an escaped identifier.

## 7a. Array

When the target type is an array, the expression builds an array of that element type. Each element converts to the element type. A nested collection expression is an element, and the element type is its target.

`Dim values = [1, 2]` has no target type, so it is an error.

## 7b. Collection initializer

The same brackets target a type that already accepts a collection initializer. The expression creates that type and adds each element. It does not build an intermediate array.

## 7c. Spread

Inside the brackets, `.. items` copies the elements of an array or a list. A range used as an element is parenthesized, so it is not read as a spread.
