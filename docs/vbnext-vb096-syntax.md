# VB-096 Shared virtual interface members

This note locks the form for slice 60b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

A VB interface can declare a shared abstract member with `Shared MustOverride` (slice 9f), and slice 47a lets a type leave out a shared virtual member with a default body that another assembly declares. A VB interface cannot declare such a member:

```vb
Interface INamed
    Shared Overridable Function DefaultName() As String ' BC30270 and BC30501 today
        Return "unnamed"
    End Function
End Interface
```

## Locked form: `Shared Overridable`

```vb
Interface INamed
    Shared Overridable Function DefaultName() As String
        Return "unnamed"
    End Function
End Interface

Structure Dog
    Implements INamed

    Public Shared Function DefaultName() As String Implements INamed.DefaultName
        Return "dog"
    End Function
End Structure

Structure Cat
    Implements INamed
End Structure

Function Describe(Of T As INamed)() As String
    Return T.DefaultName() ' "dog" for Dog, "unnamed" for Cat
End Function
```

- `Shared Overridable` before a `Function`, `Sub`, or `Property` in an interface declares a shared member with a default body, which C# 11 calls `static virtual`. `Shared` with `MustOverride` stays the abstract form, which has no body.
- The member is `Public`, as every interface member is, and its body follows the rules of a `Shared` member of a class.
- A class or structure that implements the interface may supply its own member with `Shared ... Implements INamed.DefaultName`, as slices 9f and 48a allow, or leave it out, as slice 47a allows.
- A call through a type parameter, `T.DefaultName()`, reaches the type argument's member, or the default body when the type does not supply one, as slices 42a and 47a do. As for `Shared MustOverride`, the member is reached through a type parameter, not through the interface's name.
- The modifiers may come in either order, as for any VB modifiers.

The member is emitted as C# 11 emits `static virtual`: a static, virtual method with a body.

Visual Basic 17.13 still reports BC30270.

## Rejected forms

- A `Shared Function` with a body in an interface is virtual. It would leave no form for a shared interface member that is not virtual, which C# 11 has.

## Not locked

Calling a shared virtual member through the interface's name is not covered by this note. Non-virtual shared members with a body, the plain `Shared` form this note keeps, are declared in slice 76a.
