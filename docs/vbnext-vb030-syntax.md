# VB-030 Nullable reference annotations

This note locks the spelling for slices 10b through 10e. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

The annotation is `?` after a type. On a value type, `?` remains `Nullable(Of T)`.

## Annotation

```vb
Dim name As String? = Nothing

Function Find(id As Integer) As Customer?
End Function

Sub Save(customer As Customer?)
End Sub
```

`String?` is still `String`. It is not `Nullable(Of String)`. The `?` records that the value may be `Nothing`.

`String?()` is an array of nullable strings, in the same position as `Integer?()`. `List(Of String?)` is a list of nullable strings. `List(Of String)?` is a list that may itself be `Nothing`.

`Nothing` stays the null literal. `Is Nothing` and `IsNot Nothing` stay the null tests.

## Value types

```vb
Dim count As Integer? = Nothing
```

`Integer?` is `Nullable(Of Integer)`. That meaning does not change when the project switch is on or off. A type parameter constrained to `Structure` follows the same rule: `T?` is `Nullable(Of T)`.

## Project switch

The compiler switch is `/nullable:` followed by one of these words:

| Value | Annotations | Warnings |
| --- | --- | --- |
| `disable` | off | off |
| `annotations` | on | off |
| `warnings` | off | on |
| `enable` | on | on |

`disable` is the default, including on `vbnext`. The MSBuild property is `Nullable`, with the same four words.

The switch has no effect unless the language version is `vbnext`. `default` and `latest` stay on Visual Basic 17.13.

A source directive `#Nullable Enable`, `#Nullable Disable`, `#Nullable Warnings`, `#Nullable Annotations`, or `#Nullable Restore` overrides the project switch for the rest of the file. `Restore` returns to the project switch. Slice 10c parses the directive and warns. Slice 10b stored annotations from the project switch only.

## Annotations off

`String?` keeps today's error: the type does not satisfy the structure constraint of `Nullable(Of T)`. Reference types stay oblivious. Their nullable annotation is `None`.

## Annotations on

A reference type written without `?` is not annotated (`NotAnnotated`). A reference type written with `?` is annotated (`Annotated`). No warning is reported for either form in slice 10b.

An unconstrained type parameter written with `?` stays an error. A type parameter constrained to `Class`, written with `?`, is an annotation of that type parameter. Storing that annotation is slice 10e.

`!` remains the dictionary operator. This note does not add a null-forgiving postfix.

## What each slice stores

Slice 10b stores the annotation on locals, parameters, and return types. It does not warn, and it does not change fields or properties.

Slice 10c warns from flow analysis on the experimental version.

Slice 10d emits the nullable metadata attributes.

Slice 10e stores annotations on generics and on overridden signatures.

## Flow policies approved on November 26, 2026

These decisions apply when the language version is `vbnext` and nullable warnings are enabled. `default` and `latest` stay on Visual Basic 17.13. They do not change emitted IL, runtime method identity, or overload ranking.

Inferred method type arguments keep the existing inferred CLR types. A nullable annotation is added from the arguments that contributed to each method type parameter. An annotated contribution wins over a non-annotated one. An oblivious contribution does not become a non-null guarantee. A nullable local that flow already knows is not null does not warn at the inferred result. An inferred extension receiver is not one of the reduced method's inference arguments, so that result stays oblivious unless the type arguments are written explicitly.

An ordinary caller uses the method's declared nullable contract, whether the callee was compiled from source or imported from metadata. The callee's body is checked on its own. A method that is inside a recursive cycle does not treat that cycle's declared contracts as proof that its own body meets them. A caller outside the cycle still uses the declared contract. Conservative warnings inside a recursive body remain.

`On Error` and `Resume` keep their existing runtime behavior. While a handler is active, a local that a handler assigns from `Nothing` is not treated as proven non-null, including on the statement a `Resume Next` can reach after `Error`. `On Error Resume Next` does not, by itself, discard every nullable fact in the method. A `Resume` does not fall through into the statements written after it.

When `MaybeNull` and `NotNull` are both declared on the same output, `NotNull` wins, in either attribute order. The metadata attributes are unchanged. A valid `NotNull` on a return type, including `As <NotNull> T`, is a postcondition of normal completion. The substituted type may still be nullable. An ordinary caller may rely on that postcondition. A body that returns `Nothing` is still diagnosed.
