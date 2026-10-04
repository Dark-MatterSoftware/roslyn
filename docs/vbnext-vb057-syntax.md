# VB-057 Unchecked arithmetic

This note records the spelling for unchecked arithmetic. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

[vblang #494](https://github.com/dotnet/vblang/issues/494) is a feature specification for an `Unchecked` operator:

```vb
Dim int1 As Integer = Unchecked(Integer.MaxValue + 1) ' Integer.MinValue, no compiler error
Dim int2 As Integer = Integer.MaxValue + 1            ' Compiler error
```

On `vbnext`, `Unchecked(expression)` evaluates `expression` without integer overflow checks. Integer addition, subtraction, multiplication, negation, and integer conversions inside the parentheses wrap instead of throwing `System.OverflowException`. A constant expression that overflows inside the parentheses wraps to its low bits and is not a compile-time error. The result has the type and value of `expression`.

The issue asked that the feature not break code that already uses the name. `Unchecked` stays an ordinary identifier. `Unchecked(expression)` is the operator only when it has one unnamed argument and the name `Unchecked` does not refer to anything in scope. A method, variable, or type named `Unchecked` keeps its meaning, and `[Unchecked](expression)` is never the operator.

[vblang #33](https://github.com/dotnet/vblang/issues/33) also proposed `Option Unchecked`, `Option Checked Off`, an `Unchecked ... End Unchecked` block, and a `#Checked Off` directive. It did not choose one. Those forms are not locked.
