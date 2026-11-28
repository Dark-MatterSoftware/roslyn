# CompilationReference consistency assessment

This assessment does not change the compiler. VB-039 stays open. The marker-contract correction is formally accepted and is not reopened here. The mission is [vbnext-mission.md](vbnext-mission.md). The emission architecture is [vbnext-extension-metadata-architecture.md](vbnext-extension-metadata-architecture.md).

## What was compared

One vbnext producer was consumed through the same compilation, a `CompilationReference` created from that producer, an implementation image emitted from that producer, and a metadata-only image emitted from that producer. Consumer versions were explicit 17.13, `VisualBasicParseOptions.Default`, explicit latest, and explicit `vbnext`. Default and latest both have effective language version `VisualBasic17_13`. The published compiler was `Microsoft.CodeAnalysis.VisualBasic.dll`, 6,572,032 bytes, SHA-256 `111B6076FA4C93FD52EB3F08C03A0680E6EBE66AE6D26E994ED573821129721C`.

The harness recorded `requestedRef=True` only for the compilation-reference cases, so those cases did use `CompilationReference`.

## Language version

`LookupForExtensionProperties` in `Binder_Lookup.vb` skips a candidate when it is a `PEExtensionPropertySymbol` and the consumer language version is below `VbNext`. A compilation reference exposes source symbols. Those symbols are not `PEExtensionPropertySymbol`, so the check does not run. `ReduceExtensionProperty` then creates a `ReducedExtensionPropertySymbol`, and binding succeeds.

On 17.13, default, and latest, the compilation reference binds read-only, write-only, read/write, and named parameterized properties, and the class, interface, structure, and `List(Of Integer)` receivers. Emit succeeds. The implementation reference and the metadata-only reference report BC30456, with no candidate, and emit fails. Explicit `vbnext` succeeds for all three reference kinds and for the same compilation. Ordinary extension methods, including a generic method, bind on every version and every reference kind.

The approved policy is that a consumer needs explicit `vbnext` to bind the new extension properties. The producer version does not raise the consumer version. The PE rejection is that policy. The compilation-reference success is a representation-specific hole in the same lookup method. The recommended correction is to apply the existing version check to every extension-property candidate in that method, not only to `PEExtensionPropertySymbol`. Ordinary extension-method lookup stays unchanged. This is not implemented.

## Friend access

The closeout consumer that reported BC30389 was named `FriendCompilationConsumer`. The producer `InternalsVisibleTo` named `FriendConsumer`. That consumer is not a friend.

A consumer named `FriendConsumer` binds a `Friend` extension property, an ordinary `Friend` module method, and a `Friend` extension method through a compilation reference, an implementation reference, and a metadata-only reference. The property and its getter are `Internal`. An unrelated consumer is rejected. A compilation reference reports BC30389 for the property and BC30390 for the methods, with one inaccessible candidate. A PE reference reports BC30456 and no candidate. Both results deny access. The same split appears for the ordinary `Friend` method, so it is not specific to extension properties.

No accessibility correction is proposed. A future test should use the friend assembly name and should keep the unrelated rejection. An authorized 17.13 friend still binds the extension property through a compilation reference and does not bind it through PE. That remaining difference is the language-version hole.

## Boundaries that stay as they are

Direct `CSharpCompilationReference` is rejected by a Visual Basic compilation, and direct `VisualBasicCompilationReference` is rejected by a C# compilation. Cross-language use stays on the PE path. Nullable encoding and retargeting were not changed. `RetargetingPropertySymbol.IsExtensionProperty` forwards to the underlying property, and that symbol is not a `PEExtensionPropertySymbol`, so the recommended lookup check covers it. The two-assembly retargeting fixture was not run again. Default `box(index)`, multiple parameterized properties, and the reduced nullable annotation `None` stay open and were not corrected.

The evidence is `D:\Development\VBNext\Evidence\LanguageCompletion\ExtensionCompilationReferenceAssessment\`.

## Formal acceptance

The owner accepts this assessment at `ad46b749abc2bbf1457c825df4f1246a04206928` and approves Alternative A. The consumer language-version check in `LookupForExtensionProperties` must not depend on `PEExtensionPropertySymbol`. Legacy consumers reject the new binding through every tested reference kind. Explicit `vbnext` still binds. The friend BC30389 result is a fixture mismatch and is not a compiler defect. Accessibility behavior stays as it is. The test that expected ungated compilation-reference binding is authorized to change. This acceptance does not implement the correction, and VB-039 stays open.
