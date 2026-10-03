# VB-004 Select Case patterns

This note locks the spelling for slices 5e through 5h. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

`Case Is` followed by a relational operator is unchanged. `TypeOf` is not a relational operator, so `Case Is TypeOf` is free on Visual Basic 17.13, which still reports an expected relational operator. `Case TypeOf x Is T` stays a simple case whose value is the existing Boolean `TypeOf` expression.

## 5e. Type test and relational test

```vb
Select Case value
    Case Is TypeOf Integer AndAlso Is > 0
    Case Is TypeOf String
End Select
```

`Case Is TypeOf T` matches when the Select expression passes the same type test as `TypeOf x Is T`. The Select expression must be a reference type or a type parameter, as `TypeOf` already requires.

`AndAlso` followed by a relational case (`Is > expr` or `> expr`) must also succeed. That relational test is the existing `Case` relation, applied to the Select expression. The Select expression is evaluated once. This slice does not declare a pattern variable, so the case body still sees the original type of the Select expression.

## 5f. Property patterns

```vb
Case Is TypeOf Customer AndAlso .Balance Is > 0 AndAlso .Name = "A"
```

A leading `.` after `AndAlso` reads a member of the value after the type test. That form is not part of 5e.

## 5g. Positional patterns

```vb
Case Is (1, 2)
Case Is TypeOf Point(0, 1)
```

A parenthesized list compares tuple elements, or the `ByRef` results of `Deconstruct`, to those expressions. That form is not part of 5e.

## 5h. Select Case expression

```vb
Dim label = Select Case order
                Case Is > 0
                    "positive"
                Case Else
                    "other"
            End Select
```

`Select Case` produces a value when it appears where a value is required. That form is not part of 5e. List patterns wait until phase 7.
