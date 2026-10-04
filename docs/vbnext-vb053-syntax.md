# VB-053 With that declares its variable

This note records the spelling for a `With` block that declares its variable. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

## Locked spelling

```vb
With Dim customer = GetCustomer()
    .Name = "Ada"
    Console.WriteLine(customer.Id)
End With

With Dim total As Integer = 0
    Console.WriteLine(.ToString())
End With
```

`With Dim name = expression` and `With Dim name As Type = expression` declare a local named `name` and use it as the object of the `With` block. The local is scoped to the block. Inside the block, `.Member` and `name.Member` refer to the same object. The local's type comes from `As Type`, or is inferred from the expression under the same rules as `Dim`.

`Dim` is the keyword that already declares a local, so the form reads the way a declaration reads everywhere else.

## Rejected spellings

[vblang #438](https://github.com/dotnet/vblang/issues/438) proposed `With X = "Test"`. That is already a `With` on the equality test `X = "Test"`, and it stays an equality test. `Into` and `:=` were also proposed. `:=` is a symbolic form, and `Into` already belongs to query syntax. They are not adopted.

## Not locked

`With Dim name As New Type(...)`, several names in one `With Dim`, and a `With Dim` without an initializer are not covered by this note.
