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

The owner authorized a verification closeout and withheld formal acceptance. The closeout is complete and the correction is technically ready for formal owner acceptance, subject to the documented limitations and the owner decisions below. That readiness is not acceptance. VB-039 is not closed. No production compiler change and no permanent test change were made.

The retained pre-implementation compiler was used. Fourteen static-accessor signature blobs, including the nullable accessors, match the corrected compiler. A C# caller compiled against the historical producer was not recompiled. It printed `!Hi2` and exited 0 against the historical producer and again after only the producer assembly was replaced. A historical Visual Basic caller of `get_Marker` is BC30456 because the method is a module-property accessor. The corrected compiler accepts that source because the module property is gone. That is a reflection-visible change, and previously emitted VB.Next extension libraries still need recompilation.

Modules that contain an extension property are abstract and sealed. That flag is the C# static-class contract in `IsMetadataAbstract`. Ordinary modules and extension-method-only modules stay sealed only. A mixed module that contains an extension property becomes abstract and sealed, and its ordinary extension method remains discoverable.

A `CompilationReference` retargets the reduced property to `RetargetingPropertySymbol`, with the receiver resolved to the consumer's referenced assembly. An authorized friend binds a `Friend` extension property through an implementation reference and a metadata-only reference. An unrelated assembly is BC30456. A `CompilationReference` reports BC30389.

Imported static-accessor nullable annotations match the historical compiler. The reduced public property annotation remains `None`. That is a pre-existing reduced-symbol result. VB-030 stays closed. Grouping suffixes use FNV-1a rather than the C# xxHash spelling. Consumers recognize the grouping prefixes and the marker attribute. Repeated emission was deterministic. Several named parameterized properties can share a grouping. `DefaultMemberAttribute` selects one C# indexer. VB named access binds the others. Default VB `box(index)` through a metadata reference remains BC30367.

A present `ExtensionMarkerAttribute` with an unusable constructor is omitted, the assembly emits, and the consumer reports BC30456. C# reports CS0656 and does not emit. The recommended policy is a later diagnostic when the required constructor is unusable. That change is not part of this correction and needs a separate owner decision. Extension syntax and PE consumption of the new properties stay rejected on 17.13, default, and latest. A `CompilationReference` on those versions still binds.

`ExtensionPropertyTests` passed 15 tests, of which 9 were added by the implementation. `ExtensionMethodTests` passed 37. Additional targeted suites passed and are recorded separately. The emit test project built for net472 and those tests were not executed. Exact net47 compilation remains the earlier result for package 1.0.3. A 4.7 CLR run is not claimed.

The closeout evidence is under `D:\Development\VBNext\Evidence\LanguageCompletion\`:

- `VBNEXT-EXTENSION-METADATA-VERIFICATION-CLOSEOUT-REPORT.md`
- `VBNEXT-EXTENSION-METADATA-FINAL-VERIFICATION-MATRIX.md`
- `VBNEXT-EXTENSION-METADATA-BINARY-AND-REFLECTION-CLOSEOUT.md`
- `VBNEXT-EXTENSION-METADATA-CONTRACT-AND-NULLABLE-ASSESSMENT.md`
- `VBNEXT-EXTENSION-METADATA-REGRESSION-COVERAGE-MATRIX.md`
- `VBNEXT-EXTENSION-METADATA-FINAL-OWNER-DECISION-MATRIX.md`
