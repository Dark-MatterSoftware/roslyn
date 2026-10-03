# VB-035 Collection expressions

This note locks the spelling for slices 7a through 7c. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

A collection expression is a parenthesized list of elements. The target type decides what is built. Array literals `{1, 2}` and `From {1, 2}` stay as they are. Square brackets stay escaped identifiers. Visual Basic uses parentheses for arrays, not brackets.

## Parentheses

```vb
Dim values As Integer() = (1, 2, 3)
```

Elements are expressions, separated by commas. A newline is allowed where a tuple literal already allows one.

`(1, 2)` with no target type stays a tuple. `(name)` is a one-element collection when the target type is an array, and stays a parenthesized expression otherwise. `()` is an empty collection. An empty collection with no target type is an error.

Visual Basic 17.13 still treats `(1, 2)` as a tuple, so it does not build an array.

## 7a. Array

When the target type is an array, the expression builds an array of that element type. Each element converts to the element type. A nested list is an element, and the element type is its target.

```vb
Dim values As Integer()() = ((1, 2), (3))
```

## 7b. Collection initializer

The same parentheses target a type that already accepts a collection initializer. The expression creates that type and adds each element. It does not build an intermediate array.

## 7c. Spread

Inside the parentheses, `.. items` copies the elements of an array or a list. A range used as an element is parenthesized again, so it is not read as a spread.
