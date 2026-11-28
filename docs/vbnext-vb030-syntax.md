# VB-030 Nullable reference annotations

This note locks the spelling for slices 10b through 10e and records the later owner decisions that extend it. The current normative description is [Current accepted behavior](#current-accepted-behavior). Earlier sections and the owner-acceptance chronology keep the order in which decisions were made. A later decision supersedes an earlier sentence. Formal closure of the approved feature is recorded in [Formal closure](#formal-closure). That closure does not complete VB.Next 1.0.

It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft. The mission remains [vbnext-mission.md](vbnext-mission.md).

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

An unconstrained type parameter written with `?` stays an error in this original slice. That sentence is superseded by owner decision NRT-OD-1, accepted for LC-1D: on `vbnext`, with annotations enabled, unconstrained `T?` is a nullable annotation of `T` and is not `Nullable(Of T)`. A type parameter constrained to `Class`, written with `?`, is an annotation of that type parameter. Storing that annotation is slice 10e. A type parameter constrained to `Structure` remains `Nullable(Of T)` in either case.

`!` remains the dictionary operator. This note does not add a null-forgiving postfix.

## What each slice stores

Slice 10b stores the annotation on locals, parameters, and return types. It does not warn, and it does not change fields or properties.

Slice 10c warns from flow analysis on the experimental version.

Slice 10d emits the nullable metadata attributes.

Slice 10e stores annotations on generics and on overridden signatures.

Those four sentences describe the slices as they were locked. LC-1D later stores annotations on fields, properties, delegate returns, unconstrained `T?`, and nullable array references. LC-1B later emits a per-site `NullableAttribute` and does not emit `NullableContextAttribute`. The current rules are in [Current accepted behavior](#current-accepted-behavior). The slice rows in [vbnext-phases.md](vbnext-phases.md) keep the historical slice descriptions.

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

LC-1G.4c.1 found that an explicit lambda parameter written `As String?` still bound as `Nullable(Of String)`, reported BC33101, and exposed annotation `None` and flow `NotNull`. That binding originated in LC-1D and was not an accepted language decision. LC-1G.4c.2 stores the declaration annotation on the lambda parameter. `As String?` is `String` with annotation `Annotated`, initial flow `MaybeNull`, and an unchecked use warns BC42512. An inferred parameter receives the delegate parameter annotation. An explicit parameter keeps the annotation written on it. `Integer?` remains `Nullable(Of Integer)`. Internal `Unknown` on a reference remains public `MaybeNull` and still does not warn.

LC-1G.4c.3 demonstrated that the lambda parameter kept its declaration annotation while the synthesized anonymous delegate Invoke parameter reported `None`. A nonnullable inferred delegate therefore did not warn BC42513. LC-1G.4c.4 carries that existing annotation through anonymous delegate construction. `As String?` is Annotated, `As String` is NotAnnotated, and `As Integer?` stays oblivious. A multiline lambda return uses the slice 10b declaration path: `As String?` is a nullable reference, and `As Integer?` remains `Nullable(Of Integer)`. The existing flow walker reads that return annotation. Default equality, hashing, CLR identity, and the emitted delegate template stay shared. The shared template cannot encode two conflicting nullable attributes; a named delegate still emits `NullableAttribute`, and the inaccessible anonymous template does not. `CreateArrayTypeSymbol` defaults an omitted element annotation to `None`. An explicit argument preserves it. On November 27, 2026, the owner formally accepted LC-1G.4c, including LC-1G.4c.1 through LC-1G.4c.4 and their documented limitations, on production commit `7adc9912ca072ef937cd1e6d040ac2cceb76a2aa`. The acceptance documentation baseline is `58ef71ebf9b15ac18b3838a76e9f429c1e032acb`. LC-1G.4, including the accepted LC-1G.4a projection and LC-1G.4b public symbol integration, is formally closed. The accepted compiler is Release net10.0, 6,551,552 bytes, SHA-256 `0C464DDDDC3B96A53B082B14359D652EA9A02E03DED16ACFEE95F70DF14B149E`, written 2026-11-27 17:44:22 UTC. Public API obligations 1 through 12 are accepted. LC-1G is not complete. A single-line lambda still does not bind its `As` clause. That pre-existing gap is a future consistency review and is not an authorized implementation. Body-inferred returns stay `None`. Initializer expressions do not receive method-body flow. Standalone speculation stays flow `None`. The shared anonymous template still cannot carry contradictory nullable attributes. Mid-capture cancellation remains an accepted verification limitation. On November 27, 2026, the owner formally accepted LC-1G.5, including its six tooling obligations, documented semantic limitations, and bounded verification limitations. The accepted production commit is `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`. The accepted Release net10.0 compiler is 6,553,600 bytes, SHA-256 `A1E4AEB0DE2C91EA98F12A62A6D2B4CD11E8832B0A1723309F8BDC62B1397E44`, written 2026-11-27 18:54:18 UTC. A `vbnext` compilation with annotations enabled shows a nullable reference as `String?` in symbol display, Quick Info, and generated signatures. Visual Basic 17.13 text does not gain that marker. Completion, rename, and Signature Help suites were not executed. The existing compiler remains the semantic authority, and the existing nullable-flow analysis remains the only flow authority. Three intermittent observations remain open for LC-1G.7 and are not resolved by this acceptance: the LC-1G.2a substitution BC42512, the unexplained test-host crash, and the one-time LC-1G.5 BC42511 in `ExactNet47_FixtureSuppliedNotNullIsRecognized`. This acceptance authorizes LC-1G.6 implementation and verification only. LC-1G.7, LC-4, and `NullablePublicOnly` remain unauthorized. The evidence is `LanguageCompletion/LC-1G.5-IDE-ANALYZER-AND-DIAGNOSTIC-INTEGRATION-REPORT.md`, 35,189 bytes, 75 sections, SHA-256 `0675A69AA2A468BF4A9C27920C49182E46FA20CB017742AF52594BD7B14DE39E`. On November 27, 2026, the owner formally accepted LC-1G.6, including its nine authoritative obligations, the permanent tests at `ac0c1565c326b94de3322e43695c282293b12e48`, and the documented verification limitations. No production correction was required. The production sources remain `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`. The accepted verification compiler is the Release `net10.0` assembly, 6,553,600 bytes, written 2026-11-27 19:54:48 UTC, SHA-256 `EFDEA4D051DF0E91215A13236B9031ACCD694A74B7713BD1544F37634A4E4EA7`, rebuilt from those unchanged sources. It is distinct from the historical LC-1G.5 artifact `A1E4AEB0DE2C91EA98F12A62A6D2B4CD11E8832B0A1723309F8BDC62B1397E44`. Accepted verification limitations: live Hot Reload was not executed; the Visual Studio debugger was not executed; metadata deltas were not applied to a running process; dedicated nullable multi-generation verification was not executed; local-slot bytes were not separately inspected; nullable-specific async and iterator fixtures were not added; nullable-specific active-statement fixtures were not added; separate nested-generic EnC fixtures were not added; and the C# EnC regression suites were not repeated because shared production infrastructure was unchanged. Three intermittent observations remain unresolved for the authorized LC-1G.7 review: the LC-1G.2a substitution BC42512, the unexplained test-host crash, and the one-time LC-1G.5 BC42511 in `ExactNet47_FixtureSuppliedNotNullIsRecognized`. This acceptance authorizes LC-1G.7 consolidated integration verification and narrowly scoped corrections to demonstrated defects within the previously accepted nullable implementation. It does not accept LC-1G.7, LC-1G as a whole, or VB-030. The evidence is `LanguageCompletion/LC-1G.6-NULLABLE-EDIT-AND-CONTINUE-INTEGRATION-REPORT.md`, 31,374 bytes, SHA-256 `EAA08825F65C810A9AC256DD2A2D8FC55285B0AA5556018DD8D2C6A893BB61D7`. On November 27, 2026, the owner formally accepted LC-1G.7 and formally closed LC-1G, including the consolidated inventory of 48 obligations and the nine Edit and Continue obligations represented by their existing inventory row. No production correction was required. The permanent cross-language tests are `c44f1db3be20a6ea65a6ffbdbb7400f0a5ddc216`. The accepted compiler is the Release `net10.0` assembly, 6,553,600 bytes, written 2026-11-27 20:38:02 UTC, SHA-256 `3DBDC692E7D1F8FA3E0EBBF6643561E04A8FD0042285938FA6E33E75A952AB70`, rebuilt from unchanged production sources `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`. The accepted nine-group baseline is 1,659 passed, 0 failed, and 35 skipped, including the final nullable filter of 518 passed and 6 skipped. The owner accepts the three residual intermittent risks. Their root causes remain unknown, and none is recorded as resolved. `Substitution_EveryConstructedSymbolKeepsItsArgument` is not selected by `FullyQualifiedName~Nullable`. Previously accepted semantic and verification limitations remain accepted, including the limitation that a separate representative analyzer allocation benchmark was not reported. VB-030 is not complete. At that closure, LC-4, `NullablePublicOnly`, the deferred single-line lambda `As` clause, and subsequent slices remained unauthorized. Later, on November 27, 2026, the owner authorized LC-4. The LC-4 documentation reconciliation is technically complete and awaits formal owner acceptance. It does not complete VB-030. `NullablePublicOnly` and the single-line lambda `As` clause remain deferred. That was the status at `e6c68048aba647eb0740fd8367a8c298a2b2aeb3`. On November 27, 2026, the owner formally accepted LC-4, including LC4-1 through LC4-8, by VB030-OD-CLOSURE-1, and formally closed VB-030 within its approved VB.Next 1.0 scope by VB030-OD-CLOSURE-6. `NullablePublicOnly` and the single-line lambda `As` clause remain deferred, unassigned, and explicitly nonblocking, by VB030-OD-CLOSURE-2 and VB030-OD-CLOSURE-3. A `notnull`-style constraint remains an unapproved design candidate and does not block closure, by VB030-OD-CLOSURE-4. The mission stays unchanged, by VB030-OD-CLOSURE-5. Accepted limitations and residual risks A, B, and C remain. The accepted production sources remain `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`. No subsequent slice is authorized. The current normative description is [Current accepted behavior](#current-accepted-behavior). The consolidated evidence is `LanguageCompletion/LC-1G.7-CONSOLIDATED-NULLABLE-INTEGRATION-VERIFICATION-REPORT.md`. The closure record is `LanguageCompletion/LC-1G-FORMAL-OWNER-ACCEPTANCE-AND-CLOSURE-REPORT.md`.

The approved sequence is in [vbnext-lc1g-integration-plan.md](vbnext-lc1g-integration-plan.md): LC-1G.4 is public Roslyn nullable APIs, LC-1G.5 is IDE, analyzer, and diagnostic integration, LC-1G.6 is Edit and Continue, and LC-1G.7 is consolidated acceptance. `NullablePublicOnly` is not one of those slices. Two observations from the LC-1G.2a regression stay open for LC-1G.7 and are not confirmed defects: `Substitution_EveryConstructedSymbolKeepsItsArgument` once reported an unexpected BC42512 on `methodText` under a parallel run and then passed in isolation, with `xUnit.MaxParallelThreads=1`, and in the recorded default run; the first full nullable filter after the missing-constructor diagnostic aborted with a test-host crash and no dump, and the crash cause was not established. The evidence is `LanguageCompletion/LC-1G.2a-METADATA-CORRECTNESS-CLOSEOUT-REPORT.md`, section 16. LC-1G.4c.4 ran the named substitution test independently and it passed. That test is not selected by `FullyQualifiedName~Nullable`. Neither observation has a root-cause resolution. LC-1G.7 must include both when it is separately authorized.

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

## Current accepted behavior

This section is the normative description of nullable reference types after LC-1G closure and the LC-4 documentation reconciliation. It does not add syntax, diagnostics, or semantics. It does not accept LC-4 and it does not complete VB-030. Evidence that is outside this repository is under `D:\Development\VBNext\Evidence\LanguageCompletion\`. The examples below are the forms already locked by this note or by permanent tests named with them. They were not recompiled for LC-4.

### Purpose

On `LanguageVersion.VbNext`, a reference type written with `?` may be `Nothing`. A reference type written without `?` is a non-nullable reference for nullable analysis. The feature is a modernization of Visual Basic. It is not a translation of C# nullable reference types, and it does not claim the same warnings or the same exhaustiveness.

### Activation and compatibility

`default` and `latest` stay on Visual Basic 17.13. Choosing `vbnext` does not turn nullable analysis on. The `/nullable:` values remain `disable`, `annotations`, `warnings`, and `enable`, and `disable` is the default on `vbnext`. The MSBuild property is `Nullable`, with the same four words. The switch is ignored unless the language version is `vbnext`. An unrecognized value is BC2014, `the value '{1}' is invalid for option '{0}'`.

`#Nullable Enable`, `#Nullable Disable`, `#Nullable Warnings`, `#Nullable Annotations`, and `#Nullable Restore` override the project switch for the rest of the file. `Restore` returns to the project switch. A bad setting is BC37379, `#Nullable must be followed by Enable, Disable, Warnings, Annotations, or Restore.` Visual Basic 17.13 reports `#Nullable` as an unrecognized directive.

There is no null-forgiving postfix. `!` remains the dictionary operator. `Nothing`, `Is Nothing`, and `IsNot Nothing` keep their existing meanings. `Integer?` remains `Nullable(Of Integer)` whether annotations are on or off.

With annotations off, `String?` is still BC33101: the type must be a value type or a type argument constrained to `Structure` in order to be used with `Nullable` or the nullable modifier. Reference types stay oblivious (`NullableAnnotation.None`).

### Syntax

With annotations on, the accepted forms are the existing `?` after a type:

```vb
Dim name As String? = Nothing

Function Find(id As Integer) As Customer?
End Function

Sub Save(customer As Customer?)
End Sub

Class Box(Of T)
    Public Value As T?
    Public Property Item As T?
    Public Function Id(value As T?) As T?
    End Function
End Class
```

`String?` is still `String`. The `?` is an annotation. It does not create `Nullable(Of String)` and it does not change CLR type identity. `Function Id(Of T)(value As T?) As T?` is the unconstrained form covered by `NullableAnnotationTests`. A `Structure` constraint still means `Nullable(Of T)`.

`String?()` is an array of nullable strings. `String()?` is an array reference that may be `Nothing`. A `?` before an array rank annotates the element. A `?` after an array rank annotates that array. `Public A()? As String` does not parse; the permanent test is `NullableDeclarationTests`. Write `As String()?`. `List(Of String?)` annotates the type argument. `List(Of String)?` annotates the list.

The same `?` is accepted, when annotations are on, for parameters, returns, fields, properties, locals, array elements, generic type arguments, tuple elements, delegate returns, lambda parameters, and a multiline lambda return. A single-line lambda still does not bind an `As` clause. That form is deferred and is not implemented. Nested constructions use the same marker on the type that carries the annotation. An inferred local may keep declaration annotation `None` while flow state describes the initializer. An inferred field without an `As` clause stays oblivious.

### Declaration annotations and flow state

A declared annotation is part of the signature the author wrote or the metadata that was imported. Flow state is a fact about one use. A variable declared `String?` may be flow-proven non-`Nothing` after `If name IsNot Nothing`. That refinement does not change the declaration symbol and does not change the CLR type. A declared non-nullable reference (`String` with annotations on) is `NotAnnotated`. An oblivious reference is `None`: nullable analysis does not treat it as a promise. Flow-proven non-`Nothing` and potentially `Nothing` are states of the nullable flow analysis. The analysis is conservative. It does not prove that every value is non-`Nothing`.

`NullableFlowAnalysis` is the only flow authority. Compiler binding remains the declaration authority. No second flow engine, IDE nullable model, or Edit and Continue nullable model was added.

The flow policies approved on November 26, 2026 stay in force, including inferred generic method arguments, reduced extension receivers, declared contracts for ordinary callers, recursive bodies that are not proved by their own contracts, conservative `On Error` and `Resume` invalidation, and `NotNull` winning over `MaybeNull`. Assignment, initialization, conditional tests, `Is Nothing` and `IsNot Nothing`, calls, returns, loops, and constructor initialization use that engine. Exceptional flow and recursive contracts stay conservative, as the accepted limitations record.

### Diagnostics

Warnings are reported only on `vbnext` when the nullable context enables warnings. The accepted identifiers and messages are:

| Id | Message |
| --- | --- |
| BC42510 | Possible Nothing assignment to '{0}'. |
| BC42511 | This function may return Nothing. |
| BC42512 | '{0}' may be Nothing. |
| BC42513 | Possible Nothing argument for '{0}'. |
| BC42517 | Non-nullable member '{0}' is not assigned a non-null value on every path out of this constructor. |
| BC42518 | Member '{0}' is not non-null on every path that returns from this method. |
| BC42519 | Member '{0}' is not non-null on every path that returns {1}. |
| BC42520 | '{0}' is not an accessible field or property for this member contract. |
| BC42521 | '{0}' returns a weaker nullable contract than the member it overrides. |
| BC42522 | Parameter '{0}' does not match the nullable contract of the overridden parameter. |
| BC42523 | '{0}' returns a weaker nullable contract than the interface member it implements. |
| BC42524 | Parameter '{0}' does not match the nullable contract of the implemented parameter. |
| BC42525 | '{0}' does not promise to never return, unlike the member it overrides or implements. |
| BC42526 | Setter parameter '{0}' has a different nullable annotation from property '{1}'. |
| BC42527 | Parameter '{0}' must be non-null when this method returns. |
| BC42528 | Parameter '{0}' must be non-null when this method returns {1}. |
| BC42529 | The result must be non-null when parameter '{0}' is non-null. |
| BC42530 | Parameter '{0}' must be non-null when parameter '{1}' is non-null. |
| BC42531 | '{0}' is not a parameter of this method. |
| BC42532 | Method marked with 'DoesNotReturn' should not return. |

BC42514, BC42515, and BC42516 are not nullable-reference diagnostics. Existing warning suppression is unchanged. This reconciliation does not add a suppression syntax. These warnings do not by themselves establish C# equivalence. Overload resolution, generic inference, lambda inference, delegate relaxation, overrides, interface implementation, and extension methods keep existing Visual Basic rules except where an accepted VB-030 rule above applies. A nullable annotation is not an overload-resolution rank and is not a distinct CLR type.

The standard `System.Diagnostics.CodeAnalysis` attributes `AllowNull`, `DisallowNull`, `MaybeNull`, `NotNull`, `MaybeNullWhen`, `NotNullWhen`, `NotNullIfNotNull`, `MemberNotNull`, `MemberNotNullWhen`, `DoesNotReturn`, and `DoesNotReturnIf` are compiler analysis contracts. They are not runtime enforcement, and a declared attribute is not proof that the method body meets it. They are recognized by namespace, name, and constructor signature.

### Metadata, generics, and interoperability

The supported metadata contract is `System.Runtime.CompilerServices.NullableAttribute` with the standard byte encoding: `0` oblivious, `1` not annotated, `2` annotated, including nested type arguments and array elements. VB.Next does not currently emit `NullableContextAttribute`. The decoder still reads a `NullableContextAttribute` emitted by another compiler. A target that does not define a compatible `NullableAttribute` receives one embedded definition when nullable metadata is emitted. A predefined attribute that lacks either standard constructor reports the existing missing-runtime-helper diagnostic and is not replaced by a second definition. A predefined attribute with both constructors and an incorrect base type follows the existing compiler-contract inspection.

The flat method-level `NullableAttribute(Byte())` emitted by VB.Next preview assemblies before LC-1B (`cf80f28e8aa65e42f8e0f52c6c1fddf6d54657f0`) is not a supported contract. Those assemblies need recompilation. No second decoder was added.

Imported annotations are metadata facts. They are visible to the compiler on Visual Basic 17.13, and that language version does not gain nullable warnings or a `?` in display text from them. Parameterless metadata display without an applicable VB.Next compilation does not invent `?`. This is the same class of metadata fact already described in the mission for records, `In` parameters, and `ByRef` display. This assignment does not edit the mission document. The fact is recorded here.

Annotations on generic arguments, constructed types, constructed methods, substituted symbols, reduced extension methods, nested types, array elements, and tuple elements are stored beside CLR identity. Default equality and hashing ignore them. `SymbolEqualityComparer.IncludeNullability` observes them. An annotation does not create a distinct CLR type or a distinct compiler-symbol identity.

A named delegate emits `NullableAttribute`. A shared anonymous delegate template cannot carry two contradictory nullable attributes. Explicit lambda parameters keep the annotation written on them. Target-inferred lambda parameters receive the delegate parameter annotation. A multiline lambda return uses the declaration path. A body-inferred lambda return keeps the previously accepted annotation behavior and may stay `None`.

`NullablePublicOnly` is not a Visual Basic compiler option. Imported `NullablePublicOnlyAttribute` metadata is honored. The switch stays deferred.

Reference compilation was verified for `microsoft.netframework.referenceassemblies.net47` 1.0.3 and for the installed `Microsoft.NETCore.App.Ref` 11.0.0 pack. Reflection with `NullabilityInfoContext` was run on the .NET 11 runtime. Exact .NET Framework 4.7 CLR execution was not performed. Reference compilation is not a claim of universal framework compatibility. The compiler does not take a dependency on `NullabilityInfoContext`.

### Public API, tooling, and Edit and Continue

Public nullability uses `NullableAnnotation`, `NullableFlowState`, and `NullabilityInfo`. `TypeInfo.Nullability` and `TypeInfo.ConvertedNullability` carry that `NullabilityInfo`. `TypeInfo.Type` and `TypeInfo.ConvertedType` are the corresponding types, with the flow annotation applied through `WithNullableAnnotation` when the public annotation differs. `WithNullableAnnotation` returns an immutable projection for `Annotated` and `NotAnnotated` and returns the compiler symbol for `None`. The projection preserves the specialized public interface and unwraps before compiler re-entry. It is not a compiler `TypeSymbol` subclass. Default equality stays on CLR identity. Declaration annotations and flow-derived annotations stay distinct. A standalone speculative expression remains `NullableFlowState.None` because that API does not have the enclosing method body. `IEventSymbol.NullableAnnotation` remains `None`. Annotations inside an event's delegate type are preserved.

On `vbnext` with annotations enabled, symbol display, Quick Info, and generated signatures show a nullable reference as `String?`. Visual Basic 17.13 text does not gain that marker. Analyzer consumption uses the public annotations. Completion, rename, Signature Help, and the broad refactoring suites were not executed, so those surfaces are not an exhaustive tooling guarantee.

An annotation-only edit is a metadata delta. A CLR signature change remains an existing rude edit. Runtime capability checks stay the existing checks. Live Hot Reload and the Visual Studio debugger were not executed. Metadata deltas were not applied to a running process. An emitted delta is not evidence of live application.

Accepted cross-language coverage is a VB nullable interface implemented by C#, reduced extension annotations crossing VB and C#, long-tuple element metadata, and public method type-argument annotations on an imported constructed method. The permanent class `NullableConsolidatedCrossLanguageTests` contains three tests. Those three supplement existing long-tuple coverage. They are not four new tests, and they are not universal cross-language equivalence.

### Accepted limitations

Semantic limitations that stay accepted:

- `IEventSymbol.NullableAnnotation` remains `None`.
- A shared anonymous delegate template cannot encode contradictory nullable attributes.
- Body-inferred lambda returns keep the previously accepted annotation.
- Inferred locals may keep declaration annotation `None`.
- Initializer expressions do not receive method-body flow.
- Standalone speculative expressions stay `NullableFlowState.None`.
- Internal `Unknown` may surface as public `MaybeNull` without a separate diagnostic.
- `On Error` and `Resume` can invalidate nullable facts, and a cycle does not prove its own body.
- Parameterless metadata display without an applicable VB.Next compilation does not invent `?`.
- An omitted public array element annotation stays `None`.
- `NotNull` wins over `MaybeNull`.
- The conservative flow limitations listed above this section stay in force.
- An unadorned Visual Basic type parameter remains `NullableAnnotation.None`.
- `Integer?` remains `Nullable(Of Integer)`.

Verification limitations that stay accepted, and that do not mean the language behavior is absent:

- Mid-capture cancellation was not reproduced.
- Concurrency verification was finite and representative.
- Some contexts have representative rather than exhaustive coverage.
- Exact .NET Framework 4.7 verification was reference compilation.
- Live Hot Reload, the Visual Studio debugger, and application of metadata deltas to a running process were not performed.
- Dedicated nullable multi-generation, local-slot, async, iterator, active-statement, and nested-generic Edit and Continue fixtures were not added.
- Separate C# Edit and Continue suites were not repeated.
- Complete completion, rename, Signature Help, and broad refactoring suites were not executed.
- Representative IL comparisons do not establish complete assembly identity.
- A separate representative analyzer allocation benchmark was not reported.

Three residual verification risks stay owner-accepted. None was reproduced after bounded investigation. No common cause was established. None is resolved:

- A. Unexpected BC42512 in `Substitution_EveryConstructedSymbolKeepsItsArgument`.
- B. A historical broad nullable test-host crash, whose original log was not preserved.
- C. Unexpected BC42511 in `NullableFrameworkReflectionTests.ExactNet47_FixtureSuppliedNotNullIsRecognized`.

The accepted nine-group regression matrix is 1,659 passed, 0 failed, and 35 skipped. Overlapping selections are not unique tests. The additional accepted counts are recorded in the LC-1G.7 evidence and are not rerun here. Performance figures from that evidence are observations and future baselines, not thresholds. Performance regression has not been conclusively ruled out.

### Deferred items and completion

`NullablePublicOnly` remains deferred and is not assigned to a slice. LC-1A recommended that deferral because it is a library-publishing switch and is not required to make public signatures correct. NRT-OD-3 accepts that it is not a new VB compiler option. The LC-1G closure does not decide whether that deferral blocks a future VB-030 closure.

The single-line lambda `As` clause remains a deferred specification item. Multiline lambda returns already use the declaration path. LC-1G.4c recorded the single-line gap as a future consistency review and did not authorize implementation. The LC-1G closure does not decide whether that item blocks VB-030 closure.

A C# `notnull` constraint cannot be expressed with existing Visual Basic constraint syntax. No such syntax is approved. LC-1D recorded that as unresolved documentation, not as a blocker to continuing the nullable work.

At LC-4 technical readiness, VB-030 was not complete. Slices 10a through 10e are historically Done. Slice 10f and LC-1F are owner-accepted. LC-1B through LC-1E are published implementation. LC-1G, including its 48 inventory obligations, is formally accepted and closed. LC-4 documentation is the reconciliation in this section and in the status documents. At that point, formal owner acceptance of LC-4 was still pending, and the two deferred items above still needed an owner decision before VB-030 could be closed.

### Formal closure

On November 27, 2026, the owner formally accepted LC-4 and closed VB-030 within its approved scope. The accepted documentation commit is `e6c68048aba647eb0740fd8367a8c298a2b2aeb3`. The accepted production sources remain `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`. This section does not change nullable syntax, semantics, diagnostics, metadata, or public APIs. Accepted limitations and residual risks A, B, and C remain.

- VB030-OD-CLOSURE-1. LC-4 is formally owner-accepted. LC4-1 through LC4-8 are accepted as satisfied. LC4-7 accepts the documentation of an unapproved design candidate. LC4-3 does not require a mission edit. No corrective implementation is required.
- VB030-OD-CLOSURE-2. `NullablePublicOnly` remains deferred and unassigned. It does not block VB-030 closure or VB.Next 1.0 nullable-reference completion. NRT-OD-3 remains in force: it is not a VB compiler option, and imported `NullablePublicOnlyAttribute` metadata is honored. No new compiler option is authorized.
- VB030-OD-CLOSURE-3. The single-line lambda `As` clause remains deferred and unassigned. It does not block VB-030 closure. Existing single-line lambda syntax is unchanged. No new spelling is introduced. A future consistency review needs a separate authorization.
- VB030-OD-CLOSURE-4. A generic constraint that accepts both nonnullable reference types and value types remains an unapproved design candidate. No syntax is approved. It is outside the approved VB-030 implementation scope and does not block closure.
- VB030-OD-CLOSURE-5. The mission document stays unchanged. Its metadata-facts paragraph is not required to name imported nullable annotations. The accepted metadata behavior is this note.
- VB030-OD-CLOSURE-6. VB-030 is formally complete and closed within its approved VB.Next 1.0 feature scope, including the accepted semantic limitations, verification limitations, and residual verification risks. Nullable reference types are an accepted VB.Next 1.0 language feature. This closure does not complete VB.Next 1.0, does not claim C# nullable equivalence, and does not authorize a subsequent slice.

The closure record is `LanguageCompletion/VB-030-FORMAL-OWNER-ACCEPTANCE-AND-CLOSURE-REPORT.md`.
