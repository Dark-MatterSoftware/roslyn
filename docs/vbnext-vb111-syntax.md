# VB-111 Conversion operators in interfaces

This note locks the form for slice 79b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

`docs/vbnext-vb104-syntax.md` locks `Shared MustOverride Operator` and `Shared Overridable Operator` for interface operators, and leaves conversion operators out. A VB interface still cannot declare one, and a conversion to or from a type parameter does not look in the type parameter's constraints for conversion operators:

```vb
Interface IFromInteger(Of T As IFromInteger(Of T))
    Shared MustOverride Widening Operator CType(value As Integer) As T ' BC33013 today
End Interface
```

## Locked form: the phase 69 spelling with `CType`

```vb
Interface IFromInteger(Of T As IFromInteger(Of T))
    Shared MustOverride Widening Operator CType(value As Integer) As T

    Shared Overridable Narrowing Operator CType(value As T) As Integer
        Return 0
    End Operator
End Interface

Structure Meters
    Implements IFromInteger(Of Meters)
    Public Value As Integer

    Public Shared Widening Operator CType(value As Integer) As Meters
        Return New Meters With {.Value = value}
    End Operator
End Structure

Function Make(Of T As IFromInteger(Of T))(n As Integer) As T
    Dim result As T = n          ' the Widening operator applies implicitly
    Return result
End Function

Function Back(Of T As IFromInteger(Of T))(value As T) As Integer
    Return CType(value, Integer) ' the Narrowing operator, or its default body
End Function
```

- `Shared MustOverride Widening Operator CType` and `Shared MustOverride Narrowing Operator CType` in an interface declare shared abstract conversions, with no body. `Shared Overridable` with `End Operator` declares shared virtual conversions with a default body. This is the spelling that `docs/vbnext-vb104-syntax.md` locks for other operators. C# 11 calls these `static abstract` and `static virtual` implicit and explicit operators.
- The rules for conversion operators in a class or structure apply: the source or target type must be the interface's type parameter that is constrained to the interface, as in `IFromInteger(Of T As IFromInteger(Of T))`, and a conversion cannot be declared both `Widening` and `Narrowing` between the same types.
- A class or structure supplies the conversion with a matching `Widening` or `Narrowing` `CType` operator, by signature, as phase 49 locks for other operators, or leaves out a `Shared Overridable` one.
- A conversion between a type parameter and another type also considers the conversion operators that the type parameter's interface constraints declare, classified as VB classifies a class's conversion operators: a `Widening` operator applies implicitly, and a `Narrowing` operator applies with `CType`, `CInt` and the other conversion functions, or implicitly under `Option Strict Off`. The call is constrained to the type parameter, so it reaches the type argument's operator or the default body, as phase 43 does for other operators.
- `DirectCast` and `TryCast` do not use conversion operators, as for classes.

A conversion is emitted as C# 11 emits interface conversion operators: a static abstract or static virtual `op_Implicit` or `op_Explicit` method.

Visual Basic 17.13 still reports BC30603 and BC33013.

## Rejected forms

- Only explicit conversions through a type parameter. A `Widening` operator is implicit everywhere else in VB, and C# 11 applies an interface's implicit conversions to its type parameters.

## Not locked

Conversions declared by a type parameter's class constraint, and lifting interface conversions to `Nullable`, are not covered by this note.
