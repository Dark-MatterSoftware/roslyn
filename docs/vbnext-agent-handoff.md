# Vb.Next agent handoff

A starting point for a new agent conversation. It summarizes and points to the documents that hold the detail; read those before changing anything. Written on November 3, 2026, when development paused. This independent fork is not affiliated with, endorsed by, or supported by Microsoft.

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
