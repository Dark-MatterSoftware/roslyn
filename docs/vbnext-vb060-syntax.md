# VB-060 XML literal enhancements

This note records the spelling for statement blocks inside XML literal embedded expressions. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

[vblang #397](https://github.com/dotnet/vblang/issues/397) proposes statements directly inside `<%= %>`:

```vb
<ul>
    <%= For Each genre In Model
            Yield <li><%= genre.Name %></li>
        Next %>
</ul>

<ul>
    <%= Select Case Model.Count
            Case 1
                Return <li>One Item</li>
            Case Else
                Return <li>Many Items</li>
        End Select %>
</ul>
```

When the content of `<%= %>` starts with a statement keyword that cannot start an expression, such as `For`, `Select Case`, `While`, `Do`, `Dim`, or `If` without `(`, the content is a statement block. `Return` returns the embedded value, and `Yield` makes the block an iterator. The block means the same as `(Function() ... End Function)()`, or `(Iterator Function() ... End Function)()` when it contains `Yield`. Content that starts with an expression keeps its meaning, so `<%= value = "x" %>` stays an equality test.

The thread also proposed `<%= Let value = "x" %>` and `<%= Set value = "x" %>` for assignment, and left both open. Those forms are not locked.

The backlog schedules VB-060 only if XML literals are in scope for this fork. That scope decision is not recorded yet, so there is no implementation slice until it is.
