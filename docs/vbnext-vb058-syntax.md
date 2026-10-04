# VB-058 Chaining a Sub call

This note records the spelling for chaining calls to a `Sub`. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

[vblang #530](https://github.com/dotnet/vblang/issues/530) proposed repeating the argument list after a `Sub` call:

```vb
Console.WriteLine("Test")("Chain")("Call")
```

The same text after a `Function` call indexes or invokes the returned value, so the meaning would depend on whether the method returns a value. The thread also proposed these alternatives, and did not choose one:

```vb
Console.WriteLine("Test") : ("Chain") : ("Call")
Call Console.WriteLine With ("Test"), ("Chain"), ("Call")
```

`:` already separates statements, and `With` already starts an object initializer. No spelling is locked, and a `Sub` call still has no value. There is no implementation slice until a proposal says what a chained `Sub` call means.
