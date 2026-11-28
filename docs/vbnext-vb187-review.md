# VB-187 review — Introduce Parameter in a Module

Slice 172a assessment. No production change and no test change. The canonical mission is [vbnext-mission.md](vbnext-mission.md). This note does not restate it.

Evidence labels used below: a fact is something this slice read in source, Git history, or a preserved artifact. A historical result is taken from a named earlier report and was not re-run. A hypothesis is a conclusion the current source supports but this slice did not execute.

## 1. Feature identification

| Item | Value |
| --- | --- |
| Id | VB-187 |
| Title | Assessment of introduce parameter in a `Module`, an upstream defect |
| Wave | 36, row 3 |
| Phase | 172 |
| Slice | 172a |
| Classification | Tooling assessment of an existing Visual Basic IDE refactoring. No new syntax. |
| Provider | `VisualBasicIntroduceParameterCodeRefactoringProvider` |
| Construct | A method declared in a `Module`, and, as a second symptom, a generic method invocation rewritten by the same refactoring |

The assessment concerns existing IDE refactoring behavior. It does not change `Module` semantics, language version, or the compiler.

## 2. Authoritative roadmap scope

[vbnext-missing-features.md](vbnext-missing-features.md) wave 36 defines VB-187 as an assessment with no code change. The row, as it stood at the start of this slice, said introduce parameter adds `Shared` to a method of a `Module`, which BC30433 rejects, and leaves out the type parameters of a generic method. It said the wave 35 probe records that pair as the first error of 822 of the 1,191 edits that break, at 109 places. The assessment was to find whether the two symptoms have one narrow cause, define the expected edit, and state the scope and regression risk of a fix. A fix was to be made only if the project approved it.

[vbnext-phases.md](vbnext-phases.md) phase 172 said the same, and said `docs/vbnext-vb187-review.md` would reproduce the behavior. This authorization does not permit execution, so this note does not reproduce the refactoring. It recovers the original records and inspects the current source.

[vbnext-roadmap.md](vbnext-roadmap.md) has no VB-187 status row. This slice does not add one.

The same wave keeps two other upstream introduce-parameter defects outside VB-187: a `For Each` control variable moved to call sites where it is not declared, and an unqualified expression written at a call site in another module. They are not part of this assessment.

## 3. Owner authorization

The owner accepted the final VB-030 closure and authorized slice 172a only: investigation, static source inspection, existing test inspection, architectural assessment, and documentation. No production correction, no test implementation, and no build or restore. A correction, if still justified, needs a separate owner decision.

## 4. Original observation

Two symptoms were recorded, and they are not one defect.

1. When Introduce Parameter generates a method inside a `Module`, the generated declaration was written `Public Shared`. BC30433 (`ERR_ModuleCantUseMethodSpecifier1`) is "Methods in a Module cannot be declared '{0}'." The probe text uses `Shared`.
2. On a generic method, a generated invocation could omit the type arguments, and in one trampoline shape it could also omit the value arguments. The planning sentence says the refactoring "leaves out the type parameters of a generic method." The later reproduction showed the declaration's `(Of T)` list was already present. What failed was the generated call.

VB-179 recorded the pair as an upstream Visual Basic bug the sample exposes, and gave the `Shared` symptom as 953 of the remaining edits in that earlier probe. Wave 36 then scoped an assessment, not a fix. The planning commit is `98395d5fef3` (2026-11-03), "Plan wave 36: VB-185, VB-186, and the VB-187 assessment."

## 5. Original reproduction evidence

Three preserved records, distinguished from anything this slice ran.

**Wave 35 probe.** `artifacts/probe35/report-wave35.txt` is the same file as `artifacts/probe35/report.txt` (SHA-256 `1AF1284EF8390447E9D77F2EEE9D99F8AE6F212E6E9D7962B7D7C487DB72E40E`, 489,278 bytes). [vbnext-wave35-validation.md](vbnext-wave35-validation.md) says the probe ran the 55 Visual Basic refactorings on the sample. The report header is `INFO 458 17.13 errors, 286 statements, 1958 spans`. An example that is only BC30433, from the first breaking lines:

`Public Shared Function GetValue() As ((String, Integer), Integer)` inserted at `WaveThirty.vb(25)` for "into extracted method to invoke at call sites", and `Public Shared Sub Run()` for "into new overload".

A generic example is at `WaveEleven.vb(40)`, selection `T.DefaultName()`. The extracted-method edit inserts `Public Shared Function GetV(Of T As INamed)() As String` and a call `GetV()`. The diagnostics are BC30433 and BC32050, "Type parameter 'T' ... cannot be inferred." The declaration keeps `(Of T As INamed)`. The overload edit on the same selection inserts `Public Shared Function DefaultOf(Of T As INamed)()` and a body call `DefaultOf(Of T)(...)`, and the recorded diagnostic on that line is only BC30433.

**VB-187a red run, historical.** `D:\Development\VBNext\Evidence\VbNext IDE Test\VB-187\VB-187-REPORT.md`, dated 2026-11-21, HEAD `31a50c81`. Two new tests failed before any fix. The actual text differed from the expected text by `Public Shared Sub M` and `Public Shared Function GetNum` inside `Module Program`. The report says the test host did not assert BC30433 itself. A later compile check, in the VB-187a report, says the same text with `Shared` exits 1 with BC30433, and the text without `Shared` exits 0. Those compile checks were not repeated here.

**VB-187b red run, historical.** `D:\Development\VBNext\Evidence\VbNext IDE Test\VB-187b\VB-187b-PHASE1-REPORT.md`, baseline `24c4d6fb7ba` after the module fix. Ordinary class methods, `LanguageVersion.VisualBasic16`:

- Trampoline with no call site: declarations already kept `(Of T)`. The test passed.
- Overload: declarations kept `(Of T)`. The body call was `M(x, x.GetHashCode())` where the test expected `M(Of T)(x, x.GetHashCode())`. `vbc` accepted the actual text because `T` is inferable from `x As T`.
- Trampoline whose extracted method cannot infer `T`: the call site became `GetNum()` instead of `GetNum(Of Integer)(1)`. `vbc` reported BC30455. A nearby form with the value argument and no type argument reported BC32050.

This slice did not invent a new program and did not run these tests.

## 6. Expected behavior

Established by Visual Basic rules and by the expected text of the tests that were added for the two symptoms.

- A method generated in a `Module` is not declared `Shared`. Module members are already implicitly shared. The explicit modifier is BC30433.
- A generated generic method declaration keeps the type-parameter list copied from the original method.
- A generated invocation keeps type arguments when the extracted method cannot infer them. An inferable `(Of T)` on an overload body may be simplified and still compile. The VB-187b closeout accepted that simplification and did not change the simplifier.
- The choice "and update call sites directly" rewrites the existing method and its callers. It does not generate the extra method that carried `Shared`. The probe's BC30433 rows are not this choice.
- The same expectations hold for Visual Basic 17.13, `default`, `latest`, and explicit `vbnext`. `Module` and generic methods are existing syntax. The defect is not a `vbnext` form.

## 7. Actual historical behavior

Before `24c4d6fb7ba`, the extracted-method and overload choices wrote `Public Shared` on the generated module method. The direct-update choice did not. That is the probe and the VB-187a red diff.

Before `05cd8208345`, generic declarations already kept `(Of T)`. Generated trampoline calls could drop type arguments and, when the call-site parameter identity did not match the definition, the value arguments as well. Inferable overload calls lost the explicit `(Of T)` and still compiled. That is the VB-187b red diff.

A recount of `report-wave35.txt` in this slice, by lines that start with `BREAKS`:

| Count | Meaning |
| --- | --- |
| 1,191 | Breaking edits in the report. This matches the planning figure. |
| 1,089 | Of those, Introduce Parameter. |
| 822 | Introduce Parameter edits whose first diagnostic is BC30433. Every one of them inserts `Public Shared`. This matches the planning figure. |
| 411 | Of the 822, "into extracted method to invoke at call sites". |
| 411 | Of the 822, "into new overload". |
| 0 | Of the 822, "and update call sites directly". |
| 110 | Distinct `at File(line)` locations among the 822. The planning text says 109. The recount does not reproduce 109. The difference is one location and does not change the defect. |

The 822 rows are not 822 edits whose only fault is `Shared`. Many also carry a later diagnostic, including names the call sites do not declare, ambiguous module members, and `TypeOf ... Into`. Those other faults are the separate upstream items and VB-186. BC30433 is the first diagnostic, which is what the planning sentence says.

The wave 35 validation's own findings list does not name the `Shared` or generic symptoms. It names the index-name bug, `TypeOf ... Into`, the `For Each` control variable, and the unqualified call-site expression. The `Shared` and generic symptoms come from the VB-179 review and from the probe file, not from that short findings list.

## 8. Evidence limitations

- This slice did not invoke Introduce Parameter, compile generated code, or run the unit tests. Historical pass and fail counts stay historical.
- The wave 35 probe was not re-run, so this slice does not claim that all 822 rows are gone on the current tree.
- No single copy-paste program in the planning documents is the original probe case. The probe file and the later test sources are the reproductions that were preserved.
- The planning phrase "leaves out the type parameters" does not match the reproduced declaration behavior. Declarations kept their type parameters. Invocations lost type arguments.
- The published "109 places" and this slice's count of 110 locations differ by one. The 822 and 1,191 counts match.
- `docs/vbnext-agent-handoff.md` and `docs/vbnext-release-plan.md` still describe VB-187 as open or deferred. This authorization updates only the phase, feature-plan, and roadmap status records. The roadmap has no VB-187 row, so it is unchanged. Those two other documents were left as they were.
- Fresh Features and Workspaces assemblies were not built. The closure observation still stands: the Features assembly seen at VB-030 closure does not match the previously accepted Features hash, and the accepted Workspaces assembly was absent. Nothing here executed them.

## 9. Introduce Parameter architecture

The provider is Visual Basic-specific. The rewrite is shared.

| Piece | Location |
| --- | --- |
| VB provider | `VisualBasicIntroduceParameterCodeRefactoringProvider` in `src/Features/VisualBasic/Portable/IntroduceParameter/VisualBasicIntroduceParameterCodeRefactoringProvider.vb` |
| C# provider | `CSharpIntroduceParameterCodeRefactoringProvider` |
| Shared availability and actions | `AbstractIntroduceParameterCodeRefactoringProvider` in `src/Features/Core/Portable/IntroduceParameter/AbstractIntroduceParameterCodeRefactoringProvider.cs` |
| Shared rewrite | `IntroduceParameterDocumentRewriter` in `src/Features/Core/Portable/IntroduceParameter/IntroduceParameterDocumentRewriter.cs` |
| VB method generation | `MethodGenerator.GenerateModifiers` in `src/Workspaces/SharedUtilitiesAndExtensions/Workspace/VisualBasic/CodeGeneration/MethodGenerator.vb` |
| Symbol factory | `CodeGenerationSymbolFactory.CreateMethodSymbol` in `src/Workspaces/SharedUtilitiesAndExtensions/Workspace/Core/CodeGeneration/CodeGenerationSymbolFactory.cs` |

`ComputeRefactoringsAsync` selects an expression, finds the containing declaration that has a parameter list, and requires `semanticModel.GetDeclaredSymbol` to be an `IMethodSymbol` whose kind is ordinary, local function, or constructor. It rejects a parameter of that method, a named type, and a type parameter. The VB provider adds `IsValidLanguageSpecificExpression`, which is the VB-186 rule for `TypeOf ... Into`. That hook does not generate methods.

The shared provider registers six nested actions: three choices, each with an all-occurrences twin.

| Choice | Kind | What it generates |
| --- | --- | --- |
| and update call sites directly | `Refactor` | A parameter on the existing method, and a new argument at callers |
| into extracted method to invoke at call sites | `Trampoline` | A new method that returns the expression, then the same parameter update |
| into new overload | `Overload` | A new method that calls the original method with the expression added |

`CreateMethodDeclarationAsync` builds the new method. `GenerateNewMethodInvocation` builds a trampoline call. `GenerateNewMethodOverloadAsync` builds the overload's inner call. `MapParameterToArgumentsAtInvocation` joins call-site arguments to parameters.

Visual Basic-specific behavior begins at the provider's five hooks and at `MethodGenerator`, which knows that a `Module` destination must not be marked `Shared`. The decision to pass that destination, and the decision to emit generic invocations, live in the shared rewriter. C# uses that rewriter. C# has no `TypeKind.Module`; a C# static class is `TypeKind.Class`.

## 10. Relevant Visual Basic Module semantics

These are the distinctions the refactoring meets. Other module rules are left alone.

- A `Module` is `TypeKind.Module`. A class is `TypeKind.Class`. A C# static class stays `TypeKind.Class`.
- `Binder_Utils.vb` treats members of a module as implicitly shared and rejects an explicit `Shared` modifier with `ERR_ModuleCantUseMethodSpecifier1` (BC30433), except a shared member declared in an extension block, which is a static member of the extended type.
- `SourceMethodSymbol.IsShared` is the method's shared flag. For an ordinary module method that flag is set because the binder adds `SourceMemberFlags.Shared` while stripping the explicit modifier. The public symbol is therefore `IsStatic` on the cross-language `IMethodSymbol`.
- The method is still not written with the `Shared` keyword. Emitting `Shared` because `IsStatic` is true produces source the compiler rejects.
- An unqualified call of a module method binds through the module's imports. A qualified call names the module. Introduce Parameter's receiver handling is the existing member-access path in `GenerateNewMethodInvocation`. The `Shared` defect does not depend on that qualification.
- An extension method declared in a module is still a module member. A method the refactoring generates beside it is also a module member and must not be marked `Shared`. This slice did not find a separate extension-method branch in the rewriter.

The rewriter's assumption that matters is: a static method should be generated as `Shared` unless the code-generation destination says the containing type is a module. That assumption is false for a module if the destination is left unspecified, because the symbol is static and the keyword is illegal.

## 11. Source inspection findings

Current source, read in this slice. Not executed.

**Module destination.** `CreateMethodDeclarationAsync` sets the destination to `CodeGenerationDestination.ModuleType` when `_methodSymbol.ContainingType?.TypeKind == TypeKind.Module`, and to `CodeGenerationDestination.Unspecified` otherwise. `MethodGenerator.GenerateModifiers` adds `Shared` only when `method.IsStatic AndAlso destination <> CodeGenerationDestination.ModuleType`. Together, a generated module method is not marked `Shared`. A class method, including a `Shared` class method, still receives `Unspecified`, so an actually shared class method still gets the keyword.

**Generic declaration.** `CreateMethodSymbol(IMethodSymbol method, ...)` copies `method.TypeParameters`. `MethodGenerator.GenerateTypeParameterList` emits that list. This matches the historical finding that declarations were not losing `(Of T)`.

**Generic invocation.** `MapParameterToArgumentsAtInvocation` stores `argumentParameter.OriginalDefinition`. The trampoline then looks up `validParameters`, which are parameters of the method symbol the refactoring started from. For a constructed call such as `M(Of Integer)(1)`, the definition parameter matches. `GenerateNewMethodInvocation` copies explicit type arguments from the invoked name when they are present. When the original call inferred them and the constructed method is generic, it uses that method's type arguments. The name is annotated for simplification, so an inferable argument list may still be removed. `GenerateNewMethodOverloadAsync` already built `GenericName` from `_methodSymbol.TypeArguments` before the VB-187b change; the simplifier remains free to drop an inferable list.

The earliest decision for the `Shared` symptom is the destination argument of `CreateMethodDeclarationAsync`, not the VB provider. The earliest decision for the invalid generic call is the parameter map and `GenerateNewMethodInvocation`, also in the shared rewriter.

## 12. Existing test inventory

Inspected. Not executed. Existence is not a passing run.

| Test | Path | What it covers | Covers the original observation | Executed in this slice |
| --- | --- | --- | --- | --- |
| `TestInModuleTrampolineHasNoShared` | `src/Features/VisualBasicTest/IntroduceParameter/IntroduceParameterTests.vb` | `Module Program`, expression `x * y * z`, code-action index 1. Expected `Public Function GetNum` with no `Shared`, and the original method updated to take `num`. | The extracted-method `Shared` symptom, with no caller. | No. Historical VB-187a report: failed before the fix, passed after. |
| `TestInModuleOverloadHasNoShared` | same file | Same module, plus `M1` calling `M`. Index 2. Expected `Public Sub M` with no `Shared`, body `M(x, y, z, x * y * z)`, and `M1` unchanged. | The overload `Shared` symptom, one caller in the same module. | No. Same historical result. |
| `TestGenericMethodTrampolinePreservesTypeParameters` | `IntroduceParameterGenericMethodTests` in the same file | Class method `M(Of T)`. Index 1. Expected `GetNum(Of T)` and `M(Of T)`. `LanguageVersion.VisualBasic16`. | Declaration type parameters on the trampoline. The historical red run already passed. | No. |
| `TestGenericMethodOverloadPreservesTypeParameters` | same class | Overload body. Expected text is `Return M(x, x.GetHashCode())`, the simplified form. | Inferable omission, accepted as compiling. | No. Historical: failed against an expectation of `M(Of T)` before the product decision to accept simplification. |
| `TestGenericMethodTrampolineCallSitePreservesTypeArguments` | same class | Caller `M(Of Integer)(1)` where the extracted method's parameter does not mention `T`. Expected `GetNum(Of Integer)(1)`. | The invalid omitted type argument. | No. Historical red: `GetNum()`. |
| `TestGenericMethodTrampolineUsesInferredTypeArgumentsNotAvailableToExtractedMethod` | same class | `M(Of T)(x As T, y As Integer)` extracting `y`. Expected `GetNum(Of Integer)(2)`. | Inferred `T` that the extracted method cannot infer. | No. Historical VB-187b closeout: passed after the fix. |
| C# `TestGenericMethodTrampolineCallSitePreservesTypeArguments` and `TestGenericMethodTrampolineUsesInferredTypeArgumentsNotAvailableToExtractedMethod` | `src/Features/CSharpTest/IntroduceParameter/IntroduceParameterTests.cs` | The same two call-site shapes in C#. | Shared rewriter, not modules. | No. Historical closeout: passed. |

The surrounding VB class covers class methods, all occurrences, one and many call sites, optional parameters, named arguments, `ParamArray`, cancellation tokens, constructors, properties, `Me`, and several negative cases (`TestDestructor`, `TestHighlightReturnType`, `TestClassObject`). Those are class or general cases. They do not mention a module. The C# file covers static local functions, `this`, base, overloads, and cross-document calls. C# has no module tests, which is appropriate.

No test combines a generic method with a `Module`. The probe did, at `WaveEleven.vb(40)` and `WaveNine.vb(44)`. The two fixes are independent in the source, so the combination is a coverage gap, not a third mechanism found in this slice.

No test in this inventory is a `Shared` method of a class checked beside the module tests. The destination condition leaves `TypeKind.Class` on `Unspecified`, which is the path the existing class tests already use.

## 13. Upstream provenance assessment

Local history only. No upstream fetch.

`IntroduceParameterDocumentRewriter.cs` before the VB.Next fixes continues through `f5db0a2b837` (2026-08-10), "Do not offer introduce parameter for incomplete calls (#84769)", author Ankita Khera. That commit message is an upstream Roslyn pull request. `git log -S` finds `CodeGenerationDestination.ModuleType` in this file only at `24c4d6fb7ba` (2026-11-21), "Fix VB Introduce Parameter generation in Modules (VB-187a)". `argumentParameter.OriginalDefinition` appears only at `05cd8208345` (2026-11-24), "Fix generic method invocation generation in Introduce Parameter (VB-187b)". Both commits are ancestors of HEAD.

`MethodGenerator`'s `destination <> CodeGenerationDestination.ModuleType` check comes from `46c42bd7345` (2024-07-24), "Move vb code", author Cyrus Najmabadi. That is the upstream generator rule. The pre-fix rewriter passed `CodeGenerationDestination.Unspecified`, so the generator emitted `Shared` for a static method.

Conclusion:

- The original `Shared` behavior is demonstrated upstream behavior in this fork's history, then changed by VB.Next commit `24c4d6fb7ba`.
- The original generic-invocation behavior is the same: present through the upstream-shaped history, then changed by VB.Next commit `05cd8208345`.
- Neither fault was introduced by a `vbnext` language change.
- The corrections were introduced by this fork. They are IDE edits in shared Features code. They are not a language-version gate.
- A line-by-line diff against a newly fetched `dotnet/roslyn` tree was not made. The local ancestry above is the evidence used.

## 14. Static root-cause assessment

**Shared.** Earliest decision: `CreateMethodDeclarationAsync` passed `CodeGenerationDestination.Unspecified`. The cloned module method is `IsStatic` because module members are implicitly shared. `MethodGenerator` then emits `Shared` unless the destination is `ModuleType`. The current source passes `ModuleType`. The VB provider never made this decision.

**Generic invocation.** Earliest decisions: the argument map used the constructed parameter as the dictionary key, so a lookup by the definition parameter failed and the trampoline argument list could be empty; `GenerateNewMethodInvocation` built an identifier and did not carry type arguments. The overload path already built a generic name, and the VB simplifier could remove an inferable one. The current source keys the map by `OriginalDefinition` and builds a generic name from explicit syntax or from the constructed method's type arguments.

The containing module is not misclassified. `TypeKind.Module` is the right test, and the current destination uses it. The provider does not reject a valid module selection; the probe shows the actions were offered. Call sites of the direct-update choice are a different set of failures, tracked outside VB-187. Parameter types on the generated declaration come from the expression's converted type and were not the `Shared` or type-argument fault.

The two symptoms do not have one cause. One is the code-generation destination. The other is invocation syntax and parameter identity. A module generic method can show both at once, as `WaveEleven.vb(40)` did, because both paths run for the extracted-method choice.

## 15. Compatibility assessment

The corrections already in the tree are an ordinary IDE correctness fix for existing Visual Basic, not a `vbnext` feature and not a change to compiled meaning.

- Visual Basic 17.13, `default`, and `latest` stay on 17.13 for the compiler. Introduce Parameter does not consult `LanguageVersion` for the module destination or the generic invocation. Gating the fix on `vbnext` would leave the invalid edit in place for existing code, which is the code the defect affects.
- Explicit `vbnext` uses the same refactoring. The probe sample contains `vbnext` forms, and the unit tests that pin the two symptoms use ordinary Visual Basic.
- Existing applications are unchanged until a developer invokes the refactoring. The edit then produces source the compiler already accepted: a module method without `Shared`, and a generic call that still has the type arguments it needs.
- A `Shared` method of a class is a different type kind and still gets `Shared`. An instance method is not static and does not get `Shared`.
- C# is affected only by the generic-invocation half, which is the shared rewriter. The module destination is false for C# static classes. The historical C# tests are the regression surface for that half. They were not re-run here.
- No language-version-sensitive behavior change is proposed. No compatibility exception of the kind the mission records for metadata facts is required.

Removing the already-landed fixes would restore an edit BC30433 rejects. That is not recommended.

## 16. Representative scenario matrix

Unexecuted in this slice. Retained only where the two symptoms apply. The `For Each` name, the unqualified cross-module expression, `TypeOf ... Into`, anonymous types typed as `Object`, and extract method are excluded because the feature plan keeps them as separate defects.

| Scenario | Why it is in the matrix | Expected behavior | Existing coverage | Future check, if any |
| --- | --- | --- | --- | --- |
| Module method, no caller, extracted method | The VB-187a red diff | `Public Function` without `Shared` | `TestInModuleTrampolineHasNoShared` | Already written. Not run here. |
| Module method, one caller, new overload | The other VB-187a red diff | `Public Sub` without `Shared`; caller unchanged when it still binds | `TestInModuleOverloadHasNoShared` | Already written. Not run here. |
| Several callers, direct update | Probe: this choice is never the BC30433 row | No new `Shared` method; callers gain an argument | Class tests such as `TestExpressionCaseWithMultipleMethodCall`. No module direct-update test. | Not required to explain the defect. The generation path that emitted `Shared` is not this choice. |
| Qualified and unqualified module calls | Module binding differs, but the `Shared` keyword is on the generated declaration | Declaration omits `Shared` either way | Not separated in the module tests | Excluded from a fix. Same destination check. |
| Caller in another module, class, or document | Call-site discovery is existing shared behavior | The new argument is added; a generated method in the module is still not `Shared` | C# `TestReferenceInDifferentDocumentWithUsings` for the shared call-site path. No VB module case. | Out of the two root causes. |
| Existing parameters, optional parameters, named arguments | Argument insertion, not the module keyword | Unchanged from the class tests | `TestExpressionCaseWithOptionalParameters*`, `TestNamedParameterNecessary` | Regression only if the rewriter's argument path is edited again. |
| Generic declaration in a class | Shows the planning phrase overstated declaration loss | `(Of T)` remains | `TestGenericMethodTrampolinePreservesTypeParameters` | Already written. |
| Generic overload, inferable type argument | Accepted simplification | `M(...)` compiles | `TestGenericMethodOverloadPreservesTypeParameters` | Already written. |
| Generic trampoline call that cannot infer `T` | The invalid generic edit | `GetNum(Of Integer)(...)` | VB and C# tests named above | Already written. |
| Generic method in a `Module` | The probe showed both symptoms on one edit | No `Shared`, and the call keeps type arguments it cannot infer | No unit test. Probe rows at `WaveEleven.vb(40)` and `WaveNine.vb(44)`. | Optional confirmation only. The source paths are independent. |
| Class `Shared` method and instance method | Guards a wrong destination check | Class `Shared` stays `Shared`; instance stays instance | Existing class tests follow the `Unspecified` path. No dedicated class-`Shared` assertion next to the module tests. | Optional if the destination condition is edited again. |
| Overloads and extension methods in a module | Same destination rule | A generated sibling in the module is not `Shared` | Not separately tested | Excluded. No distinct branch in the rewriter. |
| Accessibility | The generator's default accessibility is existing behavior | Not part of BC30433 or BC32050 | Not a VB-187 assertion | Excluded. |

Expected behavior in the retained rows is established by the language rule and the existing expected test text, except the combined module-plus-generic row, which is established by composing the two source paths and was not given its own test.

## 17. Correction alternatives

The corrections are already in the tree. The alternatives are about whether to change that tree again.

1. **Preserve the current source.** `CreateMethodDeclarationAsync` passes `ModuleType` for a module. The invocation path keys parameters by `OriginalDefinition` and emits generic names. Recommended. It is the smallest body of code that addresses both symptoms, and it is already written, reviewed in the VB-187a and VB-187b reports, and published.
2. **A further production change.** Not supported. A second implementation in the VB provider cannot see method generation; the provider has no hook for it. Changing `MethodGenerator` to suppress `Shared` for every `TypeKind.Module` would also change Extract Method and other generators that pass `Unspecified`. That wider change was considered in the VB-187 phase 1 report and was not the change that landed. It is not needed now.
3. **Revert `24c4d6fb7ba` and `05cd8208345`.** This restores `Public Shared` in a module and the empty or non-generic trampoline call. Rejected.
4. **More executable investigation before any status change.** The original mechanisms are visible without a run. A run would check that the historical tests still pass and that a generic module method gets both fixes together. That is optional confirmation, not a missing cause. It needs a fresh Features build, which this slice must not start.

## 18. Recommended disposition

**B. Preserve the existing behavior, and do not open a corrective slice.**

The behavior to preserve is the current source, after VB-187a and VB-187b, not the broken edits the probe recorded. Both causes are identified. Both edits are present. The tests that pin them exist. This slice does not mark the defect corrected and does not treat the historical green runs as a new verification. Owner acceptance of this assessment is what would close the open planning status.

A, a new implementation slice, would duplicate code that is already committed. C is not required to decide the cause. D would defer a defect the tree already addresses. E is not forced: the compatibility conclusion is that the landed change is an ordinary IDE fix for every Visual Basic language version, and no new language decision is open. The owner still decides whether to accept that recommendation.

## 19. Proposed future verification

No implementation slice is proposed, so this is not an authorized plan. If the owner wants executable confirmation before accepting disposition B, the bounded check is:

- Build the Visual Basic and C# Features test projects from the current tree, with fresh artifacts. Do not use the Features assembly observed at VB-030 closure, and do not use a missing Workspaces assembly.
- Run `TestInModuleTrampolineHasNoShared`, `TestInModuleOverloadHasNoShared`, and `IntroduceParameterGenericMethodTests`.
- Run the two C# generic trampoline tests, because that half of the rewriter is shared.
- One new test, only if the owner wants the probe combination pinned: a generic method in a `Module`, extracted-method choice, asserting no `Shared` and a call that still has its type argument. That test does not exist today.
- Do not re-run the 1,191-edit probe unless a later decision asks whether every historical row is gone. Many of those rows are other defects.
- No compiler rebuild is required for this IDE check. A full repository rebuild is not required.

This slice does not authorize that plan.

## 20. Artifact provenance prerequisites

Static inspection did not need a build. Any later execution does.

- Do not run the Features assembly whose SHA-256 at VB-030 closure was `F8DCC107018BF84E8B7E691271B066B5A031C54817C0627C50E29B042C5FE250`. The previously accepted Features hash remains `368903F56141A0AD2936701614F02973706195309E57F55FA47FB911874547BD`. They differ.
- Do not run a test host that needs the accepted Workspaces assembly (`32A2A990237635CEFB50A4C534FEB3FB12EB95F95E41AFC03933FCC41FB0F78A`). That artifact was absent at closure.
- A later authorized run starts from a new build of the current sources, recorded with configuration, target, size, and SHA-256, under the development-environment rules.
- The accepted compiler baseline is unchanged and was not used: `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`, Release, `net10.0`, 6,553,600 bytes, SHA-256 `3DBDC692E7D1F8FA3E0EBBF6643561E04A8FD0042285938FA6E33E75A952AB70`.

## 21. Mission drift review

Judged against [vbnext-mission.md](vbnext-mission.md). The questions are about the already-landed IDE correction, which this slice does not modify, and about this assessment.

1. The correction improves the existing Visual Basic editing experience: Introduce Parameter stops writing a module method the compiler rejects, and stops dropping type arguments a generic call needs.
2. It preserves Visual Basic culture. A module method is written without `Shared`, which is how Visual Basic already writes module methods.
3. It preserves language semantics. The compiler's module and generic rules are unchanged.
4. It preserves existing applications. Compiled behavior does not change. Only a later use of this refactoring changes.
5. It reuses the shared rewriter and the existing VB generator. It does not add a second refactoring.
6. It adds no framework dependency.
7. It adds no syntax and no language-version gate. A gate would withhold the fix from 17.13 code, which is where the defect was reproduced.
8. It supports desktop and server editing, which is the current priority. Razor, VBHTML, and MAUI are untouched.
9. The landed change is proportional: a destination flag, a parameter-key, and generic invocation syntax, plus the tests that pin them.
10. No mission drift is shown. This slice documents that conclusion and does not widen the language.

## 22. Owner decisions required

1. Accept or reject this assessment.
2. Accept or reject disposition B: no further corrective slice, because the current source already contains the two fixes.
3. Decide whether the planning status may then say the assessed defect is addressed by `24c4d6fb7ba` and `05cd8208345`. This slice does not make that mark.
4. Decide whether any executable confirmation in section 19 is wanted. It is not required for the cause, and it is not authorized here.
5. `docs/vbnext-agent-handoff.md` and `docs/vbnext-release-plan.md` still call VB-187 open or deferred. Updating them was outside this slice.

## 23. Explicit exclusions

No production compiler, Features, or Workspaces edit. No test edit. No new syntax. No change to module semantics, language-version defaults, or the mission. No change to the four protected owner files. VB-030 stays closed, including its accepted limitations and residual risks. `NullablePublicOnly`, the single-line lambda `As` clause, and a `notnull`-style constraint were not investigated. The other upstream refactoring defects were not fixed and were not re-scoped into VB-187. No build, restore, test, or executable reproduction. No new slice was authorized or started. Razor, VBHTML, and MAUI were not started. The wave 34 Visual Studio Edit and Continue rows stay Pending.

## 24. Assessment completion status

Slice 172a is technically complete and awaits owner review. The original scope was recovered. The original evidence was separated from this slice's static reading. The architecture, tests, provenance, cause, compatibility, and a bounded recommendation are recorded. The underlying refactoring defect is not marked corrected by this slice. No corrective implementation was started.

## 25. Formal owner acceptance

Accepted on November 27, 2026, at 7:22 PM (UTC-6), from the owner authorization for this closure. Sections 1 through 24 stay the assessment record. They were not rewritten, and they do not claim a fresh executable run. This section is the accepted status.

### Formal owner acceptance

Slice 172a is formally accepted, including its findings, evidence boundaries, compatibility assessment, and verification limitations. The assessment commit is `907a6383a5aa259cf229d203bf7ded0d6a1f3585`.

### The eight closure decisions

| Id | Decision |
| --- | --- |
| VB187-OD-CLOSURE-1 | Slice 172a is formally accepted. |
| VB187-OD-CLOSURE-2 | Disposition B is accepted. The current implementation is preserved. No additional corrective implementation is necessary or authorized. |
| VB187-OD-CLOSURE-3 | The existing corrections are accepted. VB-187a is `24c4d6fb7ba1544ba9084b20af6383bcf95bdbb6`. VB-187b is `05cd8208345f03a94ef10ae5c2b4091ad6479c63`. No replacement implementation is created. |
| VB187-OD-CLOSURE-4 | Historical executable evidence and the static source assessment are sufficient for administrative closure. No fresh build, restore, test, compiler invocation, or tooling probe is authorized. The historical 1,191-edit probe was not rerun, and this closure does not claim that all 822 BC30433-first edits now compile. |
| VB187-OD-CLOSURE-5 | The missing combined generic-method-in-a-`Module` unit test is an accepted, optional, nonblocking coverage candidate. No test slice is authorized. |
| VB187-OD-CLOSURE-6 | The corrections are ordinary IDE correctness fixes. They are not new language features. No `vbnext` gate is required. Visual Basic 17.13, `default`, `latest`, and explicit `vbnext` stay within their established language boundaries. |
| VB187-OD-CLOSURE-7 | Applicable status records, including the repository agent handoff and the release plan, are reconciled. Historical wording is kept and distinguished from this accepted status. |
| VB187-OD-CLOSURE-8 | VB-187 is formally closed within its approved scope. No mandatory implementation, documentation, or verification requirement remains. The next desktop or server assignment is identified and is not authorized. |

### Accepted independent root causes

VB-187a: the shared rewriter passed an unspecified code-generation destination for a `Module`. `CreateMethodDeclarationAsync` in `24c4d6fb7ba1544ba9084b20af6383bcf95bdbb6` passes `CodeGenerationDestination.ModuleType`, and the Visual Basic generator omits explicit `Shared`.

VB-187b: parameter identity and generated invocation syntax. `05cd8208345f03a94ef10ae5c2b4091ad6479c63` keys arguments by `OriginalDefinition` and preserves required generic invocation arguments. Declarations already kept their type parameters.

The two causes are independent. A generic method in a `Module` can exercise both paths. That combination is not a third defect.

### Accepted historical verification

The tests named in section 12 are present. Their historical failing and passing runs, and the historical compiler checks, stay historical. This closure adds no current pass count.

### Historical probe limitations

The wave 35 probe counts in section 7 stay as recorded: 1,191 breaking edits, 822 with BC30433 first, 411 extracted-method, 411 new-overload, and zero direct-update rows in those 822. The assessment recount found 110 locations where the planning text said 109. The 822 count is not 822 separately corrected defects, and it is not a claim that those edits now compile. Other diagnostics on those rows can belong to other defects. The probe was not rerun.

### Accepted combined regression coverage limitation

No unit test combines a generic method with a `Module` on the two corrected paths. The probe showed that combination, including `WaveEleven.vb(40)`. It was not newly executed. It is an optional future IDE regression candidate and is explicitly nonblocking. No test was added.

### Compatibility acceptance

VB187-OD-CLOSURE-6 is accepted. Module members stay implicitly shared. A class `Shared` method still receives `Shared`. An instance method does not. Generic declarations, valid inference, and the simplifier stay as they are. The generic-invocation change is in the shared rewriter and also applies to C#. Its C# evidence remains the historical tests. No compatibility exception and no language-version gate is added.

### Upstream provenance boundary

The original faults are in upstream-derived history in this fork. The two corrections were introduced here. No fresh comparison with the external `dotnet/roslyn` repository was made, and none is claimed.

### Artifact provenance restrictions

Administrative closure does not establish current binary provenance. The accepted compiler baseline remains `5fe4bcd873a37433ae512da60adbc80a1cd6eb13`, Release, `net10.0`, 6,553,600 bytes, SHA-256 `3DBDC692E7D1F8FA3E0EBBF6643561E04A8FD0042285938FA6E33E75A952AB70`. The previously accepted Features hash remains `368903F56141A0AD2936701614F02973706195309E57F55FA47FB911874547BD`. The Features assembly observed at VB-030 closure remains `F8DCC107018BF84E8B7E691271B066B5A031C54817C0627C50E29B042C5FE250` and was not executed. The accepted Workspaces hash remains `32A2A990237635CEFB50A4C534FEB3FB12EB95F95E41AFC03933FCC41FB0F78A`; that artifact was absent at closure. A later assignment that executes Features or Workspaces needs fresh provenance.

### Mission review

The mission is unchanged. The closure accepts an IDE correction already in the tree. It adds no syntax, no framework dependency, no compiler-semantic change, and no language-version behavior. Desktop and server editing stays the priority.

### Final VB-187 closure status

VB-187 is formally complete and closed within its approved scope. Slice 172a is formally accepted. Disposition B is accepted. No mandatory VB-187 implementation, documentation, or verification requirement remains. This closure does not make VB.Next 1.0 release-ready.

### Explicit exclusion of additional implementation

No replacement correction, no combined test, and no following slice is authorized by this acceptance.
