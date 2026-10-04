# VB-053 With that declares its variable

This note records the spelling for a `With` block that declares its variable. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

The original examples were:

```vb
With X = "Test"
End With

With X As Integer = 0
End With
```

`With X = "Test"` is already a `With` on the equality test `X = "Test"`. That form stays an equality test. The issue also left `With Dim`, `Into`, and `:=` open, and did not choose one. There is no implementation slice until one of those spellings is chosen.
