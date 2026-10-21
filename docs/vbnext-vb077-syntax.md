# VB-077 Deconstruction in For Each

This note locks the form for slices 39b and 39c. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Slices 5a to 5d added `Dim (x, y) = point`, which declares locals from a tuple or a `Deconstruct` method, and `(x, y) = point`, which assigns existing variables. A loop over a dictionary, a list of tuples, or an `IAsyncEnumerable` of records still needs a separate line in its body:

```vb
For Each pair In prices
    Dim (name, price) = pair
    Console.WriteLine($"{name}: {price}")
Next
```

## Locked form: `For Each (a, b) In collection`

```vb
For Each (name, price) In prices
    Console.WriteLine($"{name}: {price}")
Next

For Each (x As Integer, y As Integer) In points
    Plot(x, y)
Next

Await Each (id, payload) In channel.Reader.ReadAllAsync()
    Await HandleAsync(id, payload)
Next
```

A parenthesized list of names in place of the control variable of a `For Each` deconstructs each item, as `Dim (a, b) = item` would at the start of the body: from the elements of a tuple, or from the `ByRef` parameters of an accessible `Deconstruct` method, the same as slices 5a to 5d. The item is evaluated once per iteration.

### Each name follows the control variable rule

Each name in the list behaves as the control variable of `For Each x In items` does today. A name that is not already a local or parameter in scope is declared for the loop, with its type from the matching element under `Option Infer On`. A name that is already a local or parameter is assigned. A loop can mix the two, as in `For Each (existing, fresh) In pairs`.

### An `As` clause on each element

Each name may have its own `As` clause, as in `For Each (key As String, value As Integer) In map`. The name is then declared with that type, and the element converts to it as an assignment would, under the current `Option Strict`. A name with an `As` clause is always declared, so it is an error when a local with that name is already in scope, as it is for `For Each x As Integer In items`.

`Dim (x, y) = point` has no `As` clause today, and this note does not add one there.

### `Await Each`

`Await Each (a, b) In source` deconstructs each item of an asynchronous stream with the same rules. Slice 39c implements it after slice 39b implements `For Each`.

### `Next`

`Next` after a deconstructing loop takes no variable. `Next (name, price)` is an error.

Visual Basic 17.13 does not have the form. Slices 39b and 39c implement it on `vbnext`.

### Sources

C# 7 deconstructs in `foreach (var (key, value) in map)` and assigns existing variables in `foreach ((key, value) in map)`. Visual Basic already lets one `For Each` control variable be either new or existing, so one form covers both cases here.

## Rejected forms

A form that always declares new names, with an error for a name that is already a local, is not adopted. It would make the tuple form behave differently from `For Each x In items`.

## Not locked

Empty slots, as in `For Each (x, , z) In triples`, which slice 5c allows for `Dim (x, , z)`, are not locked. Nested lists, as in `For Each (name, (x, y)) In entries`, are not locked. An `As` clause in `Dim (x As Integer, y As Integer) = point` is not covered by this note.
