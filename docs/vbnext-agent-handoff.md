# Vb.Next agent handoff

A starting point for a new agent conversation. It summarizes and points to the documents that hold the detail; read those before changing anything. Written on November 3, 2026, when development paused. This independent fork is not affiliated with, endorsed by, or supported by Microsoft.

## Current status for VB-187 — November 27, 2026

The status sections below are the November 3, 2026, record and are kept as written. They are not the current status of VB-187.

On November 27, 2026, at 7:22 PM (UTC-6), the owner formally accepted slice 172a and closed VB-187. Disposition B is accepted. The assessment commit is `907a6383a5aa259cf229d203bf7ded0d6a1f3585`. The existing corrections are accepted and were not replaced:

- VB-187a, invalid `Shared` in a `Module`: `24c4d6fb7ba1544ba9084b20af6383bcf95bdbb6`
- VB-187b, generic invocation generation: `05cd8208345f03a94ef10ae5c2b4091ad6479c63`

The November 3 statement that slice 172a is planned and not started, and that any fix needs a separate approval, is superseded for VB-187. Slice 171a (VB-186) was later committed as `c4ffca9d60c` and is Done; the "uncommitted" section below is the November 3 snapshot of that work. No further VB-187 implementation, test, or probe is required. The missing combined generic-method-in-a-`Module` test is optional and nonblocking. The historical 1,191-edit probe was not rerun. This note does not update unrelated features. The active repository is `D:\Development\VBNext\Repo` on `vbnext/main`. The working-rules path below is the November 3 path. The detail is `docs/vbnext-vb187-review.md`.

## Current status for the Introduce Parameter local-symbol correction — November 27, 2026

**Formally complete and closed — owner accepted.**

The owner formally accepted the shared local-symbol eligibility correction. `ShouldExpressionDisplayCodeActionAsync` uses `DescendantNodesAndSelf()`, so a selected local or range-variable identifier is examined by the existing `ILocalSymbol` and `IRangeVariableSymbol` rejection. No feature identifier, phase, slice, or wave was assigned. The detail is `docs/vbnext-introduce-parameter-foreach-review.md`.

The accepted results, not rerun for closure, are RED Visual Basic 13 discovered with 11 expected failures and 2 passes, RED C# 7 discovered with 5 expected failures and 2 passes, GREEN 13 and 7 passes, and Introduce Parameter suites of 69 and 82 passes. The 151 suite passes already include the focused tests. VB-187a and VB-187b coverage stayed passing. VB-030 and VB-187 remain closed.

A parameter or a field used as a loop control variable stays outside this closure. The preferred next step named at closure, a bounded assessment of those two cases, was not the assignment that followed. The November 3 "Next approved step" below is historical.

## Current status for cross-assembly extension metadata — November 27, 2026

**Cross-assembly extension-member metadata architecture assessment complete — pending owner implementation decision.**

The owner accepted the C# 15 executable assessment as the working compatibility record and authorized an architecture investigation of VB extension properties and indexers. The detail is `docs/vbnext-extension-metadata-architecture.md`. No compiler change was made. VB-039 remains the identifier. No new identifier, phase, slice, or wave was assigned. Default `receiver(index)` lookup for a metadata reference stays a separate decision. Extension methods still cross an assembly boundary. Production implementation remains unauthorized.

## Current status for the C# 15 and .NET 11 verification — November 27, 2026

**Executable compatibility verification complete — pending owner implementation decisions.**

The owner accepted the static assessment and authorized bounded executable verification. The detail added to the assessment is section 21 of `docs/vbnext-csharp15-dotnet11-assessment.md`. Production implementation, permanent tests, new syntax, feature identifiers, phases, slices, and Wave 37 remain unauthorized. VB-030, VB-187, and the Introduce Parameter local-symbol closure remain closed.

Ordinary union construction and `Value` run on 17.13, default, latest, and vbnext. VB `IsUnion` stays false. Implicit and explicit union conversions and `Select Case` on the union are BC30311. Prohibited closed inheritance is BC37319 on 17.13, default, latest, and vbnext; permitted descendants run. Named C# extension members run on vbnext and are BC30456 on 17.13. Default extension access is BC30367 or BC30547. VB extension properties that work in one compilation are BC30456 from another assembly, including a non-indexed property, and C# does not bind them. Plain Shared interface members run against net11, net472, and net48 references; the net472 output ran on CLR 4.8. `Shared MustOverride` compiled against net472 and is static abstract, which C# rejects with CS8919. Nearest `Exit` and `Continue` behave as today's language. Partial default properties, index-from-end, expression-tree optional arguments, and an async entry point ran. net47 reference assemblies are not installed. Exact 4.7 runtime execution was not established.

The first repair candidate, if later authorized, is cross-assembly VB extension metadata. Default lookup for C# extension indexers is separate. Native unions and named `Exit`/`Continue` stay unapproved design candidates. The IDE candidates listed in the static section below were not investigated.

## Static assessment record for C# 15 and .NET 11 — November 27, 2026

**Static assessment complete — pending owner review and executable verification authorization.**

The paragraph above is the historical status of the static assessment. Executable verification was later authorized and is the current status section.

The owner authorized a documentation-only assessment of released C# 15 and the .NET 11 ecosystem. The detail is `docs/vbnext-csharp15-dotnet11-assessment.md`. No compiler source, test source, build, restore, executable probe, SDK installation, upstream merge, feature identifier, phase, slice, or wave was authorized or started. Wave 37 was not opened. VB-030, VB-187, and the Introduce Parameter local-symbol closure remain closed.

The six released C# 15 capabilities are union types, closed hierarchies, extension indexers, collection expression arguments, non-virtual static interface members on older runtimes, and labeled `break` and `continue`. Memory safety remains preview. This fork already contains the C# compiler implementation of those capabilities through the upstream merge `531d72d81e28e39628164102bd173f5b3afaf0a1` of `e4e2833be7abbc54776788ca35d5c4d2697b0261`. Visual Basic does not recognize union or closed metadata. C# extension indexers are not bound as default properties. Non-virtual static interface members are a VB-110 behavior whose older-runtime claim is source-supported and not freshly executed. Native syntax remains unapproved.

The remaining IDE candidates stay separate: a parameter or a field used as a loop control variable, mutation and expression relocation, unqualified call-site expressions, anonymous types or delegates typed as `Object`, global-qualified tuple conversion, Introduce Variable all-occurrences, Wave 34 Edit and Continue rows 4 and 5, and VB-175. Their deferral is not resolution.

## What Vb.Next is

A community fork of `dotnet/roslyn` that extends Visual Basic. The language version `vbnext` gates every new form; `default` and `latest` stay Visual Basic 17.13.

- Mission, Direction, and rules for proposals: `docs/vbnext-mission.md`. Classify every proposal; keep the boundary between language and framework API; a probe finds candidates but does not justify them.
- Language version policy: `docs/vbnext-language-version.md`.
- Roadmap and history: `docs/vbnext-roadmap.md`, `docs/vbnext-phases.md` (every phase and slice, and the "What starts now" paragraph at the end).
- Feature list, with ids `VB-nnn`, classifications, wave orders, upstream defects, and parked items: `docs/vbnext-missing-features.md`.
- Per-feature syntax notes and reviews: `docs/vbnext-vbNNN-syntax.md`, `docs/vbnext-vbNNN-review.md`.
- Toolset, sample, and Visual Studio deploy: `docs/vbnext-toolset.md`.
- Validation at the end of waves 34 and 35: `docs/vbnext-wave34-validation.md`, `docs/vbnext-wave35-validation.md`.
- Last drift review: `docs/vbnext-drift-review-wave33.md`.

## Release priorities

1. **Desktop and server applications first.** The next major objective is a usable Vb.Next desktop and server preview: the compiler, the language service, the debugger's expression evaluator, and the sample working together in Visual Studio, not more features.
2. VB Razor and VBHTML on the web, and VB MAUI and XAML, are later milestones. The Direction keeps them; the wave 33 drift review left the decision on them open.
3. Do not add new language features, new syntax, or new roadmap waves without the project owner's explicit decision.

## Architecture in brief

Vb.Next changes live in the Visual Basic compiler and the Visual Basic IDE layers of Roslyn, with a few shared files where a Visual Basic form needed a hook.

| Area | Paths |
| --- | --- |
| Syntax and parser | `src/Compilers/VisualBasic/Portable/Syntax/Syntax.xml`, `SyntaxKind.vb`, `src/Compilers/VisualBasic/Portable/Parser/` |
| Binding and semantic model | `src/Compilers/VisualBasic/Portable/Binding/`, `.../Compilation/` |
| Lowering and emit | `src/Compilers/VisualBasic/Portable/Lowering/`, `.../CodeGen/` |
| Flow analysis | `src/Compilers/VisualBasic/Portable/Analysis/FlowAnalysis/` |
| Operations (IOperation) | `src/Compilers/VisualBasic/Portable/Operations/` |
| Diagnostics and resources | `src/Compilers/VisualBasic/Portable/Errors/Errors.vb`, `VBResources.resx` |
| IDE features | `src/Features/VisualBasic/Portable/`, `src/Workspaces/VisualBasic/Portable/`, `src/Workspaces/SharedUtilitiesAndExtensions/**/VisualBasic/` |
| Debugger expression evaluator | `src/ExpressionEvaluator/` |
| Sample | `vbnext/samples/HelloVbNext/` (one module per wave, run from `Program.vb`) |
| Toolset | `vbnext/VbNext.Toolset.props`, `vbnext/Start-VbNextVisualStudio.ps1` |

Design decisions that later work must respect:

- Keyword forms over symbolic ones; XML literals stay in scope. The pattern surface is frozen (`docs/vbnext-vb180-review.md`): a new pattern form needs a proposal that weighs that review.
- Upstream Visual Basic defects are recorded apart from Vb.Next follow-ups (the upstream lists in `docs/vbnext-missing-features.md`). Fixing one is a separate decision.
- Shared code that C# also uses changes only through a narrow hook whose default keeps C# behavior, and the C# tests that cover it are run.

## Status

### Completed and committed

- Waves 1 to 35, as `docs/vbnext-phases.md` records. Wave 35 ended with `docs/vbnext-wave35-validation.md` (commit `4fd6c2042fc`).
- Wave 36 plan (`98395d5fef3`): phases 170 to 172.
- Slice 170a (`7b5bb0cbd81`): `docs/vbnext-vb185-review.md`, the cause of VB-185.
- Slice 170b (`8f72387ee0f`, HEAD): the fix for VB-185. Names in the receiver or the operand of `^n` and `a..b`, as in `Make()(^3)`, now have the symbol and type they have without the index, in the semantic model, rename, find references, go to definition, quick info, and the refactorings. Also a null check in `IsImplicitlyCallable`. Its suites passed before the commit.

### Uncommitted and unverified: slice 171a in progress

The working tree holds an implementation of slice 171a (VB-186) that is **not committed, not reviewed, and not yet tested**, and whose docs rows still say Open:

- `src/Features/Core/Portable/IntroduceParameter/AbstractIntroduceParameterCodeRefactoringProvider.cs`: a virtual `IsValidLanguageSpecificExpression` hook, default `True`.
- `src/Features/VisualBasic/Portable/IntroduceParameter/VisualBasicIntroduceParameterCodeRefactoringProvider.vb`: withholds introduce parameter on an expression that holds or is the test of a `TypeOf ... Into`.
- `src/Features/VisualBasicTest/VbNextRefactorings/VbNextRefactoringTests.vb`: tests for it.

Approval is needed before finishing, testing, or committing it.

### Planned, not started

- Slice 172a (VB-187): an assessment, no code, of the upstream defect where introduce parameter adds `Shared` in a `Module` and drops a generic method's type parameters. Any fix needs a separate approval.

### Not planned; separate decisions

- The other upstream defects (U2 to U5 of the wave 36 assessment), listed under "Not part of wave 36" in `docs/vbnext-missing-features.md`.
- The VB Razor, VBHTML, and MAUI stacks; new syntax.

### Known blockers

- **Edit and Continue in Visual Studio** is unvalidated: rows 4 and 5 of `docs/vbnext-wave34-validation.md` stay Pending. Visual Studio 2026 Insiders (HotReload.Components 18.12.29) lacks a type that upstream Roslyn's Hot Reload loader needs, so it loads no Hot Reload language service from this repo. Rerun those rows after a Visual Studio update.
- Introduce variable is not offered on the operand of `^n` or `a..b`, as on any unary operand (`docs/vbnext-phases.md`, slice 170b). Not a defect of the fix; recorded.

## Next approved step

None is approved. Slice 171a is the next slice of the plan, and work on it exists uncommitted; the project owner decides whether to review, test, and commit it, or set it aside. Do not start 172a, another wave, or new implementation without that decision.

## Working rules

- Use the repo at `C:\VB.Net Programs\VB.Next`, branch `vbnext/main`. Do not push without approval; commit only when asked.
- Each slice: implement, run its tests and the suites, update the slice row in `docs/vbnext-phases.md`, the "What starts now" paragraph, and the row in `docs/vbnext-missing-features.md`, then commit when approved.
- Do not invent spellings; take semantic choices to the project owner.
- Do not edit files while a deploy or a long build runs.
- The Grep tool fails on this path; use `rg` through the shell. `rg` skips `artifacts/` (git-ignored) unless given `--no-ignore`.
- Preserve the files' encoding; some have a UTF-8 BOM and some do not.

### Files not to modify without authorization

- **Never stage or commit** the three modified Razor test files under `src/Razor/` (paths too long for Git on this machine; their changes are unrelated). Do not restore them either.
- Do not commit `ExpressionLambdaRewriter.vb`. Do not mass-restore files that Git marks assume-unchanged.
- Do not change `Syntax.xml`, `SyntaxKind.vb`, the parser, `Errors.vb`, `VBResources.resx`, or the public API lists without an approved syntax or diagnostic change.
- Do not change git config, commit secrets, or hand-write `Co-authored-by` lines.

## Build and test

The SDK is in `%LOCALAPPDATA%\dotnet-vbnext`.

```powershell
$env:DOTNET_INSTALL_DIR = "$env:LOCALAPPDATA\dotnet-vbnext"
$env:ROSLYN_TEST_IOPERATION = "true"
$dotnet = "$env:DOTNET_INSTALL_DIR\dotnet.exe"
cmd /c "`"$dotnet`" test <project> -f net472 -m:1 -p:UseSharedCompilation=false --blame-hang-timeout 5m [--filter `"FullyQualifiedName~Name`"] > artifacts\harden\<log> 2>&1"
```

The seven suites (projects under these folders): `src/Compilers/VisualBasic/Test/Syntax`, `.../Semantic`, `.../Symbol`, `.../Emit`, `src/Features/VisualBasicTest`, `src/EditorFeatures/VisualBasicTest`, `src/Workspaces/VisualBasicTest`. Counts at slice 170b: Syntax 4,196; Semantic 5,611; Symbol 3,398; Emit 4,961; Features 4,178; EditorFeatures 8,874; Workspaces 507. The compiler suites also run on `-f net10.0`. When shared C# code changes, also run the relevant tests in `src/Workspaces/CSharpTest`, `src/Features/CSharpTest`, and `src/EditorFeatures/Test2` (rename, find references, go to definition).

Tests that need `System.Index` use `CommonReferencesNet9` (Features, EditorFeatures2) or `CommonReferencesNet6` (VB EditorFeatures).

Sample: `dotnet run --project vbnext\samples\HelloVbNext\HelloVbNext.vbproj`. It uses `artifacts\bin\vbc\Release\net472`; rebuild `src\Compilers\VisualBasic\vbc\AnyCpu\vbc.csproj` in Release after compiler changes.

Visual Studio: deploy the four extensions and start the experimental instance as `docs/vbnext-toolset.md` describes (the `RoslynDev` hive of Visual Studio 2026 Insiders, `18.0_e34b10deRoslynDev`).

The code fix and refactoring probe of VB-179 is `artifacts/probe164/VbNextCodeActionProbe.vb`; copy it into `src/Features/VisualBasicTest` to run it, and remove it afterwards. Its last report is `artifacts/probe35/report-wave35.txt`.
