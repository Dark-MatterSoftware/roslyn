# Wave 34 validation

This note records the checks at the end of wave 34: an audit of the repository, the test runs, and a session in Visual Studio with the deployed extensions. It is a note for this independent fork, which is not affiliated with, endorsed by, or supported by Microsoft.

Status: the audit and the automated runs are done. The Visual Studio session ran on November 3, 2026, in Visual Studio 2026 Insiders with the deployed extensions. The debugger, formatting, and code fix rows passed. The two Edit and Continue rows did not run, because Hot Reload is disabled in that Visual Studio, so the Edit and Continue work of wave 34 is not yet validated in Visual Studio, and the session is not complete.

## Audit

Checked on the commits from the wave 34 plan, `ff2c0d9a2c9`, to slice 165a, `a807211be19`.

| Check | Result |
| --- | --- |
| No approved syntax was reverted | Wave 34 changed none of `Syntax.xml`, `SyntaxKind.vb`, the parser, `Errors.vb`, `VBResources.resx`, or the public API list. The HelloVbNext sample, which uses the forms of waves 5 to 34, builds with `vbnext` and prints its expected output. |
| No prior work was removed | No file was deleted, and no test method was removed. One existing test expectation changed: `IntroduceVariableTests.TestCantExtractMethodTypeParameterToField` expects the name `t1`, because the `t` it expected beside the type parameter `T` does not compile (BC32089). One test gained a line: `VbNextClassifierTests.TestWaveSixteenForms` classifies the `Size` in the `With` copy of an anonymous type as a property. |
| The pattern review made no code change | Slice 165a changed four files, all in `docs`: the review, the mission, the phases, and the missing features. |
| Upstream defects stay apart from Vb.Next follow-ups | `docs/vbnext-vb179-review.md` lists them separately, and the wave 35 plan keeps them out of its Vb.Next rows. |

## Automated runs

| Run | Result |
| --- | --- |
| The seven suites on .NET Framework, after slice 164a | Passed, after the two expectation updates above. Syntax 4,196; Semantic 5,600; Symbol 3,398; Emit 4,956; Features 4,164 plus the updated test; EditorFeatures 8,868 plus the updated test; Workspaces 503. |
| The expression evaluator suite | Passed, 397. |
| The compiler suites on CoreCLR, `net10.0` | Passed. Syntax 4,194; Semantic 5,523; Symbol 3,384; Emit 5,016. |
| The deploy of the three extensions to the RoslynDev instance | Built and deployed, Release. |
| The deploy of the debugger's expression evaluator to the RoslynDev instance | Built and deployed, Release, from a drive letter mapped with `subst`. From the repo path, which has a space in it, `VSDConfigTool` failed with `Unknown argument 'C:\VB.Net'`; VB-184 fixed that, and the evaluator now deploys from the repo path with the other extensions. |
| The HelloVbNext sample | Builds and runs, with the wave 34 module: `closed 4:small draft,40:other 4`. |

## Visual Studio session

The extensions are deployed to the Visual Studio experimental instance with `vbnext/Start-VbNextVisualStudio.ps1`, and the sample opens in it. Each row is recorded as it is run. A row that is not run stays Pending, and the work it covers is not validated in Visual Studio. Edits that a row makes to the sample are undone afterward, and the tree is clean after the session.

| # | Area | Step | Expected | Result |
| --- | --- | --- | --- | --- |
| 1 | Debugger | Break on `Return text` in `WaveThirtyFour.Label`, and evaluate `order`, `text`, and `order.Item1 > 100 AndAlso order.Item2 = "open"` in the Immediate window | The tuple, the label, and whether the first arm matched | Passed, after the expression evaluator was deployed: `(250, "open")` with `Item1: 250` and `Item2: "open"`, then `"large open"`, then `True`. With only the three extensions, each of these expressions reports an internal error in the Basic compiler, `Unexpected value 'ExpressionStatement'` or `'ReturnStatement'`, because the evaluator installed with Visual Studio reads the method with its own compiler. |
| 2 | Debugger | Break after `Dim all` in `WaveThirtyFour.Run`, and evaluate `all(^1)`, `all(1..^1).Count`, and `labels(^2)` in Watch | `"closed"`, `2`, `"4:small draft"` | Passed: `"closed"` as `String`, `2` as `Integer`, and `"4:small draft"` as `String`. |
| 3 | Debugger | In the same frame, evaluate `all(^1), nq` | The value without quotes, so a format specifier still works | Passed: `closed`. |
| 4 | Edit and Continue | Break in `WaveThirtyFour.Label`, change `"other"` to `"rest"`, and continue | The edit applies without a rude edit, and `Label((40, "open"))` returns `"rest"` | Pending. Not validated: the edit did not apply, and `text` was `"other"` on the third call. The Hot Reload output reports "Hot Reload has been disabled because there are no language services that support it" when the session starts, so this row tests nothing about Vb.Next. See the note after the table. |
| 5 | Edit and Continue | Break in `WaveThirtyFour.Run`, add a line `Console.WriteLine(all.Count)`, and continue | The edit applies | Pending. Not run, for the reason of row 4. |
| 6 | Formatting | Type `Dim x = all( ^ 1 )` and `Dim y = all( 1 .. 2 )` and format the document | `all(^1)` and `all(1..2)` | Passed. |
| 7 | Code fixes | Select `{..labels, ..extra}` and run Extract Method | A method that returns `List(Of String)`, and the sample still builds | Passed. |
| 8 | Code fixes | Put the caret in `Label` and run Add accessibility modifiers on `Module WaveThirtyFour` | `Friend Module WaveThirtyFour` | Passed: `Friend Module WaveThirtyFour`. The same menu offers Extract interface on the module, which a module cannot implement; that is upstream, as the wave 35 plan records. |
| 9 | Code fixes | Rename `Size` in `label With {.Size = 3}` in `WaveSixteen` | Every `Size` of that anonymous type is renamed | Passed: the three uses are renamed to `Extent`, without a conflict. |
| 10 | Code fixes | Open the quick actions on `all(^1)` | No action adds an argument name | Passed. |

### Why Edit and Continue did not run

Visual Studio loads no Hot Reload language service from the deployed Roslyn. The composition errors of the `RoslynDev` hive, in `ComponentModelCache\Microsoft.VisualStudio.Default.err`, show that `ManagedHotReloadLoader` in `Microsoft.VisualStudio.LanguageServices` fails to load: it needs the type `Microsoft.VisualStudio.HotReload.IHotReloadComponentLoader`. The `Microsoft.VisualStudio.HotReload.Components` package that the repo builds against, `18.12.27-gc0a8171a44` in `eng/Packages.props`, has the type. The assembly installed with this Visual Studio Insiders, file version `18.12.29.43889`, does not. The loader comes from upstream Roslyn, in "Switch to Microsoft.VisualStudio.HotReload.Components services (#85672)", and Vb.Next does not change it.

So this is a mismatch between upstream Roslyn and the installed Visual Studio, not a Vb.Next defect. Rows 4 and 5 run again when a Visual Studio whose `Microsoft.VisualStudio.HotReload.Components` has that type is installed. Until then, the Edit and Continue work of wave 34 is covered only by the automated tests of phase 162.
