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

Inferred method type arguments keep the existing inferred CLR types. A nullable annotation is added from the arguments that contributed to each method type parameter. An annotated contribution wins over a non-annotated one. An oblivious contribution does not become a non-null guarantee. A non-null flow state on an argument does not by itself prove that an inferred `T` result is non-null, because a method that returns `T` is not known to be an identity. Declared `NotNull` and `NotNullIfNotNull` postconditions still apply. A reduced extension call uses the same contribution rules for the receiver: the CLR type fixed during reduction stays as it is, and the receiver's annotation is applied to the method type parameter that reduction inferred from that receiver, including a nested type argument. A type parameter of the containing type is not treated as a method type parameter. `value.Echo(Of String?)()` remains BC36907 when reduction has already fixed every type parameter, because the reduced method has no free type parameters. The unreduced call `Echo(Of String?)(value)` still carries the explicit annotation.

An ordinary caller uses the method's declared nullable contract, whether the callee was compiled from source or imported from metadata. The callee's body is checked on its own. A method that is inside a recursive cycle does not treat that cycle's declared contracts as proof that its own body meets them. A caller outside the cycle still uses the declared contract. Conservative warnings inside a recursive body remain.

`On Error` and `Resume` keep their existing runtime behavior. While a handler that can resume is active, a local, field, or property the handler may write with a nullable value is not treated as proven non-null. That includes an assignment of `Nothing`, of a nullable local or call, a `ByRef` argument, a write through another variable that names the same field, and a call that receives the object. A write of a literal or a `New` expression does not invalidate the target. The same name match used when the bound local cannot be identified also invalidates another local of that name in the method; Visual Basic does not allow two locals of one method to share a name, and a lambda is a separate method. A non-null reassignment or a null check later in the method does not remove that handler fact. That is a conservative false positive, not a missing warning. A reference result reached under `On Error Resume Next`, or under a handler that resumes, does not consume a normal-completion `NotNull` postcondition. `On Error GoTo 0` ends that exceptional continuation. `On Error Resume Next` does not, by itself, discard every nullable fact in the method. A `Resume` does not fall through into the statements written after it. Nullable analysis is not disabled for the whole procedure.

When `MaybeNull` and `NotNull` are both declared on the same output, `NotNull` wins, in either attribute order. The metadata attributes are unchanged. A valid `NotNull` on a return type, including `As <NotNull> T`, is a postcondition of normal completion. The substituted type may still be nullable. An ordinary caller may rely on that postcondition. A body that returns `Nothing` is still diagnosed.

## Owner acceptance

On November 26, 2026 the owner formally accepted LC-1F, including the conservative limitations below. That acceptance covers the nullable flow implementation. It does not complete VB-030. The same day the owner approved the LC-1G plan and authorized LC-1G.1 only. LC-1G.1 internal symbol integration is implemented. LC-1G.1a verified that foundation. Owner acceptance is still pending. LC-1G.2 through LC-1G.7 remain unauthorized. Public symbol APIs, metadata emission and import, framework compatibility, IDE and analyzer integration, diagnostic display, and nullable Edit and Continue remain outstanding in [vbnext-lc1g-integration-plan.md](vbnext-lc1g-integration-plan.md). LC-4 historical reconciliation stays deferred.

The accepted published baseline is `5c6c95fb7ae92a6abafa2628f610eecd7befe8a9`. The final LC-1F.6d implementation commit is `710bdbc87a5ce2a5d152af20d9001ec1e4acb877`. The technical review is the closeout report `LanguageCompletion/LC-1F.6d-TARGETED-FLOW-CORRECTNESS-CLOSEOUT-REPORT.md`, SHA-256 `65249FCC29849BD4EB9B79256CC2886A6558949269F2B327B11F2FB5698982D9`. That review recommended acceptance and did not itself record it. No missing warning remained in the fixtures assigned to that closeout.

These limitations stay accepted. A later missing warning is not one of them.

- A handler taint stays after a later non-null reassignment, and it stays inside a later null check.
- Handler recovery still falls back to matching a local by name when the bound symbols are not the same object.
- Recursive base-case inference is incomplete.
- Member initialization through an alias is incomplete.
- Nested-condition diagnostics stay conservative.
- A custom setter invalidates a member fact.
- A possible alias invalidates a member fact.
- Loop widening drops original-input distinction and value carriers.
- `DoesNotReturnIf` body verification is absent.
- Accessor-only `MaybeNull` and `NotNull` attributes are not flow annotations.
- `value.Echo(Of String?)()` remains BC36907 when reduction has already fixed every type parameter. That is existing Visual Basic behavior, not an accepted nullable defect.
