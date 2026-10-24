# VB-104 Operators in interfaces

This note locks the form for slice 69b. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Phase 49 lets a VB type supply the operators of an interface that another assembly declares, and phase 43 calls them through a type parameter. Phases 60 and 68 let a VB interface declare shared abstract and shared virtual functions, subs, and properties. A VB interface still cannot declare an operator, so VB cannot write a generic math interface of its own:

```vb
Interface IAddable(Of T As IAddable(Of T))
    Shared MustOverride Operator +(left As T, right As T) As T ' BC30603 today
End Interface
```

## Locked form: `Shared MustOverride` and `Shared Overridable`

```vb
Interface IAddable(Of T As IAddable(Of T))
    Shared MustOverride Operator +(left As T, right As T) As T

    Shared Overridable Operator -(value As T) As T
        Return value
    End Operator
End Interface

Structure Money
    Implements IAddable(Of Money)

    Public Amount As Decimal

    Public Shared Operator +(left As Money, right As Money) As Money
        Return New Money With {.Amount = left.Amount + right.Amount}
    End Operator
End Structure

Function Sum(Of T As IAddable(Of T))(a As T, b As T) As T
    Return a + b
End Function
```

- `Shared MustOverride Operator` in an interface declares a shared abstract operator, which has no body and no `End Operator`. C# 11 calls it `static abstract`.
- `Shared Overridable Operator` in an interface declares a shared virtual operator with a default body through `End Operator`. C# 11 calls it `static virtual`.
- This is the spelling that `docs/vbnext-vb096-syntax.md` locks for functions, subs, and properties. The modifiers may come in either order.
- The operator is `Public`, as every interface member is, and follows the rules for operators in a class or structure: its parameters and return type must involve the containing interface or, for a generic interface, one of its type parameters that is constrained to it, as in `IAddable(Of T As IAddable(Of T))`, and operators that VB requires in pairs, such as `=` with `<>`, are still required in pairs.
- Unary operators, arithmetic, comparison, logical, shift, concatenation, `Like`, and `IsTrue` and `IsFalse` may be declared this way.
- A class or structure supplies the operator with a matching operator, by signature, as phase 49 locks, or leaves out a `Shared Overridable` one, as slice 47a allows. A type that leaves out a `Shared MustOverride` operator reports BC37315.
- An operator expression over a type parameter, `a + b` for `a As T`, calls the type argument's operator, or the default body, as phase 43 does.

The operator is emitted as C# 11 emits `static abstract` and `static virtual` operators: a static abstract method, or a static virtual method with a body, with the operator's metadata name, such as `op_Addition`.

Visual Basic 17.13 still reports BC30603.

## Rejected forms

- `Operator +(...)` without modifiers as the abstract form, and `Overridable Operator` as the virtual form. Operators in classes and structures are always written `Shared`, and phases 60 and 68 already spell shared interface members `Shared MustOverride` and `Shared Overridable`.

## Not locked

Conversion operators (`Widening Operator CType` and `Narrowing Operator CType`) in interfaces, which also need conversions to be looked up through a type parameter's constraints, are not covered by this note. Neither are operators that VB cannot declare at all, such as `++`, `--`, `>>>`, and checked operators.
