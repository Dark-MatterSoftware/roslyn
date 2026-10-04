# VB-052 GUID literal

This note locks the spelling for the GUID literal. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

```vb
Dim id As Guid = &G{936DA01F-9ABD-4d9d-80C7-02AF85C822A8}
Dim compact As Guid = &G{936DA01F9ABD4d9d80C702AF85C822A8}
```

The prefix is `&G` or `&g`, the same shape as `&H`, `&O`, and `&B`. Braces hold the digits. The hyphenated form is eight, four, four, four, and twelve hexadecimal digits. The compact form is thirty-two hexadecimal digits. Hex digits are case-insensitive. The value's type is `System.Guid`.

`#2016-06-01#` stays a date. Square brackets stay escaped identifiers. A bare `&G` followed by a name, as in `&Goo`, stays concatenation. Parentheses and the `{0x...,{0x..}}` form are not literals. A tagged string such as `"..."GUID` stays a string; that is a different proposal.

Visual Basic 17.13 still treats `&G{...}` as concatenation.
