# Vb.Next desktop validation: Windows Forms and WPF from the command line

Milestone DR-1 checks that Windows Forms and WPF applications build and run with the Vb.Next 1.0.0-preview.1 compiler, on `net10.0-windows` and `net48`, with and without `vbnext`, and compares ordinary VB with the stock compiler. It is a note for this independent fork, which is not affiliated with, endorsed by, or supported by Microsoft. The mission and the language and framework API boundary are in [vbnext-mission.md](vbnext-mission.md).

Status: completed from the command line on November 11, 2026. Runs DR-1A to DR-1I passed or ended as recorded below. No Visual Studio, designer, or debugger validation of desktop applications was done; that is pending, as [Untested](#untested) lists.

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
- `My.Settings` was read, not saved or reloaded.
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

## Untested

Remaining desktop validation, in priority order:

1. `net472` and `net47`.
2. The Windows Forms designer in Visual Studio.
3. The WPF XAML editor and designer.
4. My Project pages and the files they generate.
5. Builds and debugging of desktop applications inside Visual Studio.
6. Hot Reload and Edit and Continue.
7. Deployment and platform configurations: x86, x64, and ARM64; publish; single-file; ClickOnce.

Also untested: the edges of the Windows Forms application framework, such as a single-instance application, a splash screen, `UnhandledException`, high-DPI settings other than the default, and saving `My.Settings`.

Earlier IDE checks of console projects are in [vbnext-vs1-validation.md](vbnext-vs1-validation.md); they did not cover desktop applications.
