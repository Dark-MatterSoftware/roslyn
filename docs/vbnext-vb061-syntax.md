# VB-061, VB-062, VB-063 Asynchronous streams

This note records the spellings for asynchronous disposal, asynchronous iteration, and async iterators. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

## Locked spellings

### Asynchronous disposal (VB-061): `Async Using`

```vb
Async Using connection = Await OpenAsync()
    Await connection.SendAsync(message)
End Using

Async Using stream = File.OpenRead(path)
```

A resource that implements `IAsyncDisposable` is released with an awaited `DisposeAsync` when the block ends. The second line is the `Using` declaration from slice 2e: with no `End Using`, the resource is released at the end of the enclosing block. The form is valid only in an `Async` method or lambda. Slice 26b implements it.

The VB language design meeting named this form. In [vblang #37](https://github.com/dotnet/vblang/issues/37), Anthony Green wrote on 2017-09-13 that "`IAsyncDisposable` and `Async Using` which we're looking at for VB 16/C# 8" would raise the priority of `Await` in `Finally`.

`Async` is not a reserved word, so `Async` at the start of a statement is an identifier today. An identifier followed by `Using` is not a valid statement, so the form does not change the meaning of existing code. `Async` is also the method modifier that allows `Await`, so the form reads as the asynchronous `Using`.

### Asynchronous iteration (VB-062): `Await Each ... Next`

```vb
Await Each item In asyncSource
    If item.IsDone Then Exit For
    Console.WriteLine(item)
Next
```

The loop iterates an `IAsyncEnumerable(Of T)`. It awaits `MoveNextAsync` for each item and awaits `DisposeAsync` on the enumerator when the loop ends. `Exit For` leaves the loop and `Continue For` starts the next item, the same as in `For Each`. The form is valid only in an `Async` method or lambda. Slice 26c implements it.

[vblang #163](https://github.com/dotnet/vblang/issues/163), opened by Anthony Green on 2017-09-19, proposes this loop, and comments on the issue in 2020 ask for it by that name. `Each` is a reserved word, so `Await Each` cannot start an `Await` expression, and the form does not change existing code.

### Async iterators (VB-063): `Async Iterator Function`

```vb
Async Iterator Function EnumerateCustomers() As IAsyncEnumerable(Of Customer)
    Do
        Dim c = Await ReadNextAsync()
        Yield c
    Loop
End Function
```

A function with both modifiers can `Await` and `Yield`, and returns `IAsyncEnumerable(Of T)` or `IAsyncEnumerator(Of T)`. Before `vbnext`, the two modifiers together report BC36936, "'Async' and 'Iterator' modifiers cannot be used together", and Visual Basic 17.13 keeps that error. Slice 26d implements it on `vbnext`, and the slice 26c loop consumes it.

[vblang #163](https://github.com/dotnet/vblang/issues/163) proposes both existing modifiers on one function. C# marks the method `async` and infers the iterator from `yield`, but VB already requires `Iterator` for `Yield`, so both modifiers stay.

## Rejected spellings

`Await Using`, the transliteration of the C# `await using` in [enhanced-using.md](features/enhanced-using.md#asynchronous-disposal), is not adopted. `Await` marks where a method suspends, and the dispose suspends at `End Using`, not at the statement that starts the block.

`Await For Each`, named in the accepted answer to [vb.net equivalent to await foreach in c#](https://stackoverflow.com/questions/71374654/vb-net-equivalent-to-await-foreach-in-c) on Stack Overflow and matching the C# `await foreach`, is not adopted. The locked loop is `Await Each`, from the VB proposal.

## Shared by every form

VB reports BC36943 for `Await` inside `Finally`. The compiler lowers each locked form itself, so the awaited `DisposeAsync` runs when the body completes, on `Exit` and `Return`, and when the body throws. User code still cannot `Await` in `Finally`.

The loop and the iterator use `[EnumeratorCancellation]` and `WithCancellation` from the libraries. Neither needs syntax.

## Not locked

`Iterator Async Function`, with the modifiers in the other order, is not locked. Async iterator lambdas are not covered by this note.

[vblang #163](https://github.com/dotnet/vblang/issues/163) also asks about async LINQ, such as `Async From` or `Await item In asyncSource`. That proposal is not part of phase 26.

`ConfigureAwait` on the awaited dispose and the awaited `MoveNextAsync` is a call on the resource or the source, the same as in C#, and needs no syntax.
