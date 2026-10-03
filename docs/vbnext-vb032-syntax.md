# VB-032 Primary constructors

This note locks the spelling for slices 6e through 6g. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

A primary constructor is a parameter list on a `Class` or, in 6g, a `Structure`. The parameters are in scope in instance initializers. They are not properties. A parameter becomes a stored value only when an initializer uses it.

## Parameter list

```vb
Class Customer(name As String, id As Integer)
    Public ReadOnly Property Name As String = name
    Public ReadOnly Property Id As Integer = id
End Class
```

The list follows the type name. `(Of T)` remains the type-parameter list. When a type has both, the type parameters come first:

```vb
Class Box(Of T)(value As T)
    Public ReadOnly Property Value As T = value
End Class
```

A `(` that is not followed by `Of` starts the primary constructor. Visual Basic 17.13 still rejects that list.

## 6e. Instance initializers

An instance field initializer and an instance auto-property initializer may name a primary constructor parameter. The parameter is evaluated from the constructor argument. Shared initializers cannot use it.

## 6f. Base or this chain

```vb
Class Derived(name As String)
    Inherits Base(name)
End Class
```

`Inherits Base(arguments)` passes those arguments to the base constructor. `Inherits Base` with no argument list stays the existing form. A constructor that chains to another constructor of the same type uses the existing `MyClass` or `Me` call; that call may name a primary constructor parameter.

## 6g. Structure

A `Structure` uses the same parameter list and the same initializer scope. Structures do not use `Inherits`.
