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

On November 26, 2026 the owner formally accepted LC-1F, including the conservative limitations below. That acceptance covers the nullable flow implementation. It does not complete VB-030. The same day the owner approved the LC-1G plan and authorized LC-1G.1 only. Public symbol APIs, framework compatibility, IDE and analyzer integration, diagnostic display, and nullable Edit and Continue remain outstanding in [vbnext-lc1g-integration-plan.md](vbnext-lc1g-integration-plan.md). LC-4 historical reconciliation stays deferred.

The accepted published baseline is `5c6c95fb7ae92a6abafa2628f610eecd7befe8a9`. The final LC-1F.6d implementation commit is `710bdbc87a5ce2a5d152af20d9001ec1e4acb877`. The technical review is the closeout report `LanguageCompletion/LC-1F.6d-TARGETED-FLOW-CORRECTNESS-CLOSEOUT-REPORT.md`, SHA-256 `65249FCC29849BD4EB9B79256CC2886A6558949269F2B327B11F2FB5698982D9`. That review recommended acceptance and did not itself record it. No missing warning remained in the fixtures assigned to that closeout.

On November 26, 2026 the owner formally accepted LC-1G.1, including its documented limitations, and authorized LC-1G.2 only. The accepted compiler baseline is `613f195758d5cdd19d932d244bf1254ee59b2e2b`. The technical review is `LanguageCompletion/LC-1G.1a-INTERNAL-SYMBOL-VERIFICATION-REPORT.md`, SHA-256 `3F6F37DD672DB0E04DF066F690AC982A23131A607F13CDB50B1F2A51FF5DFE7F`. The implementation evidence is `LanguageCompletion/LC-1G.1-INTERNAL-SYMBOL-INTEGRATION-REPORT.md`, SHA-256 `35ACDFC0F35DF86212E3D7DC8E0469C9D4E25B1D0B019F80690DB7C5BC5353DD`. That review recommended acceptance and did not itself record it. This paragraph records the acceptance. It does not complete VB-030 and it does not accept LC-1G.2.

The accepted internal behavior is: constructed and substituted annotations stay on the symbol that carries them; a reduced extension stores original-arity annotations and later constructions of one partial reduction do not overwrite each other; a class constraint captures a declaration annotation and does not by itself prove a non-null result; `As <NotNull> T` remains a normal-completion postcondition independent of the substituted annotation; retargeting forwards stored annotations; an event declaration captures nested type-argument annotations; `IEventSymbol.NullableAnnotation` and `IMethodSymbol.TypeArgumentNullableAnnotations` stay `None`; default symbol equality is unchanged. The documented limitations that remain are the LC-1F limitations below, the absence of `Class?`, and the absence of public nullable API exposure. The metadata behavior is accepted in the following paragraph. LC-1G.1 does not complete VB-030.

On November 27, 2026, at 02:30:28 UTC, the owner formally accepted LC-1G.2, including LC-1G.2a and its documented limitations, and authorized LC-1G.3 only. The accepted compiler baseline is `6b937cd7cbcb283d5abe4eeb8c492ccd0858b766`. The original implementation is `440c44bb68ae20091da0aab62ec5673e5e898a67`. The closeout implementation is the accepted baseline. The implementation evidence is `LanguageCompletion/LC-1G.2-NULLABLE-METADATA-INTEGRATION-REPORT.md`, SHA-256 `5E05D3B5D21DD41E17B278143594F8B480D8C3CAFC1F23A909EE596880E49445`. The technical review is `LanguageCompletion/LC-1G.2a-METADATA-CORRECTNESS-CLOSEOUT-REPORT.md`, SHA-256 `96E4741455A59BA95CCB0EF5F077B9F569F29594C604FB695CF5880FE948B984`. That review recommended acceptance and did not itself record it. This paragraph records the acceptance. It does not complete VB-030 and it does not accept LC-1G.3.

The accepted metadata behavior is: tuple element annotations, including long-tuple `Rest`, round-trip through standard `NullableAttribute` bytes; nested generic and array annotations are preserved; an event emits and decodes its constructed type without a public event annotation; an imported reference constraint keeps its annotation; a missing predefined `NullableAttribute` is embedded; a predefined `NullableAttribute` that exists but lacks either standard constructor reports `ERR_MissingRuntimeHelper` and is not replaced by a second definition; a netmodule declaration keeps a nested annotation. These limitations stay accepted:

- A predefined `NullableAttribute` with both standard constructors and an incorrect base type follows the existing inspected compiler-contract behavior.
- VB.Next does not currently emit `NullableContextAttribute`.
- An unrecognized malformed attribute blob may leave a valid enclosing nullable context in force.
- A recognized unusable nullable transform recovers under the established decoder policy.
- Public nullable symbol API integration remains incomplete.
- `NullablePublicOnly` retains its deferred disposition.

On November 27, 2026, at 02:59:36 UTC, the owner formally accepted LC-1G.3, including its documented limitations, and authorized LC-1G.4 implementation only. The accepted verification baseline is `1a8941af87fb9c48025566550132697547e81b78`. LC-1G.3 changed tests and documentation only. It did not modify production compiler code. The technical review is `LanguageCompletion/LC-1G.3-FRAMEWORK-REFLECTION-COMPATIBILITY-REPORT.md`, 27251 bytes, SHA-256 `51C43C7C887CA47297C594CC08A4578EC6BEBD8A093756AFA23F38046789D912`. That review recommended acceptance and did not itself record it. This paragraph records the acceptance. It does not complete VB-030 and it does not accept LC-1G.4.

The seven framework obligations are accepted with these dispositions. Exact modern .NET nullable attribute behavior passed for the existing .NET 10 emit references and for exact .NET 11. Exact .NET Framework 4.7 attribute compilation and reference behavior passed for compilation, diagnostics, metadata, import, and IL. Compiler-generated attributes are embedded only where the target does not already supply a compatible definition: .NET Framework 4.7 and 4.7.2 embed one `NullableAttribute`, and .NET 11 uses the predefined type. The previously recorded .NET Framework 4.7.2 `NullabilityInfoContext` test-project failure was a test-architecture correction. Those facts compile only for the modern host, and the net472 emit-test build completed with zero errors. Reflection interpretation passed on the .NET 11 runtime with `NullabilityInfoContext` and on the net10.0 host through the existing reflection facts. Analysis does not require a modern framework API, and the portable compiler does not reference `NullabilityInfoContext`. Older-target fallback passed, including the exact .NET 11 reference and runtime check.

Exact .NET Framework 4.7 verification used `microsoft.netframework.referenceassemblies.net47` 1.0.3. Exact .NET 11 verification used the installed `Microsoft.NETCore.App.Ref` 11.0.0 reference pack, whose `System.Runtime` identity is 11.0.0.0, and ran on `Microsoft.NETCore.App` 11.0.0. Existing .NET 10 reflection verification remains the net10.0 host facts. The successful net472 command is `dotnet build src/Compilers/VisualBasic/Test/Emit/Microsoft.CodeAnalysis.VisualBasic.Emit.UnitTests.vbproj -c Release -f net472 --nologo --no-restore`.

These LC-1G.3 limitations stay accepted:

- Exact .NET Framework 4.7 CLR execution was not performed.
- Exact 4.7 reference compilation is not equivalent to exact historical runtime execution.
- The 4.7 reference pack does not contain `System.ValueTuple`.
- No additional tuple dependency was introduced.
- `NullabilityInfoContext` is unavailable on the older framework targets.
- Older-target metadata inspection used a metadata reader.
- Individual older-target flow-attribute fixtures were not all repeated.
- A separate exact .NET 11 C# consumer matrix was not repeated.
- The historical prebuilt net11 image read against net10 references remains distinct from the exact .NET 11 verification.
- One intentionally stopped additional scan produced no result.

Supplemental owner-provided evidence is a string presence check, not a complete assembly API inventory. The .NET 11 `System.Runtime` image contains the v11.0 target identity, `NullableAttribute`, and `NullabilityInfoContext`. The .NET 10 `System.Runtime` image contains the v10.0 identity and the same two type names. Neither scanned net47 nor net472 `mscorlib` contains those strings. The net472 emit-test build completed with zero errors.

LC-1G.5 through LC-1G.7 remain unauthorized. LC-1G.4 is authorized for implementation and is not owner-accepted. Implementation stopped before a production change: a shared compiler type cannot carry one annotated use, and a public projection was not introduced. The evidence is `LanguageCompletion/LC-1G.4-PUBLIC-NULLABLE-API-INTEGRATION-REPORT.md`. That report does not accept LC-1G.4.

On November 27, 2026, at 03:20:43 UTC, the owner approved Option A: an immutable public annotation projection outside the compiler symbol hierarchy. The architecture-stop baseline is `669b63edb3fc898558d7564b0af2fb07aae16089`. A compiler `TypeSymbol` subclass is not the selected approach. The projection carries an immutable annotation, preserves the specialized public interface of the underlying type, unwraps to the compiler symbol on re-entry, and preserves applicable annotations when a public operation needs them. Default equality and hash behavior stay on CLR identity. This authorizes LC-1G.4a, the public annotation projection architecture proof, only. LC-1G.4b is public symbol API integration and was not authorized by that decision. LC-1G.4c is semantic-model nullability and consolidated public API verification and was not authorized by that decision. Those names subdivide the existing LC-1G.4 phase. They are not new language features. LC-1G.4 is not complete. Semantic-model flow integration is not part of LC-1G.4a.

LC-1G.4a implemented that projection as `PublicTypeProjection` in `src/Compilers/VisualBasic/Portable/Symbols/PublicTypeProjection.vb`. It is not a `TypeSymbol`. `WithNullableAnnotation` returns the projection for `Annotated` and `NotAnnotated`, and returns the existing compiler symbol for `None`. Named types, error types, arrays, and type parameters keep `INamedTypeSymbol`, `IErrorTypeSymbol`, `IArrayTypeSymbol`, and `ITypeParameterSymbol`. `EnsureVbSymbolOrNothing` unwraps a projection before the existing cast. Public `ISymbol.Equals` recognizes a projection. `SymbolEqualityComparer.Default` ignores reference nullable annotations. `SymbolEqualityComparer.IncludeNullability` observes the projection annotation and stored nested type-argument and array-element annotations. Internal `TypeSymbol.Equals` is unchanged. Public named-type `Construct` records annotations on a newly constructed type and does not stamp the shared definition. LC-1G.4 is not complete. The evidence is `LanguageCompletion/LC-1G.4a-PUBLIC-PROJECTION-ARCHITECTURE-PROOF-REPORT.md`.

On November 27, 2026, at 04:04:57 UTC, the owner formally accepted LC-1G.4a, including its documented limitations. The accepted implementation is `58b0360cc9cbde3e1af0ecc3d8e2f71db27bd8bf`. The accepted architecture is the immutable public annotation projection outside the compiler symbol hierarchy. Accepted limitations include the absence of separate fixtures for tuples, unbound generics, and anonymous types; pointer-like metadata remaining an error type; `VisualBasicSymbolVisitor` not accepting a projection; repeated `WithNullableAnnotation` allocating; and identity construction not stamping a shared definition. This acceptance does not accept LC-1G.4 or the rest of the public nullable API surface. The owner authorized LC-1G.4b only. LC-1G.4c, LC-1G.5 through LC-1G.7, LC-4, and `NullablePublicOnly` remain unauthorized. The report is 34843 bytes, SHA-256 `38CA2A44931193B6FDE3A2037B9185935B0322CEFA86998DCF3090FBCCDAD4CF`.

On November 27, 2026, at 13:55:57 UTC, the owner formally accepted LC-1G.4b.1, including its documented proof limitations, and permanently approved Option A as the public annotation projection architecture for LC-1G.4b. The accepted evidence is `LanguageCompletion/LC-1G.4b.1-IDENTITY-CONSTRUCTION-ARCHITECTURE-RESOLUTION-REPORT.md`, 39230 bytes, SHA-256 `E551DDD5CDFD7BDF55B16D871FE79239F80C87A224C0F0944AC76F78BC3371E1`. Accepted proof limitations include member views for fields, properties, methods, and parameters, with events and unrecognized members still forwarded; per-call member allocation; and the method view remaining a bounded representation at the time of that proof. This acceptance authorizes LC-1G.4b.2 only. It does not accept LC-1G.4b or LC-1G.4. LC-1G.4c, LC-1G.5 through LC-1G.7, LC-4, and `NullablePublicOnly` remain unauthorized.

LC-1G.4b.2 completed the public symbol API on Option A. The implementation commit is `39bf619009098ea2f760d95c8b2e8d020ed66c5e`. The reduced-receiver correction is `9731dc6e58353cfd5ecc75358a4bba41e860de16`. Named-type and method identity construction, member and event projection, reduced-extension public argument mapping, anonymous member annotations, arrays, tuples, containing types, imported constraints, retargeted public symbols, and compiler re-entry preserve the requested annotations. Default equality, `IncludeNullability`, and hash behavior stay on the proved contracts. An unadorned Visual Basic type parameter remains `NullableAnnotation.None`. An event's top-level annotation remains `None`; annotations inside its delegate type are preserved. `Nullable(Of T)` is not a nullable reference annotation. Public `TypeInfo` nullability remains LC-1G.4c. LC-1G.4b is technically ready for formal owner acceptance and is not owner-accepted. LC-1G.4 is not complete. LC-1G.4c, LC-1G.5 through LC-1G.7, LC-4, and `NullablePublicOnly` remain unauthorized. The evidence is `LanguageCompletion/LC-1G.4b.2-PUBLIC-SYMBOL-API-INTEGRATION-CLOSEOUT-REPORT.md`.

On November 27, 2026, at 15:41:31 UTC, the owner formally accepted LC-1G.4b, including LC-1G.4b.2, LC-1G.4b.3, and their documented limitations, on published baseline `46674f536be83dc47ca00c66b1c3dae9ae07e4d3`. The accepted verification report is `LanguageCompletion/LC-1G.4b.3-FINAL-PUBLISHED-BASELINE-VERIFICATION-REPORT.md`, 41582 bytes, SHA-256 `50D9EE177D11F52D5C3C81DDE6EA3EB96857BB8344D8A618755DCD820CF4BF22`. Option A remains the approved immutable public annotation projection. This acceptance authorizes LC-1G.4c only. It does not accept LC-1G.4c or LC-1G.4. LC-1G.5 through LC-1G.7, LC-4, and `NullablePublicOnly` remain unauthorized. Both LC-1G.2a observations remain open for LC-1G.7. The LC-1G.4b.3 performance observations stay recorded for later verification and are not an optimization task.

LC-1G.4c is implemented and technically verified. It is not owner-accepted. `GetTypeInfo` reports expression nullability from the existing nullable-flow walker. The member semantic model captures that result for its own bound method block, does not publish diagnostics or constructor facts, and does not introduce a second walker. `VisualBasicTypeInfo` supplies the original and converted `NullabilityInfo`. The public type annotation is the flow state applied with `WithNullableAnnotation`, reusing the approved projection. A standalone speculative expression remains `NullableFlowState.None` because that API does not have the enclosing method body. The historical matrix on this implementation is 1,654 passed, 0 failed, and 35 skipped. Twenty-one new semantic-model tests passed. LC-1G.4 is technically complete and is awaiting owner acceptance of LC-1G.4c. LC-1G.5 through LC-1G.7, LC-4, and `NullablePublicOnly` remain unauthorized.

The approved sequence is in [vbnext-lc1g-integration-plan.md](vbnext-lc1g-integration-plan.md): LC-1G.4 is public Roslyn nullable APIs, LC-1G.5 is IDE, analyzer, and diagnostic integration, LC-1G.6 is Edit and Continue, and LC-1G.7 is consolidated acceptance. `NullablePublicOnly` is not one of those slices. Two observations from the LC-1G.2a regression stay open for LC-1G.7 and are not confirmed defects: `Substitution_EveryConstructedSymbolKeepsItsArgument` once reported an unexpected BC42512 on `methodText` under a parallel run and then passed in isolation, with `xUnit.MaxParallelThreads=1`, and in the recorded default run; the first full nullable filter after the missing-constructor diagnostic aborted with a test-host crash and no dump, and the crash cause was not established. The evidence is `LanguageCompletion/LC-1G.2a-METADATA-CORRECTNESS-CLOSEOUT-REPORT.md`, section 16.

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
