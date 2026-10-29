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
| VB-066 | Pattern variables | `docs/vbnext-vb004-syntax.md` left this open: after `Case Is TypeOf Customer`, the case body still sees the original type. Before slice 27b, a test that named its result, in `Select Case` and in `TypeOf ... Is`, was not in this tree. On `vbnext`, `Case Is TypeOf Customer Into c` and `If TypeOf value Is Customer Into c Then` declare it, with the spelling from `docs/vbnext-vb066-syntax.md`. |
| VB-067 | Property bodies that use the automatic backing field | An auto-implemented property already has a `_Name` field that code can name, but before slice 28b a property with a `Get` or `Set` body had no automatic storage, so validation in `Set` needed a declared field. C# 14 added `field` for this. On `vbnext`, `Field` in the property body is that storage, with the spelling from `docs/vbnext-vb067-syntax.md`. |

## Wave 6

Checked against this tree after wave 5, by compiling the library shapes that wave 5 code meets next. Each gap below reports an error on `vbnext` today.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-068 | Configured asynchronous streams | Done in phase 30: on `vbnext` both forms bind by pattern. Before that, `Await Each` required `IAsyncEnumerable(Of T)`, so `Await Each n In source.WithCancellation(token)` and `source.ConfigureAwait(False)` report BC37392: `ConfiguredCancelableAsyncEnumerable(Of T)` is a structure with the same members but not the interface. `Async Using` has the same limit for `ConfigureAwait` on `IAsyncDisposable`. C# binds both by pattern. |
| VB-069 | `Await` in `Catch` and `Finally` | Done in phase 31: on `vbnext` both blocks await, and `Await` in a `When` filter or `SyncLock` still reports BC36943. No new syntax. Before that, `Await` there reported BC36943, so cleanup that is itself asynchronous, such as logging, rolling back, or `DisposeAsync`, could not be awaited where it belongs. [vblang #37](https://github.com/dotnet/vblang/issues/37) was approved in principle by the VB language design meeting on 2017-08-09, which decided that `GoTo` from a `Catch` back into its `Try` keeps working. C# has allowed it since C# 6. |
| VB-070 | Task-like `Async` return types | Done in phase 32: on `vbnext` an `Async Function` returns `ValueTask`, `ValueTask(Of T)`, or any type that names its builder with `AsyncMethodBuilderAttribute`, and the attribute on the function chooses its builder. No new syntax. Before that, `Async Function F() As ValueTask(Of Integer)` reported BC36945, so an `IAsyncDisposable.DisposeAsync` or `IAsyncEnumerator(Of T).MoveNextAsync` implementation could not be `Async`. [dotnet/roslyn #72241](https://github.com/dotnet/roslyn/issues/72241) was closed as by design in 2024. C# 7 added `ValueTask` and `AsyncMethodBuilder` types, and C# 10 the method-level builder attribute. |
| VB-071 | Async iterator lambdas | Done in slice 33c: on `vbnext` a multi-line `Async Iterator Function` lambda, locked in `docs/vbnext-vb071-syntax.md`, awaits and yields. `docs/vbnext-vb061-syntax.md` left this open. Before that, a lambda could not be both `Async` and `Iterator`, so an `IAsyncEnumerable(Of T)` argument needed a separate function. |
| VB-072 | A type test that guards the rest of a block | Done in slice 33d: on `vbnext`, `If TypeOf value IsNot T Into name Then Return`, locked in `docs/vbnext-vb072-syntax.md`, declares the name for the rest of the block. `docs/vbnext-vb066-syntax.md` left this open. Before that, `IsNot` with `Into` reported BC37396, and a name declared in an `If` condition was out of scope after `End If`. |

## Wave 7

Checked against this tree after wave 6, by compiling the library shapes that wave 6 code meets next against .NET 11 with `LangVersion` `vbnext`. Each gap below reports an error on `vbnext` today. `Parallel.ForEachAsync` with an async lambda, queries over `IAsyncEnumerable(Of T)`, `Task.WhenEach`, `Yield` in a `Try` with a `Catch` in an async iterator, and `Dictionary.GetAlternateLookup(Of ReadOnlySpan(Of Char))` already compile.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-073 | Reading `ref readonly` returns | Done in phase 35: on `vbnext` a `ref readonly` return reads as a value. No new syntax. Before that, reading an element of a `ReadOnlySpan(Of T)`, as in `span(0)`, reports BC30643, "property is of an unsupported type", because its indexer returns `ref readonly T`. `ReadOnlySpan(Of Char)` from `String.AsSpan` is the most common span in .NET, so VB can pass one along but not look inside it. A `Span(Of T)` element and a `ByRef` return such as `CollectionsMarshal.GetValueRefOrAddDefault` already work. C# 7.2 added `ref readonly` returns. |
| VB-074 | `Using` on a ref structure | Done in phase 36: on `vbnext` `Using` calls a ref structure's own `Dispose`. No new syntax. Before that, `Using gate.EnterScope()` on a .NET 9 `Lock` reported BC36010, because `Lock.Scope` is a ref structure, which cannot implement `IDisposable`, with a `Dispose` method. C# 8 binds `using` to an accessible `Dispose` on a ref structure. Slice 25a covers `SyncLock` on a `Lock`, not other scope types or an explicit `EnterScope`. |
| VB-075 | Ref structure locals in `Async` and `Iterator` methods | Done in phase 37: on `vbnext` such a local is an error only when its value is live across an `Await` or `Yield`. No new syntax. Before that, a `Span(Of T)` or `ReadOnlySpan(Of T)` local in an `Async` or `Iterator` function reported BC37052 even when it is not used across an `Await` or `Yield`, so parsing a buffer before awaiting needs a separate method. C# 13 allows such a local when its value is not live across a suspension point. |
| VB-076 | Attributes on lambda parameters | Done in phase 38: on `vbnext` lambda parameters carry attributes, including `EnumeratorCancellation`. Before that, `<FromQuery> name As String` in a lambda reported BC36634. ASP.NET Core minimal API handlers, written as lambdas, read these attributes, and `docs/vbnext-vb071-syntax.md` left `EnumeratorCancellation` on an `Async Iterator Function` lambda open for the same reason. C# 10 added attributes on lambdas and their parameters. |
| VB-077 | Deconstruction in `For Each` | Done in phase 39: on `vbnext`, `For Each (key, value) In map` and `Await Each (key, value) In source`, locked in `docs/vbnext-vb077-syntax.md`, deconstruct each item from a tuple or a `Deconstruct` method. Before that, `For Each (key, value) In map` treated the tuple as existing variables and reported BC30451, so iterating a dictionary, a list of tuples, or an `IAsyncEnumerable` of records needed a `Dim (key, value) = item` line in the body. C# 7 has `foreach (var (key, value) in map)`. |

## Wave 8

Checked against this tree after wave 7, by compiling and running the library shapes that wave 7 code meets next against .NET 11 with `LangVersion` `vbnext`: generic math, parsing through `IParsable(Of T)`, and writing through spans and `CollectionsMarshal`. Three gaps report an error on `vbnext` today, and two compile but do the wrong thing. `Func(Of ReadOnlySpan(Of Char), Integer)` with a lambda, a delegate with a span parameter, `Select Case` on a `ReadOnlySpan(Of Char)` with string cases, `MemoryMarshal.CreateReadOnlySpan` with a `ref readonly` parameter, a `ByRef Structure` that holds a span field, `CollectionsMarshal.GetValueRefOrAddDefault(...) += 1`, and `ArgumentNullException.ThrowIfNull` with its caller argument name already work.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-078 | Running types that implement shared abstract members | Done in phase 41: on `vbnext` such a type loads and runs on .NET. No new syntax. Before that, a type that supplied a `Shared MustOverride` member with `Public Shared Function ... Implements`, the form slice 9f added, or that implemented `IParsable(Of T)` from .NET, compiled, but loading it threw `TypeLoadException`, "Virtual Static Method", because the implementing method was emitted as a virtual shared method. Slice 9f was checked on .NET Framework, which cannot run such a type. |
| VB-079 | Shared abstract properties and shared virtual members through a type parameter | Done in phase 42: on `vbnext` they are reached through the type parameter, from any assembly. No new syntax. Before that, slice 9g called a `Shared MustOverride` function through `T`, but `T.Zero` on `INumber(Of T)`, a shared abstract property, and `T.CreateChecked(2)`, a generic shared virtual function with a default body, reported BC37314, "A shared abstract or virtual interface member cannot be accessed". Generic math in .NET 7 and later is built from these members. C# 11 added them. |
| VB-080 | Operators through a type parameter | Done in phase 43: on `vbnext` operators, compound assignment, and checked operators bind through the constraints. No new syntax. Before that, `a + b` and `a > b`, where `T` is constrained to `INumber(Of T)` or `IComparisonOperators(Of T, T, Boolean)`, reported BC30452, because VB looked for operators only on the type itself, not on the shared abstract operators of its constraints. A generic `Sum`, `Average`, or `Max` over numbers could not be written without them. C# 11 binds such operators through the constraint. |
| VB-081 | Writing through a `ByRef` return on a value | Done in phase 44: on `vbnext` such an assignment writes through the reference. No new syntax. Before that, `CollectionsMarshal.AsSpan(list)(0) = 5` and `span.Slice(1)(0) = 7` reported BC30068, "Expression is a value and therefore cannot be the target of an assignment", because the span is a value, although its indexer returns a reference to the element. The same assignment through a span local, and slice 7e's `First(items) = 9`, already work. C# 7 assigns through any `ref` return. |
| VB-082 | `ByRef` locals | Done in phase 45: on `vbnext`, `Dim ByRef` and `Dim ByRef ReadOnly`, locked in `docs/vbnext-vb082-syntax.md`, declare locals that refer to variables. Before that, `Dim ByRef slot As Integer = CollectionsMarshal.GetValueRefOrAddDefault(counts, key, exists)` compiled on `vbnext` with no diagnostic, because `ByRef` was accepted as a declaration modifier and then ignored, so `slot` was a copy and `slot += 1` did not change the dictionary. A reference that several statements use, such as a dictionary slot that is read and then updated, needs a call per use. C# 7 has `ref` locals, and C# 7.3 lets them be reassigned. |
| VB-083 | `Object` members on a type parameter with interface constraints | Done in slice 42c: on `vbnext` the `Object` methods of a name overload the constraint's methods of that name. No new syntax. Found in slice 42a. Before that, `value.ToString()` on a `T As INumber(Of T)`, or on any `T As IFormattable`, reported BC30455 for the `format` and `formatProvider` parameters, because lookup found `IFormattable.ToString(format, formatProvider)` on the constraint and stopped before `Object.ToString()`. Every .NET numeric interface includes `IFormattable`, so generic math code meets this on its first `ToString`. C# adds the `Object` members to the constraint's members. |

## Wave 9

Checked against this tree after wave 8, by compiling and running the shapes that wave 8 code meets next against .NET 11 with `LangVersion` `vbnext`: VB types that take part in generic math, and the storage that span code builds on. Each gap below reports an error on `vbnext` today. `ParamArray values As ReadOnlySpan(Of Integer)` already works. Declaring a `Shared Overridable` member with a body in a VB interface, and `AddressOf T.Create` through a type parameter, which reports BC37314, are not taken into this wave.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-084 | Shared virtual members with a default body | Done in phase 47: on `vbnext` only shared abstract members must be supplied. No new syntax. Before that, a structure that implemented `IAdditionOperators(Of Money, Money, Money)` reported BC37315 for `op_CheckedAddition`, which the interface declares as a shared virtual member with a default body, so a type does not have to supply it. VB treats every shared abstract or virtual interface member as one the type must implement. C# 11 requires only the abstract ones. |
| VB-085 | Implementing shared abstract properties | Done in phase 48: on `vbnext` a `Shared` property implements a shared abstract property. No new syntax. Before that, `Public Shared ReadOnly Property AdditiveIdentity As Meters Implements IAdditiveIdentity(Of Meters, Meters).AdditiveIdentity` reported BC30505, "Methods or events that implement interface members cannot be declared 'Shared'", and BC37315. Slice 9f lets a `Shared` function implement a shared abstract function, but not a property, and generic math interfaces such as `INumberBase(Of T)` declare `Zero`, `One`, and `AdditiveIdentity` as shared abstract properties. |
| VB-086 | Implementing generic math operators | Done in phase 49: on `vbnext`, locked in `docs/vbnext-vb086-syntax.md`, an operator supplies the matching interface operators by signature. Before that, `Public Shared Operator +(left As Money, right As Money) As Money` in a structure that implemented `IAdditionOperators(Of Money, Money, Money)` reported BC37315 for the interface's `+`, because a VB operator has no `Implements` clause and an operator was not matched to an interface operator by signature. A VB type cannot be used with generic math until its operators can implement the interface's. C# 11 implements such operators implicitly or explicitly. |
| VB-087 | Ref fields | Done in phase 50: on `vbnext`, locked in `docs/vbnext-vb087-syntax.md`, a `ByRef Structure` declares `ByRef` and `ByRef ReadOnly` fields, sets them with `field = ByRef reference` in `Sub New`, and cannot outlive what they refer to. Ref fields from other assemblies are still unsupported. Before that, `Private ByRef _value As Integer` in a `ByRef Structure` reported BC30235. A ref structure that holds a reference to one variable, as `Span(Of T)` does internally, needs a ref field. C# 11 added `ref` fields to `ref struct` types. |
| VB-088 | Inline arrays | Done in phase 51: on `vbnext` an inline array variable is indexed with `buffer(i)`, converts to `Span(Of T)` and `ReadOnlySpan(Of T)`, and is iterated by `For Each`. No new syntax. Before that, `buffer(0)` on a structure marked `<InlineArray(4)>` reports BC30690, "Structure cannot be indexed because it has no default property". .NET 8 added `InlineArrayAttribute` for fixed-size buffers that convert to spans, and C# 12 indexes, slices, and iterates them. |

## Wave 10

Checked against this tree after wave 9, by compiling and running the shapes that wave 9 code meets next against .NET 11 with `LangVersion` `vbnext`: generic math through delegates, inline arrays that are not variables, and the lifetime of ref field structures. Several shapes already work: slicing a span or an inline array with a range, as in `b(1..3)`; `OverloadResolutionPriorityAttribute`; a lambda typed `Func(Of Window, Integer)` over a `ByRef Structure`; a collection expression assigned to `ReadOnlySpan(Of Integer)`; a generic inline array; `Partial` properties; and `Public Shared Function Increment(value As Ticks) As Ticks Implements IIncrementOperators(Of Ticks).op_Increment`, which supplies an operator that VB cannot declare.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-089 | `AddressOf` through a type parameter | Done in phase 53: on `vbnext` the delegate is created with `constrained. ldftn`. Before that, `Return AddressOf T.Parse` for `T As IParsable(Of T)`, typed `Func(Of String, IFormatProvider, T)`, reports BC37314, "A shared abstract or virtual interface member cannot be accessed". Slice 42a calls such members through a type parameter, but a delegate cannot be made from one. C# 11 creates the delegate with `constrained. ldftn`. No new syntax. |
| VB-090 | Inline array values | Done in phase 54: on `vbnext` the elements of a value are read through a span over a temporary. Before that, `Make()(0)` and `For Each v In Make()`, where `Make` returns an inline array, report BC37416, because slice 51a builds spans only over variables. C# 12 reads the elements of such a value by keeping it in a temporary for as long as the expression needs it. No new syntax. |
| VB-091 | Ref fields from other assemblies | Done in phase 55: on `vbnext` they read, write, and pass `ByRef` as VB ref fields do. Before that, a `ref` or `ref readonly` field declared in C#, such as `public ref int Value;` in a `ref struct`, reports a use-site error in VB, because phase 50 reads only ref fields that VB declares. No new syntax. |
| VB-092 | `ByRef` locals over ref field structures | Done in phase 56: on `vbnext`, locked in `docs/vbnext-vb092-syntax.md`, a write through a `ByRef` local follows its referent. Before that, `Dim ByRef r = c`, where `c` is a `ByRef` parameter of a structure that holds a ref field, followed by `r = New Cell(local)`, is not checked, and lets the structure outlive `local`. The rules in `docs/vbnext-vb087-syntax.md` cover `ByRef` parameters and `Me`, not `ByRef` locals. Needs a design note first. |
| VB-093 | Returning ref field structures | Done in phase 57: on `vbnext`, locked in `docs/vbnext-vb093-syntax.md`, a function returns such a structure when it refers only to the caller's storage. Before that, a function or property could not return a structure that holds a ref field (BC37414 since slice 50d), so a factory such as `Function Wrap(ByRef value As Integer) As Cell`, or a slicing method on a `ByRef Structure`, cannot be written. C# 11 allows it with escape analysis and `scoped` parameters. Needs a design note first. |
| VB-094 | Returning spans over inline array locals | Done in phase 57: on `vbnext` a span over local storage reports BC37418 when it would leave the method. Found in slice 54a. `Function F() As Span(Of Integer)` can `Return b`, where `b` is a local inline array, because slice 51a converts any writable variable to a span and VB does not check where a span goes. The caller gets a span over storage that no longer exists. C# 12 reports that the span cannot escape the method. Joins the VB-093 design note in phase 57, because both decide what a returned ref structure may refer to. |

## Wave 11

Checked against this tree after wave 10, by compiling and running the shapes that wave 10 code meets next against .NET 11 with `LangVersion` `vbnext`: declaring the reference and generic math forms that VB so far only consumes. Several shapes already work: a generic method whose type parameter is `Of T As ByRef`, which C# 13 spells `allows ref struct`; a `ByRef Structure` that implements an interface; calling `MemoryMarshal.CreateReadOnlySpan`, whose first parameter is `ref readonly`; `ParamArray values As List(Of Integer)`; an inline array of a generic element type; `Partial` constructors; and `Shared MustOverride` members declared in a VB interface.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-095 | `NameOf` an unbound generic type | Done in phase 59: on `vbnext` it gives the type's name. Before that, `NameOf(List(Of))` reported BC30182, "Type expected". C# 14 accepts `nameof(List<>)`. VB already writes an unbound generic type as `List(Of)` in `GetType(List(Of))`. No new syntax. |
| VB-096 | Shared virtual interface members with a body | Done for functions and subs in phase 60: on `vbnext`, locked in `docs/vbnext-vb096-syntax.md`, `Shared Overridable` declares them, and properties since phase 68. Before that, `Shared Overridable Function DefaultName() As String` with a body in a VB interface reports BC30270 and BC30501. Slice 47a lets a type leave out such a member that another assembly declares, but VB cannot declare one. C# 11 declares them as `static virtual`. Needs a design note first, because it combines `Shared` and `Overridable`. |
| VB-097 | Declaring `ByRef ReadOnly` functions | Done in phase 61: on `vbnext`, locked in `docs/vbnext-vb097-syntax.md`, `ByRef ReadOnly Function` returns a `ref readonly` reference; `ref readonly` properties remain. Before that, `ByRef ReadOnly Function FirstValue() As Integer` reported BC30242, "'ReadOnly' is not valid on a method declaration". Slice 35a reads `ref readonly` results from other assemblies, but VB cannot declare one. C# 7.2 declares `ref readonly` returns. Needs a design note first. |
| VB-098 | Declaring `ByRef ReadOnly` parameters | Done in phase 62: on `vbnext`, locked in `docs/vbnext-vb098-syntax.md`, they are emitted as C# `in` parameters. Before that, `Function Twice(ByRef ReadOnly value As Integer)` reported BC30181. VB can pass arguments to `in` and `ref readonly` parameters, but cannot declare one. C# 7.2 declares `in` parameters and C# 12 `ref readonly` parameters. Needs a design note first. |

## Wave 12

Checked against this tree after wave 11, by compiling and running the shapes that wave 11 code meets next against .NET 11 with `LangVersion` `vbnext`: the wave 11 forms across assemblies and on more kinds of members. Several shapes already work: `ReadOnly Structure`, emitted with `IsReadOnlyAttribute`; a `ByRef ReadOnly Function` declared in an interface and implemented by a class, or declared `MustOverride` and overridden; an `Optional ByRef ReadOnly` parameter; an extension method whose receiver is `ByRef ReadOnly`; and a C# `ref readonly` property, read directly or through `Dim ByRef ReadOnly`.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-099 | `in` parameters of virtual and interface members from other assemblies | Done in phase 64: on `vbnext` they are called, overridden, and implemented with `ByRef ReadOnly`, and report `RefKind.In`. Before that, `s.Score(x)`, where C# declares `public abstract int Score(in int value)`, reports BC30657, "'Score' has a return type that is not supported or parameter types that are not supported", and so do overriding it and implementing an interface method that takes `in`. C# puts the `InAttribute` required modifier on the `in` parameters of virtual, interface, and delegate members, and VB rejects members with required modifiers it does not know. The same holds for the VB interface members that slice 62b emits, once another assembly reads them. Such parameters, and `in` parameters from metadata in general, also report `RefKind.Ref` instead of `In`. No new syntax. |
| VB-100 | Lambdas for `ByRef ReadOnly` delegates | Done in phase 66: on `vbnext` a lambda parameter takes `ByRef ReadOnly` from the delegate. Before that, `Dim m As Measure = Function(v) v * 10`, where `Delegate Function Measure(ByRef ReadOnly value As Integer) As Integer`, reports BC36532, "Nested function does not have the same signature as delegate". Slice 62b declares such delegates, but no lambda converts to one, because a lambda parameter cannot be `ByRef ReadOnly` (BC37419). C# infers `in` for an untyped lambda parameter from the delegate. No new syntax. |
| VB-101 | `NameOf` a qualified unbound generic type | Done in phase 65: on `vbnext` it gives the type's name; members of unbound types, such as `NameOf(List(Of).Count)`, remain. Before that, `NameOf(System.Collections.Generic.List(Of))` reported BC37244, "This expression does not have a name". Slice 59a covers only an unqualified name such as `NameOf(List(Of))`. C# 14 accepts `nameof(System.Collections.Generic.List<>)`. No new syntax. |
| VB-102 | Shared properties in interfaces | Done in phase 68: on `vbnext` an interface declares `Shared MustOverride` and `Shared Overridable` properties. Before that, `Shared MustOverride ReadOnly Property Zero As T` in a VB interface reports BC30273, "'Shared' is not valid on an interface property declaration", and `Shared Overridable ReadOnly Property Label As String` with a `Get` body reports BC30603. VB interfaces declare shared abstract and shared virtual functions and subs, but not properties, so a VB interface cannot declare a member such as `INumberBase(Of T).Zero`. `docs/vbnext-vb096-syntax.md` already locks `Shared Overridable Property`. No new syntax. |
| VB-103 | `ByRef ReadOnly` operator operands | Done in phase 67: on `vbnext` operators other than conversions, `IsTrue`, and `IsFalse` take them, and C# `in` operators are operators; phase 74 adds the rest. Before that, `Shared Operator +(ByRef ReadOnly a As Vec, ByRef ReadOnly b As Vec) As Vec` reports BC30651, "operator parameters cannot be declared 'ByRef'", and BC37419, whose message lists operators among the places `ReadOnly` is valid. C# allows `in` on operator parameters, so large structures can pass their operands without a copy. Extends the `docs/vbnext-vb098-syntax.md` form to operators. |
| VB-104 | Operators in interfaces | Done in phase 69: on `vbnext`, locked in `docs/vbnext-vb104-syntax.md`, `Shared MustOverride Operator` and `Shared Overridable Operator` declare them; conversion operators remain. Before that, `Shared MustOverride Operator +(left As T, right As T) As T` in a VB interface reports BC30603, "Statement cannot appear within an interface body". Phase 49 implements interface operators that another assembly declares, but VB cannot declare a generic math interface of its own. C# 11 declares `static abstract` and `static virtual` operators. Needs a design note first. |
| VB-105 | Read-only structure members | Done in phase 70: on `vbnext`, locked in `docs/vbnext-vb105-syntax.md`, `ReadOnly Function`, `ReadOnly Sub`, and `ReadOnly Get` declare them. Before that, `ReadOnly Function Length() As Integer` in a `Structure` reports BC30242, "'ReadOnly' is not valid on a method declaration". C# 8 marks a member `readonly` so that it cannot change the structure and is called on a `ReadOnly` field without a copy. `ReadOnly Property` already means a property without a setter, so the spelling needs a design note first. |

## Wave 13

Checked against this tree after wave 12, by compiling and running the shapes that wave 12 code meets next against .NET 11 with `LangVersion` `vbnext`, with a C# library alongside: interfaces as C# 8 and later fill them in, and the C# 11 to 14 forms that VB code calls. Several shapes already work: a null-conditional assignment such as `p?.Name = "set"`, and a structure that supplies `IMinMaxValue(Of T)`, `IIncrementOperators(Of T)`, and `IComparisonOperators(Of T, T, Boolean)` and is clamped through a type parameter.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-106 | Calling C# `params` collections | Done in phase 72: on `vbnext`, for the span, `List(Of T)`, and `IEnumerable(Of T)` collections; other collection types remain. Before that, `Spans.Sum(1, 2, 3)`, where C# 13 declares `Sum(params ReadOnlySpan<int> values)`, reports BC30311 and BC30057, because VB expands only `ParamArray` arrays from metadata. .NET 9 and later add `params ReadOnlySpan(Of T)` overloads across the libraries. No new syntax. |
| VB-107 | C# 14 extension members | Done in phase 78: on `vbnext` for extension blocks that are not generic; generic blocks and extension indexers remain. Before that, `"one two three".WordCount`, where C# 14 declares `extension(string s) { public int WordCount => ...; }`, reports BC30456, "'WordCount' is not a member of 'String'". VB reads the extension methods of such blocks, but not extension properties or static extension members. No new syntax. |
| VB-108 | Generic attributes | Done in phase 77: on `vbnext` the short name finds the generic attribute. Before that, `<Tag(Of Integer)>`, where C# 11 declares `TagAttribute<T>`, reported BC30002, "Type 'Tag' is not defined", and VB cannot declare a generic attribute class of its own. C# 11 declares and applies generic attributes. No new syntax. |
| VB-109 | Instance interface properties with a body | Done in phase 75: on `vbnext` they are default interface properties. Before that, `ReadOnly Property Greeting As String` with a `Get` body in an interface reported BC30603. VB declares instance interface methods with a default body, and phase 68 shared properties with one, but not instance properties. C# 8 declares default interface properties. No new syntax. |
| VB-110 | Shared interface members that are not virtual | Done in phase 76: on `vbnext` they are static interface members. Before that, `Shared Function Describe() As String` with a body in an interface reported BC30270. `docs/vbnext-vb096-syntax.md` keeps that form for a shared member that is not virtual, which C# 8 declares as a static interface member, but leaves it unimplemented. No new syntax. |
| VB-111 | Conversion operators in interfaces | Done in phase 79: on `vbnext` they declare static abstract and virtual conversions, and conversions through a type parameter use them; expression trees report BC36534. Before that, `Shared MustOverride Widening Operator CType(value As Integer) As T` in an interface reported BC33013. Phase 69 leaves conversions out, because a conversion through a type parameter needs the type parameter's constraints to be searched for conversion operators. C# 11 declares static abstract conversion operators. Needs a design note first. |
| VB-112 | `ByRef ReadOnly` conversions and lifted operators | Done in phase 74: on `vbnext` every operator takes `ByRef ReadOnly` operands and such operators lift. Before that, `Widening Operator CType(ByRef ReadOnly value As Big) As Long` reported BC30651, and `a + b` over `Vec?` operands with a `ByRef ReadOnly` operator reports BC30512 under `Option Strict On`, because phase 67 leaves conversions, `IsTrue`, `IsFalse`, and lifting out. C# allows `in` on all operators and lifts them. No new syntax. |
| VB-113 | `NameOf` members of unbound generic types | Done in phase 73: on `vbnext` they give the member's name. Before that, `NameOf(System.Collections.Generic.List(Of).Count)` reported BC31424, a misleading forwarded-type error, in a project, where slice 65a's test reports BC30002. C# 14 accepts `nameof(List<>.Count)`. No new syntax. |

## Wave 14

Checked against this tree after wave 13, by compiling and running the shapes that wave 13 code meets next against .NET 11 with `LangVersion` `vbnext`, with a C# library alongside: the `params`, extension, and operator forms that wave 13 left open, and the C# 13 and 14 forms around them. Several shapes already work: `OverloadResolutionPriorityAttribute` choosing between C# overloads, `SyncLock` on a `System.Threading.Lock`, which calls `Lock.Enter`, a `Span(Of Integer)` passed to a C# method whose type parameter `allows ref struct`, a `ByRef Structure` that implements `IDisposable` in a `Using` block, and a VB generic attribute class applied by its short name.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-114 | Other `params` collection types | Done in phase 81: on `vbnext` for collection builders, classes and structures with `Add`, and the interfaces `List(Of T)` implements. Before that, `Bags.CountImm(1, 2, 3)`, where C# 13 declares `CountImm(params ImmutableArray<int> values)`, reported BC30311 and BC30057, as do `params HashSet<int>` and `params ICollection<int>`, because phase 72 covers only spans, `List(Of T)`, and `IEnumerable(Of T)`. VB's own `ParamArray values As HashSet(Of Integer)` reports BC30050. No new syntax. |
| VB-115 | C# 14 compound assignment operators | Done in phase 82: on `vbnext` for `+=`, `-=`, `*=`, `/=`, `<<=`, and `>>=`, including the `checked` forms. Before that, `c += 5`, where C# 14 declares `public void operator +=(int amount)` on `Counter` and no `operator +`, reported BC30452, "Operator '+' is not defined for types 'Counter' and 'Integer'", because VB rewrites `+=` as `c = c + 5`. No new syntax. |
| VB-116 | C# 14 extension operators | Done in phase 83: on `vbnext` for unary and binary operators of extension blocks that are not generic. Before that, `a + b` over `Vec` operands, where C# 14 declares `operator +` in `extension(Vec)`, reported BC30452, because VB looks for operators only in the operand types. No new syntax. |
| VB-117 | Generic C# 14 extension blocks | Done in phase 84: on `vbnext` for properties and static methods of generic blocks, inferring the block's type arguments from the receiver; generic members and operators of generic blocks remain. Before that, `numbers.Tail` and `List(Of Integer).Empty()`, where C# 14 declares them in `extension<T>(List<T> list)`, reported BC30456, because phase 78 reads only blocks that are not generic. No new syntax. |
| VB-118 | Ref structures for interface constraints | Done in phase 85: on `vbnext` a ref structure meets an interface constraint by implementing it. Before that, `Generic.Use(New Wrapper())`, where C# 13 declares `Use<T>(T value) where T : IDisposable, allows ref struct` and `Wrapper` is a `ByRef Structure` that implements `IDisposable`, reported BC32044, because VB checks the constraint as a boxing conversion. No new syntax. |
| VB-119 | `allows ref struct` type parameters | Done before wave 14 by slice 7m: `Function Echo(Of T As ByRef)(value As T) As T` takes a `Span(Of Integer)`, and phase 85 lets `{IDisposable, ByRef}` take a `ByRef Structure` that implements the interface. The probe declared `Function Echo(Of T)` without the `ByRef` constraint, where `Echo(span)` reports BC37376 as designed. No new syntax. |

## Wave 15

Checked against this tree after wave 14, by compiling and running the shapes that wave 14 code meets next against .NET 11 with `LangVersion` `vbnext`, with a C# library alongside: the interface members that C# 8 completes, the C# 12 lambda and alias forms, and the generic extension members that wave 14 left open. Several shapes already work: `CallerArgumentExpressionAttribute`, as `ArgumentNullException.ThrowIfNull(value)` reports `value`, generic methods of generic C# 14 extension blocks, which C# also emits as classic extension methods, and `With` on a `Record Structure`.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-120 | Interface member accessibility | Done in phase 88: on `vbnext` for methods and properties with a body. Before that, `Private Function Secret() As Integer` with a body in an interface reported BC30270, "'Private' is not valid on an interface method declaration", as do `Friend` and `Protected`. C# 8 declares private, internal, and protected interface members with a body. No new syntax. |
| VB-121 | Shared interface fields and constructors | Done in phase 89: on `vbnext` for `Shared` fields, constants, and `Shared Sub New`. Before that, `Shared Counter As Integer` in an interface reported BC30602, and `Shared Sub New()` reported BC30363, "'Sub New' cannot be declared in an interface". C# 8 declares static fields and static constructors in interfaces. No new syntax. |
| VB-122 | Interface custom events | Done in phase 90: on `vbnext` the `AddHandler` and `RemoveHandler` bodies are the default implementation. Before that, `Custom Event Changed As EventHandler` with accessor bodies in an interface reported BC31121, "'Custom' modifier is not valid on events declared in interfaces". C# 8 declares default event accessors in interfaces. No new syntax. |
| VB-123 | Lambda optional and ParamArray parameters | Done in phase 91: on `vbnext` through the anonymous delegate VB infers. Before that, `Function(Optional x As Integer = 3) x` reported BC33010, and `Function(ParamArray xs() As Integer) xs.Length` reports BC33009. C# 12 allows default values and `params` in lambdas. No new syntax. |
| VB-124 | Generic extension operators | Done in phase 92: on `vbnext`, inferring the block's type arguments from the operands. Before that, `a + b` over `Box(Of Integer)` operands, where C# 14 declares `operator +` in `extension<T>(Box<T>)`, reported BC30452, because phase 84 leaves the operators of generic blocks out. No new syntax. |
| VB-125 | Tuple type aliases | Done in phase 93: on `vbnext` through a new `TypeAliasImportsClauseSyntax` node, which also takes a nullable tuple. Before that, `Imports Pair = (Left As Integer, Right As Integer)` reported BC30203, "Identifier expected", because an `Imports` alias names only a namespace or a named type. C# 12 aliases any type. No new keyword. |

## Wave 16

Checked against this tree after wave 15, by compiling and running the shapes that wave 15 code meets next against .NET 11 with `LangVersion` `vbnext`, with a C# library alongside. Several shapes already work: `Async Function Main`, the `ModuleInitializer` attribute, covariant return types on `Overrides`, `OverloadResolutionPriority`, interpolated string handlers, `SyncLock` on `System.Threading.Lock` through `EnterScope`, `UnscopedRef` members, `Partial Property`, `b?.Name = value`, and an array literal converted to `ReadOnlySpan(Of T)`.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-126 | Aliases for predefined and array types | Done in phase 95: on `vbnext` for predefined, array, and nullable types. Before that, `Imports Num = Integer` and `Imports Ints = Integer()` reported BC30183, "Keyword is not valid as an identifier", because phase 93 aliased only types that start with `(`. C# 12 aliases any type. No new syntax: the alias uses phase 93's `TypeAliasImportsClauseSyntax`. |
| VB-127 | Tuple equality | Done in phase 96: on `vbnext` as `AndAlso` of the element comparisons, or `OrElse` for `<>`. Before that, `(1, 2) = (1, 2)` reported BC30452, "Operator '=' is not defined for types '(Integer, Integer)' and '(Integer, Integer)'". C# 7.3 compares tuples element by element with `==` and `!=`. No new syntax. |
| VB-128 | `With` on structures | Done in phase 97: on `vbnext` by copying the value, including tuples. Before that, `p With {.Y = 5}` on a `Structure` that is not a record reported BC37377, "'Point' is not a record, so it cannot be copied with 'With'". C# 10 copies any structure with `with`. No new syntax. |
| VB-129 | `With` on anonymous types | Done in phase 98: on `vbnext` as a new value of the same anonymous type. Before that, `a With {.Y = 5}` on `New With {.X = 1, .Y = 2}` reported BC37377. C# 10 copies anonymous types with `with`. No new syntax. |
| VB-130 | Null-conditional compound assignment | Done in phase 99: on `vbnext` for the compound operators, including chains of `?.`. Before that, `b?.Count += 1` reported BC30068, "Expression is a value and therefore cannot be the target of an assignment", though `b?.Name = value` already assigns. C# 14 allows compound assignment through `?.`. No new syntax. |
| VB-131 | Array literals for collection types | Done in phase 100: on `vbnext` wherever the target type is known, and as method arguments since phase 106. Before that, `Dim list As List(Of Integer) = {1, 2, 3}` reported BC30311, as do `HashSet(Of Integer)` and `ImmutableArray(Of Integer)`, though `ReadOnlySpan(Of Integer)` converts. C# 12 collection expressions build any collection type that has a collection initializer or a `CollectionBuilder`. No new syntax. |

## Wave 17

Checked against this tree after wave 16, by compiling the shapes that wave 16 code meets next against .NET 11 with `LangVersion` `vbnext`, with a C# library alongside. The parenthesized spread `(..a, 3)` already builds a `List(Of Integer)`.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-132 | Collection literals as arguments | Done in phase 106: on `vbnext`, when no overload applies otherwise and exactly one parameter type can build the literal. Before that, `Use({1, 2})` and `Use((1, 2))`, where `Use` takes a `List(Of Integer)`, reported BC30311, because overload resolution does not consider the collections that phase 100 and the parenthesized form build. C# 12 passes collection expressions to any collection parameter. No new syntax. |
| VB-133 | Spreads in array literals | Done in phase 102: on `vbnext` for the collection types phase 100 builds and for one-dimensional arrays; since phase 108 a type without `AddRange` may instead have a constructor that takes an `IEnumerable(Of T)`. Before that, `Dim b As List(Of Integer) = {..a, 3}` reported BC37359, "A range can only be used to slice a one-dimensional array", though `(..a, 3)` spreads `a`. C# 12 spreads with `..` in collection expressions. No new syntax. |
| VB-134 | Null-conditional nothing-coalescing assignment | Done in phase 104: on `vbnext`, including chains of `?.`. Before that, `box?.Name ?= "x"` reported BC30068, because phase 99 leaves `?=` out of the assignments through `?.`. C# 14 allows `box?.Name ??= "x"`. No new syntax. |
| VB-135 | Nullable tuple equality | Done in phase 103: on `vbnext` with VB's lifted semantics, a `Boolean?` that is `Nothing` when either side is `Nothing`. Before that, `t = (1, 2)`, where `t` is `(Integer, Integer)?`, reported BC30452, because phase 96 compares only tuples that are not nullable. C# lifts tuple `==` and `!=` to nullable tuples. No new syntax. |
| VB-136 | Span receivers for extension methods | Done in phase 105: on `vbnext` for arrays, spans, and strings, when no other extension of that name applies. Before that, `a.CountOf(2)`, where `a` is an `Integer()` and C# declares `CountOf<T>(this ReadOnlySpan<T> span, T value)`, reported BC30456, because an extension method's receiver does not convert from an array to a span. C# 14 first-class spans call such extensions on arrays. No new syntax. |

## Wave 18

Checked against this tree after wave 17, by compiling the collection literal shapes that wave 17 code meets next against .NET 11 with `LangVersion` `vbnext`. Several shapes already work: `Dim d As Dictionary(Of String, Integer) = {}`, a literal for a property in `New Holder With {.Items = {1, 2}}`, and a literal returned from a lambda converted to `Func(Of List(Of Integer))`. `Dim e As List(Of Integer) = Echo({1, 2})`, where `Echo(Of T)` returns its argument, reports BC30311, as C# rejects `Echo([1, 2])` without a natural type, so it is not a gap.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-137 | Spreads into collections without `AddRange` | Done in phase 108: on `vbnext` through a constructor that takes an `IEnumerable(Of T)`. Before that, `Dim s As HashSet(Of Integer) = {..a, 3}` reported BC30456, "'AddRange' is not a member of 'HashSet(Of Integer)'", because phases 100 and 102 spread with `AddRange`. C# 12 adds each spread element. No new syntax. |
| VB-138 | Type inference from collection literal arguments | Done in phase 111: on `vbnext` from the literal's element type, for collection parameters whose one type argument is `T`. Before that, `Generic({1, 2})`, where `Generic(Of T)` takes a `List(Of T)`, reported BC36645, because phase 106 skips parameters whose type uses the method's type parameters. C# 12 infers `T` from the elements. No new syntax. |
| VB-139 | Collection literals as named arguments | Done in phase 109: on `vbnext`, by the parameter the name selects. Before that, `Named(1, values:={2, 3})` reported BC30311, because phase 106 rebuilds only positional arguments. No new syntax. |
| VB-140 | Collection literals in `If` | Done in phase 110: on `vbnext` from the target type or the other branch's collection type. Before that, `Dim c As List(Of Integer) = If(flag, {1}, {2, 3})` reported BC30311, because the branches are array literals that the `If` gives an array type. C# 12 target-types `flag ? [1] : [2, 3]`. No new syntax. |

## Wave 19

Checked against this tree after wave 18, by compiling and running against .NET 11 with `LangVersion` `vbnext` the shapes that wave 18 code meets next, beyond collection literals: generic math, records that inherit records, delegates, spans, interpolated string handlers, and deconstruction. Several shapes already work: `Sum(Of T As INumber(Of T))` with `T.Zero` and `+=`, `For Each` over a `ReadOnlySpan(Of Integer)`, `StringBuilder.Append($"x{1}y")`, `Count({1, 2})` for an `IEnumerable(Of T)` parameter, `Dim (number, word) = Pair()`, and `ToString` and `With` on a derived record, which keep the runtime type.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-141 | Equality of records that inherit records | Done in phase 115: on `vbnext`, through an overridable `EqualityContract` and an overridable typed `Equals`, as C# 9 declares them. Before that, `New Animal("Rex").Equals(dog)`, where `Dog(Name, Breed)` inherits `Animal(Name)`, returns `True` both ways, because the synthesized `Equals` casts to its own type, without the runtime type check that C# 9 makes through `EqualityContract`. Since phase 113 a derived record's `Equals`, `GetHashCode`, and `ToString` include the base record's members; before that they skipped them. No new syntax. |
| VB-142 | Positional parameters of inherited properties | Done in phase 113: on `vbnext`, a positional parameter reuses an accessible inherited property of the same name and type. Before that, `Record Class Dog(Name As String, Breed As String)` that `Inherits Animal(Name)` declares a second `Name` property and its own `Deconstruct`, with two BC40003 warnings, and the two `Name` properties drift apart: `a With {.Name = "Max"}` through an `Animal` reference leaves `CType(copy, Dog).Name` as `"Rex"`. C# 9 reuses an inherited property of the same name and type. No new syntax. |
| VB-143 | Record `=` and `<>` | Done in phase 114: on `vbnext`, as `op_Equality` and `op_Inequality` that call `Equals`, unless the record declares them. Before that, `plain = a` and `d = New Dog("Rex", "Lab")` on records report BC30452, "Operator '=' is not defined", because records synthesize `Equals` but no operators. C# 9 records declare `==` and `!=` that call `Equals`, and C# code calls them on VB records today only through `Equals`. No new syntax. |
| VB-144 | `AddressOf` without a target type | Done in phase 116: on `vbnext`, `Dim` infers VB's anonymous delegate type from a group with one method, as it does for a lambda, rather than C#'s `Action` and `Func`. Before that, `Dim handler = AddressOf Hello` reported BC30581, "'AddressOf' expression cannot be converted to 'Object'", though `Dim f = Sub() Hello()` infers a delegate type. C# 10 gives a method group with one candidate a natural function type. No new syntax; which delegate type it infers is a semantic choice to settle in phase 116. |
| VB-145 | C# records that inherit Visual Basic records | Done in phase 121: on `vbnext`, every record class declares an overridable `PrintMembers(builder As StringBuilder)`, which `ToString` calls, as C# 9 declares it. Found in phase 115. `public record Cat(string Name, int Lives) : Animal(Name)`, where `Animal` is a Visual Basic record, reports CS0115, "'Cat.PrintMembers(StringBuilder)': no suitable method found to override", because Visual Basic records build `ToString` in one method and declare no `Protected Overridable Function PrintMembers(builder As StringBuilder) As Boolean`, which C# 9 records override; since phase 115 that is the only error. No new syntax. |

## Wave 20

Checked against this tree after wave 19, by compiling and running against .NET 11 with `LangVersion` `vbnext` the shapes that wave 19 code meets next: `AddressOf` outside `Dim`, generic records, and records that cross between Visual Basic and C#, with a C# library alongside. Several shapes already work: `Record Class Box(Of T)(Value As T)`, a record that inherits `Box(Of Integer)`, its `=`, `ToString`, and `With`, and `Dim o As Object = Sub() Hello()`.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-146 | `AddressOf` to `Object` and `[Delegate]` | Done in phase 118: on `vbnext`, through the anonymous delegate phase 116 infers, which a delegate type still beats in overload resolution. Before that, `Dim o As Object = AddressOf Hello` reported BC30581 and `Run(AddressOf Hello)`, where `Run` takes a `[Delegate]`, reports BC30939, though phase 116 infers a delegate type for `Dim handler = AddressOf Hello` and `Dim o As Object = Sub() Hello()` already converts. C# 10 converts a method group with a natural type to `object` and `Delegate`. No new syntax. |
| VB-147 | `AddressOf` in `If` and array literals | Done in phase 119: on `vbnext`, dominant type inference counts each `AddressOf` as the type phase 116 infers, as it counts a lambda. Before that, `If(flag, AddressOf Hello, AddressOf Bye)` reported BC36911, "Cannot infer a common type", and `{AddressOf Hello, AddressOf Bye}` reports BC36717 and BC30581, because the dominant type of the branches and elements does not use the type phase 116 infers. C# 10 infers `Action` for `flag ? Hello : Bye`. No new syntax. |
| VB-148 | Visual Basic records that inherit C# records | Done in phase 120: a class from metadata with a `<Clone>$` method is a record, so on `vbnext` Visual Basic records chain to C# records and `With` copies them. Before that, `Record Class VbDog(Name As String, Breed As String)` that `Inherits CsAnimal(Name)`, where `CsAnimal` is a C# 9 record, reports BC40005, "property 'EqualityContract' shadows an overridable method", and prints `VbDog { Breed = Lab }`, because `IsRecord` is `False` for record types from metadata, so the derived record declares a new `EqualityContract`, compares and prints only its own members, and does not chain to the C# `Equals`. C# recognizes a record from metadata by its `<Clone>$` method. No new syntax. |

## Wave 21

Checked against this tree after wave 20, by compiling and running against .NET 11 with `LangVersion` `vbnext` the shapes that wave 20 code meets next, in records and in `AddressOf`. Several shapes already work: `Public ReadOnly Property Fahrenheit As Double = Degrees * 9 / 5 + 32` initialized from a positional parameter, a `Sub New()` that calls `Me.New("anonymous")`, `ReadOnly Record Structure`, a record that `Implements IComparable(Of Version)`, `Dim f As Func(Of Integer, Integer) = t` where `t` is set from `AddressOf Twice`, `Apply(AddressOf Twice, 5)` and `Make(AddressOf Answer)`, which infer their type arguments from the method, and `Dim f As Func(Of Action) = Function() AddressOf Hello`.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-149 | Lambda return types from `AddressOf` | Done in phase 125: on `vbnext`, a lambda that returns `AddressOf` infers the type phase 116 infers. Before that, `Dim factory = Function() AddressOf Hello` inferred `Function <generated method>() As Object`, so `factory()()` reports BC30454, "Expression is not a method", because return type inference does not use the type phase 116 infers. C# 10 infers `Func<Action>` for `() => Hello`. No new syntax. |
| VB-150 | `IEquatable(Of T)` on records | Done in phase 123: on `vbnext`, through the synthesized typed `Equals`; a declared `Equals(other As R)` needs an `Implements` clause. Before that, `GetType(IEquatable(Of Point)).IsAssignableFrom(GetType(Point))` is `False`, because records declare `Equals(other As Point)` but not the interface, so code that constrains `T As IEquatable(Of T)` cannot take a record. C# 9 records implement `IEquatable<R>`. No new syntax. |
| VB-151 | Unread positional parameters | Done in phase 124: on `vbnext`, warning BC42516 names a positional parameter that the record never reads. Before that, `Record Class Account(Id As Integer, Owner As String)` that declares `Public Property Owner As String` drops the `Owner` argument without a warning, so `New Account(1, "Ann").Owner` is `Nothing`; so does a parameter of a derived record that is not passed to the base record, since phase 113. C# 9 reports CS8907, "Parameter 'Owner' is unread. Did you forget to use it to initialize the property with that name?". A new warning, but no new syntax. |

## Wave 22

Checked against this tree after wave 21, by compiling and running against .NET 11 with `LangVersion` `vbnext` the record and delegate shapes that wave 21 code meets next, with a C# library alongside, and a few newer .NET shapes. Several shapes already work: `Required` properties in records, which `With` copies without setting them again; `Optional` and `ParamArray` positional parameters; `Record Class Pair(Of T As IComparable(Of T))` with an initializer that reads its parameters; an `Async` lambda that returns `AddressOf`; a declared `ToString` in a record; `ToString` printing members in the order C# prints them; `SyncLock` on a `System.Threading.Lock`, which enters it through the `Lock` API; and `<ModuleInitializer>`.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-152 | Records under a sealed `ToString` | Done in phase 127: on `vbnext`, such a record declares no `ToString` and keeps `PrintMembers`. Before that, a record that inherits a record whose `ToString` is `NotOverridable Overrides`, in Visual Basic, or `sealed override`, in C# 10, reports BC30267, "cannot override ... because it is declared 'NotOverridable'", because every record class declares its own `ToString`. C# 10 declares no `ToString` in such a record. No new syntax. |
| VB-153 | Declared typed `Equals` on records | Done in phase 128: on `vbnext`, a record's declared `Equals(other As R)` implements `IEquatable(Of R).Equals` without an `Implements` clause, as in C# 9. Before that, since phase 123, a record that declares its own `Public Overloads Function Equals(other As R) As Boolean` reports BC30149 until it adds `Implements IEquatable(Of R).Equals`, because Visual Basic implements interface members only through an `Implements` clause. C# 9 records implement `IEquatable<R>` with a declared `Equals(R)` implicitly. No new syntax; whether the record implements it implicitly is a semantic choice to settle in phase 128. |

## Wave 23

Checked against this tree after wave 22, by compiling and running against .NET 11 with `LangVersion` `vbnext` shapes from newer C# and .NET code beyond records and delegates, with a C# library alongside. Several shapes already work: `Async Function Main() As Task`; `Debug.Assert(True, $"{Expensive()}")`, which does not call `Expensive` through the interpolated string handler; `Function Names(Of T As {Structure, [Enum]})()`; calling a C# method constrained to `unmanaged` with a Visual Basic structure; and `Select Case` on a `ReadOnlySpan(Of Char)` with string cases.

| Id | Feature | Gap in this tree |
| --- | --- | --- |
| VB-154 | `[Delegate]` and `MulticastDelegate` constraints | Done in phase 130: on `vbnext`, both can constrain a type parameter. Before that, `Function Combine(Of T As [Delegate])(a As T, b As T) As T` reports BC32061, "'[Delegate]' cannot be used as a type constraint", though `[Enum]` already can be one. C# 7.3 allows `where T : System.Delegate` and `where T : System.MulticastDelegate`. No new syntax. |

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

### Wave 6 order

The three gaps with no new syntax come first, smallest first, then the two designs that wave 5 left open.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-068 | Configured asynchronous streams | M | Binds the members that VB-061 and VB-062 already call by pattern instead of by interface. Fixes the most common wave 5 failure. |
| 2 | VB-069 | `Await` in `Catch` and `Finally` | L | A lowering change in the async rewriter, like the one slice 26b uses for `Async Using`, plus `GoTo` from `Catch` into `Try`. |
| 3 | VB-070 | Task-like `Async` return types | L | A builder chosen from the return type instead of the three fixed builders. Follows VB-069 so both state machine changes are tested together. |
| 4 | VB-071 | Async iterator lambdas | M | Needs a design note first. Reuses the slice 26d lowering. |
| 5 | VB-072 | A type test that guards the rest of a block | M | Needs a design note first, because it changes where a name from an `If` condition is in scope. |

### Wave 7 order

The three memory gaps come first, because each one blocks span-based code that the next one needs, then the two lambda and loop gaps.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-073 | Reading `ref readonly` returns | M | Symbols and binding for a return kind that metadata already describes. Every later span gap reads span elements. |
| 2 | VB-074 | `Using` on a ref structure | M | Binds `Dispose` by pattern for one kind of type, beside the slice 30b pattern for `DisposeAsync`. |
| 3 | VB-075 | Ref structure locals in `Async` and `Iterator` methods | L | A flow check for values live across `Await` and `Yield`, in place of the blanket error. Follows VB-073 so those locals can be read. |
| 4 | VB-076 | Attributes on lambda parameters | M | The attribute list moves from an error to the synthesized parameter. Then `EnumeratorCancellation` reaches an async iterator lambda. |
| 5 | VB-077 | Deconstruction in `For Each` | M | Needs a design note first, because `For Each (a, b) In` could declare new names or assign existing ones. Reuses the VB-005 deconstruction from slices 5a to 5d. |

### Wave 8 order

The three generic math gaps come first, in the order a test needs them: a type that implements the members must load before code can call them, and calls must work before operators can be built on them. Then the two reference gaps, smallest first.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-078 | Running types that implement shared abstract members | M | An emit fix for the slice 9f form, with method implementation records in place of virtual shared methods. Every later generic math test runs such a type on .NET. |
| 2 | VB-079 | Shared abstract properties and shared virtual members through a type parameter | L | Extends the slice 9g `constrained.` call to properties, generic methods, and members with a default body. |
| 3 | VB-080 | Operators through a type parameter | L | Operator resolution looks at the shared abstract operators of a type parameter's constraints, then emits them as 9g calls. Follows VB-079, which it reuses. |
| 4 | VB-081 | Writing through a `ByRef` return on a value | S | The assignment target rule for a `ByRef` property or indexer whose receiver is a value. |
| 5 | VB-082 | `ByRef` locals | L | Needs a design note first, because it adds a declaration form and its rules for lambdas, `Async`, and reassignment. The first slice makes today's silently ignored `Dim ByRef` an error. |

VB-083, found in slice 42a, joins phase 42 as slice 42c, because it is member lookup on the same type parameters.

### Wave 9 order

The three generic math authoring gaps come first, smallest first, so that a VB type can implement `INumber(Of T)` step by step. Then the two storage gaps.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-084 | Shared virtual members with a default body | S | A fix to the BC37315 check. Every later generic math slice implements interfaces that declare such members. |
| 2 | VB-085 | Implementing shared abstract properties | M | Extends slice 9f from functions to properties, with the slice 41a emit rules for their accessors. |
| 3 | VB-086 | Implementing generic math operators | L | Needs a design note first, because it decides how a VB operator implements an interface operator. Follows VB-084 and VB-085, which a test type needs as well. |
| 4 | VB-087 | Ref fields | L | Needs a design note first, because it adds a field form and its rules for assignment and lifetime. Reuses the `ByRef` local machinery from phase 45. |
| 5 | VB-088 | Inline arrays | M | Element access, slicing, and `For Each` for one kind of structure, emitted through `Unsafe` and `MemoryMarshal` as C# does. |

### Wave 10 order

The three gaps that need no new rules come first, smallest first. Then the two lifetime gaps, which each need a design note.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-089 | `AddressOf` through a type parameter | S | Delegate creation with the constrained call that slice 42a already emits for calls. |
| 2 | VB-090 | Inline array values | M | Keeps the value in a temporary that lives to the end of the statement, so the span that slice 51a builds stays valid. |
| 3 | VB-091 | Ref fields from other assemblies | M | Reads `ref` and `ref readonly` fields from metadata, and reuses the phase 50 emitter. |
| 4 | VB-092 | `ByRef` locals over ref field structures | S | Needs a design note first, to extend the slice 50a rules from `ByRef` parameters to `ByRef` locals. |
| 5 | VB-093 | Returning ref field structures | L | Needs a design note first, because it replaces a fixed rule from slice 50a with rules for what a returned structure may refer to. |

### Wave 11 order

The gap with a form already in the language comes first. Then the three declaration gaps, each with a design note, smallest first.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-095 | `NameOf` an unbound generic type | S | Parses the `GetType` form inside `NameOf`. |
| 2 | VB-096 | Shared virtual interface members with a body | M | Needs a design note first. Emits the default body that slice 47a already calls. |
| 3 | VB-097 | Declaring `ByRef ReadOnly` functions | M | Needs a design note first. Emits the `IsReadOnlyAttribute` marking that slice 35a reads. |
| 4 | VB-098 | Declaring `ByRef ReadOnly` parameters | M | Needs a design note first, because it decides how VB passes an argument that is not a variable. |

### Wave 12 order

The gap that blocks calls into C# libraries comes first. Then the gaps whose forms are already in the language, smallest first, and last the two that need a design note.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-099 | `in` parameters from other assemblies | M | Blocks calling, overriding, and implementing C# members with `in` parameters, and VB's own `ByRef ReadOnly` interface members across assemblies. |
| 2 | VB-101 | `NameOf` a qualified unbound generic type | S | Extends the slice 59a parse to qualified names. |
| 3 | VB-100 | Lambdas for `ByRef ReadOnly` delegates | S | Lets a lambda take its parameters' `ByRef ReadOnly` from the delegate, as slice 62b's parameters read in method bodies. |
| 4 | VB-103 | `ByRef ReadOnly` operator operands | S | Lifts BC30651 for `ByRef ReadOnly` only, and reuses the slice 62b emitter and argument passing. |
| 5 | VB-102 | Shared properties in interfaces | M | Declares the shared properties that slice 48a implements and slice 60b's methods already match. |
| 6 | VB-104 | Operators in interfaces | M | Needs a design note first. Declares the operators that phase 49 implements and phase 43 calls through a type parameter. |
| 7 | VB-105 | Read-only structure members | M | Needs a design note first, because `ReadOnly` already has a meaning on properties. |

### Wave 13 order

The gap that blocks calls into the .NET libraries comes first. Then the gaps that finish wave 11 and 12 forms, smallest first, then the C# 11 and 14 forms, and last the one that needs a design note.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-106 | Calling C# `params` collections | M | Blocks calls to library overloads that take only `params ReadOnlySpan(Of T)`. Builds the span as a collection expression already does. |
| 2 | VB-113 | `NameOf` members of unbound generic types | S | Extends slice 65a from the type to its members, and replaces the misleading error. |
| 3 | VB-112 | `ByRef ReadOnly` conversions and lifted operators | M | Finishes phase 67 with the conversion and lifting paths it left out. |
| 4 | VB-109 | Instance interface properties with a body | S | Reuses the phase 68 property block in interfaces for instance properties. |
| 5 | VB-110 | Shared interface members that are not virtual | S | Gives the plain `Shared` form that `docs/vbnext-vb096-syntax.md` keeps its meaning. |
| 6 | VB-108 | Generic attributes | M | Applies and declares attribute classes with type parameters. |
| 7 | VB-107 | C# 14 extension members | L | Reads extension properties and static extension members from the metadata C# 14 emits. |
| 8 | VB-111 | Conversion operators in interfaces | L | Needs a design note first, because conversions through a type parameter need new lookup rules. |

### Wave 14 order

The gap that blocks calls into the .NET libraries comes first. Then the C# 14 operator forms, smallest first, then the gap that finishes phase 78, then the ref structure forms, last the one that needs a design note.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-114 | Other `params` collection types | M | Finishes phase 72 with the collection types that `ImmutableArray(Of T)` and other library overloads take. |
| 2 | VB-115 | C# 14 compound assignment operators | M | Types that declare only `+=` cannot be used with `+=` from VB at all. |
| 3 | VB-116 | C# 14 extension operators | M | Adds the extension blocks of phase 78 to operator lookup. |
| 4 | VB-117 | Generic C# 14 extension blocks | M | Finishes phase 78 for blocks with type parameters, which most library extensions use. |
| 5 | VB-118 | Ref structures for interface constraints | M | Lets VB ref structures reach generic library code that C# 13 opened to them. |
| 6 | VB-119 | `allows ref struct` type parameters | S | Already declared by slice 7m's `ByRef` constraint; phase 86 records it and tests it with phase 85. |

### Wave 15 order

The interface members that C# 8 completes come first, smallest first, then the lambda forms, then the gap that finishes phases 83 and 84, then the alias, which changes `Imports` parsing.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-120 | Interface member accessibility | S | Extends the interface bodies of phases 75 and 76 with the accessibility C# 8 gives them. |
| 2 | VB-121 | Shared interface fields and constructors | M | Gives shared interface members the state and initialization that C# 8 interfaces have. |
| 3 | VB-122 | Interface custom events | M | Finishes default interface members with the last member kind. |
| 4 | VB-123 | Lambda optional and ParamArray parameters | M | Lets VB lambdas take the parameter forms that VB methods already take. |
| 5 | VB-124 | Generic extension operators | S | Finishes phase 83 for generic blocks with phase 84's inference. |
| 6 | VB-125 | Tuple type aliases | M | Changes how `Imports` aliases parse, so it comes last. |

### Wave 16 order

The gap that finishes phase 93 comes first, then the operator and copy forms, smallest first, then the assignment form, last the conversion that touches every array literal.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-126 | Aliases for predefined and array types | S | Finishes phase 93 for the other types that are not names. |
| 2 | VB-127 | Tuple equality | M | Lets tuples compare as C# compares them, element by element. |
| 3 | VB-128 | `With` on structures | S | Extends the record `With` to every structure. |
| 4 | VB-129 | `With` on anonymous types | S | Extends the same copy to anonymous types. |
| 5 | VB-130 | Null-conditional compound assignment | M | Finishes the `?.` assignment that already works for `=`. |
| 6 | VB-131 | Array literals for collection types | L | Changes how array literals convert, so it comes last. |

### Wave 17 order

The gaps that finish phases 100, 96, and 99 come first, smallest first, then the span receivers, last the change to overload resolution, which touches every call.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-133 | Spreads in array literals | S | Gives `{...}` the spreads that `(...)` already has. |
| 2 | VB-135 | Nullable tuple equality | S | Lifts phase 96 to nullable tuples. |
| 3 | VB-134 | Null-conditional nothing-coalescing assignment | M | Finishes phase 99 with `?=`. |
| 4 | VB-136 | Span receivers for extension methods | M | Opens the span extensions of the .NET libraries to arrays. |
| 5 | VB-132 | Collection literals as arguments | L | Changes overload resolution, so it comes last. |

### Wave 18 order

The gaps that finish phases 102 and 106 come first, smallest first, then the `If` target, last the type inference, which touches overload resolution.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-137 | Spreads into collections without `AddRange` | S | Finishes phase 102 for `HashSet(Of T)` and other types with only `Add`. |
| 2 | VB-139 | Collection literals as named arguments | S | Finishes phase 106 for named arguments. |
| 3 | VB-140 | Collection literals in `If` | M | Gives `If(...)` branches the target type, as `Dim` and `Return` give it. |
| 4 | VB-138 | Type inference from collection literal arguments | M | Changes type inference, so it comes last. |

### Wave 19 order

The record gaps come first, as one property per name gives the equality work a single member to compare, then the delegate inference, which changes what `Dim` infers.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-142 | Positional parameters of inherited properties | S | Gives a derived record one property per name, which equality then compares once. |
| 2 | VB-143 | Record `=` and `<>` | S | Operators that call `Equals`, so phase 115 fixes both at once. |
| 3 | VB-141 | Equality of records that inherit records | L | Changes the synthesized `Equals`, `GetHashCode`, and `ToString` of every record. |
| 4 | VB-144 | `AddressOf` without a target type | M | Changes what `Dim` infers, so it comes last, after its semantics are settled. |

### Wave 20 order

The `AddressOf` gaps that finish phase 116 come first, smallest first, then the record gaps between Visual Basic and C#, the one that reads C# records before the one that changes how Visual Basic records print.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-146 | `AddressOf` to `Object` and `[Delegate]` | S | Gives the phase 116 type to the two targets that are not delegate types. |
| 2 | VB-147 | `AddressOf` in `If` and array literals | M | Feeds the phase 116 type into dominant type inference. |
| 3 | VB-148 | Visual Basic records that inherit C# records | M | Recognizes C# records from metadata, so phases 113 to 115 chain to them. |
| 4 | VB-145 | C# records that inherit Visual Basic records | M | Splits `ToString` into `PrintMembers`, which every record then overrides, so it comes last. |

### Wave 21 order

The record gaps come first, smallest first, then the lambda inference, which touches return type inference.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-150 | `IEquatable(Of T)` on records | S | Declares the interface the typed `Equals` already implements. |
| 2 | VB-151 | Unread positional parameters | S | One new warning where a record drops an argument. |
| 3 | VB-149 | Lambda return types from `AddressOf` | M | Feeds the phase 116 type into return type inference, so it comes last. |

### Wave 22 order

The record that cannot compile comes first, then the convenience that needs a decision.

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-152 | Records under a sealed `ToString` | S | Unblocks records that inherit from a record with a sealed `ToString`, in either language. |
| 2 | VB-153 | Declared typed `Equals` on records | S | Changes how a declared member implements an interface, after its semantics are settled. |

### Wave 23 order

| # | Id | Feature | Rank | Why this slot |
| --- | --- | --- | --- | --- |
| 1 | VB-154 | `[Delegate]` and `MulticastDelegate` constraints | S | Lifts one constraint check, as `[Enum]` is already allowed. |
