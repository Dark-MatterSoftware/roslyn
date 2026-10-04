# VB-055 For Each with an index

This note locks the spelling for `For Each` with an index. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

```vb
For Each album With position In top40Chart
    Console.WriteLine(position)
Next

For Each item As String With index In items
Next
```

`With` names an `Integer` local for the loop. The first item is 0. The value increases by one at the end of each iteration, including when the body runs `Continue For`. A name already used by the loop variable, or by a local or parameter in the enclosing block, is an error. Visual Basic 17.13 still expects `In` after the loop variable.
