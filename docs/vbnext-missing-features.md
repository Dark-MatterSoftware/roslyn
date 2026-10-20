# Vb.Next missing features

**Vb.Next** is an independent community effort. It is not affiliated with, endorsed by, or supported by Microsoft. This list is a project plan for investigation. It does not approve syntax, and it does not implement a language feature.

The baseline is `main` commit `8bd473fe`, where `LanguageVersion.Latest` is `VisualBasic17_13`. Compiler layout and compatibility risks are in [vbnext-roadmap.md](vbnext-roadmap.md). Phases and per-feature slices are in [vbnext-phases.md](vbnext-phases.md).

Demand notes cite open issues on [dotnet/vblang](https://github.com/dotnet/vblang) by comment count as of 2026-09-30. Comment count measures discussion, not votes. "C# N" means the C# version that shipped the analogous feature, used only as a landmark.

## Already in this tree

Do not schedule these again. They are gated in `ParserFeature.vb` or recorded as merged in `docs/Language Feature Status.md`:

- Through VB 16: async and iterators, `NameOf`, interpolated and multiline strings, null-conditional `?.`, tuples and inferred tuple names, binary literals and digit separators, `Private Protected`, non-trailing named arguments, partial modules and interfaces, read-only auto-properties, collection initializers, array literals, XML literals, `With` blocks, LINQ query syntax, extension methods, nullable value types (`Nullable(Of T)`).
- VB 16.9: consuming init-only setters (`Feature.InitOnlySettersUsage`). Declaring them is still missing.
- VB 17.0: caller-argument-expression attribute.
- VB 17.13: recognizing the `Unmanaged` constraint, and overload-resolution priority.

Visual Basic also already has project-level `Imports` and `Module` entry points, so global usings and top-level statements are not missing in the C# sense.

## Review of a post-2016 gap list

Checked against this tree, not against a general description of VB. "Keep" means the gap is real and already has an id below.

| Suggestion | Verdict | Plan id |
| --- | --- | --- |
| Records, including record structs | Keep. `IsRecord` is hard-coded false. Include non-destructive `With` expressions in the same design. VB already has anonymous types and `With` blocks. | VB-031 |
| Pattern matching and switch expressions | Keep, with a narrower first slice. `Select Case` already has `Case Is` and `Case To`, and `TypeOf Is` exists. Missing are expression-level matches and list, property, and positional patterns. | VB-004 |
| Tuples and destructuring | Split. Tuple types, tuple literals, element names (`Name As String`, `Name:=value`), and inferred names shipped in VB 15 and 15.3 (`ParseTupleType`, `Feature.Tuples`, `Feature.InferredTupleNames`). Deconstruction assignment is the missing part. | VB-005 |
| Init-only setters | Split. VB 16.9 can call init-only setters. Source methods hard-code `IsInitOnly` to false, so VB cannot declare them. | VB-002 |
| `required` properties | Keep. Applying the attribute is `ERR_DoNotUseRequiredMember`. | VB-011 |
| `Span(Of T)`, `ref struct`, `ReadOnlySpan(Of T)` | Keep as a language gap. VB can mention an existing restricted type in limited synchronous code, and it errors when that type would be lifted (`ERR_RestrictedType1` and the lift errors). It cannot declare a `ref` structure. `IsRefLikeType` is hard-coded false. Ranges (VB-006) are part of making `Span` usable. | VB-037 |
| `nint` / `nuint` | Keep. `IsNativeIntegerType` is hard-coded false. | VB-020 |
| Unsafe pointers | Parked. Different safety model from the rest of this plan. The `Unmanaged` constraint itself is already recognized at VB 17.13. | Parked |
| Primary constructors | Keep. | VB-032 |
| Collection expressions and spread | Keep the new syntax. Array literals and `From { ... }` initializers already exist. | VB-035 |
| Target-typed `New` | Keep. First new syntax, after the small gate-proving changes. | VB-001 |
| Top-level statements | Absent, and low value. A `Module` with `Sub Main` already covers the scenario. | Parked |
| File-scoped namespaces | Absent. The VB-shaped version is optional `End Namespace`, not a brace-less C# port. | VB-059 |
| Default interface methods | Keep the authoring gap. Calling an existing default method is already gated on `RuntimeSupportsDefaultInterfaceImplementation`. VB interfaces still cannot supply a body. | VB-038 |
| Extension blocks | Keep the C# 14 block form. Extension methods themselves have shipped since well before 2016. | VB-039 |

Gaps that list does not mention, and that this plan already tracks: nullable reference types (VB-030, the largest vblang discussion), ranges and indexes (VB-006), target-typed `default` (VB-007), nothing-coalescing assignment (VB-010), `Enum` constraints (VB-008), `Async Sub Main` (VB-009, rejected today by `ERR_AsyncSubMain`), raw string literals (VB-033), readonly structures (VB-018), covariant returns (VB-017), generic attributes (VB-014), module initializers (VB-012), and partial properties (VB-015).

Also add, from the same era, and not on that list: `Using` without a block (VB-023), null-conditional assignment (VB-024), constant interpolated strings (VB-025), and `ParamArray` of collection types (VB-026).

## How the waves work

| Wave | Meaning | What "done" looks like for an item |
| --- | --- | --- |
| 0 | Prerequisite. No new syntax. | A design note for an opt-in language version. `default` and `latest` stay on 17.13. |
| 1 | First implementation candidates. Bounded compiler work and a clear existing VB form to extend. | Design note, then compiler tests. IDE follow-up is separate. |
| 2 | Interop. The compiler already sees the metadata and rejects it, warns, or ignores it. | A program can declare or honor the construct and emit the same shape C# already emits. |
| 3 | Large designs. Cross-cutting binding, metadata, and IDE work. | A design note only, until a Wave 1 feature has landed. |
| 4 | Visual Basic-shaped proposals from vblang. Not C# clones. | A design note that says whether the idea is in scope. |
| 5 | Modern .NET consumption. Library shapes from .NET 6 onward that VB cannot use, or uses with the wrong behavior. | A design note for any new syntax, then compiler tests against the real library types. |
| Parked | Explicitly not in this plan. | Revisit only if the list is revised. |

Status for every item below is **proposed**. Row order inside a wave is a catalog. The build sequence is [Implementation order](#implementation-order).

## Wave 0

| Id | Feature | Why it is first |
| --- | --- | --- |
| VB-000 | Opt-in experimental language version | Every later feature needs a gate in `LanguageVersion.vb`, `ParserFeature.vb`, `/langversion`, and the public API files. Trace `Feature.UnmanagedConstraint` or `Feature.InitOnlySettersUsage` before adding a value. |

## Wave 1

| Id | Feature | Demand / landmark | Where it would land |
| --- | --- | --- | --- |
| VB-001 | Target-typed `New` | C# 9. VB already binds `New` and `As New`. | `ParseExpression.vb`, object-creation binding, conversions. Reuse `BoundObjectCreationExpression`. |
| VB-002 | Declare init-only properties | Consumption exists at VB 16.9. Prerequisite for anything record-like. | `Symbols/Source`, binding, emit. Metadata must match `init` accessors and `IsExternalInit`. |
| VB-008 | `Enum` constraint | [vblang #306](https://github.com/dotnet/vblang/issues/306). Small constraint-binder change next to the existing `Unmanaged` work. | `Binding/Binder_Constraints.vb`, symbols. |
| VB-009 | `Async Sub Main` | The compiler reports `ERR_AsyncSubMain` today. C# has had async entry points since 7.1. | Entry-point binding in `VisualBasicCompilation.vb`. |
| VB-004 | Pattern matching on `Select Case` and `TypeOf` | [vblang #124](https://github.com/dotnet/vblang/issues/124), [#337](https://github.com/dotnet/vblang/issues/337), [#541](https://github.com/dotnet/vblang/issues/541), [#542](https://github.com/dotnet/vblang/issues/542). `Case Is` and `Case To` already exist. Switch expressions and list, property, and positional patterns are a later slice of this item, not the first one. | `Binder_SelectCase.vb`, case-clause bound nodes, parser. |
| VB-005 | Deconstruction and discards | [vblang #346](https://github.com/dotnet/vblang/issues/346). Tuples exist; taking them apart into locals does not. | Parser, binding, lowering. |
| VB-006 | Ranges and indexes | [vblang #418](https://github.com/dotnet/vblang/issues/418), [#559](https://github.com/dotnet/vblang/issues/559). C# 8 `..` and `^`. | Scanner or parser, binding, well-known members. |
| VB-007 | Target-typed default | [vblang #272](https://github.com/dotnet/vblang/issues/272). C# `default` without a type. | `ParseExpression.vb`, conversions. |
| VB-010 | Nothing-coalescing assignment | [vblang #522](https://github.com/dotnet/vblang/issues/522), [#381](https://github.com/dotnet/vblang/issues/381), [#578](https://github.com/dotnet/vblang/issues/578). VB has `If(x, y)` and `AndAlso`. This is assignment syntax, not nullable reference types. | Parser, `Binder_Expressions.vb`. Keep the design from depending on VB-030. |
| VB-023 | `Using` without a block | C# 8 using declarations. VB already has block `Using`. | `ParseStatement.vb`, lowering. |

These rows are the Wave 1 catalog. Build them in the positions given in [Implementation order](#implementation-order), not from top to bottom of this table.

## Wave 2

These are gaps the current compiler already names. `TypeSymbol.vb` returns fixed "not a record / not a ref-like type / not a readonly structure / not a union" answers. Attributes for module initializers and `SkipLocalsInit` warn with `WRN_AttributeNotSupportedInVB`. Required-member attributes are rejected with `ERR_DoNotUseRequiredMember`. Generic attributes are called out as unsupported. Partial properties and partial events return nothing from the symbol API. `UnmanagedCallersOnly` is rejected in `SourceMethodSymbol.vb`.

| Id | Feature | Notes |
| --- | --- | --- |
| VB-011 | Required members | Author and object-initializer enforcement. Depends on initializer binding. Natural follow-up to VB-002. |
| VB-012 | Module initializers | Allow `<ModuleInitializer>` instead of warning. |
| VB-013 | `SkipLocalsInit` | Allow the attribute instead of warning. |
| VB-014 | Generic attributes | Attributes on generic types are rejected today. |
| VB-015 | Partial properties | C# 13. Symbol API is stubbed. |
| VB-016 | Partial events and constructors | C# 14. Event symbol API is stubbed the same way. |
| VB-017 | Covariant returns | The assembly symbol already knows whether the runtime supports them. The language does not let VB declare them. |
| VB-018 | Readonly structures | `ITypeSymbol.IsReadOnly` is hard-coded false. |
| VB-019 | Ref returns | Narrower than ref structs. Property ref-return surface exists only so the shared API compiles. |
| VB-020 | Native integers | `IsNativeIntegerType` is hard-coded false. `nint` / `nuint` as type names. |
| VB-022 | `UnmanagedCallersOnly` | Niche interop. Small, but easy to get wrong. Do not block Wave 1 on it. |
| VB-024 | Null-conditional assignment | C# 14 `?.` on the left of an assignment. Reading through `?.` already exists. | `Binder_Expressions.vb`, lowering. |
| VB-025 | Constant interpolated strings | C# 10. Interpolation itself shipped in VB 14. | Binding of constant expressions, emit. |

## Wave 3

Do not start these until a Wave 1 feature has gone through parser, binder, tests, and an opt-in language version.

| Id | Feature | Why it waits |
| --- | --- | --- |
| VB-030 | Nullable reference types | Highest discussion on vblang ([#355](https://github.com/dotnet/vblang/issues/355), 94 comments). Every symbol's nullable annotation is hard-coded to `None`. The spelling is locked in `docs/vbnext-vb030-syntax.md`. `?` on a value type remains `Nullable(Of T)`. |
| VB-031 | Records | `IsRecord` is hard-coded false. Covers record classes, record structures, and non-destructive `With` expressions. Needs VB-002, and probably VB-005. VB already has anonymous types and `With` blocks; the design has to say what a record adds. |
| VB-032 | Primary constructors | C# 12. Touches type declarations, parameters, and lowering. |
| VB-033 | Raw string literals | VB 14 already has multiline strings. [vblang #301](https://github.com/dotnet/vblang/issues/301) is about literal parsing. A new literal has to be distinct from the one that exists. |
| VB-034 | UTF-8 string literals | C# 11. Scanner plus emit. Low demand relative to VB-030. |
| VB-035 | Collection expressions | C# 12 unified literals and the spread element. VB already has collection initializers and array literals. [vblang #571](https://github.com/dotnet/vblang/issues/571) is the closer, smaller request. |
| VB-026 | `ParamArray` of collection types | C# 13 params collections. Depends on how far VB-037 has gone if the element type is a `ref` structure. |
| VB-036 | Static abstract interface members | Generic math. Large signature and constraint work. |
| VB-037 | Ref structs | `IsRefLikeType` is hard-coded false. VB cannot declare a `ref` structure. Existing restricted types such as `Span(Of T)` can appear in limited synchronous code and are rejected when lifted. Ranges (VB-006) belong with this work. |
| VB-038 | Authoring default interface members | The runtime-support flag exists. VB interfaces do not implement other interfaces today. |
| VB-039 | Extension members beyond extension methods | C# 14 extensions. VB already has extension methods. Authoring the new block form is a separate design. |

## Wave 4

Visual Basic proposals that are worth a yes/no design note. They are not scheduled as implementations.

| Id | Feature | Issue |
| --- | --- | --- |
| VB-050 | Expression-bodied or compact members | [#61](https://github.com/dotnet/vblang/issues/61), [#403](https://github.com/dotnet/vblang/issues/403), [#567](https://github.com/dotnet/vblang/issues/567) |
| VB-051 | Concise lambdas | [#256](https://github.com/dotnet/vblang/issues/256), [#371](https://github.com/dotnet/vblang/issues/371), [#563](https://github.com/dotnet/vblang/issues/563) |
| VB-052 | GUID literal | [#27](https://github.com/dotnet/vblang/issues/27) |
| VB-053 | `With` that declares its variable | [#438](https://github.com/dotnet/vblang/issues/438) |
| VB-054 | `TypeOf` over several types | [#23](https://github.com/dotnet/vblang/issues/23) |
| VB-055 | `For Each` with index | [#525](https://github.com/dotnet/vblang/issues/525) |
| VB-056 | `For Each ByRef` | [#555](https://github.com/dotnet/vblang/issues/555) |
| VB-057 | Unchecked arithmetic option | [#33](https://github.com/dotnet/vblang/issues/33), [#494](https://github.com/dotnet/vblang/issues/494) |
| VB-058 | Chaining a `Sub` call | [#530](https://github.com/dotnet/vblang/issues/530). `Sub` has no value, so this needs a design, not a port of fluent C#. |
| VB-059 | Optional `End Namespace` | [#532](https://github.com/dotnet/vblang/issues/532) |
| VB-060 | XML literal enhancements | [#397](https://github.com/dotnet/vblang/issues/397). This extends a VB-only feature. |

## Wave 5

Checked against this tree after every wave 0 to 4 item had a slice. Each gap below is in the shape of libraries that VB programs call today. The Visual Basic compiler has no reference to `IAsyncEnumerable`, `IAsyncDisposable`, `System.Threading.Lock`, or interpolated string handlers.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-061 | Asynchronous disposal | Before slice 26b, `Using` called `Dispose` only, so a resource that implements `IAsyncDisposable` could not be released with `Await DisposeAsync()` by the statement. EF Core contexts, streams, and ASP.NET Core services implement it. On `vbnext`, `Async Using` releases it. |
| VB-062 | Asynchronous iteration | Before slice 26c, `For Each` needed `GetEnumerator`, so an `IAsyncEnumerable(Of T)`, which EF Core, gRPC, and `System.Threading.Channels` return, could not be iterated without hand-written `MoveNextAsync` loops. On `vbnext`, `Await Each` iterates it. |
| VB-063 | Async iterators | Before slice 26d, an `Iterator` function could not `Await`, and an `Async` function could not `Yield`, so VB could not author an `IAsyncEnumerable(Of T)`. On `vbnext`, an `Async Iterator Function` does both. Follows VB-062. |
| VB-064 | `SyncLock` on `System.Threading.Lock` | No new syntax. `SyncLock` on a .NET 9 `Lock` object reports BC37329, so VB can lock one only by converting it, which warns with BC42508 and goes through `Monitor`. C# calls `Lock.EnterScope` for the same code. |
| VB-065 | Interpolated string handlers | No new syntax. Before slice 25b, an interpolated string converted to `String`, `FormattableString`, or `IFormattable` only, so a parameter of a handler type, used by .NET logging, `Debug.Assert`, and `StringBuilder.Append` overloads, could not take `$"..."`. On `vbnext` it can. |
| VB-066 | Pattern variables | `docs/vbnext-vb004-syntax.md` left this open: after `Case Is TypeOf Customer`, the case body still sees the original type. A test that names its result, in `Select Case` and in `TypeOf ... Is`, is not in this tree. |
| VB-067 | Property bodies that use the automatic backing field | An auto-implemented property already has a `_Name` field that code can name, but a property with a `Get` or `Set` body has no automatic storage. Validation in `Set` needs a declared field. C# 14 added `field` for this. |

## Parked

- Pointers ([vblang #46](https://github.com/dotnet/vblang/issues/46)). A different safety model from the rest of this list.
- Unions and other in-progress C# 15 designs. `TypeSymbol` already says VB has no unions. Wait until the upstream design stops moving.
- Top-level statements and file-scoped namespaces. Modules and namespace blocks already cover the scenarios. VB-059 is the VB-shaped version of the namespace idea.
- Hosting and runtime portability (Azure Functions, MAUI, a separate IoT runtime). Those are not compiler language features.
- `Option Legacy`. A compatibility mode that can change the meaning of existing programs.

## Implementation order

Difficulty is the size of the compiler change, judged from this tree:

| Rank | Meaning |
| --- | --- |
| S | One area, and an existing feature to copy. |
| M | Parser plus binder, or symbols plus emit. |
| L | A new syntax or bound-node shape, plus lowering. |
| XL | Most of binding and symbols, or a change in the shared compiler core. |

The sequence is that difficulty order, with one adjustment: a feature moves earlier when a later feature cannot be correct without it. Nullable reference types stay last so the features above them are not rewritten once annotations exist. Wave 4 is not in this line.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-000 | Opt-in language version | S | Gates everything else. Design note first. The version value is a separate change, with no new syntax beside it. |
| 2 | VB-008 | `Enum` constraint | S | Copies the existing `Unmanaged` constraint path and proves the gate. |
| 3 | VB-009 | `Async Sub Main` | S | Entry-point binding only. The compiler rejects it today. |
| 4 | VB-012 | Module initializers | S | The attribute is already parsed and warned on. |
| 5 | VB-013 | `SkipLocalsInit` | S | Same attribute change as VB-012. |
| 6 | VB-022 | `UnmanagedCallersOnly` | S | Same attribute change. The calling-convention tests are the risk, so it stays with the other attributes. |
| 7 | VB-001 | Target-typed `New` | M | First new syntax. Reuses object-creation binding. |
| 8 | VB-007 | Target-typed default | S | Uses the target-typing conversions added for VB-001. |
| 9 | VB-010 | Nothing-coalescing assignment | M | Lowers to a conditional assignment that already exists. |
| 10 | VB-023 | `Using` declaration | M | Lowers to block `Using`. |
| 11 | VB-025 | Constant interpolated strings | M | Interpolation already parses. This adds constant folding. |
| 12 | VB-024 | Null-conditional assignment | M | Reading through `?.` exists. The assignment form needs lowering. |
| 13 | VB-002 | Declare init-only properties | M | Unblocks required members and records. |
| 14 | VB-011 | Required members | M | Initializer enforcement. Follows VB-002 because both decide how an object is constructed. |
| 15 | VB-014 | Generic attributes | M | Symbols and metadata, no new statement. |
| 16 | VB-018 | Readonly structures | M | A modifier plus emit. |
| 17 | VB-020 | Native integers | M | Type names and conversions. |
| 18 | VB-006 | Ranges and indexes | M | Closed syntax plus well-known members. `Span` work is not useful until this exists. |
| 19 | VB-017 | Covariant returns | L | Override checking and a covariant signature in metadata. |
| 20 | VB-033 | Raw string literals | M | Scanner-sensitive, and it should land before the other new literal. |
| 21 | VB-034 | UTF-8 string literals | M | Follows the literal work in VB-033. |
| 22 | VB-005 | Deconstruction and discards | L | New assignment shape. Records depend on it. |
| 23 | VB-004 | Pattern matching, first slice | L | Extend `Select Case` only. Switch expressions and list, property, and positional patterns stay in this id and start after that slice. |
| 24 | VB-015 | Partial properties | L | Introduces partial-member machinery. |
| 25 | VB-016 | Partial events and constructors | L | Reuses VB-015. |
| 26 | VB-032 | Primary constructors | L | Declaration shape, capture, and lowering. |
| 27 | VB-035 | Collection expressions | L | Lists and arrays first. Spread into a `Span` waits for VB-037. |
| 28 | VB-019 | Ref returns | L | Comes before any `ref` structure design. |
| 29 | VB-037 | Ref structs | XL | After ranges and ref returns. |
| 30 | VB-026 | `ParamArray` of collection types | L | A `Span` parameter waits for VB-037. A list or `IEnumerable` parameter can move up to sit with VB-035. |
| 31 | VB-031 | Records | XL | After init-only, required members, and deconstruction. Includes record structs and non-destructive `With`. |
| 32 | VB-038 | Author default interface methods | XL | Interface bodies, binding, and emit. |
| 33 | VB-039 | Extension blocks | XL | A new member form beyond extension methods. |
| 34 | VB-036 | Static abstract interface members | XL | Follows the interface work in VB-038. |
| 35 | VB-030 | Nullable reference types | XL | Last. It crosses every bound expression, and `?` already means `Nullable(Of T)`. |

Start at row 1. VB-004's Select Case spelling is locked in `docs/vbnext-vb004-syntax.md`. VB-032's primary constructor spelling is locked in `docs/vbnext-vb032-syntax.md`. Slice 12a is implemented. `TypeOf` lists are locked in `docs/vbnext-vb054-syntax.md`. The GUID literal spelling is locked in `docs/vbnext-vb052-syntax.md`. Slice 13b is implemented. `For Each` with an index is locked in `docs/vbnext-vb055-syntax.md`. Slice 14b is implemented. Optional `End Namespace` is locked in `docs/vbnext-vb059-syntax.md`. Slice 15b is implemented. `docs/vbnext-vb053-syntax.md` records that a `With` variable spelling is not locked. Slice 17b is implemented. `docs/vbnext-vb050-syntax.md` records that a compact member spelling is not locked. The `Unchecked(expression)` spelling is locked in `docs/vbnext-vb057-syntax.md`. Slice 19b is implemented. `docs/vbnext-vb051-syntax.md` records that a concise lambda spelling is not locked. The XML statement block spelling is locked in `docs/vbnext-vb060-syntax.md`, and its implementation waits until XML literals are placed in scope. `docs/vbnext-vb058-syntax.md` records that a `Sub` chain spelling is not locked. Every wave 4 item now has a note.

### Wave 4, if one is pulled in

These stay off the main line. This is only where each would fit.

| Id | Feature | Rank | Fits after |
| --- | --- | --- | --- |
| VB-054 | `TypeOf` over several types | S | VB-009 |
| VB-052 | GUID literal | S | VB-007 |
| VB-055 | `For Each` with index | M | VB-023 |
| VB-059 | Optional `End Namespace` | M | VB-023 |
| VB-053 | `With` that declares its variable | M | VB-023 |
| VB-050 | Compact members | M | VB-002 |
| VB-057 | Unchecked arithmetic | M | VB-020 |
| VB-056 | `For Each ByRef` | M | VB-019 |
| VB-051 | Concise lambdas | L | VB-004 |
| VB-060 | XML literal enhancements | L | VB-001, and only if XML is in scope |
| VB-058 | Chaining a `Sub` call | Design | A written proposal. No implementation slot until the design says what a `Sub` chain returns. |

### Wave 5 order

Interop with no new syntax comes first, then the asynchronous forms in dependency order, then the two designs that change how existing code reads.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-064 | `SyncLock` on `System.Threading.Lock` | S | One lowering change behind the language version. Fixes behavior without syntax. |
| 2 | VB-065 | Interpolated string handlers | L | Binding of the handler pattern and its constructor arguments. No syntax, and the .NET logging and assertion APIs depend on it. |
| 3 | VB-061 | Asynchronous disposal | M | The smallest new statement form. VB-062 releases its enumerator the same way. |
| 4 | VB-062 | Asynchronous iteration | L | Builds on VB-061 for the enumerator. |
| 5 | VB-063 | Async iterators | XL | A state machine that both awaits and yields. Needs VB-062 to be testable from VB. |
| 6 | VB-066 | Pattern variables | L | Extends the `Select Case` patterns from VB-004. Needs a design note first, because the name it declares is visible in the case body. |
| 7 | VB-067 | Property bodies with the automatic backing field | M | Needs a design note first. A contextual keyword can collide with a member already named the same. |
