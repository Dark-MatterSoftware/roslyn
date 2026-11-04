# Vb.Next Preview 1 validation

Checks for 1.0.0-preview.1, under [vbnext-release-plan.md](vbnext-release-plan.md). It is a note for this independent fork, which is not affiliated with, endorsed by, or supported by Microsoft.

Status: phase 1, the isolated-folder portability test, passed on November 3, 2026. The release candidate, built from commit `e5afdaecd0e`, passed its local checks, and phase 2, the clean-machine test in Windows Sandbox, passed on November 4, 2026. Preview 1 is not yet published.

## Phase 1: isolated folder on the development machine

Purpose: show that the Vb.Next compiler works outside this repository with stock .NET SDKs, through `vbnext/VbNext.Toolset.props`, without changes to the compiler, the props, or the sample. This is not a clean machine: Visual Studio, several SDKs, and the .NET Framework developer packs are installed.

### Source and compiler build

| Item | Value |
| --- | --- |
| Branch and commit | `vbnext/main`, `c4ffca9d60c` |
| Build command | `dotnet build src\Compilers\VisualBasic\vbc\AnyCpu\vbc.csproj -c Release -f net472`, with the SDK in `%LOCALAPPDATA%\dotnet-vbnext` that `global.json` pins |
| Result | Succeeded in 33 seconds: 0 errors, 1 warning (BC42305, a duplicate `summary` tag in an XML comment in `Binder_ObjectInitializer.vb`, already in the source) |
| Output | `artifacts\bin\vbc\Release\net472`: 21 files and 13 culture folders, 47 files in all |
| Version | `vbc.exe` product version `5.13.0-dev`; file version of the compiler assemblies `42.42.42.42424`, the placeholder of a local build |

### Test layout

Everything below is in `C:\VbNextPortabilityTest`, outside the repo, and no `global.json` or `Directory.Build.props` above it applies.

```text
C:\VbNextPortabilityTest
  compiler\net472\          copy of artifacts\bin\vbc\Release\net472
  sdk10\  sdk11\            one tree per SDK, each with:
    global.json             pins the SDK, rollForward disable
    vbnext\VbNext.Toolset.props          copied unchanged
    vbnext\samples\...                   copied unchanged, without bin and obj
    tests\...                            the projects below, made by setup-tests.ps1
  setup-tests.ps1  run-tests.ps1         the scripts that make and run the tests
  logs\sdk10\  logs\sdk11\               build logs at detailed verbosity, outputs, summary.txt
```

Each run is a child PowerShell process, so its settings apply to that process only: `NUGET_PACKAGES` points at an empty folder in the test folder, node reuse is off, the build server is shut down at the end, and the .NET 11 run sets `DOTNET_ROOT`, `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR`, and `DOTNET_MULTILEVEL_LOOKUP=0` to the pinned SDK, as `vbnext/Start-VbNextVisualStudio.ps1` does. No global setting changed.

Each build is `dotnet build <project> -v:d -p:VbNextCompilerDir=C:\VbNextPortabilityTest\compiler\net472\`. The sample under .NET 10 is restored with `dotnet restore <project> -p:TargetFramework=net10.0` and built with `--no-restore -p:TargetFramework=net10.0`, because `dotnet build` does not pass that property to its restore step, which then reports NETSDK1045 for the sample's `net11.0`.

### Projects

| Project | Toolset imported | `LangVersion` | Source | Expected |
| --- | --- | --- | --- | --- |
| HelloVbNext | Yes, by its `Directory.Build.props` | `vbnext` | The sample, waves 5 to 35 | Builds and prints its output |
| NewVbNext | Yes | `vbnext` | `TypeOf ... Into`, `numbers(^1)`, a `Select Case` expression | Builds and runs |
| PlainVb | Yes | default | Visual Basic 17.13 code | Builds and runs |
| CsApp | No | default | C# | Builds with the SDK's `csc` |
| NoToolsetPlain | No | default | Visual Basic 17.13 code | Builds with the SDK's `vbc` |
| VbNextDefaultLang | Yes | default | The NewVbNext source | Rejected by the Vb.Next compiler: the forms need `vbnext` |
| NoToolsetVbNext | No | `vbnext` | The NewVbNext source | Rejected by the SDK's `vbc`: BC2014 |
| NoToolsetDefault | No | default | The NewVbNext source | Rejected by the SDK's `vbc`: syntax errors |
| Mixed solution | NewVbNext, PlainVb, and CsApp | — | — | Each project uses its own compiler |

### Results

| Check | .NET 10 LTS, SDK `10.0.403`, `net10.0` | .NET 11, SDK `11.0.100-rc.1.26425.128`, `net11.0` |
| --- | --- | --- |
| Vb.Next syntax compiles and runs | Passed: NewVbNext prints `into:portable`, `last:30`, `select:three` | Passed, the same output |
| HelloVbNext output | Passed: 67 lines, ending in the wave 34 line `closed 4:small draft,40:other 4`, the wave 35 line `1,30,8,20 4 items ending in 20, not a list`, and `disposed sample` | Passed: the same 67 lines |
| An ordinary VB project builds with the toolset | Passed: PlainVb prints `plain:10` | Passed |
| C# builds with the stock compiler | Passed: `sdk\10.0.403\Roslyn\bincore\csc.exe`, alone and in the mixed solution | Passed: the pinned SDK's `Roslyn\bincore\csc.exe` |
| Without the toolset, ordinary VB builds with the stock compiler | Passed: `sdk\10.0.403\Roslyn\bincore\vbc.exe`, `plain:10` | Passed: the pinned SDK's `vbc.exe` |
| The stock compiler rejects `LangVersion` `vbnext` | Passed: BC2014, "the value 'vbnext' is invalid for option 'langversion'" | Passed |
| The stock compiler rejects Vb.Next syntax | Passed: BC30035, BC30071, BC30072, BC30088, BC30201, BC30205 | Passed, the same errors |
| The Vb.Next compiler keeps the forms behind `vbnext` | Passed: BC36716, "Visual Basic 17.13 does not support 'Into' after a type test", with the same syntax errors | Passed |
| No compiler is loaded from the repo | Passed: every VB build with the toolset runs `C:\VbNextPortabilityTest\compiler\net472\vbc.exe`, and no line of any build log names `C:\VB.Net Programs` | Passed |
| Warnings | None in any test build | None |

The HelloVbNext output of the two runs is identical, line for line.

### Limitations of phase 1

- Not a clean machine. Visual Studio, other SDKs (10.0.400, 11.0 rc.2 and rc.3), and the .NET Framework developer packs are installed; phase 2 covers this.
- The pinned .NET 11 SDK is the repo's local install in `%LOCALAPPDATA%\dotnet-vbnext`, not an installer from Program Files.
- The sample has no complete recorded output. The check uses the lines that `Program.vb` determines, the wave 34 and wave 35 lines from [vbnext-wave34-validation.md](vbnext-wave34-validation.md) and [vbnext-wave35-validation.md](vbnext-wave35-validation.md), and the identical output on both SDKs. These 67 lines are now the baseline for phase 2.
- "No compiler from the repo" is shown by the compiler path in the build logs and the absence of the repo path, not by tracing which files the process opened.
- Console apps only, in the Debug configuration.
- Building the sample for `net10.0` needs the separate restore above; the release sample's target framework follows D3.
- The compiler's file version is a local build placeholder, `42.42.42.42424`; release versioning is a later checklist item of the release plan.

## Candidate package

The first internal candidate of the ZIP, built on November 3, 2026. It is not a release: it is built from `c4ffca9d60c` with uncommitted changes, and it is not published.

### Changes

| File | Change |
| --- | --- |
| `vbnext/VbNext.Toolset.props` | `VbNextCompilerDir`, if set, wins; otherwise a `compiler\net472` folder beside the props, as in the ZIP; otherwise the repo's `artifacts\bin\vbc\Release\net472`. |
| `vbnext/samples/HelloVbNext/HelloVbNext.vbproj` | Targets `net10.0` instead of `net11.0`. The source is unchanged. |
| `vbnext/packaging/New-VbNextPreviewPackage.ps1` | New. Builds the compiler, stages the layout, writes `VERSION.txt` and `MANIFEST.sha256`, and makes the ZIP and its checksum file in `artifacts\vbnext-release`. |
| `vbnext/packaging/README.md`, `vbnext/packaging/NOTICE.md` | New. The ZIP's README and notice. |
| `vbnext/packaging/validation/` | New. The validation kit: `Test-VbNextPackage.ps1`, `expected-output.txt`, and the Windows Sandbox steps in `README.md`. |

The folder was first `vbnext/release/`, which the `[Rr]elease/` pattern of `.gitignore` ignores; it is `vbnext/packaging/` instead, and `.gitignore` is unchanged.

The script builds `vbc.csproj` as the toolset note does, with two build properties and no change to `eng/`: `-p:InformationalVersion=1.0.0-preview.1+<commit>` and `-p:DeterministicSourcePaths=true`. Without the second, the compiler assemblies hold the repo path in the caller file paths of their diagnostics, 29 times in `Microsoft.CodeAnalysis.dll` and 75 in `Microsoft.CodeAnalysis.VisualBasic.dll`. The script fails if any staged file names the repo, the user profile, or the user name; its first run failed on the `.vs` cache of the sample, and it now copies only the sample files that git tracks. The XML documentation files are kept.

### Result

The first build, from `vbnext/release/`, was `1618ca3d...d62c`. The candidate below is the rebuild from `vbnext/packaging/`; only `VERSION.txt` differs, in its build time and its list of uncommitted changes.

| Check | Result |
| --- | --- |
| Package | `VbNext-1.0.0-preview.1-win-net472.zip`, 9,831,365 bytes, one top-level folder `VbNext-1.0.0-preview.1` with 87 files: the compiler (47, with 13 culture folders), the samples (34), the props, `README.md`, `NOTICE.md`, `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, and `VERSION.txt`, plus `MANIFEST.sha256` |
| SHA-256 | `8f86f7b8fbb2b578228cc4b7f7445525a6e65ae7ff03c502b0061f28c0ebcbfe`, in `VbNext-1.0.0-preview.1-SHA256SUMS.txt`; it matches the ZIP |
| Extraction | Extracts with `System.IO.Compression`; every manifest entry matches its file, and no file is unlisted |
| Version metadata | `vbc.exe`, `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.VisualBasic.dll`: assembly version `42.42.42.42` and public key token `31bf3856ad364e35`, as validated in phase 1; file version `42.42.42.42424`; product version `1.0.0-preview.1+c4ffca9d60c2dcc4bcc31120ac990574f5b2c248` |
| `vbc /version` | `1.0.0-preview.1 (<developer build>)`; the commit hash attribute is set only in official builds |
| Extracted package, SDK 10.0.403, in `C:\VbNextPortabilityTest\Package Test\` | HelloVbNext builds and prints the 67 lines of the phase 1 baseline; a project outside the package that imports the props by its full path builds and runs. Neither sets `VbNextCompilerDir`; both run the package's `compiler\net472\vbc.exe`; no build log names the repo; no warnings |
| The repo's sample, through the fallback | Builds with the pinned SDK and the repo's `artifacts\bin\vbc\Release\net472\vbc.exe`; run with the stock `dotnet` host, it prints the baseline |

### Validation kit, local run

`vbnext/packaging/validation/Test-VbNextPackage.ps1`, copied to `C:\VbNextPortabilityTest\Kit Test\validation kit\`, run on the candidate extracted to `C:\VbNextPortabilityTest\Kit Test\extracted package\`, with the stock `C:\Program Files\dotnet\dotnet.exe`, `-SdkVersion 10.0.403`, and a work folder `Kit Test\work dir`:

| Check | Result |
| --- | --- |
| Manifest | PASS: 87 entries, none mismatched or unlisted |
| Compiler version | PASS: `1.0.0-preview.1 (<developer build>)` |
| HelloVbNext builds with the package compiler, and its output | PASS: 67 of 67 lines equal the baseline |
| A Vb.Next app outside the package | PASS |
| An ordinary VB app with the toolset | PASS |
| A C# app with the stock compiler | PASS: `sdk\10.0.403\Roslyn\bincore\csc.exe` |
| Vb.Next forms need `vbnext` | PASS: BC36716 |
| The stock compiler rejects `vbnext`, and the forms | PASS: BC2014; syntax errors |
| Removing the import restores the stock compiler | PASS |
| Every build used the reported SDK | PASS: 10.0.403 |
| Result | PASS, exit code 0 |

A second run with `-ExpectedVersion 9.9.9` reported one FAIL and exited with 1, and its manifest check still passed with the sample's `bin` and `obj` folders present.

### Open issues

- The repo's pinned SDK in `%LOCALAPPDATA%\dotnet-vbnext` has only the .NET 11 RC1 runtime, not .NET 10, so it builds the `net10.0` sample but cannot run it (0x80008096). As decided, the sample stays on `net10.0` without `RollForward`, and it runs with the stock .NET 10 SDK and runtime; `docs/vbnext-toolset.md` says so.
- `VERSION.txt` records the uncommitted changes; a release ZIP comes from a clean, committed release commit (D8). Resolved by the release candidate below.

## Release candidate

Commit `e5afdaecd0eadab48d93eeb23c9ce3d0bf7f4dd2` on `vbnext/main`, "Prepare Vb.Next Preview 1 packaging and validation", pushed to `origin/vbnext/main`. It commits the packaging, the validation kit, the props and sample changes, and these docs; since `c4ffca9d60c`, where the seven suites passed, it changes nothing under `src/`, `eng/`, or `global.json`.

| Check | Result |
| --- | --- |
| Package | `VbNext-1.0.0-preview.1-win-net472.zip`, 9,830,783 bytes, built by `vbnext/packaging/New-VbNextPreviewPackage.ps1` from the committed source |
| SHA-256 | `9d1e7297af844111f91d9c61e345adc38e2683a31510dfbaa54cd10bddf9ca84`; it matches `VbNext-1.0.0-preview.1-SHA256SUMS.txt` |
| `VERSION.txt` | Commit `e5afdaecd0e...`, source state clean, built with the pinned SDK `11.0.100-rc.1.26425.128` |
| Version metadata | Product version `1.0.0-preview.1+e5afdaecd0eadab48d93eeb23c9ce3d0bf7f4dd2`; assembly version `42.42.42.42` and token `31bf3856ad364e35`, unchanged |
| Compiler build | 0 errors; 1 warning, BC42305, already in the source |
| Validation kit, local, SDK 10.0.403, freshly extracted to a path with spaces | 12 of 12 PASS, exit code 0 |

## Phase 2: clean machine

Passed on November 4, 2026, run by the project owner on a separate Windows Pro desktop with `vbnext/packaging/validation/README.md`.

| Item | Value |
| --- | --- |
| Environment | Windows Sandbox, Windows NT 10.0.26100.0, with only the stock .NET SDK 10.0.403 installed |
| Package | The release candidate above: version 1.0.0-preview.1, source commit `e5afdaecd0eadab48d93eeb23c9ce3d0bf7f4dd2` |
| Checksum in the sandbox | `9d1e7297af844111f91d9c61e345adc38e2683a31510dfbaa54cd10bddf9ca84`, confirmed independently by the owner; the same as the release candidate's |
| Validation kit | 12 of 12 checks PASS; result PASS |
| HelloVbNext | 67 of 67 output lines match the baseline |
| C# | Compiled with the stock SDK compiler |
| Removing the import | Restores the stock Visual Basic compiler |

What this validates: on a clean Windows machine with only the stock .NET 10 SDK, the ZIP needs nothing from the development machine, compiles and runs `vbnext` code, leaves ordinary VB and C# with their usual compilers, and is removed by deleting its import. Not validated on a clean machine: the .NET 11 SDK, other .NET 10 SDK versions, target frameworks other than `net10.0`, project types other than console apps, and builds inside Visual Studio.
