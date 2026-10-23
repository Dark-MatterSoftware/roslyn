# VB-093 and VB-094 Returning ref structures

This note locks the rules for slice 57b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Two gaps decide the same question: what a ref structure that leaves a method may refer to. A ref structure here is any `ByRef Structure`, from VB or from another assembly, including `Span(Of T)` and `ReadOnlySpan(Of T)`.

- VB-094. Slice 51a converts any inline array variable to a span, so a function can return a span over its own local, and the caller gets a span over storage that no longer exists:

  ```vb
  Function F() As Span(Of Integer)
      Dim b As Buffer4
      Return b ' not reported today
  End Function
  ```

- VB-093. Slice 50d reports BC37414 for every function or property that returns a structure that holds a ref field, so a factory cannot be written:

  ```vb
  Function Wrap(ByRef value As Integer) As Cell ' BC37414 today
      Return New Cell(value)
  End Function
  ```

## Locked rule: values that refer to local storage

A value of a ref structure type refers to local storage when it can refer to a variable of the current method. Each of these does:

- A span converted from an inline array that is a local, a `ByVal` parameter, `Me` in a structure, a temporary such as a function result, or a field of a structure reached through one of those.
- `New T(...)` of a structure that holds a ref field, when an argument passed `ByRef` is a local, a `ByVal` parameter, `Me`, a temporary, or a field of a structure reached through one of those, or when any argument of a ref structure type refers to local storage.
- A call or property whose result is a ref structure, under the same conditions on its arguments and its receiver.
- A local of a ref structure type that is given a value that refers to local storage anywhere in the method. The rule does not follow the order of statements, so a value assigned later in a loop still counts.

Every other value of a ref structure type refers only to the caller's storage. Examples are a `ByVal` or `ByRef` parameter of the ref structure type, a span over an array or a class field, and `New Cell(x)` where `x` is a `ByRef` parameter, an array element, a class field, or a `ByRef` result.

### Where such a value cannot go

A value that refers to local storage cannot be:

- returned from a function or property, by `Return` or by assigning the function name;
- assigned to a `ByRef` parameter, or to a field of a structure reached through a `ByRef` parameter, through `Me`, or through a `Dim ByRef` local that refers to one of those, as slice 56b follows it.

These report a new error, BC37418, "This value can refer to a variable of this method, so it cannot leave the method."

### Returning a structure that holds a ref field

A function or property may now return a structure that holds a ref field. BC37414 is no longer reported for the declaration. A `Return` of such a structure is checked by the rule above, so `Return New Cell(value)` is allowed where `value` is a `ByRef` parameter, and is BC37418 where it is a local.

Slice 50d's BC37415 still applies to any structure that holds a ref field when it is assigned to a `ByRef` parameter or through `Me`, whatever it refers to. This note does not relax it.

Visual Basic 17.13 has neither ref fields nor inline arrays.

## Rejected forms

- A span over a local inline array may be used only as an argument, a `For Each` collection, or an element access. It would reject a span local, the most common use.
- Only `Return` of a span built directly from a local is an error. Storing the span in a local first would escape the check.
- Keeping BC37414 for every function and property. A factory over `ByRef` arguments is safe and useful.
- C#'s escape analysis with a `scoped` parameter modifier. It needs a new keyword and a flow analysis, and the fixed rules above cover the shapes that waves 9 and 10 meet.

## Not locked

`scoped` parameters, returning a structure that refers to a `ByVal` parameter's own storage through a ref field of that parameter, and ref structures captured by lambdas or iterators, which slice 7g already rejects, are not covered.
