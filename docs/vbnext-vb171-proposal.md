# VB-171 Relational tests inside positional patterns

This note proposes a form for VB-171, in the structure that [vbnext-mission.md](vbnext-mission.md#proposal-template) sets out. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

## Proposal

Let an element of a positional pattern test its value with a relational operator or a range, as a `Case` clause already tests a whole value:

```vb
Select Case order                       ' order As (Integer, String)
    Case Is (> 0, "open")               ' a positive quantity that is open
    Case Is (1 To 9, "draft")           ' a quantity from 1 to 9 that is a draft
    Case Is (0, "closed")               ' equality, as slice 5g already compares
End Select
```

## Classification

Native VB syntax.

## Developer problem

A positional pattern, `Case Is (1, "a")` from slice 5g, compares each element with `=`. Code that needs anything else for one element, such as "positive and open", cannot say it in the pattern: `Case Is (> 0, "open")` reports BC30201, "Expression expected", so the test moves into a nested `Select Case` or an `If`, apart from the other elements it belongs with.

## Why this is VB

VB already writes these tests in `Case` clauses: `Case Is > 0` and `Case 1 To 9` are among the oldest forms in the language. The proposal adds no operator and no keyword; it lets the element of a positional list take the same two forms that a whole `Case` value takes. `Case Is` already precedes the list, so the element writes the operator alone, as `Case Is > 0` writes it after `Is`. The range form, `1 To 9`, is VB's own; C# has no range test in a pattern and writes `>= 1 and <= 9`.

## Spelling options

The project chooses one before any implementation:

1. **The operator alone, and `To` for a range:** `Case Is (> 0, "open")` and `Case Is (1 To 9, "draft")`. The element reads as the rest of a `Case Is` clause. This is the recommended spelling.
2. **`Is` repeated in each element, and `To` for a range:** `Case Is (Is > 0, "open")` and `Case Is (1 To 9, "draft")`. Each element reads as a complete `Case Is` clause, at the cost of repeating `Is`.
3. **The operator alone, without ranges:** `Case Is (> 0, "open")` only, as C# 9 writes `(> 0, "open")`; a range is written as two patterns or a separate test.

## Semantics

- Each element is compared as a `Case` clause compares a value: `> 0` as `Case Is > 0`, and `1 To 9` as `Case 1 To 9`, with VB's conversions and `Option Compare` for strings. An element without an operator keeps the `=` comparison of slice 5g.
- The operators are `=`, `<>`, `<`, `<=`, `>`, and `>=`, the ones `Case Is` takes.
- The forms apply wherever a positional pattern does, including after a type test, as in `Case Is TypeOf Point(> 0, 0)`, and in nested lists, as in `Case Is ((> 0, 0), "a")`.
- The value is evaluated once, and the elements are tested left to right, as slice 5g tests them.

## Alternatives

- **A nested `Select Case` or an `If`**, which works today, separates one element's test from the others.
- **A member filter after a type test**, `Case Is TypeOf Point AndAlso .X Is > 0` from slice 5f, already tests a member with a relational operator, but only after a type test and by member name.
- **Interop only.** Not applicable: this is how VB code is written, not a .NET capability.

## Compatibility and semantic impact

- Every proposed form is a syntax error today (BC30201, or BC30198 for `1 To 9`), so no existing code changes meaning.
- The forms are gated on `LanguageVersion.VbNext`. Visual Basic 17.13 keeps its errors.
- No framework type or API is involved, so the feature is independent of the target framework, as [the language and framework API boundary](vbnext-mission.md#language-and-framework-api-boundary) requires.
- Operations: a relational element would be an `IRelationalPatternOperation`, and a range an `IBinaryPatternOperation` of two relational patterns, where slice 154a's rules for constant patterns hold; otherwise the clause keeps the case clause it has.

## Complexity and risks

- Small. The parser takes an operator or a `To` inside a positional list, and the binder reuses the comparisons of relational and range `Case` clauses.
- A reader must know that `>` inside the list belongs to the element, not to the `Case Is` before it; spelling option 2 removes that doubt at the cost of repetition.
- The forms add to the pattern surface of VB-004, which is already the largest pattern design in this project.

## Implementation and documentation impact

- Parser: a positional element that starts with a relational operator, or that contains `To`.
- Syntax: a node for a relational or range element in `Syntax.xml`.
- Binder: the comparisons in `Binder_SelectCase.vb`, reusing relational and range clause binding.
- Operations: relational and binary patterns where slice 154a applies.
- IDE: classification and completion inside the list.
- Documentation: `docs/vbnext-vb004-syntax.md` gains the forms; `docs/vbnext-phases.md` gains an implementation slice.

## Recommendation

Proceed with spelling option 1, if the project approves it. Implementation is a later slice, planned only after that decision.

## Decision

Approved in slice 158a, with spelling option 1: an element of a positional pattern may be a relational operator and a value, as in `Case Is (> 0, "open")`, or a range with `To`, as in `Case Is (1 To 9, "draft")`, with the semantics above. Slice 158b implements it.
