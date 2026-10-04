# VB-051 Concise lambdas

This note records the spelling for concise lambdas. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

The issues proposed three spellings:

```vb
Dim names = customers.Select(_.Name)                ' vblang #256
Dim names = customers.Select(Fn(c) c.Name)          ' vblang #371
Dim names = customers.Select(.Name)                 ' vblang #563
```

[vblang #256](https://github.com/dotnet/vblang/issues/256) is labeled "LDM Reviewed: No plans". `_` is also the line continuation character. [vblang #371](https://github.com/dotnet/vblang/issues/371) uses `Fn` as a contextual keyword. A language designer replied that `Fn(x)(y)` is already an invocation followed by an index, and that a lambda keyword that depends on imports is hard to read. [vblang #563](https://github.com/dotnet/vblang/issues/563) uses a leading `.`, which already refers to the object of an enclosing `With` block.

The issues did not choose among `_`, `$1`, `Fn`, and a leading `.`. `Function(c) c.Name` keeps its meaning. There is no implementation slice until one of those spellings is chosen.
