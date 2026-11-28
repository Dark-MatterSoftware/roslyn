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
