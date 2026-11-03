# VB-179 Code fixes and refactorings on the Vb.Next forms

This note records what slice 164a found when the common Visual Basic code fixes and refactorings ran on the Vb.Next forms, what it fixed, and what it leaves for later. It is a note for this independent fork. It is not a Microsoft proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Classification: Tooling/diagnostics. The project decided the scope: fix the compiler bugs, the exceptions, and the wrong edits on Vb.Next forms, and record the bugs that Vb.Next only exposes in upstream Visual Basic.

## How the forms were probed

A probe test loaded the `vbnext/samples/HelloVbNext` sample with `vbnext` and the .NET 9 reference assemblies. It found the Vb.Next forms by compiling the same text as 17.13 and taking the statements where 17.13 reports an error: 272 statements, from 429 errors. On those statements it ran:

- every Visual Basic refactoring provider, 55 of them, on each statement, each node up to the statement, and each token start;
- every code fix, 84 providers, for the 209 diagnostics that the IDE analyzers and the compiler report in the sample;
- rename of every source symbol that the statements declare or use, 341 symbols.

For each action it recorded an exception, or an edit that adds compile errors to the documents it changes. It did not run fix all, or the actions that open a dialog. The probe is kept outside the tests, in `artifacts/probe164`, because it reads the sample from disk.

| | First run | After slice 164a |
| --- | --- | --- |
| Actions applied | 4,398 | 4,339 |
| Actions that throw | 94 | 11, all upstream |
| Edits that add errors | 1,183 | 1,070, of which 953 are one upstream bug |

The renames of operators that the probe also counted are left out: rename is not offered on an operator.

## Fixed in the compiler

- **Data flow analysis of a region in an index, a slice, or a collection literal.** Extract method and the analyzers ask for it, and it failed, so extract method was not offered on `^1`, `1..n`, the receiver of `list(^1)` or `span(1..n)`, `{"x", "y"}` as a `List(Of String)`, or `..items` in a literal. The index binding also marked the user's receiver and operands compiler generated where its lowered value shares them, so a parameter read through `list(^1)` was not a read of the parameter, and `depth` in `^depth` was an implicit operation. Now the lowered value is marked compiler generated except for the source nodes it shares, the semantic model records the source argument and receiver, and the region analysis of a region in them analyzes them. A collection literal or a spread records the compiler generated node that stands for it. Tests: `VbNextRegionAnalysisTests`.
- **Locals that a deconstruction declares** are now variables declared in a region, so extract method keeps them with the loop that declares them.
- **A failed nested deconstruction** in `For Each ((name, count As Long), ) In items` gave `count` an error type over its `As Long`, which asserted in Debug. A name with an As clause keeps its type.
- **A conversion from an undeclared name to a type parameter** whose constraint interface declares a `CType` operator asserted in Debug; extract method binds such code while it works. The constraint conversions are classified only for a source with a type.
- **The members of an extension block** had no declared symbol in the semantic model, and a declaration directly in the block asserted. They are now the module's members, as the compiler declares them.
- **A syntax walker** read the first child of a node before checking that it had one, and an empty slot of a deconstruction list has none, so the simplify type names analyzer threw on `For Each ((name, count), ) In items`. Tests: `VbNextSyntaxWalkTests`.
- **The name of a property in the `With` copy of an anonymous type**, `label With {.Size = 3}`, is now the property, so rename and find references reach it.

## Fixed in the IDE

| Feature | What it did on Vb.Next forms | Now |
| --- | --- | --- |
| Extract method | threw in an interface member body, in an extension block, and on `For Each` or `Await Each` with deconstruction | adds a private method to the interface, a method to the module, and keeps the loop's names |
| Extract method | made a method that lost `Async` for `Async Using` and `Await Each` | counts them as awaits |
| Extract method | extracted `Field`, a `ByRef` local or a `Return` of a `ByRef Function`, `= ByRef`, a ByRef Structure or span value, the target of `?.` assignment, and an `Into` name used later | not offered on them |
| Rename | threw on an alias of a tuple or an array | completes, and reports the alias's uses as conflicts (below) |
| Add accessibility modifiers | dropped `Record`, `ByRef`, `Iterator`, `Overloads`, and the other modifiers that declaration modifiers cannot represent | keeps them in the order written |
| Use named arguments | named the argument of `list(^1)`, which turns the index off | not offered on an `Index` or `Range` that does not convert to the parameter, as in C# |
| Invert `If` | inverted a condition with `TypeOf ... Into` | not offered |
| Generate `Equals` and `GetHashCode` | generated `=` and `<>` that pass a ByRef Structure as `Object` | no operators for a ref-like type |
| Encapsulate field | made an abstract property for a field of an interface | a `Shared` property with its body |
| Convert method to property | duplicated `ReadOnly` on a `ReadOnly` or `ByRef ReadOnly Function` | not offered on them |
| Convert property to methods | broke a `Shared` abstract interface property, its implementations, and a record's positional property | not offered on them |
| Convert tuple to struct | converted a deconstruction target, a tuple that `=` compares, and an alias's tuple | leaves them tuples |
| Introduce variable | named a local `t` beside a type parameter `T`, and introduced a constant from `NameOf` of a parameter in an attribute | avoids type parameter names, and is not offered there |
| Introduce parameter | threw on `NameOf` of a parameter in an attribute | not offered in an attribute |
| Simplify interpolation | removed `.ToString()` from a span, which `String.Format` cannot take | keeps it in Visual Basic |

Tests: `VbNextExtractMethodTests`, `VbNextAddAccessibilityModifiersTests`, `VbNextRefactoringTests`, `VbNextRenameTests`, and compiler tests beside the forms.

## Left for later

Vb.Next gaps, with follow-up rows:

- **VB-181.** `Make()(^1) = x`, `Make()(^2) &= "!"`, and `Items(^1) = x` through a property report BC30068, because the binder stores such a receiver of an index from the end in a sequence, which cannot be assigned. `list(^1) = x`, `Make()(1) = x`, and an array from a call work. Extract method finds it when it extracts `queue` from `queue(^2) &= "!"`.
- **VB-182.** Find references does not find the uses of an alias of a tuple or an array, so rename of such an alias reports them as conflicts; rename of a record's positional property does not rename its parameter.
- **VB-183.** Extract method and introduce variable break the `For Each ... Yield ... Next` expression in an XML literal, `TypeOf x IsNot T Into name` used after an `If`, and a `Return` of a ByRef Structure.

Upstream Visual Basic bugs that the sample exposes, recorded and not fixed:

- Introduce parameter adds `Shared` to a method of a `Module` (953 of the remaining edits), and leaves out type parameters of a generic method.
- Extract method and introduce variable type an anonymous delegate or an anonymous type as `Object`, which Option Strict On rejects. Vb.Next gives more expressions such types: `AddressOf` with a natural type, lambdas with `Optional` and `ParamArray`, and `With` copies of anonymous types.
- Extract method of the receiver of an element or field assignment returns a copy of a structure, such as an inline array, that cannot be assigned. Extract method of an expression in a single-line `If` throws.
- Introduce variable and introduce parameter for all occurrences replace a `New` expression in an `As New` clause, and the control variable of `For Each x In`.
- Convert tuple to struct qualifies the new type with `Global.` without the project's root namespace.
- Convert property to methods breaks a `Default` property, and rename of a member that implements an interface from metadata breaks the implementation.
- Replace conditional with statements types an anonymous delegate as `Object`.
