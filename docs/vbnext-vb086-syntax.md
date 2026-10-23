# VB-086 Implementing interface operators

This note locks the form for slice 49b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Slices 41a and 48a let a VB type supply the shared abstract functions and properties of an interface with `Shared ... Implements`, and phase 43 calls interface operators through a type parameter. A VB type still cannot supply an interface operator, because a VB operator has no `Implements` clause:

```vb
Structure Money
    Implements IAdditionOperators(Of Money, Money, Money)

    Public Amount As Decimal

    ' BC37315: cannot implement the interface's +
    Public Shared Operator +(left As Money, right As Money) As Money
        Return New Money With {.Amount = left.Amount + right.Amount}
    End Operator
End Structure
```

## Locked form: implicit implementation by signature

An operator declared in a class or structure supplies an operator of an interface that the type implements, directly or through base interfaces, when both are the same operator and their parameter and return types match after the interface's type arguments are substituted. No new syntax is added, and the example above compiles.

- The same operator means the same metadata name, such as `op_Addition` for `+` or `op_Equality` for `=`. A `Widening` or `Narrowing` conversion operator supplies an interface's implicit or explicit conversion operator in the same way.
- One VB operator supplies that operator for every implemented interface that declares it with a matching signature, such as `+` for both `IAdditionOperators(Of Money, Money, Money)` and the copy that `INumber(Of Money)` inherits.
- The operator is emitted with a method implementation record for each interface operator it supplies, and is not virtual, as slice 41a emits shared implementing methods.
- The operator stays an ordinary operator of the type, so `a + b` on two `Money` values calls it directly.
- An interface operator that no declared operator matches is still reported with BC37315, unless it is a shared virtual operator with a default body, which slice 47a no longer requires.

VB already requires some operators in pairs, such as `=` with `<>` and `<` with `>`. Those rules are unchanged.

### Checked forms

A checked operator such as `op_CheckedAddition` is not declared in VB. The generic math interfaces declare the checked forms as shared virtual operators with a default body that calls the unchecked operator, so a VB type that supplies `+` gets checked addition through that default, as slice 47a allows.

### Operators VB cannot declare

Some interface operators have no VB operator form: `++` and `--`, which `INumber(Of T)` requires as `op_Increment` and `op_Decrement`, and `>>>`, which `IBinaryInteger(Of T)` requires as `op_UnsignedRightShift`. A type supplies these with the existing slice 9f form, a `Shared` function that implements the metadata name:

```vb
Public Shared Function op_Increment(value As Money) As Money Implements IIncrementOperators(Of Money).op_Increment
    Return New Money With {.Amount = value.Amount + 1}
End Function
```

Visual Basic 17.13 does not have the form.

### Sources

C# 11 implements an interface's static abstract operators with a matching operator, implicitly, or explicitly with the interface name. Visual Basic has no explicit form for operators, so matching by signature covers the common case without new syntax.

## Rejected forms

- An `Implements` clause on operators. Naming the interface operator, as in `Implements IAdditionOperators(Of Money, Money, Money).op_Addition`, would make source spell metadata names, and matching by signature already identifies the operator.
- Both forms together. The explicit form is only needed when signature matching is ambiguous, which the generic math interfaces do not cause.

## Not locked

Declaring a checked operator in VB, and `++`, `--`, or `>>>` operator syntax, are not covered by this note.
