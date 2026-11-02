# Drift review after wave 33

This note records a review of the project against [vbnext-mission.md](vbnext-mission.md) and its [language and framework API boundary](vbnext-mission.md#language-and-framework-api-boundary), made after wave 33 and before wave 34 was probed, and the project's decisions on it. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

## Where the project holds to the mission

- New syntax is almost all keyword-based and VB-shaped: `Async Iterator Function`, `Await Each`, `Into`, `Field`, `Record Class`, `ByRef ReadOnly`, `With` on values, and `Case Is (1 To 9, "draft")`. The symbolic exceptions were reviewed in [VB-174](vbnext-vb174-review.md).
- Every new form is gated on `LanguageVersion.VbNext`, every slice checks that Visual Basic 17.13 keeps its behavior, and code that targets .NET Framework 4.7.2 builds; the Visual Basic suites run against it.
- Every feature that depends on a framework type implements a contract that .NET or C# defines, or binds members by name. VB-172 was the exception, and it was withdrawn.

## Findings and decisions

| # | Finding | Decision |
| --- | --- | --- |
| 1 | The Direction names VB Razor and VBHTML, and VB MAUI, as two of the three target stacks, and ranks Razor and XAML compatibility fourth and fifth among the goals. Since VB-060, statement blocks in XML literals, no wave has addressed them; the plans mention them only in one parked line. | Keep the Direction, record the gap here, and decide later. |
| 2 | Waves 24 to 32 were planned from probes of C#. In [vbnext-missing-features.md](vbnext-missing-features.md), 19 rows justify a gap with what C# writes, and 2 give why it is VB. | Already addressed: from wave 33, rows give a classification and why it is VB. Earlier rows are not retagged. |
| 3 | IDE slices check quick info, completion, and classification. The debugger's expression evaluator has no `vbnext` tests, and code fixes, refactorings, and Edit and Continue have few. "Developer-facing feature complete" in the mission covers more than has been checked. | Plan a hardening wave for the debugger expression evaluator, formatting, Edit and Continue, and code fixes on the Vb.Next forms. |
| 4 | `IsRecord` for C# records, `RefKind.In` for C# `in` parameters, and symbol display for `ByRef` locals and collection `ParamArray` parameters do not depend on the language version, so they show on Visual Basic 17.13 through the compiler API and the IDE. | Accept them as facts about symbols from metadata, as Roslyn designs symbols, and record the exception in the mission. |
| 5 | The mission's list of compiler contracts left out the span conversions of VB-136, the C# record conventions of VB-145 and VB-148, the range members `Count` or `Length` with `Slice` or `Substring`, and VB's own spread pattern, `AddRange` then `ToArray`. | Extend the list in the mission, and record the spread pattern as VB's own, justified by VB-035. |
| 6 | The phase map in [vbnext-phases.md](vbnext-phases.md) stopped at phase 112; two sections of [vbnext-roadmap.md](vbnext-roadmap.md) describe the project before wave 0; and the "What starts now" paragraph had grown to one paragraph for every wave. | Extend the phase map to phase 160, mark the two roadmap sections historical, and split "What starts now" into a paragraph per wave, with its text unchanged. |
| 7 | Parser simplicity is a ranked goal, but no document weighs how the syntax and pattern surface grows, such as VB-004 with VB-171, or the lookahead and backtracking the parser adds for them. | Add the question to the proposal template's "Complexity and risks", and review the pattern surface in the next wave. |

## What follows

- Wave 34 is a hardening wave, as finding 3 decides, and it reviews the pattern surface, as finding 7 decides.
- The target stacks of finding 1 stay open for a later decision.
