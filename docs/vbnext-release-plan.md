# Vb.Next release plan

A plan for distributing Vb.Next to other developers, from 1.0.0-preview.1 to a stable 1.0. It is a plan for this independent fork, which is not affiliated with, endorsed by, or supported by Microsoft. Written on November 3, 2026, at `vbnext/main` commit `c4ffca9d60c`, when the project moved from feature work to release preparation.

Nothing here is implemented yet. No language feature, syntax, or development wave is added by this plan; the language surface is the one [vbnext-missing-features.md](vbnext-missing-features.md) records. Under [vbnext-mission.md](vbnext-mission.md#completion-status), no feature is yet "Documented and released"; Preview 1 is the first step toward that status.

## 1. Release strategy

| Release | What ships | Purpose |
| --- | --- | --- |
| 1.0.0-preview.1 | A Windows ZIP: the `net472` compiler, `VbNext.Toolset.props`, docs, and the sample | Let developers try `vbnext` with `dotnet build` and an existing .NET SDK |
| 1.0.0-beta.1 | A per-project NuGet compiler toolset, with its own package id | Install and remove through `PackageReference`, without a path to a folder |
| 1.0.0-rc.1 | The same package, after installation, compatibility, and regression validation | Prove the supported matrix and the behavior of ordinary VB code |
| 1.0.0 | The documented, supported compiler distribution | A support policy, signed artifacts, and an automated release |
| Visual Studio track | A separate VSIX line, versioned on its own | Editor, debugger, and Edit and Continue; not a gate for any compiler release |

The compiler releases use the existing Microsoft .NET SDK and never a custom SDK. `vbnext/VbNext.Toolset.props` already swaps the compiler through the SDK's own `Vbc` task properties ([vbnext-toolset.md](vbnext-toolset.md)), so a custom SDK would add an installer, a rebase on every SDK release, and a support burden without a capability the props lack.

## 2. Preview 1 requirements

| # | Requirement | How it is met |
| --- | --- | --- |
| R1 | Use an existing Microsoft .NET SDK | The props sets `VbcToolPath` and `VbcToolExe`; the SDK and its `Vbc` task are unchanged. |
| R2 | No custom .NET SDK | Out of scope for every release in this plan. |
| R3 | Package the compiler and its dependencies | The inventory in section 3. |
| R4 | MSBuild integration through `VbNext.Toolset.props` | Imported per project or from a `Directory.Build.props`. |
| R5 | Opt in with `LangVersion` `vbnext` | `default` and `latest` stay Visual Basic 17.13 ([vbnext-language-version.md](vbnext-language-version.md)). |
| R6 | Ordinary VB.NET behavior is preserved | Gated on `LanguageVersion.VbNext`; verified by the suites, and on a clean machine by gate P5. |
| R7 | C# compilation is not changed | The props touches only the `Vbc` task. It also sets `UseSharedCompilation=false`, which turns off the compiler server for any C# project that imports it; the docs say to import it only into VB projects. |
| R8 | Installation, usage, and removal instructions | A `README.md` in the ZIP, written from [vbnext-toolset.md](vbnext-toolset.md). Removal is deleting the import. |
| R9 | Windows and SDK limitations documented | Section 4 and the README's Known limitations. |
| R10 | A clean-machine installation test | Gate P5; the first task in section 6. |

## 3. Distribution design

### ZIP contents

```text
VbNext-1.0.0-preview.1-win-net472.zip
  VbNext/
    README.md                  installation, usage, removal, limitations
    LICENSE.txt                the repo's License.txt (MIT)
    THIRD-PARTY-NOTICES.txt    the repo's notices
    NOTICE.md                  Vb.Next disclaimer, source commit, trademark note
    VERSION.txt                release version, commit, SDK used to build
    MANIFEST.sha256            SHA-256 of every other file
    VbNext.Toolset.props
    compiler/net472/           vbc.exe and the files in the inventory
    samples/HelloVbNext/       the sample, with its Directory.Build.props
```

Under D2, the props uses a `compiler\net472\` folder beside itself when there is one, so the extracted folder works from any path; `VbNextCompilerDir` still overrides it, and in the repo the props falls back to `..\artifacts\bin\vbc\Release\net472\`. The props sits at the root so that the samples' `Directory.Build.props` finds it at `..\VbNext.Toolset.props`, as in the repo. `vbnext/packaging/New-VbNextPreviewPackage.ps1` builds the ZIP, and `vbnext/packaging/validation/` holds the validation kit; [vbnext-preview1-validation.md](vbnext-preview1-validation.md) records the first candidate.

### Compiler dependency inventory

From `artifacts\bin\vbc\Release\net472`, built from `src\Compilers\VisualBasic\vbc\AnyCpu\vbc.csproj`: 21 files and 13 culture folders.

| Files | Role |
| --- | --- |
| `vbc.exe`, `vbc.exe.config`, `vbc.rsp` | The compiler, its binding redirects, its default response file |
| `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.VisualBasic.dll` | The Vb.Next compiler |
| `Microsoft.CodeAnalysis.ExternalAccess.RazorCompiler.dll` | Shipped with `vbc` upstream; kept unless proven unused |
| `System.Buffers`, `System.Collections.Immutable`, `System.Memory`, `System.Numerics.Vectors`, `System.Reflection.Metadata`, `System.Runtime.CompilerServices.Unsafe`, `System.Text.Encoding.CodePages`, `System.Threading.Tasks.Extensions` | .NET Framework support libraries |
| `Microsoft.DiaSymReader.Native.{x86,amd64,arm64}.dll` | Windows PDB writing |
| `cs` ... `zh-Hant` folders | Localized compiler messages |
| `*.xml` | API documentation; not needed to compile, candidates to drop |

Host requirements: Windows, .NET Framework 4.7.2 or later, and a .NET SDK, which provides MSBuild and the `Vbc` task. `vbc.exe` reports version `5.13.0-dev`, upstream's numbering, and the assemblies keep the `Microsoft.CodeAnalysis` names ([vbnext-roadmap.md](vbnext-roadmap.md#public-api-and-package-identity)). They run in the `vbc.exe` process, out of the SDK's process; whether that identity is acceptable for a preview is decision D4.

### Versions and names

- Release version: SemVer, `1.0.0-preview.1`, `1.0.0-beta.1`, `1.0.0-rc.1`, `1.0.0`. It is independent of the Roslyn assembly version.
- Git tag: `vbnext-v1.0.0-preview.1`, annotated, on `vbnext/main`. The prefix keeps it apart from upstream tags.
- Artifacts: `VbNext-<version>-win-net472.zip` and `VbNext-<version>-SHA256SUMS.txt`.
- `VERSION.txt` records the version, the full commit SHA, and the SDK version from `global.json`.

### GitHub Releases

Preview 1 is published by hand. The GitHub CLI is not installed on the development machine, so the release is created on the web.

1. Tag the release commit on `vbnext/main` and push the tag only.
2. Create a release from the tag, marked as a pre-release, with notes: what `vbnext` adds, the limitations, and the disclaimer.
3. Upload the ZIP and the checksum file.
4. Download both from the release page and verify the checksum (gate P3).

### SHA-256 checksums

`Get-FileHash -Algorithm SHA256` of each artifact, written as `<hash>  <file>` lines. The file is published beside the ZIP, and the release notes repeat the ZIP's hash.

### Licensing and notices

The repo has `License.txt` (MIT) and `THIRD-PARTY-NOTICES.txt` from upstream. Every artifact carries both, plus `NOTICE.md`: the project's independence from Microsoft, the source commit, and a statement that ".NET" and "Visual Basic" are Microsoft trademarks used to describe compatibility. The wording is decision D5.

### Reproducible builds

- Build from a clean checkout of the tagged commit, with the SDK `global.json` pins (`11.0.100-rc.1.26425.128`).
- The one build command is `dotnet build src\Compilers\VisualBasic\vbc\AnyCpu\vbc.csproj -c Release -f net472`, from a path the README names.
- Preview 1: a written build record in the release notes. Beta 1: a build in GitHub Actions, and two builds of the same commit whose compiler assemblies have equal hashes. The compiler is built deterministically upstream; this is not yet checked for this fork.

### NuGet roadmap (Beta 1)

- A Vb.Next package id; the `Microsoft.` prefix is reserved on nuget.org (decision D6).
- `build/<id>.props` with the toolset props' settings, and `tools/net472/` with the inventory.
- A development dependency, so it does not flow to consumers of a library.
- Overrides only the Visual Basic compiler. Upstream's `src/NuGet/Microsoft.Net.Compilers.Toolset` package replaces the C# targets and tasks too, so it is a reference, not a template.
- Later, a CoreCLR `vbc` for builds on Linux and macOS, and Roslyn's build task in the package, so the compiler does not depend on the build task of the user's SDK.

### Visual Studio VSIX track

The four extensions in [vbnext-toolset.md](vbnext-toolset.md) replace Roslyn for C# and Visual Basic in a Visual Studio instance. Their manifests keep upstream's ids and `Publisher="Microsoft"` (`src/VisualStudio/Setup/source.extension.vsixmanifest`, `src/Compilers/Extension/source.extension.vsixmanifest`, `src/ExpressionEvaluator/Package/source.extension.vsixmanifest`). They are validated only in the `RoslynDev` hive of Visual Studio 2026 Insiders, 18.12 for the wave 34 checklist and 18.13 for the desktop designer runs ([vbnext-desktop-validation.md](vbnext-desktop-validation.md#visual-studio-designer-validation-dr-3)), and Edit and Continue is Pending ([vbnext-wave34-validation.md](vbnext-wave34-validation.md)). In that hive a build can use the extension's compiler instead of the Preview 1 compiler ([known limitations](vbnext-desktop-validation.md#known-limitations)). A public VSIX needs its own identity and publisher, signing, a Visual Studio version range limited to tested builds, and a design for living beside the in-box Roslyn. Until then, Visual Studio support is documented as experimental, for contributors.

## 4. Release quality gates

### What is verified, assumed, and untested

| Capability | Status | Evidence |
| --- | --- | --- |
| `dotnet build` and `dotnet run` of a `vbnext` project through the props | Verified on the development machine | HelloVbNext row, [vbnext-wave35-validation.md](vbnext-wave35-validation.md) |
| The seven suites on `net472` at `c4ffca9d60c` | Verified: 31,737 passed, 305 skipped, none failed | Slice 171a, [vbnext-phases.md](vbnext-phases.md) |
| The compiler suites on `net10.0` | Verified at wave 35, not rerun since | [vbnext-wave35-validation.md](vbnext-wave35-validation.md) |
| `default` and `latest` stay 17.13 | Verified by design and tests | [vbnext-language-version.md](vbnext-language-version.md) |
| A clean machine with only a stock SDK | Verified for .NET 10: 12 of 12 checks in Windows Sandbox with SDK 10.0.403 (gate P5) | [vbnext-preview1-validation.md](vbnext-preview1-validation.md) |
| SDKs other than the pinned .NET 11 preview | Verified with SDK 10.0.403 on a clean machine, and with SDK 10.0.400 for the desktop samples; other SDK bands untested | [vbnext-preview1-validation.md](vbnext-preview1-validation.md), [vbnext-desktop-validation.md](vbnext-desktop-validation.md) |
| Targets other than `net11.0` | Verified for the sample on `net10.0`, and for the desktop samples on `net10.0-windows` and `net48`; `net472` and `net47` untested | [vbnext-preview1-validation.md](vbnext-preview1-validation.md), [vbnext-desktop-validation.md](vbnext-desktop-validation.md) |
| C# projects in the same solution use the stock compiler | Verified for the tested solutions only: the mixed solution of phase 1 on the development machine, the P5 check on a clean machine with SDK 10.0.403, and the VS-1 solution built from the command line with Visual Studio 18.11's MSBuild. Not shown for other SDKs or for builds started inside the IDE | [vbnext-preview1-validation.md](vbnext-preview1-validation.md), [vbnext-vs1-validation.md](vbnext-vs1-validation.md) |
| Removing the import restores the stock compiler | Verified from the command line on a clean machine with SDK 10.0.403 (phase 2). Phase 1 and VS-1's automated checks show only that projects without the import use the SDK's compiler; removing the import inside the IDE, VS-1 rows 15 and 16, is Pending | [vbnext-preview1-validation.md](vbnext-preview1-validation.md), [vbnext-vs1-validation.md](vbnext-vs1-validation.md) |
| Ordinary VB code compiles to the same output as with the stock compiler | Verified only for the desktop samples: identical metadata and IL in four pairs | [vbnext-desktop-validation.md](vbnext-desktop-validation.md) |
| Windows Forms and WPF on `net10.0-windows` and `net48` with `dotnet build`, SDK 10.0.400 | Verified from the command line for the DR-1 samples; `net48` WPF needs an explicit `mscorlib` reference | [vbnext-desktop-validation.md](vbnext-desktop-validation.md) |
| Windows Forms and WPF designers in Visual Studio | Verified for the tested workflows only, in the `RoslynDev` experimental instance of Visual Studio 2026 Insiders 18.13 (DR-3B to DR-3D.4): the code editor, the Windows Forms designer with a property edit and save, and the WPF designer after a build, with a property edit and save. Partly backed by build logs and file comparisons, partly by the owner's observation; DR-3D.4 is user-reported only | [vbnext-desktop-validation.md](vbnext-desktop-validation.md#visual-studio-designer-validation-dr-3) |
| Debugging desktop applications in Visual Studio | Verified for the tested workflows only: breakpoints, stepping, Locals, Watch, and Immediate for the Preview 1-built `net10.0-windows` Windows Forms and WPF samples, in the `RoslynDev` instance of Visual Studio 2026 Insiders 18.13, under `/debugexe` with Managed (.NET Core) (DR-4B to DR-4D). `net48` and F5 from a project untested | [vbnext-desktop-validation.md](vbnext-desktop-validation.md#visual-studio-debugging-and-edit-and-continue-dr-4-dr-5) |
| Edit and Continue and Hot Reload for desktop applications | Two tested edits, each applied to a running Windows Forms application by attach: one ordinary VB method-body edit (DR-5B), and one Vb.Next-specific edit, `_stages(^1)` to `_stages(^2)`, which passed (DR-5D). After the DR-5D update the Immediate window failed on `_stages(^1)` with `BC30201`, an open evaluator defect with no established cause. Other edits of Vb.Next forms, rude edits, Hot Reload in general, WPF and XAML Hot Reload, and stock Visual Studio untested; not a general Edit and Continue or Hot Reload claim | [vbnext-desktop-validation.md](vbnext-desktop-validation.md#visual-studio-debugging-and-edit-and-continue-dr-4-dr-5) |
| My Project and the generated application infrastructure for desktop applications | Partial: DR-6 P2-A and P2-B passed from the command line for the Windows Forms sample on `net10.0-windows` and `net48`, ordinary VB and `vbnext`, with hand-written designer files: equivalent generated-style `My` infrastructure, `My.Resources`, and one user-scoped `My.Settings` value saved and reloaded across processes. The Visual Studio generators, My Project pages, application framework edges, and P2-C through P2-F untested | [vbnext-desktop-validation.md](vbnext-desktop-validation.md#my-project-and-the-generated-application-infrastructure-dr-6) |
| Publishing for desktop applications | Untested | [vbnext-desktop-validation.md](vbnext-desktop-validation.md#untested) |
| Linux and macOS | Not supported: `vbc.exe` is .NET Framework | `vbc.csproj` |
| Builds inside Visual Studio use Vb.Next | Verified for stock Visual Studio 18.11 through its MSBuild from the command line (VS-1); in the `RoslynDev` experimental instance a build can use the extension's compiler instead (DR-3B.3, DR-3B.4) | [vbnext-vs1-validation.md](vbnext-vs1-validation.md), [vbnext-desktop-validation.md](vbnext-desktop-validation.md#known-limitations) |

### Desktop validation status

As of November 13, 2026:

- Complete: the command-line matrix of Windows Forms and WPF on `net10.0-windows` and `net48`, with `vbnext` and as ordinary VB (DR-1); the tested Windows Forms and WPF designer workflows (DR-3); and basic debugging of the `net10.0-windows` Windows Forms and WPF samples for the tested workflows (DR-4), all in the `RoslynDev` experimental instance where Visual Studio is involved, as [vbnext-desktop-validation.md](vbnext-desktop-validation.md) records with the evidence for each run.
- Partly complete: Edit and Continue. One ordinary VB method-body edit applied to a running Windows Forms application without a restart (DR-5B). The tested Vb.Next-specific scenario, an edit of `_stages(^1)` to `_stages(^2)` in a running Windows Forms application (DR-5D), passed: the update was valid and applied, the new code ran in the same process without a save, rebuild, or restart, and its shutdown integrity check passed. After that update, the Immediate window failed on `_stages(^1)` with `BC30201`; this evaluator defect is open, and a missing language version in the delta PDB is a hypothesis only, not a confirmed cause. Other edits of Vb.Next forms are untested, and Edit and Continue in general, Hot Reload in general, and WPF and XAML Hot Reload are unvalidated. DR-5B's shutdown integrity check failed: the source was saved and an IDE build using the RoslynDev compiler followed, rebuilding the isolated copy; that the save caused the build is inferred. The evidence is preserved.
- Partly complete: My Project and the generated application infrastructure (DR-6). P2-A: the IL of the generated-style `My` infrastructure of the Windows Forms sample is identical as ordinary VB and with `vbnext`, on `net10.0-windows` and `net48`, and the smoke runs pass; the one difference, VB-065, is outside `My` code. P2-B: `My.Resources` and one user-scoped `My.Settings` value, saved and then reloaded in a separate process, passed in all four cells. The designer files were written by hand; the Visual Studio generators, My Project pages, application framework edges, and P2-C through P2-F are untested.
- Not complete: Visual Studio integration as a whole, and desktop readiness. Vb.Next is a preview and is not production-ready.
- Remaining requirements: Edit and Continue of other Vb.Next forms, the evaluator defect after an Edit and Continue update, and Hot Reload in general, including WPF and XAML Hot Reload; the rest of My Project and the generated application infrastructure, DR-6 P2-C through P2-F, including the Visual Studio generators and My Project pages; `net472` and `net47`; publishing and platform configurations; and the [known limitations](vbnext-desktop-validation.md#known-limitations), among them the compiler routing of the experimental instance, the compiler check that does not see which compiler MSBuild ran, the opt-in array-slice workaround, and VB-065 regression coverage.
- Next: the [proposed next milestone](vbnext-desktop-validation.md#proposed-next-milestone) names My Project and the generated application infrastructure as the next desktop milestone; DR-6 P2-A and P2-B are done, and P2-C through P2-F each need the owner's approval. The evaluator defect seen in DR-5D is tracked separately. Desktop stays first; Razor and VBHTML, and MAUI, are not advanced.
### Preview 1

- P1: The release commit is on `vbnext/main` and pushed; the seven suites pass on `net472` at that commit, with counts recorded.
- P2: The ZIP matches the inventory and layout in section 3, and `VERSION.txt` names the commit.
- P3: The published SHA-256 matches the downloaded ZIP.
- P4: README, removal steps, limitations, `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, and `NOTICE.md` are in the ZIP.
- P5: A clean Windows machine with only a stock .NET SDK passes all of these:
  - a new console project outside the repo, with `LangVersion` `vbnext`, builds and runs;
  - the sample builds and prints its expected output;
  - a VB project without `vbnext` builds and runs with the toolset;
  - a C# project in the same solution builds with the SDK's compiler, shown in a build log;
  - without the import, `vbnext` is rejected by the SDK's compiler.
- P6: The known limitations are listed: Windows only, the SDK versions tested, editor squiggles in a normal Visual Studio, Edit and Continue unvalidated, and the upstream defects in [vbnext-missing-features.md](vbnext-missing-features.md).

Status of the Preview 1 gates on November 4, 2026, for the candidate from `e5afdaecd0e`:

| Gate | Status |
| --- | --- |
| P1 | Met by equivalence, accepted by the owner (D12): the suites passed at `c4ffca9d60c`, and nothing under `src/`, `eng/`, or `global.json` changed since |
| P2 | Met: 87 files in the layout of section 3; `VERSION.txt` names the commit, source state clean |
| P3 | Open until publication: the downloaded ZIP is checked against the published SHA-256 |
| P4 | Met: README with install, use, and removal, the limitations, `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, `NOTICE.md` |
| P5 | Met for .NET 10: 12 of 12 checks in Windows Sandbox with SDK 10.0.403 |
| P6 | Met in the README for the package; the release notes add Edit and Continue and the upstream defects |

### Beta 1

- B1: The NuGet package installs and uninstalls through `PackageReference` in a new project, and uninstalling restores the stock compiler.
- B2: The P5 scenarios pass with the package on two SDK bands, the pinned .NET 11 SDK and the current .NET 10 LTS SDK.
- B3: The package and the ZIP are built by GitHub Actions from a tag, and two builds of one commit have equal compiler hashes.
- B4: A library that references the package does not pass it to its consumers.

### RC1

- C1: A compatibility matrix passes, with the target frameworks, project types (console, class library, Windows Forms, ASP.NET Core), and analyzers the owner chooses.
- C2: A set of existing VB projects, built without `vbnext`, compiles with the same diagnostics, and the same output where builds are deterministic, as with the stock compiler.
- C3: The seven suites on `net472`, the compiler suites on `net10.0`, and the sample pass at the RC commit.
- C4: No open defect the owner marks as release-blocking.

### Stable 1.0

- S1: The artifacts are signed, under decision D7.
- S2: User documentation for each `vbnext` form, a support and versioning policy, and `SECURITY.md` for the fork.
- S3: A release is fully automated from a tag, including checksums and notes.
- S4: An RC with no release-blocking defect for an agreed period.

## 5. Release blockers

Preview 1, from repository evidence, as first recorded and as of November 4, 2026:

1. **No release layout.** Resolved: the props finds a `compiler\net472` folder beside itself, and `vbnext/packaging/New-VbNextPreviewPackage.ps1` assembles the ZIP.
2. **No clean-machine validation.** Resolved for .NET 10: the release candidate passed in Windows Sandbox with only SDK 10.0.403 ([vbnext-preview1-validation.md](vbnext-preview1-validation.md)). The .NET 11 SDK is validated only on the development machine.
3. **Identity, provisionally accepted.** The assemblies keep Microsoft's names, with the developer-build assembly version `42.42.42.42`; the product version is now `1.0.0-preview.1+<commit>`. The roadmap requires own ids and versions before anyone installs a Vb.Next build ([vbnext-roadmap.md](vbnext-roadmap.md#public-api-and-package-identity)). D4 accepts this for Preview 1, with the disclaimer, and it is revisited before Beta 1.
4. **No Vb.Next notice.** Resolved: `NOTICE.md` is in the ZIP, beside `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt`.
5. **No release process.** Resolved for Preview 1 as a manual process: the package script, `VERSION.txt`, `MANIFEST.sha256`, and the checksum file. A release workflow stays a Beta 1 item.

Still open for publishing Preview 1, found in the release-readiness review:

6. **No place to report problems.** Resolved: the owner turned on GitHub issues, and the release notes point to https://github.com/Dark-MatterSoftware/roslyn/issues. Discussions stay off.
7. **The security policy is upstream's.** Resolved on November 4, 2026: the owner turned on GitHub private vulnerability reporting, and the release notes send Vb.Next reports to https://github.com/Dark-MatterSoftware/roslyn/security/advisories/new. `SECURITY.md`, the same on `main` and `vbnext/main` and unchanged since the fork point, is the upstream Roslyn policy: it sends reports to the Microsoft Security Response Center and asks for no public issues. GitHub shows the policy of the default branch, `main`, which stays a clean upstream sync, so the file is left as it is. Reports of problems that also occur with Microsoft's own compiler still belong to Microsoft, as that file says.
8. **Gate P1 by equivalence.** Resolved: the owner accepts the suites at `c4ffca9d60c` for Preview 1, since the release commit `e5afdaecd0e` changes nothing under `src/`, `eng/`, or `global.json`.

Not Preview 1 blockers, recorded as known limitations:

- Edit and Continue in Visual Studio: the missing `IHotReloadComponentLoader` condition that [vbnext-wave34-validation.md](vbnext-wave34-validation.md#why-edit-and-continue-did-not-run) recorded for Visual Studio 18.12 is no longer observed in 18.13, which does not mean that all Hot Reload issues are resolved, one ordinary edit applied in DR-5B, and one edit of `_stages(^1)` to `_stages(^2)` applied in DR-5D, after which the Immediate window failed on `_stages(^1)`, an open defect ([vbnext-desktop-validation.md](vbnext-desktop-validation.md#visual-studio-debugging-and-edit-and-continue-dr-4-dr-5)); wave 34's rows 4 and 5, and other edits of Vb.Next forms, remain unvalidated.
- The VSIX track as a whole.
- VB-187 (slice 172a) and the other upstream defects listed apart in [vbnext-missing-features.md](vbnext-missing-features.md).
- Introduce variable not offered on the operand of `^n` or `a..b` (slice 170b).
- Linux and macOS.

## 6. First implementation task

**Clean-machine validation of the existing compiler, without new packaging.**

On a clean Windows machine, such as Windows Sandbox or a fresh virtual machine, with only a stock .NET SDK installed:

1. Copy `artifacts\bin\vbc\Release\net472` and `vbnext\VbNext.Toolset.props` from the release candidate commit.
2. Run the P5 scenarios with `VbNextCompilerDir` set, once with a .NET 10 LTS SDK and once with the pinned .NET 11 SDK (D3).
3. Record the results in a new `docs/vbnext-preview1-validation.md`.

It changes no code, needs no new infrastructure, and settles blocker 2 and the layout question in blocker 1. It is one session of work.

## 7. Project management

### Release checklist for Preview 1

1. The owner records decisions D1 to D9. Done on November 3, 2026; D3, D4, and D6 are provisional.
2. Clean-machine validation passes (section 6).
3. The release layout is settled, and the props default is changed if D2 chooses it.
4. `README.md`, `NOTICE.md`, and `VERSION.txt` for the ZIP are written.
5. The release commit is chosen; the seven suites pass there (P1).
6. The compiler is built from a clean checkout of that commit.
7. The ZIP is assembled and checked against the inventory (P2).
8. The checksum file is written.
9. P5 runs once more against the assembled ZIP.
10. The tag is created and pushed.
11. The GitHub pre-release is published with the ZIP, the checksums, and the notes.
12. The download is verified (P3).
13. The handoff and this plan record the release.

### Owner decisions

Recorded on November 3, 2026. "Provisional" means the decision holds until the validation it names confirms it.

| # | Decision | Status |
| --- | --- | --- |
| D1 | Version `1.0.0-preview.1`, tag `vbnext-v1.0.0-preview.1`. | Decided. |
| D2 | A portable ZIP whose props finds the compiler by a path relative to itself, `..\compiler\net472\`. | Decided. The one-line change to the release copy of the props is a later checklist item. |
| D3 | .NET 10 LTS is the supported baseline; the pinned .NET 11 configuration is validated too. | Decided for .NET 10: the clean-machine validation passed with SDK 10.0.403, and the sample targets `net10.0`. The .NET 11 RC1 SDK is validated only on the development machine, so Preview 1 lists it as tested, not supported. |
| D4 | Preview 1 keeps the existing Roslyn assembly identities where compatibility needs them. The current VSIX files are not distributed. | Provisional for the identities, to be revisited before Beta 1. Decided for the VSIX files. |
| D5 | The ZIP includes `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, the Vb.Next disclaimer in `NOTICE.md`, and the HelloVbNext sample. | Decided. The trademark wording is written with the release docs. |
| D6 | The Beta 1 package id is `VbNext.Compiler.Toolset`. | Provisional, to be checked for availability on nuget.org. |
| D7 | The Preview ZIP is unsigned, with SHA-256 checksums. | Decided for previews; signing for Stable 1.0 stays open. |
| D8 | A new, validated release commit is made later; it is not `c4ffca9d60c`. | Decided. An upstream sync, if any, lands on `main` first under the [branch policy](vbnext-roadmap.md#branch-policy). |
| D9 | VB-187 (slice 172a) is deferred and is not a Preview 1 blocker. | Decided. |

Recorded on November 4, 2026, after the clean-machine validation:

| # | Decision | Status |
| --- | --- | --- |
| D10 | GitHub issues are the public feedback channel; discussions stay off. | Decided. Issues are on. |
| D11 | Security reports about Vb.Next go to a Vb.Next channel, not to Microsoft. | Decided. The channel is GitHub private vulnerability reporting, turned on. |
| D12 | The suites at `c4ffca9d60c` stand for Preview 1; no rerun at `e5afdaecd0e`. | Decided. |
| D13 | The ZIP is not rebuilt to add a feedback link to its README; the release notes carry it. | Decided. |
| D14 | The default branch stays `main`. | Decided. |
| D15 | The tag `vbnext-v1.0.0-preview.1` is on `e5afdaecd0eadab48d93eeb23c9ce3d0bf7f4dd2`, and the release ZIP is the one with SHA-256 `9d1e7297af844111f91d9c61e345adc38e2683a31510dfbaa54cd10bddf9ca84`. | Decided. |

### Tentative schedule

| Week | Work |
| --- | --- |
| 1 | Owner decisions; clean-machine validation |
| 2 | Release docs, layout, and a dry run of the ZIP |
| 3 | Preview 1 published |
| 4–9 | Beta 1: the NuGet package and a GitHub Actions build |
| 10–13 | RC1: the compatibility matrix and the comparison with the stock compiler |
| After RC1 | Stable 1.0, after an agreed period without release-blocking defects |

The dates move with the owner's decisions; no date is a commitment.

### Keeping tasks small

- One task per session: one checklist item, or one gate.
- A task names the files it reads and the one file it writes.
- Validation runs record counts in a document and do not repeat the suites unless code changed.
- New language work, waves, and phase 172a stay outside this plan unless the owner decides otherwise.
