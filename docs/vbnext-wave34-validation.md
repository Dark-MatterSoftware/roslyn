# Wave 34 validation

This note records the checks at the end of wave 34: an audit of the repository, the test runs, and a session in Visual Studio with the deployed extensions. It is a note for this independent fork, which is not affiliated with, endorsed by, or supported by Microsoft.

Status: the audit and the automated runs are done. The Visual Studio session is pending, so the debugger, Edit and Continue, and code fix work of wave 34 is not yet validated in Visual Studio.

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
| The HelloVbNext sample | Builds and runs, with the wave 34 module: `closed 4:small draft,40:other 4`. |

## Visual Studio session

The extensions are deployed to the Visual Studio experimental instance with `vbnext/Start-VbNextVisualStudio.ps1`, and the sample opens in it. Each row is recorded as it is run. A row that is not run stays Pending, and the work it covers is not validated in Visual Studio.

| # | Area | Step | Expected | Result |
| --- | --- | --- | --- | --- |
| 1 | Debugger | Break on `Return text` in `WaveThirtyFour.Label`, and evaluate `order`, `text`, and `order.Item1 > 100 AndAlso order.Item2 = "open"` in the Immediate window | The tuple, the label, and whether the first arm matched | Pending |
| 2 | Debugger | Break after `Dim all` in `WaveThirtyFour.Run`, and evaluate `all(^1)`, `all(1..^1).Count`, and `labels(^2)` in Watch | `"closed"`, `2`, `"4:small draft"` | Pending |
| 3 | Debugger | In the same frame, evaluate `all(^1), nq` | The value without quotes, so a format specifier still works | Pending |
| 4 | Edit and Continue | Break in `WaveThirtyFour.Label`, change `"other"` to `"rest"`, and continue | The edit applies without a rude edit, and `Label((40, "open"))` returns `"rest"` | Pending |
| 5 | Edit and Continue | Break in `WaveThirtyFour.Run`, add a line `Console.WriteLine(all.Count)`, and continue | The edit applies | Pending |
| 6 | Formatting | Type `Dim x = all( ^ 1 )` and `Dim y = all( 1 .. 2 )` and format the document | `all(^1)` and `all(1..2)` | Pending |
| 7 | Code fixes | Select `{..labels, ..extra}` and run Extract Method | A method that returns `List(Of String)`, and the sample still builds | Pending |
| 8 | Code fixes | Put the caret in `Label` and run Add accessibility modifiers on `Module WaveThirtyFour` | `Friend Module WaveThirtyFour` | Pending |
| 9 | Code fixes | Rename `Size` in `label With {.Size = 3}` in `WaveSixteen` | Every `Size` of that anonymous type is renamed | Pending |
| 10 | Code fixes | Open the quick actions on `all(^1)` | No action adds an argument name | Pending |
