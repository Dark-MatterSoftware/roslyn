# VB-072 A type test that guards the rest of a block

This note locks the form for slice 33d. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

`docs/vbnext-vb066-syntax.md` scopes a name declared with `Into` to the `Then` block of its `If`. Code that checks its input first and returns early has to nest the rest of the method inside that block, or test the type a second time:

```vb
Function Describe(value As Object) As String
    If TypeOf value Is Customer Into c Then
        ' the rest of the method, one level deeper
        Return c.Name & " owes " & c.Balance.ToString()
    End If
    Return "not a customer"
End Function
```

## Locked form: `IsNot ... Into` in a guard `If`

```vb
Function Describe(value As Object) As String
    If TypeOf value IsNot Customer Into c Then Return "not a customer"

    Return c.Name & " owes " & c.Balance.ToString()
End Function

Sub Process(items As IEnumerable(Of Object))
    For Each item In items
        If TypeOf item IsNot Order Into pending Then
            Log("skipped " & item.ToString())
            Continue For
        End If

        Ship(pending)
    Next
End Sub
```

`TypeOf value IsNot T Into name` is the whole condition of an `If`, on a single line or in a block. The name has type `T`. It is in scope from the statement after the `If`, or after its `End If`, to the end of the block that contains the `If`, where it holds `value` converted to `T`. It is not in scope in the condition or in the `Then` part, where the test failed.

### The `Then` part always leaves

The end of the `Then` part must be unreachable, as it is when the part ends with `Return`, `Exit`, `Continue`, `Throw`, or `GoTo`. Otherwise the statements after the `If` could run when `value` is not a `T`, and `Into` is an error. The rule is the same in a single-line `If` and in a block.

### No `Else` or `ElseIf`

A guard `If` has no `Else` and no `ElseIf`. Either one with `IsNot ... Into` is an error, so the name has one meaning: from after the `If` to the end of the block.

### Names

The name is declared in the block that contains the `If`, as a `Dim` there would be. It is an error when that block, or an enclosing one, already declares the name, or when a later statement in the block declares it again. A lambda after the `If` can capture the name.

`IsNot` with `Into` anywhere else, such as in an `ElseIf`, under `Not`, in an `AndAlso` or `OrElse` chain, or in a `Case`, still reports BC37396.

Visual Basic 17.13 does not have `Into`. Slice 33d implements the guard form on `vbnext`.

### Sources

C# 9 declares a pattern variable that is in scope after `if (value is not T name) return;` and definitely assigned when the `if` statement completes. Swift's `guard let`, where the `else` branch must leave, uses the same rule as this note.

## Rejected forms

Allowing a `Then` part that can finish normally, with the name in scope after the `If` and an error only where it is read before it is definitely assigned, is not adopted. That is the C# rule. Here the requirement is visible in the `If` itself.

Allowing an `Else` block, with the name in scope there as well as after the `If`, is not adopted.

## Not locked

`IsNot ... Into` as one operand of an `OrElse` chain, as in `If value Is Nothing OrElse TypeOf value IsNot T Into name Then Return`, is not locked. A guard in a `Do While` or `Loop Until` condition is not covered by this note.
