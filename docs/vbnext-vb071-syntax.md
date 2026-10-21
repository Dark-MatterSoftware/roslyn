# VB-071 Async iterator lambdas

This note locks the spelling for slice 33c. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Slice 26d added `Async Iterator Function` for methods, and `docs/vbnext-vb061-syntax.md` left lambdas open. A lambda today cannot be both `Async` and `Iterator`, so code that passes an `IAsyncEnumerable(Of T)` to a method, or returns one from a query helper, needs a separate named function.

## Locked spelling: `Async Iterator Function`

```vb
Dim numbers = Async Iterator Function()
                  For i = 1 To 3
                      Await Task.Delay(10)
                      Yield i
                  Next
              End Function

Await Each n In numbers()
    Console.WriteLine(n)
Next

Dim lines As Func(Of String, IAsyncEnumerable(Of String)) =
    Async Iterator Function(path As String)
        Using reader As New StreamReader(path)
            Dim line = Await reader.ReadLineAsync()
            While line IsNot Nothing
                Yield line
                line = Await reader.ReadLineAsync()
            End While
        End Using
    End Function
```

A multi-line lambda that starts with `Async Iterator Function` both awaits and yields, as an `Async Iterator Function` method from slice 26d does. The modifiers come in the same order as on a method. The body is lowered as the method body is, so `Await`, `Yield`, `Try`, `Finally`, `Async Using`, and `Await Each` mean the same thing in it.

There is no single-line form, because `Yield` is a statement. `Async Iterator Sub` is not a form, as `Iterator Sub` is not.

### The return type

The lambda's return type is `IAsyncEnumerable(Of T)` or `IAsyncEnumerator(Of T)`. When the lambda converts to a delegate type, the delegate's return type supplies it, and any other return type is an error, as it is for the method. An `As` clause, as in `Async Iterator Function() As IAsyncEnumerable(Of Integer)`, supplies it too.

Without a target delegate type or an `As` clause, the lambda's return type is `IAsyncEnumerable(Of T)`, where `T` is inferred from the `Yield` operands in the same way an `Iterator` lambda infers `IEnumerable(Of T)`, including when the lambda has no `Yield`.

### Cancellation

A lambda parameter cannot carry `EnumeratorCancellation`, so the token passed to `GetAsyncEnumerator`, for example by `WithCancellation`, does not reach a lambda's parameters. A lambda that needs a token takes it as an ordinary parameter, or captures one from the enclosing method.

Visual Basic 17.13 keeps reporting that a lambda cannot be both `Async` and `Iterator`. Slice 33c implements the lambda on `vbnext`.

### Sources

C# lambdas cannot be iterators, so C# 8 writes an async iterator inside a method as a local function. Visual Basic already has `Iterator` lambdas, so the async form follows them rather than C#. Slice 26d and `docs/vbnext-vb061-syntax.md` record the method form.

## Rejected spellings

`Iterator Async Function`, with the modifiers in the other order, is not adopted. The method form uses `Async Iterator`, and one order for both keeps the forms easy to read and to complete in the editor.

Requiring a target delegate type or an `As` clause is not adopted. It would make `Dim numbers = Async Iterator Function() ...` an error where `Dim numbers = Iterator Function() ...` compiles today.

## Not locked

`Async Iterator Function` as a single-line lambda is not locked. Attributes on lambda parameters, which would let `EnumeratorCancellation` apply to a lambda, are not covered by this note.
