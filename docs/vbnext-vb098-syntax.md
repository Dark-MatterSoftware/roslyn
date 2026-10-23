# VB-098 `ByRef ReadOnly` parameters

This note locks the form for slice 62b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

VB can pass arguments to C# `in` and `ref readonly` parameters, but cannot declare a parameter that refers to the caller's variable without allowing writes to it:

```vb
Function Twice(ByRef ReadOnly value As Integer) As Integer ' BC30181 today
    Return value * 2
End Function
```

## Locked form: `ByRef ReadOnly` parameters

`ByRef ReadOnly` before a parameter declares a parameter that refers to the caller's variable and cannot be written through it, which C# 7.2 declares with `in`. It is the pair of `ByRef` and `ReadOnly` that `Dim ByRef ReadOnly` locals, `ByRef ReadOnly` fields, and `ByRef ReadOnly Function` already use.

- It may appear wherever `ByRef` may appear on a parameter, and with `Optional`. It cannot appear on a `ParamArray` parameter or in an `Async` or `Iterator` method, as `ByRef` cannot.
- In the method body the parameter is read as a `ReadOnly` variable: assigning it or compound assigning it is an error, passing it `ByRef` passes a copy, a `Dim ByRef ReadOnly` local may refer to it but a `Dim ByRef` local may not, and a method called on a structure parameter works on a copy, as for a `ReadOnly` field.
- A caller passes a variable by reference, including a `ReadOnly` variable, without a copy. A value that is not a variable, such as a literal or a function result, is copied into a temporary that is passed by reference, as VB already does for a `ByRef` argument that is not a variable.
- For overloading it counts as `ByRef`, so a method cannot be overloaded only on `ByRef ReadOnly` against `ByRef` or `ByVal`. It overrides or implements only a parameter that is `in` or `ref readonly`.
- It is emitted as C# emits `in` parameters: a by-reference parameter marked `IsReadOnlyAttribute`, with the `InAttribute` required modifier on interface, virtual, and delegate members.

The modifiers may come in either order, as for any VB modifiers.

Visual Basic 17.13 does not have the form.

## Rejected forms

- `ReadOnly ByRef value As Integer` as the only spelling. VB lets modifiers come in either order, and every other `ByRef ReadOnly` form is written with `ByRef` first.
- Emitting C# 12 `ref readonly` parameters, where passing a value is a warning. VB already copies a value passed `ByRef` into a temporary, and `in` keeps that behavior.

## Not locked

C# 12 `ref readonly` parameters, `ByRef ReadOnly` parameters of lambdas, and `scoped` parameters are not covered by this note.
