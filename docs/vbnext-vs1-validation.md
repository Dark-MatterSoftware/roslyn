# Vb.Next VS-1 validation: Preview 1 in stock Visual Studio

Phase VS-1 checks the published Vb.Next 1.0.0-preview.1 compiler in a stock Visual Studio, with no Roslyn VSIX. It is a note for this independent fork, which is not affiliated with, endorsed by, or supported by Microsoft.

Status: the automated checks passed on November 4, 2026. In the Visual Studio IDE, the owner's session on November 4 confirmed the build, the run, and debugging, and observed the expected limitations of IntelliSense, Watch, Immediate, and Hot Reload; rows 2, 5, 9, 11, 15, and 16 are still Pending.

## Environment

| Item | Value |
| --- | --- |
| Visual Studio | Visual Studio Community 2026, Release channel, 18.11.3; MSBuild 18.11.1.51303 at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` |
| .NET SDK | 10.0.403 in `C:\Program Files\dotnet`, pinned by `C:\VbNext VS Test\global.json` |
| Package | `VbNext-1.0.0-preview.1-win-net472.zip`, downloaded from the GitHub release `vbnext-v1.0.0-preview.1`; SHA-256 `9d1e7297af844111f91d9c61e345adc38e2683a31510dfbaa54cd10bddf9ca84`, the approved hash and the one in the published checksum file |
| Test folder | `C:\VbNext VS Test`, outside the repo, with a space in its path |

## Test solution

`C:\VbNext VS Test\solution\VbNextVs1.sln`, made by `C:\VbNext VS Test\setup-vs1.ps1`:

| Project | Toolset imported | `LangVersion` | Purpose |
| --- | --- | --- | --- |
| HelloVbNext | Yes, by the package's `samples\Directory.Build.props` | `vbnext` | The sample inside the extracted package |
| VbNextDebugApp | Yes, by full path | `vbnext` | Debugging: an ordinary method, a method with Vb.Next forms, and a loop for Hot Reload |
| PlainVbApp | Yes, by full path | default | Ordinary VB with the toolset |
| CSharpApp | No | default | C# beside the VB projects |

Outside the solution, `C:\VbNext VS Test\removal-check\` has StockPlainVb (ordinary VB, no import) and StockVbNext (`vbnext`, no import).

## Automated results: Visual Studio's MSBuild

`C:\VbNext VS Test\check-vs-msbuild.ps1` runs Visual Studio 18.11's `MSBuild.exe`, the .NET Framework MSBuild that the IDE builds with, with `-restore -v:d -m:1`, and reads which compiler built each assembly from the log. These are command-line runs of Visual Studio's MSBuild, not builds started from the IDE.

| Check | Result |
| --- | --- |
| Build of the solution | Passed: exit 0, no errors, no warnings; SDK 10.0.403 |
| Rebuild of the solution | Passed, the same |
| VB projects that import the toolset | HelloVbNext, VbNextDebugApp, and PlainVbApp are compiled by `C:\VbNext VS Test\package\VbNext-1.0.0-preview.1\compiler\net472\vbc.exe` |
| C# project | Compiled by the SDK's compiler, `sdk\10.0.403\Roslyn\binfx\..\bincore\csc.exe`, through the SDK's .NET Framework bridge |
| No file from the development repo | No build log names the repo |
| Output, run with the stock `dotnet` | HelloVbNext: 67 lines, equal to `vbnext/packaging/validation/expected-output.txt`; PlainVbApp: `plain:10`; CSharpApp: `csharp:ok`; VbNextDebugApp: `total:10`, `text:portable`, `last:30 size:three`, then `1: hot reload: before`, ... |
| Without the import, ordinary VB | StockPlainVb is compiled by the SDK's `bincore\vbc.exe` and prints `plain:10` |
| Without the import, `vbnext` | StockVbNext fails with BC2014 from the SDK's `vbc.exe` |

## Manual checklist in the Visual Studio IDE

Open `C:\VbNext VS Test\solution\VbNextVs1.sln` in Visual Studio 2026 18.11.3 (the Release channel, not Insiders), the normal instance with no Roslyn VSIX. Set Tools > Options > Projects and Solutions > Build and Run > "MSBuild project build output verbosity" to Detailed for item 2, and set it back afterwards.

| # | Area | Action | Expected | Result |
| --- | --- | --- | --- | --- |
| 1 | Build | Build > Rebuild Solution | 4 succeeded, 0 failed | Passed: 4 succeeded, 0 failed, 0 skipped, in 8.104 seconds |
| 2 | Compiler | In the Output window, Build pane, search for `vbc.exe` and `csc.exe` | The three VB projects run the package's `...\compiler\net472\vbc.exe`; CSharpApp runs the SDK's `csc.exe` | Pending |
| 3 | Error List, build | Set the Error List filter to "Build Only" | No errors | Passed: 0 errors, reported by the owner |
| 4 | Error List, IntelliSense | Set the filter to "Build + IntelliSense", open `VbNextDebugApp\Program.vb` | Expected limitation, not a build failure: IntelliSense errors on `Into text` (line 12), `Select Case` as a value (line 16), and `^1` (line 22), and possibly on `vbnext` as a language version, because the editor is the Roslyn inside Visual Studio | Observed: several hundred IntelliSense errors and warnings, almost all on the Vb.Next forms in HelloVbNext, as stock Visual Basic 17.13 reads them: records ("Too few type arguments to 'Point(Of X As Integer, Y As Integer)'"), `Select Case` as a value ("'Case' can only appear inside a 'Select Case' statement"), extension properties ("'WordCount' is not a member of 'String'"), `For Each ... Yield` in XML literals ("Expected closing '%>'"), ref structures ("Types with embedded references are not supported in this version of your compiler"), shared interface members, and `Index` and `Range` values; and one analyzer suggestion, CA1861. None is a build error (row 3) |
| 5 | Run | Set HelloVbNext as the startup project; Debug > Start Without Debugging | The 67 lines of the baseline, ending in `disposed sample` | Pending: not reported. VbNextDebugApp did run under the debugger and printed `total:10`, `text:portable`, and `last:30 size:three` |
| 6 | Breakpoint, ordinary VB | Startup project VbNextDebugApp; breakpoint on line 4 (`Dim sum As Integer = 0`) in `Total`; F5 | Stops on line 4 | Passed, as reported: debugging of ordinary VB code worked |
| 7 | Stepping and locals | F10 to line 6, F10 a few times; open the Locals window | Steps through the loop; `values`, `sum`, and `v` with their values | Passed, as reported with row 6; the individual locals were not listed |
| 8 | Breakpoint, Vb.Next method | Breakpoint on line 12 (`If TypeOf value Is String Into text Then`) in `Describe`; F5 to it | Stops on line 12 | Passed: the debugger stopped in `Describe`, where it showed the local `text` (row 10) |
| 9 | Stepping in Vb.Next code | F10 to line 13; then F5 to the second call, and F10 to line 22 | Steps as written; on the second call the `Into` test is false and execution reaches line 22 | Pending: stepping was not reported, although the debugger reached `Describe` (row 8) and the Watch and Immediate checks ran there (rows 12, 13) |
| 10 | Locals in Vb.Next code | At line 13, and at line 22 | Locals shows `value`, `text`, `numbers`, `size`; record what it shows | Passed for `text`: the local bound by `Into text` shows `"portable"`. `value`, `numbers`, and `size` were not reported |
| 11 | Watch, ordinary | At line 6, add Watch `sum` and `values.Length` | Their values | Pending: not reported |
| 12 | Watch, Vb.Next method | At line 22, add Watch `numbers(^1)`, `size`, `numbers.Length` | Likely limitation: the expression evaluator in Visual Studio reports an internal error in the Basic compiler for a method with a Vb.Next form, as in the wave 34 checklist; record the exact message for each expression | Observed: `numbers(^1)` fails with BC30201, "Expression expected". The evaluator installed with Visual Studio parses the expression as stock Visual Basic, which has no `^n`. `size` and `numbers.Length` were not reported |
| 13 | Immediate | At line 22, in the Immediate window, `?size` and `?numbers(^1)` | Record the result; the same limitation is likely | Observed: `?numbers(^1)` fails with BC30201, as in row 12. `?size` was not reported |
| 14 | Hot Reload | Remove the breakpoints; F5; while the loop prints, change `"hot reload: before"` on line 26 to `"hot reload: after"`; press Hot Reload (Alt+F10) | Untested: record whether it applies, whether the output changes, and any message in the Hot Reload output | Observed: Hot Reload fails with BC30205, "End of statement expected", at `Program.vb` line 12, `If TypeOf value Is String Into text Then`. Hot Reload compiles the changed document with the Roslyn inside Visual Studio, which cannot parse the Vb.Next form, so no edit to a file with a Vb.Next form applies |
| 15 | Removal | Stop debugging; in `PlainVbApp.vbproj` delete the `<Import ... VbNext.Toolset.props />` line; Rebuild PlainVbApp | Builds with the SDK's `vbc.exe` (Output pane), runs `plain:10`; then undo the edit | Pending: no result reported yet |
| 16 | Removal, `vbnext` | In `VbNextDebugApp.vbproj` delete the `Import` line; Rebuild it | Fails with BC2014 in the Error List; then undo the edit and rebuild | Pending: no result reported yet |

Record a result for each row as Passed, Failed, or Observed (for the expected-limitation rows 4, 12, 13, and 14), with any message.

### Other observation

Visual Studio showed a warning about an incompatible extension that needs a restart. Its cause is not identified. No Roslyn VSIX was installed in this instance for VS-1; whether the warning relates to an earlier Roslyn deployment, which went to the separate `RoslynDev` instance of Visual Studio Insiders, or to an unrelated extension is not known.

## Findings

Compiler and build, which Vb.Next provides:

- Builds in the IDE use the Vb.Next compiler from the package for the projects that import the toolset, and the SDK's compiler for C#: Rebuild Solution succeeds with 0 build errors (rows 1, 3), and the command-line runs of the same MSBuild show which compiler built each project. Row 2 would confirm the compiler paths in the IDE's own build output.
- The program built in Visual Studio runs correctly (VbNextDebugApp's output).
- The debugger works on the Vb.Next build: breakpoints and stepping in ordinary VB, a breakpoint in a method with Vb.Next forms, and a local bound by `Into` shown with its value (rows 6, 7, 8, 10). These come from the debug information the compiler writes.

IDE language services, which come from the Roslyn inside Visual Studio and do not know `vbnext`:

- IntelliSense reports several hundred errors on Vb.Next forms (row 4), none of which is a build error.
- Watch and Immediate cannot evaluate an expression with a Vb.Next form, such as `numbers(^1)`: BC30201 (rows 12, 13).
- Hot Reload fails in a file with a Vb.Next form: BC30205 at the `Into` test (row 14). Edit and Continue goes through the same compiler, so it is not available for such files either.

These IDE limitations are what phase VS-2, the experimental Vb.Next language services, would address; Preview 1 does not.

### Known issue: array slices in the experimental instance

This is outside VS-1, which uses stock Visual Studio; it is recorded here beside the Watch and Immediate findings. In Visual Studio 2026 Insiders, in the `RoslynDev` experimental instance with this repo's expression evaluator deployed (VB-184), the Immediate window evaluates the Vb.Next forms, but by default an array slice has the right length and zero elements. The compiler's slicing is not affected. An experimental, evaluator-only opt-in works around it for a slice that is the whole expression; it is off by default and is not a general production fix. Evidence, from the sessions at the breakpoint on line 43 of the harness's `VbNextForms`:

- Defect (C3-D7, C3-D11, C3-D13, Insiders 18.12.12328.396): `? numbers(1..^1)` shows `{Length=3} 0, 0, 0`; `numbers(1..4)`, `numbers(0..)`, `numbers(middle)` with a `Range` local, and a slice of an array literal also show zeros with the right length. `? numbers(1..^1)(0)` shows 0, and Locals still shows `numbers` as 10, 20, 30, 40, 50 afterwards.
- Control: the compiled slice is correct; `? slice` and Locals show 20, 30, 40. `? New Integer() {20, 30, 40}` and `Enumerable.ToArray(...)` also show the expected values. Only array slices were checked this way; the element values of a `List` slice were not checked.
- IL (C3-D8, C3-D10): the deployed Release evaluator emits the expected IL for `numbers(1..^1)`, `newarr` and then `System.Array.Copy`, the same as the Debug evaluator.
- Workaround (C3-D15): `? numbers(1..^1), emulator` shows 20, 30, 40, where the same expression without the specifier shows zeros. The `emulator` format specifier is not a documented, supported Visual Basic debugger feature: its behavior may change between Visual Studio versions, and it may not work for an expression that needs real function evaluation. It is a diagnostic workaround, not a guaranteed solution.
- Cause: not confirmed. The leading hypothesis is a problem at the boundary between the debugger's emulator and function evaluation; it is not confirmed as a defect in the debugger's implementation (Concord). The opt-in below, which avoids `Array.Copy` in the evaluator's method, is consistent with this hypothesis but does not confirm it.
- Opt-in (C3-D22 to C3-D29A), experimental: `VBNEXT_EE_ARRAYSLICE_LOOP=1`. When it is set and the module's portable PDB records `vbnext`, the evaluator copies the elements of a slice that is the whole expression with a loop instead of `Array.Copy`. Only the exact value `1` enables it. It is read from the evaluator's process each time an evaluation context is created; the evaluator runs in `devenv.exe`, so the variable must be in the environment that Visual Studio starts with, for example set in the PowerShell session that runs `vbnext\Start-VbNextVisualStudio.ps1`. Turning it on or off needs a Visual Studio restart.
- Off by default: without the variable, or with any other value, the evaluator emits the legacy `Array.Copy` lowering unchanged. Modules built with VB 17.13, and modules with a Windows PDB, are unchanged with the variable set or not.
- Live result (C3-D29D, Insiders 18.13.12409.143, Release evaluator SHA256 `DC964F96...`, MVID `0513f815-b1ea-4a5d-9601-7bb16667f626`): the evaluator was loaded from the `RoslynDev` extension folder, in `devenv.exe` only, and `VBNEXT_EE_ARRAYSLICE_LOOP=1` was confirmed in `devenv.exe`'s environment block. With the opt-in, `? numbers(1..^1)` and `? numbers(middle)` show `{Length=3} 20, 30, 40`; without it, the same evaluator shows `0, 0, 0`. `? slice`, `? numbers(^1)`, `? word(..5)`, `? numbers(fromEnd)`, `? size`, `? numbers`, `? numbers(0)`, `? numbers.Length`, `? numbers(1..^1), emulator`, and Locals are the same in both runs, with no errors or side-effect warnings.
- Tests: the evaluator's unit tests cover an inline range (`a(1..^1)`) and a stored `Range` (`a(r)`, the form of `numbers(middle)`), `String`, `Object`, and generic arrays, ByRef and captured sources, a single read of the source, unchanged VB 17.13 and Windows PDB modules, and the exact-`1` rule; the filtered pass of 41 tests in C3-D29A passed. Offline, the evaluator's methods with the loop off and on matched on .NET Framework 4.7.2 (C3-D23, 25 cases) and on .NET 10.0.15 (C3-D27, 32 cases).
- Limitation: only a slice that is the whole expression is rewritten (`SliceRewriter_NestedSlice_IsNotRewritten`). A slice inside a larger expression, such as `numbers(1..^1)(0)` or `Sum(numbers(1..^1))`, still uses `Array.Copy` and may still show zeros, with the opt-in or without. There, `, emulator` remains the diagnostic workaround, with the caveats above.

## Remaining gaps

- Row 2: the compiler paths in the IDE's Build output.
- Row 5: HelloVbNext's 67 lines when run from the IDE.
- Rows 9 and 11: stepping in the Vb.Next method, and Watch on ordinary expressions.
- The unreported parts of rows 10, 12, and 13: Locals `value`, `numbers`, `size`; Watch `size` and `numbers.Length`; Immediate `?size`.
- Rows 15 and 16: removing the import in the IDE.
- The cause of the incompatible-extension warning.
