# VB-066 Pattern variables

This note locks the spelling for slice 27b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

`docs/vbnext-vb004-syntax.md` left this open. After `Case Is TypeOf Customer`, the case body still sees the original type of the Select expression, and after `If TypeOf value Is Customer Then`, the `Then` block still sees `value` as declared. Code converts the value again to use it as a `Customer`.

## Locked spelling: `Into` after the type

```vb
Select Case shape
    Case Is TypeOf Circle Into c
        total += c.Radius * c.Radius * Math.PI
    Case Is TypeOf Square Into s
        total += s.Side * s.Side
End Select

If TypeOf sender Is Button Into b Then
    b.Enabled = False
End If

If TypeOf item Is Order Into o AndAlso o.Total > 100 Then
    Ship(o)
ElseIf TypeOf item Is Quote Into q Then
    Review(q)
End If
```

`Into name` follows the type of a type test and declares a local named `name` whose type is the tested type. The local holds the tested value converted to that type. The value is evaluated once. A value type, such as `Case Is TypeOf Integer Into n`, is unboxed into the local. The type is written, so the form compiles under `Option Strict On` and `Option Infer Off`.

The local is an ordinary local. Code can assign to it. A name that is already a local or a parameter in an enclosing block reports the existing error for a local that hides another.

### In `Select Case`

`Case Is TypeOf T Into name` extends the slice 5e type test. The name is in scope in the body of that `Case` only. A named type test is the only clause of its `Case`, so `Case Is TypeOf A Into x, Is TypeOf B` is an error. The slice 5e relational test and the slice 5f member tests can still follow with `AndAlso`, as in `Case Is TypeOf Customer Into c AndAlso .Balance Is > 0`.

### In `If`

`TypeOf value Is T Into name` is the condition of an `If` or an `ElseIf`, in a block or on a single line, or one operand of the `AndAlso` chain that forms that condition. The name is in scope in the operands to its right in that chain and in the `Then` block of that `If` or `ElseIf`. It is not in scope in a later `ElseIf`, in `Else`, or after `End If`. Each `ElseIf` declares its own names.

The test cannot appear under `Not`, under `OrElse`, or inside another expression, because the name would not be definitely assigned where it is in scope.

### Where `Into` keeps its existing meaning

`Into` is a contextual keyword. After a complete type in `TypeOf ... Is`, an identifier is a syntax error today, so the form does not change existing code. Inside a query expression, `Into` keeps its query meaning: in `Aggregate o In list Where TypeOf o Is Customer Into Count()`, `Into` still ends the `Aggregate` clause.

Visual Basic 17.13 reports that the form is not available.

### Sources

The VB language design meeting of 2018-12-19, which reviewed the community summary on [vblang #337](https://github.com/dotnet/vblang/issues/337), wrote `Case x As String` and then considered `Case String Into x` and `If o Matches String Into x`, after [a suggestion by AdamSpeight2008](https://github.com/dotnet/vblang/issues/119#issuecomment-313021036). In [vblang #541](https://github.com/dotnet/vblang/issues/541), zspitz argued that with `Into` "the main point of the pattern is not the variable declaration, but the type check", and that `Into` already names a result in LINQ queries. The fork keeps the existing `TypeOf ... Is` and `Case Is TypeOf` forms and adds `Into` at the end of them, rather than adding the `Matches` keyword.

## Rejected spellings

`Case c As Customer`, the variable pattern in [vblang #124](https://github.com/dotnet/vblang/issues/124) by Anthony Green and in [vblang #541](https://github.com/dotnet/vblang/issues/541), is not adopted. It starts like the value match `Case c`, and the `If` form needs a new keyword, as in `If value Matches c As Customer Then`.

`TypeOf value Is Customer c`, the C# order suggested in a comment on vblang #124, is not adopted. VB names a declared thing with a keyword, as `Dim` and `As` do, and does not place two names side by side.

`Case Is TypeOf Customer Dim c`, suggested by zspitz on vblang #124, is not adopted. `Dim` starts a statement everywhere else in VB.

Narrowing the tested variable itself, so that `If TypeOf animal Is Duck Then animal.Quack()` compiles, is not adopted for this phase. Anthony Green describes it for ModVB in [Type-Inference Enhancements](https://anthonydgreen.net/2024/06/06/the-agenda-type-inference-enhancements/). It works only on locals and parameters, not on a Select expression such as `GetShape()`, and it changes overload resolution in code that compiles today, for example a call to an overload that takes `Duck`.

## Not locked

`If TypeOf value IsNot T Into name Then Return`, with the name in scope after the `If`, is not locked. `IsNot` with `Into` is an error.

`Into` after a type list from `docs/vbnext-vb054-syntax.md`, as in `TypeOf value Is {Customer, Order} Into x`, is an error, because the local has no single type.

`Into` in a `Do While`, `Do Until`, or `Loop While` condition, in a `Case` that is not `Case Is TypeOf`, and in an expression outside an `If` or `ElseIf` condition, such as `Dim ok = TypeOf value Is T Into x`, is not locked.
