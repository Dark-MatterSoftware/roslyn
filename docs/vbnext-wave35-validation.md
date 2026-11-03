# Wave 35 validation

This note records the checks at the end of wave 35: an audit of the repository, the test runs, the sample, and a rerun of the VB-179 probe of the code fixes and refactorings. It is a note for this independent fork, which is not affiliated with, endorsed by, or supported by Microsoft.

Status: the audit, the automated runs, and the probe are done. Wave 35 added no Visual Studio checklist of its own. The Edit and Continue rows of `docs/vbnext-wave34-validation.md` stay Pending, for the reason that note records.

## Audit

Checked on the commits from the wave 35 plan, `341c1cb8a4b`, to slice 169a, `f72dca8dcd3`.

| Check | Result |
| --- | --- |
| No approved syntax was reverted | Wave 35 changed none of `Syntax.xml`, `SyntaxKind.vb`, the parser, `Errors.vb`, `VBResources.resx`, or the public API lists. The HelloVbNext sample, which uses the forms of waves 5 to 35, builds with `vbnext` and prints its expected output. |
| No prior work was removed | No file was deleted. One test method was replaced: `VbNextRenameTests.RenameAnAliasOfATupleOrArrayReportsItsUsesAsConflicts`, from slice 164a, recorded the defect that slice 167a fixed, and `RenameAnAliasOfATupleOrArrayRenamesItsUses` runs the same cases and expects the uses renamed. One existing expectation changed: `RecordTests.DerivedPositionalRecord_KeepsItsPropertyWhenTheTypeDiffers` reports the shadowing warning on `Name` instead of `Name As Integer`, because a positional property is now located at its parameter's name. Wave 35 added 18 test methods. |
| Shared code that C# uses keeps its behavior | Slices 167a and 168a changed `IParameterSymbolExtensions.cs`, `PropertySymbolReferenceFinder.cs`, and `SelectionResult.cs`. The C# Workspaces suite, the find references and rename tests of EditorFeatures2, and the C# extract method tests pass. |
| Upstream defects stay apart from Vb.Next follow-ups | `docs/vbnext-missing-features.md` lists the upstream Visual Basic defects, and the upstream tooling and Visual Studio defects, apart from the Vb.Next rows, and so do the findings below. |

## Automated runs

| Run | Result |
| --- | --- |
| The seven suites on .NET Framework, after slice 168a | Passed. Syntax 4,196; Semantic 5,601; Symbol 3,398; Emit 4,961; Features 4,171; EditorFeatures 8,870; Workspaces 507. |
| The C# suites that the shared changes touch | Passed. Workspaces 1,230; the find references and rename tests of EditorFeatures2, 6,208; extract method in Features, 381. |
| The result provider suites, after slice 169a | Passed. Visual Basic 122; C# 344. |
| The compiler suites on CoreCLR, `net10.0` | Passed. Syntax 4,194; Semantic 5,524; Symbol 3,384; Emit 5,021. |
| The deploy of the four extensions to the RoslynDev instance, from the repo path | Built and deployed, Release, after the caches that the earlier `subst` build left were deleted, as `docs/vbnext-toolset.md` describes. |
| The HelloVbNext sample | Builds and runs, with the wave 35 module: `1,30,8,20 4 items ending in 20, not a list`. |

## Probe of the code fixes and refactorings

The probe of VB-179, kept outside the tests in `artifacts/probe164`, ran again on the sample, which now has the modules of waves 34 and 35. It ran the 55 Visual Basic refactorings, the 84 code fixes, and rename on the 286 statements where Visual Basic 17.13 reports an error, applied 4,893 actions, and renamed 367 symbols; its report is in `artifacts/probe35`. The totals do not compare with the report of slice 164a, whose sample had 272 such statements, so the rerun is compared at the places that wave 35 changed.

| Place | Slice 164a | Wave 35 |
| --- | --- | --- |
| Extract method and introduce variable on the `For Each ... Yield ... Next` of an XML literal | 5 actions break | none |
| Extract method on a guard `If` whose name is used after it | 3 actions break | none |
| Extract method on a `Return` of a ByRef Structure | 1 action breaks | none |
| Rename of an alias of a tuple, and of a record's positional property | 2 renames break | none |
| Extract method on the receiver of `queue(^2) &= "!"` | 1 action breaks | none |
| Exceptions | 16 | 16, the same ones, all in upstream Visual Basic |

### Findings

The rerun and a comparison of each finding with the same edit on a Visual Basic 17.13 form found two Vb.Next follow-ups and two upstream defects. They are recorded in `docs/vbnext-missing-features.md` and are not yet planned.

- Vb.Next: on `Make()(^1)`, in a read such as `Dim f = Make()(^3)` or a target such as `Make()(^2) += 5`, extract method, introduce variable, and introduce parameter take the name `Make` for a value of the module's type, and write `Dim make As WaveThirtyFive`. On `Make()(2)` extract method writes code that compiles, and the other two are not offered. The semantic model gives the name of a call whose result is indexed from the end the wrong type.
- Vb.Next: introduce parameter on `TypeOf value IsNot String Into text`, the condition of a guard `If`, replaces the test with a parameter and leaves `text` undeclared, so it binds to the namespace `System.Text`. On `TypeOf value IsNot String` the same refactoring writes code that compiles.
- Upstream: introduce parameter on a `For Each` control variable read in the loop moves the name to the call sites, where it is not declared, for `For Each order In orders` as for the names of `For Each (size, state) In orders`.
- Upstream: introduce parameter that updates the call sites writes the expression unqualified there, so a call in another module can bind to a member of the same name in a third module, as `Make` and `Items` in the sample do.
