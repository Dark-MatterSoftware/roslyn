# VB-059 Optional End Namespace

This note locks the spelling for an optional `End Namespace`. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

```vb
Namespace Sample
Class Test
End Class
```

On `vbnext` the namespace continues to the end of the file. `End Namespace` remains valid. A second `Namespace` before `End Namespace` still nests, so a file that already closes each namespace keeps its meaning. Visual Basic 17.13 still expects `End Namespace`.
