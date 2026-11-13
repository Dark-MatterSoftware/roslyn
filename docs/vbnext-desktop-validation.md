# Vb.Next desktop validation: Windows Forms and WPF, from the command line and in Visual Studio

Milestone DR-1 checks that Windows Forms and WPF applications build and run with the Vb.Next 1.0.0-preview.1 compiler, on `net10.0-windows` and `net48`, with and without `vbnext`, and compares ordinary VB with the stock compiler. It is a note for this independent fork, which is not affiliated with, endorsed by, or supported by Microsoft. The mission and the language and framework API boundary are in [vbnext-mission.md](vbnext-mission.md). Milestone DR-3 then checks the same samples in the Windows Forms and WPF designers of Visual Studio Insiders, as [Visual Studio designer validation](#visual-studio-designer-validation-dr-3) records. Milestone DR-4 checks basic debugging of the Preview 1-built samples in the `RoslynDev` instance, and milestone DR-5 checks Edit and Continue; DR-5 is not complete: its run DR-5B applied one ordinary VB method-body edit, and its run DR-5D one edit of a Vb.Next index from the end, as [Visual Studio debugging and Edit and Continue](#visual-studio-debugging-and-edit-and-continue-dr-4-dr-5) records.

Status:

- Command line: completed on November 11, 2026. Runs DR-1A to DR-1I passed or ended as recorded below; all eight configurations in [Summary by configuration](#summary-by-configuration) passed.
- Visual Studio designers: the tested Windows Forms and WPF designer workflows are complete, in runs DR-3B to DR-3D.4 on November 11 and 12, 2026, in the `RoslynDev` experimental instance of Visual Studio 2026 Insiders. Some of these results are backed by captured logs and file comparisons, and some only by what the owner observed; the [designer results](#designer-results) say which.
- Visual Studio debugging: basic debugging of the Preview 1-built Windows Forms and WPF samples passed for the tested workflows in the `RoslynDev` instance (DR-4B to DR-4D, November 12, 2026), and one ordinary VB method-body Edit and Continue change applied to a running Windows Forms application (DR-5B), as [Visual Studio debugging and Edit and Continue](#visual-studio-debugging-and-edit-and-continue-dr-4-dr-5) records. DR-5B's shutdown integrity check failed, as that section explains. In DR-5D, also on November 12, an Edit and Continue change of `_stages(^1)` to `_stages(^2)` applied to a running Windows Forms application without a save, rebuild, or restart, and its shutdown integrity check passed; after the update, the Immediate window failed on `_stages(^1)`, an open evaluator defect that the [DR-5D section](#dr-5d-edit-and-continue-of-a-vbnext-index) records.
- My Project (DR-6): on November 12 and 13, 2026, P2-A and P2-B passed from the command line for the Windows Forms sample on `net10.0-windows` and `net48`, ordinary VB and `vbnext`, with hand-written designer files: the generated-style `My` infrastructure, `My.Resources`, and one user-scoped `My.Settings` value saved and reloaded across processes, as [My Project and the generated application infrastructure (DR-6)](#my-project-and-the-generated-application-infrastructure-dr-6) records. P2-C through P2-F, including the Visual Studio generators, are untested.
- Not complete: Visual Studio integration as a whole, and desktop readiness. Edit and Continue of Vb.Next forms other than the one index edit of DR-5D, the Immediate window after an Edit and Continue update, Hot Reload in general, WPF and XAML Hot Reload, My Project beyond DR-6 P2-A and P2-B, publishing, and broader IDE compatibility are unverified, as [Known limitations](#known-limitations) and [Untested](#untested) list. Vb.Next is a preview; it is not production-ready.

## Scope and method

| Item | Value |
| --- | --- |
| Builds | `dotnet build -v:d -bl --disable-build-servers`, one build per test project and no retries, run from inside the test folder |
| SDK | .NET SDK 10.0.400, pinned by a `global.json` copied into each test folder |
| Test folders | One isolated folder per run, outside the repo; source copied from the previous run, without `bin`, `obj`, or logs |
| Restore | An empty local package source; no package was downloaded or added |
| Vb.Next compiler | The `vbc.exe` of the `VbNext-1.0.0-preview.1` package, built from commit `e5afdaecd0e`, through the package's `VbNext.Toolset.props`; its PDBs record compiler version `1.0.0-preview.1+e5afdaecd0eadab48d93eeb23c9ce3d0bf7f4dd2`. The package was checked against its `MANIFEST.sha256`, 87 of 87 files, before and after each run |
| Stock compiler | The SDK's `Roslyn\bincore\vbc.exe`, compiler version `5.9.0-1.26379.115+14fbf8d5271c`, in copies without the toolset import |
| `net48` references | The installed .NET Framework 4.8 reference assemblies, passed through `/sdkpath`; no targeting pack was installed |
| Smoke harness | Each application, started with `--smoke <log>`, checks itself after its window is shown, writes `PASS` or `FAIL` per check and `RESULT PASS` to the log, and exits with 0 only when every check passed. Each run had a 60-second limit |
| Comparisons | A read-only reader of the assembly metadata lists references, types, members, attributes, member references, user strings, and a hash of each method's IL; generated `.g.vb`, `.baml`, and `.g.resources` files are compared by hash |

Every build log was read for each `Vbc` task: the tool path, `/langversion`, `/sdkpath`, and the references.

## Results

All builds that succeeded reported 0 warnings and 0 errors.

| Run | Framework | Application | Compiler | `LangVersion` | Build | Smoke | Outcome |
| --- | --- | --- | --- | --- | --- | --- | --- |
| DR-1A | `net10.0-windows` | Windows Forms, application framework | Preview 1 | `vbnext` | Failed: 2 errors, BC30389 | Not run | Incomplete: the test code read the `Protected` members `MainForm` and `IsSingleInstance`; not a compiler defect |
| DR-1A.1 | `net10.0-windows` | Windows Forms, application framework | Preview 1 | `vbnext` | Passed | 17 of 17, exit 0 | Passed |
| DR-1B, copy A | `net10.0-windows` | Windows Forms | Preview 1 | default | Failed as expected: 2 errors, BC30201 | Not run | Passed: `^1` and `^2` are rejected without `vbnext` |
| DR-1B, copies B and C | `net10.0-windows` | Windows Forms, ordinary VB | Preview 1 and stock | default | Passed, both | 17 of 17 each, exit 0 | Passed: identical metadata and IL |
| DR-1C | `net48` | Windows Forms, ordinary VB | Preview 1 and stock | default | Passed, both | 16 of 16 each, exit 0 | Passed: identical metadata and IL |
| DR-1D | `net48` | Windows Forms | Preview 1 | `vbnext` | Passed | 16 of 16, exit 0 | Passed: `^1` and `^2` without `System.Index` |
| DR-1E | `net10.0-windows` | WPF | Preview 1 | `vbnext` | Passed | 15 of 15, exit 0 | Passed, including the WPF temporary project |
| DR-1F | `net10.0-windows` | WPF, ordinary VB | Preview 1 and stock | default | Passed, both | 15 of 15 each, exit 0 | Passed: identical generated files, metadata, and IL |
| DR-1G | `net48` | Console | Preview 1 | `vbnext` | Passed | 4 of 4, exit 0 | Passed: interpolated strings fall back as expected |
| DR-1H | `net48` | WPF, ordinary VB | Preview 1 and stock | default | Failed, both: 1 error, MC1000, before any `Vbc` task | Not run | Blocked by the SDK's WPF build tooling, not by a compiler |
| DR-1H.1 | `net48` | WPF, ordinary VB, explicit `mscorlib` reference | Preview 1 and stock | default | Passed, both | 15 of 15 each, exit 0 | Passed: the missing `mscorlib` reference was the blocker |
| DR-1I | `net48` | WPF, explicit `mscorlib` reference | Preview 1 | `vbnext` | Passed | 15 of 15, exit 0 | Passed |

The runs reported .NET runtime 10.0.15 on `net10.0-windows` and 4.0.30319.42000, .NET Framework 4.8, on `net48`.

### Summary by configuration

| Framework | Application | With `vbnext`, Preview 1 | Ordinary VB, Preview 1 and stock |
| --- | --- | --- | --- |
| `net10.0-windows` | Windows Forms | Passed: DR-1A.1 | Passed: DR-1B, copies B and C |
| `net48` | Windows Forms | Passed: DR-1D | Passed: DR-1C |
| `net10.0-windows` | WPF | Passed: DR-1E | Passed: DR-1F |
| `net48` | WPF | Passed: DR-1I, with the explicit `mscorlib` reference | Passed: DR-1H.1, with the explicit `mscorlib` reference |

Each result is backed by the run's build log, its smoke log, and, for the ordinary VB pairs, the metadata and IL comparison. The `mscorlib` reference is the project-level workaround in [Known tooling workarounds](#known-tooling-workarounds). `net472` and `net47` were not covered.

## Validated

### Ordinary VB compatibility

- In DR-1B, DR-1C, DR-1F, and DR-1H.1, the copy built with the Preview 1 compiler and the copy built with the stock compiler, from the same source, have identical metadata and IL: assembly references, types, members, attributes, member references, user strings, and the IL of every method.
- The WPF pairs, DR-1F and DR-1H.1, also generate identical `.g.vb`, `.baml`, and `.g.resources` files.
- In DR-1F, the PDBs differ only in the compiler-version and runtime-version strings they record.
- With the default language version, the Preview 1 compiler rejects `^1` and `^2` with BC30201, "Expression expected." (DR-1B, copy A).

### WPF temporary-project routing

A WPF project whose XAML refers to a type of its own project builds a temporary project (`*_wpftmp.vbproj`) during markup compilation. In DR-1E, DR-1F, DR-1H.1, and DR-1I:

- The temporary project imports `VbNext.Toolset.props` when its parent does, so the props reach it.
- Each build has exactly two `Vbc` tasks, the temporary project's and the main project's. Both use the selected compiler, the package's for a copy with the import and the SDK's for a copy without it, and the same language version: `/langversion:vbnext` when the project sets it, and none otherwise.
- The props' `VbNextCheckCompiler` target runs in both projects when the import is present.
- Markup compilation runs `MarkupCompilePass1`, `GenerateTemporaryTargetAssembly`, `MarkupCompilePass2`, and `CleanupTemporaryTargetAssembly`, in that order.

### Application framework and `My`

Windows Forms (DR-1A.1, DR-1B, DR-1C, DR-1D), with `MyType` `WindowsForms` and `StartupObject` `My.MyApplication`:

- The application starts through `My.MyApplication`, which derives from `WindowsFormsApplicationBase`.
- `My.Application.Info.AssemblyName`, `My.Application.OpenForms`, `My.Forms.Form1`, `My.Application.SaveMySettingsOnExit`, `My.Settings.GreetingText`, and `My.Computer` return the expected values.
- `InitializeComponent` sets the form's text and creates its controls, and a `WithEvents` button's `Handles` handler runs.
- On `net10.0-windows` the `ApplyApplicationDefaults` event is raised and `Application.HighDpiMode` is `SystemAware`; on `net48` the `Startup` event is raised.

WPF (DR-1E, DR-1F, DR-1H.1, DR-1I): `Application.Current` is the project's `Application`, its `Startup` event is raised, `StartupUri` opens `MainWindow`, `InitializeComponent` sets the title and creates the named controls, a XAML resource of a local type resolves, and a `Click=` handler in XAML runs.

What was not covered:

- `My Project\Application.Designer.vb`, `Settings.Designer.vb`, and `Form1.Designer.vb` were written by hand in the style that Visual Studio generates. No generator, settings designer, or My Project page was run, so they validate only that such code compiles and runs.
- `My.Settings` was read, not saved or reloaded, in DR-1; [DR-6](#my-project-and-the-generated-application-infrastructure-dr-6) later saved and reloaded one user-scoped setting.
- `MyType` follows the target. The SDK sets `MyType` to `Empty` only when the target is not .NET Framework, so a WPF project on `net48` gets `My.MyApplication`, `My.MyComputer`, `My.MyProject`, and its helper types, and the same project on `net10.0-windows` has none.

### Vb.Next indexing

- On `vbnext`, `stages(^1)` and `stages(^2)` on a `List(Of String)` lower to `get_Item(get_Count() - n)`, with `sub.ovf`, and no `System.Index` or `System.Range` type or member is referenced (DR-1D, DR-1E, DR-1I).
- The receiver is evaluated once: a field is loaded once and duplicated with `dup`, and a local is read again.
- The emitted IL on `net48` WPF (DR-1I) is identical to the ordinary `stages(stages.Count - 1)` form (DR-1H.1); only the language version that the PDB records differs.
- Indexing from the end works on `net48` and `net10.0-windows`, in Windows Forms and WPF. The .NET Framework 4.8 reference assemblies define no `System.Index` or `System.Range`.
- A value of type `Index`, such as `Dim i = ^1`, needs `System.Index` (VB-158). On a target without it, an index from the end outside an index argument or a range reports BC37358, "An index from the end can only be used as an array index." This was read from the binder in DR-1D, not built.

### Interpolated strings (VB-065)

DR-1E showed a difference in code generation between `vbnext` and 17.13, and DR-1G checked it on `net48`:

- Enabling `vbnext` can change the overload that an interpolated string selects when an interpolated string handler overload exists. On `net10.0-windows`, `log.AppendLine($"...")` binds to `StringBuilder.AppendLine(ByRef StringBuilder.AppendInterpolatedStringHandler)` on `vbnext`, and to `AppendLine(String)` with `String.Format` on 17.13. The output is the same.
- A type is a handler only when it has `System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute`.
- An interpolated string converted to `String` still uses `String.Format`, on every language version; `FormattableString` and `IFormattable` use `FormattableStringFactory.Create`.
- On a framework without the handler contracts, the ordinary overloads are chosen. On `net48` with `vbnext`, `StringBuilder.Append` and `AppendLine` bind to their `String` overloads with `String.Format`, and no handler type is referenced (DR-1G, DR-1I).
- This follows the compiler-contract rule in [vbnext-mission.md](vbnext-mission.md). Documentation of the overload change in the VB-065 entries, and regression tests on `net48` and on modern .NET, are pending.

## Known tooling workarounds

### `net48` WPF with `dotnet build` needs an explicit `mscorlib` reference

- Symptom: `error MC1000: Unknown build error, 'Could not find assembly 'mscorlib, Version=0.0.0.0, ...'` from `MarkupCompilePass1`, before any `Vbc` task (DR-1H).
- Cause: `dotnet build` runs the SDK's WPF targets with the `net10.0` `PresentationBuildTasks`, which load types only from the references passed to them. The Visual Basic targets pass `System.dll` but not `mscorlib.dll`, because `vbc` finds `mscorlib` through `/sdkpath`; the C# targets pass `mscorlib.dll`.
- Workaround, in the project: `<ItemGroup><Reference Include="mscorlib" /></ItemGroup>`. It resolves to the installed .NET Framework 4.8 reference assembly, reaches `MarkupCompilePass1`, `GenerateTemporaryTargetAssembly`, and `MarkupCompilePass2`, and adds `mscorlib.dll` to `/reference` beside `/nostdlib` and `/sdkpath`, with no warning (DR-1H.1).
- The stock VB compiler fails the same way, so this is not a Vb.Next regression. The workaround is deliberately kept in the project and not in `VbNext.Toolset.props`. It may be an upstream .NET SDK issue; none has been filed.

### Adaptations of the samples for `net48`

These follow from the target framework, not from the compiler, and were applied alike to both copies of each pair:

- `Environment.ProcessId` became `Process.GetCurrentProcess().Id`.
- In Windows Forms, the `Startup` event replaced `ApplyApplicationDefaults`, and the `Application.HighDpiMode` check was removed, which leaves 16 checks.

## Visual Studio designer validation (DR-3)

Runs DR-3A to DR-3D.4 check the DR-1 samples in Visual Studio, in the `RoslynDev` experimental instance of Visual Studio 2026 Insiders, where the four extensions of [vbnext-toolset.md](vbnext-toolset.md) replace Roslyn. The owner operated the IDE. Each run was otherwise limited to a read-only preflight, the launch, passive capture, and a comparison after Visual Studio closed.

### Environment and method

| Item | Value |
| --- | --- |
| Visual Studio | Visual Studio 2026 Insiders 18.13.12409.143, instance `e34b10de`, started with `/rootSuffix RoslynDev` by `vbnext\Start-VbNextVisualStudio.ps1 -SdkDir 'C:\Program Files\dotnet'` |
| Projects | Isolated copies outside the repo, each with a baseline inventory of SHA-256 hashes: `C:\VbNext IDE Test\DR3B-WinFormsVbNext` and `C:\VbNext IDE Test\DR3C-WinFormsDesigner`, from the DR-1A Windows Forms sample, and `C:\VbNext IDE Test\DR3D-WpfDesigner`, from the DR-1E WPF sample; all `net10.0-windows` with `LangVersion` `vbnext` and the Preview 1 toolset import |
| Hive | The `18.0_e34b10deRoslynDev` hive was backed up in full before each designer run. After each run, the backups, the deployed extensions, the expression evaluator (SHA-256 `DC964F96...`), and the repo were checked unchanged |
| Not done | No F5, debugging, or Hot Reload; no manual source edits; no compiler, toolset, or extension change |

Evidence types in [Designer results](#designer-results):

- **Log**: captured build logs, a binary log and a diagnostic log.
- **Files**: SHA-256 comparisons of sources, build outputs, and hive files before and after a run, and metadata and IL comparisons.
- **User**: what the owner observed and reported in the IDE, with no captured artifact.

### Designer results

| Run | Check | Result | Evidence |
| --- | --- | --- | --- |
| DR-3B (DR-3B.1, DR-3B.2) | The Windows Forms sample in the code editor, with View Code only and no designer | Passed, as reported by the owner: the project loaded, `^1` and `^2` had no squiggles, and the Error List showed 0 errors, 0 warnings, and 0 messages. Quick Info was not verified. An unexpected build during the session used the RoslynDev compiler, investigated in DR-3B.3 | User for the editor; Files for the unchanged sources and the hive |
| DR-3B.3, DR-3B.4 | Which compiler that build used, and why | Confirmed: the RoslynDev compiler is selected through per-hive MSBuild `ImportBefore` files, as [Known limitations](#known-limitations) describes. The metadata and IL equal DR-1A.1's | Log and Files: one isolated Insiders MSBuild build with `RoslynHive` set; 0 metadata and IL differences against DR-1A.1 |
| DR-3C | The Windows Forms designer: `Form1` opens and shows its controls; `RunButton`'s caption changed from Run to Run2 in the Properties window and saved, then back to Run and saved | Passed, as reported by the owner. After Visual Studio closed, from file comparisons: `Form1.vb` byte-identical; `Form1.Designer.vb` changed only cosmetically, its designer comments (`'RunButton` became `' RunButton`) and its line endings, partly converted from LF to CRLF; the event handlers, `WithEvents` fields, and `^1` and `^2` unchanged; `Form1.resx` and `DR1AWinFormsVbNext.slnx` were new | User for the designer and the round trip; Files for the final state. Only the final save was captured, not the intermediate Run2 save |
| DR-3D, stage 2 | The WPF designer on `MainWindow.xaml` | Blocked: "Build the Project to update Design view", and XDG0008 on `local:StageNames`. No property was changed and nothing was saved | User |
| DR-3D.1 | Why the designer could not resolve `StageNames` | The isolated copy had never been built. The designer needs the project's own built assembly for a type that XAML names through a `clr-namespace` without `assembly=`. `StageNames` is public, not nested, and has a parameterless constructor | Files: read-only investigation |
| DR-3D.2 | One controlled build of the isolated WPF copy with Insiders MSBuild, without `RoslynHive` | Passed: both `Vbc` tasks, the `*_wpftmp` project's and the main project's, ran the Preview 1 `vbc.exe` (SHA-256 `FAD3987F...`) with `/langversion:vbnext`; 0 warnings and 0 errors; 0 metadata and IL differences against DR-1E | Log: binary log and diagnostic log, kept in `%TEMP%\vbnext-dr3d2b-evidence` on the test machine; Files |
| DR-3D.3 | The WPF designer after that build | Passed, as reported by the owner: `MainWindow` rendered, `StageNames` resolved, and the Error List was empty, so XDG0008 was not shown; a Run2 property edit was made and not saved. From file comparisons afterwards: all sources were byte-identical and `bin` was unchanged, so no build output was written; the designer's cache in the hive held a copy of the built `DR1EWpfVbNext.dll` (SHA-256 `B18ED76C...`) | User for the render, the Error List, and the edit; Files for the sources, outputs, designer cache, and hive. No save occurred, so none was captured |
| DR-3D.4 | The WPF designer: property edit and XAML save round trip | Passed, as reported by the owner | User only: no save capture, log, or hash comparison exists for this run |

### Side effects of opening a project

- Observed in DR-3D.3, in the tested environment (Insiders 18.13, the `RoslynDev` instance): loading the WPF project ran a design-time build in Visual Studio's MSBuild node. It rewrote the restore files in `obj`, regenerated `MainWindow.g.vb` and `GeneratedInternalTypeHelper.g.vb`, and rewrote `DR1EWpfVbNext_MarkupCompile.cache` and `.lref`; after the load, the `obj\...\MainWindow.baml` that the DR-3D.2 build had written was no longer present. This is a recorded observation of design-time project-load behavior, not a confirmed Vb.Next defect; its cause was not investigated, and whether a later build restores the file was not checked. The assembly in `bin` and `obj`, and `DR1EWpfVbNext.g.resources`, which embeds the built BAML, were unchanged.
- In DR-3D.3, the hive's `Designer\Cache` held a copy of the project's built output and shadow copies of the XAML files (Files).
- After the DR-3C session, `Form1.resx` and `DR1AWinFormsVbNext.slnx` were new beside the project (Files).
- Each session changed the hive's `privateregistry.bin`, settings, and window layouts. The extensions and the expression evaluator were unchanged after every run (Files).

## Visual Studio debugging and Edit and Continue (DR-4, DR-5)

Runs DR-4B to DR-4D debug the DR-1 samples, built by the Preview 1 compiler, in the `RoslynDev` instance of Visual Studio 2026 Insiders 18.13.12409.143, and DR-5B and DR-5D each apply one Edit and Continue change. The owner operated the IDE; each stage was separately authorized, and the rest was read-only preflight, passive capture, and comparison afterwards. The evidence types are those of [DR-3](#environment-and-method), plus **Capture**: a read-only scan of the memory-mapped files of `devenv` and the debuggee, which also lists IL-only assemblies that the process module list omits. DR-5D adds **EnC log**: the Roslyn Edit and Continue log and the delta files it writes when `Microsoft_CodeAnalysis_EditAndContinue_LogDir` is set.

These runs validate basic debugging for the tested Windows Forms and WPF workflows, one ordinary VB method-body Edit and Continue edit (DR-5B), and one Edit and Continue edit of a Vb.Next index from the end (DR-5D). Other edits of Vb.Next forms are untested, the Immediate window failed on a Vb.Next index after the DR-5D update, and Hot Reload in general, WPF and XAML Hot Reload, and broader desktop readiness are unvalidated.

### DR-4 and DR-5 environment and method

| Item | Value |
| --- | --- |
| Projects | Isolated source-only copies with SHA-256 baseline inventories: `C:\VbNext IDE Test\DR4B-WinFormsDebug`, `DR5-WinFormsEnc`, and `DR5D-WinFormsEncIndex`, from DR-1A; `DR4D-WpfDebug`, from DR-1E; all `net10.0-windows`, `LangVersion` `vbnext`, the Preview 1 toolset import |
| Builds | DR-4B, DR-4D, and DR-5B Stage 1: one command-line Debug build per copy (DR4B, DR4D, DR5) with Insiders MSBuild, `-restore`, the empty package source, `-nr:false`, without `RoslynHive`; in each of those builds, exactly one `Vbc` task (two for WPF) ran the Preview 1 `vbc.exe` (SHA-256 `FAD3987F...`) with `/langversion:vbnext /optimize- /debug:portable`; 0 warnings, 0 errors; the PDB records `1.0.0-preview.1+e5afdaecd...` and `vbnext`. The DR-5D Stage 1 command-line build of the DR5D copy, checked separately, also ran exactly one `Vbc` task with the Preview 1 `vbc.exe`, as the [DR-5D results](#dr-5d-edit-and-continue-of-a-vbnext-index) record |
| Debugging (DR-4) | `devenv /rootSuffix RoslynDev /debugexe <app>.exe --smoke <log>`, with the debugger type set once to Managed (.NET Core), saved in the `.slnx` that Visual Studio writes beside the executable |
| Edit and Continue (DR-5B) | The executable started outside Visual Studio, with no arguments, with `DOTNET_MODIFIABLE_ASSEMBLIES=debug` in that process's environment only; the `.vbproj` opened in Visual Studio; Attach to Process with Managed (.NET Core); no Build, Rebuild, or F5 |
| Edit and Continue (DR-5D) | As DR-5B, plus: Visual Studio started as `devenv /rootSuffix RoslynDev` with the `.vbproj`, from a PowerShell at Medium integrity, not elevated, with `Microsoft_CodeAnalysis_EditAndContinue_LogDir` set in that shell only, so the Roslyn Edit and Continue log and delta files were written with the evidence; a process and file monitor with no time limit, from before the launch until after shutdown; the edit left unsaved and discarded at shutdown (Don't Save) |
| Expression evaluator | The deployed RoslynDev VB evaluator, `Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator.ExpressionCompiler.dll` 42.42.42.42424, SHA-256 `DC964F96...`, MVID `0513f815-b1ea-4a5d-9601-7bb16667f626`; the in-box evaluator of Visual Studio is 5.1300.26.52805, SHA-256 `AF0E71BA...` |
| Not changed | Compiler, evaluator, extensions, toolset files, and the repo. `VBNEXT_EE_ARRAYSLICE_LOOP` unset |

### DR-4: debugging results

| Run | Check | Result | Evidence |
| --- | --- | --- | --- |
| DR-4B | Windows Forms, Preview 1 Debug build | Passed: 0 metadata and IL differences against DR-1A.1 | Log, Files |
| DR-4B, DR-4C | Windows Forms debugging: breakpoints in `RunButton_Click`, `Form1_Shown`, and the `check` lambda, stepping, Locals, Watch, Immediate | Passed, as reported by the owner. Breakpoints did not bind until the debugger type was changed from the native-only default to Managed (.NET Core). The smoke run in the session passed | User; Files for the smoke log |
| DR-4C | Vb.Next index in the Immediate window | `? _stages(^1)` returned `"vbnext"`. `^2` was not evaluated in Windows Forms | User |
| DR-4C | Which VB evaluator was loaded | Only the deployed RoslynDev VB evaluator (`DC964F96...`, MVID `0513f815...`) was mapped in `devenv`; no in-box VB evaluator | Capture |
| DR-4D | WPF, Preview 1 Debug build | Passed: both `Vbc` tasks ran Preview 1; 0 metadata and IL differences against DR-1E | Log, Files |
| DR-4D | WPF debugging: breakpoints, stepping, Locals, Watch, Immediate | Passed, as reported by the owner; the smoke run passed 15 of 15 | User; Files for the smoke log |
| DR-4D | Vb.Next index | `stages(^1)` returned `"vbnext"`, `stages(^2)` returned `"framework"`, and `stages(stages.Count - 1)` matched `stages(^1)` | User |
| DR-4D | Which evaluators were loaded | The deployed RoslynDev VB evaluator only; the deployed C# evaluator was also mapped; no in-box VB evaluator | Capture |

Observed in DR-4D, not investigated: starting a WPF application under the debugger loaded the XAML diagnostics components (`VsDebugEng.Xaml`, `DesignTools.*`) in Visual Studio, injected `WpfTap` into the application, and loaded `Microsoft.VisualStudio.HotReload.Components.dll` 18.13.31.23281, all without a request.

### DR-5B: Edit and Continue results

| Stage | Check | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Preview 1 Debug build of the DR5 copy | Passed: `vbc.exe` `FAD3987F...`, `/optimize-`, `/debug:portable`; 0 metadata and IL differences against DR-4B. The dll and PDB differ from DR-4B only through the embedded PDB path. The standalone smoke run was **not run** | Log, Files |
| 2 | External launch | One process, PID 27332, no arguments, main window "DR-1A Vb.Next WinForms"; no build process during the launch | Files: launch record and process monitor |
| 2 | Project load | The IDE restored the project against the user NuGet configuration, rewriting `project.assets.json`, the `dgspec`, and `project.nuget.cache` in `obj`; `bin` was unchanged until the build below | Files |
| 2 | Attach | Managed (.NET Core) to PID 27332 | User |
| 2 | One edit | With the app paused in `RunButton_Click`, line 13 `ResultLabel.Text = LastResult` was changed to `ResultLabel.Text = LastResult & " [enc]"`. At 17:55:06 CST the Hot Reload output showed "Applying code updates..." then "Code changes were successfully applied", with no prompt, rude edit, or restart request. After continuing, the label showed `vbnext [enc]`, and the Immediate window gave `? _stages(^1)` = `"vbnext"` and `? LastResult` = `"vbnext"`. The process was the same PID throughout, with no restart | User, from the owner's screenshots; Files for the unchanged process |
| 2 | Shutdown integrity | **Failed.** `Form1.vb` was saved at 18:02:46 CST, and an IDE build using the RoslynDev compiler followed at 18:02:50, replacing the DR5 `bin` and `obj` dll and PDB (dll `EA28CEA4...`, PDB `9D897EEC...`); the new PDB records compiler version `5.13.0-dev`. The executable was unchanged. That the save caused the build is an inference, not an established fact | Files |
| 3 | Preservation and restore | The modified source, the RoslynDev-built dll and PDB, the `.vs` session files, the restore files, the monitor logs, and the Visual Studio service logs were preserved on the test machine in `%TEMP%\vbnext-dr5b2-evidence-preserved-20261112-181011`, with a SHA-256 manifest; as a temporary folder, it may be removed by temporary-file cleanup. Line 13 of `Form1.vb` was restored byte for byte (SHA-256 `839C19E9...`); all 10 sources match the baseline inventory. The RoslynDev-built dll and PDB were deliberately left in place, so DR5 has its original sources but no Preview 1 build output | Files |

#### DR-5B verified

- One ordinary VB method-body change applied to a running, externally started, Preview 1-built `net10.0-windows` Windows Forms application, attached with Managed (.NET Core), and the next click ran the new code without a restart (User).
- The edited method also contains `_stages(^1)`, and the file was handled as part of project `DR1AWinFormsVbNext` in the IDE workspace (Visual Studio log). So in the `RoslynDev` instance, an edit is not blocked just because the file contains a Vb.Next form. The Vb.Next form itself was not edited.
- The missing `IHotReloadComponentLoader` condition that [vbnext-wave34-validation.md](vbnext-wave34-validation.md#why-edit-and-continue-did-not-run) recorded for Visual Studio 18.12.29 is no longer observed in Visual Studio 18.13, with `HotReload.Components` 18.13.31.23281: the composition errors of the hive no longer name it, the brokered Edit and Continue and Hot Reload services were requested in the session log, and the Edit and Continue client log records `GetCapabilitiesAsync`, `GetAvailabilityAsync`, and `PrepareModuleForUpdateAsync` at 17:54:18 CST. This does not mean that all Hot Reload issues are resolved.
- After shutdown, outside DR5: the repo, the toolset files, DR-4B, DR-1A, DR-4D, the extensions, the 10 evaluator hashes, the hive backups, and the environment variables were unchanged.

#### DR-5B inferred

- The save at 18:02:46 CST caused the IDE build at 18:02:50. This is inferred from the timing; nothing in the evidence records what started the build.
- The IDE build used the RoslynDev compiler through the per-hive `ImportBefore` files, as in [Known limitations](#known-limitations).
- The delta was compiled by the RoslynDev Roslyn inside Visual Studio, not by the Preview 1 compiler. The two have no compiler or Edit and Continue source differences between `e5afdaecd0e` and `c46e7c2`, but the exact commit of the RoslynDev build is not recorded.

#### DR-5B untested

- Any edit of a Vb.Next form itself, for example `^1` to `^2`, ranges, `Select Case` expressions, `Await Each`, or `Async Using`. At the time of DR-5B, the VB Edit and Continue unit tests covered edit analysis next to those forms, and no compiler test emitted a delta for them. DR-5D later validated one such live edit, `^1` to `^2`, as [DR-5D](#dr-5d-edit-and-continue-of-a-vbnext-index) records; the other forms remain untested.
- Rude edits, edits that add or remove members, Hot Reload without the debugger, and Edit and Continue under F5 or `/debugexe`.
- Hot Reload in general, WPF Edit and Continue and XAML Hot Reload, `net48`, and stock Visual Studio without the `RoslynDev` extensions.
- Repeatability: DR-5B is one edit in one session.

#### DR-5B deviations and evidence limits

- `/debugexe` started the application twice in both DR-4C (PIDs 2804, then 12280) and DR-4D (20212, then 3856): a first run that went straight through to `RESULT PASS`, then the run that stopped at the breakpoints. The owner accepted both as recorded deviations; the cause was not investigated.
- DR-5B's shutdown integrity check failed, as the results table records; DR5 keeps RoslynDev-built output.
- Not captured in DR-5B: the label text before the edit; which evaluator `devenv` loaded, because the capture could not open `devenv`, which appeared to run elevated, unlike in DR-4D; the module's symbol path; and the delta contents, because the service logs record only call names.
- The process monitor stopped on its 90-minute limit at 17:31 CST, before Visual Studio started at 17:49:28, so no process record covers the session, including any design-time compiler runs.
- The stock hive `18.0_e34b10de` also changed during the session: settings and keybinding files from 17:45:43 to 17:46:13, `CurrentSettings.vssettings` at 17:49:37, and `privateregistry.bin` at 18:03:30, CST. A Visual Studio Installer update check ran at 17:45:44, and Windows Error Reporting logged Visual Studio telemetry uploads at 17:49:54 and 18:03:30. No process record shows a stock Visual Studio start, and no extension, compiler, evaluator, or debugger setting change was found; the activity is only partly explained.
- The preserved evidence is in `%TEMP%\vbnext-dr5b2-evidence-preserved-20261112-181011` on the test machine, with a SHA-256 manifest, and may be removed by temporary-file cleanup.

### DR-5D: Edit and Continue of a Vb.Next index

DR-5D changes line 12 of `Form1.vb`, in `RunButton_Click`, from `LastResult = _stages(^1)` to `LastResult = _stages(^2)`, on November 12, 2026, in a source-only copy of the DR-1A sample, `C:\VbNext IDE Test\DR5D-WinFormsEncIndex`. `_stages` is a `List(Of String)` of `"designer"`, `"framework"`, and `"vbnext"`, so the label is expected to change from `vbnext` to `framework`. The method is that of DR-5B, with the differences in the [DR-4 and DR-5 environment and method](#dr-4-and-dr-5-environment-and-method) table. The evidence is on the test machine in `C:\VbNext IDE Test\evidence\DR5D-stage2`, with a SHA-256 manifest (`E9D88FEB...`, 27 files), and in `C:\VbNext IDE Test\evidence\DR5D-stage1-outputs` for the Stage 1 build output. Results marked User come from the owner's observation and screenshots; the screenshots are not preserved with the evidence.

| Stage | Check | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Preview 1 Debug build | Passed: one command-line build, in which exactly one `Vbc` task ran the Preview 1 `vbc.exe` with `/langversion:vbnext`; 0 warnings, 0 errors; the PDB records compiler version `1.0.0-preview.1+e5afdaecd...` and language version `vbnext`. Output: dll `209036FC...`, PDB `E6E8835A...`, executable `18D44060...` | Log, Files |
| 1 | Smoke run | Passed: 17 PASS, 0 FAIL, `RESULT PASS` | Files |
| 2 | External launch | One process, PID 15128, at 21:20:08 CST, no arguments, main window "DR-1A Vb.Next WinForms"; `DOTNET_MODIFIABLE_ASSEMBLIES=debug` in that process's environment only, unset for the user, the machine, and the launching shell | Files: launch record and process monitor |
| 2 | Visual Studio | One `devenv`, PID 3704, at 21:25:19 CST, with `/rootSuffix RoslynDev` and the DR5D `.vbproj`, at Medium integrity and not elevated | Files: process monitor |
| 2 | Attach | Managed (.NET Core) to PID 15128; the Modules window showed the module's symbols loaded | User |
| 2 | Loaded components | At both captures, 21:32 and 21:38 CST, `devenv` mapped the RoslynDev 42.42 Roslyn language services and expression evaluators and `HotReload.Components`, and the application mapped only the DR5D `bin` dll and executable | Capture |
| 2 | Before the edit | Paused at line 13: `? LastResult` = `"vbnext"`, `? _stages(^1)` = `"vbnext"`, `? _stages(^2)` = `"framework"` | User |
| 2 | One edit, applied | Line 12 changed from `^1` to `^2` in the editor, and not saved. The Edit and Continue log records solution update 1.1 with "Project summary ... ValidChanges", "Solution update 1.1 status: Ready", and "Module update: capabilities=[Baseline], types=[02000008], methods=[0600001D]"; the delta files are timestamped 21:35:23 CST. The Hot Reload output showed "Code changes were successfully applied" at 21:35:23 CST, with no rude edit or restart request | EnC log; User for the Hot Reload output |
| 2 | The delta | The logged old and new sources differ only in line 12. The delta updates one method, `RunButton_Click` (`0x0600001D`), and adds member references for `get_Count`, `get_Item`, and `set_Text`. Its IL equals the baseline IL of the method except that `ldc.i4.1` becomes `ldc.i4.2` before `sub.ovf`, that is, `_stages(_stages.Count - 2)`, and the member reference tokens are the new ones. The delta PDB has the one method, with sequence points on lines 10, 12, 13, and 14 | EnC log |
| 2 | After the update | After continuing, the first click's label still showed `vbnext`, as expected, because line 12 had already run. On the next click, paused at line 13, `? LastResult` = `"framework"`, and after continuing the label showed `framework`. The process was PID 15128 throughout, with no second application start, no restart, and no rebuild | User; Files for the unchanged process |
| 2 | Immediate window after the update | **Failed:** `? _stages(^1)` gave `error BC30201: Expression expected.`, where it had returned `"vbnext"` before the update; `? LastResult` still evaluated. This is an open defect, below | User |
| 2 | Shutdown integrity | **Passed.** Detach All; the application exited at 21:42:43 and `devenv` at 21:42:50 CST, closed with Don't Save. `Form1.vb` was never written: no file event, SHA-256 `839C19E9...`, its original timestamp. No file event in `bin`; the dll, PDB, and executable are unchanged. No compiler process ran | Files: process and file monitor, hashes |

#### DR-5D verified

- In the `RoslynDev` instance of Visual Studio 2026 Insiders 18.13.12409.143, with the deployed 42.42 Roslyn, an Edit and Continue change of a Vb.Next index from the end, `_stages(^1)` to `_stages(^2)`, was accepted as a valid change (EnC log), emitted as a one-method update whose IL computes `Count - 2` (EnC log), and applied to a running, externally started, Preview 1-built `net10.0-windows` Windows Forms application, which then ran the new code in the same process, PID 15128 (User; Files).
- The edit was never saved, nothing was rebuilt, and the Preview 1 build output and the sources were unchanged after shutdown (Files).
- `Microsoft_CodeAnalysis_EditAndContinue_LogDir`, set only in the shell that started Visual Studio, produced the Roslyn log and the delta files (EnC log).

#### DR-5D evaluator failure and hypothesis

After the update, `? _stages(^1)` failed with `BC30201: Expression expected.` while paused in the updated method; before the update it succeeded, and `? LastResult` succeeded after it. This is an open evaluator defect: its cause is not established, and it is not fixed.

Hypothesis, not a confirmed cause: the VB expression evaluator reads the language version from the compilation options recorded in the PDB for the method's version (`PdbCompilationOptions.TryGetLanguageVersion(symReader, methodVersion)`, called from `EvaluationContext.vb`). For an updated method, that is the delta PDB, which records no compilation options, so the evaluator would fall back to its default language version, in which `^` does not parse.

- For: the delta PDB's only custom debug information is a default namespace, with no compilation options and no language version, while the baseline PDB records `language-version=vbnext`; the failure appeared only after the update; `BC30201` is the error for `^` where an expression is expected; `? LastResult` needs no Vb.Next syntax.
- Against, or not known: the failure was not reproduced in a test; the source revision of the deployed 42.42 evaluator is not recorded; the method version the debugger passed, and what the symbol reader returned for it, were not captured; only one Vb.Next expression was tried after the update.

#### DR-5D untested

- Other Vb.Next forms, rude edits, more than one edit, edits that add or remove members, and Hot Reload without the debugger.
- Whether the evaluator failure affects other Vb.Next forms, other frames, or methods that were not updated.
- WPF, `net48`, F5 from a project, and stock Visual Studio.
- Repeatability: DR-5D is one edit in one session.

#### DR-5D deviations and evidence limits

- Screenshot-only: the attach and the Modules window, the Immediate window results before and after the update including `BC30201`, the Hot Reload output, and the label values. The screenshots are not preserved with the evidence; the Edit and Continue log corroborates the valid change and the applied update, but not what the IDE displayed.
- Four break and resume cycles were logged before the edit for one click; function evaluation by the Immediate window is the likely cause, which is inferred.
- `devenv` started about 32 short-lived x86 `msvsmon` processes during the attach, from 21:28:28 to 21:28:52 CST, and one MSBuild node at the project load.
- The shell that started Visual Studio exited while Visual Studio was running, after Visual Studio had inherited its environment. A first shell, at High integrity, was abandoned before any launch.
- The monitor's `TokenElevation` query failed throughout; its elevation type and integrity level readings worked.
- Opening the project restored it against the user NuGet configuration, rewriting `project.assets.json`, the `dgspec`, and `project.nuget.cache` in `obj`; Visual Studio created `.vs`, including `.suo`.
- `privateregistry.bin` of the `RoslynDev` hive changed, as each session changes it. That of the stock hive `18.0_e34b10de` also changed content, SHA-256 `BA953623...` to `350BE806...`, between the snapshots before and after the session, while its last-write time stayed at 18:03:30 CST; no stock Visual Studio process was recorded. The change is unexplained.
- Two empty files, `Local\ApplicationPrivateSettings.lock` and `Local\Extensions\extensions.configurationchanged`, are missing from the DR-3B, DR-3C, DR-3D, and DR-4B hive backups in `%TEMP%`; the D29C backup and the live hive still have both. The snapshot before the launch, at 18:53 CST on November 12, counted 1,339 files in each of the DR-3B and DR-3C backups, and the snapshot after shutdown, at 21:46 CST, 1,337; the snapshots did not count the DR-3D and DR-4B backups. The backup folders that held the files all have a last-write time of 20:54:08 CST on November 12; a folder's last-write time does not record what changed it, and the exact time of the removal is not recorded. The monitor did not watch `%TEMP%`. The cause is unknown; the backups were not restored.

## My Project and the generated application infrastructure (DR-6)

DR-6 checks the `My` infrastructure of a Windows Forms project, as ordinary VB and with `vbnext`, on `net10.0-windows` and `net48`. P2-A and P2-B ran from the command line on November 12 and 13, 2026; P2-C through P2-F are untested. All `My Project` designer files, `Application.Designer.vb`, `Settings.Designer.vb`, and `Resources.Designer.vb`, were written by hand in the style that Visual Studio generates, the same in every cell of a phase. No Visual Studio generator, settings designer, resource designer, or My Project page ran, so DR-6 shows that such code compiles and runs with the Preview 1 compiler, not that the generators produce it; generator validation is P2-F, untested.

### DR-6 environment and method

| Item | P2-A and P2-B |
| --- | --- |
| Compiler | Preview 1 `vbc.exe`, SHA-256 `FAD3987F...`, through the project's import of `VbNext.Toolset.props`; each build log shows that compiler as the tool path, and no stock `vbc.exe` or `RoslynHive` |
| SDK and restore | SDK 10.0.400 from the sample's `global.json`; `dotnet build -v:d --disable-build-servers` with a binary log, restoring from the empty package source of DR-1C |
| Cells | For each target, cell A with no `LangVersion` (17.13) and cell B with `-p:LangVersion=vbnext`, built from identical source; one build per cell, no retries |
| Sample | The DR-1 Windows Forms sample, `MyType` `WindowsForms`, `StartupObject` `My.MyApplication`, in fresh copies under `C:\VbNext IDE Test` |
| Comparison | Metadata, method IL with tokens replaced by names, PDB compilation options, and, in P2-B, the embedded resources, read with System.Reflection.Metadata |
| Visual Studio, debugger | Not used |

### DR-6 P2-A: ordinary VB and `vbnext` `My` infrastructure

P2-A built `DR6P2A-N10-A`, `-N10-B`, `-N48-A`, and `-N48-B`, copied from the DR-1B and DR-1C Preview 1 samples, and compared them with each other and with the earlier stock-SDK builds `DR1B-C-OrdinaryVb-StockSdk` and `DR1C-C-Net48-StockSdk` (cell C, not rebuilt).

| Check | `net10.0-windows` | `net48` |
| --- | --- | --- |
| Builds | A and B: 0 warnings, 0 errors | A and B: 0 warnings, 0 errors |
| Metadata and IL | A = C. A and B differ only in `Form1.Form1_Shown` and its lambda, as below | A = B = C |
| `My` methods | All 35 identical in A and B: `MyApplication` 4, `MyComputer` 1, `MyProject` 6, `MyForms` 9, `MyWebServices` 7, `ThreadSafeObjectProvider` 2, `MySettings` 5, `MySettingsProperty` 1 | Identical, as all IL |
| Entry point | `My.MyApplication::Main` | `My.MyApplication::Main` |
| PDB options | A and B differ only in the language version, `17.13` and `vbnext`; A and C only in the compiler and runtime versions | The same |
| Smoke run | A and B: 17 PASS of 17, `RESULT PASS` | A and B: 16 PASS of 16, `RESULT PASS` |

The one difference outside `My` code is VB-065. In the sample's `--smoke` harness in `Form1_Shown`, interpolated strings passed to `StringBuilder.AppendLine` bind to `AppendInterpolatedStringHandler` with `vbnext` and to `String.Format` with 17.13, on `net10.0-windows` only, as [Interpolated strings (VB-065)](#interpolated-strings-vb-065) records. Evidence: `C:\VbNext IDE Test\evidence\DR6-P2A`, SHA-256 manifest `28675F68...`, 66 files.

### DR-6 P2-B: `My.Resources` and persistent `My.Settings`

P2-B added to the P2-A sources, the same in all four cells: `My Project\Resources.resx` with one string, `P2BGreeting`, `"Hello from My.Resources"`, and a hand-written `Resources.Designer.vb` in `My.Resources`; a user-scoped string setting, `P2BNote`, default `"default-note"`, in `Settings.settings` and `Settings.Designer.vb`; and a test file, `P2BTest.vb`, a `Handles Me.Shown` handler with no interpolated strings. `SaveMySettingsOnExit` stays `False`, so only the explicit `Save` writes settings. To keep settings apart, each build set `-p:Company` and `-p:Product` to the cell's name, and the four executables are in different folders; the assembly name stays `DR1AWinFormsVbNext`.

The cells were `DR6P2B-R2-N10-A`, `-N10-B`, `-N48-A`, and `-N48-B`, built at 08:00 CST on November 13, 2026. All four builds used the Preview 1 compiler, with no `/langversion` in A and `/langversion:vbnext` in B, and had 0 warnings and 0 errors. An external runner started each executable twice, as two processes, with a token unique to the run, `persisted-<cell>-<GUID>`:

1. Save process: `P2BNote` is `"default-note"`; it is set to the token; `My.Settings.Save()`; then it is set to `"unsaved-after-save"` without saving.
2. The runner checks that exactly one file, `user.config`, exists in the cell's own settings folder, that it holds the token and neither the unsaved value nor the application-scoped setting, and that no roaming folder exists.
3. Reload process: `P2BNote` is the token at start; it is set to `"unsaved-before-reload"` without saving; `My.Settings.Reload()`; `P2BNote` is the token again.
4. The runner checks that `user.config` has the same size, last-write time, and SHA-256 after the reload process.

Both processes also check `My.Resources.P2BGreeting`, `My.Resources.ResourceManager.BaseName`, the application-scoped `My.Settings.GreetingText`, and `My.Application.SaveMySettingsOnExit`.

| Cell | Save process | Reload process | `My.Resources.P2BGreeting` | `user.config` SHA-256, after save and after reload |
| --- | --- | --- | --- | --- |
| N10-A, `net10.0-windows`, 17.13 | 8 PASS, 0 FAIL, exit 0 | 8 PASS, 0 FAIL, exit 0 | `Hello from My.Resources` | `912BA02D...`, unchanged |
| N10-B, `net10.0-windows`, `vbnext` | 8 PASS, 0 FAIL, exit 0 | 8 PASS, 0 FAIL, exit 0 | `Hello from My.Resources` | `95A8C116...`, unchanged |
| N48-A, `net48`, 17.13 | 8 PASS, 0 FAIL, exit 0 | 8 PASS, 0 FAIL, exit 0 | `Hello from My.Resources` | `3A3E6891...`, unchanged |
| N48-B, `net48`, `vbnext` | 8 PASS, 0 FAIL, exit 0 | 8 PASS, 0 FAIL, exit 0 | `Hello from My.Resources` | `DF724006...`, unchanged |

The four `user.config` files, under `%LOCALAPPDATA%`, are distinct, and each holds only its own cell's token:

- `DR6P2B-R2-N10-A\DR1AWinFormsVbNext_Url_uyhpensf0aaffxqyezaepfshkxkhzvou\1.0.0.0\user.config`
- `DR6P2B-R2-N10-B\DR1AWinFormsVbNext_Url_sd5oqhkauamrl0addoqepj1pf35qhoag\1.0.0.0\user.config`
- `DR6P2B-R2-N48-A\DR1AWinFormsVbNext.exe_Url_411csstyys2sf2ed54kwbndpeiyjshvg\1.0.0.0\user.config`
- `DR6P2B-R2-N48-B\DR1AWinFormsVbNext.exe_Url_dynamm1knam4fqfenhd3x0ykoqmxlg3r\1.0.0.0\user.config`

`%LOCALAPPDATA%` gained exactly these four folders, which were left in place, and `%APPDATA%` did not change.

Comparison of cell A with cell B:

| Check | `net10.0-windows` | `net48` |
| --- | --- | --- |
| Method IL | 58 of 60 methods identical. `Form1.Form1_Shown` and `_Closure$__15-0._Lambda$__0`, in the `Form1.vb` inherited from P2-A, differ by VB-065 as in P2-A | 60 of 60 identical |
| `P2B_Shown` | Identical IL; no interpolated string handler | Identical IL |
| Metadata | The VB-065 type and member references and the token, blob, and IL hash shifts they cause, as in P2-A; and the intentional `AssemblyCompany` and `AssemblyProduct` values | Only the intentional `AssemblyCompany` and `AssemblyProduct` values |
| Embedded resources | Identical: `DR1AWinFormsVbNext.Resources.resources`, one entry, `P2BGreeting`, SHA-256 `CA629141...`, the same on both targets | Identical, the same resource |
| PDB options | Only the language version, `17.13` and `vbnext` | Only the language version |

Differences by target, not by language version:

- `user.config`: on `net10.0-windows` the folder is named `DR1AWinFormsVbNext_Url_<hash>` and the section types come from `System.Configuration.ConfigurationManager`, version 10.0.0.0, 941 bytes; on `net48` the folder is `DR1AWinFormsVbNext.exe_Url_<hash>` and the types come from `System`, version 4.0.0.0, 869 bytes. This is runtime configuration behavior, not compiler output.
- IL: the inherited sample has `ApplyApplicationDefaults` on `net10.0-windows` and `MyApplication_Startup` on `net48`, and `MyApplication`'s constructor and `Form1_Shown` differ with the sample's source for each target.
- PDB options: the conditional compilation symbols and the global imports.

Compared with P2-A, P2-B adds only `P2B_Shown` and its closure, the `P2BNote` accessors, and the `My.Resources` members; `Form1`'s constructor changes to connect the `Handles Me.Shown` handler, and the PDB lists 9 source files instead of 7. Evidence: `C:\VbNext IDE Test\evidence\DR6-P2B`, SHA-256 manifest `0F40C874...`, 126 files; the validation run is in its `rerun` folder.

#### DR-6 P2-B first attempt and harness correction

The first P2-B attempt, on November 12, stopped before any run. Both `net48` builds failed with `BC30451` for `ConfigurationManager` and `ConfigurationUserLevel` at one line of the first test file, which looked up the `user.config` path with `ConfigurationManager`; the `net48` project has no reference to `System.Configuration.dll`. The `net10.0-windows` builds succeeded. The errors were in the test file, not in the `My` code or the compiler, and were the same in A and B. The corrected test file, revision r2, has no `ConfigurationManager` lookup, takes the token from the runner, and adds the unsaved changes after the save and before the reload; the runner finds `user.config` instead. The first attempt's cells and evidence are kept unchanged, and the correction is recorded in the `harness-fix` folder of the P2-B evidence.

### DR-6 untested

- P2-C through P2-F, the remaining phases of DR-6:
  - P2-C: the application lifecycle, single-instance behavior, exceptions, shutdown, and a custom `Sub Main`.
  - P2-D: `My.Forms` default-instance behavior with primary constructors.
  - P2-E: the `My` infrastructure of WPF on .NET Framework 4.8 (`net48`).
  - P2-F: Visual Studio My Project pages and actual designer generation; it requires separate approval.
- The Visual Studio `My` generators, the settings and resource designers, and the My Project pages; DR-6 used hand-written files only.
- Settings other than one user-scoped string, other setting types and scopes, settings upgrade across versions, and roaming settings; resources other than one string.
- WPF, `net472` and `net47`, and the edges of the application framework that [Untested](#untested) lists.
- Visual Studio and the debugger.

### DR-6 deviations and evidence limits

- `Company` and `Product` were set per cell from the command line, so assembly attributes differ between the cells by design.
- P2-A: a variable-name clash in the copy script left one cell's `My Project` folder empty; the copy was redone before the build. The IL comparison script needed two fixes before its output was recorded, and only error-free output is in the evidence. The smoke runs were made after the `net10.0-windows` difference was found.
- Hive backups in `%TEMP%`: after DR-5D, more files went missing from the backups. A check at 22:42 CST on November 12 found the D29C backup at 1,230 files where the snapshots up to 22:34:38 CST had counted 1,234. On November 13 each backup was compared with the inventory written when it was made: D29C lacks 4 files, `ExcludedDirectories.lst`, `settings.json`, `ApplicationPrivateSettings.lock`, and `extensions.configurationchanged`, 800 bytes, and DR-3B, DR-3C, DR-3D, and DR-4B each lack the same 48 files, 39,330,246 bytes, among them caches, the two `privateregistry.bin` log files, window layouts, and the two empty files that [DR-5D deviations and evidence limits](#dr-5d-deviations-and-evidence-limits) records. Every remaining file still has the hash its inventory records. The affected backup folders have last-write times of 22:34:51 to 22:34:52 CST on November 12. The cause is unknown; no DR-6 command wrote to these backups, and the backups were not restored.
- The surviving backups were copied on November 13, 07:46 to 07:54 CST, to `C:\VbNext IDE Test\evidence\Protected-Backups`, each file checked against its source by SHA-256, with a SHA-256 manifest (`41566336...`, 6,501 files). The backups did not change during the copy, and their file counts were the same before and after the P2-B validation run. During that operation a helper function named `LP`, a built-in PowerShell alias of `Out-Printer`, was called three times with a path and no input; the default printer's queue showed no job afterwards.

## Known limitations

- **Compiler routing in the experimental instance.** The RoslynDev compiler extension installs per-hive MSBuild `ImportBefore` files under `%LOCALAPPDATA%\Microsoft\MSBuild\Current\`. When a build sets `RoslynHive` to that hive, as builds in the experimental instance do, and `UseSharedCompilation` is `false`, as `VbNext.Toolset.props` sets it, they point `VbcToolPath` at the extension's own compiler, assembly version `42.42.42.42` (`5.13.0-dev`), instead of the Preview 1 compiler (DR-3B.3, DR-3B.4). It is recorded as a known IDE integration issue, not fixed. The output matched Preview 1's for the DR-1A sample, which does not make the two compilers interchangeable. Use a command-line build without `RoslynHive` when the Preview 1 compiler must be the one used, as in DR-3D.2.
- **The compiler check.** The props' `VbNextCheckCompiler` target checks only that the configured `vbc.exe` exists. It does not check which compiler MSBuild actually ran; only a build log, or the compiler version a PDB records, shows that.
- **Array slices in the debugger.** The expression evaluator's array-slice workaround, `VBNEXT_EE_ARRAYSLICE_LOOP=1`, is experimental, opt-in, and off by default, and the underlying Visual Studio debugger behavior is not conclusively explained ([vbnext-vs1-validation.md](vbnext-vs1-validation.md#known-issue-array-slices-in-the-experimental-instance)). The investigation is closed and stays closed unless new regression evidence appears.
- **WPF designer and local types.** The WPF designer may need a completed build before it renders XAML that uses a type of its own project (DR-3D.1 to DR-3D.3). In DR-3D.3, the `MainWindow.baml` in `obj` was also absent after the project load, an observed design-time behavior with no confirmed cause, as [Side effects of opening a project](#side-effects-of-opening-a-project) records.
- **VB-065.** The overload change for interpolated string handlers needs more regression coverage on `net48` and on modern .NET, as [Interpolated strings (VB-065)](#interpolated-strings-vb-065) records.
- **An IDE build can follow a save during an Edit and Continue session.** In DR-5B, the edited `Form1.vb` was saved at 18:02:46 CST, and an IDE build using the RoslynDev compiler followed at 18:02:50, replacing the Preview 1 output. That the save caused the build is an inference, not an established fact. Before a test that must keep the Preview 1 output, discard the edit (Don't Save) or keep a copy of the output.
- **Vb.Next forms in the Immediate window after an Edit and Continue update.** In DR-5D, after one update of `RunButton_Click`, `? _stages(^1)` failed in that method with `BC30201: Expression expected.`, though it had succeeded before the update, and `? LastResult` still evaluated. This is an open defect; its cause is not established, and it is not fixed. The hypothesis is in [DR-5D evaluator failure and hypothesis](#dr-5d-evaluator-failure-and-hypothesis).
- **`/debugexe` defaults to native debugging.** With `/debugexe` on an apphost, breakpoints in VB code bind only after the debugger type is set to Managed (.NET Core) (DR-4B). The repeated launch under `/debugexe` (DR-4C, DR-4D) is recorded and not explained.
- **`net47` and `net472`.** Not yet tested for desktop applications.
- **Unverified in Visual Studio.** Edit and Continue and Hot Reload beyond the one ordinary method-body edit of DR-5B and the one index edit of DR-5D, other edits of Vb.Next forms, the Immediate window after an update, WPF and XAML Hot Reload, debugging on `net48` and under F5 from a project, My Project pages and the files they generate, publishing, and broader IDE compatibility. DR-3, DR-4, DR-5B, and DR-5D tested only the workflows in their tables.

## Untested

Remaining desktop validation, in priority order:

1. Edit and Continue of Vb.Next forms other than an index from the end (DR-5D tested `^1` to `^2` only), and the Immediate window after an update, where DR-5D failed; rude edits; Hot Reload in general; WPF and XAML Hot Reload. Basic debugging is done for the tested `net10.0-windows` workflows under `/debugexe` and by attach (DR-4, DR-5B, DR-5D); `net48` and F5 from a project are not.
2. My Project pages and the files they generate. DR-6 P2-A and P2-B tested hand-written, generated-style `My` files from the command line only; P2-C through P2-F are untested.
3. `net472` and `net47`.
4. Deployment and platform configurations: x86, x64, and ARM64; publish; single-file; ClickOnce.
5. Designer workflows beyond those DR-3 tested, and a WPF save round trip with captured evidence.

Also untested: the edges of the Windows Forms application framework, such as a single-instance application, a splash screen, `UnhandledException`, and high-DPI settings other than the default. Saving and reloading `My.Settings` is tested for one user-scoped string setting only (DR-6 P2-B).

Earlier IDE checks of console projects are in [vbnext-vs1-validation.md](vbnext-vs1-validation.md); they did not cover desktop applications.

## Proposed next milestone

A proposal, not yet authorized; each step needs the owner's approval before it starts:

1. Done: basic Visual Studio debugging for Windows Forms and WPF, with breakpoints, stepping, Locals, Watch, and the Immediate window, for the tested workflows (DR-4).
2. Partly done: Edit and Continue, one ordinary VB method-body edit (DR-5B), and one edit of a Vb.Next index from the end, `_stages(^1)` to `_stages(^2)`, which passed for its primary objective (DR-5D). The Immediate window failure after the DR-5D update is an open defect, tracked separately; no investigation or fix of it is authorized.
3. Partly done: My Project and the generated application infrastructure (DR-6). P2-A and P2-B passed from the command line with hand-written designer files; P2-C through P2-F are untested, and each needs the owner's approval.
4. Additional compatibility and publishing checks.

The array-slice investigation is not reopened by this milestone unless new regression evidence appears. Desktop stays first: this milestone does not advance Razor and VBHTML or MAUI work.
