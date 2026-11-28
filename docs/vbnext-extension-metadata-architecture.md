# Cross-assembly extension property and indexer metadata

**Status: Architecture assessment accepted. Alternative A is approved under VB-039. The production correction is not owner-accepted.**

The owner accepted this assessment, including its findings, uncertainties, and verification limitations, and approved Alternative A under the existing VB-039 feature. No new feature identifier, phase, slice, or wave is authorized. The canonical mission is unchanged. VB-030, VB-187, and the Introduce Parameter closure stay closed. Wave 37 stays unopened.

The executable C# 15 assessment remains the working compatibility record. This note covers one gap from that record: a Visual Basic extension property or indexer that works in the compilation that declares it is not visible to another assembly.

## What was approved

VB-039 slices 9c, 9d, and 9e, in [vbnext-phases.md](vbnext-phases.md), approve an `Extension` block in a module, a property, a parameterized property used as `box(index)` when the type has no default property, and a `Shared` member called on the extended type. The tests in `ExtensionPropertyTests` are one compilation. VB-107 is the separate consumption of the grouping metadata C# emits. Visual Basic does not have a generic extension block.

The assembly boundary is a completeness gap in VB-039. It is not a new capability and it is not an extension-method defect. A method marked with `Extension` still round-trips to Visual Basic and to C#.

## What the metadata does

A VB extension property is a source property whose first parameter is the receiver. The compiler emits that property and its static accessors on the module. The module does not get a type-level `ExtensionAttribute` unless it also contains an extension method. There is no `<G>$` grouping type, no marker type, no `ExtensionMarkerAttribute`, and no `DefaultMemberAttribute`.

VB and C# both discover extension properties by that grouping contract. `PENamedTypeSymbol.BuildExtensionMembers` already reads it, and a C# extension library is already usable from `vbnext`. A 17.13 metadata reference does not bind those reconstructed properties. `CompilationReference` is source symbols, so a 17.13 compilation that references a vbnext compilation already binds the properties. That is existing behavior.

`box(index)` works for source symbols and for a `CompilationReference`. It does not work for a metadata reference, because default lookup never sees the reconstructed extension property. C# extension indexers have the same Visual Basic lookup gap. Emitting `DefaultMemberAttribute` would let C# use `receiver[index]`. It would not by itself make VB `box(index)` work across a metadata reference. That lookup change stays separate.

## Recommendation

The smallest complete architecture is to emit the existing grouping contract and keep the current source symbols, lowering, and accessor signatures. Do not share the C# `ExtensionGroupingInfo` type. Do not teach Visual Basic to treat the current module property as the interoperable form, because C# would still be unable to read it. Do not keep two public properties.

Unreleased libraries compiled with the current shape would need to be recompiled before another assembly could see the extension properties. Callers already compiled against the static accessors can keep working if those signatures stay.

Exact .NET Framework 4.7 compilation was not run. `microsoft.netframework.referenceassemblies.net47` 1.0.3 is not in the current NuGet cache. net472 was not substituted.

The evidence is under `D:\Development\VBNext\Evidence\LanguageCompletion\`:

- `VBNEXT-EXTENSION-METADATA-ARCHITECTURE-ASSESSMENT-REPORT.md`
- `VBNEXT-EXTENSION-METADATA-ARCHITECTURE-MATRIX.md`
- `VBNEXT-EXTENSION-METADATA-CONTRACT-MATRIX.md`
- `VBNEXT-EXTENSION-METADATA-IMPLEMENTATION-PROPOSAL.md`
- `VBNEXT-EXTENSION-METADATA-OWNER-DECISION-MATRIX.md`

## Owner decisions

| Decision | Disposition |
| --- | --- |
| Architecture assessment | Accepted |
| Alternative A | Approved |
| Feature identifier | Existing VB-039. No new identifier |
| Static accessor signatures | Preserve |
| Module property that includes the receiver | Replace. No duplicate public property |
| Emitter | Visual Basic-specific. No shared C# refactoring |
| Named extension-property lookup | Not changed by this authorization |
| Default `box(index)` through a metadata reference | Separate correction. Not authorized |
| `CompilationReference` language-version discrepancy | Separate correction candidate. Not authorized |
| Exact net47 reference package | Bounded restore of `Microsoft.NETFramework.ReferenceAssemblies.net47` 1.0.3 |
| Generic extension-block authoring | Outside scope |
| Nullable regression coverage | Required. VB-030 stays closed |
| Wave 37 | Unopened |
| Canonical mission | Unchanged |

Acceptance of the assessment does not establish that the production correction has succeeded. The implementation that followed is recorded below and is pending owner acceptance.

## Implementation result

The correction is implemented on `vbnext/main` and is not owner-accepted. VB-039 is not closed. A module that contains an extension property now emits the grouping and marker metadata, a type-level `ExtensionAttribute`, and `DefaultMemberAttribute` for a parameterized extension property. The module property that included the receiver is no longer emitted. The static accessors remain. A property-only module is abstract and sealed so C# recognizes it as a static class. An ordinary module is not marked abstract.

Named VB-to-VB and VB-to-C# consumption succeed through a metadata reference and a metadata-only reference assembly. C# `receiver[index]` succeeds. VB `box(index)` through a metadata reference remains BC30367. A metadata reference on 17.13, default, and latest still does not bind the properties. A `CompilationReference` on those versions still does. Both of those results stay separate.

Exact net47 compilation used `Microsoft.NETFramework.ReferenceAssemblies.net47` 1.0.3. It was not executed on a 4.7 CLR. net472 and net48 ran on CLR 4.8.10008.0. .NET Standard 2.0 compiled after `Microsoft.VisualBasic` was added to the reference set. The earlier single-DLL BC35000 result was an invalid reference set.

Previously emitted VB.Next extension libraries need to be recompiled before another assembly can bind the properties. The evidence is under `D:\Development\VBNext\Evidence\LanguageCompletion\`:

- `VBNEXT-EXTENSION-METADATA-IMPLEMENTATION-REPORT.md`
- `VBNEXT-EXTENSION-METADATA-VERIFICATION-MATRIX.md`
- `VBNEXT-EXTENSION-METADATA-BINARY-COMPATIBILITY-REPORT.md`
- `VBNEXT-EXTENSION-METADATA-FRAMEWORK-COMPATIBILITY-MATRIX.md`
- `VBNEXT-EXTENSION-METADATA-OWNER-ACCEPTANCE-MATRIX.md`

## Verification closeout

The owner authorized a verification closeout and withheld formal acceptance until that closeout finished. The closeout is recorded below. Formal acceptance follows in the next section. VB-039 is not closed. No production compiler change and no permanent test change were made.

The retained pre-implementation compiler was used. Fourteen static-accessor signature blobs, including the nullable accessors, match the corrected compiler. A C# caller compiled against the historical producer was not recompiled. It printed `!Hi2` and exited 0 against the historical producer and again after only the producer assembly was replaced. A historical Visual Basic caller of `get_Marker` is BC30456 because the method is a module-property accessor. The corrected compiler accepts that source because the module property is gone. That is a reflection-visible change, and previously emitted VB.Next extension libraries still need recompilation.

Modules that contain an extension property are abstract and sealed. That flag is the C# static-class contract in `IsMetadataAbstract`. Ordinary modules and extension-method-only modules stay sealed only. A mixed module that contains an extension property becomes abstract and sealed, and its ordinary extension method remains discoverable.

A `CompilationReference` retargets the reduced property to `RetargetingPropertySymbol`, with the receiver resolved to the consumer's referenced assembly. An authorized friend binds a `Friend` extension property through an implementation reference and a metadata-only reference. An unrelated assembly is BC30456. A `CompilationReference` reports BC30389.

Imported static-accessor nullable annotations match the historical compiler. The reduced public property annotation remains `None`. That is a pre-existing reduced-symbol result. VB-030 stays closed. Grouping suffixes use FNV-1a rather than the C# xxHash spelling. Consumers recognize the grouping prefixes and the marker attribute. Repeated emission was deterministic. Several named parameterized properties can share a grouping. `DefaultMemberAttribute` selects one C# indexer. VB named access binds the others. Default VB `box(index)` through a metadata reference remains BC30367.

A present `ExtensionMarkerAttribute` with an unusable constructor is omitted, the assembly emits, and the consumer reports BC30456. C# reports CS0656 and does not emit. Extension syntax and PE consumption of the new properties stay rejected on 17.13, default, and latest. A `CompilationReference` on those versions still binds.

`ExtensionPropertyTests` passed 15 tests, of which 9 were added by the implementation. `ExtensionMethodTests` passed 37. Additional targeted suites passed and are recorded separately. The emit test project built for net472 and those tests were not executed. Exact net47 compilation remains the earlier result for package 1.0.3. A 4.7 CLR run is not claimed.

The closeout evidence is under `D:\Development\VBNext\Evidence\LanguageCompletion\`:

- `VBNEXT-EXTENSION-METADATA-VERIFICATION-CLOSEOUT-REPORT.md`
- `VBNEXT-EXTENSION-METADATA-FINAL-VERIFICATION-MATRIX.md`
- `VBNEXT-EXTENSION-METADATA-BINARY-AND-REFLECTION-CLOSEOUT.md`
- `VBNEXT-EXTENSION-METADATA-CONTRACT-AND-NULLABLE-ASSESSMENT.md`
- `VBNEXT-EXTENSION-METADATA-REGRESSION-COVERAGE-MATRIX.md`
- `VBNEXT-EXTENSION-METADATA-FINAL-OWNER-DECISION-MATRIX.md`

## Formal acceptance

The owner formally accepts the cross-assembly metadata emission correction, including the documented limitations. Alternative A remains the approved metadata architecture. This acceptance is the emission correction only. VB-039 is not closed.

Accepted implementation: `c9a6e26f4fe5fac8e104985069eb417a41c9827e`. Architecture acceptance: `04e586aae619f0e75f34dfc15bbdd77be2294f42`. Verification closeout: `05d3c91fbe647f2752f5f17433824b4f2e696b41`.

Accepted evidence includes the 14 matching static-accessor signature blobs, the unchanged C# caller that printed `!Hi2` against both producers, extension-method compatibility, the reflection-visible replacement of the receiver-bearing module property, the abstract and sealed flag for modules that contain extension properties, deterministic FNV grouping names, class-receiver retargeting to `RetargetingPropertySymbol`, authorized friend access through PE and metadata-only references, preserved nullable accessor metadata, reference-assembly metadata, older-framework compilation, and 17.13 PE rejection. The reported regression runs were 274 passing tests, zero failures, and one skip, counted as separate suites.

Accepted limitations: previously emitted VB.Next extension libraries must be recompiled. The compiled caller is a constructed C# fixture. Retargeting coverage is the demonstrated class receiver. The reduced public nullable annotation remains `None`. Exact .NET Framework 4.7 runtime execution was not established. Permanent coverage is still absent for authorized friend access, retargeting, multiple parameterized properties, the reduced nullable projection, and mixed-module flags.

The owner separately approves Alternative B. When emission requires `ExtensionMarkerAttribute` and an existing definition cannot supply the required constructor, the compiler must report an error, must not emit an undiscoverable extension member, and must not embed a replacement definition. That correction is authorized and is not part of this acceptance. A missing attribute still embeds. A valid framework or referenced attribute is used and is not embedded again.

These candidates stay open: default PE `box(index)`, the `CompilationReference` language-version discrepancy, the `CompilationReference` friend diagnostic difference, C# consumption of a second parameterized property in one grouping, and the reduced nullable projection.

## Marker contract correction

Alternative B is formally accepted. Implementation `378bcaf0de8c35511aec76bf82d2b01835d2682b`. Verification `f39746d45beb0e63ac004575d405fcb1645f3da1`. The accepted evidence is the BC35000 reuse, demand-driven validation, full and metadata-only rejection, preserved embedding and valid attributes, 11 permanent tests, the 3-failure then 11-pass results, 258 targeted passes with zero failures and zero skips, exact net47 compilation, and net11 producer and consumer compilation. Nullable contracts stay accepted. Documented limitations that do not authorize more production changes: the non-attribute shape was not retested, conflicting definitions stay on existing resolution, a 4.7 CLR was not executed, the net472 project was built and not executed, the metadata comparison was representative, explicit default and latest were not separately listed, and a new standalone net11 runtime run was not established. VB-039 remains open. When a compilation emits an extension property and an existing `ExtensionMarkerAttribute` cannot supply the required string constructor, the compiler reports BC35000, `ERR_MissingRuntimeHelper`, with the argument `System.Runtime.CompilerServices.ExtensionMarkerAttribute..ctor`. Emission fails, including metadata-only emission. The compiler does not embed a replacement definition.

A missing attribute is still embedded. A valid framework or referenced attribute is used and is not embedded again. An additional constructor does not hide a valid string constructor. A compilation that does not emit an extension property, including a Visual Basic 17.13 compilation, does not report the diagnostic merely because an unusable attribute is referenced.

The diagnostic is the same missing-runtime-helper diagnostic accepted for an unusable `NullableAttribute` constructor. No new diagnostic identifier was added. The report is demand-driven: `MethodCompiler` reports it while compiling a type that declares an extension property, and metadata-only emit reports it from `SynthesizedMetadataCompiler` before the image is written.

A valid net11 producer keeps the same module flags, grouping name `<G>$8D07098A8288F079`, marker name `<M>$7F93D3209D6E48BB`, and `get_Marker` signature blobs `00010E1208` and `20000E`. Exact net47 compilation still embeds a missing marker, uses a referenced string constructor, and rejects a parameterless constructor. A 4.7 CLR run is not claimed. The nullable missing-constructor tests still pass. VB-039 remains open.

## CompilationReference consistency

The marker-contract acceptance does not correct reference-kind behavior. A later assessment, recorded in `docs/vbnext-extension-compilationreference-assessment.md`, confirmed that `LookupForExtensionProperties` applies the `vbnext` check only to `PEExtensionPropertySymbol`. Compilation references therefore bind extension properties on 17.13, default, and latest. Implementation and metadata-only references report BC30456. Ordinary extension methods are unaffected. The earlier friend BC30389 came from a consumer assembly name that did not match `InternalsVisibleTo`. A correctly named friend binds through all three reference kinds. That observation is not a compiler defect, and no accessibility change is authorized by the assessment. The owner accepts that assessment and the implemented lookup correction. Alternative A is permanent. The friend result stays classified as a fixture mismatch. Default PE `box(index)` stays open.

## Default indexer lookup

A later assessment, recorded in `docs/vbnext-extension-default-indexer-assessment.md`, traced the open default form. Named PE lookup adds reconstructed extension properties. Default lookup walks `GetMembers()` and does not. An interface variable never reaches that lookup, even in the same compilation. The accepted language-version check is in the named lookup only, so a 17.13 compilation reference still binds `box(0)`. The recommended correction is a binder collection override, an interface call, and the same version check. It is not implemented and it is not accepted. Metadata emission stays accepted. VB-039 stays open.
