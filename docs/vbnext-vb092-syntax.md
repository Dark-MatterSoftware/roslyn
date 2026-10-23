# VB-092 `ByRef` locals over ref field structures

This note locks the rule for slice 56b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Slice 50d keeps a structure that holds a ref field from outliving the variables it refers to. It cannot be assigned to a `ByRef` parameter, or to a field of a structure reached through a `ByRef` parameter or `Me` (BC37415). A `Dim ByRef` local from phase 45 can refer to the same variables, and a write through it is not checked:

```vb
Sub Store(ByRef c As Cell)
    Dim local = 1
    Dim ByRef r = c
    r = New Cell(local) ' not reported: c now refers to local after Store returns
End Sub
```

## Locked rule: a write through a `ByRef` local follows its referent

A `Dim ByRef` local refers to the variable that its initializer names. A write through the local is checked as if it were written to that variable directly:

- Assigning a structure that holds a ref field to a `ByRef` local is BC37415 when the local refers to a `ByRef` parameter, or to a field of a structure reached through a `ByRef` parameter or `Me`.
- Assigning one to a field of a structure reached through such a `ByRef` local is BC37415 in the same cases.
- When the local refers to another `ByRef` local, the rule follows that local's referent in turn.
- A `ByRef` local that refers to a local of the method, or to a `ByVal` parameter, may be assigned, as that variable may be.

The error is reported on the assignment, as slice 50d reports it. No new syntax, and no new error.

### What does not change

- `Dim ByRef ReadOnly` locals cannot be assigned, so the rule does not apply to them.
- Passing a `ByRef` local `ByRef` to another method is checked by that method's own rules, as passing the variable itself is.
- A structure that holds a ref field cannot be an array element, a field of a class, or the result of a `ByRef Function` (slices 7g and 50d), so a `ByRef` local that refers to one of those cannot hold such a structure.

Visual Basic 17.13 has neither ref fields nor `Dim ByRef` locals.

## Rejected forms

- A `Dim ByRef` local cannot refer to a structure that holds a ref field at all. It would reject safe uses, such as a `ByRef` local over a local of the method.
- Any whole assignment through a `ByRef` local of such a structure is an error. It would reject the same safe uses.

## Not locked

Returning a structure that holds a ref field, and `scoped` parameters, are left to the VB-093 design note in phase 57.
