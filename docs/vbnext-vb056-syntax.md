# VB-056 For Each ByRef

This note locks the spelling for `For Each ByRef`. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

```vb
For Each ByRef item In values
    item += 1
Next

For Each ByRef item As Integer In values
Next

For Each ByRef item With index In values
Next
```

`ByRef` makes the loop variable a reference to each element of a one-dimensional array. Assigning to the variable, or to a field of a structure, writes that element. The variable is new for the loop. An existing variable is an error. A collection that is not a one-dimensional array of the variable's type is an error. A lambda cannot capture the variable. `With` may name an index, as in VB-055. Visual Basic 17.13 still rejects `ByRef` in this position.
