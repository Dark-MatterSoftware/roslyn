# Vb.Next mission and language design principles

**Vb.Next** is an independent community effort. It is not affiliated with, endorsed by, or supported by Microsoft.

This is the canonical statement of the project's mission and design principles. Other documents link here instead of restating it. The roadmap ([vbnext-roadmap.md](vbnext-roadmap.md)), the feature plan ([vbnext-missing-features.md](vbnext-missing-features.md)), the phases ([vbnext-phases.md](vbnext-phases.md)), and the syntax notes (`vbnext-vbNNN-syntax.md`) record the decisions made under it.

## Mission

Vb.Next exists to bring Visual Basic forward as a modern, expressive, approachable, readable, and productive language on the modern .NET platform, while preserving the distinctive identity and culture of Visual Basic.

It is not an attempt to turn VB into C# with different keywords. It is not a syntax-parity exercise, and it is not a collection of features added because another language has them.

Vb.Next should let VB developers benefit from modern .NET capabilities and interoperate naturally with C# and the wider .NET ecosystem, while new code is authored in syntax and semantics designed for Visual Basic.

## Direction

Adopted in wave 4, and first recorded in [vbnext-roadmap.md](vbnext-roadmap.md#direction).

Vb.Next targets three stacks:

- Modern VB.NET on desktop and server
- VB Razor and VBHTML on the web
- VB MAUI on mobile and desktop

Language stability matters more than feature density. A language decision is judged by these goals, in this order:

1. Readability
2. Keyword-based syntax
3. Parser simplicity
4. Razor compatibility
5. XAML and markup compatibility
6. Long-term tooling support

New syntax should look unmistakably like Visual Basic. A keyword form is preferred over a symbolic, C#-style form. XML literals are in scope, because VBHTML and markup generation depend on them.

## Core principles

Every proposed language enhancement is evaluated against these principles, together with the goals above.

1. **Clarity over brevity.** Prefer code that communicates intent clearly over code that merely uses fewer characters.
2. **Approachability over cleverness.** Features should be understandable to ordinary VB developers, not only to compiler experts or language enthusiasts.
3. **VB-first authoring.** Native VB syntax must feel natural to VB. Do not copy C# syntax merely to achieve superficial parity.
4. **Compatibility by default.** Existing VB code keeps its meaning and behavior. New semantics are versioned, explicit, or otherwise isolated. In this tree, `default` and `latest` stay on Visual Basic 17.13, and new behavior is gated on `LanguageVersion.VbNext`, as [vbnext-language-version.md](vbnext-language-version.md) records.
5. **Interoperability without syntax imitation.** VB should be able to consume modern .NET and C# capabilities even when native VB authoring syntax is not justified.
6. **Explicitness and safety for advanced features.** Features involving references, lifetimes, mutation, memory, or evaluation order need a clear safety and readability model.
7. **Developer experience is part of the language.** Diagnostics, IntelliSense, Quick Info, formatting, navigation, analyzers, debugging, documentation, and IDE behavior are part of feature completeness.
8. **Feature parity is a means, not the mission.** The goal is a capable modern VB ecosystem, not identical feature lists or identical syntax to C#.
9. **Pragmatism and continuity.** Preserve the strengths of VB: productive application development, readable code, approachable tooling, and practical access to the .NET platform.

A feature is not accepted for native VB syntax only because C# has it, and it is not rejected only because it came from C#. It is judged on its value, semantics, readability, compatibility, complexity, and fit with VB.

## Language and framework API boundary

**Vb.Next modernizes the Visual Basic language, compiler, diagnostics, and tooling. It does not hardcode or duplicate the evolving .NET API surface into the language.**

Framework types, properties, and methods are available to VB code through the project's target framework and referenced assemblies, just as they are to C# code. Their availability does not depend on Vb.Next adding special language support for each API.

A proposal distinguishes:

1. A genuine VB language, compiler, diagnostics, or tooling gap.
2. An API already available through the target framework or referenced libraries.
3. A framework API unavailable to a particular target, such as .NET Framework 4.7.

Only the first is a Vb.Next feature. The second needs no language change, and the third is decided by the target, not by the language.

Vb.Next preserves the ability to target older frameworks. It does not require newer framework APIs, or make their names part of the language, merely to provide modern functionality.

**Compatibility rule:** target-framework and referenced-assembly availability determine which APIs can be used. Vb.Next language features stay independent of specific framework API versions unless a proposal explicitly justifies otherwise.

**Compiler contracts.** Some framework types and attributes exist so that compilers recognize them, and .NET defines what a compiler does with each: `IAsyncEnumerable(Of T)` and `IAsyncDisposable`, `System.Index` and `System.Range`, `System.Threading.Lock`, `ValueTask` and `AsyncMethodBuilderAttribute`, `InlineArrayAttribute`, `CollectionBuilderAttribute`, `ParamCollectionAttribute`, interpolated string handlers, `IsExternalInit`, and the attributes of required members. C# follows the same contracts. Three kinds of shared conventions count as contracts too: the conversions of arrays and strings to `Span(Of T)` and `ReadOnlySpan(Of T)` that C# 14 defines; the metadata of C# records, `<Clone>$`, `PrintMembers`, `EqualityContract`, and `IEquatable(Of R)`, which VB reads and writes so that records cross between the languages; and the members a range binds by name, `Count` or `Length` with `Slice` or `Substring`, as C# 8 binds them. One member pattern is VB's own: a spread in a collection or array literal calls `AddRange`, then `ToArray` for an array, as slice 7c of VB-035 chose; it is justified by that decision, and it binds by name, so it works on any target whose type has the members. A feature that implements such a contract is justified by it, standing: it switches on only where the target framework or a referenced assembly declares the type, code that needs the type reports an error on a target without it, and other code builds for that target as before. A feature that maps VB syntax onto an ordinary API is not a contract; VB-172, which would have made date literals convert to `DateOnly` and `TimeOnly`, was withdrawn for that reason.

**Facts about symbols from metadata.** What a symbol read from metadata is does not depend on the language version that reads it, as Roslyn designs symbols. So a few changes made for `vbnext` also show on Visual Basic 17.13, through the compiler API and the IDE, and not through compiled code, for example: a C# record class reports `IsRecord`, a C# `in` parameter reports `RefKind.In` and displays as `ByRef ReadOnly`, and symbol display writes `ByRef` for a `ByRef` local and `ParamArray` for a collection parameter. They are accepted as an exception to [compatibility by default](#core-principles), which keeps the meaning of code; a new change of this kind is recorded here.

## Classifying a proposal

Before a feature is recommended or implemented, it is classified as one of:

- **Native VB syntax.** VB developers author the capability with new VB syntax.
- **Interop/runtime support.** VB consumes or emits the .NET capability without new syntax.
- **Native syntax + interop.** Both are justified, and each is evaluated on its own.
- **Tooling/diagnostics.** No new syntax and no new runtime capability: what the compiler reports, such as diagnostics and the operations that analyzers see. VB-170, which stops a cascading error, is one.
- **Deferred.** Possibly valuable, but not yet justified, too risky, too complex, or waiting on another decision.
- **Rejected.** Conflicts with the mission, adds unacceptable complexity, or has too little value.

A wave may start from a probe of what C# or .NET code does that VB cannot yet do. A probe finds candidates; it does not justify them. Each candidate is classified and argued against these principles before it joins a wave.

## Design questions

Every proposed enhancement answers:

1. What concrete problem does this solve for VB developers?
2. Why are existing VB language features insufficient?
3. Is this a runtime or .NET capability, an interop need, or a native VB authoring need? Is it a language, compiler, diagnostics, or tooling gap, or an API that the target framework or a library already provides, or lacks, as [Language and framework API boundary](#language-and-framework-api-boundary) distinguishes them? If the feature depends on a specific framework type or version, what justifies that, and what happens on a target that lacks it?
4. Why is the proposed syntax natural and readable in VB?
5. Who benefits: beginners, everyday application developers, library authors, advanced systems developers, or several of these?
6. What are the compatibility and language-version implications?
7. What are the effects on overload resolution, type inference, evaluation order, nullability, exceptions, and existing code?
8. What compiler, IDE, diagnostics, documentation, and testing work is required?
9. What are the alternatives, including supporting the capability through interop without new syntax?
10. What complexity does this add to the language as a whole?

A native-syntax proposal includes a short **Why this is VB** section: why existing VB features are not enough, and why the proposed authoring experience is natural and readable in VB.

## Design review rules

- Separate the capability from its proposed syntax.
- Separate runtime support from native language authoring.
- Prefer existing VB idioms when they solve the problem cleanly.
- Do not introduce syntax only to make VB code resemble C#.
- Do not reject a feature only because it originated in C#.
- Treat the cumulative complexity of the language as a design cost.
- Review interactions with previously accepted features, not just the feature alone.
- Do not silently change a previously accepted decision. If a proposal conflicts with a principle or an earlier decision, stop and explain the conflict.
- New syntax is not invented during implementation. Its spelling is locked in a syntax note, chosen by the project, before the slices that implement it.

## Adopted spellings under review

Some spellings adopted before this document are symbolic and follow C#:

- `^1` and `a..b` for indexes and ranges (VB-006)
- `?=` for nothing-coalescing assignment (VB-010)
- `..` for a spread in collection and array literals (VB-035)

They stay adopted and implemented, and code that uses them keeps compiling on `vbnext`. VB-174 reviewed them against the keyword preference in [Direction](#direction), and [vbnext-vb174-review.md](vbnext-vb174-review.md) records the decision: all four stay. `a..b` and `x ?= y` are settled; `^n` and `..items` stay flagged as candidates for a keyword alternative, which would be its own proposal.

## Completion status

A feature is not complete because the parser or compiler accepts it. These statuses are distinct:

| Status | Meaning | How the project documents show it |
| --- | --- | --- |
| Design accepted | The capability, its classification, and any syntax are approved. | A decision marked Adopted in the roadmap, a done slice that locks syntax with its `vbnext-vbNNN-syntax.md` note, or a wave plan the project chose. |
| Implementation started | Some compiler slices of the phase are done. | A phase with both Done and Open slices in [vbnext-phases.md](vbnext-phases.md). |
| Compiler implementation complete | Every compiler slice of the phase is done on `vbnext`, with tests. | Every compiler slice of the phase is Done, and the row in [vbnext-missing-features.md](vbnext-missing-features.md) reads "Done in phase N". |
| Developer-facing feature complete | The editor and analyzers follow the feature, and it runs in Visual Studio. | The wave's IDE phase is Done. A phase that adds no syntax needs no IDE slice, so it reaches this status with its compiler slices. |
| Documented and released | Documented for users and shipped in a release. | No feature has reached this status. The `HelloVbNext` sample and these planning documents are not user documentation or a release. |

"Done" on a slice means that slice is done, not that the feature is. These statuses apply to rows written after this document; earlier rows are not retagged.

## When a decision is made or changed

1. Update the document that records the decision: the roadmap, a syntax note, or the feature plan.
2. Update the proposal's status in [vbnext-missing-features.md](vbnext-missing-features.md).
3. Update [vbnext-phases.md](vbnext-phases.md) if planned work is affected.
4. Update the language specification or feature documentation when appropriate.
5. Record deferred and rejected alternatives, with the reason.
6. Keep this document the one statement of the mission; link to it rather than restating it.

A decision is not invented, and a proposal is not marked accepted, without explicit project approval. If a decision is unclear, record it as unresolved and ask.

## Exploring an enhancement

1. Check this document, the roadmap, the feature plan, the syntax notes, and earlier decisions.
2. Find whether the idea is already proposed, accepted, deferred, rejected, or implemented.
3. Classify it and answer the design questions.
4. Present the recommendation and its tradeoffs before implementation.
5. Do not implement a change to the language surface until its design is approved.
6. When implementing an approved feature, stay within the approved syntax and semantics, and flag any expansion of scope.
7. Exploration is not approval.

When uncertain, do not add syntax or begin implementation. Investigate, explain the uncertainty, and recommend the smallest coherent VB-first design that preserves compatibility and the mission.

## Proposal template

A new proposal uses these sections:

- **Proposal.** Name and a short description.
- **Classification.** Native VB syntax, Interop/runtime support, Native syntax + interop, Tooling/diagnostics, Deferred, or Rejected.
- **Developer problem.** The concrete problem being solved.
- **Why this is VB.** Why the capability, and any syntax, fit VB.
- **Alternatives.** Existing VB idioms and interop-only support.
- **Compatibility and semantic impact.** Existing code, language version, overload resolution, inference, evaluation, runtime behavior, and edge cases.
- **Complexity and risks.** Language, compiler, IDE, learning, maintenance, and ecosystem costs, including how the proposal grows the syntax and pattern surface and what lookahead or backtracking it adds to the parser, since parser simplicity is a ranked goal.
- **Implementation and documentation impact.** Affected compiler and IDE areas, tests, specification, roadmap, and phases.
- **Recommendation.** Proceed, revise, defer, reject, or request design approval.

A syntax note that locks new syntax includes the **Why this is VB** section. Notes written before this document are not rewritten.
