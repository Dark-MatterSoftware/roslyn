# VB-067 Property backing fields

This note locks the spelling for slice 28b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

An auto-implemented property, such as `Property Name As String`, gets a `Private` field named `_Name`. A property with a `Get` or `Set` body gets no storage, so validation in `Set`, or change notification, needs a declared field with the property's name and type written a second time.

## Locked spelling: `Field`

```vb
Public Property Name As String
    Get
        Return Field
    End Get
    Set
        If String.IsNullOrWhiteSpace(Value) Then Throw New ArgumentException("A name is required.")
        Field = Value
    End Set
End Property

Public ReadOnly Property Settings As Dictionary(Of String, String)
    Get
        If Field Is Nothing Then Field = LoadSettings()
        Return Field
    End Get
End Property
```

In the `Get` and `Set` bodies of a property, `Field` is storage that the compiler declares for that property. It has the property's type, so it compiles under `Option Strict On` and `Option Infer Off`, and it is `Shared` when the property is `Shared`. `Field` reads and assigns it in either accessor, including the `Get` of a `ReadOnly` property and the `Set` of a `WriteOnly` property. A lambda in an accessor that uses `Field` uses the same storage. The compiler declares the storage only for a property whose accessors use `Field`.

`Field` reads like the `Value` parameter that a `Set` already has without declaring it. C# 14 added the `field` keyword for the same purpose.

### An existing `Field` keeps its meaning

`Field` means the storage only where it would otherwise not be declared: when the accessor has no local or parameter named `Field`, and the type, its base types, and its imports have no member or type named `Field`. Where code that compiles today uses `Field`, it means what it meant before, so no existing code changes. A member named `Field` that is added later takes that name back from the storage.

`Field` outside a property's `Get` or `Set` body is an ordinary identifier.

### The storage is not `_Name`

The storage has a name that Visual Basic code cannot write, so it never conflicts with a member, and a type can declare its own `_Name` beside a property that uses `Field`. Code outside the property reaches the value only through the property. The name in metadata is a compiler detail that slice 28b chooses, and it is not part of the language.

This differs from an auto-implemented property, whose `_Name` field stays as it is today.

### Where `Field` is not available

A property with parameters, such as `Property Item(index As Integer) As T`, has one storage location for every argument, so `Field` there is an error. A `MustOverride` property and an interface property have no bodies.

Visual Basic 17.13 keeps reporting that `Field` is not declared.

### Sources

[vblang #196](https://github.com/dotnet/vblang/issues/196), "Implicit property backing fields" by Anthony Green, proposed a property-scoped `_P` and named "a magic keyword to mean 'the implicit backing field' like `Field`" as the alternative. The VB language design meeting of 2017-11-15 rejected the implicit `_P`. The [C# 14 `field` keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/field-keyword) gives a property body unnamed storage.

## Rejected spellings

The implicit `_Name` from vblang #196 is not adopted. The design meeting rejected it, and in [comments on the issue](https://github.com/dotnet/vblang/issues/196) reduckted noted that projects name fields differently, such as `_name`, and Bill-McC asked what renaming `_P` or `P` would do.

`Property Name As String WithBacking _name`, suggested by reduckted on vblang #196, is not adopted. It adds a keyword to the declaration and repeats a name that the property body already implies.

Using the property's own name for its storage inside its accessors, also suggested there, is not adopted. Inside an accessor, `Name` already calls the property, so the change would alter code that compiles today.

## Not locked

C# 14 treats `field` as a keyword even when a member has that name, and code reaches the member with `this.field`. That rule is not adopted.

An initializer on a property with bodies, as in `Property Count As Integer = 1` followed by `Get` and `Set`, is not locked. Assigning a `ReadOnly` property that uses `Field` from a constructor is not locked. `Field` in the accessors of a `Custom Event` is not covered by this note.
