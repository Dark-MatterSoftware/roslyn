# LC-1G nullable integration plan

The owner approved this plan on November 26, 2026. The owner formally accepted LC-1G.1 on November 26, 2026, including its documented limitations. On November 27, 2026, at 02:30:28 UTC, the owner formally accepted LC-1G.2, including LC-1G.2a and its documented limitations, and authorized LC-1G.3 only. The acceptance record is in [vbnext-vb030-syntax.md](vbnext-vb030-syntax.md). The accepted LC-1G.1 compiler baseline is `613f195758d5cdd19d932d244bf1254ee59b2e2b`. The accepted LC-1G.2 compiler baseline is `6b937cd7cbcb283d5abe4eeb8c492ccd0858b766`. Accepting LC-1G.2 does not complete VB-030 and does not accept LC-1G.3. On November 27, 2026, at 02:59:36 UTC, the owner formally accepted LC-1G.3, including its documented limitations, and authorized LC-1G.4 implementation only. The accepted LC-1G.3 verification baseline is `1a8941af87fb9c48025566550132697547e81b78`. The technical review is `LanguageCompletion/LC-1G.3-FRAMEWORK-REFLECTION-COMPATIBILITY-REPORT.md`, SHA-256 `51C43C7C887CA47297C594CC08A4578EC6BEBD8A093756AFA23F38046789D912`. LC-1G.3 did not modify production compiler code. LC-1G.5 through LC-1G.7 remain unauthorized. Accepting LC-1G.3 does not complete VB-030 and does not accept LC-1G.4.

The historical LC-1G.2a evidence report, section 20, names LC-1G.5 as `NullablePublicOnly` and LC-1G.6 as IDE and analyzer integration. That numbering is not this plan. The approved sequence is LC-1G.1 internal symbol integration, LC-1G.2 metadata import and emission, LC-1G.3 framework and reflection compatibility, LC-1G.4 public Roslyn nullable APIs, LC-1G.5 IDE, analyzer, and diagnostic integration, LC-1G.6 Edit and Continue, and LC-1G.7 consolidated acceptance. `NullablePublicOnly` stays deferred and is not assigned to a slice. The historical report is not rewritten.

The owner accepted the LC-1F flow implementation the same day. The acceptance record, the locked flow policies, and the accepted conservative limitations are in [vbnext-vb030-syntax.md](vbnext-vb030-syntax.md). Slice 10f in [vbnext-phases.md](vbnext-phases.md) points here. This plan does not reopen LC-1F and does not reconcile LC-4.

The accepted compiler baseline is `5c6c95fb7ae92a6abafa2628f610eecd7befe8a9`. The final LC-1F.6d implementation commit is `710bdbc87a5ce2a5d152af20d9001ec1e4acb877`. The technical review is `LanguageCompletion/LC-1F.6d-TARGETED-FLOW-CORRECTNESS-CLOSEOUT-REPORT.md`, SHA-256 `65249FCC29849BD4EB9B79256CC2886A6558949269F2B327B11F2FB5698982D9`.

VB-030 is not complete. Accepting nullable flow does not make public APIs, metadata, framework reflection, or IDE integration complete.

## Locked policies

These stay in force for every slice:

- Inferred method type arguments keep annotations from the contributing arguments, including a reduced extension receiver and a nested type argument. The existing combination rule stays: an annotated contribution wins, a non-null result requires every contribution to be non-null, and an oblivious contribution blocks a non-null guarantee. Inferred CLR types, overload selection, and runtime method identity stay as they are. A currently non-null argument does not prove that a method returning `T` returns non-null.
- An ordinary caller uses the declared contract. The callee body is checked on its own. A recursive cycle does not prove its own body from those declarations.
- `On Error` and `Resume` keep today's lowering. Handler invalidation stays conservative.
- `NotNull` wins over `MaybeNull` on the same output, in either attribute order. Metadata encoding of the attributes stays. A return-targeted `NotNull`, including `As <NotNull> T`, refines the result on normal completion. The earlier generic `NotNull` concern stays closed as a fixture mistake.

## Accepted limitations that this plan does not remove

Sticky handler taint after a later non-null write or inside a later null check. Local-name matching when handler symbols are not the same object. Incomplete recursive base-case inference. Incomplete member initialization through aliases. Conservative nested-condition diagnostics. Custom-setter invalidation. Possible-alias invalidation. Loop widening. Absent `DoesNotReturnIf` body verification. Accessor-only attribute interpretation. BC36907 on an explicit type argument supplied to an already fully reduced extension method.

A missing warning found while implementing a slice is a new defect. Record it and escalate it. Do not classify it as one of these limitations, and do not reopen LC-1F automatically.

## What the source already does

Verified by reading the tree at the accepted baseline. No compiler build was run for this plan.

Friend annotation storage already exists for source, metadata, and substituted symbols:

- `NamedTypeSymbol` stores a type-argument annotation array. `INamedTypeSymbol.TypeArgumentNullableAnnotations` returns it, or `None` for each argument when the array has not been set.
- `ConstructedMethod` stores a method type-argument array behind `GetTypeArgumentNullableAnnotations` and `SetTypeArgumentNullableAnnotations`. `SubstitutedMethodSymbol.GetReturnNullableAnnotation` substitutes the original return annotation. Overload resolution stamps the array after `Construct`. `Construct` on the specialized generic method allocates a new constructed method. That constructed method's `Equals` and `GetHashCode` compare CLR type arguments and do not compare the annotation array.
- Source, PE, and substituted methods, parameters, fields, and properties override the friend nullable getters. `NullableAnnotationSubstitution` copies annotations through containing-type and method substitution, including array element annotations.
- `ArrayTypeSymbol.ElementNullableAnnotation` is stored and is what `IArrayTypeSymbol.ElementNullableAnnotation` returns.
- `NullableMetadataDecoder` walks nullable bytes and returns the original type with `None` when the walk fails or the byte cursor does not match. `NullableMetadataEncoder` walks nested type arguments and array elements. For a tuple it walks `TupleUnderlyingType` rather than the tuple facade.
- Flow in `NullableFlowAnalysis` reads return, parameter, field, and type-argument annotations. For a reduced extension call it derives the annotation from the receiver expression and the unreduced signature. That derivation is the accepted LC-1F.6d behavior.

Verified gaps:

- `ReducedExtensionMethodSymbol` does not override a nullable getter. Reduction infers CLR types through a `BoundRValuePlaceholder` and stores the fixed types on the reduced symbol. `GetReturnNullableAnnotation` therefore stays the `MethodSymbol` default, `None`. The public method type-argument getter does not read the friend array.
- No file under `Symbols/Retargeting` reads or copies a nullable annotation. A retargeted method, parameter, field, property, or event uses the base getter. Tuple retargeting rebuilds the tuple with `WithUnderlyingType` and does not mention annotations.
- No file under `Symbols/Tuples` sets a nullable annotation. The encoder's use of `TupleUnderlyingType` is the retained long-tuple and tuple-element obligation, not a new defect discovered as a missing warning.
- `ITypeSymbol.NullableAnnotation` returns `None` for every type. `ITypeSymbol.WithNullableAnnotation` returns the same symbol. `IMethodSymbol.TypeArgumentNullableAnnotations` returns `None` for every argument and ignores the friend array. `IMethodSymbol.ReceiverNullableAnnotation` returns `None`. `IEventSymbol.NullableAnnotation` returns `None`.
- `IMethodSymbol.ReturnNullableAnnotation`, `IParameterSymbol.NullableAnnotation`, `IFieldSymbol.NullableAnnotation`, and `IPropertySymbol.NullableAnnotation` call the friend getters, so source, PE, and substituted symbols already surface those values. Reduced and retargeted symbols do not, because they never override the getters.
- `ITypeParameterSymbol.ReferenceTypeConstraintNullableAnnotation` is overridden for PE and substituted type parameters. No source type-parameter override was found. VB has no approved `Class?` constraint syntax, so LC-1G does not add one.
- `VisualBasicTypeInfo` converts to public `TypeInfo` with both nullability values set to nothing. `src/Features/VisualBasic`, `src/Workspaces/VisualBasic`, and `src/Compilers/VisualBasic/Portable/Emit/EditAndContinue` contain no `NullableAnnotation` references.
- `SubstitutedNamedType.Equals` compares type-argument symbols and does not compare the side annotation array. `SymbolEqualityComparer.IncludeNullability` therefore cannot see a VB annotation that lives only in that array. C# stores the corresponding fact on `TypeWithAnnotations` and its public method wrapper returns `TypeArgumentsWithAnnotations.ToPublicAnnotations()`.

Hypothesis, not a verified defect: two constructed methods that compare equal can still be distinct objects, because the `Construct` path that was read allocates a new symbol and then stamps it. A later cache that interns by `Equals` could collapse two annotation states. LC-1G.1 has to check the construction caches before it treats the friend array as stable.

`PrebuiltContracts_DecodeAcrossFrameworks_WhenEvidenceIsPresent` skips, with an explicit reason, when `VBNEXT_LC1A_CONTRACTS` is unset and its default evidence directory is absent. A skip is not framework acceptance. An explicitly configured missing directory fails. Exact framework-matrix acceptance remains LC-1G.3. Exact .NET 11 reference assemblies are not part of the emit test project. The net472 `NullabilityInfoContext` test-project failure remains the retained framework-test obligation. Reflection checks already exist in `NullableDeclarationTests` and `NullableMetadataEmissionTests` for the shapes those tests emit.

## Extension annotations

LC-1F.6d corrected the call-site flow result. It did not put an annotation on `ReducedExtensionMethodSymbol`. LC-1G needs that propagation before a public API, a semantic model, or a retargeted reduced method can agree with the warning.

The dependency is:

1. Keep the accepted flow derivation. Do not replace it with a second inference.
2. Decide which reduced and retargeted symbols must store the same annotation the flow derivation already computed, including a nested generic return and a method type argument fixed from the receiver.
3. Only then may `IMethodSymbol.TypeArgumentNullableAnnotations` and `ReturnNullableAnnotation` read that storage. Returning the friend array from every method symbol would still report `None` for a reduced method that never received the array, and it would still ignore a retargeted method.

## Public API boundary

LC-1G corrects existing Roslyn contracts. It does not add a public signature. `Construct`, symbol identity, CLR identity, and overload ranking stay as they are.

C# is the reference for how those existing contracts behave. It is not a reason to change the VB flow policies above. A slice that needs a new public member stops and escalates.

`INamedTypeSymbol.TypeArgumentNullableAnnotations` and `IArrayTypeSymbol.ElementNullableAnnotation` already return stored values. An API is not missing merely because a neighboring getter returns `None`.

## Obligation inventory

Counts are the retained items below. They are not test counts.

| Area | Count | Disposition |
| --- | --- | --- |
| Internal symbols | 7 | LC-1G.1 |
| Metadata | 10 | LC-1G.2 |
| Framework and reflection | 7 | LC-1G.3 |
| Public Roslyn APIs | 12 | LC-1G.4, after the internal facts those APIs must read |
| IDE, analyzers, and diagnostics | 6 | LC-1G.5 |
| Edit and Continue | 1 | LC-1G.6 |
| Cross-language gaps that are not already a row above | 4 | Covered inside LC-1G.2 and LC-1G.4, then checked again in LC-1G.7 |
| Consolidated acceptance | 1 | LC-1G.7 |

Internal symbols:

1. Retargeting annotation preservation.
2. Constructed generic type annotations, including a retargeted constructed type.
3. Constructed generic method annotations, including the friend array's identity and any cache that shares the symbol.
4. Reduced extension method annotations, without replacing the accepted flow derivation.
5. Substitution consistency between `NullableAnnotationSubstitution` and retargeting.
6. Annotation identity: `Equals` and `GetHashCode` stay CLR identity unless an existing comparer is specified to include nullability, and that comparer must then be able to see the stored annotation.
7. Source and imported symbols must agree when they describe the same contract.

Metadata:

1. Long-tuple `Rest` reconstruction.
2. Tuple element annotations through `TupleUnderlyingType`.
3. Nested constructed generic annotations.
4. A portable conditional-import test that does not depend on a machine evidence directory.
5. Corrupt and malformed metadata, locking the decoder's oblivious result or a deliberate change to it.
6. Embedded `NullableAttribute` identity.
7. An incompatible predefined `NullableAttribute`.
8. Netmodule nullable metadata.
9. Cross-assembly preservation, including retargeting.
10. Source versus metadata contract consistency.

Framework:

1. Exact modern .NET nullable attribute behavior.
2. Exact .NET Framework 4.7 attribute compilation and reference behavior.
3. Compiler-generated attributes only where the target does not already supply a compatible definition.
4. The previously recorded .NET Framework 4.7.2 `NullabilityInfoContext` test-project failure.
5. Reflection interpretation of emitted metadata.
6. Framework-specific compiler-contract availability. Analysis must not require a modern framework API.
7. Older-target fallback, including the retained exact .NET 11 reference check. Reading a net11 image against net10 references is not that check.

Public APIs:

1. `ITypeSymbol.NullableAnnotation`.
2. `ITypeSymbol.WithNullableAnnotation`.
3. `INamedTypeSymbol.TypeArgumentNullableAnnotations`, already wired to stored type arguments.
4. `IMethodSymbol.TypeArgumentNullableAnnotations`. LC-1G.4b maps a reduced method to its public arity.
5. `IMethodSymbol.ReturnNullableAnnotation` and `ReceiverNullableAnnotation`.
6. `IParameterSymbol.NullableAnnotation`.
7. `IFieldSymbol.NullableAnnotation` and `IPropertySymbol.NullableAnnotation`.
8. `IEventSymbol.NullableAnnotation`.
9. `IArrayTypeSymbol.ElementNullableAnnotation`, already wired.
10. `ITypeParameterSymbol` constraint annotations, without new constraint syntax.
11. Public `TypeInfo` nullability from `VisualBasicTypeInfo`. Implemented and technically verified by LC-1G.4c. LC-1G.4c.2 corrected explicit and inferred nullable lambda parameters. Not owner-accepted.
12. `SymbolEqualityComparer.IncludeNullability` and the existing analyzer-facing contracts. No new public signature.

Tooling:

1. IDE consumption of nullable annotations.
2. Analyzer consumption.
3. Semantic model consistency with the public annotations.
4. Diagnostic type display.
5. Display of annotations in applicable tooling.
6. Existing code fixes and refactorings where a changed annotation would change their behavior.

Edit and Continue: nullable annotation behavior across an EnC delta.

Cross-language gaps still to cover, using the historical LC-1F.5 and LC-1F.5a results as the tested baseline rather than as a new run:

- Already reported: a C# method base consumed by VB, a VB base consumed by C#, VB metadata reimported by VB, and a C# interface implemented by VB.
- Not established by those reports: a VB interface implemented by C#, public annotations on a reduced extension across VB and C#, long-tuple element round trip, and public method type-argument annotations on an imported constructed method.
- Intentional policy, not an interoperability defect: `NotNull` winning over `MaybeNull`, accessor-only attributes staying out of flow, and BC36907.

## Sequence

The provisional order stands. One dependency changes how LC-1G.4 starts.

LC-1G.1 comes first. Public API work cannot begin by returning the existing method type-argument array from every symbol. Reduced and retargeted symbols do not have that array. LC-1G.4 therefore follows the internal storage work, and it follows any LC-1G.2 change that alters what an imported symbol stores.

LC-1G.2 follows LC-1G.1. Tuple, malformed-metadata, netmodule, and embedded-attribute work read the encoder and decoder directly, but they should use the annotation identity LC-1G.1 settles so a retargeted or reduced symbol is not emitted one way and imported another.

LC-1G.3 follows LC-1G.2 when the bytes change. The existing reflection tests can be extended there. The net472 test-project failure is investigated in that slice, not fixed by taking a dependency on a newer framework API.

LC-1G.5 follows the public and semantic facts it displays. LC-1G.6 follows LC-1G.1 because EnC matches symbols, and it follows LC-1G.4 if the delta is observed through the public APIs. LC-1G.7 is last.

No production slice is independent of LC-1G.1. LC-1G.5 and LC-1G.6 are independent of each other once LC-1G.1 and LC-1G.4 are done. LC-1G.3 is independent of LC-1G.5 and LC-1G.6.

The first implementation slice is LC-1G.1. The flow warning is already accepted, and the remaining inconsistencies are symbol storage, retargeting, and reduction. Later APIs and metadata have to read that storage.

## LC-1G.1 — Internal symbol integration and retargeting

Classification: compiler correctness.

Objective: store one annotation result for constructed, substituted, reduced, and retargeted symbols, using the annotations LC-1F already computes.

Obligations: the seven internal items. The public getters stay behavior-compatible in this slice except where a test reads a friend annotation. Do not switch `IMethodSymbol.TypeArgumentNullableAnnotations` to the friend array here.

Source: `ReducedExtensionMethodSymbol.vb`, `RetargetingSymbolTranslator.vb` and the retargeting symbol types, `SubstitutedMethodSymbol.vb`, `NullableAnnotationSubstitution.vb`, `NamedTypeSymbol.vb`, `OverloadResolution.vb` where it stamps method annotations, and the accepted derivation in `NullableFlowAnalysis.vb`.

Current state: substitution and constructed-method storage exist. Reduction and retargeting drop annotations. Method equality ignores the annotation array.

Production change: copy annotations across retargeting and reduction, including a nested return and a type argument fixed from an extension receiver. Check construction caches before sharing a stamped symbol. Leave CLR types, overload selection, lowering, and the accepted flow derivation in place.

Tests: extend `NullableGenericMethodAnnotationTests` and `NullableFlowTargetedCloseoutTests` with friend-level or semantic assertions on a reduced method, a retargeted method, and a constructed method whose annotations differ. Add the missing negative case where an oblivious argument blocks a non-null inference. Capture a failing-before log on the accepted baseline before editing production code. A filter that matches nothing is a failed run.

Legacy: `default` and `latest` stay on Visual Basic 17.13, and a nullable-disabled `vbnext` compilation does not gain annotations.

Cross-language and framework: not this slice, except that retargeting must not change IL. Compare IL for one reduced extension call and one ordinary generic call against Visual Basic 17.13.

Public API: confirm the public method type-argument getter still returns `None` at the end of this slice, so the API change remains LC-1G.4.

Resources: one compiler slice and the Visual Basic emit tests on `net10.0`. No full solution build. Stop if C: is below 10 GiB free, and do not start the build at or below 12 GiB free without authorization.

Dependencies: none. This slice blocks the others.

Escalation: a need to change `Equals` for all symbols, to change overload ranking, or to add a public API. A missing warning in the accepted flow fixtures.

Acceptance: reduced, retargeted, and constructed symbols preserve the annotation flow already warns about. The accepted LC-1F.6d extension and handler tests still pass. IL for the representative methods matches Visual Basic 17.13.

LC-1G.1 implementation, accepted on November 26, 2026: a reduced extension method stores an original-arity annotation array. Type parameters fixed by the receiver stay at their original ordinals and are not free parameters of the reduced method. Remaining free parameters stay on the reduced method, and a later construction merges those annotations back by `ReducedFrom`. The call stamps the array from the receiver and the ordinary arguments. The accepted flow derivation remains. Retargeting copies constructed type-argument annotations, array element annotations, and a tuple facade when a new underlying type is created. Retargeted methods, parameters, fields, properties, and type-parameter constraint annotations forward the underlying values. Events still have no annotation field, and `IEventSymbol.NullableAnnotation` stays `None`. Event binding uses the existing declaration capture, so a constructed event type such as `MessageHandler(Of String?)` stores the type-argument annotation and retargeting forwards it. Event metadata emission and decoding remain LC-1G.2. Long-tuple `Rest` and tuple metadata emission stay LC-1G.2. Constructed and substituted symbols already kept distinct annotations; equality and hashing were not changed, and no annotation cache collision was reproduced. A class constraint does not keep a reduced return non-null by itself. `As T` with `T As Class` captures `NotAnnotated` at the declaration, and a nullable type argument substitutes to `Annotated` on the reduced and unreduced calls. `As T?` stays `Annotated`. `As <NotNull> T` is a postcondition: the substituted annotation follows the type argument, and the reduced method forwards `ReturnTypeFlowAnalysisAnnotations` so the contract still suppresses the caller warning. Public `IMethodSymbol.TypeArgumentNullableAnnotations` still returns `None`. Existing public return and parameter getters follow the friend getters, so a reduced or retargeted symbol can now report an annotation those getters previously returned as `None`. No public signature was added.

LC-1G.1a verification, included in the November 26, 2026 acceptance: four concerns were checked against the published LC-1G.1 tree. A class constraint is a reference-type declaration rule, not a non-null result guarantee, and it is distinct from an explicit `NotNull` postcondition. Distinct extension lookups already allocate distinct reduced symbols. One reduced definition no longer bakes a call-specific annotation into a return type shared by later constructions; each construction keeps its own array. Event declarations capture nested type-argument annotations through the existing declaration binder. The public event annotation stays `None`. No event metadata encoder or decoder change was made. No equality, cache, syntax, constraint-policy, or public-signature change was made.

Exclusions: public API exposure, metadata byte changes, IDE, EnC, and every accepted flow limitation.

## LC-1G.2 — Metadata import and emission

Classification: metadata interoperability. Implemented. Not owner-accepted.

Objective: round-trip the stored annotations through the existing nullable metadata attributes.

Obligations: the ten metadata items, plus event type metadata and a nontrivial imported reference constraint through retargeting.

Source: `NullableMetadataEncoder.vb`, `NullableMetadataDecoder.vb`, `PEModuleBuilder.NullableMetadata.vb`, `TupleTypeDecoder.vb`, `TupleTypeSymbol.vb`, `Binder_Symbols.vb` `LookupTupleType`, `SourceEventSymbol.vb`, and `PEEventSymbol.vb`.

Current state: owner-accepted on November 27, 2026, at 02:30:28 UTC, including the LC-1G.2a closeout and its documented limitations. Source tuple elements stamp logical annotations onto the `ValueTuple` chain, including `Rest`. Import copies those annotations when tuple reconstruction builds a new chain. An event emits and decodes `NullableAttribute` on the event, and `IEventSymbol.NullableAnnotation` stays `None`. A predefined `NullableAttribute` that is present but missing either standard constructor reports `ERR_MissingRuntimeHelper` and is not replaced by an embedded definition. A missing predefined type is still embedded. A netmodule declaration keeps a nested annotation. `default`, `latest`, and Visual Basic 17.13 do not emit the attribute for a declaration that stores no VB.Next annotation. Representative method bodies match when only reference annotations differ. The external LC-1A contract images are an optional acceptance fixture: the ordinary suite skips them when they are absent, and `VBNEXT_LC1A_CONTRACTS` makes a missing directory a failure.

Production change: capture tuple element annotations while binding, carry them across `Rest` reconstruction, decode event metadata with the existing transform, and report the existing missing-runtime-helper diagnostic when a predefined `NullableAttribute` lacks a standard constructor. No new metadata format, diagnostic, syntax, or public signature.

Tests: `NullableMetadataRoundTripTests`, `NullableMetadataCorrectnessCloseoutTests`, `NullableMetadataImportTests`, and `NullableMetadataEmissionTests`. The prebuilt framework-image test skips when its evidence directory is absent and `VBNEXT_LC1A_CONTRACTS` is unset. A configured missing directory fails. Exact framework-matrix acceptance remains LC-1G.3.

Legacy: Visual Basic 17.13, `default`, and `latest` emit no new nullable attributes. `NullablePublicOnly` stays deferred.

Cross-language: VB to VB, VB to C#, and C# to VB for a nested generic, an eight-element tuple, and an event. A `NotNull` postcondition stays independent of a substituted annotated return.

Framework: older-target embedding remains the existing `Mscorlib40` emit tests. Exact .NET Framework 4.7, modern .NET, and .NET 11 reference acceptance remains LC-1G.3.

Public API: no signature was added. Imported named type-argument annotations and return annotations use the existing getters. `IEventSymbol.NullableAnnotation` and `IMethodSymbol.TypeArgumentNullableAnnotations` stay `None`. A tuple facade still has arity 0, so its public type-argument array stays empty; the logical element annotations live on the underlying chain.

Resources: emit, symbol, and semantic tests on `net10.0`. No full solution build.

Dependencies: LC-1G.1, accepted on November 26, 2026.

Escalation: none taken. A new attribute shape, a C# metadata-reader change, and a new malformed-metadata diagnostic were not required.

Acceptance: recorded on November 27, 2026, at 02:30:28 UTC, in [vbnext-vb030-syntax.md](vbnext-vb030-syntax.md). The accepted compiler baseline is `6b937cd7cbcb283d5abe4eeb8c492ccd0858b766`. The original implementation is `440c44bb68ae20091da0aab62ec5673e5e898a67`. The technical review is `LanguageCompletion/LC-1G.2a-METADATA-CORRECTNESS-CLOSEOUT-REPORT.md`.

Exclusions: `NullablePublicOnly`, IDE display, public nullable API exposure, and flow-policy changes.

## LC-1G.3 — Framework and reflection compatibility

Classification: compatibility verification.

Objective: show the emitted contract against modern .NET, .NET Framework 4.7, and reflection, without making analysis depend on a modern framework API.

Obligations: the seven framework items.

Source: the embedded attribute symbol, `PEModuleBuilder.NullableMetadata.vb`, and the existing reflection facts in `NullableDeclarationTests` and `NullableMetadataEmissionTests`.

Current state: owner-accepted on November 27, 2026, at 02:59:36 UTC, including the documented limitations. The accepted verification baseline is `1a8941af87fb9c48025566550132697547e81b78`. `NullableFrameworkReflectionTests` compiles the representative contract against `microsoft.netframework.referenceassemblies.net47` 1.0.3 and against the installed `Microsoft.NETCore.App.Ref` 11.0.0 reference pack, whose `System.Runtime` identity is 11.0.0.0. The .NET 11 runtime 11.0.0 reads the emitted image with `NullabilityInfoContext`. The historical net472 emit-test build failure was the test project referencing `NullabilityInfoContext`, which that target does not provide. Those two facts now compile only for the modern test host, and the net472 emit test project builds with zero errors. Exact .NET Framework 4.7 CLR execution was not performed and is not claimed. Exact 4.7 reference compilation is not historical 4.7 CLR execution. The 4.7 pack does not contain `System.ValueTuple`, and no tuple dependency was added. Older-target inspection used a metadata reader. Individual older-target flow-attribute fixtures were not all repeated. A separate exact .NET 11 C# consumer matrix was not repeated. The prebuilt net11 image read against net10 references remains distinct from the exact .NET 11 verification. One intentionally stopped additional scan produced no result. `NullablePublicOnly` stays deferred. LC-1G.3 did not modify production compiler code.

Production change: only if a target embeds a duplicate attribute, misses a compatible existing attribute, or fails to load metadata that LC-1G.2 emits. Do not reference `NullabilityInfoContext` from the compiler.

Tests: compile the nullable attributes against the .NET Framework 4.7 reference assemblies and against the modern references the emit project already uses. Add an exact .NET 11 reference run only when those assemblies are available. Investigate the recorded net472 test-project failure and fix the test project or the emission mismatch that it demonstrates. If the .NET 11 references are absent, record that limit. Do not treat a skipped filter as a pass.

Legacy: Framework 4.7 output must load as ordinary IL. No new runtime helper.

Cross-language: C# reflection and VB reflection of the same image where `NullabilityInfoContext` is available on the test runtime.

Public API: unchanged in this slice.

Resources: targeted emit tests. A multi-targeting run needs the same disk gates as LC-1G.1.

Dependencies: LC-1G.2 for any byte change. The investigation of the existing failure can start from the LC-1G.2 output.

Escalation: a requirement to ship a modern-only framework API inside the compiler, or to drop Framework 4.7.

Acceptance: recorded on November 27, 2026, at 02:59:36 UTC, in [vbnext-vb030-syntax.md](vbnext-vb030-syntax.md). The accepted verification baseline is `1a8941af87fb9c48025566550132697547e81b78`. The technical review is `LanguageCompletion/LC-1G.3-FRAMEWORK-REFLECTION-COMPATIBILITY-REPORT.md`. The same sample contract is visible to reflection on the modern test runtime, the Framework 4.7 compilation emits a compatible attribute story, and the slice did not change production compiler code.

Exclusions: new language syntax and IDE work.

## LC-1G.4 — Public Roslyn symbol API integration

Classification: public API integration.

Objective: make the existing nullable properties return the annotations LC-1G.1 and LC-1G.2 stored.

Obligations: the twelve public API items, plus public annotations for a reduced extension and for an imported constructed method.

Source: the `Implements` getters on `TypeSymbol`, `MethodSymbol`, `EventSymbol`, `NamedTypeSymbol`, `ArrayTypeSymbol`, and `ParameterSymbol`. C# reference: `src/Compilers/CSharp/Portable/Symbols/PublicModel/MethodSymbol.cs`. Semantic conversion: `TypeInfo.vb`.

Current state: several getters are already honest. The type-level annotation, `WithNullableAnnotation`, the method type-argument array, the receiver annotation, and the event annotation are not. Public `TypeInfo` carries no VB nullability. Implementation stopped before a production change. A shared compiler type cannot carry one annotated use, and a public projection was not introduced. The evidence is `LanguageCompletion/LC-1G.4-PUBLIC-NULLABLE-API-INTEGRATION-REPORT.md`. That report does not accept LC-1G.4. On November 27, 2026, at 03:20:43 UTC, the owner approved Option A and authorized LC-1G.4a only. The architecture-stop baseline is `669b63edb3fc898558d7564b0af2fb07aae16089`. The projection is immutable, stays outside the compiler `TypeSymbol` hierarchy, and preserves specialized public interfaces, compiler re-entry, default equality, hash behavior, and CLR identity. A compiler type subclass was rejected. LC-1G.4b and LC-1G.4c subdivide this phase. Semantic-model flow integration is LC-1G.4c. LC-1G.4 is not complete. LC-1G.4a implemented `PublicTypeProjection` for named types, error types, arrays, and type parameters. `WithNullableAnnotation` now returns that projection. On November 27, 2026, at 04:04:57 UTC, the owner accepted LC-1G.4a, including its documented limitations, at `58b0360cc9cbde3e1af0ecc3d8e2f71db27bd8bf`, and authorized LC-1G.4b only. That acceptance does not accept LC-1G.4. LC-1G.4c remains unauthorized. The evidence is `LanguageCompletion/LC-1G.4a-PUBLIC-PROJECTION-ARCHITECTURE-PROOF-REPORT.md`. On November 27, 2026, at 13:55:57 UTC, the owner accepted LC-1G.4b.1, including its documented proof limitations, and permanently approved Option A for LC-1G.4b. That acceptance authorizes LC-1G.4b.2 only. LC-1G.4b.2 completed that closeout at `39bf619009098ea2f760d95c8b2e8d020ed66c5e`, with the reduced-receiver correction at `9731dc6e58353cfd5ecc75358a4bba41e860de16`. LC-1G.4b is technically ready for formal owner acceptance and is not owner-accepted. Public `TypeInfo` nullability remains LC-1G.4c. LC-1G.4c remains unauthorized. The evidence is `LanguageCompletion/LC-1G.4b.1-IDENTITY-CONSTRUCTION-ARCHITECTURE-RESOLUTION-REPORT.md`. On November 27, 2026, at 15:41:31 UTC, the owner formally accepted LC-1G.4b, including LC-1G.4b.2, LC-1G.4b.3, and their documented limitations, on published baseline `46674f536be83dc47ca00c66b1c3dae9ae07e4d3`. That acceptance authorizes LC-1G.4c only. It does not accept LC-1G.4c or LC-1G.4. LC-1G.5 through LC-1G.7, LC-4, and `NullablePublicOnly` remain unauthorized. Both LC-1G.2a observations remain open for LC-1G.7. LC-1G.4c is implemented and technically verified. It is not owner-accepted. Public `TypeInfo` nullability comes from the existing walker through the member semantic model. LC-1G.4 is technically complete and is awaiting owner acceptance of LC-1G.4c. LC-1G.4c.2 corrected the LC-1D lambda binding so `As String?` is a nullable reference and an inferred parameter keeps the delegate annotation. Internal `Unknown` on a reference remains public `MaybeNull` and still does not warn. LC-1G.4c is technically ready for formal owner acceptance and is not owner-accepted. LC-1G.5 through LC-1G.7, LC-4, and `NullablePublicOnly` remain unauthorized.

Production change: return stored annotations from the existing properties, including reduced and retargeted symbols. `WithNullableAnnotation` must return a symbol whose existing nullable property matches the request, or it must keep today's identity when the annotation did not change. Do not add a parameter to `Construct`. Do not put annotations into CLR equality.

Tests: a new public-API test class beside `NullableAnnotationTests`, covering positive and negative annotations for a local, a parameter, a return, a field, a property, an array element, a constructed type, a constructed method, a reduced extension, a retargeted symbol, and an imported method. Assert `SymbolEqualityComparer.Default` still equates `String` and `String?` when that is the existing comparer contract, and assert `IncludeNullability` sees a stored difference only after the comparer has a fact to read. Include a C# program that reads a VB image through these properties, and a VB program that reads a C# image. Failing-before evidence is this class run against the post-LC-1G.2 compiler before the getter change.

Legacy: Visual Basic 17.13 symbols stay oblivious. `default` and `latest` do not change.

Framework: one imported Framework 4.7 image and one modern image, so a getter does not depend on the test host's reflection stack.

Public API: no signature change. If a correct result cannot be expressed with the current signatures, stop and escalate.

Resources: symbol and emit tests on `net10.0`.

Dependencies: LC-1G.1, and the imported shapes from LC-1G.2.

Escalation: any new public member, or a change to default symbol equality.

Acceptance: the inventory above matches the stored annotations for both source and metadata, and the accepted flow tests still pass.

Exclusions: IDE text, EnC, and new syntax.

## LC-1G.5 — IDE, analyzer, and diagnostic integration

Classification: tooling integration.

Objective: consumers of the public semantic model and diagnostics agree with the annotations LC-1G.4 exposes.

Obligations: the six tooling items.

Source: `src/Features/VisualBasic`, `src/Workspaces/VisualBasic`, `src/EditorFeatures/VisualBasic`, VB symbol display, and the diagnostic formatters that print a type. No `NullableAnnotation` use was found in the VB Features or Workspaces trees.

Current state: diagnostics can name the CLR type without `?`. That was observed in LC-1D for BC30610 and was left unchanged. Display work has not started.

Production change: thread existing annotations into semantic display and into the IDE surfaces that already print types, and adjust a code fix only where it would now mis-apply because an annotation became visible. Gate new nullable text on `vbnext` with annotations enabled.

Tests: the Visual Basic Features, Workspaces, and EditorFeatures tests that cover quick info, completion, and rename for a nullable signature, plus a diagnostic-text test. Add an analyzer-style consumer that reads `NullableAnnotation` through the public API. Failing-before evidence is a diagnostic or quick-info assertion that fails before the display change. Do not require a full IDE solution build when the focused projects are enough.

Legacy: Visual Basic 17.13 diagnostic text stays byte-for-byte on the existing tests.

Cross-language: quick info or diagnostic text for a C# reference and for a VB reference of the same signature.

Public API: consume LC-1G.4. Do not add an IDE-only annotation API.

Resources: focused tooling tests. A full Workspaces run needs a separate resource check.

Dependencies: LC-1G.4. Independent of LC-1G.6.

Escalation: a new public display API, or a change to 17.13 diagnostic wording.

Acceptance: enabled `vbnext` display shows the annotation the public API returns, and 17.13 wording is unchanged.

Exclusions: new code fixes that only exist to insert `?`, and VB Razor.

## LC-1G.6 — Edit and Continue

Classification: compiler correctness.

Objective: a nullable annotation survives the EnC delta rules already used for signatures.

Obligations: nullable Edit and Continue.

Source: `src/Compilers/VisualBasic/Portable/Emit/EditAndContinue`. No nullable references were found there.

Current state: unverified. This plan did not run an EnC session.

Production change: include stored annotations in the delta comparison when an existing EnC edit already rebuilds the signature. Do not change runtime lowering. An annotation-only edit must either be accepted as a valid delta or rejected with the existing rude-edit machinery. Pick the behavior that matches how EnC already treats a signature change, and record it in the slice report.

Tests: the Visual Basic EnC tests, with one positive annotation edit and one negative edit that must be rude if the signature identity ignores annotations. Failing-before evidence is that pair run before the EnC change. Compare IL for a method that is edited without an annotation change.

Legacy: an EnC session on Visual Basic 17.13 does not gain nullable rude edits.

Public API: a delta observed through LC-1G.4 reports the annotation the new compilation stores.

Resources: EnC emit tests on `net10.0`.

Dependencies: LC-1G.1. Public observation depends on LC-1G.4. Independent of LC-1G.5.

Escalation: a new rude-edit code, or a debugger-protocol change.

Acceptance: the chosen delta behavior is tested in both directions, and a non-nullable edit still emits the same method body.

Exclusions: IDE debugging UI and flow-analysis changes.

## LC-1G.7 — Consolidated integration acceptance

Classification: documentation or acceptance, plus compatibility verification.

Objective: one review across the completed slices. It does not add behavior.

Obligations: the historical broader-integration bucket, the four cross-language gaps, and the LC-1F regression filters as a final check rather than as a substitute for the slice tests.

Source: no new production surface. The tests are the classes named below.

Retained observations, not confirmed defects, for this consolidated review: during LC-1G.2a, on the tree that became `6b937cd7cbcb283d5abe4eeb8c492ccd0858b766`, one parallel run of `Substitution_EveryConstructedSymbolKeepsItsArgument` reported an unexpected BC42512 on `methodText`. The same test then passed in isolation, with `xUnit.MaxParallelThreads=1`, and in the recorded default regression. No cache collision or concurrency defect was established. The first full nullable filter after the missing-constructor diagnostic aborted because the test host crashed, and no dump was captured. A reentrancy guard was added before the recorded passing run. The crash cause was not established. The evidence is `LanguageCompletion/LC-1G.2a-METADATA-CORRECTNESS-CLOSEOUT-REPORT.md`, section 16, and `LanguageCompletion/LC-1G.2a-regression.log`. Do not treat either observation as a diagnosed defect.

Current state: LC-1F.6d reported, and this plan does not re-run, these historical results: nullable emit 435 passed and 6 skipped; `FlowAnalysisTests` 704 passed; `OverridesTests|PropertyTests` 231 passed; `OverloadResolutionTests` 51 passed; `TypeArgumentInference` 53 passed; `Lambda_AnonymousDelegateInference` 7 passed; `ExtensionMethodTests` 37 passed; `Unstructured|ErrorHandling` 34 passed and 28 skipped. Those filters overlap. Do not add them into one total.

Tests: re-run the slice tests first. Then run the historical filters that the changed projects actually contain, and state which project each filter used. The emit project is `src/Compilers/VisualBasic/Test/Emit/Microsoft.CodeAnalysis.VisualBasic.Emit.UnitTests.vbproj`. Semantic tests are `src/Compilers/VisualBasic/Test/Semantic/Microsoft.CodeAnalysis.VisualBasic.Semantic.UnitTests.vbproj`. Symbol tests are `src/Compilers/VisualBasic/Test/Symbol/Microsoft.CodeAnalysis.VisualBasic.Symbol.UnitTests.vbproj`. A zero-test result fails the run. Representative IL comparisons from the earlier slices stay in the evidence. Legacy `default` and `latest` are checked on one nullable program and one program that uses no nullable syntax.

Dependencies: LC-1G.1 through LC-1G.6.

Escalation: any new missing warning, any IL mismatch, or any desire to start LC-4 inside this slice.

Acceptance: the owner can accept LC-1G from that evidence. This plan does not accept it in advance.

Exclusions: LC-4, and any production edit presented as a drive-by fix.

## Test inventory

Existing emit classes used by the nullable work:

- `NullableAnnotationTests`
- `NullableDeclarationTests`
- `NullableMetadataEmissionTests`
- `NullableMetadataImportTests`
- `NullableAnalysisAttributeTests`
- `NullableCoreFlowTests`
- `NullableFlowCorrectnessTests`
- `NullableMethodContractFlowTests`
- `NullableMemberAndElementFlowTests`
- `NullableMemberFlowCorrectnessTests`
- `NullableInitializationAndMemberContractTests`
- `NullableContractConsistencyTests`
- `NullablePostconditionVerificationTests`
- `NullableContractCorrectnessTests`
- `NullableFlowConsolidatedAcceptanceTests`
- `NullableFlowVerificationCloseoutTests`
- `NullableGenericMethodAnnotationTests`
- `NullableFlowPolicyTests`
- `NullableFlowTargetedCloseoutTests`

Symbol and semantic filters that LC-1F already used, and that later slices may rerun when their layer changes: `FlowAnalysisTests`, `OverridesTests`, `PropertyTests`, `OverloadResolutionTests`, `TypeArgumentInference`, `Lambda_AnonymousDelegateInference`, `ExtensionMethodTests`, and the unstructured and error-handling filters. Do not invent a class named `TypeInferenceTests`. It is not in this tree.

Each new fact needs a positive annotation and a negative or oblivious annotation. Each slice that can change emitted IL compares at least one method with Visual Basic 17.13.

## Outstanding questions

No language-policy decision is waiting. The four flow policies and the conservative limitations stay as accepted.

LC-1G.4 should correct `ITypeSymbol.NullableAnnotation` and `WithNullableAnnotation` because those members already exist and currently discard the annotation. That is a behavior change for analyzers, and it is in this plan. It is not a new public signature. If the implementation cannot do it without a new member, stop.

LC-1G.5 may change `vbnext` diagnostic text when annotations are enabled. It must not change Visual Basic 17.13 text. That constraint is part of the slice, not a new owner decision.

Source `Class?` constraint syntax is not approved. Do not add it while preserving PE constraint annotations.

## Mission check

This plan stays inside VB-030 integration. It adds no syntax, no AI-specific feature, and no dependency that would make nullable analysis require a modern framework API. `default` and `latest` stay on Visual Basic 17.13. Desktop and server compilation remains the consumer that matters here. VB Razor, VBHTML, and VB MAUI are out of scope.
