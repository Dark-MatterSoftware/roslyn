# Default extension-indexer lookup

This assessment is not an implementation and it is not an acceptance. The CompilationReference language-version correction stays accepted. VB-039 stays open.

The mission is [vbnext-mission.md](vbnext-mission.md). The metadata history is [vbnext-extension-metadata-architecture.md](vbnext-extension-metadata-architecture.md).

## What was approved

Slices 9c, 9d, and 9e approve a parameterized extension property used as `box(index)` when the receiver has no default property. That is source behavior. Cross-assembly default lookup was left open when metadata emission was accepted.

## What the accepted compiler does

The compiler is SHA-256 `7A41BCCDF076943D79573E17CC80A8C3A8DFA9EBFE5D20530E4F6DD9AE558B68`, from `ed1bf941010ae22278ad0c0e01f69e4fe7abaed1`.

On `vbnext`, the same compilation and a compilation reference bind `box(0)` for a class, a structure, a generic class with no instance default property, and a class that implements an interface. An interface-typed variable is BC30547. Named access binds in every case.

An implementation reference and a metadata-only reference bind the name and reject `box(0)` with BC30367 for a class, BC30690 for a structure, and BC30547 for an interface. There is no candidate.

On 17.13, default, and latest, a compilation reference rejects the name with BC30456 and still binds `box(0)`. A PE reference rejects both. An ordinary instance default property wins. Three parameterized properties make `box(0)` BC30521 in source. A write-only property reports BC30057 for both forms in the same compilation because the reduced property drops its parameters when it has no getter.

## Cause

`AppendProbableExtensionDefaultProperties` walks `GetMembers()`. Reconstructed PE extension properties are not in that list. Named lookup adds them in `PENamedTypeSymbol.AppendProbableExtensionProperties`. Default lookup has no matching override.

`LookupDefaultPropertyInInterface` never calls `LookupForExtensionDefaultProperties`. The class path does, after the instance search fails.

`LookupForExtensionDefaultProperties` does not test the consumer language version.

## Recommended correction

Alternative A, not authorized: add the PE instance extension properties to default collection, call the same lookup from the interface path when no instance default property is found, and skip candidates when the consumer language version is below `vbnext`. Do not change metadata. Do not use `DefaultMemberAttribute` to choose among several parameterized properties. The write-only parameter loss stays a separate defect.

`ExtensionProperty_PeReference_DefaultIndexRemainsUnresolved` still expects BC30367. A later implementation must change that expectation under the same authorization.

The evidence is `D:\Development\VBNext\Evidence\LanguageCompletion\VBNEXT-EXTENSION-DEFAULT-INDEXER-ASSESSMENT-REPORT.md`.
