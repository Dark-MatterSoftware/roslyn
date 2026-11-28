# Introduce Parameter on a For Each control variable

**Implementation complete and verified within authorized scope — pending formal owner acceptance.**

The owner accepted disposition A and authorized the bounded shared eligibility correction. Sections 1–20 are the accepted assessment record and describe the repository as it was before that correction. Section 21 records the correction, the executable verification, and the remaining limitations. The candidate stays unassigned. This note does not assign a feature identifier, phase, slice, or wave, and it does not close the defect or reopen VB-030 or VB-187.

The canonical mission is [vbnext-mission.md](vbnext-mission.md). This note does not restate it.

In sections 1–20, a static finding is something the assessment read in source, and a historical result is taken from a named earlier artifact. Those sections were written before this correction was built or tested. Section 21 separates that historical record from the RED, GREEN, and suite runs executed for the correction.

## 1. Assignment authorization

The owner authorized a bounded static assessment of the unassigned Introduce Parameter defect in which a `For Each` control variable is unavailable at rewritten call sites. The authorization is assessment and documentation only. It does not authorize a correction, a test change, a build, a restore, a probe, or execution of an existing assembly.

VB-187 remains formally closed at `388db4fd03419bc8b4c8548d4043c66e053abbcf`. VB-030 remains formally closed at `c5b21efac74b4f9785ed3b0c93e3ed290d9e2007`.

## 2. Authoritative defect wording

The wave 35 validation, [vbnext-wave35-validation.md](vbnext-wave35-validation.md), records this upstream finding:

> introduce parameter on a `For Each` control variable read in the loop moves the name to the call sites, where it is not declared, for `For Each order In orders` as for the names of `For Each (size, state) In orders`.

[vbnext-missing-features.md](vbnext-missing-features.md) repeats that sentence and, under "Not part of wave 36", keeps it with the other unassigned upstream defects. The VB-187 closure names the same candidate and does not give it an identifier.

A separate inventory sentence, from VB-179, says introduce variable and introduce parameter for all occurrences replace the control variable of `For Each x In`. That sentence is not this candidate. This assessment does not close it.

The conceptual method pair in the assignment, with `Console.WriteLine(item * 2)` inside `For Each item In items`, is an investigation aid. It is not an observed reproduction. The preserved probe edits introduce the control-variable identifier, not a larger arithmetic expression.

## 3. Historical evidence

The preserved probe is `artifacts/probe35/report-wave35.txt`, identical to `artifacts/probe35/report.txt`. SHA-256 `1AF1284EF8390447E9D77F2EEE9D99F8AE6F212E6E9D7962B7D7C487DB72E40E`, 489,278 bytes, 1,215 lines. The harness that produced it is `artifacts/probe164/VbNextCodeActionProbe.vb`. This assessment did not rerun it.

The harness reparses the HelloVbNext sample as Visual Basic 17.13, collects those error spans, and on the `vbnext` compilation adds a zero-width span, the token span, and every ancestor span. Nested code actions are applied. Identical report lines are deduplicated. A row exists only when the edit adds a compiler error. Absence of a row is not evidence that an action was suppressed.

The sample statement the validation names is still `vbnext/samples/HelloVbNext/WaveThirtyFour.vb` line 24, the body of `For Each (size, state) In orders`:

`labels.Add($"{size}:{Label((size, state))}")`

For `size` and for `state`, the probe records six breaking Introduce Parameter rows each: the zero-width span and the identifier span, and for each span the three actions. The direct-update edit inserts the bare name at `Program.vb` and the first diagnostic is BC30451, "'size' is not declared" or "'state' is not declared". The extracted-method edit inserts `Return size` or `Return state` in a new `Public Shared` function. The new-overload edit inserts `Run(size)` or `Run(state)` in a new `Public Shared Sub Run`. Those two actions also report BC30433 first, then BC30451. BC30433 is the VB-187 `Shared` defect, already corrected. BC30451 is this defect.

The same six-row pattern, with BC30451 naming the control variable, is present for these current sample reads. Line numbers still match the current files:

| Read | Form in the current sample | Probe rows |
| --- | --- | --- |
| `WaveThirtyFour.vb(24)` `size`, `state` | `For Each (size, state)` | 6 + 6 |
| `WaveThirty.vb(20)` `count` | `For Each ((name, count As Long), )` | 6 |
| `WaveThirtyTwo.vb(20)` `points` | `For Each (points, subject)` | 6 |
| `WaveTen.vb(48)` `square` | `For Each square In Squares()` | 6 |
| `WaveTwenty.vb(23)` `[step]` | `For Each [step] In steps` | 6 |
| `WaveSeven.vb(25)` `item`, `price` | `For Each (item, price)` | 6 + 6 |
| `WaveNine.vb(67)` `score` | `For Each score In scores` | 6 |

That is 9 control-variable names, 7 statements, 54 breaking Introduce Parameter rows, 18 for each action. Thirty-six of the 54 also carry BC30433, because the extracted-method and new-overload edits were generated in a `Module`. The 18 direct-update rows are BC30451 without BC30433. They do not depend on the VB-187 correction. `subject` and `name` are declared by those loops and are not read in the body, and they have no such rows.

The whole Introduce Parameter break count in the file is 1,089 rows: 444 extracted-method, 423 new-overload, 222 direct-update. BC30451 appears on 343 of them. Those 343 are not 343 copies of this defect. Many are ordinary locals, type parameters, or other names. The 54 rows above are the ones whose diagnostic name is a `For Each` control variable at the loop body that still matches the current sample.

`For Each order In orders` does not occur in the sample or in the probe. The validation says the same edit was compared on a Visual Basic 17.13 form. That comparison source and its diagnostics were not preserved. The claim is historical testimony, not a recovered edit.

Two related historical rows are not counted as this defect:

- On the same `WaveThirtyFour.vb(24)` span, three rows introduce `labels`, the `Dim` local, and report BC30451. Same eligibility hole, different variable.
- Introduce Variable for all occurrences of `square`, `score`, and `[step]` reports BC30451 because it inserts `Dim square1 = square` outside the loop. That is the separate VB-179 all-occurrences sentence. No Introduce Parameter row in the file uses the title "all occurrences".

## 4. Current source status

At the time of the assessment, the defect remained. No commit after the probe had changed the eligibility walk that missed these names. Section 21 records the later correction of that walk.

`ShouldExpressionDisplayCodeActionAsync` still takes `expression.DescendantNodes()` and returns no actions when `GetSymbolInfo` yields `ILocalSymbol` or `IRangeVariableSymbol`. The blame for that walk is `4262648cadff` (Cyrus Najmabadi, 2024-02-27), an ancestor of this branch. The local-symbol rejection itself comes from `7c814a8896ca` (Ankita Khera, 2021). This assessment did not fetch `dotnet/roslyn` and does not claim that repository's current text.

VB.Next commits that touch Introduce Parameter do not change that walk:

- `c4ffca9d60c` adds `IsValidLanguageSpecificExpression` for `TypeOf ... Into` (VB-186).
- `24c4d6fb7ba` stops generating `Shared` in a `Module` (VB-187a).
- `05cd8208345` keeps generic type arguments on generated invocations (VB-187b).

An `IdentifierNameSyntax` has no child syntax nodes. When the selected expression is the control-variable name, `DescendantNodes()` is empty, the local check never runs, and all three actions are offered. The rewriter then copies that name into the call site, the extracted method, or the generated overload. `MapExpressionToParametersAsync` uses the same descendant-only walk and only remaps `IParameterSymbol`, so a local name is pasted unchanged.

A larger expression that contains the name as a descendant is the case the existing check is written to reject. `TestExpressionCaseWithLocal` in the Visual Basic and C# Introduce Parameter tests covers `l * x * y * z`, not a bare local and not `For Each`. The preserved probe's breaking selections are the identifier and the zero-width span on it, not the surrounding interpolation or addition. This assessment does not claim those larger selections failed.

## 5. Relevant architecture

Introduce Parameter is one shared provider with a Visual Basic and a C# export.

| Step | Where |
| --- | --- |
| Availability | `VisualBasicIntroduceParameterCodeRefactoringProvider`, `CSharpIntroduceParameterCodeRefactoringProvider` |
| Selection and common eligibility | `AbstractIntroduceParameterCodeRefactoringProvider.ComputeRefactoringsAsync` |
| VB-only exclusion | `IsValidLanguageSpecificExpression`, currently `TypeOf ... Into` only |
| Local and range-variable exclusion | `ShouldExpressionDisplayCodeActionAsync` |
| Actions | direct update (`Refactor`), extracted method (`Trampoline`), new overload (`Overload`); each also has an all-occurrences twin |
| Call-site search | `FindCallSitesAsync`, deferred until the action runs |
| Parameter remapping and edits | `IntroduceParameterDocumentRewriter` |
| Module destination | `CreateMethodDeclarationAsync`, `CodeGenerationDestination.ModuleType` since VB-187a |

The check that an expression can be evaluated outside the method is the local and range-variable walk. It is incomplete for a selected node that is itself the local reference. It is not missing entirely, and the later VB-187 edits do not bypass it. There is no separate data-flow pass and no check that the name is in scope at the destination.

## 6. Symbol and scope analysis

Visual Basic `For` and `For Each` locals are built in `ForOrForEachBlockBinder`. An inferred `For Each x In items`, an `As` clause, and a deconstruction name that does not already refer to a variable are `LocalSymbol` values. Their scope is the loop, not the caller. A name that already refers to a variable is not redeclared: a parameter, a local, or a field can be the control variable. A property cannot (`ERR_LoopControlMustNotBeProperty` in `Binder_Statements.VerifyForLoopControlReference`).

The eligibility walk distinguishes symbols only by kind, and only on descendant nodes:

| Symbol | Selected identifier | Expression that contains the identifier |
| --- | --- | --- |
| Parameter of the containing method | Rejected, because `GetSymbolInfo` on the expression itself is that parameter | Offered. Parameters are remapped to the caller's arguments |
| Other local, including a `For Each` control variable | Offered. The hole | Not offered, when `GetSymbolInfo` returns `ILocalSymbol` |
| Range variable | Same hole if the selection is the name | Not offered |
| Field, property, constant that is not a local | Offered. Not this defect | Offered, unless some other check rejects it |
| Two locals with the same name | Not distinguished, because the local check does not see the selected name | Distinguished, because `GetSymbolInfo` returns the symbol in scope at that node |

Pasting the original identifier does not preserve symbol identity. At the call site the name binds to whatever is in scope there. The preserved rows bind to nothing and report BC30451. A same-named symbol at the destination would be a different binding. No preserved `For Each` row shows that silent success. Text matching is not a suitable repair; the existing check is already symbol-based and needs to see the selected node.

## 7. Action-variant analysis

All three actions copy the selected expression out of the loop. None of them keeps it inside the loop body.

| Action | Offered today for the control-variable name | Historical edit | Can the expression stay in a scope that has the variable? | Behavior preserved? |
| --- | --- | --- | --- | --- |
| Direct update | Yes | `Program.vb` gains the bare name. BC30451 | No. Callers of `Run` are outside the loop | No |
| Extracted method | Yes | `Return size` in a new module-level function. BC30433 and BC30451 in the probe | No. The new method is inserted before the original method, not inside the loop | No |
| New overload | Yes | `Run(size)` in a new `Run`. BC30433 and BC30451 in the probe | No. The original signature's replacement calls the new method with the expression | No |
| All-occurrences twins | The provider registers them. The probe's nested titles do not say "all occurrences", and identical lines are deduplicated | Not separately recovered | The match walk can include any equivalent expression in the method, which for a simple `For Each x` can include the header name | No, for the same scope reason |

After VB-187a, the extracted method and the overload in a `Module` no longer add `Shared`. The pasted name is still not in scope. That is a static finding. The probe was not rerun, so this note does not say those rows now compile or now fail with only BC30451.

Suppressing the action is the policy already stated on the local-symbol check and already expected by `TestExpressionCaseWithLocal` for an expression that contains a local.

## 8. Behavioral preservation

A control variable is assigned once per iteration. Moving a read of it to a caller evaluates it once per call, before the loop, in a scope that does not have that variable. A generated edit that compiled would still change the value, the frequency, and the timing.

The same is true when the control variable is an existing parameter or field. Those are not rejected as locals. The refactoring would treat a parameter as an ordinary parameter and remap it to the caller's argument, so the caller computes one value instead of the loop assigning the parameter on each iteration. A field name would be pasted and read at the caller before the loop writes it. Neither shape appears in the preserved `For Each` rows. They are static findings, not historical failures.

An expression inside the loop that does not use the control variable, such as a literal, is a normal Introduce Parameter candidate. The loop does not by itself make it invalid. Side effects in such an expression already move to the caller under the existing design. This assessment does not propose a new analysis for that general move.

Captured loop variables, `ByRef` control variables, and values mutated during the loop are still locals or still assigned by the loop. Moving the read does not preserve the capture or the per-iteration value. No new delegate, closure, or collection should be invented to make the operation succeed.

## 9. Scenario matrix

"Historical" means a preserved probe row, not a run performed now. "Static" means current source. Unexecuted scenarios are not called tested.

| Scenario | Evidence | Expected behavior | Static finding | Uncertainty |
| --- | --- | --- | --- | --- |
| Implicit control variable (`square`, `score`, `[step]`) | 18 probe rows, BC30451 | Do not offer | Offered when the selection is the name | None for the identifier. Compound uses were not recovered |
| Explicit `As` clause (`count As Long` in a deconstruction) | 6 probe rows | Do not offer | Same hole. The symbol is still a local | A non-deconstruction `For Each x As T` body was not a recovered row |
| Previously declared parameter | No probe row | Do not offer when the loop assigns it | The parameter check rejects the bare name and allows an expression that uses the parameter | Needs a future test. Compiling is not correctness |
| Previously declared local | No separate probe row | Do not offer | Same local hole for the bare name | Not separately recovered |
| Previously declared field | No probe row | Do not offer when the loop assigns the field | Offered. Fields are not locals | Needs a future test |
| Nested `For Each` | No probe row | Do not offer for either control variable | Each is a local | Not recovered |
| Shadowed names | No silent-success row | Keep symbol identity. Do not offer | Pasting the text can bind a different symbol | Not observed |
| Expression independent of the control variable | Existing tests use parameters and literals | Keep offering | The local check does not reject it | Not re-executed |
| Direct reference, the identifier | 54 probe rows | Do not offer | Offered | Established |
| Compound expression that uses the control variable | Not a preserved failing edit. C# and VB tests expect a compound local expression to be refused | Do not offer | Descendant walk should refuse it | Not re-executed for `For Each` |
| Iteration-local state other than the control variable | `labels` on the same line, 3 rows | Do not offer | Same hole | Same mechanism, not a second root cause |
| Captured control variable | No probe row | Do not offer | Still a local | Not recovered |
| Side-effecting expression with no local | No `For Each` row | Existing move-to-caller design | Still offered | Out of scope |
| Direct update | 18 of the 54 rows, BC30451 only | Suppress | Offered and pastes the name | Established historically |
| Extracted method | 18 rows, BC30433 and BC30451 | Suppress. `Shared` is already corrected | Still pastes the name outside the loop | Probe not rerun after VB-187a |
| New overload | 18 rows, BC30433 and BC30451 | Suppress | Same | Probe not rerun after VB-187a |
| Generic method | VB-187b tests, not `For Each` | No interaction | The hole does not depend on generics | Do not disturb VB-187b |
| Module member | The 54 rows are in modules | Same suppression. No `vbnext` gate | Module destination is already fixed | Direct-update rows never needed the `Shared` fix |
| Class instance or `Shared` member | No recovered `For Each` row | Same suppression | The provider is not module-specific | Future test if the shared check changes |
| Visual Basic 17.13 | Validation testimony for `For Each order In orders`. Source not preserved | Same suppression. `For Each` is old syntax | The check is not language-version gated | The 17.13 edit text is missing |
| Explicit `vbnext` | Deconstruction rows | Same suppression | Deconstruction locals are ordinary locals | Established for the identifier |
| Deconstruction, including nested and empty slots | `size`, `state`, `points`, `item`, `price`, `count` | Do not offer | Same hole | Nested-list body reads were not a separate recovered set beyond `count` |
| `Await Each (round, score)` | 6 rows each for `round` and `score` at `WaveSeven.vb(45)` | Same suppression | Same hole | Related iteration form, not a second cause |
| `For` loop control variable | No probe row | Same suppression | `ForOrForEachBlockBinder` builds that local too | Not recovered |
| C# `foreach` identifier | No C# probe | Do not offer | The hole is in the shared method | Requires C# tests if the shared method changes |
| C# compound local | `TestExpressionCaseWithLocal` is `TestMissing` | Keep refusing | Descendant walk | Not re-executed |

## 10. Root-cause confidence

One mechanism. It is not joined to VB-187.

1. Established. `ShouldExpressionDisplayCodeActionAsync` inspects descendants only. A selected control-variable identifier has none. The rewriter pastes that identifier. The probe shows the paste and BC30451 for all three actions. Current source still has the walk. The walk is upstream-authored history in this fork. VB-187a and VB-187b did not change it.

Not a second root cause: BC30433 on the extracted method and the overload. That is closed VB-187. Thirty-six of the 54 rows contain both diagnostics. The 18 direct-update rows contain only BC30451.

2. Strongly supported, not executed. Including the selected node in the existing `GetSymbolInfo` walk would classify these locals and suppress every action, which is what the comment on that walk already says. A compound expression is already on that path.

3. Plausible, not observed in these rows. A destination that already has the same name can bind the pasted identifier to a different symbol. A parameter or field used as the control variable can produce an edit that compiles and still changes iteration behavior. All-occurrences can replace a header identifier when it is semantically equivalent to the selected read.

4. Undetermined. The exact 17.13 `For Each order In orders` edit. Whether any compound `For Each` expression was offered. Current `dotnet/roslyn` text.

## 11. Correction alternatives

Alternative A, suppress the action when the expression depends on a variable the destination does not have. This is the existing policy. The comment says locals and range variables are refused because call sites would be wrong.

Alternative B, correct the existing eligibility walk so the selected node is included. This is the same policy as A, applied to the node the probe actually selected. It does not add a `For Each` special case. It also covers an ordinary local identifier such as `labels`, which is the same hole. A `For Each`-only check would miss that hole and would duplicate the symbol test that already exists.

Alternative C, keep a valid transformation. No current action can. Each one evaluates the expression outside the loop. A new delegate or collection is out of scope and is not recommended.

Alternative D, preserve the implementation. Not applicable. The hole is still in source.

Alternative E, more execution before any recommendation. Not required to identify the hole or the historical failure. Execution is required before a correction is accepted, and it is specified in section 14. It is not the primary disposition.

Correctness favors A implemented as B. The change is one shared predicate, consistent with Roslyn's existing structure. Compatibility risk is that bare local identifiers, in Visual Basic and in C#, stop offering Introduce Parameter. That is the intended refusal. Regression risk is an expression that is only a parameter or a literal and must stay available. Those do not bind as locals.

## 12. Compatibility assessment

This is ordinary IDE correctness. `For Each` control variables and method locals exist in Visual Basic 17.13. The check is not gated on `LanguageVersion.VbNext`, and a correction should not be. `default` and `latest` stay 17.13. Explicit `vbnext` stays opt-in for language forms. Deconstruction and `Await Each` are `vbnext` syntax that happen to exercise the same old hole. Compiler semantics, framework targets, and valid Introduce Parameter operations on parameters and literals stay as they are.

## 13. Cross-language implications

The eligibility hole and the paste are shared. The historical failures are Visual Basic. C# `foreach` iteration variables are locals, so the same bare-identifier selection is the same shared hole. This assessment does not change C#. A future shared correction needs the C# Introduce Parameter tests, including a bare local or `foreach` identifier that must not be offered, and the existing compound-local test that must remain missing.

## 14. Proposed future verification

Not authorized now. If the owner later authorizes a correction:

- Add one failing Visual Basic test that selects the control-variable identifier, for an implicit `For Each`, and expects no action. Include one deconstruction name and one ordinary local identifier, because the walk is not `For Each`-specific.
- Change only the shared eligibility walk so the selected node is inspected with the same `ILocalSymbol` and `IRangeVariableSymbol` test.
- The same test then passes. Direct update, extracted method, and new overload are absent for that selection.
- A parameter expression and a literal stay offered, including in a `Module`, and the VB-187a and VB-187b tests stay as they are.
- Add the C# counterpart for a bare local or `foreach` identifier.
- Do not treat a compiling edit as success. The action should be absent, so evaluation frequency does not change.
- Run the Visual Basic and C# Introduce Parameter tests in `src/Features/VisualBasicTest/Microsoft.CodeAnalysis.VisualBasic.Features.UnitTests.vbproj` and `src/Features/CSharpTest/Microsoft.CodeAnalysis.CSharp.Features.UnitTests.csproj`. A full solution build is not required for that focused change. Compiler suites are not required if no compiler project changes.
- Fresh artifact provenance for any Features assembly that is executed. The accepted compiler baseline remains `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`, Release `net10.0`, 6,553,600 bytes, SHA-256 `3DBDC692E7D1F8FA3E0EBBF6643561E04A8FD0042285938FA6E33E75A952AB70`. The Features discrepancy and the absent Workspaces artifact recorded at VB-187 closure are unchanged. This assessment did not rebuild or run them.

Language version in the test should include 17.13 and explicit `vbnext` for a deconstruction form. No `vbnext` gate belongs in the product change.

## 15. Recommended disposition

Primary disposition: A. A bounded corrective implementation is recommended.

The recommendation is Alternative B, which is how Alternative A's existing policy covers the node the probe selected. It is a recommendation, not authorization. Alternatives C and D do not fit. Alternative E is the verification that follows authorization, not a reason to delay the recommendation.

## 16. Proposed planning classification

Leave the candidate unassigned. Do not open a wave, phase, or slice, and do not invent a feature identifier in this assessment.

When the owner wants the correction, the right vehicle is an ordinary IDE-correctness change in the existing Introduce Parameter provider, recorded as a tooling fix of an upstream defect. It does not need a language-feature identifier to be true, and it should not be folded into VB-187. VB-187 is closed and has a different cause.

## 17. Release-readiness implications

[vbnext-release-plan.md](vbnext-release-plan.md) does not name this candidate. Gate P6 treats the upstream defects in the feature plan as known limitations. VB-187's closure says the other upstream defects remain separate decisions and are not Preview 1 blockers. This candidate is one of those defects.

Recommended classification, without changing the release plan: a known limitation, not a Preview 1 blocker and not, by itself, a statement about VB.Next 1.0 readiness. This assessment does not say that desktop development or VB.Next 1.0 is release-ready.

## 18. Mission review

The assessment supports desktop and server IDE correctness. It leaves Visual Basic culture, existing applications, and compiler semantics unchanged. It does not add syntax, a framework API, or a language-version gate. The recommended future change is the smallest use of the shared refactoring that already implements this policy. It does not touch UnisonCore. No mission amendment is required.

## 19. Explicit limitations

- No build, restore, test, probe, or assembly execution.
- The 54-row count is a reading of one preserved file, correlated with the current sample. It is not a new probe.
- The 17.13 `order` comparison was not recovered as an edit.
- Compound expressions, nested loops, captures, fields, and parameters-as-control-variables were not historically executed for this defect.
- Introduce Variable all-occurrences on `square`, `score`, and `[step]` is a separate observation.
- The unqualified call-site expression, anonymous types typed as `Object`, and `Global.` tuple conversion stay unassigned and were not assessed.
- Protected owner files were dirty in the working tree at the start and were not modified. Their hashes matched the authorized values.

## 20. Required owner decisions

1. Accept or reject disposition A.
2. If accepted, authorize the bounded correction and its tests, including the shared C# test. That authorization is not given here.
3. Decide whether the correction, once authorized, receives a feature identifier. This assessment recommends not inventing one until that decision.
4. Leave VB-030 and VB-187 closed.

## 21. Authorized correction and verification

The owner accepted disposition A and authorized alternative B: include the selected expression in the existing shared eligibility analysis. The production change is one call in `ShouldExpressionDisplayCodeActionAsync`:

`expression.DescendantNodes()` became `expression.DescendantNodesAndSelf()`.

`GetSymbolInfo` still rejects `ILocalSymbol` and `IRangeVariableSymbol`, including a selected identifier that has no child nodes. A compound expression that already contained such a symbol is still rejected through its descendants. A parameter of the containing method is still rejected earlier in `ComputeRefactoringsAsync`, before this walk. No `For Each` special case, language-version gate, compiler change, or other `DescendantNodes()` call was added. `MapExpressionToParametersAsync` was not changed.

The correction was verified on Debug `net472` with `dotnet test -m:1 -p:UseSharedCompilation=false`. The `net10.0-windows` test target was not executed. A targeted restore of each unit-test project was required because no `project.assets.json` existed for them. Package caches were left in place.

RED, with the new tests compiled and the production source unchanged, offered all six nested actions for a bare local. That is an expected assertion failure, not a build or discovery failure.

| Run | Filter | Result | xUnit time |
| --- | --- | --- | --- |
| Visual Basic RED | `FullyQualifiedName~TestLocalSymbolEligibility` | 13 discovered, 2 passed, 11 failed, 0 skipped | 21.2486 s |
| C# RED | same | 7 discovered, 2 passed, 5 failed, 0 skipped | 29.8629 s |
| Visual Basic GREEN | same | 13 passed, 0 failed, 0 skipped | 10.3978 s |
| C# GREEN | same | 7 passed, 0 failed, 0 skipped | 23.4929 s |
| Visual Basic suite | `FullyQualifiedName~IntroduceParameter` | 69 passed, 0 failed, 0 skipped | 43.8100 s |
| C# suite | same | 82 passed, 0 failed, 0 skipped | 33.2695 s |

The 11 Visual Basic and 5 C# RED failures are `TestMissing` failures whose message lists the six nested actions. The two tests that passed on RED in each language are the preservation cases: a parameter of the containing method stays unavailable, and `x + 1` inside a loop still offers six actions. Those are not defect reproductions. An existing rule already rejects the parameter. The arithmetic expression does not select a local.

GREEN covers a Visual Basic 17.13 implicit `For Each` control variable, the same selection with the caret inside the identifier, an explicitly typed `For Each` control variable, `default` and `latest`, a `vbnext` `For Each` deconstruction local, a `vbnext` `Await Each` deconstruction local, an ordinary local and its caret, a shadowed `For Each` name, and a query range variable. The C# tests cover an ordinary local, a `foreach` iteration variable, both as a span and as a caret, and a query range variable. `default` and `latest` were executed, not only inferred. No `vbnext` gate was added.

The suites reran `TestExpressionCaseWithLocal`, the literal and parameter-expression rewrites, `TestInModuleTrampolineHasNoShared`, `TestInModuleOverloadHasNoShared`, and the generic-method tests, including `TestGenericMethodTrampolineCallSitePreservesTypeArguments` and `TestGenericMethodOverloadPreservesTypeParameters`. VB-187a and VB-187b were not modified.

Fresh Debug `netstandard2.0` provenance, copied into the `net472` test output and confirmed by equal hashes:

| Assembly | When | Bytes | SHA-256 |
| --- | --- | --- | --- |
| `Microsoft.CodeAnalysis.Features.dll` | RED, production unchanged | 7,220,224 | `A607A2D046B4A04B719C6FDB999E4D5AF4C2A1CB6556A974ECD3FE455B98CBA4` |
| `Microsoft.CodeAnalysis.Features.dll` | after the correction | 7,220,224 | `3CB3E1F0BAC1548F424441D30F3B29A8CD2DB55025222FDF4E8B598345522423` |

The Visual Basic compiler, Workspaces, and Visual Basic Features assemblies kept the same hashes across that correction. The historical Release `net10.0` compiler record, commit `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`, size 6,553,600, SHA-256 `3DBDC692E7D1F8FA3E0EBBF6643561E04A8FD0042285938FA6E33E75A952AB70`, is not this run. The historical Features hash remains the malformed value recorded at that closure and is not treated as current provenance.

The preserved probe's 54 `For Each` rows, 12 `Await Each` rows, and 3 ordinary-local rows were not rerun. The correction addresses the eligibility walk those rows exercised. It does not claim that all 69 rows, or the 1,089 Introduce Parameter breaking rows, were executed again. `For Each order In orders` remains unrecovered testimony.

Still outside this correction: a parameter or a field used as a loop control variable, mutation and evaluation-frequency analysis, unqualified call-site expressions, anonymous types or delegates typed as `Object`, `Global.` tuple conversion, and Introduce Variable all-occurrences. Passing the local-symbol tests does not fix those.

The change is ordinary IDE correctness. It advances desktop and server tooling, leaves Visual Basic culture and existing compiled applications unchanged, adds no syntax or framework dependency, and does not alter 17.13, `default`, or `latest` compiler semantics. Valid Introduce Parameter actions remain available. The shared provider changed by one call. UnisonCore is untouched. No mission amendment was made.

Formal closure, a permanent feature identifier, and Wave 37 remain owner decisions. VB-030 stays closed at `c5b21efac74b4f9785ed3b0c93e3ed290d9e2007`. VB-187 stays closed at `388db4fd03419bc8b4c8548d4043c66e053abbcf`.
