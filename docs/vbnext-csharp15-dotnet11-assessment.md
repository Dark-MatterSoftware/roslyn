# C# 15 and .NET 11 language compatibility assessment

**Status: Executable compatibility verification complete — pending owner implementation decisions.**

Sections 1 through 20 are the accepted static assessment. Their historical status was **Static assessment complete — pending owner review and executable verification authorization.** That record is unchanged in substance. Section 21 records the later executable verification. This document does not approve native syntax, a feature identifier, a phase, a slice, or a wave. It does not change the canonical mission in [vbnext-mission.md](vbnext-mission.md). Production implementation remains unauthorized.

Access date for the official pages below: 2026-11-27.

## 1. Repository baseline

| Check | Result |
| --- | --- |
| Repository | `D:\Development\VBNext\Repo` |
| Branch | `vbnext/main` |
| Remote | `https://github.com/Dark-MatterSoftware/roslyn.git` |
| Local HEAD | `9472ceb6bf98dc0aaf285a92e0a26c56097bb82b` |
| Subject | `Formally close Introduce Parameter local-symbol eligibility correction` |
| Parent | `5a3731bdbb0df6f01918213c6b01a52c6b4f4372` |
| `origin/vbnext/main` | `9472ceb6bf98dc0aaf285a92e0a26c56097bb82b` |
| Remote `refs/heads/vbnext/main` | `9472ceb6bf98dc0aaf285a92e0a26c56097bb82b` |
| Working tree | Four protected owner files only. Nothing staged. |
| Canonical mission object | `39d09e6d5bf776009de9da531bbac2ade79c673d` (`git rev-parse HEAD:docs/vbnext-mission.md`) |

The expected baseline matched. No reset, restore, stash, or clean was performed.

Protected files, hashed before the assessment and left unmodified:

| File | SHA-256 |
| --- | --- |
| `docs/vbnext-toolset.md` | `8077E509C163A190769557E2A8E2553BA0576AE592B5C0839953D8E11EC381DF` |
| `vbnext/Start-VbNextVisualStudio.ps1` | `2F7F42816DF4F921174D0F5EDCA9C84C4721F42849064B43B80D7E76D971BDBE` |
| `vbnext/Deploy-VbNextExtensions.ps1` | `1BE183E4892081306C2B865FFCC54FCA69EB735306AC69E288C0C920DDF6517E` |
| `vbnext/VbNext.VisualStudio.ps1` | `A3994ED35A5E878044B57484C88578D34EBF538C7BF6C39F5FF08CF7DE848ABF` |

Closed work that this assessment does not reopen:

| Closure | Commit |
| --- | --- |
| VB-030 | `c5b21efac74b4f9785ed3b0c93e3ed290d9e2007` |
| VB-187 | `388db4fd03419bc8b4c8548d4043c66e053abbcf` |
| Introduce Parameter local-symbol eligibility | `9472ceb6bf98dc0aaf285a92e0a26c56097bb82b` |

The accepted compiler baseline `5fe4bcd873a37433ae512da60adbc80a1cd6eb13` stays the historical Release compiler baseline. The Debug Features assembly accepted for the Introduce Parameter correction is not provenance for a future C# 15 test. The malformed historical Features hash and the absent historical Workspaces assembly remain historical limitations.

## 2. Upstream Roslyn baseline

| Point | Commit | Date | Subject |
| --- | --- | --- | --- |
| Original fork point | `8bd473fe7df62aaf8117a37a162c1fb4ee398ba5` | 2026-09-30 | `Remove legacy pull diagnostics methods (#85702)` |
| First VB.Next commit | `45e99f0ad2cf75b44f57e88ed0975752eccd1e7d` | 2026-09-30 | `Add a Vb.Next roadmap for experimental Visual Basic work.` |
| Latest upstream commit contained in this fork | `e4e2833be7abbc54776788ca35d5c4d2697b0261` | 2026-10-19 | `Add tests for ref returning function pointers (#85949)` |
| Merge that brought it in | `531d72d81e28e39628164102bd173f5b3afaf0a1` | 2026-10-19 | `Merge dotnet/roslyn main (e4e2833b) into vbnext/main` |

`git merge-base` of the first VB.Next commit and `e4e2833` is the fork point above. `e4e2833` is an ancestor of HEAD. No later commit message in `e4e2833..HEAD` uses an upstream pull-request number. The fork has not imported upstream since that merge. Product version in `eng/Versions.props` is 5.13.0. `LanguageVersion.CSharp15` is `1500`, and `LanguageVersion.CurrentVersion` is `CSharp15` (`src/Compilers/CSharp/Portable/LanguageVersion.cs`).

The C# 15 implementation inspected below is the implementation in this tree, not a claim that it equals `dotnet/roslyn` `main` on the access date. The GitHub commit list for `dotnet/roslyn` `main`, read on 2026-11-27, continues through an abbreviated commit `93fbfb0` (`Mark Csi MSBuild task as multithread-safe (#86636)`, 2026-11-27). That range was not cloned or diffed. Later upstream edits are a limitation of this assessment, not evidence that the six released features are absent locally. They are present locally.

## 3. Official inventory

Authoritative language page, fetched 2026-11-27:

<https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15>

C# 15 is the default language version for `net11.0` in the .NET 11 SDK. Only memory safety requires `preview`.

Released:

1. Collection expression arguments.
2. Union types.
3. Closed hierarchies.
4. Extension indexers.
5. Labeled `break` and `continue`.
6. Non-virtual static interface members on older runtimes.

Preview, separate from the six:

7. Memory safety, including pointer relaxations under `preview` and updated caller obligations under the `updated-memory-safety-rules` feature. The compiler records `System.Runtime.CompilerServices.MemorySafetyRulesAttribute`.

Specification index, fetched 2026-11-27:

<https://github.com/dotnet/csharplang/blob/main/Language-Version-History.md>

That index lists the first five released features and links these specifications. Champion issues are labeled implemented and still awaiting an ECMA specification:

| Capability | Specification | Champion |
| --- | --- | --- |
| Collection expression arguments | `proposals/csharp-15.0/collection-expression-arguments.md` | Language-version history |
| Unions | `proposals/csharp-15.0/unions.md` | <https://github.com/dotnet/csharplang/issues/9662> |
| Closed hierarchies | `proposals/csharp-15.0/closed-hierarchies.md` | <https://github.com/dotnet/csharplang/issues/9499> |
| Extension indexers | `proposals/csharp-15.0/extension-indexers.md` | <https://github.com/dotnet/csharplang/issues/9856> |
| Labeled break and continue | `proposals/csharp-15.0/labeled-break-continue.md` | Language-version history |

The language-version history does not list non-virtual static interface members or memory safety. The .NET 11 overview, fetched the same day from <https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview>, lists the other released features and memory safety, and omits non-virtual static interface members. It also does not mark memory safety as preview in that short list. The dedicated C# 15 page is the inventory used here. The omissions are documentation drift, not a different released set. The static-interface behavior is in the local C# compiler (`SourceMemberMethodSymbol.CheckFeatureAvailabilityAndRuntimeSupport`). Memory safety is preview in both the C# 15 page and `MemorySafetyRulesVersion`, which is marked experimental.

.NET 11 itself is generally available on the overview page. Its runtime and library additions, including `System.Text.Json` union support and the `UnionAttribute` / `IUnion` scaffolding, are framework libraries. They are not VB syntax.

Historical November 25, 2026 review, reconciled and not treated as current fact:

| Document | Path |
| --- | --- |
| Index | `D:\Development\VBNext\Evidence\LanguageCompletion\00-README.md` |
| Completion assessment | `01-COMPLETION-ASSESSMENT.md` |
| Feature matrix | `02-FEATURE-MATRIX.md` |
| Interoperability matrix | `03-INTEROP-CONSUMPTION-MATRIX.md` |
| Historical reconciliation | `04-HISTORICAL-RECONCILIATION.md` |
| Scope and owner decisions | `05-VBNEXT-1.0-SCOPE-AND-OWNER-DECISIONS.md` |
| Independent Grok review | `06-INDEPENDENT-GROK-LANGUAGE-REVIEW.md` |

That review's baseline was `f82b93db41821767fe537dd46300798161535b52`. Its C# 15 inventory matches the page fetched today. Its VB-187 row is stale: VB-187 is closed. Its "wait until the design stops moving" posture is superseded by release of the design. No later owner decision accepted or rejected its proposed C# 15 consumption probe. This assessment is that probe, done statically.

## 4. How to read a row

Implementation, interoperability, verification, and design are independent. A percentage of C# syntax covered is not a result.

Evidence labels used below:

- **Source.** Read from this tree. Not executed in this assignment.
- **Historical.** An earlier report says a test or probe ran. Not rerun.
- **Hypothesis.** What the source implies a future run would do. Not a test result.

## 5. Union types

### Released shape

A C# union is a class or structure, not a new CLR type. The local contract names, from `AttributeDescription` and `WellKnownTypes`, are:

- `System.Runtime.CompilerServices.UnionAttribute`
- `System.Runtime.CompilerServices.IUnion`
- Nested `IUnionMembers` (`WellKnownMemberNames.UnionMembersInterfaceName`)
- A `Value` property (`WellKnownMemberNames.ValuePropertyName`)
- Optional `HasValue`
- `TryGetValue`
- One single-parameter factory per case, collected by `NamedTypeSymbol.UnionFactoryMethods`

`SourceNamedTypeSymbol.IsUnionTypeCore` is true for a `union` declaration or for `UnionAttribute`. Interfaces are not union types. `CSharpCompilation` synthesizes `UnionAttribute` onto a `union` declaration. Case types are the parameter types of those factories (`NamedTypeSymbol.UnionCaseTypes`). C# supplies implicit conversions from each case and exhaustive `switch` matching (`UnionMatchingRewriter`).

Local implementation and tests:

- `src/Compilers/CSharp/Portable/Symbols/NamedTypeSymbol.cs`
- `src/Compilers/CSharp/Portable/Symbols/Synthesized/SynthesizedUnionCtor.cs`
- `src/Compilers/CSharp/Portable/Symbols/Synthesized/SynthesizedUnionValuePropertySymbol.cs`
- `src/Compilers/CSharp/Portable/Binder/UnionMatchingRewriter.cs`
- `src/Compilers/CSharp/Test/CSharp15/UnionsTests.cs`
- `src/Compilers/CSharp/Test/Syntax/Parsing/UnionParsingTests.cs`

`UnionsTests.UnionType_01` constructs a Visual Basic compilation against the emitted image and asserts `IsUnion` is false and `UnionCaseTypes` is empty for both a union structure and a marked interface. That assertion matches the VB implementation. The test was not executed.

### VB.Next

`TypeSymbol.ITypeSymbol_IsUnion` returns false, and `UnionCaseTypes` is empty, with the comment that VB does not have unions (`src/Compilers/VisualBasic/Portable/Symbols/TypeSymbol.vb`, the `ITypeSymbol_IsUnion` and `ITypeSymbol_UnionCaseTypes` properties). `PublicTypeProjection` forwards those members, so the public API stays false. There is no VB parser, binder, conversion, pattern, or exhaustiveness support for unions.

Records, tuples, `Select Case`, `TypeOf`, and conversion operators are not union support. A record hierarchy is open. A tuple is a fixed product. `Select Case` does not know a closed set of union cases.

Ordinary member access is a different question from union-aware support. A union emitted as a class or structure with constructors and a `Value` property is an ordinary type. VB can be expected to call those members without knowing that the type is a union. That expectation is a hypothesis until a consumer compilation is run. Implicit conversion from a case, union pattern matching, and exhaustiveness are absent.

### Directions

| Direction | Finding |
| --- | --- |
| C# union consumed by VB 17.13 | Ordinary members: hypothesis, likely supported. `IsUnion`: source says false on every language version, because the property does not consult language version. Conversions and patterns: unsupported. |
| C# union consumed by VB.Next | Same. No `vbnext` behavior recognizes the attribute. |
| VB.Next authoring consumed by C# | Unsupported. VB does not emit `UnionAttribute` or the factory shape from a union declaration. |
| VB.Next to VB.Next across an assembly | Same absence. |

### Recommendation

1. Basic consumption of members: no new syntax. Verify with an executable probe before treating it as accepted.
2. Union-aware conversions and `Select Case` exhaustiveness: an interoperability and language-semantics candidate. New conversions change binding and belong on explicit `vbnext` if approved. Reporting `IsUnion` from metadata is a symbol fact. The mission already allows a symbol fact read from metadata to surface on 17.13 through the compiler API. Whether this one should is an owner decision. The current C# test encodes the opposite.
3. Reciprocal authoring: after the consumption semantics exist. Not part of basic consumption.
4. Native declaration: a design study, not a requirement. Illustrative and unapproved spellings are in section 13. Records do not make this unnecessary, and C# syntax does not make it required.

Framework class: B for any compiler behavior that keys off `UnionAttribute` or `IUnion`. A for ordinary calls. D for `System.Text.Json` union converters. Older targets that lack the attribute type must still compile programs that do not use a union-aware feature.

Desktop: not a Windows Forms or WPF blocker. It becomes a mixed-language library issue when a public API is a union and the VB caller needs the conversion or the exhaustive case split. Until that API exists, the gap is a modernization candidate.

## 6. Closed hierarchies

### Released shape

`closed` is a contextual modifier. A closed class is implicitly abstract and cannot also be `sealed`, `static`, or explicitly `abstract`. Direct derivation outside the declaring assembly is a compile-time error. Derivation is not transitive. Exhaustiveness applies to the known direct descendants.

Metadata, from this tree:

- `System.Runtime.CompilerServices.IsClosedTypeAttribute` (`AttributeDescription.IsClosedTypeAttribute`).
- `CompilerFeatureRequired("ClosedClasses")` on instance constructors whose containing type `IsClosed` (`MethodSymbol.AddClosedClassesFeatureRequiredAttribute`).
- C# import reads the attribute (`PENamedTypeSymbol.IsClosed`) and allows `CompilerFeatureRequiredFeatures.ClosedClasses` on those constructors (`PEMethodSymbol.DeriveCompilerFeatureRequiredDiagnostic`).

The CLR does not enforce the restriction. A compiler that emits the derived type produces a type the runtime will load. `NotInheritable` is a different restriction: it forbids every further derived type, in every assembly. `Friend` limits who can name a type. `MustInherit` is abstractness. None of them records "these are all the direct descendants" or stops only cross-assembly derivation.

Local tests: `src/Compilers/CSharp/Test/CSharp15/ClosedClassesTests.cs`, `src/Compilers/CSharp/Test/Syntax/Parsing/ClosedModifierParsingTests.cs`. Not executed.

### VB.Next

`TypeSymbol.ITypeSymbol_IsClosed` returns false. `GetClosedDerivedTypeInfo` throws `InvalidOperationException`. No VB file mentions `ClosedClasses` or `IsClosedTypeAttribute`.

`PEMethodSymbol.DeriveCompilerFeatureRequiredUseSiteInfo` allows `RequiredMembers` and `UserDefinedCompoundAssignmentOperators`. It does not allow `ClosedClasses`. A use of a constructor that carries `CompilerFeatureRequired("ClosedClasses")` therefore takes the unsupported-feature path and reports `ERR_UnsupportedCompilerFeature` (BC37319).

What that means, as source, not as a run:

- A non-closed descendant's own constructor is not marked, so constructing that descendant from VB does not see the attribute. Hypothesis: ordinary use of the public descendants succeeds.
- Deriving in VB calls a closed base constructor and should report BC37319. That blocks the derivation. It does not permit an external class to derive. The diagnostic is "unsupported compiler feature," not a closed-hierarchy diagnostic.
- Exhaustiveness is absent because the subtype set is never read.

### Recommendation

Mandatory interoperability correction, after an executable confirmation of BC37319: recognize `IsClosedTypeAttribute`, allow `ClosedClasses` on the constructors that carry it, and report a specific diagnostic when VB `Inherits` a closed type across an assembly. Do not add a modifier. The inheritance rule is metadata compatibility. It can apply on 17.13, because no existing program could legally inherit a closed type, and BC37319 already rejects the constructor. Changing the diagnostic text is still an owner decision.

Exhaustiveness over a consumed closed hierarchy is a later language-semantics candidate on `vbnext`, not a requirement for calling the types. Native authoring of `closed` is not authorized and is not recommended ahead of the consumption rule.

Desktop: not a blocker for applications that use the public descendants. It matters for a library whose extension point is "derive from this closed base," which VB should refuse clearly.

## 7. Extension indexers

### Released shape

An extension indexer is an indexer in an `extension` block with a named receiver. C# emits it as an extension property and synthesizes `DefaultMemberAttribute` on the extension grouping type from the first indexer's metadata name (`ExtensionGroupingInfo.synthesizeDefaultMemberAttributeIfNeeded`). Instance indexers win over extension indexers. The specification's consumption order is instance indexer, implicit indexer, extension indexer, extension implicit indexer.

Local tests: `src/Compilers/CSharp/Test/CSharp15/ExtensionIndexersTests.cs`. Not executed.

### What VB already has

VB-039 extension blocks and VB-107 consumption of C# extension properties are implemented. A parameterized extension property is the VB indexer. `ExtensionPropertyTests.ExtensionIndexer_ReceiverAndIndexArePassed` declares `Property Item(index As Integer)` inside `Extension(box As Box)` and compiles `box(0)`. That test was not executed here. The mechanism is source-visible:

- `ReducedExtensionPropertySymbol.IsDefault` is true when the unreduced property has more than one parameter (receiver plus at least one index).
- `LookupDefaultPropertyInClass` calls `LookupForExtensionDefaultProperties` only after the type's own default property is missing.
- That helper keeps a reduced candidate only when `reduced.IsDefault` is true.

So a same-compilation VB extension property with extra parameters already participates in `receiver(index)` for a class, structure, or module receiver. No `Indexer` keyword is needed to author that.

### C# consumption gap

C# extension properties are not ordinary members of the static class. `PENamedTypeSymbol.BuildExtensionMembers` builds `PEExtensionPropertySymbol` values and stores them on `GetExtensionMembers().InstanceProperties`. `AppendProbableExtensionProperties` adds those for named lookup. `AppendProbableExtensionDefaultProperties` does not. It walks `GetMembers()` and keeps properties whose `IsExtensionProperty` is already true. `PEExtensionPropertySymbol` is not in `GetMembers()`, and its own `IsDefault` is the synthesized-property default, false. `SynthesizedPropertyBase.IsDefault` returns false. The reduced wrapper would have reported default, but the candidate is never collected.

`LookupDefaultProperty` for an interface or a type parameter never calls the extension-default helper at all. The C# sample `numbers(2)` on `IEnumerable(Of Integer)` is in that hole even before the metadata-collection hole.

Named access is a separate path. `LookupForExtensionProperties` does include `InstanceProperties`, and it is skipped when language version is below `vbnext` for a `PEExtensionPropertySymbol`. Hypothesis: on `vbnext`, `numbers.Item(2)` can bind, and `numbers(2)` reports BC30547 (`ERR_InterfaceNoDefault1`) or, for a class, BC30367 (`ERR_NoDefaultNotExtend1`). That split is a hypothesis.

### Reciprocal authoring

A VB extension property is a source property whose parameter list starts with the extension receiver (`SourcePropertySymbol.ComputeParameters`). It is not the C# grouping type, `ExtensionMarkerAttribute`, or `DefaultMemberAttribute` on that grouping. `IsExtensionProperty` on an imported `PEPropertySymbol` stays the base false value. Hypothesis: a VB-authored extension indexer is visible in the compilation that declares it, and is not reconstructed as an extension indexer by C# or by another VB assembly. Same-compilation success is not cross-assembly authoring.

### Recommendation

Interoperability correction only: teach the existing default-property lookup to see C# extension indexers, including interface receivers, and decide explicitly whether VB extension properties should round-trip. Do not add an `Indexer` keyword. Keep the `vbnext` gate that already surrounds C# extension-property consumption. 17.13 should not gain a new binding for `receiver(index)`.

Desktop: not a blocker for application code that calls ordinary default properties. It is a mixed-solution gap when a C# library's supported way to index a receiver is an extension indexer.

## 8. Collection expression arguments

C# 15 adds a leading `with(...)` element that supplies constructor or collection-builder arguments, then the elements. The local syntax is `WithElementSyntax`. Tests live under `src/Compilers/CSharp/Test/Emit3/Semantics/CollectionExpressionTests_WithElement_*.cs`. Not executed.

VB collection expressions are parenthesized and target-typed, for example `Dim values As List(Of Integer) = (1, 2, 3)` (`CollectionExpressionTests.CollectionExpression_TargetsList`). They are rejected as a collection expression on 17.13. Materialization in `TryBindCollectionInitializerFromElements` uses a parameterless constructor plus `Add`, with `AddRange` for a spread. `GetCollectionBuilderMethod` accepts a builder method with exactly one `ReadOnlySpan` parameter. There is no element that passes extra arguments.

Expressive equivalence already exists: `New List(Of String)(capacity) From { ... }` and `New HashSet(Of String)(comparer) From { ... }`. That is ordinary construction, not a collection expression, and not the builder-argument contract. A longer existing form does not by itself erase a collection-expression gap. The gap is also not a reason to copy `with`. VB already uses `With` for object initializers and the `With` statement. A parenthesized collection expression is also the tuple spelling, disambiguated by target type. A leading `with(...)` element would collide with both.

Recommendation: no new syntax. Classification is optional convenience, deferred. If a later library contract cannot be called by ordinary construction, reopen it as a compiler-contract question, still without the C# spelling. Framework class: A for the spelling, B for `CollectionBuilderAttribute`, which VB already recognizes for the single-span shape.

## 9. Non-virtual static interface members on older runtimes

This is the feature that matters for .NET Framework 4.7.

C# distinguishes the categories in `SourceMemberMethodSymbol.CheckFeatureAvailabilityAndRuntimeSupport` and `Binder.CheckReceiverAndRuntimeSupportForSymbolAccess`:

| Category | Runtime requirement in this C# compiler |
| --- | --- |
| Instance default implementation | `RuntimeFeature.DefaultImplementationsOfInterfaces` |
| Protected interface member | Same runtime support |
| Non-virtual static member with a body | Language version only, through `ReportLackOfRuntimeSupportForStaticMembersInInterfaces`. Public and internal members do not get the default-interface runtime error. |
| Static abstract or static virtual | `RuntimeFeature.VirtualStaticsInInterfaces` |

VB-110 is the existing feature. On `vbnext`, a plain `Shared` method or property with a body in an interface clears `MustOverride` and is not virtual (`SourceMethodSymbol.CreateRegularMethod`, `SourcePropertySymbol.Create`). `Shared MustOverride` and `Shared Overridable` remain the static abstract and static virtual spellings. 17.13 still rejects `Shared` on an interface method (`DefaultInterfaceMethodTests.InterfaceSharedMustOverride_RejectedOn17_13`).

The use-site check in `Binder.ReportDiagnosticsIfObsoleteOrNotSupported` reports the default-interface runtime error for an external instance member that does not require implementation. A shared member is outside that branch. Protected accessibility is still reported when the runtime feature is absent. The check reads `RuntimeSupportsDefaultInterfaceImplementation`, which is `RuntimeFeature.DefaultImplementationsOfInterfaces`, not a hardcoded target-framework name.

Source reading: VB does not impose a broader runtime ban on non-virtual static interface methods and properties than C# 15 does. Static abstract and static virtual stay runtime-gated, as in C#. The older-target claim is not executed. No test in `DefaultInterfaceMethodTests.vb` compiles a plain `Shared` interface method with a body against a reference assembly that lacks the runtime feature.

Recommendation: no new syntax. Treat a bounded correctness change as necessary only if the executable probe shows a remaining ban. Keep the declaration on `vbnext`. Do not move the feature onto 17.13. Framework class: A for the non-virtual static member. C for static abstract, static virtual, instance default implementations, and protected interface members.

## 10. Labeled break and continue

C# `break` and `continue` take an optional name (`Binder.BindBreakOrContinue`). The label sits on the enclosing loop or `switch`. Local tests were not separately inventoried beyond the binder. The feature is released.

VB already has `Exit Do`, `Exit For`, `Exit While`, `Exit Select`, `Continue Do`, `Continue For`, `Continue While`, `GoTo`, and statement labels (`Parser.ParseContinueStatement`, `Parser.ParseExitStatement`). `Exit` and `Continue` select a construct by kind. They bind to the nearest construct of that kind. Two nested `For` loops, or two nested `Do` loops, cannot name the outer one. Different-kind nesting can: `Exit For` inside a `Do` that is inside a `For` leaves the `For`.

`GoTo` can leave the outer loop. It is the existing no-new-syntax alternative, and it is a poor way to say "next outer iteration" because the label sits after the loop or before it, and `Finally` still runs. A Boolean flag is the other existing alternative.

The capability is a real readability gap for same-kind nesting. It is not required for desktop or server programs. It does not affect metadata or C# interop.

Illustrative, unapproved, and not to be implemented from this note:

1. No new syntax. Keep `Exit` / `Continue` by kind, and `GoTo` for the rare outer target.
2. A name on the existing statement: `Exit For Outer` / `Continue For Outer`, with `Outer:` on the `For`.
3. A name only, without repeating the kind, resolved to a labeled loop.

Do not introduce `Break`. Do not replace `Exit` or `Continue`. Any future syntax is explicit `vbnext` and needs a syntax note before implementation. `Await Each`, iterators, `Try`/`Finally`, definite assignment, and the control-flow graph would all have to treat the named target as the existing unlabeled `Exit` already treats its target. That is ordinary lowering work, not a new runtime.

Recommendation: defer. Optional language convenience. Not a milestone blocker.

## 11. Memory safety preview

Separate from the six released features. The .NET 11 SDK includes it as preview. Enabling the updated rules requires `preview` and `updated-memory-safety-rules`. The local enum `MemorySafetyRulesVersion` is marked experimental. `MemorySafetyRulesAttribute` is `System.Runtime.CompilerServices.MemorySafetyRulesAttribute`. Pointer declaration, address-of, `fixed`, pointer conversion of `stackalloc`, and `sizeof` can leave an `unsafe` context under the preview rules. Dereference, pointer member access, pointer element access, and function-pointer invocation stay unsafe. A `safe` contextual keyword and an `unsafe(...)` expression are part of the preview.

VB pointer types exist as metadata symbols (`PointerTypeSymbol`) and remain parked in [vbnext-missing-features.md](vbnext-missing-features.md). ByRef, ByRef-like structures, `Span`, and P/Invoke are already supported and are not this preview. This assessment found no VB recognition of `MemorySafetyRulesAttribute` and no requirement, in the released surface, that VB author pointer syntax in order to call ordinary .NET 11 libraries.

Recommendation: defer. Do not implement preview semantics as a release requirement. Revisit metadata consumption only if a library's public contract becomes unusable, or silently unsafe, without recognizing the attribute. That condition was not found. Framework class: C if the preview rules are ever implemented. They are not a .NET Framework 4.7 requirement.

## 12. C# 13 and C# 14 reconciliation

The November 25 matrix marked the C# 13 and C# 14 rows Done-C except the uncertainties below. This pass checked that the cited implementations are still in the tree. It did not rerun the wave probes. Historical execution stays historical. Absence of a contrary source defect is not fresh interoperability proof.

| Capability | C# | Current source posture | This assessment |
| --- | --- | --- | --- |
| Params collections | 13 | VB-026, VB-106, VB-114. `IsUsableParamArray` and `GetCollectionParamArrayElementTypeByName`. | Keep. Not reopened. |
| `SyncLock` on `System.Threading.Lock` | 13 | VB-064. | Keep. Contract type, absent on older targets. |
| Ref-like generics and `AllowsRefLikeType` | 13 | VB-037 and later slices. | Keep. |
| Ref-like interface implementation | 13 | VB-118. | Keep. |
| Ref locals across `Await` / `Yield` | 13 | VB-075. Pointers stay parked. | Keep the ref-struct rule. Unsafe preview is section 11. |
| Partial properties | 13 | VB-015. `SourcePropertySymbol.IsPartial`. | Keep. |
| Partial indexers specifically | 13 | No test whose name covers `Partial Property Item`. | Unverified residue. Not a new defect. |
| Overload resolution priority | 13 | Imported in `PEPropertySymbol.GetOverloadResolutionPriority`. | Keep. Historical probe. |
| `\e` | 13 | Not a VB escape. | Not applicable. `ChrW` is the existing facility. Reject a new escape copied from C#. |
| `^` in an object initializer | 13 | `^` exists (VB-006). Initializer form not re-read as its own feature. | Historical unverified. Not a blocker. |
| Extension properties, operators, generic blocks | 14 | VB-039, VB-107, VB-116, VB-117, VB-124. | Keep. Indexers are the C# 15 row. |
| Null-conditional assignment | 14 | VB-024, VB-130, VB-134. | Keep. |
| `NameOf` of an unbound generic | 14 | VB-095, VB-101, VB-113. | Keep. |
| Span conversions | 14 | VB-136. | Keep. |
| Lambda parameter modifiers | 14 | VB-100 infers `ByRef ReadOnly`. | Keep. VB has no `out` parameter spelling to mirror. |
| `Field` in a property body | 14 | VB-067. | Keep. |
| Partial events and constructors | 14 | VB-016. | Keep. |
| User-defined compound assignment | 14 | VB-115. Allowed in the VB feature-required set. | Keep. |
| Ignored `#:` directives | 14 | File-based C# tooling. | Not a VB language feature. |
| Optional arguments in expression trees | 14 | Not re-verified. | Insufficient evidence. Not a desktop blocker. |
| Async entry-point helper | 14 | VB async entry points exist from earlier slices. Helper selection not re-read. | Insufficient evidence. Not reopened. |

## 13. Language design

The mission's questions, for the only capabilities where native syntax is even plausible.

### Unions

Why this is VB: a value that is one of several known shapes is a domain-modeling problem, and VB already teaches `Select Case` and records. The missing part is the closed case set and the conversion into it. Existing syntax does not express that set.

It solves a meaningful problem only when callers match on cases. Ordinary construction does not need new syntax.

Keyword room exists (`Union`). A declaration would be readable. It would add a type-declaration form and a pattern interaction. Parser cost is real and unapproved. It preserves old source if it is a new declaration keyword gated on `vbnext`. It can appear in Razor as a type declaration the way a `Class` can. It does not name a framework API. The attribute is a compiler contract, which the mission already allows when the feature is explicit about targets that lack it.

Unapproved alternatives, at most the ones worth writing down:

1. No new syntax. Call the constructors and inspect `Value`.
2. Consumption only: implicit conversion and exhaustive `Select Case` for a C# union, no VB declaration.
3. A later `Union Name(A, B)` declaration, only after 2 is specified.

### Closed hierarchies

Why this would be VB: exhaustiveness over a known set of domain types. Existing `NotInheritable` solves a different problem. No new modifier is authorized. The no-new-syntax alternative is the metadata rule in section 6. That is the recommendation.

### Extension indexers

Existing parameterized properties already solve authoring. The gap is lookup of C# metadata. No new keyword.

### Collection arguments

Existing `New` solves the demonstrated cases. A new element spelling collides with `With` and with tuples. No new syntax.

### Named Exit and Continue

Why this is VB: `Exit For` already names the construct in words. The hole is two loops of the same kind. The no-new-syntax alternative is enough for the milestone. The named form in section 10 is the only spelling worth a later design note. It preserves the keywords.

### Static interface members and memory safety

No new syntax. The first is an existing VB-110 form. The second is a deferred preview and a parked pointer model.

## 14. Framework and language version

| Class | Meaning | Capabilities |
| --- | --- | --- |
| A | Compiler or language, no new runtime dependency | Labeled control flow. Collection-expression spelling. Non-virtual static interface members. Ordinary calls to a union's members. |
| B | Target-dependent compiler contract | Union-aware behavior (`UnionAttribute`, `IUnion`). Closed-type recognition (`IsClosedTypeAttribute`, `ClosedClasses`). Collection builders. The existing span, lock, and params contracts. |
| C | Needs a runtime the older target does not have | Instance default interface implementations. Static abstract and static virtual interface members. Protected interface members. Memory-safety preview, if it were implemented. |
| D | Framework library, not syntax | .NET 11 JSON union support, compression, LINQ joins, numeric types, and the rest of the overview's library list. |

A compiler hosted on a current SDK must keep emitting assemblies whose own code does not require those B or C contracts. Missing contract type: report the existing missing-helper or unsupported-feature diagnostic at the use, and leave unrelated code able to target .NET Framework 4.7. Do not hardcode framework method names for this set. The runtime checks that already exist are `RuntimeFeature` members.

`default` and `latest` stay Visual Basic 17.13. Explicit `vbnext` is required for new syntax and for new binding, including extension-indexer lookup if that correction is approved. Recognizing a metadata fact, and refusing cross-assembly inheritance of a closed type, are compatibility corrections. They may apply on 17.13 because they do not give old source a new successful binding. That choice is an owner decision for each diagnostic, not a blanket rule.

## 15. Tooling

No layer below was changed.

| Capability | Layers that already exist | Layers that would be new if approved |
| --- | --- | --- |
| Union member calls | Metadata import of ordinary types | None for ordinary calls |
| Union conversions and patterns | Records, `Select Case`, conversions | Binder conversions, patterns, flow, diagnostics, semantic model, IOperation. `IsUnion` is a public symbol fact |
| Closed inheritance | `Inherits`, accessibility, BC37319 | Import of `IsClosed`, one diagnostic, exhaustiveness only if approved |
| Extension indexer consumption | Extension property lookup, default properties | Default-property collection for `PEExtensionPropertySymbol`, including interfaces |
| Collection arguments | Collection expressions, `New`, initializers | A new element form, which is not recommended |
| Static interface members | VB-110 parse, bind, emit, runtime-feature check | A net47 test, not a new layer, unless the probe finds a hole |
| Named Exit / Continue | Exit, Continue, labels, flow | Parser, binder, flow, CFG, EnC, debugger |
| Memory safety | Pointer metadata symbol, parked | Not recommended |

IntelliSense, formatting, refactorings, the expression evaluator, and Edit and Continue need no C# 15 work to keep today's VB programs working. They would follow any later approved language change under the usual feature-completeness rule. The open evaluator and Edit and Continue items in the release plan are unchanged by this assessment.

## 16. Desktop and server

| Gap | Desktop and server role |
| --- | --- |
| Union-aware language behavior | Cross-language library candidate. Not a Windows Forms, WPF, or console blocker. |
| Closed-hierarchy diagnostic | Cross-language candidate. Applications use descendants. |
| Extension indexer default lookup | Mixed-solution candidate. |
| Collection `with` arguments | Optional. Ordinary `New` covers capacity and comparers. |
| Static interface members on older runtimes | Relevant to .NET Framework 4.7 libraries. Source says the ban is already lifted. Verify before calling it done. |
| Named Exit / Continue | Optional readability. |
| Memory safety | Deferred preview. |
| .NET 11 library APIs | Available through the target framework. Not compiler work. |
| Razor and MAUI | No implementation. None of these gaps is a reason to start either stack. |

No C# 15 syntax gap is classified as a desktop release blocker. The release plan's unfinished desktop items stay unfinished. This document does not change their acceptance criteria.

What "contemporary VB on .NET 11" requires, from this assessment: the language can target current and older frameworks, consume ordinary and already-accepted modern metadata, and interoperate with C# libraries without adopting C# spelling. That bar is met for the accepted C# 13 and C# 14 surface, with the verification limits in section 12. It is not met for union-aware and closed-hierarchy-aware behavior, or for C# extension indexers used as the default member. Those three are the modernization residue. They do not, by themselves, make the desktop milestone unfinished, and finishing them is not what finishes the milestone.

## 17. Proposed sequence

No identifiers, phases, slices, or waves.

1. Owner reviews this assessment.
2. If authorized, the executable plan in the evidence directory. Fresh provenance. No reuse of the Introduce Parameter Debug Features assembly or the historical Release compiler as the proof.
3. Interoperability corrections that the probe confirms: closed-class metadata, then C# extension indexers as default properties.
4. Decide union consumption semantics. Authoring stays a later design study.
5. Older-runtime static interface probe. Correct only a confirmed extra ban.
6. Leave collection arguments, named `Exit` / `Continue`, and memory safety deferred.

Complexity, as a planning estimate, not a commitment: closed-class recognition is small; extension-indexer lookup is small to medium because interface receivers and cross-assembly VB properties are part of the same lookup; union conversions and exhaustiveness are large; native union declarations are a design project of their own.

## 18. Mission drift

The recommended direction advances desktop and server work by ranking interop and older-target correctness above new spellings. It preserves VB keywords, 17.13, and explicit `vbnext` for anything that would change binding or add syntax. It protects .NET Framework 4.7 by keeping runtime checks on `RuntimeFeature` and by refusing new syntax that needs a .NET 11 type. It keeps framework APIs out of the grammar. It does not copy `union`, `closed`, `with(...)`, or `break`. It adds no runtime dependency for the recommended corrections. Razor and XAML are untouched. UnisonCore is untouched. The canonical mission object was not edited.

No recommended item fails those tests. A native `Union` or `closed` declaration approved without the design questions, or a `with` element, or an implementation of the memory-safety preview, would fail them. Those are not recommended.

## 19. Preserved IDE candidates

These remain separate. This assessment did not investigate or implement them, and it does not treat their deferral as a fix.

- Parameter used as a loop control variable.
- Field used as a loop control variable.
- General mutation and expression relocation.
- Unqualified call-site expressions.
- Anonymous types or delegates incorrectly typed as `Object`.
- Global-qualified tuple conversion.
- Introduce Variable all-occurrences.
- Wave 34 environment-blocked Edit and Continue verification.
- VB-175 `ITuple` positional patterns.

## 20. Limitations

- No build, restore, test, or executable probe ran.
- Upstream `dotnet/roslyn` after `e4e2833` (2026-10-19) was not diffed. The commit list on 2026-11-27 shows `main` has moved.
- "Likely" member access and the BC30367 / BC30547 / BC37319 expectations are source hypotheses.
- Historical wave probes were not rerun, so C# 13 and C# 14 rows are not freshly executed interoperability proof.
- The November 25 documents are identified and reconciled. They are not the current implementation record.
- Feature identifiers, phases, slices, and Wave 37 were not assigned.

Evidence, written beside this document and outside the git commit:

- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-COMPATIBILITY-ASSESSMENT-REPORT.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-FEATURE-COMPATIBILITY-MATRIX.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-INTEROPERABILITY-AND-FRAMEWORK-MATRIX.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-PROPOSED-VERIFICATION-PLAN.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-OWNER-DECISION-MATRIX.md`

## 21. Executable verification

The owner accepted sections 1 through 20 and authorized verification only. The historical status of those sections remains the static assessment. This section does not replace their findings. It records what the compilers did. Production source, test source, Features, and Workspaces were not modified. No permanent test was added. No syntax, feature identifier, phase, slice, or wave was assigned. Wave 37 stayed closed. The five static evidence files were not edited.

Verification baseline: `vbnext/main` at `8fd88e08e66986d1cb54532aaee5e3056dcea653`. Compilers were built Release for net10.0 from that tree. `csc` and `vbc` exited 0. Reported version: `5.13.0-dev (<developer build>)`. Hashes and paths are in `CSHARP15-DOTNET11-EXECUTED-ARTIFACT-PROVENANCE.md`. The SDK 11.0.100 compiler was used only as a union-producer control and as the compiler of a symbol harness that loaded the in-tree assemblies. It is not the in-tree compiler.

Upstream after the October 19 merge was reviewed in a bounded way and was not imported. The relevant compiler files for unions and extension grouping had no later commits. A later cast-simplifier change and a later collection-inference change exist upstream and are not in this fork. In-tree results and released-SDK results are distinguished where both were run. The union member dump from the SDK producer matched the in-tree dump.

Language versions. 17.13, default, and latest agreed on ordinary union consumption, the BC37319 rejection, the extension-indexer rejection, and the Shared-interface-body rejection. vbnext is required for named extension lookup, target-typed collection expressions, partial properties, and plain Shared interface members. Union conversions stay rejected on vbnext.

Ordinary union consumption. Constructors and `Value` run for reference, value, and generic unions on 17.13, default, latest, and vbnext. A net10 consumer of a producer that embeds the attribute also runs. `HasValue` and `TryGetValue` are not generated. VB `IsUnion` is false and the case list is empty. C# reports the cases. Implicit assignment, `CType`, and `Select Case` on the union are BC30311. `TypeOf` on `Value` works. There is no exhaustiveness diagnostic. Native union declarations were not added.

Closed hierarchies. Permitted descendants construct, assign, and call. Prohibited inheritance is BC37319 on 17.13, default, latest, and vbnext, including a generic closed base and a nested closed type. C# reports CS9382. The diagnostic is the existing unsupported-feature diagnostic. It prevented the invalid inheritance in the cases that were compiled. VB `IsClosed` is false. That is recognition and diagnostic quality, not an enforcement failure in those cases. The feature-required rejection was not removed.

Extension members. Named C# extension getters and setters run on vbnext, including interfaces, generic arrays, value types, evaluation order, instance precedence, imports, and accessibility. Default access is BC30367 for a class without a default member and BC30547 for an interface. 17.13 rejects named access with BC30456. The same-compilation VB indexer prints `hi`, and the existing test `ExtensionIndexer_ReceiverAndIndexArePassed` passed (1 passed, 0 failed, exit 0). A VB extension library emits static accessors and an assembly `ExtensionAttribute`, without the C# grouping type. Another VB assembly, on vbnext and on 17.13, reports BC30456 for the indexer and for a non-indexed property. C# does not bind them either. That gap is separate from default lookup.

The later architecture assessment of that gap is [vbnext-extension-metadata-architecture.md](vbnext-extension-metadata-architecture.md). It does not replace this section. The VB reader already understands the C# grouping contract. The owner accepted that assessment and approved Alternative A under existing VB-039: emit the grouping contract, keep the static accessor signatures, and do not add a second public property. Ordinary extension methods are not part of the gap. Default `box(index)` lookup through a metadata reference and the `CompilationReference` language-version discrepancy stay separate. The correction is implemented. A later verification closeout compared the retained pre-implementation compiler with the published compiler. Static accessor signatures match, and an unchanged C# caller ran against both producer images. Formal owner acceptance is still withheld. VB-039 is not closed. Named consumption and the C# indexer succeed. VB `box(index)` through a metadata reference and the `CompilationReference` language-version discrepancy remain open. A present `ExtensionMarkerAttribute` with an unusable constructor is omitted by Visual Basic, while C# reports CS0656 and does not emit. The reduced public nullable annotation remains `None` while the accessor annotations are preserved. The closeout record is `D:\Development\VBNext\Evidence\LanguageCompletion\VBNEXT-EXTENSION-METADATA-VERIFICATION-CLOSEOUT-REPORT.md`. The implementation record remains `VBNEXT-EXTENSION-METADATA-IMPLEMENTATION-REPORT.md`.

Collections. C# `with` ran for a capacity, a custom constructor, a builder argument, a spread, and evaluation order. VB `New List(Of T)(capacity) From` and a dictionary comparer ran on net11 and net472. vbnext target-typed parentheses assigned to a list ran; 17.13 treats that expression as a tuple. Dictionary `with` element syntax was not successfully compiled here. No `with` element was added. The executed constructions are not a claim of complete builder equivalence.

Static interface members. A plain Shared method, sub, and property with a body compile on vbnext against net11, net472, and net48, and the calls run, including both directions and a second VB assembly. Metadata for the plain method is static, not virtual, and not abstract. 17.13, default, and latest reject the body with BC30603. net47 reference assemblies are not installed. The net472 executables ran on CLR 4.8.10008.0, which does not establish the original 4.7 runtime. C# rejects static abstract, static virtual, instance default interface methods, and protected static interface members on net472. VB `Shared MustOverride` compiled against those same references and is static abstract. That is a confirmed negative-control defect. The plain Shared path was not changed.

Other probes. Nearest `Exit` and `Continue`, `GoTo`, and `Finally` behaved as today's language. A C# labeled `break` compiled as a capability control. Memory-safety preview metadata was present on an ordinary method, and VB 17.13 and vbnext called it. Partial default properties run on vbnext and are BC30639 on 17.13. Index-from-end on a typed array and a `List` ran. Expression trees with an omitted optional argument match the C# control. An async entry point ran. An ordinary 17.13 program ran on net11 and net472. Small Windows Forms and WPF inheritance compiles succeeded and are not full desktop compatibility.

Confirmed gaps, in the order recommended if the owner later authorizes work: cross-assembly VB extension metadata; C# extension-indexer default lookup, preserving 17.13; a runtime check for static abstract or virtual interface members, without changing plain Shared methods; union metadata recognition; union conversions, patterns, and exhaustiveness only under later decisions; a design assessment for native unions; a design assessment for named `Exit` and `Continue`; closed-type symbol and diagnostic wording while the current rejection remains. No C# 13 or C# 14 completeness correction was confirmed by the residues that ran. Collection syntax stays deferred. The IDE candidates in section 19 were not investigated.

Limitations that are not passes and not defects: net47 references, exact 4.7 execution, .NET Standard, `/refout` reference assemblies, `Await Each`, extension methods and operators in this command-line setup, and the dictionary `with` element. Upstream changes after October 19 were not merged, so in-tree compatibility is not a claim about every later `dotnet/roslyn` commit.

Evidence for this section, outside the repository:

- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-EXECUTABLE-VERIFICATION-MATRIX.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-EXECUTED-ARTIFACT-PROVENANCE.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-EXECUTED-INTEROPERABILITY-AND-FRAMEWORK-MATRIX.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-CONFIRMED-GAP-AND-CORRECTION-CANDIDATES.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-POST-VERIFICATION-OWNER-DECISION-MATRIX.md`
- `D:\Development\VBNext\Evidence\LanguageCompletion\CSHARP15-DOTNET11-EXECUTABLE-COMPATIBILITY-VERIFICATION-REPORT.md`
