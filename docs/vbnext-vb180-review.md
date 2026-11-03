# VB-180 Review of the pattern surface

This note reviews the patterns of VB-004, VB-066, and VB-171, as `docs/vbnext-drift-review-wave33.md` decided in its finding 7. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Classification: design review; no code change. The review records what the surface is, how the parser reads it, what it costs, and the project's decision.

## The forms

All forms are on `vbnext` only. Visual Basic 17.13 reports each one as not available.

| Form | Example | Locked in |
| --- | --- | --- |
| Type test in `Select Case` | `Case Is TypeOf Customer` | `docs/vbnext-vb004-syntax.md`, 5e |
| Relational test after a type test | `Case Is TypeOf Integer AndAlso Is > 0` | 5e |
| Member test after a type test | `Case Is TypeOf Customer AndAlso .Balance > 0` | 5f |
| Positional pattern | `Case Is (1, 2)`, `Case Is TypeOf Point(0, 1)` | 5g |
| Relational and range elements | `Case Is (> 0, "open")`, `Case Is (1 To 9, "draft")`, at any level | VB-171, `docs/vbnext-vb171-proposal.md` |
| List pattern | `Case Is {1, 2}`, `Case Is {}` | 11a |
| `Select Case` expression | `Dim label = Select Case order ... End Select` | 5h |
| Named type test in `Select Case` | `Case Is TypeOf Circle Into c` | `docs/vbnext-vb066-syntax.md` |
| Named type test in `If` | `If TypeOf value Is Order Into o AndAlso o.Total > 100 Then` | VB-066 |
| Guard | `If TypeOf value IsNot Customer Into c Then Return` | `docs/vbnext-vb072-syntax.md` |
| Type list | `If TypeOf value Is {Customer, Order} Then` | `docs/vbnext-vb054-syntax.md` |

Each form reuses a VB keyword in its existing sense: `Is` before a test, `TypeOf ... Is` for a type, `To` for an inclusive range, `AndAlso` to add a condition, `Into` to name a result, as LINQ does, and `.Member` for a member of the value, as `With` does. The values are compared as `Case` compares them, with VB's conversions and `Option Compare`.

## How the parser reads them

Almost every form is chosen from the first one or two tokens:

- After `Case Is`, the next token chooses: `TypeOf` a type test, `(` a positional pattern, `{` a list pattern, and anything else the existing relational clause.
- After the type of a type test, `(` followed by `)` or `,` is an array type, and any other `(` starts the positional elements; that is a peek of two tokens.
- After `AndAlso` in a type test, `.` starts a member test, and anything else a relational test.
- In a positional element, a relational operator starts a relational element, and `To` after a value makes it a range.
- `Into` is a contextual keyword, read only after the type of a type test, and only where a name may be declared: a `Case Is TypeOf`, or the condition of an `If` or `ElseIf`.
- After `TypeOf ... Is`, `{` starts a type list.
- `Select` where a value is expected starts a `Select Case` expression. Between its arms, the parser looks past line breaks and blank lines for `Case` or `End Select`, so that a value ends where a statement would.

There is one place where the parser rewinds. A positional element that starts with `(` is first read as a nested pattern. If the nested list holds no relational or range element, at any level, the parser restores the scanner and reads the element again as a tuple value, so `Case Is ((1, 2), 3)` compares a tuple, as it did before VB-171. The attempt at each level reads the levels inside it once more, so the work grows with the square of the nesting depth. Nesting in a pattern is shallow in practice.

## What it costs

- **Syntax.** 11 node types: the type, positional, list, and member case clauses, the case filter, the relational and range elements, the `Select Case` expression and its arm, the type list, and the named type test.
- **Binding.** `Binder_SelectCase.vb` grew from 580 lines before slice 5e to 1,440. Each pattern binds to the condition it tests, so no new lowering pass exists; `BoundPositionalPattern`, `BoundRelationalSubpattern`, and `BoundTypeOfDeclaration` keep the source shape for operations and the semantic model.
- **Parsing.** The case clauses in `ParseStatement.vb` grew by about 400 lines, with one restore point.
- **Tooling.** Each layer of the IDE has had to learn the forms: formatting and indentation, classification, completion and keyword recommendation, end construct generation, Edit and Continue (slice 162a), the debugger's expression evaluator (slice 161a), and the code fixes and refactorings (slice 164a, where extract method, invert `If`, and convert tuple to struct needed rules for `Into` and positional patterns).
- **Tests.** 40 compiler tests are about them: 15 in `SelectCaseTypePatternTests`, 4 in `PositionalRelationalPatternTests`, 19 in `PatternVariableTests`, and 2 in `TypeOfListTests`, with cases in `VbNextOperationTests` and in the Select Case tests, besides the IDE tests.

## Decision

The project keeps every form. The surface is consistent: each form reads from one or two tokens with an existing VB keyword in its existing sense, and the one place the parser rewinds keeps the meaning of code that compiled before VB-171.

The surface is frozen at these forms. A new pattern form, such as `Not` or `Or` between patterns, a member test nested inside a member or positional element, a slice or a rest in a list pattern, or a pattern outside `Select Case` and the condition of an `If`, needs a proposal in the form of `docs/vbnext-mission.md`, and its "Complexity and risks" weighs this review: the node types it adds, any lookahead past two tokens or any new restore point, and the IDE layers it must reach.
