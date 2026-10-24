# VB-105 Read-only structure members

This note locks the form for slice 70b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

`ReadOnly Structure` already declares a structure whose members cannot change it. A structure that is not read-only as a whole cannot mark one member as not changing it, so calling that member on a `ReadOnly` field or a `ByRef ReadOnly` parameter works on a copy:

```vb
Structure Point2
    Public X As Integer
    Public Y As Integer

    ReadOnly Function Length() As Integer ' BC30242 today
        Return X + Y
    End Function
End Structure
```

## Locked form: `ReadOnly` on methods and on `Get`

```vb
Structure Point2
    Public X As Integer
    Public Y As Integer

    ReadOnly Function Length() As Integer
        Return X + Y
    End Function

    ReadOnly Sub Print()
        System.Console.WriteLine(X & ", " & Y)
    End Sub

    Property Scale As Integer
        ReadOnly Get
            Return X * 10
        End Get
        Set(value As Integer)
            X = value \ 10
        End Set
    End Property

    Property Name As String ' auto-implemented: its Get is read-only
End Structure
```

- `ReadOnly` before `Function` or `Sub` in a structure declares a read-only member, which C# 8 writes `readonly`. It cannot assign a field of `Me`, assign `Me`, pass a field of `Me` `ByRef` except to a `ByRef ReadOnly` parameter, or call a member of `Me` that is not read-only without working on a copy, as members of a `ReadOnly Structure` already behave.
- `ReadOnly` before `Get` inside a property block in a structure declares a read-only getter. `ReadOnly Property` keeps its meaning, a property without a setter. The two may be combined: `ReadOnly Property X As Integer` with a `ReadOnly Get`.
- The `Get` of an auto-implemented property in a structure is read-only without being marked, as C# makes auto-implemented getters, because it only reads the backing field.
- A call to a read-only member on a `ReadOnly` field, a `ByRef ReadOnly` parameter, or a `Dim ByRef ReadOnly` local does not copy the structure. A call to a member that is not read-only still works on a copy.
- `ReadOnly` on a member of a class, module, or interface, on a `Shared` member, or on `Set`, reports an error. On a member of a `ReadOnly Structure` it is allowed and changes nothing.

A read-only member is emitted as C# 8 emits `readonly` members: the method or accessor carries `IsReadOnlyAttribute`, so C# calls it without a defensive copy.

Visual Basic 17.13 still reports BC30242.

## Rejected forms

- `ReadOnly Property` meaning a read-only member. It already means a property without a setter, and changing it would change existing programs.
- Marking getters read-only by looking at what their bodies do. A body that happens not to write fields today could change later, and the promise to callers would change silently. Only auto-implemented getters, whose bodies the compiler writes, are read-only without being marked.

## Not locked

Read-only events, read-only `Set` accessors, and read-only operators are not covered by this note.
