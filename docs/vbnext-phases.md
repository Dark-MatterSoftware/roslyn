# Vb.Next phases and slices

**Vb.Next** is an independent community effort. It is not affiliated with, endorsed by, or supported by Microsoft. This note slices the backlog in [vbnext-missing-features.md](vbnext-missing-features.md). It does not approve syntax, and it does not implement a language feature.

A **phase** is a stretch of the implementation order that ends in a usable compiler increment. A **slice** is one change inside a phase: compiler tests for that behavior, gated by the experimental language version, with no IDE work required to call the slice done. IDE support is a follow-up slice and does not block the compiler slice.

`default` and `latest` stay on `17.13` until a later, explicit decision says otherwise. Every slice after 0b is rejected on `17.13`.

Local test for a slice is the Visual Basic project under `src/Compilers/VisualBasic/Test/` that owns the layer just changed. The full suite stays out of the slice exit check.

## Phase map

| Phase | Rows | Outcome when the phase ends |
| --- | --- | --- |
| 0. Gate | 1 | An opt-in version exists. `default` and `latest` are unchanged. |
| 1. Prove the gate | 2–6 | Five small features use that version. No new statement form. |
| 2. Shorthand | 7–12 | Target typing, `Using`, and assignment sugar parse and bind. |
| 3. Construction | 13–17 | Init-only, required, generic attributes, readonly structures, `nint`. |
| 4. Index and text | 18–21 | Ranges, covariant returns, raw strings, UTF-8 strings. |
| 5. Matching | 22–23 | Deconstruction, then a first `Select Case` pattern slice. |
| 6. Type shape | 24–26 | Partial members and primary constructors. |
| 7. Collections and memory | 27–30 | Collection expressions, ref returns, ref structs, collection `ParamArray`. |
| 8. Records | 31 | Record classes, then `With` copies, then record structs. |
| 9. Interfaces | 32–34 | Default interface methods, extension blocks, static abstract members. |
| 10. Nullability | 35 | Annotations, then flow, then metadata. |
| 11. List patterns | VB-004, after phase 7 | `Case Is { }` matches an array, then a list. |
| 12. TypeOf lists | VB-054, pulled in from wave 4 | `TypeOf value Is {T1, T2}` matches any listed type. |
| 13. GUID literals | VB-052, pulled in from wave 4 | `&G{...}` is a `System.Guid` value. |
| 14. For Each index | VB-055, pulled in from wave 4 | `For Each item With index In items` numbers each item. |
| 15. Optional End Namespace | VB-059, pulled in from wave 4 | A namespace may run to the end of the file. |
| 16. With variable | VB-053, pulled in from wave 4 | A `With` block that declares its variable. |
| 17. For Each ByRef | VB-056, pulled in from wave 4 | A loop variable that refers to each array element. |
| 18. Compact members | VB-050, pulled in from wave 4 | A member whose body is one expression. |
| 19. Unchecked arithmetic | VB-057, pulled in from wave 4 | `Unchecked(expression)` wraps integer overflow. |
| 20. Concise lambdas | VB-051, pulled in from wave 4 | A shorter way to write a one-parameter lambda. |
| 21. XML statement blocks | VB-060, pulled in from wave 4 | Statements inside `<%= %>`. |
| 22. Sub chains | VB-058, pulled in from wave 4 | Calling a `Sub` again with new arguments. |
| 23. IDE | All `vbnext` syntax | The Visual Basic editor understands the forms above. |
| 24. Real projects | All `vbnext` syntax | `dotnet build` compiles a project with `LangVersion` `vbnext`, with analyzers on. |
| 25. Runtime interop | VB-064, VB-065, wave 5 | `SyncLock` uses `System.Threading.Lock`, and `$"..."` passes to an interpolated string handler. |
| 26. Asynchronous streams | VB-061, VB-062, VB-063, wave 5 | VB releases `IAsyncDisposable` resources, iterates `IAsyncEnumerable(Of T)`, and authors one. |
| 27. Pattern variables | VB-066, wave 5 | A type pattern names its result for the code it guards. |
| 28. Property backing fields | VB-067, wave 5 | A property with a body stores its value without a declared field. |
| 29. IDE for wave 5 | Phases 25 to 28 | The editor understands the forms above, as phase 23 did for earlier syntax. |
| 30. Configured asynchronous streams | VB-068, wave 6 | `Await Each` and `Async Using` take `WithCancellation` and `ConfigureAwait`. |
| 31. Await in Catch and Finally | VB-069, wave 6 | An `Async` method awaits in `Catch` and `Finally`. |
| 32. Task-like returns | VB-070, wave 6 | An `Async Function` returns `ValueTask`, `ValueTask(Of T)`, or a type with a method builder. |
| 33. Async lambdas and guards | VB-071, VB-072, wave 6 | Design notes for async iterator lambdas and for a type test that guards the rest of a block, then the locked forms. |
| 34. IDE for wave 6 | Phases 30 to 33 | The editor understands any syntax that phase 33 locks. |
| 35. Read-only references | VB-073, wave 7 | Code reads `ReadOnlySpan(Of T)` elements and other `ref readonly` returns. |
| 36. Using on ref structures | VB-074, wave 7 | `Using` releases a ref structure through its `Dispose`. |
| 37. Ref structures in async code | VB-075, wave 7 | `Async` and `Iterator` methods declare span locals that do not cross `Await` or `Yield`. |
| 38. Lambda parameter attributes | VB-076, wave 7 | A lambda parameter carries attributes, including `EnumeratorCancellation`. |
| 39. Deconstructing loops | VB-077, wave 7 | A design note for deconstruction in `For Each`, then the locked form. |
| 40. IDE for wave 7 | Phases 35 to 39 | The editor understands any syntax that phase 39 locks. |
| 41. Shared abstract implementations | VB-078, wave 8 | A type that implements `Shared MustOverride` members, or `IParsable(Of T)`, loads and runs on .NET. |
| 42. Shared abstract access | VB-079, VB-083, wave 8 | `T.Zero` and `T.CreateChecked(x)` read and call shared abstract and shared virtual members through a type parameter, and `value.ToString()` works on such a type parameter. |
| 43. Generic math operators | VB-080, wave 8 | `a + b` and `a > b` bind to the shared abstract operators of a type parameter's constraints. |
| 44. Writing through references | VB-081, wave 8 | `AsSpan(list)(0) = 5` writes through a `ByRef` return whose receiver is a value. |
| 45. ByRef locals | VB-082, wave 8 | A design note for `ByRef` locals, then the locked form. |
| 46. IDE for wave 8 | Phases 41 to 45 | The editor understands any syntax that phase 45 locks. |
| 47. Shared virtual defaults | VB-084, wave 9 | A type implements an interface without supplying its shared virtual members that have a default body. |
| 48. Shared property implementations | VB-085, wave 9 | A `Shared` property implements a shared abstract property, such as `AdditiveIdentity`. |
| 49. Operator implementations | VB-086, wave 9 | A design note for implementing interface operators, then the locked form. |
| 50. Ref fields | VB-087, wave 9 | A design note for ref fields in a `ByRef Structure`, then the locked form. |
| 51. Inline arrays | VB-088, wave 9 | Code indexes, slices, and iterates an `<InlineArray>` structure. |
| 52. IDE for wave 9 | Phases 47 to 51 | The editor understands any syntax that phases 49 and 50 lock. |
| 53. `AddressOf` through type parameters | VB-089, wave 10 | `AddressOf T.Parse` makes a delegate to a shared abstract or virtual member. |
| 54. Inline array values | VB-090, wave 10 | Code reads and iterates the elements of an inline array that is a value, such as a function result. |
| 55. Ref fields from metadata | VB-091, wave 10 | Code reads and writes `ref` and `ref readonly` fields declared in C#. |
| 56. `ByRef` local lifetime | VB-092, wave 10 | A design note extends the slice 50a rules to `ByRef` locals, then the check. |
| 57. Returning ref field structures | VB-093, wave 10 | A design note for returning a structure that holds a ref field, then the locked rules. |
| 58. IDE for wave 10 | Phases 53 to 57 | The editor understands any syntax that phases 56 and 57 lock. |
| 59. `NameOf` unbound generics | VB-095, wave 11 | `NameOf(List(Of))` is `"List"`. |
| 60. Shared virtual interface members | VB-096, wave 11 | A design note for declaring them, then the locked form. |
| 61. `ByRef ReadOnly` functions | VB-097, wave 11 | A design note for declaring `ref readonly` returns, then the locked form. |
| 62. `ByRef ReadOnly` parameters | VB-098, wave 11 | A design note for declaring `ref readonly` parameters, then the locked form. |
| 63. IDE for wave 11 | Phases 59 to 62 | The editor understands any syntax that phases 60 to 62 lock. |
| 64. `in` parameters from metadata | VB-099, wave 12 | Code calls, overrides, and implements C# members with `in` parameters. |
| 65. Qualified `NameOf` unbound generics | VB-101, wave 12 | `NameOf(System.Collections.Generic.List(Of))` is `"List"`. |
| 66. Lambdas for `ByRef ReadOnly` delegates | VB-100, wave 12 | A lambda converts to a delegate with `ByRef ReadOnly` parameters. |
| 67. `ByRef ReadOnly` operator operands | VB-103, wave 12 | An operator takes `ByRef ReadOnly` operands. |
| 68. Shared interface properties | VB-102, wave 12 | A VB interface declares shared abstract and shared virtual properties. |
| 69. Interface operators | VB-104, wave 12 | A design note for declaring operators in an interface, then the locked form. |
| 70. Read-only structure members | VB-105, wave 12 | A design note for read-only structure members, then the locked form. |
| 71. IDE for wave 12 | Phases 64 to 70 | The editor understands any syntax that phases 69 and 70 lock. |
| 72. Calling `params` collections | VB-106, wave 13 | Code calls C# `params ReadOnlySpan(Of T)` and other `params` collection methods with separate arguments. |
| 73. `NameOf` unbound generic members | VB-113, wave 13 | `NameOf(List(Of).Count)` is `"Count"`. |
| 74. `ByRef ReadOnly` conversions and lifting | VB-112, wave 13 | Conversions, `IsTrue`, and `IsFalse` take `ByRef ReadOnly` operands, and such operators lift to `Nullable`. |
| 75. Instance interface properties | VB-109, wave 13 | An interface declares instance properties with a default body. |
| 76. Shared non-virtual interface members | VB-110, wave 13 | An interface declares `Shared` members with a body that are not virtual. |
| 77. Generic attributes | VB-108, wave 13 | Code applies and declares attributes with type parameters. |
| 78. C# 14 extension members | VB-107, wave 13 | Code reads C# 14 extension properties and static extension members. |
| 79. Interface conversion operators | VB-111, wave 13 | A design note for conversion operators in interfaces and conversions through type parameters, then the locked form. |
| 80. IDE for wave 13 | Phases 72 to 79 | The editor understands any syntax that phase 79 locks, and shows the wave 13 forms. |
| 81. Other `params` collections | VB-114, wave 14 | Code calls C# `params` collections of the other types C# 13 allows, and VB's own `ParamArray` takes them. |
| 82. Compound assignment operators | VB-115, wave 14 | `c += 5` calls the instance `+=` operator that C# 14 declares. |
| 83. Extension operators | VB-116, wave 14 | Code uses the operators that C# 14 extension blocks declare. |
| 84. Generic extension blocks | VB-117, wave 14 | Code reads the members of generic C# 14 extension blocks. |
| 85. Ref structures for interface constraints | VB-118, wave 14 | A `ByRef Structure` that implements an interface is a type argument where C# 13 allows ref structures. |
| 86. `allows ref struct` type parameters | VB-119, wave 14 | Type parameters take ref structures, with the form slice 7m already declares. |
| 87. IDE for wave 14 | Phases 81 to 86 | The editor understands any syntax that phase 86 locks, and shows the wave 14 forms. |
| 88. Interface member accessibility | VB-120, wave 15 | Interface members with a body may be `Private`, `Friend`, or `Protected`. |
| 89. Shared interface fields and constructors | VB-121, wave 15 | An interface declares `Shared` fields and a `Shared Sub New`. |
| 90. Interface custom events | VB-122, wave 15 | An interface declares a `Custom Event` with default accessors. |
| 91. Lambda optional and ParamArray parameters | VB-123, wave 15 | Lambda parameters may be `Optional` with a default value, or `ParamArray`. |
| 92. Generic extension operators | VB-124, wave 15 | Code uses the operators of generic C# 14 extension blocks. |
| 93. Tuple type aliases | VB-125, wave 15 | An `Imports` alias names a tuple type. |
| 94. IDE for wave 15 | Phases 88 to 93 | The editor shows the wave 15 forms. |
| 95. Aliases for predefined and array types | VB-126, wave 16 | An `Imports` alias names `Integer`, `Integer()`, and other types that are not names. |
| 96. Tuple equality | VB-127, wave 16 | `=` and `<>` compare tuples element by element. |
| 97. `With` on structures | VB-128, wave 16 | `With` copies any structure. |
| 98. `With` on anonymous types | VB-129, wave 16 | `With` copies an anonymous type. |
| 99. Null-conditional compound assignment | VB-130, wave 16 | `b?.Count += 1` assigns when `b` is not `Nothing`. |
| 100. Array literals for collection types | VB-131, wave 16 | An array literal converts to `List(Of T)`, `HashSet(Of T)`, `ImmutableArray(Of T)`, and other collection types. |
| 101. IDE for wave 16 | Phases 95 to 100 | The editor shows the wave 16 forms. |
| 102. Spreads in array literals | VB-133, wave 17 | `{..a, 3}` spreads `a` into a collection, as `(..a, 3)` does. |
| 103. Nullable tuple equality | VB-135, wave 17 | `=` and `<>` compare nullable tuples. |
| 104. Null-conditional nothing-coalescing assignment | VB-134, wave 17 | `box?.Name ?= "x"` assigns when `box` is not `Nothing` and `Name` is. |
| 105. Span receivers for extension methods | VB-136, wave 17 | An array calls extension methods declared on `ReadOnlySpan(Of T)` and `Span(Of T)`. |
| 106. Collection literals as arguments | VB-132, wave 17 | `{1, 2}` and `(1, 2)` pass to collection parameters. |
| 107. IDE for wave 17 | Phases 102 to 106 | The editor shows the wave 17 forms. |
| 108. Spreads into collections without `AddRange` | VB-137, wave 18 | `{..a, 3}` spreads into `HashSet(Of Integer)` and other types with only `Add`. |
| 109. Collection literals as named arguments | VB-139, wave 18 | `values:={2, 3}` passes to a collection parameter. |
| 110. Collection literals in `If` | VB-140, wave 18 | `If(flag, {1}, {2, 3})` builds the target collection. |
| 111. Type inference from collection literal arguments | VB-138, wave 18 | `Generic({1, 2})` infers `T` for a `List(Of T)` parameter. |
| 112. IDE for wave 18 | Phases 108 to 111 | The editor shows the wave 18 forms. |

Wave 4 stays off this line. A pulled-in item uses the "fits after" row in the backlog and becomes one extra slice in that phase.

## Phase 0 — Gate

| Slice | Work | Done when |
| --- | --- | --- |
| 0a | Design note. Trace `Feature.UnmanagedConstraint` from `ParserFeature.vb` through `VisualBasicParseOptions`, `/langversion` in `VisualBasicCommandLineParser.vb`, the `ERRID`, and an existing command-line test. | The note names the experimental version spelling and lists every file a new version touches. No code change. |
| 0b | Add the version value. Parse it from `/langversion`. Map `default` and `latest` to `VisualBasic17_13` still. | A command-line test accepts the new spelling and rejects it as `latest`. No syntax is enabled yet. The spelling chosen in [vbnext-language-version.md](vbnext-language-version.md) is `vbnext` (`LanguageVersion.VbNext = 9999`). |

## Phase 1 — Prove the gate

Each row is already one slice. Land them in order. Each one is the pattern the next copies.

| Slice | Id | Done when |
| --- | --- | --- |
| 1a | VB-008 `Enum` constraint | Done. `As {Structure, Enum}` and `As Enum` are legal on `vbnext`. The same syntax reports BC36716 on `17.13`. |
| 1b | VB-009 async entry point | Done. `Async Function Main() As Task` and `As Task(Of Integer)` compile on `vbnext` and the process waits for them. `Async Sub Main` still reports `ERR_AsyncSubMain`, including on `17.13`. |
| 1c | VB-012 Module initializers | Done. A `Shared` parameterless `Sub` marked `<ModuleInitializer>` runs before `Main` on `vbnext`. The same attribute still reports BC42381 on `17.13` and does not run. |
| 1d | VB-013 `SkipLocalsInit` | Done. On `vbnext`, the attribute on a method, property, type, nested type, or module omits `.locals init`. Visual Basic 17.13 still reports BC42381 and keeps local initialization. |
| 1e | VB-022 `UnmanagedCallersOnly` | Done. On `vbnext`, a Shared ordinary method may be marked `<UnmanagedCallersOnly>`, including a `CallConv` calling convention. Managed code cannot call it, and it cannot be `Main` or a module initializer. Visual Basic 17.13 still reports BC37316. |

## Phase 2 — Shorthand

| Slice | Id | Done when |
| --- | --- | --- |
| 2a | VB-001 | Done. `Dim p As Point = New(1, 2)` binds on `vbnext` and calls the matching constructor. Visual Basic 17.13 still parses that form as a tuple type. |
| 2b | VB-001 | Done. On `vbnext`, `New(arguments)` takes its type from an assignment, a `Return`, or an argument. |
| 2c | VB-007 | Done. On `vbnext`, `Default` in a local `As` clause, an assignment, a `Return`, or an argument is the default of that type. Visual Basic 17.13 still reports an expected-expression error. |
| 2d | VB-010 | Done. On `vbnext`, `x ?= y` assigns `y` when `x` is `Nothing` and leaves `x` unchanged otherwise. It lowers to `x = If(x, y)`. Visual Basic 17.13 reports BC36716. |
| 2e | VB-023 | Done. On `vbnext`, a `Using` with no `End Using` disposes at the end of the enclosing block. An explicit `End Using` still releases the resource there. Visual Basic 17.13 still expects `End Using`. |
| 2f | VB-025 | Done. On `vbnext`, `$"..."` is a constant when every hole is a string constant and no hole has an alignment or format. A number hole stays non-constant. Visual Basic 17.13 still requires a constant expression. |
| 2g | VB-024 | Done. On `vbnext`, `receiver?.Member = value` assigns only when the receiver is not `Nothing`, and the right-hand side runs only then. Visual Basic 17.13 still requires an assignable left side. |

2d uses `?=`. The backlog did not lock the spelling.

## Phase 3 — Construction

| Slice | Id | Done when |
| --- | --- | --- |
| 3a | VB-002 | Done. On `vbnext`, `Public InitOnly Property Name As String` is an auto-property whose setter is init-only, and a constructor can assign it. Visual Basic 17.13 reports BC36716. |
| 3b | VB-002 | Done. On `vbnext`, assigning an `InitOnly` property outside a constructor or object initializer is an error. An interface can declare one, and an implementing property must use an init-only setter. |
| 3c | VB-011 | Done. On `vbnext`, `Public Required Property Name As String` must be set in an object initializer. A constructor assignment does not count. Visual Basic 17.13 reports BC36716. |
| 3d | VB-011 | Done. On `vbnext`, `<SetsRequiredMembers>` on a constructor lets callers skip the object initializer. Visual Basic 17.13 reports BC42381 and still requires the initializer. |
| 3e | VB-014 | Done. On `vbnext`, an attribute class may be generic, and a use site may pass type arguments, as in `<Info(Of String)>`. Visual Basic 17.13 still rejects both. |
| 3f | VB-018 | Done. On `vbnext`, `ReadOnly Structure Point` is a readonly structure. Instance fields must be ReadOnly, auto-implemented instance properties must be ReadOnly, and field-like instance events are errors. Visual Basic 17.13 reports that ReadOnly is not valid on a Structure. |
| 3g | VB-020 | Done. On `vbnext`, `nint` is `System.IntPtr` and `nuint` is `System.UIntPtr`. `Integer` and `Long` convert with `nint`, and `UInteger` and `ULong` convert with `nuint`, through the conversions those types already have. Visual Basic 17.13 reports an undefined type. A type already named `nint` still wins. |

## Phase 4 — Index and text

| Slice | Id | Done when |
| --- | --- | --- |
| 4a | VB-006 | Done. On `vbnext`, `items(^1)` is the last element of an array and `items(^2)` is the second to last. A receiver with side effects is evaluated once. `^` between two operands is still exponentiation. Visual Basic 17.13 reports an expected-expression error. Using `^n` other than as an array index is an error. |
| 4b | VB-006 | Done. On `vbnext`, `items(1..4)` is a slice of a one-dimensional array. The start is inclusive and the end is exclusive. Either side may be omitted, and either side may be an index from the end. A receiver with side effects is evaluated once. Visual Basic 17.13 still reports an expected-identifier error. Using a range other than to slice a one-dimensional array is an error. `Span` slicing stays in phase 7. |
| 4c | VB-017 | Done. On `vbnext`, an override may narrow a class return type when the return converts by a widening reference conversion and the runtime supports covariant returns. The emitted method keeps the narrower return, a new slot, a method implementation of the base method, and `PreserveBaseOverridesAttribute`. Visual Basic 17.13 still reports that the methods differ by their return types. A runtime without that support does the same. |
| 4d | VB-033 | Done. On `vbnext`, `r"""text"""` is a string. Quotes inside it are characters, and a multi-line raw string drops the whitespace that precedes the closing quotes. `{name}` stays text. `"""hello"""` is still a normal string whose value contains a quote. Visual Basic 17.13 still treats `r` as a name. |
| 4e | VB-033 | Done. On `vbnext`, `$r"""Hello {name}"""` is an interpolated raw string. A single `{` starts a hole, `{{` is a literal brace, and quotes inside the text are characters. A multi-line form drops the whitespace that precedes the closing quotes. Visual Basic 17.13 still reports an illegal `$`. |
| 4f | VB-034 | Done. On `vbnext`, `"hi"u8` is a `Byte()` of the UTF-8 bytes, with no byte-order mark. A raw string takes the same suffix. Visual Basic 17.13 still reports an expected end of statement on `u8`. The `ReadOnlySpan(Of Byte)` form waits for phase 7. |

## Phase 5 — Matching

Deconstruction finishes before pattern matching. Records in phase 8 use both.

| Slice | Id | Done when |
| --- | --- | --- |
| 5a | VB-005 | Done. On `vbnext`, `Dim (x, y) = point` declares locals from a tuple. The tuple is evaluated once, and each name takes the element in that position. A value that is not a tuple of the same length is an error. The form infers each type, so under `Option Infer Off` it reports BC37388, and code declares the variables with `As` clauses and assigns them with `(x, y) = point`. Visual Basic 17.13 still treats the parentheses as array bounds. |
| 5b | VB-005 | Done. On `vbnext`, `(x, y) = point` assigns each element into an existing variable. The tuple is evaluated once. A value that is not a tuple of the same length is an error. Visual Basic 17.13 still reports a syntax error on `(`. |
| 5c | VB-005 | Done. On `vbnext`, `Dim (x, , z) = point` and `(x, , z) = point` skip the empty slot. The empty slot still counts toward the tuple length, and the tuple is evaluated once. Visual Basic 17.13 still treats the parentheses as array bounds or a syntax error. |
| 5d | VB-005 | Done. On `vbnext`, a type that is not a tuple is deconstructed by an accessible method named `Deconstruct` whose `ByRef` parameters match the slots. An extension method is used when the type has no instance method of that name. The value is evaluated once, and an empty slot is still passed to the method. A value that is neither a tuple of that length nor a match for `Deconstruct` is an error. |
| 5e | VB-004 | Done. On `vbnext`, `Case Is TypeOf Integer AndAlso Is > 0` matches when the Select expression has that type and passes the relational test. The relational test compares the value as the tested type, so it compiles under `Option Strict On`. `Case Is TypeOf String` is the type test alone. The Select expression is evaluated once. Visual Basic 17.13 still reports an expected relational operator. |
| 5f | VB-004 | Done. On `vbnext`, `Case Is TypeOf Customer AndAlso .Balance Is > 0 AndAlso .Name = "A"` reads those members after the type test. The Select expression is evaluated once. |
| 5g | VB-004 | Done. On `vbnext`, `Case Is (1, 2)` compares tuple elements, and `Case Is TypeOf Point(1, 2)` compares the `ByRef` results of `Deconstruct`. The Select expression is evaluated once. |
| 5h | VB-004 | Done. On `vbnext`, `Dim label = Select Case order` followed by `Case` arms and `End Select` produces the matching arm. The Select expression is evaluated once. `Case Else` supplies the value when nothing earlier matches. Visual Basic 17.13 still reports an expected expression, and the following `Case` and `End Select` lines stay outside a Select statement. List patterns wait until phase 7. |

5e through 5h use the spelling locked in `docs/vbnext-vb004-syntax.md`. 6e through 6g use the spelling locked in `docs/vbnext-vb032-syntax.md`. 7a through 7c use the spelling locked in `docs/vbnext-vb035-syntax.md`.

## Phase 6 — Type shape

| Slice | Id | Done when |
| --- | --- | --- |
| 6a | VB-015 | Done. On `vbnext`, `Partial Property Label As String` is the defining declaration, and a second `Partial Property Label As String` with `Get` and `Set` is the implementing declaration. The declarations must have the same type and modifiers, and the implementing declaration supplies the accessors. Visual Basic 17.13 still reports that a property cannot be declared `Partial`. |
| 6b | VB-015 | Done. On `vbnext`, the `Get` and `Set` of a partial property may be declared on different parts. The parts must have the same type and modifiers. A read-write property still needs both accessors once the parts are combined. |
| 6c | VB-016 | Done. On `vbnext`, `Partial Event Click As Action` is the defining declaration, and `Partial Custom Event Click As Action` with `AddHandler`, `RemoveHandler`, and `RaiseEvent` is the implementing declaration. The declarations must have the same type and modifiers. Visual Basic 17.13 still reports that an event cannot be declared `Partial`. |
| 6d | VB-016 | Done. On `vbnext`, `Partial Sub New()` is the defining declaration and must be empty. A second `Sub New` with the same parameters supplies the body. The declarations must have the same modifiers. Visual Basic 17.13 still reports that a constructor cannot be declared `Partial`. |
| 6e | VB-032 | Done. On `vbnext`, a class parameter list is a constructor, and an instance field or auto-property initializer may name those parameters. The constructor argument is evaluated once. A shared initializer cannot use the parameter, and the parameter is not a property. Visual Basic 17.13 still rejects the list. |
| 6f | VB-032 | Done. On `vbnext`, `Inherits Base(arguments)` passes those arguments to the base constructor, and the arguments may name primary constructor parameters. `Inherits Base` with no argument list stays the existing form. An explicit constructor may chain to the primary constructor with `Me.New`. Visual Basic 17.13 still treats the argument list as a generic type. |
| 6g | VB-032 | Done. On `vbnext`, a structure parameter list is a constructor, and an instance field or auto-property initializer may name those parameters. The parameterless constructor remains and zeroes the structure. A shared initializer cannot use the parameter, and the parameter is not a property. Visual Basic 17.13 still rejects the list. |

## Phase 7 — Collections and memory

| Slice | Id | Done when |
| --- | --- | --- |
| 7a | VB-035 | Done. On `vbnext`, `(1, 2, 3)` builds an array when the target type is an array. `(1, 2)` with no target type stays a tuple. `()` is an empty array when the target type is an array. Square brackets stay escaped identifiers. Visual Basic 17.13 still treats the parentheses as a tuple. |
| 7b | VB-035 | Done. On `vbnext`, `(1, 2, 3)` creates a type that already accepts a collection initializer and adds each element. `()` creates an empty instance. A parenthesized value that already converts to that type stays that value. Visual Basic 17.13 still treats the parentheses as a tuple. |
| 7c | VB-035 | Done. On `vbnext`, `.. items` inside the parentheses copies an array or a list through `AddRange`. An array target is that list converted with `ToArray`. A parenthesized range stays a range. Visual Basic 17.13 still rejects the range. |
| 7d | VB-026 | Done. On `vbnext`, `ParamArray values As List(Of T)` and `ParamArray values As IEnumerable(Of T)` accept an expanded argument list. One argument that already converts to the parameter type is passed as that value. Visual Basic 17.13 still requires an array. |
| 7e | VB-019 | Done. On `vbnext`, `ByRef Function First(items As Integer()) As Integer` returns an assignable value. `Return items(0)` returns that element, and `First(items) = 9` writes through the returned address. Visual Basic 17.13 still rejects `ByRef` before `Function`. |
| 7f | VB-019 | Done. On `vbnext`, `ByRef ReadOnly Property First As Integer` returns an assignable value. `Return items(0)` returns that element, and `First = 9` writes through the returned address. A `ByRef ReadOnly` auto-property returns its backing field. Visual Basic 17.13 still rejects `ByRef` before `Property`. |
| 7g | VB-037 | Done. On `vbnext`, `ByRef Structure Buffer` is a ref-like structure. It can be a local and a field of another ByRef structure. It cannot be boxed, stored as a field of a class, or lifted into an iterator, an async method, or a lambda. Visual Basic 17.13 still rejects `ByRef` before `Structure`. |
| 7h | VB-037 | Done. On `vbnext`, `Span(Of T)` is a ref-like type. A local of that type is legal and can be emitted. It cannot be boxed or stored as a field of a class. A method that a ref-like type overrides, such as `ToString` on `ReadOnlySpan(Of Char)` or on a `ByRef Structure`, calls the override; before a fix found in wave 7, overload resolution reported BC30521 there, because the override and `ValueType.ToString` could not shadow by receiver type. Visual Basic 17.13 still reports that the type is obsolete. |
| 7i | VB-006 | Done. On `vbnext`, `values(^1)` and `values(1..4)` index and slice a `Span(Of T)` or `ReadOnlySpan(Of T)`. The start is inclusive and the end is exclusive. `values(^1) = 9` writes through the span indexer. |
| 7j | VB-034 | Done. On `vbnext`, `"hi"u8` has type `ReadOnlySpan(Of Byte)` when that type is available. The value is the UTF-8 bytes, with no byte-order mark. When that type is not available, the literal stays a `Byte()`. |
| 7k | VB-035 | Done. On `vbnext`, `(1, 2, 3)` creates a `Span(Of Integer)` when that is the target, and `(1, ..items)` copies the spread into a `ReadOnlySpan(Of Integer)`. The elements are stored in an array and passed to the span constructor. |
| 7l | VB-026 | Done. On `vbnext`, `ParamArray values As Span(Of Integer)` accepts an expanded argument list. The arguments are stored in an array and passed to the span constructor. |
| 7m | VB-037 | Done. On `vbnext`, `Sub Use(Of T As ByRef)(value As T)` allows a ByRef structure or `Span(Of T)` as the type argument. Without `ByRef`, that type argument is rejected. A type parameter with this constraint cannot be boxed or stored as a field of a class. Visual Basic 17.13 still rejects `ByRef` in the constraint. |

## Phase 8 — Records

Depends on phase 3 (init-only, required) and slice 5d (deconstruction). Positional records also depend on slice 6e.

| Slice | Id | Done when |
| --- | --- | --- |
| 8a | VB-031 | Done. On `vbnext`, `Record Class Point` with public properties emits value equality, `GetHashCode`, and `ToString` of the form `Point { X = 1, Y = 2 }`. Visual Basic 17.13 still rejects `Record` before `Class`. |
| 8b | VB-031 | Done. On `vbnext`, `a With { .Y = 3 }` copies record `a` and assigns the listed members. An init-only property can be set on the copy, and the original record is unchanged. A record class has a `Protected` copy constructor that copies every field without running field initializers, and a public `<Clone>$` method that calls it, the names C# uses. `With` calls `<Clone>$`, so it works on a positional record, copies private state from outside the class, and keeps the runtime type of a derived record. Visual Basic 17.13 still rejects `With` after an expression. |
| 8c | VB-031 | Done. On `vbnext`, `Record Class Point(X As Integer, Y As Integer)` stores each parameter in an init-only property. `New Point(1, 2)` constructs it, and `Dim (x, y) = point` deconstructs it. |
| 8d | VB-031 | Done. On `vbnext`, `Record Structure Point` emits value equality, `GetHashCode`, and `ToString` of the form `Point { X = 1, Y = 2 }`. `a With { .Y = 3 }` copies it and can assign an init-only property. Visual Basic 17.13 still rejects `Record` before `Structure`. |

## Phase 9 — Interfaces

| Slice | Id | Done when |
| --- | --- | --- |
| 9a | VB-038 | Done. On `vbnext`, an interface method may have a body. A class that implements the interface and does not supply the method uses that body. The class may still supply its own with `Implements`. A method without a body still must be implemented. Visual Basic 17.13 still rejects a body inside an interface. |
| 9b | VB-038 | Done. On `vbnext`, when a class implements a derived interface, the most specific default body is the implementation. A derived body covers an abstract base method. Two equally specific bodies still leave the base method unimplemented. |
| 9c | VB-039 | Done. On `vbnext`, `Extension(s As String)` ... `End Extension` inside a module may declare a property. `text.WordCount` calls that property with `text` as the receiver. Visual Basic 17.13 does not parse `Extension` as a block. |
| 9d | VB-039 | Done. On `vbnext`, an extension block may declare a parameterized property. `box(index)` calls it when the type has no default property of its own, and both the receiver and the index are passed. |
| 9e | VB-039 | Done. On `vbnext`, an extension block may declare a `Shared` method or property. `String.Twice("hi")` and `String.Marker` call those members. The receiver parameter is not passed. |
| 9f | VB-036 | Done. On `vbnext`, an interface may declare `Shared MustOverride Function`. A class supplies it with `Public Shared Function ... Implements`. Visual Basic 17.13 still rejects `Shared` on an interface method. |
| 9g | VB-036 | Done. On `vbnext`, `T.GetName()` calls a `Shared MustOverride` member when `T` is constrained to that interface. The call is emitted as `constrained.` followed by `call`. |

## Phase 10 — Nullability

The spelling is locked in `docs/vbnext-vb030-syntax.md`. `?` on a value type remains `Nullable(Of T)`.

| Slice | Id | Done when |
| --- | --- | --- |
| 10a | VB-030 | Done. `docs/vbnext-vb030-syntax.md` locks `?` as the reference annotation and `/nullable:` as the project switch. `Integer?` remains `Nullable(Of Integer)`. The default switch is `disable`. |
| 10b | VB-030 | Done. On `vbnext`, `/nullable:enable` or `/nullable:annotations` stores an annotation on a local, a parameter, and a method return type. `As String?` stays `String` and is annotated. `As String` is not annotated. `Integer?` remains `Nullable(Of Integer)`. Visual Basic 17.13, and `vbnext` with annotations off, still reject `String?`. No warnings yet. |
| 10c | VB-030 | Done. On `vbnext`, `/nullable:enable` warns when Nothing reaches a non-nullable local, parameter, return, or member access. `If x IsNot Nothing` treats `x` as not null in that branch. `#Nullable Enable|Disable|Warnings|Annotations|Restore` overrides the project switch for the rest of the file. Visual Basic 17.13 still reports `#Nullable` as an unrecognized directive. |
| 10d | VB-030 | Done. On `vbnext`, a method whose return type and parameters carry annotations emits `NullableContextAttribute` when every flag agrees, and `NullableAttribute` with one byte per return value and parameter when they differ. `1` is not annotated and `2` is annotated. Annotations off emits neither attribute. |
| 10e | VB-030 | Done. On `vbnext`, `/nullable:annotations` stores a class-constrained `T?` as `T`. `List(Of String?)` records the type-argument annotation, `List(Of String)?` annotates the list, and `String?()` is an array of nullable strings. An overriding method keeps the annotations written on its signature. A structure-constrained `T?` stays `Nullable(Of T)`. An unconstrained `T?`, and any of these forms with annotations off, still report the structure-constraint error. |

## Phase 11 — List patterns

Deferred from slice 5h until collection expressions existed. The spelling is in `docs/vbnext-vb004-syntax.md`. Braces are the list pattern. Parentheses stay positional.

| Slice | Id | Done when |
| --- | --- | --- |
| 11a | VB-004 | Done. On `vbnext`, `Case Is {1, 2}` matches a one-dimensional array of that length, and `Case Is {}` matches an empty array. The array is evaluated once. A value that is not a one-dimensional array is an error. Visual Basic 17.13 still reports an expected relational operator. |
| 11b | VB-004 | Done. On `vbnext`, `Case Is {1, 2}` matches a `List(Of T)` whose count is 2 and whose indexer returns those values. `Case Is {}` matches an empty list. The list is evaluated once. A value that is neither a one-dimensional array nor a collection with a count and an indexer is still an error. |

## Phase 12 — TypeOf lists

Pulled in from wave 4. The spelling is in `docs/vbnext-vb054-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 12a | VB-054 | Done. On `vbnext`, `TypeOf value Is {Customer, Order}` is true when the value is either type, and `TypeOf value IsNot {Customer, Order}` is true when it is neither. The value is evaluated once. Visual Basic 17.13 still rejects the brace list. |

## Phase 13 — GUID literals

Pulled in from wave 4. The spelling is in `docs/vbnext-vb052-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 13a | VB-052 | Done. `docs/vbnext-vb052-syntax.md` locks `&G{936DA01F-9ABD-4d9d-80C7-02AF85C822A8}` and the compact `&G{936DA01F9ABD4d9d80C702AF85C822A8}`. The value's type is `System.Guid`. `#` stays a date, and square brackets stay escaped identifiers. |
| 13b | VB-052 | Done. On `vbnext`, `&G{936DA01F-9ABD-4d9d-80C7-02AF85C822A8}` and `&G{936DA01F9ABD4d9d80C702AF85C822A8}` are `System.Guid` values. A malformed literal is an error. `&Goo` stays concatenation. Visual Basic 17.13 still treats `&G{...}` as concatenation. |

## Phase 14 — For Each index

Pulled in from wave 4. The spelling is in `docs/vbnext-vb055-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 14a | VB-055 | Done. `docs/vbnext-vb055-syntax.md` locks `For Each item With index In items`. The index is an `Integer` that starts at 0. |
| 14b | VB-055 | Done. On `vbnext`, `For Each item With index In items` numbers each item from 0, and `Continue For` still advances the index. The index can live across an `Await`, including in a Debug build. A name already used by the loop variable or by an enclosing local or parameter is an error. Visual Basic 17.13 still expects `In`. |

## Phase 15 — Optional End Namespace

Pulled in from wave 4. The spelling is in `docs/vbnext-vb059-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 15a | VB-059 | Done. `docs/vbnext-vb059-syntax.md` locks a namespace that runs to the end of the file. A second `Namespace` before `End Namespace` still nests. |
| 15b | VB-059 | Done. On `vbnext`, a namespace with no `End Namespace` runs to the end of the file. A second `Namespace` still nests. `End Namespace` remains valid. Visual Basic 17.13 still expects `End Namespace`. |

## Phase 16 — With variable

Pulled in from wave 4. The note is in `docs/vbnext-vb053-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 16a | VB-053 | Done. `docs/vbnext-vb053-syntax.md` locks `With Dim name = expression` and `With Dim name As Type = expression`. `With X = expr` stays an equality test. |
| 16b | VB-053 | Done. On `vbnext`, `With Dim name = expression` and `With Dim name As Type = expression` declare a local scoped to the block and use it as the `With` object. The initial value binds against an enclosing `With`. `With X = expr` stays an equality test. Visual Basic 17.13 still reports an expected expression after `With`. |

## Phase 17 — For Each ByRef

Pulled in from wave 4. The spelling is in `docs/vbnext-vb056-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 17a | VB-056 | Done. `docs/vbnext-vb056-syntax.md` locks `For Each ByRef item In values`. The loop variable refers to each element of a one-dimensional array. |
| 17b | VB-056 | Done. On `vbnext`, `For Each ByRef item In values` writes each element of a one-dimensional array. An existing variable, a collection that is not that array, and a lambda capture are errors. Visual Basic 17.13 still rejects `ByRef`. |

## Phase 18 — Compact members

Pulled in from wave 4. The note is in `docs/vbnext-vb050-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 18a | VB-050 | Done. `docs/vbnext-vb050-syntax.md` locks `= Get expression` on a read-only property, `= Return expression` on a `Function`, and `= Call invocation` on a `Sub`. `ReadOnly Property P As T = expr` stays an auto-property initializer. |
| 18b | VB-050 | Done. On `vbnext`, `ReadOnly Property P As T = Get expr`, `Function F() As T = Return expr`, and `Sub S() = Call M()` compile to a `Get` block, a function body, and a sub body. The compact body comes after any `Implements` or `Handles` clause, and an `Async` function can `Await` in it. A keyword that belongs to another member kind reports BC37386. Visual Basic 17.13 still reports an expected end of statement after the header. |

## Phase 19 — Unchecked arithmetic

Pulled in from wave 4. The spelling is in `docs/vbnext-vb057-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 19a | VB-057 | Done. `docs/vbnext-vb057-syntax.md` locks `Unchecked(expression)`. `Unchecked` stays an identifier, and the operator applies only when that name refers to nothing in scope. The `Option` and block forms are not locked. |
| 19b | VB-057 | Done. On `vbnext`, `Unchecked(expression)` wraps integer overflow at run time and in constant expressions. A symbol named `Unchecked` keeps its meaning. Visual Basic 17.13 still reports that `Unchecked` is not declared. |

## Phase 20 — Concise lambdas

Pulled in from wave 4. The note is in `docs/vbnext-vb051-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 20a | VB-051 | Done. `docs/vbnext-vb051-syntax.md` records that `_`, `$1`, `Fn`, and a leading `.` were left open. `Function(c) c.Name` keeps its meaning. |

VB-051 is deferred. There is no implementation slice.

## Phase 21 — XML statement blocks

Pulled in from wave 4. The spelling is in `docs/vbnext-vb060-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 21a | VB-060 | Done. `docs/vbnext-vb060-syntax.md` locks a statement block inside `<%= %>` that starts with a statement keyword. `Return` and `Yield` are scoped to the block. `Let` and `Set` assignment forms are not locked. |
| 21b | VB-060 | Done. On `vbnext`, a statement block inside `<%= %>` produces the value of its `Return`, or a sequence of its `Yield` values. Visual Basic 17.13 still expects an expression. |

## Phase 22 — Sub chains

Pulled in from wave 4. The note is in `docs/vbnext-vb058-syntax.md`.

| Slice | Id | Done when |
| --- | --- | --- |
| 22a | VB-058 | Done. `docs/vbnext-vb058-syntax.md` records that `F(a)(b)`, `: (b)`, and `Call F With (a), (b)` were left open. |

VB-058 is rejected. There is no implementation slice.

## Phase 23 — IDE

The editor work for the syntax above. Tests live under `src/EditorFeatures/VisualBasicTest/` and `src/Features/VisualBasicTest/`, with `vbnext` parse options.

| Slice | Area | Done when |
| --- | --- | --- |
| 23a | End constructs | Done. On `vbnext`, Enter after `Extension(s As String)` inserts `End Extension`, and Enter after `Dim x = Select Case value` inserts `End Select`. `Record Class` and `Record Structure` get `End Class` and `End Structure`. A compact member gets no `End`, and Enter after a `Using` statement inserts no `End Using`, because the `Using` is a declaration. Visual Basic 17.13 still inserts `End Using`. An unfinished Select Case expression ends at its own line instead of taking the `End` of the enclosing block. The IDE layers build against the `vbnext` syntax API. |
| 23b | Classification | Done. On `vbnext`, `InitOnly`, `Required`, `Record`, and `Extension` classify as keywords. A GUID literal classifies as a number instead of stopping classification for the file. Raw strings and UTF-8 strings classify as strings, and `..` and `?=` classify as operators. |
| 23c | Keyword completion | Done. On `vbnext`, completion offers `InitOnly` and `Required` where a property can start, `Record` where a class or structure can start, and `Extension` at the start of a module member. After `Record` it offers only `Class` and `Structure`, and after `InitOnly` or `Required` it offers `Property`. Visual Basic 17.13 does not offer them. |
| 23d | Outlining and indentation | Done. On `vbnext`, extension blocks and Select Case expressions collapse like the blocks they resemble. Formatting indents extension members one level and lays out a Select Case expression like a Select Case statement, with `End Select` under `Select`. Enter inside either block indents to the member, arm, or arm value level. A Select Case expression keeps blank and comment lines between its arms instead of ending at the first one. |
| 23e | Pretty listing | Done. On `vbnext`, committing a line pretty lists compact members like other members. Statements after a `Using` with no `End Using`, and members of a namespace that runs to the end of the file, stay at the level of the header. Visual Basic 17.13 still indents after a `Using` with no `End Using`. Inside an XML statement block, committing a line keeps the statements and adds no parentheses, and the block's body indents from its first statement. |

## Phase 24 — Real projects

A project outside this repo compiles with the Vb.Next compiler. The steps are in [vbnext-toolset.md](vbnext-toolset.md).

| Slice | Area | Done when |
| --- | --- | --- |
| 24a | Toolset | Done. `vbnext/VbNext.Toolset.props` points the SDK's `Vbc` task at the `vbc.exe` built from this repo. `vbnext/samples/HelloVbNext` builds with `dotnet build` and runs a record, an extension property, a compact member, a Select Case expression, an XML statement block, and a `Using` declaration. The sample also builds with every .NET analyzer on. |
| 24b | Analyzers and IOperation | Done. Analyzers run on `vbnext` code, and the toolset leaves them on. A sequence, a from-end index, a range, and an array length have an operation. Nodes that a binder synthesizes from the user's syntax are compiler generated, so each piece of syntax has one explicit operation. A Select Case pattern's value is its whole test. A local declared by `With Dim` or tuple deconstruction has an operation without a declarator. A record `With` copy uses only syntax from the compilation. A `For Each` index binds in a semantic model. `VbNextOperationTests.vb` builds the operation tree and the control flow graph for each construct. Every `vbnext` compiler test passes with `ROSLYN_TEST_IOPERATION` set: a block declares the temporaries of the sequences in its statements, `Dim (x, y)` declares `x` and `y` in its statement list like `Dim x, y`, and the control flow graph verifier knows a `With Dim` target and a collection expression the way it knows `With expression` and `From { }`. |
| 24c | Editor in Visual Studio | Done. The language service, the out-of-process analysis host, and the compiler extension built from this repo deploy into the `RoslynDev` hive of Visual Studio 2026 Insiders 18.12, with the steps in [vbnext-toolset.md](vbnext-toolset.md). Visual Studio 2026 18.10 could not load them, because the repo builds against Visual Studio 18.12 packages. `HelloVbNext` opens in that instance with no squiggles or errors on `vbnext` syntax, and the .NET analyzers report on it, such as CA1861 for a constant array argument. |

## Phase 25 — Runtime interop

Wave 5 rows 1 and 2 in [vbnext-missing-features.md](vbnext-missing-features.md#wave-5-order). Neither adds syntax. Tests use the real .NET types.

| Slice | Id | Done when |
| --- | --- | --- |
| 25a | VB-064 | Done. On `vbnext`, `SyncLock` on a `System.Threading.Lock` enters through `Lock.EnterScope` and leaves by disposing the scope, on normal exit, on `Exit Sub`, and when the body throws. The IL matches the C# `lock` statement. A `Yield` inside such a block reports BC37052, because the scope cannot cross it, and a Debug build of an `Async` method does not hoist the scope. A `Lock` without the expected members, and Visual Basic 17.13, still report BC37329. Converting a `Lock` to `Object` still warns with BC42508. |
| 25b | VB-065 | Done. On `vbnext`, an interpolated string converts to a type with the interpolated string handler attribute. The handler is constructed with the literal length and hole count, then its `AppendLiteral` and `AppendFormatted` calls run in order, with `alignment` and `format` passed by name. A constructor that ends with a `ByRef Boolean` can skip the appends, and `Boolean` appends stop at the first `False`. Overload resolution prefers a handler parameter to `String` for a string with holes, and a constant string still picks `String`. Arguments named by the handler argument attribute, and `""` for the instance, are evaluated once, in order, and passed to the constructor. A name that is not the instance or an earlier `ByVal` parameter reports BC37387. Tests call `Debug.Assert` and `StringBuilder.Append` from .NET 9, `DefaultInterpolatedStringHandler`, and custom handlers. Visual Basic 17.13 still reports BC30311. |

## Phase 26 — Asynchronous streams

Wave 5 rows 3 to 5. The spelling comes first, because all three forms share it.

| Slice | Id | Done when |
| --- | --- | --- |
| 26a | VB-061, VB-062, VB-063 | Done. [vbnext-vb061-syntax.md](vbnext-vb061-syntax.md) locks `Async Using` for asynchronous disposal, `Await Each ... Next` for asynchronous iteration, with `Exit For` and `Continue For`, and `Async Iterator Function` for async iterators. `Await Using` and `Await For Each` are not adopted. No code change. |
| 26b | VB-061 | Done. On `vbnext`, `Async Using` awaits `DisposeAsync` on a resource that implements `IAsyncDisposable`, in a block and in a `Using` declaration, with several resources released innermost first and a resource of `Nothing` skipped. The resource is released when the body completes, throws, returns a value, or leaves through `Exit` or `Continue`, and an exception keeps its stack trace. Because Visual Basic cannot await in `Finally`, lowering catches the exception, awaits the release after the `Try`, and then rethrows or continues the jump. Outside an `Async` method it reports BC37389, a resource that is not `IAsyncDisposable` reports BC37390, and inside `Catch`, `Finally`, or `SyncLock` it reports BC36943. The operation is an asynchronous `IUsingOperation`. Visual Basic 17.13 reports that `Async Using` is not available. |
| 26c | VB-062 | Done. On `vbnext`, `Await Each ... Next` iterates an `IAsyncEnumerable(Of T)`: it calls `GetAsyncEnumerator`, awaits `MoveNextAsync` for each item, reads `Current`, and awaits the enumerator's `DisposeAsync` when the loop ends, including on `Exit For`, `Return`, and exceptions. `Continue For` and `With index` work as in `For Each`, and a declared loop variable compiles under `Option Strict On` and `Option Infer Off`. The release uses the slice 26b lowering. The statement is a `ForEachStatementSyntax` whose first keyword is `Await` rather than `For`, and its operation is an asynchronous `IForEachLoopOperation`. Outside an `Async` method it reports BC37391, a collection that is not `IAsyncEnumerable(Of T)` reports BC37392, and inside `Catch`, `Finally`, or `SyncLock` it reports BC36943. Visual Basic 17.13 reports that `Await Each` is not available. |
| 26d | VB-063 | Done. On `vbnext`, an `Async Iterator Function` returning `IAsyncEnumerable(Of T)` or `IAsyncEnumerator(Of T)` can both `Await` and `Yield`, and the slice 26c loop consumes it. The state machine follows the C# design: a class driven by `AsyncIteratorMethodBuilder` that returns `MoveNextAsync` and `DisposeAsync` as `ValueTask`s backed by `ManualResetValueTaskSourceCore(Of Boolean)`. `Return` ends the iteration. When the consumer stops early, for example with `Exit For`, `DisposeAsync` resumes the function in a disposal mode that runs its `Finally` blocks and releases its `Async Using` resources and `Await Each` enumerators. `GetAsyncEnumerator` on the creating thread reuses the instance, and each enumeration starts from the original argument values. A `CancellationToken` parameter marked `<EnumeratorCancellation>` receives the token passed to `GetAsyncEnumerator`, linked with the argument when both are given. Another return type reports BC37393, and Visual Basic 17.13 keeps BC36936. More than one `EnumeratorCancellation` parameter reports BC37398, a `CancellationToken` parameter without it reports warning BC42514, and the attribute where it has no effect reports warning BC42515. Edit and Continue treats the function as both async and an iterator, and counts `Await Each` and `Async Using` as suspension points. Async iterator lambdas are not part of this slice. |

## Phase 27 — Pattern variables

Wave 5 row 6. Extends the `Select Case` patterns of phase 5 and phase 11.

| Slice | Id | Done when |
| --- | --- | --- |
| 27a | VB-066 | Done. [vbnext-vb066-syntax.md](vbnext-vb066-syntax.md) locks `Into` after the tested type, as in `Case Is TypeOf Customer Into c` and `If TypeOf value Is Customer Into c Then`. In `Select Case` the name is in scope in that `Case` body. In `If` it is in scope in the rest of its `AndAlso` chain and in that `Then` block, not in `ElseIf`, `Else`, or after `End If`. `Case c As Customer`, the C# order `Is Customer c`, `Dim c` after the type, and narrowing the tested variable are not adopted. `IsNot` with `Into` is not locked. No code change. |
| 27b | VB-066 | Done. On `vbnext`, `Case Is TypeOf T Into name` and, in the condition of an `If` or `ElseIf`, `TypeOf value Is T Into name` declare a local of the tested type. The value is evaluated once, a value type is unboxed into the local, and the local can be assigned. It is definitely assigned in that `Case` body, in the rest of the `AndAlso` chain, and in that `Then` block, and it is out of scope in other `Case` blocks, in a later `ElseIf`, in `Else`, and after `End If`. Positional tests and member filters after the `Case` test read the local, a query inside the condition keeps its own `Into`, and the forms compile under `Option Strict On` and `Option Infer Off`. Another clause in the same `Case` reports BC37395, `Into` with `IsNot`, a type list, `Not`, `OrElse`, or in a `Select Case` expression reports BC37396, and a name that repeats or hides another local or a parameter reports the existing errors. The operation is an `IIsPatternOperation` with a declaration pattern. Visual Basic 17.13 reports that `Into` after a type test is not available. |

## Phase 28 — Property backing fields

Wave 5 row 7.

| Slice | Id | Done when |
| --- | --- | --- |
| 28a | VB-067 | Done. [vbnext-vb067-syntax.md](vbnext-vb067-syntax.md) locks `Field` for storage that the compiler declares for a property with a `Get` or `Set` body, as in `Set : Field = Value : End Set`. An existing local, parameter, member, or type named `Field` keeps its meaning, so code that compiles today does not change. The storage has a name that code cannot write, so it is not the `_Name` field of an auto-implemented property and never conflicts with a member. The implicit `_Name` of vblang #196, `WithBacking _name`, and the property's own name are not adopted. No code change. |
| 28b | VB-067 | Done. On `vbnext`, `Field` in the `Get` or `Set` body of a property reads and writes storage of the property's type that the compiler declares, including in a `ReadOnly` `Get`, a `WriteOnly` `Set`, a `Shared` property, a `Structure`, and a lambda in an accessor, and under `Option Strict On`, `Option Infer Off`, and `Option Explicit Off`. A local, parameter, member, inherited member, or type named `Field` keeps its meaning, and `[Field]` is an ordinary name. The storage is a `Private` field named `<Name>k__BackingField`, so a type can still declare `_Name`. `Field` in a property with parameters reports BC37397. `Field` outside a property body, and on Visual Basic 17.13, is an undeclared name. |

## Phase 29 — IDE for wave 5

| Slice | Area | Done when |
| --- | --- | --- |
| 29a | Editor | Done. On `vbnext`, Enter after `Await Each item In source` inserts `Next`, and after `Async Iterator Function` inserts `End Function`. Enter after `Async Using` inserts no `End Using`, because the statement is a declaration, as `Using` is from slice 23a. `Async`, `Await`, `Each`, and `Into` classify as keywords, and `Field` in a property body classifies as a keyword. Completion offers `Each` after an `Await` that starts a statement, `Using` after an `Async` that starts a statement, `Iterator` after `Async` and `Async` after `Iterator`, `Into` after the type of `Case Is TypeOf` and of a `TypeOf ... Is` that names its result in an `If` or `ElseIf` condition, and `Field` in the `Get` and `Set` bodies of a property without parameters. Visual Basic 17.13 does not offer them. Formatting and smart indentation indent the bodies of `Async Using` and `Await Each` and the `Case` and `Then` blocks that use `Into`. |

## Phase 30 — Configured asynchronous streams

Wave 6 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 30a | VB-068 | Done. On `vbnext`, `Await Each` iterates a collection by pattern first, as C# does: an accessible `GetAsyncEnumerator` function that can be called without arguments, whose result has a `MoveNextAsync` function that awaits to `Boolean` and a readable `Current` property. `ConfiguredCancelableAsyncEnumerable(Of T)` matches, so `Await Each n In source.WithCancellation(token)` passes the token to the iterator and `source.ConfigureAwait(False)` resumes without the captured context. The enumerator is released through `IAsyncDisposable` when it implements it, through its own awaitable `DisposeAsync` otherwise, and not at all when it has neither. Members that return `Task` and `Task(Of Boolean)` also match. A collection that matches neither the pattern nor `IAsyncEnumerable(Of T)` still reports BC37392. |
| 30b | VB-068 | Done. On `vbnext`, `Async Using` releases a resource that does not convert to `IAsyncDisposable` by pattern, as C# does: an accessible `DisposeAsync` function that can be called without arguments and is awaited, as `ConfiguredAsyncDisposable` from `ConfigureAwait(False)` has. `Async Using stream.ConfigureAwait(False)` releases the stream without the captured context. A structure is released without a `Nothing` check, a class with one. A resource that implements `IAsyncDisposable` is unchanged, and a resource with neither still reports BC37390. The operation reports the `DisposeAsync` that is called. |

## Phase 31 — Await in Catch and Finally

Wave 6 row 2. No new syntax. [vblang #37](https://github.com/dotnet/vblang/issues/37).

| Slice | Id | Done when |
| --- | --- | --- |
| 31a | VB-069 | Done. On `vbnext`, `Await` in a `Catch` block of an `Async` method or lambda awaits, and the exception variable keeps its value across the `Await`. The `Catch` body runs after the protected region, as C# lowers it, so a bare `Throw` rethrows the caught exception with its original stack trace, `When` filters and several handlers still choose the handler, a `Finally` runs after the `Catch` body, and `Exit Try`, `Return`, and nested `Try` blocks behave as before. `GoTo` from the `Catch` back into its `Try` still works. `Err` is set when the `Catch` body starts and cleared when it ends; after an `Await` it follows the thread the method resumes on, as it does after any `Await`. `Await` in a `When` filter still reports BC36943. Visual Basic 17.13 still reports BC36943. |
| 31b | VB-069 | Done. On `vbnext`, `Await` in a `Finally` block awaits, and the `Finally` still runs when the `Try` completes, throws, or leaves through `Exit`, `Continue`, `Return`, or `GoTo`. The `Finally` body runs after the protected region, as the slice 26b `Async Using` lowering releases its resource: an exception from the `Try` or a `Catch` is rethrown after the `Finally` with its original stack trace, a `Return` value is computed before the `Finally` runs, and nested `Try` blocks and `Await` in a `Catch` of the same `Try` work together. Disposing an `Async Iterator` while it is suspended in the `Try` runs the `Finally`. `Await` in `SyncLock` still reports BC36943. Visual Basic 17.13 still reports BC36943. |

## Phase 32 — Task-like returns

Wave 6 row 3. No new syntax. [dotnet/roslyn #72241](https://github.com/dotnet/roslyn/issues/72241).

| Slice | Id | Done when |
| --- | --- | --- |
| 32a | VB-070 | Done. On `vbnext`, an `Async Function` or async lambda returns `ValueTask` or `ValueTask(Of T)` and builds the result with the builder that the return type names in its `AsyncMethodBuilderAttribute`, which is `AsyncValueTaskMethodBuilder`, so it can implement `IAsyncDisposable.DisposeAsync`. A `Return` in it takes the same operand as in a function that returns `Task` or `Task(Of T)`, a lambda converts to a delegate that returns either type, and a method that completes without suspending returns a completed `ValueTask`. Visual Basic 17.13 still reports BC36945. |
| 32b | VB-070 | Done. On `vbnext`, an `Async Function` or async lambda returns any type that names its builder with `AsyncMethodBuilderAttribute`, as C# does: a type without type parameters names a builder without them, a type with one names an open generic builder with one, and the builder has the same accessibility as the type. `AsyncMethodBuilderAttribute` on an `Async Function` chooses its builder instead, for any return type an `Async Function` can return. A builder needs a `Public Shared Function Create()` that returns it and a `Public` property `Task` of the return type, or BC37402 names it; the other builder members are found by name, as they are for the built-in builders. BC37399 replaces BC36945 on `vbnext` and names task-like types, and for a return type other than `Task` and `Task(Of T)`, BC37400 and BC37401 replace BC36952 and BC37055 and name the actual return type. Visual Basic 17.13 still reports BC36945 and ignores the attribute on a method. |

## Phase 33 — Async lambdas and guards

Wave 6 rows 4 and 5.

| Slice | Id | Done when |
| --- | --- | --- |
| 33a | VB-071 | Done. `docs/vbnext-vb071-syntax.md` locks `Async Iterator Function(...) ... End Function` as a multi-line lambda, in the same modifier order as the method form, and infers `IAsyncEnumerable(Of T)` from the `Yield` operands when there is no target delegate type or `As` clause. No code change. |
| 33b | VB-072 | Done. `docs/vbnext-vb072-syntax.md` locks `If TypeOf value IsNot T Into name Then ...` as the whole condition of a single-line or block `If` with no `Else` or `ElseIf`, whose `Then` part always leaves. The name is in scope from after the `If` to the end of the enclosing block. No code change. |
| 33c | VB-071 | Done. On `vbnext`, a multi-line `Async Iterator Function` lambda both awaits and yields, using the slice 26d lowering. Without a target delegate type or an `As` clause it infers `IAsyncEnumerable(Of T)` from its `Yield` operands, or `IAsyncEnumerable(Of Object)` without one, and generic type inference digs into it as it does into `IEnumerable(Of T)`. A target delegate or `As` clause that returns `IAsyncEnumerable(Of T)` or `IAsyncEnumerator(Of T)` supplies the type, and any other return type, or `Async Iterator Sub`, reports BC37393. The lambda captures locals, and `Try`, `Finally` with `Await`, and `Exit For` from `Await Each` work in it. Visual Basic 17.13 still reports BC36936. |
| 33d | VB-072 | Done. On `vbnext`, `If TypeOf value IsNot T Into name Then ...`, on a single line or in a block and through parentheses, declares a name that is in scope from the statement after the `If` to the end of the enclosing block, including nested blocks and lambdas there, and is definitely assigned there. It is not in scope before the `If`, in its condition, or in its `Then` part. A `Then` part whose last statement is not `Return`, `Exit`, `Continue`, `Throw`, or `GoTo` reports BC37403, and an `Else` or `ElseIf` reports BC37404. A name that an enclosing block or a later statement in the block also declares reports the usual hiding or duplicate error, and `IsNot ... Into` elsewhere, such as in an `ElseIf`, still reports BC37396. The semantic model finds the name after the guard. |

## Phase 34 — IDE for wave 6

| Slice | Area | Done when |
| --- | --- | --- |
| 34a | Editor | Done. On `vbnext`, completion offers `Iterator` after an `Async` that starts a lambda, beside `Function` and `Sub`, and `Into` after the type of a `TypeOf ... IsNot` that is the whole condition of an `If`, through parentheses, but not in an `ElseIf` or an `AndAlso` chain. Visual Basic 17.13 does not offer them. Enter after `Async Iterator Function()` in an expression inserts `End Function`, `Async`, `Iterator`, and `Into` classify as keywords, and smart indentation indents the lambda body and the `Then` block of a guard and keeps the statement after a single-line guard at the block's indentation. |

## Phase 35 — Read-only references

Wave 7 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 35a | VB-073 | Done. On `vbnext`, a property, indexer, or function from metadata that returns `ref readonly T` reads as a value of type `T`, so `span(i)` on a `ReadOnlySpan(Of T)`, `chars(0)` on a `ReadOnlySpan(Of Char)`, and `For Each` over a `ReadOnlySpan(Of T)`, whose enumerator's `Current` returns `ref readonly T`, compile and read the element. The result is a value, not a variable: assigning to it reports BC30526 or BC30068, passing it `ByRef` passes a copy, and calling a method on a structure it returns calls it on a copy, as C# does. A delegate whose `Invoke` returns `ref readonly` is still unsupported, because a lambda cannot return `ByRef`. Visual Basic 17.13 still reports BC30643 for such a property and BC30657 for such a method, and overload resolution still skips them there. |

## Phase 36 — Using on ref structures

Wave 7 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 36a | VB-074 | Done. On `vbnext`, the block and declaration forms of `Using`, with an expression or a declared variable, release a ref structure that has an accessible `Dispose` `Sub` that can be called without arguments, as C# does, so `Using gate.EnterScope()` on a .NET 9 `Lock` exits the lock when the block ends, including when it ends with an exception. The operation reports the `Dispose` that is called. A ref structure without such a `Dispose`, and any other resource that does not implement `IDisposable`, still reports BC36010, and Visual Basic 17.13 still reports BC36010 for a ref structure. |

## Phase 37 — Ref structures in async code

Wave 7 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 37a | VB-075 | Done. On `vbnext`, an `Async` method or lambda declares a local of a ref structure type, such as `Span(Of T)` or `ReadOnlySpan(Of Char)`, uses it, and iterates it with `For Each`, when no `Await` falls between an assignment of the local and a read of it, in Release and Debug builds. A local whose value is live across an `Await`, the enumerator of a `For Each` over a span whose body awaits, and a ref structure value that an expression would have to keep while it awaits, as in `span.Slice(Await F())`, report BC37405 where the value is used. The scope of a `SyncLock` on a `Lock` that is held across a `Yield` reports BC37405 too, in place of BC37052. Visual Basic 17.13 still reports BC37052 at the declaration. |
| 37b | VB-075 | Done. On `vbnext`, the rule from 37a applies to `Yield` in an `Iterator` or `Async Iterator` method or lambda: a span local whose value is not live across a `Yield` or `Await` compiles and runs in Release and Debug builds, and one that is live across a `Yield`, or the enumerator of a `For Each` over a span whose body yields, reports BC37405. Visual Basic 17.13 still reports BC37052. |

## Phase 38 — Lambda parameter attributes

Wave 7 row 4.

| Slice | Id | Done when |
| --- | --- | --- |
| 38a | VB-076 | Done. On `vbnext`, attributes on the parameters of a lambda bind with the lambda, report their errors there, such as BC30002 for an undefined attribute and BC30662 for one that is not valid on a parameter, and are emitted on the parameters of the method the lambda becomes, so reflection over the delegate's `Method`, as an ASP.NET Core minimal API handler uses, sees them. This holds for `Function` and `Sub` lambdas, for a parameter whose type comes from the target delegate, and for a lambda that captures locals. The semantic model reports them on the lambda parameter. Attributes that change how a call binds, such as `<ParamArray>` and caller information, are not covered. Visual Basic 17.13 still reports BC36634. |
| 38b | VB-076 | Done. On `vbnext`, `<EnumeratorCancellation>` on a `CancellationToken` parameter of an `Async Iterator Function` lambda that returns `IAsyncEnumerable(Of T)` receives the token passed to `GetAsyncEnumerator`, combined with the argument as on a method, so `WithCancellation` cancels the lambda's loop. Two marked parameters report BC37398, and the attribute on any other parameter, or on a lambda that is not an `Async Iterator` returning `IAsyncEnumerable(Of T)`, reports BC42515. |

## Phase 39 — Deconstructing loops

Wave 7 row 5.

| Slice | Id | Done when |
| --- | --- | --- |
| 39a | VB-077 | Done. `docs/vbnext-vb077-syntax.md` locks `For Each (a, b) In collection`, where each name follows the `For Each` control variable rule, so a new name is declared and an existing local is assigned, each name may have its own `As` clause, and `Await Each (a, b) In source` takes the same form. No code change. |
| 39b | VB-077 | Done. On `vbnext`, `For Each (a, b) In collection` deconstructs each item, from the elements of a tuple or the `ByRef` parameters of a `Deconstruct` method, as slices 5a to 5d do, so `For Each (item, price) In prices` over a `Dictionary` works through `KeyValuePair.Deconstruct`. Under `Option Infer On` a name that is not already a variable is declared with the type of its element, a name that is already a variable is assigned, and a name with an `As` clause is declared with that type, the element converting to it. Each iteration has its own names, so a lambda captures that iteration's values. An item that is not a tuple of that many elements and has no such `Deconstruct` reports BC37363, a name with an `As` clause that hides a variable reports BC30616, and a new name under `Option Infer Off` reports BC30451. The semantic model gives each name its type. Visual Basic 17.13 parses the list as a tuple expression, as before. |
| 39c | VB-077 | Done. On `vbnext`, `Await Each (a, b) In source` deconstructs each item of an `IAsyncEnumerable(Of T)` as slice 39b does, and the names keep their values across an `Await` in the body, in Debug and Release builds. A deconstruction statement from slices 5a to 5d in an `Async` method, which tripped a compiler assertion when its temporaries were kept across an `Await`, now compiles. |

## Phase 40 — IDE for wave 7

| Slice | Area | Done when |
| --- | --- | --- |
| 40a | Editor | Done. On `vbnext`, completion offers `In` after the closing parenthesis of `For Each (a, b)` and `As` after a name in the list, and Visual Basic 17.13 does not offer `In` there. Enter after `For Each (a, b) In items` inserts `Next`, smart indentation indents the loop body, the names classify as a `For Each` control variable does, and their uses classify as locals. An attribute on a lambda parameter classifies as a class. Renaming a name the loop declares updates its uses and reports a conflict with a variable outside the loop, and Convert For Each to For is not offered for a deconstructing loop. |

## Phase 41 — Shared abstract implementations

Wave 8 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 41a | VB-078 | Done. On `vbnext`, a class or structure that supplies a `Shared MustOverride` member with `Public Shared Function ... Implements`, from an interface declared in VB or from .NET, such as `IParsable(Of T)`, is emitted with a method implementation record and a shared method that is not virtual, final, or a new slot, and the interface member is abstract, virtual, and shared, as .NET requires. .NET loads the type, a call through slice 9g reaches it, and the interface map of `IParsable(Of Money)` names `Money.Parse` and `Money.TryParse`. The tests run on .NET, not only on .NET Framework, which cannot load such a type. Calling `T.Parse` on a .NET interface through a type parameter still reports BC37314 until phase 42. |

## Phase 42 — Shared abstract access

Wave 8 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 42a | VB-079 | Done. On `vbnext`, a shared abstract member of a type parameter's interface constraint, from any assembly, is reached through the type parameter: `T.Zero` and `T.One` on `INumber(Of T)` read through `constrained.` calls to the getter, `T.Parse(text, provider)` on `IParsable(Of T)` calls through `constrained.`, and a shared abstract property with a setter, declared in C#, is assigned and read. Reaching such a member through the interface itself, as in `IShape(Of Circle).Create()`, reports BC37314 for an interface declared in VB too, as C# reports CS8926, because the runtime cannot call it there. `AddressOf T.Create` still reports BC37314. Visual Basic 17.13 still reports BC37314 for a member from another assembly and BC32098 for `T.Zero`. |
| 42b | VB-079 | Done. On `vbnext`, a shared virtual member with a default body, declared in C# and used both as written and as a structure overrides it, and generic shared members such as `T.CreateChecked(2)`, with `Of Integer` inferred, and `T.CreateSaturating(Of Long)(value)`, are called through `constrained.`, as is `T.IsEvenInteger(value)`. The rule from 42a already covered them, so this slice adds tests. Visual Basic 17.13 still reports BC32098. |
| 42c | VB-083 | Done. On `vbnext`, when a type parameter has no class constraint and its interface constraints declare instance methods of a name, the methods of that name in `Object`, or `ValueType` for a `Structure` constraint, join them as overloads, as in C#. So `value.ToString()` on a `T As INumber(Of T)` or `T As IFormattable` calls `Object.ToString` in place of reporting BC30455 for `IFormattable.ToString(format, formatProvider)`, and `value.ToString("N1", culture)` still calls the interface. A base method with the same signature as a constraint's method stays hidden, so `ToString()` declared by an interface constraint is still called through that interface. On a `T As IEquatable(Of T)`, `value.Equals(CObj(other))` now calls `Object.Equals(Object)`, an exact match, where Visual Basic 17.13 with `Option Strict Off` narrows the argument to `T` for `IEquatable(Of T).Equals`. Visual Basic 17.13 still reports BC30455. |

## Phase 43 — Generic math operators

Wave 8 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 43a | VB-080 | Done. On `vbnext`, an operand whose type is a type parameter offers the shared abstract operators that its interface constraints and their base interfaces declare, so `+`, `-`, `*`, `/`, `Mod`, unary `-` and `+`, `=`, `<>`, `<`, `>`, `<=`, `>=`, `And`, `Or`, `Xor`, `Not`, `<<`, and `>>` on a `T As INumber(Of T)`, `IBinaryInteger(Of T)`, or `IComparisonOperators(Of T, T, Boolean)` bind and are emitted as `constrained.` calls, and a generic `Sum`, `Average`, and `Larger` compile and run. An operator in an interface is now read from metadata as an operator when it targets `TSelf`, a type parameter constrained to the interface, and a shift operator may take its count as a type parameter, as C# reads them; because reading metadata does not depend on the language version, Visual Basic 17.13 now names such a member as an operator, as in `Widening Operator CType(x As T) As Integer` in BC37315, in place of `Function op_Implicit(x As T) As Integer`. Constraints that declare no such operator still report BC30452, and an `Object` operand with a type parameter that has no class constraint is still late bound. Visual Basic 17.13 still reports BC30452 and BC30487. |
| 43b | VB-080 | Done. On `vbnext`, compound assignment such as `total += value`, `-=`, `*=`, and `/=` on a type parameter uses the operators from 43a. With integer overflow checks on, as they are by default, `+`, `-`, `*`, `/`, and unary `-` from a constraint call the checked form that the same interface declares, such as `op_CheckedAddition`, so a generic sum of `Integer` or `Byte` values and negating `Integer.MinValue` throw `OverflowException` as `Integer` arithmetic does, and with checks off they wrap. Conversion operators of the constraints are not covered: no interface in the .NET 11 core libraries declares one, generic math converts through `CreateChecked`, `CreateSaturating`, and `CreateTruncating` from 42b, and a conversion operator on an interface declared in C# still reports BC30311. |

## Phase 44 — Writing through references

Wave 8 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 44a | VB-081 | Done. On `vbnext`, assigning to, compound assigning, and passing `ByRef` a `ByRef` property or indexer whose receiver is a value, such as `CollectionsMarshal.AsSpan(list)(0) = 5`, `CollectionsMarshal.AsSpan(list)(1) += 10`, `Increment(CollectionsMarshal.AsSpan(list)(2))`, `whole.Slice(1)(0) = 7`, and `whole.Slice(1)(1) *= 3`, write through the returned reference into the list or array, in Release and Debug builds. A property with a setter whose receiver is a value still reports BC30068, because the write would be lost, and so does a `ref readonly` return from phase 35, such as an element of `MemoryExtensions.AsSpan("ab")`. Visual Basic 17.13 still reports BC30068. |

## Phase 45 — ByRef locals

Wave 8 row 5.

| Slice | Id | Done when |
| --- | --- | --- |
| 45a | VB-082 | Done. On `vbnext`, `ByRef` before `Dim`, `Static`, or `Const` locals, which compiled and was ignored, reports BC30247, "'ByRef' is not valid on a local variable declaration", until slice 45c gives it a meaning. The same hole on other declarations is closed with their existing messages: a field reports BC30235, an event BC30243, a `Sub` or `Sub New` BC30242, a `Declare` BC30244, and an operator BC33013. `ByRef Function`, `ByRef ReadOnly Property`, and `ByRef Structure` keep their meaning from phase 7. Visual Basic 17.13 still reports BC30183 for `Dim ByRef`. |
| 45b | VB-082 | Done. `docs/vbnext-vb082-syntax.md` locks `Dim ByRef name [As type] = reference`. The initializer is required and must be a variable or a `ByRef` function, property, or indexer result, with no conversion, assignment always writes through and the local cannot refer to another variable later, `Dim ByRef ReadOnly` refers to a variable or a `ref readonly` result without allowing writes, a lambda cannot capture such a local, and in `Async` and `Iterator` methods it follows the phase 37 rule for span locals. No code change. |
| 45c | VB-082 | Done. On `vbnext`, `Dim ByRef name [As type] = reference` declares a local that refers to the array element, local, structure or class field, `ByRef Function` result, or `ByRef` indexer result it is initialized from, and reading, assigning, compound assigning, and passing it `ByRef` act on that variable, in Release and Debug builds, so `slot += 1` on a slot from `CollectionsMarshal.GetValueRefOrAddDefault` counts words in a dictionary and `head = 9` on `CollectionsMarshal.AsSpan(list)(0)` changes the list. A `ByRef` local may be initialized from another, and the semantic model reports it with `IsRef` and `RefKind.Ref`. A missing initializer, `As New`, a constant, a computed value, a property with a setter, and a `ReadOnly` field report BC37406, a type that differs from the referenced variable's reports BC37407, a lambda that uses the local reports BC37384, and `Static ByRef` keeps BC30247 from 45a. |
| 45d | VB-082 | Done. On `vbnext`, `Dim ByRef ReadOnly name [As type] = reference` refers to a variable, a `ReadOnly` field, another `ByRef ReadOnly` local, or a `ref readonly` result such as an element of `text.AsSpan()` or of a `ReadOnlySpan(Of Integer)` over an array, and reading it sees later writes to that variable, without copying it. Assigning or compound assigning it reports BC30064, and passing it `ByRef` passes a copy, as a `ReadOnly` field does. A plain `Dim ByRef` initialized from a `ReadOnly` field, a `ByRef ReadOnly` local, or a `ref readonly` result reports BC37406, and `ReadOnly` on a local without `ByRef` still reports BC30247. The semantic model reports `RefKind.RefReadOnly`. Referring to a `ReadOnly` field takes its address, as C# does for `ref readonly`, which PEVerify does not accept. |
| 45e | VB-082 | Done. On `vbnext`, a lambda that uses a `ByRef` local reports BC37384, from slice 45c. In an `Async` or `Iterator` method, a `Dim ByRef` or `For Each ByRef` local that is set and used between suspensions compiles and runs in Release and Debug builds, and one whose reference is live across an `Await` or `Yield` reports BC37408, "'ByRef' local cannot be used across an 'Await' or 'Yield'", where it is used after the suspension, as phase 37 does for span locals with BC37405. Setting the reference does not count as a use, and a Debug build no longer tries to keep such a local in the state machine. |

## Phase 46 — IDE for wave 8

| Slice | Area | Done when |
| --- | --- | --- |
| 46a | Editor | Done. On `vbnext`, completion offers `ByRef` after `Dim` and `ReadOnly` after `Dim ByRef` in a local declaration, but not after `Static` or `Const`, and Visual Basic 17.13 does not offer them. `Dim`, `ByRef`, and `ReadOnly` classify as keywords and the name as a local. Quick info shows `(local variable) ByRef first As Integer` and `ByRef ReadOnly`, because the symbol display now writes `ByRef` and `ByRef ReadOnly` for a `ByRef` local when its format asks for it; symbol display does not depend on the language version, so a C# `ref readonly` local shown through Visual Basic reads `ByRef ReadOnly r3 As R` with `IncludeRef`, in place of `r3 As R`. After `T.`, completion lists the shared abstract and shared virtual properties and methods of `T`'s interface constraints, and not their instance members, and Visual Basic 17.13 lists nothing there, as before. |

## Phase 47 — Shared virtual defaults

Wave 9 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 47a | VB-084 | Done. On `vbnext`, a class or structure that implements an interface does not have to supply a shared virtual member that has a default body, such as `op_CheckedAddition` on `IAdditionOperators(Of TSelf, TOther, TResult)` or a C# `static virtual` method, and BC37315 is reported only for the shared abstract members that it does not supply. A call through a type parameter reaches the default body when the type does not supply the member, and the type's own member when it does. Visual Basic 17.13 still reports BC37315 for both kinds. |

## Phase 48 — Shared property implementations

Wave 9 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 48a | VB-085 | Done. On `vbnext`, `Public Shared ReadOnly Property AdditiveIdentity As Meters Implements IAdditiveIdentity(Of Meters, Meters).AdditiveIdentity` supplies a shared abstract property from .NET, and a `Shared Property` with a setter supplies one declared in C# with `get` and `set`. Their accessors are emitted as slice 41a emits shared implementing methods, so `T.AdditiveIdentity` and `T.Count = 2` reach them through a type parameter on .NET. Visual Basic 17.13 still reports BC30505 and BC37315. |

## Phase 49 — Operator implementations

Wave 9 row 3.

| Slice | Id | Done when |
| --- | --- | --- |
| 49a | VB-086 | Done. `docs/vbnext-vb086-syntax.md` locks implicit implementation by signature: an operator declared in a class or structure supplies the same operator of every implemented interface whose parameter and return types match after substitution, with no new syntax, and is emitted with method implementation records. Checked forms come from the interfaces' default bodies, and `++`, `--`, and `>>>`, which VB cannot declare, are supplied with the slice 9f form, a `Shared` function that implements `op_Increment`, `op_Decrement`, or `op_UnsignedRightShift`. No code change. |
| 49b | VB-086 | Done. On `vbnext`, a `Shared Operator` in a structure or class supplies the operators of the same metadata name, parameter types, and return type that its implemented interfaces declare, as 49a locks, emitted with method implementation records. A VB `Money` structure with `+`, `=`, and `<>`, a `Shared ReadOnly Property AdditiveIdentity` from 48a, and a `Shared Function op_Increment` from slice 9f implements `IAdditionOperators`, `IAdditiveIdentity`, `IEqualityOperators`, and `IIncrementOperators`, works with a generic `Sum`, `a = b`, and `T.op_Increment` through type parameters on .NET, and checked addition runs through the interface's default body. An operator with another signature, such as `Money + Integer`, does not implement `+`, which still reports BC37315. Visual Basic 17.13 still reports BC37315 for the interface's operators. |

## Phase 50 — Ref fields

Wave 9 row 4.

| Slice | Id | Done when |
| --- | --- | --- |
| 50a | VB-087 | Done. `docs/vbnext-vb087-syntax.md` locks `Private ByRef _value As Integer` in a `ByRef Structure`, set only in its `Sub New` with `_value = ByRef value` from a `ByRef` parameter of that `Sub New`, an array element, a class field, or a `ByRef` result, while a plain assignment writes through. `Private ByRef ReadOnly` refers without allowing writes. A structure that holds a ref field cannot be returned or assigned to a `ByRef` parameter, and ref fields need .NET 7 or later. No code change. |
| 50b | VB-087 | Done. On `vbnext`, `ByRef` declares a ref field in a `ByRef Structure`, `field = ByRef reference` in its `Sub New` sets it from the sources 50a allows, and reading, assigning, compound assigning, and passing it `ByRef` act on the referenced variable, on .NET 7 or later. A ref field outside a `ByRef Structure` or on a `Shared` field (BC30235), on an older runtime (BC37409), `= ByRef` outside `Sub New` or on another field (BC37410), from another source (BC37411), and a type mismatch (BC37412) are errors. Ref fields from other assemblies, such as C# `ref` fields, are still reported as unsupported. |
| 50c | VB-087 | Done. On `vbnext`, `Private ByRef ReadOnly` declares a ref field that cannot be written through, set from a `ReadOnly` field of a class or a `ref readonly` result as well. Assigning or compound assigning it reports BC30064, passing it `ByRef` passes a copy, and only `Dim ByRef ReadOnly` can refer to it. A plain ref field set from a `ReadOnly` field or a `ref readonly` result reports BC37413. The field is emitted with `IsReadOnlyAttribute`, so C# reads it as `ref readonly`. A plain ref field is written through even when the structure that holds it is a value, such as a function result. Fields of a class may be `Shared`. |
| 50d | VB-087 | Done. On `vbnext`, a structure that holds a ref field, by declaring one or through an instance field of such a structure, cannot be the return type of a `Function`, operator, or property (BC37414), or be assigned to a `ByRef` parameter or to a field of a structure reached through a `ByRef` parameter or `Me` (BC37415), as 50a locks. Only ref fields declared in Visual Basic count, so `Span(Of T)` can still be returned. A `Dim ByRef` local that refers to such a structure is not covered by the note and is not checked. |

## Phase 51 — Inline arrays

Wave 9 row 5. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 51a | VB-088 | Done. On `vbnext`, an element of a structure marked `<InlineArray(n)>` can be read and assigned with `buffer(i)`, the structure converts to `Span(Of T)` and `ReadOnlySpan(Of T)`, and `For Each` iterates its elements. A span is built as C# 12 builds it, with `MemoryMarshal.CreateSpan(Unsafe.As(Of TBuffer, T)(buffer), n)`, called directly instead of through a private helper method. `buffer(i)` indexes that span, so a variable index is checked at run time, and a constant index outside the buffer reports BC37417. A writable variable gives `Span(Of T)` and an assignable element, while a `ReadOnly` variable gives `ReadOnlySpan(Of T)`. The buffer must be a variable: indexing or iterating a value such as a function result reports BC37416, where C# allows reading it. Slicing with a range is not covered. |

## Phase 52 — IDE for wave 9

| Slice | Area | Done when |
| --- | --- | --- |
| 52a | Editor | Done. For the forms locked in phases 49 and 50: on `vbnext`, completion offers `ByRef` before a field of a `ByRef Structure`, `ReadOnly` after it, and `ByRef` after `=` in its `Sub New`, and none of them elsewhere or on 17.13. `ByRef`, `ReadOnly`, and `= ByRef` classify as keywords, and quick info shows a ref field as `ByRef` or `ByRef ReadOnly`. Phase 49 adds no keyword. |

## Phase 53 — `AddressOf` through type parameters

Wave 10 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 53a | VB-089 | Done. On `vbnext`, `AddressOf T.Parse`, where `T` is constrained to an interface that declares `Parse` as a shared abstract or shared virtual method, makes a delegate that calls the type argument's member, emitted with `constrained. ldftn` as C# 11 emits it. When the delegate's signature needs a relaxation stub, such as a `T` result returned as `Object`, the stub calls the member through `T` as slice 42a does. Visual Basic 17.13 still reports BC37314. |

## Phase 54 — Inline array values

Wave 10 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 54a | VB-090 | Done. On `vbnext`, `Make()(i)` and `For Each v In Make()` read the elements of an inline array that is a value, through a `ReadOnlySpan(Of T)` over a temporary. The temporary is never reused within the method, so a nested `Make(20)(Make(0)(1))` and nested loops over values read the right elements. Assigning an element of a value reports BC30068, and BC37416 is gone. Converting a value to a span with `Dim s As ReadOnlySpan(Of Integer) = Make()` is still not a conversion. |

## Phase 55 — Ref fields from metadata

Wave 10 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 55a | VB-091 | Done. On `vbnext`, a `ref` field declared in C# reads, assigns, compound assigns, and passes `ByRef` through the variable it refers to, including through a generic `ref struct`, and a `ref readonly` field, which C# marks with `IsReadOnlyAttribute`, reads but reports BC30068 when assigned, as phase 50 does for VB ref fields. Quick info shows them as `ByRef` and `ByRef ReadOnly`. Metadata fields now carry their ref kind and ref custom modifiers in place of the unsupported placeholder modifier, so field references emit and match by signature with them. Substituted and retargeted fields now pass the ref kind through, so a ref field of a generic VB `ByRef Structure` writes through as well. A structure with a ref field is managed, so it no longer satisfies an `unmanaged` constraint, and BC37332 is reported as C# reports CS8377; before, the placeholder's BC30656 stood in for that check. Visual Basic 17.13 still reports BC30656. Only ref fields declared in this compilation count for the slice 50d lifetime rules. |

## Phase 56 — `ByRef` local lifetime

Wave 10 row 4.

| Slice | Id | Done when |
| --- | --- | --- |
| 56a | VB-092 | Done. `docs/vbnext-vb092-syntax.md` locks that a write through a `Dim ByRef` local follows its referent: assigning a structure that holds a ref field to the local, or to a field of a structure reached through it, is BC37415 when the local refers to a `ByRef` parameter, or to a field reached through one or through `Me`, following `ByRef` locals in turn. No code change. |
| 56b | VB-092 | Done. On `vbnext`, the rules that 56a locks are checked: BC37415 is reported for `r = New Cell(local)` and `q.First = New Cell(local)` when `r` or `q` is a `Dim ByRef` local over a `ByRef` parameter, and for a `ByRef` local over such a local, while a `ByRef` local over a local of the method or a `ByVal` parameter may still be assigned. The declaration records what the local refers to, so the check runs where the write is bound. |

## Phase 57 — Returning ref field structures

Wave 10 row 5.

| Slice | Id | Done when |
| --- | --- | --- |
| 57a | VB-093, VB-094 | Done. `docs/vbnext-vb093-syntax.md` locks when a ref structure value refers to local storage: a span over a local, `ByVal`, `Me`, or temporary inline array, a `New` or call whose `ByRef` arguments or ref structure arguments do, and any local given such a value anywhere in the method. Such a value cannot be returned or assigned to a `ByRef` parameter or through `Me` (new BC37418), and BC37414 is lifted so that a function may return a structure that holds a ref field when it refers only to the caller's storage. No code change. |
| 57b | VB-093, VB-094 | Done. On `vbnext`, the rules that 57a locks are checked by a pass over each method body after binding, which finds the locals that hold a value over local storage before it reports, so a local returned in a loop before a later assignment is caught. BC37418 is reported for `Return New Cell(local)`, `Return New Cell(byValValue)`, `Return b` and `Return s.Slice(1)` over a local inline array, assigning the function name, and `target = b` into a `ByRef` span parameter. `Return New Cell(byRefValue)`, `Return New Cell(items(0))`, a span over an array, and a span over a `ByRef` inline array parameter may leave the method. BC37414 is removed. |

## Phase 58 — IDE for wave 10

| Slice | Area | Done when |
| --- | --- | --- |
| 58a | Editor | Done, with no change. Phases 56 and 57 lock rules and no syntax, so there are no new keywords to classify or complete. BC37418 reaches the editor as any compiler error does, and quick info already shows C# ref fields as `ByRef` and `ByRef ReadOnly` since slice 55a. |

## Phase 59 — `NameOf` unbound generics

Wave 11 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 59a | VB-095 | Done. On `vbnext`, `NameOf(List(Of))` and `NameOf(Dictionary(Of ,))` give the type's name, as C# 14 gives it for `nameof(List<>)`, including in a `Const`. The argument is parsed as `GetType` parses its type when it has a type argument list with no types, and bound as `GetType` binds an unbound type, so a wrong count reports BC32043. A qualified name such as `NameOf(System.Collections.Generic.List(Of))` is not covered. Visual Basic 17.13 still reports BC30182. |

## Phase 60 — Shared virtual interface members

Wave 11 row 2.

| Slice | Id | Done when |
| --- | --- | --- |
| 60a | VB-096 | Done. `docs/vbnext-vb096-syntax.md` locks `Shared Overridable` for a shared interface member with a default body, which a type may supply with `Shared ... Implements` or leave out, reached through a type parameter. No code change. |
| 60b | VB-096 | Done for `Function` and `Sub`. On `vbnext`, `Shared Overridable` with a body declares the member, emitted as a static virtual method that C# reads as `static virtual`. A type may supply its own with `Shared ... Implements` or leave it out, and `T.DefaultName()` reaches the type's member or the default body. `Shared Overridable` without a body, a plain `Shared` method with a body, and a call through the interface's name report BC30270, BC30270, and BC37314. A `Shared Overridable Property` is not done yet, because interfaces do not yet declare shared properties in VB. |

## Phase 61 — `ByRef ReadOnly` functions

Wave 11 row 3.

| Slice | Id | Done when |
| --- | --- | --- |
| 61a | VB-097 | Done. `docs/vbnext-vb097-syntax.md` locks `ByRef ReadOnly Function` for a `ref readonly` return, whose `Return` may also name a variable that cannot be written, and whose result callers read but cannot write through. `ByRef ReadOnly Property` keeps its meaning, so `ref readonly` properties are left for a later note. No code change. |
| 61b | VB-097 | Done. On `vbnext`, `ByRef ReadOnly Function` is emitted with the `InAttribute` required modifier on its return and `IsReadOnlyAttribute` on its return value, so C# and slice 35a read it as `ref readonly`, and the result cannot be written through: assigning it reports BC30068 and `Dim ByRef` over it BC37406, while `Dim ByRef ReadOnly` over it refers to the variable. `Return` may name a `ReadOnly` field, whose own address is returned, and reports BC30068 for a value. `IMethodSymbol.RefKind` and `IPropertySymbol.RefKind` now report `RefReadOnly` for any `ref readonly` member, and symbol display writes `ByRef ReadOnly Function`. |

## Phase 62 — `ByRef ReadOnly` parameters

Wave 11 row 4.

| Slice | Id | Done when |
| --- | --- | --- |
| 62a | VB-098 | Done. `docs/vbnext-vb098-syntax.md` locks `ByRef ReadOnly` parameters, emitted as C# `in`: read as `ReadOnly` variables in the body, passed by reference from a variable, and copied into a temporary from a value. No code change. |
| 62b | VB-098 | Done. On `vbnext`, `ByRef ReadOnly` parameters are emitted as C# emits `in`: the metadata `In` flag, `IsReadOnlyAttribute`, and the `InAttribute` required modifier on interface, virtual, and delegate members, so C# calls them with `in` and implements a VB interface member with an `in` parameter. In the body the parameter reads as a `ReadOnly` variable: assigning or compound assigning it reports BC30064, `Dim ByRef` over it BC37406, and a structure method called on it works on a copy. A caller passes a variable, including a `ReadOnly` field, by reference without a copy, and a value through a temporary. `ReadOnly` without `ByRef`, or on a lambda parameter, reports BC37419. `IParameterSymbol.RefKind` is `In` for these parameters; C# `in` parameters read from metadata are unchanged. |

## Phase 63 — IDE for wave 11

| Slice | Area | Done when |
| --- | --- | --- |
| 63a | Editor | Done. For the forms locked in phases 60 to 62: on `vbnext`, completion offers `ReadOnly` after a parameter's `ByRef` but not a lambda parameter's, `ReadOnly` and `Function` after a member's `ByRef`, `Function` after `ByRef ReadOnly`, and `Overridable` after `Shared` in an interface, and none of them on 17.13. The new forms classify as keywords, and quick info shows `ByRef ReadOnly Function` and `ByRef ReadOnly` parameters. `IMethodSymbol.ReturnsByRefReadonly` and `IPropertySymbol.ReturnsByRefReadonly` now report `ref readonly` members instead of always `False`. |

## Phase 64 — `in` parameters from metadata

Wave 12 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 64a | VB-099 | Done. On `vbnext`, methods, indexers, and delegates from another assembly whose `in` parameters carry the `InAttribute` required modifier are called as slice 62b calls `ByRef ReadOnly` parameters, including a `ReadOnly` field passed without a copy, and a `ByRef ReadOnly` parameter overrides or implements them, so C# calls the VB member through its own type. This covers C# abstract, virtual, and interface members and the VB members that slice 62b emits, read back from metadata. `in` and `ref readonly` parameters from metadata, which C# marks with `IsReadOnlyAttribute` or `RequiresLocationAttribute`, now report `RefKind.In` and display as `ByRef ReadOnly`. A plain `ByRef` parameter that overrides or implements a `ByRef ReadOnly` one, or the other way round, reports BC37420 or BC37421, as C# requires `in` to match; before, VB copied the modifier silently and the override could write through it. Visual Basic 17.13 still reports BC30657 and BC30643 for such members, and still copies a `ReadOnly` field passed to an `in` parameter. |

## Phase 65 — Qualified `NameOf` unbound generics

Wave 12 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 65a | VB-101 | Done. On `vbnext`, `NameOf(System.Collections.Generic.List(Of))` gives `"List"`, as slice 59a does for `NameOf(List(Of))`, including with `Global.`, an `Imports` alias, a nested type such as `NameOf(Dictionary(Of ,).KeyCollection)`, and in a `Const`. The name binds as `GetType` binds it, so a wrong count reports BC32043 and an unknown type BC30002, and the semantic model gives the unbound type. A member of an unbound type, such as `NameOf(List(Of).Count)`, which C# 14 accepts, is not covered and reports BC30002. Visual Basic 17.13 still reports BC30182. |

## Phase 66 — Lambdas for `ByRef ReadOnly` delegates

Wave 12 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 66a | VB-100 | Done. On `vbnext`, a lambda whose parameters have no modifiers, typed or not, converts to a delegate with `ByRef ReadOnly` parameters, declared in VB or C# with `in`, including as an argument, and those parameters read as `ReadOnly` variables in the lambda's body, as slice 62b's do in a method: assigning one reports BC30064, `Dim ByRef` over it BC37406, and a structure method called on it works on a copy. A lambda parameter written `ByRef` also takes `ReadOnly` from the delegate; before, it converted with a parameter that could write through the caller's reference. `ByVal` written on the parameter still reports BC36532, and `ReadOnly` written on it BC37419. The semantic model reports `RefKind.In` for such a parameter. |

## Phase 67 — `ByRef ReadOnly` operator operands

Wave 12 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 67a | VB-103 | Done. On `vbnext`, unary, arithmetic, comparison, and logical operators take `ByRef ReadOnly` operands, emitted as C# emits `in` operator parameters, and an operator expression, a compound assignment, and `AndAlso` over such an `And` pass them as slice 62b passes arguments: a variable or `ReadOnly` field by reference, a value through a temporary. C# operators with `in` parameters, and VB ones read back from metadata, are now operators. `ByRef` without `ReadOnly` still reports BC30651, as does `ByRef ReadOnly` on a conversion, `IsTrue`, or `IsFalse`, which VB calls without that argument passing; BC37419 is no longer reported beside BC30651. Such operators are not lifted to `Nullable` operands. Before `vbnext` they are not operators, so `a + b` still reports BC30452, while `op_Addition` can still be called by name. |

## Phase 68 — Shared interface properties

Wave 12 row 5. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 68a | VB-102 | Done. On `vbnext`, a VB interface declares `Shared MustOverride` properties, emitted as static abstract, and `Shared Overridable` properties with `Get` and `Set` bodies, emitted as static virtual, as slice 60b declares functions and subs; C# reads them as `static abstract` and `static virtual`. A type supplies them with `Shared ... Implements` as slice 48a lets it, or leaves out a `Shared Overridable` one, and `T.Zero` and `T.Label = ...` reach the type's property or the default body through a type parameter. `Shared Overridable` without a body reports BC30273, a type that leaves out a `Shared MustOverride` property BC37315, and access through the interface's name BC37314. Visual Basic 17.13 still reports BC30273 and BC30502. |

## Phase 69 — Interface operators

Wave 12 row 6.

| Slice | Id | Done when |
| --- | --- | --- |
| 69a | VB-104 | Done. `docs/vbnext-vb104-syntax.md` locks `Shared MustOverride Operator` for a shared abstract interface operator and `Shared Overridable Operator ... End Operator` for a shared virtual one, the spelling of phases 60 and 68, for every operator VB declares except conversions. No code change. |
| 69b | VB-104 | Done. On `vbnext`, `Shared MustOverride Operator` in a VB interface declares a static abstract operator and `Shared Overridable Operator ... End Operator` a static virtual one with its default body, which C# reads as `static abstract` and `static virtual`, implements, and calls. A type supplies them by signature as phase 49 does, or leaves out a `Shared Overridable` one, and `a + b`, `-a`, and `a = b` over a type parameter call the type's operator or the default body as phase 43 does. Operators in interfaces are now checked as in classes: pairs such as `<` with `>` report BC33033, and the containing-type rule accepts a type parameter constrained to the interface. A plain `Shared Operator` in an interface still reports BC30603, a conversion operator BC33013, and a type that leaves out a `Shared MustOverride` operator BC37315. Visual Basic 17.13 still reports BC30603 and BC33013. |

## Phase 70 — Read-only structure members

Wave 12 row 7.

| Slice | Id | Done when |
| --- | --- | --- |
| 70a | VB-105 | Done. `docs/vbnext-vb105-syntax.md` locks `ReadOnly Function` and `ReadOnly Sub` for read-only structure methods and `ReadOnly Get` for read-only getters, keeps `ReadOnly Property` as a property without a setter, and makes auto-implemented getters in structures read-only without a mark. No code change. |
| 70b | VB-105 | Done. On `vbnext`, `ReadOnly Function` and `ReadOnly Sub` in a structure, `ReadOnly Get`, and the `Get` of an auto-implemented structure property are emitted with `IsReadOnlyAttribute`, so C# and `IMethodSymbol.IsReadOnly` read them as `readonly` members, and C# `readonly` members from metadata read as read-only too. In such a member, assigning a field of `Me` reports BC30068, and a member that may change the structure, called on `Me` or on a structure field of `Me`, works on a copy, as C# does. A call to a read-only member on a `ReadOnly` field or a `ByRef ReadOnly` parameter passes the structure without a copy. `ReadOnly` on a `Shared` member or a class member reports BC30242, on `Set` or a class `Get` BC31099, and where `IsReadOnlyAttribute` is missing BC35000; an auto-implemented getter there stays as before. Visual Basic 17.13 still reports BC30242. |

## Phase 71 — IDE for wave 12

| Slice | Area | Done when |
| --- | --- | --- |
| 71a | Editor | Done. For the forms locked in phases 69 and 70, and the shared interface members of phase 68: on `vbnext`, completion offers `Function`, `Sub`, `Property`, and `Operator` after `Shared MustOverride` or `Shared Overridable` in an interface, where before it offered only `Async`; `Function` and `Sub` after `ReadOnly` in a structure but not in a class or after `Shared`; and `Get` after `ReadOnly` where a structure property's accessor starts, and none of them on 17.13. The new forms classify as keywords, and quick info and symbol display write `ReadOnly Function` for read-only methods, as C# writes `readonly` with the ref options. |

## Phase 72 — Calling `params` collections

Wave 13 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 72a | VB-106 | Done. On `vbnext`, a call passes separate arguments, or none, to a C# 13 `params` parameter of type `ReadOnlySpan(Of T)`, `Span(Of T)`, `List(Of T)`, or `IEnumerable(Of T)`, the collection types VB's own `ParamArray` takes, building the collection as VB does for its own, and a generic method infers `T` from those arguments. Where a library declares both `params T()` and `params ReadOnlySpan(Of T)`, as `String.Concat` and `Path.Combine` do, VB still picks the array overload, so existing calls do not change, and an overload with fixed parameters, such as `String.Format(format, arg0)`, still beats an expanded `params ReadOnlySpan(Of Object)`, as it beats an expanded array, including for interpolated strings. VB's own `ParamArray` of a collection type is now emitted with `ParamCollectionAttribute`, so C# calls it with separate arguments. `IParameterSymbol.IsParamsCollection` and `IsParamsArray` now tell the two apart, and symbol display writes `ParamArray` for both. Other collection types that C# 13 allows are not covered. Visual Basic 17.13 still reports BC30057 for separate arguments, and passes the collection itself, positionally or by name, as before. |

## Phase 73 — `NameOf` unbound generic members

Wave 13 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 73a | VB-113 | Done. On `vbnext`, `NameOf(List(Of).Count)`, `NameOf(System.Collections.Generic.Dictionary(Of ,).Keys)`, `NameOf(List(Of).Add)`, and `NameOf(List(Of).Enumerator.Current)` give the member's name, as C# 14 gives `nameof(List<>.Count)`, including in a `Const`. The qualifier binds as `GetType` binds it, and the last name is looked up in the generic type's definition; a nested type such as `NameOf(Dictionary(Of ,).KeyCollection)` still binds as a type. A member that does not exist reports BC30456 in place of the misleading BC30002 or BC31424, and one that is not accessible reports the usual accessibility error. |

## Phase 74 — `ByRef ReadOnly` conversions and lifting

Wave 13 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 74a | VB-112 | Done. On `vbnext`, `Widening` and `Narrowing` conversions, `IsTrue`, and `IsFalse` take `ByRef ReadOnly` operands, and they work in `CType`, `CInt`, assignments, `If` conditions, and `AndAlso` over such an `And`. An operator with `ByRef ReadOnly` operands lifts to `Nullable` operands, so `a + b` over `Vec?` gives `Nothing` when an operand is `Nothing`. C# conversions with `in` parameters from metadata are conversions. A value that the lowering of a lifted operator or conversion passes to a `ByRef ReadOnly` parameter is copied into a temporary that is freed after the call. `ByRef` without `ReadOnly` on any operator still reports BC30651. Before `vbnext` such a conversion still reports BC30311 where it would be used. |

## Phase 75 — Instance interface properties

Wave 13 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 75a | VB-109 | Done. On `vbnext`, an interface declares an instance property with `Get` and `Set` bodies, its default implementation, emitted as a virtual property that is not abstract, as interfaces already declare instance methods with a body. A class may supply its own with `Implements` or leave it out, a call through the interface reaches the class's property or the default body, and C# reads it as a default interface property. A property without a body stays abstract, and an abstract property with parameters no longer meets the auto-property check. A plain `Shared` property with a body reports BC30273, and a class that leaves out an abstract property BC30149. Visual Basic 17.13 still reports BC30603. |

## Phase 76 — Shared non-virtual interface members

Wave 13 row 5. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 76a | VB-110 | Done. On `vbnext`, a `Shared Function`, `Sub`, or `Property` with a body in an interface declares a static member that is not abstract or virtual, which C# reads and calls as a static interface member, called through the interface's name as `IFactory.Describe()` or `IFactory.Version`, as the rejected form of `docs/vbnext-vb096-syntax.md` keeps it. A type cannot implement it (BC30401), and reaching it through a type parameter reports BC32098, as C# reports for non-virtual static members. Visual Basic 17.13 still reports BC30270. |

## Phase 77 — Generic attributes

Wave 13 row 6. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 77a | VB-108 | Done. On `vbnext`, `<Tag(Of Integer)>` finds `TagAttribute(Of T)` without the `Attribute` suffix, as `<Tag>` finds `TagAttribute`, for simple and qualified names, so a generic attribute declared in C# or VB is applied as either `<Tag(Of Integer)>` or `<TagAttribute(Of Integer)>`, with named arguments. Declaring a VB generic attribute class and applying it with its full name already worked on `vbnext`. A type argument that uses a type parameter still reports BC32067, as C# 11 forbids. `SyntaxFacts.IsAttributeName` now also recognizes generic names. |

## Phase 78 — C# 14 extension members

Wave 13 row 7. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 78a | VB-107 | Done. On `vbnext`, an extension property that C# 14 declares, read-only or read-write, reads and writes as a property of the receiver, as `text.WordCount` and `text.Shout = "!"`, and a static extension property or method of a C# 14 extension block reads as a member of the extended type, as `String.Empty2` and `String.Repeat(3)`, as VB extension blocks already declare them. VB reads the extension block's declarations from the grouping type C# emits and calls the implementation methods C# emits beside it. Generic extension blocks, such as `extension<T>(List<T> list)`, and extension indexers are not covered. Visual Basic 17.13 still reports BC30456. |

## Phase 79 — Interface conversion operators

Wave 13 row 8.

| Slice | Id | Done when |
| --- | --- | --- |
| 79a | VB-111 | Done. `docs/vbnext-vb111-syntax.md` locks `Shared MustOverride` and `Shared Overridable` `Widening` and `Narrowing` `Operator CType` in interfaces, the phase 69 spelling, and lets a conversion between a type parameter and another type use the conversions its interface constraints declare, `Widening` implicitly and `Narrowing` explicitly, as VB classifies a class's conversions. No code change. |
| 79b | VB-111 | Done. On `vbnext`, `Shared MustOverride` and `Shared Overridable` `Widening` and `Narrowing` `Operator CType` declare static abstract and static virtual conversion operators in an interface that C# reads and uses, a structure or class supplies them by signature as phase 49 lets it, and a conversion between a type parameter and another type finds the conversions its interface constraints declare and calls them constrained to the type parameter, as `Dim result As T = n` and `CType(value, Integer)`. `Widening` converts implicitly and `Narrowing` only explicitly under `Option Strict On`. Such conversions, and any other call to a shared abstract or virtual interface member, inside an expression tree report BC36534, because the tree could only name the interface's own method. Visual Basic 17.13 still reports BC30603 and BC33013 for the declarations and does not search constraints for conversions. |

## Phase 80 — IDE for wave 13

| Slice | Area | Done when |
| --- | --- | --- |
| 80a | Editor | Done. For the wave 13 forms: on `vbnext`, completion offers `Widening` and `Narrowing` after `Shared MustOverride` or `Shared Overridable` in an interface, `Operator` after them, and `Get` and `Set` inside an interface property, and none of them on 17.13. `Function`, `Sub`, and `Property` after `Shared` in an interface were already offered. The new forms classify as keywords. Quick info shows a C# `params` collection parameter as `ParamArray values As IEnumerable(Of Integer)`, a C# 14 extension property as a property of the class that declares it, as VB's own extension properties show, and a generic attribute applied by its short name as the attribute class, as the full name shows. |

## Phase 81 — Other `params` collections

Wave 14 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 81a | VB-114 | Done. On `vbnext`, a call passes separate arguments, or none, to a C# 13 `params` parameter of a type that names a builder with `CollectionBuilderAttribute`, such as `ImmutableArray(Of T)`, built by the builder's `Create` from a `ReadOnlySpan(Of T)`; of a structure or class with a public parameterless constructor and an `Add` method, such as `HashSet(Of T)` or a collection that is not generic, created and filled with `Add`; of `ICollection(Of T)` or `IList(Of T)`, given a `List(Of T)` as C# gives one; or of `IReadOnlyCollection(Of T)` or `IReadOnlyList(Of T)`, given an array. The element type is the `T` of the one `IEnumerable(Of T)` such a class implements, and a generic method infers its type arguments from the elements. VB's own `ParamArray` takes the same types and C# calls it with separate arguments; `String`, and a class without `Add` such as `Stack(Of T)`, still report BC30050. Visual Basic 17.13 still reports BC30311 and BC30057 for the calls and BC30050 for the declarations. |

## Phase 82 — Compound assignment operators

Wave 14 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 82a | VB-115 | Done. On `vbnext`, `c += 5` calls the instance `operator +=` that C# 14 declares on `c`'s type, `op_AdditionAssignment`, and `-=`, `*=`, `/=`, `<<=`, and `>>=` call theirs, in place of `c = c + 5`, even where the type also declares `+`. `c` is a class instance, read once, so it may be a `ReadOnly` property, or a variable of a structure type, changed in place. With integer overflow checks on, the `checked` form is called where C# declares one. Where no such operator applies, as for a structure property, a value of another type, or VB's `&=`, `^=`, and `\=`, which C# has no operators for, `c = c + 5` applies as before. Visual Basic 17.13 still reports BC30452, and BC30657 for a call to `op_AdditionAssignment` by name. |

## Phase 83 — Extension operators

Wave 14 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 83a | VB-116 | Done. On `vbnext`, `a + b`, `-a`, `a = b`, and the other unary and binary operators find the operators that C# 14 extension blocks in scope declare, through the namespaces a file imports as extension methods are found, when the operand types and their constraints declare no operator of that kind, so a type's own operator still wins. An extension block applies when an operand converts to the type it extends by identity, reference, or boxing conversion, as a `Circle` reaches an operator for `Shape`, and its operators lift to `Nullable` operands as other operators do. VB reads the operator declarations from the grouping type C# emits and calls the static implementation methods beside it. Generic extension blocks are not covered. Without the import, and in Visual Basic 17.13, `a + b` still reports BC30452. |

## Phase 84 — Generic extension blocks

Wave 14 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 84a | VB-117 | Done. On `vbnext`, the extension properties, static extension properties, and static extension methods of a generic C# 14 extension block read as phase 78 reads blocks that are not generic: `numbers.Tail`, `List(Of Integer).Empty()`, and `List(Of String).Kind` for `extension<T>(List<T> list)`, `numbers.Size` and `values.Size` over a list and an array for `extension<T>(IEnumerable<T> items)`, and `(7).Self` for `extension<T>(T value) where T : struct`. The block's type arguments are inferred from the receiver or the type: from the type itself, else its nearest base type that matches, else the one interface that matches; the implementations C# emits are constructed with them, and a block whose constraints they do not satisfy does not apply, so `"text".Self` reports BC30456. Generic members of generic blocks, and operators of generic blocks, are not covered. Visual Basic 17.13 still reports BC30456. |

## Phase 85 — Ref structures for interface constraints

Wave 14 row 5. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 85a | VB-118 | Done. On `vbnext`, a `ByRef Structure` that implements an interface meets that interface constraint, as C# 13 checks it, because a ref structure cannot be boxed: `Generic.Use(New Wrapper())`, where C# declares `Use<T>(T value) where T : IDisposable, allows ref struct`, compiles, and the generic method calls the interface members on the structure. A `Structure` constraint is met as before. Where the type parameter does not allow ref structures, only BC37376 is reported, and a ref structure that does not implement the interface still reports BC32044. Visual Basic 17.13 still reports BC32044. |

## Phase 86 — `allows ref struct` type parameters

Wave 14 row 6.

| Slice | Id | Done when |
| --- | --- | --- |
| 86a | VB-119 | Done. `docs/vbnext-vb119-syntax.md` records that slice 7m already declares type parameters that take ref structures, as C# 13's `allows ref struct`, with `ByRef` in the constraint: `Function Echo(Of T As ByRef)(value As T) As T` and `Sub Close(Of T As {IDisposable, ByRef})(value As T)`. The wave 14 probe declared `Echo(Of T)` without it. `(Of ByRef T)`, which this slice first locked, is withdrawn so that VB has one spelling. No code change. |
| 86b | VB-119 | Done. No new code: a test checks that, on `vbnext`, `Echo(Of T As ByRef)` takes a `Span(Of Integer)` and an `Integer`, that `Close(Of T As {IDisposable, ByRef})` takes a `ByRef Structure` that implements `IDisposable`, with phase 85, and that C# reads the type parameter as `allows ref struct`. |

## Phase 87 — IDE for wave 14

| Slice | Area | Done when |
| --- | --- | --- |
| 87a | Editor | Done. For the wave 14 forms, which add no syntax: on `vbnext`, completion offers slice 7m's `ByRef` constraint after `As` and in a `{...}` constraint list, but not after `Class` or a `ByRef` already listed, and not on 17.13. Quick info and symbol display show the implementation of a C# 14 extension operator as an operator, `Operator VecOperators.+(a As Vec, b As Vec) As Vec`, where it showed the `op_Addition` method, and a generic extension property with the block's type argument substituted, `ReadOnly Property ListExtensions.Tail As Integer`. Compound assignment statements show no quick info, for C# 14 operators as for other operators. |

## Phase 88 — Interface member accessibility

Wave 15 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 88a | VB-120 | Done. On `vbnext`, an interface method or property with a body may be `Private`, emitted as an instance method that is not virtual and called only from the interface's other bodies, or `Friend` or `Protected`, emitted as default implementations, as C# 8 declares them, and `Public` may be written. A `Protected` member is called from the bodies of interfaces that inherit it; from elsewhere it reports BC30390, and a class cannot implement a `Private` member (BC30401). A member without a body that is not `Public` still reports BC30270 or BC30273, because a type must implement it. A body in an interface now reaches the members of the interfaces it inherits through the implicit `Me`, where it reported BC30469. C# reads the accessibility. Visual Basic 17.13 still reports BC30270. |

## Phase 89 — Shared interface fields and constructors

Wave 15 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 89a | VB-121 | Done. On `vbnext`, an interface declares `Shared` fields, including `Shared ReadOnly`, with or without an initializer, `Const` constants, and a `Shared Sub New`, which runs once before the interface's shared members are first used, after the field initializers, as C# 8 declares static fields, constants, and static constructors. Fields and constants of an interface are `Public` unless they say otherwise, and C# reads them. An initializer without a `Shared Sub New` is run by the shared constructor VB emits for it. Instance fields, as `Dim Other As Integer`, still report BC30602, and an instance `Sub New` BC30363. Visual Basic 17.13 still reports BC30602, BC30270, and BC30363. |

## Phase 90 — Interface custom events

Wave 15 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 90a | VB-122 | Done. On `vbnext`, an interface declares a `Custom Event` with an `As` clause and `AddHandler`, `RemoveHandler`, and `RaiseEvent` bodies. `AddHandler` and `RemoveHandler` are its default implementation, emitted as virtual accessors that C# subscribes through, so a class may leave the event out or supply its own `Custom Event ... Implements`, as C# 8 declares default event accessors. `RaiseEvent` stays a private helper of the interface that is not virtual, and a class's `RaiseEvent` does not implement it. A `Custom Event` without an `As` clause still reports BC31121, and a missing accessor BC31131 or BC31132, as in a class. Visual Basic 17.13 still reports BC31121. |

## Phase 91 — Lambda optional and ParamArray parameters

Wave 15 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 91a | VB-123 | Done. On `vbnext`, a lambda parameter with an `As` clause may be `Optional` with a constant default, and the last one `ParamArray` of a one-dimensional array, so `Dim f = Function(Optional x As Integer = 3) x * 2` calls as `f()` and `f(5)`, and `Function(ParamArray xs() As Integer) xs.Length` as `g()` and `g(1, 2, 3)`, through the anonymous delegate VB infers for the lambda, whose `Invoke` takes the default and the `ParamArray`, as C# 12 allows default values and `params` in lambdas. Lambdas whose defaults differ get different anonymous delegates. Converted to a delegate type such as `Func(Of Integer, Integer)`, the lambda takes that type's parameters. An `Optional` parameter without an `As` clause still reports BC33010, a missing default BC30812, a default that is not constant BC30059, a `ParamArray` that is not an array BC30050, and one that is not last BC30192. Visual Basic 17.13 still reports BC33010 and BC33009. |

## Phase 92 — Generic extension operators

Wave 15 row 5. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 92a | VB-124 | Done. On `vbnext`, the operators of a generic C# 14 extension block, such as `+`, unary `-`, and `=` in `extension<T>(Box<T>)`, apply to `Box(Of Integer)` and `Box(Of String)` operands, with `T` inferred from the first operand that matches the extended type as phase 84 infers it, and the implementation C# emits constructed with it, as phase 83 applies the operators of blocks that are not generic. They lift to `Nullable` operands. Operands that do not fit the constructed operator, as `Box(Of Integer) + Box(Of String)`, report BC30311. Visual Basic 17.13 still reports BC30452. |

## Phase 93 — Tuple type aliases

Wave 15 row 6. No new keyword. A new syntax node, `TypeAliasImportsClauseSyntax`, holds the alias and a type that is not a name, and `SimpleImportsClauseSyntax` stays as it is.

| Slice | Id | Done when |
| --- | --- | --- |
| 93a | VB-125 | Done. On `vbnext`, `Imports Pair = (Left As Integer, Right As Integer)` names a tuple type, with its element names, as C# 12 aliases any type, and `Dim p As Pair` declares one. An alias whose target starts with `(` parses as a `TypeAliasImportsClauseSyntax`, so `Imports MaybePair = (Integer, Integer)?` names a nullable tuple, and an alias may be a type argument, as in `List(Of Named)`. `GetDeclaredSymbol` on the clause returns the alias, and `GetAliasInfo` finds it where it is used. A duplicate alias still reports BC30572, and an undefined element type BC30002. Visual Basic 17.13 parses the clause as before and still reports BC30203. |

## Phase 94 — IDE for wave 15

| Slice | Area | Done when |
| --- | --- | --- |
| 94a | Editor | Done. For the wave 15 forms, only on `vbnext`: completion offers `Public`, `Private`, `Friend`, `Protected`, and `Protected Friend` for interface members, and `Friend` after `Protected`, `Optional` and `ParamArray` for lambda parameters, with only `Optional` after an optional one, and `Custom Event` in interfaces, with `Event` after `Custom`. Quick info on the alias in `Imports Pair = (Left As Integer, Right As Integer)` shows `(Left As Integer, Right As Integer)`, as C# shows a tuple alias in its `using` directive, and on an optional lambda parameter shows its default, `(parameter) x As Integer = 3`, as for a method's optional parameter. Visual Basic 17.13 offers none of these keywords. |

## Phase 95 — Aliases for predefined and array types

Wave 16 row 1. No new syntax: the alias uses phase 93's `TypeAliasImportsClauseSyntax`.

| Slice | Id | Done when |
| --- | --- | --- |
| 95a | VB-126 | Done. On `vbnext`, `Imports Num = Integer`, `Imports Ints = Integer()`, `Imports Grid = Integer(,)`, `Imports MaybeNum = Integer?`, `Imports MaybeDay = System.DayOfWeek?`, and `Imports Lists = System.Collections.Generic.List(Of String)()` name the type, as C# 12 aliases any type, and `Dim n As Num` declares one. An alias that starts with a predefined type keyword or `(`, or a name followed by `?` or `()`, parses as a `TypeAliasImportsClauseSyntax`, and a plain name stays a `SimpleImportsClauseSyntax`. As for any alias, the target binds without the file's other `Imports`, so a generic element type is written in full. Native and portable PDBs record the alias, where an alias of an array type made emit fail. Visual Basic 17.13 still reports BC30183 for a keyword and BC30205 for `()`. |

## Phase 96 — Tuple equality

Wave 16 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 96a | VB-127 | Done. On `vbnext`, `(a, b) = (c, d)` means `a = c AndAlso b = d`, and `(a, b) <> (c, d)` means `a <> c OrElse b <> d`, for two tuples of the same length, as C# 7.3 compares tuples. Both operands are evaluated once, left to right, before any element is compared. Each element uses its own `=` or `<>`, so `(1, 2) = (1L, 2L)` widens, nested tuples compare element by element, tuples longer than seven elements compare through `Rest`, and a `Nothing` in an `Integer?` element gives a `Boolean?` result, as `AndAlso` gives over lifted comparisons. `Case (1, 2)` matches a tuple in `Select Case`, and the comparison works in field initializers. Tuples of different lengths, and elements without `=`, still report BC30452, and inside an expression tree the comparison reports BC36534 once. Visual Basic 17.13 still reports BC30452. |

## Phase 97 — `With` on structures

Wave 16 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 97a | VB-128 | Done. On `vbnext`, `p With {.Y = 5}` copies any structure that is not a record, including tuples such as `t With {.Count = 3}`, and sets the named fields and properties on the copy, as C# 10 copies structures with `with`. The whole value is copied, so private fields carry over, the original does not change, `InitOnly` properties may be set, and `.X` in a value reads the copy. A member that is not there reports BC30456, a member set twice BC30989, a `ReadOnly` field BC30064, a `ReadOnly` property BC30526, and a method BC30990; a class that is not a record still reports BC37377. `New Point() With {...} With {...}` still parses the second `With` as part of the creation, so the creation needs parentheses. Visual Basic 17.13 still reports BC30205. |

## Phase 98 — `With` on anonymous types

Wave 16 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 98a | VB-129 | Done. On `vbnext`, `a With {.Y = 5}` on an anonymous type creates a new value of the same anonymous type, with the listed properties set and the others read from the original, as C# 10 copies anonymous types with `with`. `Key` properties may be set too, since the copy is a new value. The original is evaluated first, then the listed values in the order they are written, each converted to its property's type, and `.X` in a value reads the original. A property that is not there reports BC30456, a property set twice BC30989, and a value that does not convert BC30512 under `Option Strict On`. Visual Basic 17.13 still reports BC30205. |

## Phase 99 — Null-conditional compound assignment

Wave 16 row 5. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 99a | VB-130 | Done. On `vbnext`, `b?.Count += 1` and the other compound assignments, `-=`, `*=`, `/=`, `\=`, `^=`, `<<=`, `>>=`, and `&=`, evaluate `b` once and read and write the member only when `b` is not `Nothing`, as `b?.Name = value` already does and as C# 14 allows. When `b` is `Nothing`, the right side is not evaluated. Fields, properties, array elements such as `b?.Items(1) += 10`, and chains such as `b?.Inner?.Count += 1` work. Each `?.` in the target now binds as its own conditional access, which also fixes `b?.Inner?.Count = 1`: before, its operation tree failed the flow analysis checks. A `ReadOnly` property still reports BC30526, and a value that does not convert BC30512. A compound assignment that only a C# 14 `+=` operator supports is not tried through `?.`. Visual Basic 17.13 still reports BC30068. |

## Phase 100 — Array literals for collection types

Wave 16 row 6. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 100a | VB-131 | Done. On `vbnext`, a one-dimensional array literal such as `{1, 2, 3}` converts to a type with a collection initializer, such as `List(Of Integer)`, `HashSet(Of Integer)`, or a class of its own with `Add`, and to a type with a `CollectionBuilder`, such as `ImmutableArray(Of Integer)` and `ImmutableList(Of String)`, as C# 12 collection expressions build them, the way phase 81 builds `params` collections and the parenthesized `(1, 2, 3)` builds collections. It applies wherever the target type is known, as in `Dim`, assignment, `Return`, and field initializers, and `{}` builds an empty collection. Each element converts to the element type, so `{"text"}` for `List(Of Integer)` reports BC30512 under `Option Strict On`. A literal that already converts keeps that conversion, so arrays, spans, `IEnumerable(Of T)`, and `IList(Of T)` still get an array. A literal with more than one dimension, and a type that is not a collection, still report BC30311. An argument to a method still reported BC30311 here, as the parenthesized form did; phase 106 passes both forms as arguments. Visual Basic 17.13 still reports BC30311. |

## Phase 101 — IDE for wave 16

| Slice | Area | Done when |
| --- | --- | --- |
| 101a | Editor | Done. For the wave 16 forms, on `vbnext`: completion after `With {.` lists the members a copy can set, for a structure its writable fields and properties, for an anonymous type all its properties, `Key` ones included, and for a record its properties, leaving out members already set and offering nothing on 17.13. Quick info on the alias in `Imports Num = Integer` shows `Structure System.Int32`, and in `Imports Ints = Integer()` shows `Integer()`; an alias of a constructed type, such as `Integer?`, shows its definition, as for other aliases. Semantic classification no longer fails on an alias of a type that is not a name, which cast its clause to `SimpleImportsClauseSyntax` since phase 93, and colors such an alias as the structure, array, or tuple it names. The wave 16 forms classify as other VB code does. |

## Phase 102 — Spreads in array literals

Wave 17 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 102a | VB-133 | Done. On `vbnext`, `{..a, 3}` converted to a collection type or a one-dimensional array spreads the elements of `a`, as `(..a, 3)` does and as C# 12 spreads with `..`: into `List(Of Integer)` and other types with `Add` and `AddRange`, into `Integer()`, `IEnumerable(Of Integer)`, and the other read-only interfaces through an array, into `IList(Of Integer)` and `ICollection(Of Integer)` through a `List(Of Integer)`, into spans, and into `ImmutableArray(Of Integer)` through its collection builder. The source may be an array or a list. A spread with nothing to spread into, as in `Dim b = {..a}`, `Dim c As String = {..a}`, `Dim f As Object = {..a}`, or a two-dimensional array, reports BC37359 once, and a type without `AddRange`, such as `HashSet(Of Integer)`, reported BC30456, as the parenthesized form did, until phase 108. Visual Basic 17.13 does not parse `..a` and still reports BC30157. |

## Phase 103 — Nullable tuple equality

Wave 17 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 103a | VB-135 | Done. On `vbnext`, `=` and `<>` compare a nullable tuple with a tuple or another nullable tuple, on either side, and lift as VB lifts `=` and `<>` on nullable values: the result is a `Boolean?`, `Nothing` when either side is `Nothing`, so `n = (1, 2)` and `n = n` are `Nothing` for a `Nothing` tuple `n`, and otherwise the values compare element by element as phase 96 compares them. `If t = (1, 2) Then` takes the branch only when the result is `True`. C# instead treats two null tuples as equal and returns a `bool`; this slice keeps VB's rules, so that a lifted tuple comparison means what lifted `=` means everywhere else in VB. Tuples of different lengths still report BC30452. Visual Basic 17.13 still reports BC30452. |

## Phase 104 — Null-conditional nothing-coalescing assignment

Wave 17 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 104a | VB-134 | Done. On `vbnext`, `box?.Name ?= "x"` evaluates `box` once and, when `box` is not `Nothing`, applies `Name ?= "x"` to it, as C# 14 allows `??=` through `?.`: `Name` becomes `"x"` only when it is `Nothing`, and, as for any `?=` in this tree, `Name = If(Name, "x")` writes the value back otherwise. When `box` is `Nothing`, the value is not evaluated. Fields, properties, and chains such as `box?.Inner?.Name ?= "x"` work. The statement binds as a block that stores each receiver in a temporary and tests it with an `If`, because a conditional access around `?=` would read its receiver twice, which the flow analysis does not accept; the shared flow analysis code does not change. Visual Basic 17.13 still reports BC30068. |

## Phase 105 — Span receivers for extension methods

Wave 17 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 105a | VB-136 | Done. On `vbnext`, a one-dimensional array calls an extension method whose first parameter is `ReadOnlySpan(Of T)` or `Span(Of T)`, a `Span(Of T)` calls one on `ReadOnlySpan(Of T)`, and a `String` calls one on `ReadOnlySpan(Of Char)`, as C# 14 first-class spans do, with `T` inferred from the element type and the receiver converted to the span, so `a.FillWith(7)` through a `Span(Of Integer)` writes into the array. Span extensions are considered only when no other extension method of that name applies to the receiver, so code that binds today keeps its extension: `a.Contains(2)` still calls `Enumerable.Contains`, where C# 14 prefers `MemoryExtensions.Contains`. A multidimensional array or a type without such a conversion, such as `List(Of Integer)`, still reports BC30456. Visual Basic 17.13 still reports BC30456. |

## Phase 106 — Collection literals as arguments

Wave 17 row 5. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 106a | VB-132 | Done. On `vbnext`, `{1, 2}`, `(1, 2)`, `()`, and spreads such as `{..a, 9}` pass to a parameter of a collection type, such as `List(Of Integer)`, `HashSet(Of String)`, or `ImmutableArray(Of Integer)`, in module functions, instance methods, and extension methods, as C# 12 passes collection expressions. When overload resolution finds no applicable method, each such argument is built as the one parameter type at its position that can build it, and resolution runs again, so a call that resolves today keeps its overload: `Kind({1, 2})` still calls an `Integer()` overload over a `List(Of Integer)` one. Two collection overloads at the same position, such as `List(Of Integer)` and `HashSet(Of Integer)`, still report BC30518, a parameter whose type uses the method's type parameters, such as `List(Of T)`, reported BC36645 until phase 111 inferred `T` from the literal, elements that do not convert keep BC30311, and named arguments were not rebuilt until phase 109. Visual Basic 17.13 still reports BC30311. |

## Phase 107 — IDE for wave 17

| Slice | Area | Done when |
| --- | --- | --- |
| 107a | Editor | Done. For the wave 17 forms, on `vbnext`: quick info on a call whose collection literal argument picks an overload, such as `Count({1, 2})`, shows that overload, `Function M.Count(values As List(Of Integer)) As Integer (+ 1 overload)`, completion after an array lists the extension methods on `ReadOnlySpan(Of T)` and `Span(Of T)` it can call, such as `CountOf`, and not on 17.13, and quick info on such a call shows the extension on the span, `<Extension> Function ReadOnlySpan(Of Integer).CountOf(value As Integer) As Integer`. Both come from the binding of phases 105 and 106, so no editor code changes; the tests declare a small `ReadOnlySpan(Of T)` in their C# library, because the editor tests reference .NET Framework. |

## Phase 108 — Spreads into collections without `AddRange`

Wave 18 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 108a | VB-137 | Done. On `vbnext`, `{..a, 3}` and `(..a, 3)` spread into a collection type that has `Add` but not `AddRange`, such as `HashSet(Of Integer)` and `SortedSet(Of Integer)`, as C# 12 does: the elements are gathered in a `List(Of T)` and passed to the type's public constructor that takes an `IEnumerable(Of T)`, so a set drops repeated elements as its `Add` would. Types with `AddRange` keep it. A type with neither `AddRange` nor such a constructor still reports BC30456; C# adds each element with `Add` there, which a collection initializer here cannot express. Visual Basic 17.13 does not parse the spread. |

## Phase 109 — Collection literals as named arguments

Wave 18 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 109a | VB-139 | Done. On `vbnext`, `Join(separator:="-", values:={6, 7})`, `Join(values:=(8, 9), separator:="+")`, and `Join("/", values:={1, 2})` build each named collection literal as the type of the parameter its name selects, matched without regard to case, when no overload applies otherwise and exactly one such parameter type can build it, as phase 106 does for positional arguments. A name that matches no parameter still reports BC30272 and BC30455. Visual Basic 17.13 still reports BC30311. |

## Phase 110 — Collection literals in `If`

Wave 18 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 110a | VB-140 | Done. On `vbnext`, `If(flag, {1}, {2, 3})` converted to a collection type, in `Dim`, `Return`, and the other places a target type is known, builds each `{...}` or `(...)` branch as the target, as C# 12 target-types a conditional expression, so `If(flag, {1}, (2, 2, 3))` builds a `HashSet(Of Integer)`. A literal branch next to a branch of a collection type, as in `If(flag, items, {4, 5})` and `If(items, {7})`, builds as that type, so `If(items, {7})` falls back to a new list only when `items` is `Nothing`. An `If` that already widens to the target keeps its conversion, so an `Integer()` target still gets an array, while the narrowing from `Object` that a mixed `If({1}, (2, 3))` infers no longer fails at run time. Branches with no common type, such as `{1}` and `{"text"}`, still report BC36913. Visual Basic 17.13 still reports BC30311. |

## Phase 111 — Type inference from collection literal arguments

Wave 18 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 111a | VB-138 | Done. On `vbnext`, `Generic({1, 2})`, where `Generic(Of T)` takes a `List(Of T)`, a `HashSet(Of T)`, or another collection type whose one type argument is `T`, builds the literal as that type constructed with the literal's element type, from which `T` is inferred, as C# 12 infers from collection expressions, when no overload applies otherwise. The element type is the one VB gives the literal, so `Generic((1, 2L, 3))` infers `Long`, and `Generic({1, "a"})` infers `Object`, as `Dim x = {1, "a"}` gives `Object()`, where C# finds no best type. A second argument may fix `T` too, as in `Merge(first, {2, 3})`. An empty literal, a literal with a spread, and a parameter whose type argument is not just `T` still report BC36645. Visual Basic 17.13 still reports BC36645. |

## Phase 112 — IDE for wave 18

| Slice | Area | Done when |
| --- | --- | --- |
| 112a | Editor | Done. For the wave 18 forms, on `vbnext`: quick info on `Generic({1, 2})` shows the method constructed with the type argument the literal infers, `Function M.Generic(Of Integer)(values As List(Of Integer)) As Integer`, from the binding of phase 111. Left from phase 94, completion offers `Shared` and `Const` for interface members, for the shared members, fields, and constants of phases 76 and 89, but not `Const` after `Shared`, and neither on 17.13. Phases 108 to 110 add no syntax and need no other editor change. |

## Phase 113 — Positional parameters of inherited properties

Wave 19 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 113a | VB-142 | Done. On `vbnext`, in `Record Class Dog(Name As String, Breed As String)` that `Inherits Animal(Name)`, the parameter `Name` declares no property, because the nearest base type with a member of that name has an accessible instance property `Name` of the same type, as C# 9 reuses it, so `a With {.Name = "Max"}` through an `Animal` reference changes the one `Name` that `Dog` has, and neither BC40003 is reported. The synthesized `Deconstruct` reads inherited properties and hides the base `Deconstruct` as `Overloads`. So that dropping the second property loses no member from equality, a derived record's `Equals` also calls the base record's `Equals` through `MyBase`, its `GetHashCode` starts from the base hash code, and its `ToString` prints the base record's members first, `Puppy { Name = Bo, Breed = Pug, Age = 1 }`, for records that are not positional too. A parameter whose type differs from the inherited property's still declares its own property with BC40003, where C# reports an error, and a parameter that is not passed to the base constructor is dropped without a warning. Only records declared in Visual Basic count as base records. Visual Basic 17.13 does not parse records. |

## Phase 114 — Record `=` and `<>`

Wave 19 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 114a | VB-143 | Done. On `vbnext`, every record declares `Public Shared Overloads Operator =(left, right) As Boolean` and `<>`, emitted as `op_Equality` and `op_Inequality`, so C# code uses them as `==` and `!=`. For a record class `=` is `left Is right OrElse (left IsNot Nothing AndAlso left.Equals(right))`, as C# 9 defines `==`, so `Nothing` equals only `Nothing`; for a record structure it is `left.Equals(right)`, and a `Point?` operand lifts it the VB way, to `Nothing` when either side is `Nothing`. `<>` is `Not (left = right)`. A record that declares either operator with its own type for both operands gets neither, and its own operators keep VB's rule that they come in pairs. Visual Basic 17.13 does not parse records. |

## Phase 115 — Equality of records that inherit records

Wave 19 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 115a | VB-141 | Done. On `vbnext`, records of different runtime types are not equal, as in C# 9: every record class declares `Protected Overridable ReadOnly Property EqualityContract As Type`, which returns its own type and which a derived record overrides, not `Overridable` in a `NotInheritable` record that inherits no record. The typed `Equals(other As R)` is `Overridable`; a record that inherits no record compares `EqualityContract` with `other.EqualityContract`, and a derived record calls its base record's `Equals` through `MyBase`, as phase 113 does, and declares `NotOverridable Overrides Function Equals(other As Base) As Boolean`, which returns `Equals(CObj(other))`, so a comparison through a base reference reaches the derived members. So `New Animal("Rex").Equals(dog)`, `dog.Equals(New Animal("Rex"))`, and `animal = dogAsAnimal` are `False`, `dogAsAnimal.Equals(otherDogAsAnimal)` compares `Breed` too, and the hash code of a record that inherits no record includes its `EqualityContract`. Phase 113 brings the base record's members into `Equals`, `GetHashCode`, and `ToString`. C# 9 can now find the `EqualityContract`, `<Clone>$`, and copy constructor it needs to derive `record Cat(string Name, int Lives) : Animal(Name)` from a Visual Basic record, but still reports CS0115 for the `PrintMembers(StringBuilder)` it overrides, which Visual Basic records do not declare, VB-145. Visual Basic 17.13 does not parse records. |

## Phase 116 — `AddressOf` without a target type

Wave 19 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 116a | VB-144 | Done. On `vbnext`, with `Option Infer On`, `Dim handler = AddressOf Hello` infers VB's anonymous delegate type for the one method of that name, the type `Dim f = Sub() Hello()` already infers, chosen over C# 10's `Action` and `Func` so that `AddressOf` and lambdas agree: `Dim t = AddressOf Twice` gets `Function <generated method>(x As Integer) As Integer`, the same type `Function(x As Integer) x * 2` gets. The delegate keeps the method's `ByRef`, `Optional`, and `ParamArray` parameters, and the group may be an instance method, an extension method, or a generic method with its type arguments, as in `AddressOf Echo(Of String)`. A group with more than one method, including overloads and extension methods together, a generic method without type arguments, and a method that returns `ByRef` still report BC30581, as C# gives no natural type to such groups. Only `Dim` infers it; until phase 118, `Option Infer Off` and an `AddressOf` passed to `Object` or `[Delegate]` still reported BC30581. Visual Basic 17.13 still reports BC30581. |

## Phase 117 — IDE for wave 19

| Slice | Area | Done when |
| --- | --- | --- |
| 117a | Editor | Done. For the wave 19 forms, on `vbnext`: quick info on `a = b` between records shows the synthesized operator, `Operator Point.=(left As Point, right As Point) As Boolean`, and on `Me.EqualityContract` in a derived record shows `ReadOnly Property Dog.EqualityContract As Type`; completion offers `EqualityContract` inside a derived record but not after a record from outside, and offers the inherited `Name` after a derived positional record. Quick info on a local set from `AddressOf Twice` shows `t As 'a`, where `'a` is `Delegate Function (x As Integer) As Integer`, as for a lambda local, and completion offers `Invoke` after it, but not on 17.13. All of it comes from the binding of phases 113 to 116, so no editor code changes. |

## Phase 118 — `AddressOf` to `Object` and `[Delegate]`

Wave 20 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 118a | VB-146 | Done. On `vbnext`, `Dim o As Object = AddressOf Hello`, `Run(AddressOf Twice)`, where `Run` takes a `[Delegate]` or a `MulticastDelegate`, `CType(AddressOf Twice, Object)`, and `TryCast(AddressOf Twice, [Delegate])` create the anonymous delegate phase 116 infers and convert it, as a lambda converts to `Object`. As for a lambda, such a target is worse than any delegate type, so `Pick(AddressOf Hello)` still calls `Pick(a As Action)` over `Pick(o As Object)`. Under `Option Infer Off`, `Dim h = AddressOf Hello` now stores the delegate in an `Object`, as `Dim h = Sub() Hello()` does. Groups phase 116 gives no type, such as overloads, still report BC30581 and BC30939. Visual Basic 17.13 still reports both. |

## Phase 119 — `AddressOf` in `If` and array literals

Wave 20 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 119a | VB-147 | Done. On `vbnext`, the dominant type of `If(flag, AddressOf Hello, AddressOf Bye)` and of `{AddressOf Hello, AddressOf Bye}` counts each `AddressOf` as the anonymous delegate type phase 116 infers, as it counts a lambda, so both infer that type, as C# 10 infers `Action` for `flag ? Hello : Bye`. `AddressOf` and lambdas mix when their signatures and parameter names agree, as in `{AddressOf Twice, AddressOf Square, Function(x As Integer) x + 1}`. Methods with different signatures have no common type, as lambdas with different signatures have none: `Object` is assumed, and `Option Strict On` reports BC36912 and BC36914. Visual Basic 17.13 still reports BC36911, BC36717, and BC30581. |

## Phase 120 — Visual Basic records that inherit C# records

Wave 20 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 120a | VB-148 | Done. A class from metadata with an instance `<Clone>$` method without parameters is a record, as C# 9 recognizes one, so `IsRecord` is `True` for C# record classes, including generic ones, and stays `False` for C# record structures, which `With` already copies as structures, and for other classes. On `vbnext`, `Record Class VbDog(Name As String, Breed As String)` that `Inherits CsAnimal(Name)` therefore reuses the C# `Name`, overrides the C# `EqualityContract` without BC40005, seals an override of the C# `Equals(CsAnimal)`, chains `Equals` and `GetHashCode` to the C# record through `MyBase`, overrides its `<Clone>$`, and prints `VbDog { Name = Rex, Breed = Lab }`, so `New CsAnimal("Rex").Equals(dogAsAnimal)` is `False` both ways and `dogAsAnimal With {.Name = "Max"}` stays a `VbDog`. `tom With {.Name = "Kit"}` on a C# record copies it through its `<Clone>$` method and protected copy constructor, as for a Visual Basic record, instead of reporting BC37377, and `With` on a generic Visual Basic record, such as `Box(Of Integer)`, no longer reports BC30517. Printing a member whose type is a type parameter, as in `Box(Of Integer)`, showed nothing until phase 121. Visual Basic 17.13 does not parse records. |

## Phase 121 — C# records that inherit Visual Basic records

Wave 20 row 4. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 121a | VB-145 | Done. On `vbnext`, every record class declares `Protected Overridable Overloads Function PrintMembers(builder As StringBuilder) As Boolean`, `Private` and not `Overridable` in a `NotInheritable` record that inherits no record, which overrides the base record's, Visual Basic or C#, as C# 9 declares it. It calls `MyBase.PrintMembers(builder)` first and appends `, ` when that printed anything, appends `Name = ` and each public member as an `Object`, and returns whether it printed anything; a record without members of its own returns the base result. `ToString` builds `Name { `, calls `PrintMembers` and appends a space when it returns `True`, then `}`, in a `StringBuilder`; a record structure appends its members inline, as it has no `PrintMembers`. So `public record Cat(string Name, int Lives) : Animal(Name)` compiles against a Visual Basic `Animal` without CS0115, and a Visual Basic program prints `Cat { Name = Tom, Lives = 9 }`, compares it, and copies it with `With`. Because each member is appended as an `Object`, `Box(Of Integer)` prints `Box { Value = 5 }` instead of an empty value, and a member that is `Nothing` prints as empty instead of throwing `NullReferenceException`. A record without members prints `Empty { }`, as in C#, instead of `Empty {  }`. Visual Basic 17.13 does not parse records. |

## Phase 122 — IDE for wave 20

| Slice | Area | Done when |
| --- | --- | --- |
| 122a | Editor | Done. For the wave 20 forms, on `vbnext`: quick info on a local set from `If(flag, AddressOf Hello, AddressOf Bye)` shows `pick As 'a`, and on one set from `{AddressOf Hello, AddressOf Bye}` shows `handlers As 'a()`, where `'a` is `Delegate Sub ()`; quick info on `Me.PrintMembers` in a derived record shows `Function Dog.PrintMembers(builder As Text.StringBuilder) As Boolean (+ 1 overload)`, the overload count Visual Basic shows for any declared `Overrides` method; and completion offers `PrintMembers` inside a derived record but not after a record from outside. Quick info on a C# record from metadata shows `Class CsAnimal`, as for a Visual Basic record, `Class Animal`. All of it comes from the binding of phases 118 to 121, so no editor code changes. |

## Phase 123 — `IEquatable(Of T)` on records

Wave 21 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 123a | VB-150 | Done. On `vbnext`, every record class and record structure lists `IEquatable(Of R)` among its interfaces, and its synthesized `Equals(other As R)` implements `IEquatable(Of R).Equals`, as C# 9 records implement `IEquatable<R>`. So `Same(New Point(1, 2), New Point(1, 2))`, where `Same(Of T As IEquatable(Of T))`, takes a record structure, a record class, or a generic record, and a call through `IEquatable(Of Animal)` on a `Dog` still keeps records of different runtime types apart. A derived record implements `IEquatable(Of Dog)` as well as the `IEquatable(Of Animal)` it inherits. Until phase 128, a record that declared its own `Equals(other As R)` had to add `Implements IEquatable(Of R).Equals`, as any Visual Basic implementation must, or it got BC30149 at the record's name. Visual Basic 17.13 does not parse records. |

## Phase 124 — Unread positional parameters

Wave 21 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 124a | VB-151 | Done. On `vbnext`, a positional parameter that declares no property, because the record declares a member of that name or reuses an inherited property, and that neither an `Inherits` argument nor a field or property initializer of the record names, gets the new warning BC42516, "Parameter 'Owner' is never read. Initialize the member named 'Owner' from it, as in '= Owner', or pass it to the base record.", as C# 9 reports CS8907. So `Record Class Account(Id As Integer, Owner As String)` with its own `Public Property Owner As String` warns, `Public Property Owner As String = Owner` and `Public Count As Integer = Count` keep the value without a warning, and a derived record that passes `Inherits Animal("fixed")` instead of `Inherits Animal(Name)` warns about `Name`. The check reads the names in those places, so `Me.Owner` does not count as reading the parameter. Visual Basic 17.13 does not parse records. |

## Phase 125 — Lambda return types from `AddressOf`

Wave 21 row 3. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 125a | VB-149 | Done. On `vbnext`, a single-line lambda that returns `AddressOf Hello`, or `(AddressOf Twice)`, infers the type phase 116 infers as its return type, so `Dim factory = Function() AddressOf Hello` can be called as `factory()()`, as C# 10 infers `Func<Action>`, and `Pick(Function() AddressOf Twice)`, where `Pick(Of R)` takes a `Func(Of R)`, infers `R` as that delegate type. A multi-line lambda's `Return AddressOf Twice` infers it through the dominant type, as phase 119 made possible. A group with more than one method, such as an overloaded `Log`, still infers `Object` and reports BC30581. Visual Basic 17.13 still infers `Object` and reports BC30581 and BC30454. |

## Phase 126 — IDE for wave 21

| Slice | Area | Done when |
| --- | --- | --- |
| 126a | Editor | Done. For the wave 21 forms, on `vbnext`: quick info on a local set from `Function() AddressOf Hello` shows `factory As 'a`, where `'a` is `Delegate Function () As 'b` and `'b` is `Delegate Sub ()`, and completion offers `Invoke` after `factory().`; quick info on `Same(New Point(1, 2), New Point(1, 2))`, where `Same(Of T As IEquatable(Of T))`, shows `Function M.Same(Of Point)(a As Point, b As Point) As Boolean`, as the record now meets the constraint. BC42516 reaches the editor as any compiler warning does. All of it comes from the binding of phases 123 to 125, so no editor code changes. |

## Phase 127 — Records under a sealed `ToString`

Wave 22 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 127a | VB-152 | Done. On `vbnext`, a record whose nearest inherited `ToString()` is `NotOverridable`, declared in Visual Basic as `NotOverridable Overrides` or in C# 10 as `sealed override`, declares no `ToString`, as C# 10 does, so `Record Class Derived(Id As Integer, Extra As String)` that `Inherits SealedBase(Id)` compiles without BC30267 and prints `SealedBase#1` through the base `ToString`, and so does a record two levels down. Such a record still declares `PrintMembers`, which chains to the base record's, so `PrintMembers(builder)` writes `Id = 1, Extra = x`. Visual Basic 17.13 does not parse records. |

## Phase 128 — Declared typed `Equals` on records

Wave 22 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 128a | VB-153 | Done. Settled: a record's declared `Equals(other As R)` implements `IEquatable(Of R).Equals` without an `Implements` clause, as in C# 9, because the record declares `IEquatable(Of R)` itself. On `vbnext`, an instance method of a record class or structure named `Equals` that takes one `R`, not `ByRef`, and returns `Boolean`, and has no `Implements` clause, implements it, so `Record Class Loose` with its own `Public Overloads Function Equals(other As Loose) As Boolean` compiles without BC30149, and a call through `IEquatable(Of Loose)` and the record's `=` reach the declared method. An `Implements IEquatable(Of R).Equals` clause still works, and other overloads of `Equals` implement nothing. Declaring an `Equals` or a `Deconstruct` beside the synthesized ones also no longer fails in emit, where unifying the metadata names of a member's overloads reached the synthesized members. Visual Basic 17.13 does not parse records. |

## Phase 129 — IDE for wave 22

| Slice | Area | Done when |
| --- | --- | --- |
| 129a | Editor | Done. For the wave 22 forms, on `vbnext`: quick info on `d.ToString()`, where `d` is a record under a `NotOverridable` base `ToString`, shows the base method, `Function SealedBase.ToString() As String (+ 1 overload)`, and completion still offers `PrintMembers` inside such a record. A record's declared `Equals(other As R)` no longer gets the BC30149 squiggle, as the compiler of phase 128 reports none. All of it comes from the binding of phases 127 and 128, so no editor code changes. |

## Phase 130 — `[Delegate]` and `MulticastDelegate` constraints

Wave 23 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 130a | VB-154 | Done. On `vbnext`, `[Delegate]` and `MulticastDelegate` can constrain a type parameter, as C# 7.3 allows, so `Combine(Of T As [Delegate])(a As T, b As T) As T` compiles, `Combine(Of Action)(...)` combines two lambdas, `Combine(New Action(AddressOf Hello), New Action(AddressOf Hello))` infers `T` as `Action`, and `Function MethodName(Of T As MulticastDelegate)(d As T)` reads `d.Method`. `Combine(Of Integer)` reports BC32044, as a type argument that does not derive from the constraint does, and `{Structure, [Delegate]}` reports BC32108, as `Structure` with any class constraint does. `Object`, `ValueType`, and `Array` still report BC32061. Visual Basic 17.13 still reports BC32061. |

## Phase 131 — IDE for wave 23

| Slice | Area | Done when |
| --- | --- | --- |
| 131a | Editor | Done. For the wave 23 forms, on `vbnext`: quick info on `Combine(New Action(AddressOf Hello), New Action(AddressOf Hello))`, where `Combine(Of T As [Delegate])`, shows `Function M.Combine(Of Action)(a As Action, b As Action) As Action`, and completion after a parameter of type `T As [Delegate]` offers the members of `[Delegate]`, such as `DynamicInvoke`. It comes from the binding of phase 130, so no editor code changes. |

## Wave 24 — hardening

Wave 24 adds no language features. It checks the `vbnext` forms of waves 1 to 23 where the per-slice loop does not: on CoreCLR, through the operation trees and control flow graphs that analyzers read, and in Visual Studio.

### Phase 132 — CoreCLR runs

| Slice | Area | Done when |
| --- | --- | --- |
| 132a | Compiler tests | Done. The Syntax, Semantic, Symbol, and Emit suites run on `net10.0` as well as `net472`, which runs the tests marked `CoreClrOnly`, those that need .NET 7 or later, in about 40 Emit test files. Two of them failed, both in the tests: `ReadOnlyMembersDoNotChangeTheStructure` expected IL that displays `Function Point2.Length()`, from before read-only structure members displayed as `ReadOnly Function`, and `CSharpRecord_DerivesFromAVisualBasicRecord` compiled its C# record against `mscorlib` while the Visual Basic record it derives from used the CoreCLR references, so C# reported CS8869. Both now pass on both runtimes; apart from `CodepageOptionUnicodeMembers01`, as on `net472` until slice 133b, every test passes on `net10.0`. |

### Phase 133 — Operation trees and control flow

| Slice | Area | Done when |
| --- | --- | --- |
| 133a | IOperation | Done. One test binds an `Imports` tuple alias, tuple `=` and `<>`, nullable tuple `=`, `With` on a structure and on an anonymous type, `?.` compound and `?=` assignment, collection literals with spreads into `List(Of T)` and `HashSet(Of T)`, as positional, named, and inferred arguments, and in `If`, an `Optional` lambda parameter, record `=`, `<>`, and `With`, and `AddressOf` in `Dim`, to `Object`, in `If`, in an array literal, and returned from a lambda, then checks the operation tree and control flow graph of each method and lists every `OperationKind.None`. Tuple `=` and `<>`, including the lifted comparison of nullable tuples, bound to a sequence that the operation factory reported as `None`; they now report `ITupleBinaryOperation` with the two operands, as C# reports `==` and `!=` on tuples, recognized from the sequence that stores the operands before comparing their elements. `With` on a structure or an anonymous type still reports `None`, a known limitation, as the copy is a sequence of a temporary and member assignments for which C# reports `IWithOperation`; the test locks that list to exactly those two forms. Every other form already maps to an existing operation. |
| 133b | IOperation | Done. `MethodTests.CodepageOptionUnicodeMembers01`, which failed in every run of the Symbol suite since target-typed `New` and was taken for a code page problem, failed because its `Return New à–¾(Ÿü)`, whose `–` ends the type name, parses as `New` without a type, as target-typed `New` is written; the return type `à–¾` is an error type, so no conversion binds it, and the placeholder that waits for a target type reached the operation tree, where the control flow graph builder asserts on placeholders it does not know. Such a placeholder, for a `New` whose type is missing, is now an `IInvalidOperation`, as an expression that failed to bind, so `Return New(1)` to an undefined type, `Dim x As Undefined = New(2)`, and the code page test build their control flow graphs, and every suite passes. |

### Phase 134 — Visual Studio smoke tests

| Slice | Area | Done when |
| --- | --- | --- |
| 134a | Visual Studio | Done. With the extensions deployed after wave 22, `vbnext\Start-VbNextVisualStudio.ps1` opens `HelloVbNext` in the `RoslynDev` instance of Visual Studio 2026 Insiders, and the Error List shows 0 errors and 0 warnings for the solution, under Build + IntelliSense, across its 22 files with the `vbnext` forms of waves 5 to 22. In `WaveTwentyTwo.vb`, `Record Class`, `NotOverridable Overrides`, and the records' members classify as in source, CodeLens counts their references, quick info on `Matches(New Sku("ab-1"), New Sku("AB-1"))` shows `Function WaveTwentyTwo.Matches(Of Sku)(a As Sku, b As Sku) As Boolean`, and completion after `vip.`, a `VipBadge`, lists `Deconstruct`, `Equals`, `GetHashCode`, `GetType`, `Level`, `Name` once, and `ToString`, without the protected `EqualityContract` and `PrintMembers`. In `WaveTwenty.vb`, quick info on `pick`, set from `If(..., AddressOf Dbl, AddressOf Inc)`, shows `pick As 'a`, where `'a` is `Delegate Function (value As Integer) As Integer`, and CodeLens counts the `AddressOf` uses of `Inc`. The wave 23 constraint has no sample yet, so it is covered by the editor tests of phase 131. |

## Wave 25

Wave 25 takes the one gap that wave 24 left open, VB-155, then returns to the syntax proposals that earlier waves deferred.

### Phase 135 — Operations for `With` on structures and anonymous types

Wave 25 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 135a | VB-155 | Done. `spot With {.X = .Y + 1}` on a structure and `anon With {.B = 3}` on an anonymous type report `IWithOperation`, as C# reports `with` on both, instead of `OperationKind.None`: the operand is the original, the clone method is `Nothing`, and the initializer assigns each listed member of the implicit receiver, with `.Y` in a value of a structure copy reading the implicit receiver too, so no synthesized temporary appears in the tree, and `GetOperation` on the `With` returns it. The operation factory recognizes the sequence that phases 97 and 98 bind, which stores the original in a temporary, and those sequences are no longer compiler generated. An anonymous type's `With` whose value reads the original, as `anon With {.B = .A + 10}` does, still reports `OperationKind.None`, because C# cannot read the original in that initializer, so the control flow graph builder has no receiver there. |

### Phase 136 — Deferred syntax proposals

| Slice | Id | Done when |
| --- | --- | --- |
| 136a | VB-051 | Done. After wave 24, every syntax proposal is either implemented or rejected except VB-051, concise lambdas. Reviewed again against `docs/vbnext-vb051-syntax.md`: `_.Name` collides with line continuation, `Fn(c) c.Name` with an invocation followed by an index, and a leading `.Name` with the object of a `With` block, and no other spelling has been proposed. It stays deferred; `Function(c) c.Name` stays the spelling. |

## Wave 26

### Phase 137 — `NameOf` of parameters in attributes

Wave 26 row 1. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 137a | VB-156 | Done. On `vbnext`, `NameOf(value)` in an attribute on a method, on one of its parameters, or on its return type finds the method's parameter `value`, as C# 11 finds it, so `<NotNullIfNotNull(NameOf(value))>` on a parameter or a return type and `<Note(NameOf(count))>` on a method compile and store the parameter's name, and the semantic model gives the parameter for the `NameOf` argument. The attribute binders of such methods, for compilation and for the semantic model, look the method's parameters up only for a lookup inside a `NameOf` argument, so `<Note(text)>` still reports BC30451, and so does `NameOf(value)` in an attribute on a field. Visual Basic 17.13 still reports BC30451. |

### Phase 138 — Index and range on lists and strings

Wave 26 row 2. No new syntax.

| Slice | Id | Done when |
| --- | --- | --- |
| 138a | VB-157 | Open. On `vbnext`, `list(^1)` and `s(^1)` index from the end of any type with a `Count` or `Length` and an `Integer` indexer, and `list(1..3)` and `s(1..3)` slice any type with a `Count` or `Length` and a `Slice(start, length)` method, and a `String` through `Substring`, evaluating the receiver once, as C# 8 does. Visual Basic 17.13 still reports BC37358 and BC37359. |

### Phase 139 — IDE for wave 26

| Slice | Area | Done when |
| --- | --- | --- |
| 139a | Editor | Open. For the wave 26 forms, on `vbnext`: quick info, completion, and classification follow the binding of phases 137 and 138. |

## What starts now

The direction in `docs/vbnext-roadmap.md` adopts VB-050, VB-053, and VB-060, rejects VB-058, and defers VB-051. Slices 16b, 18b, and 21b are done. Every adopted wave 4 proposal now has an implementation on `vbnext`. Phase 23 brings the editor along, and slices 23a through 23e are done. Phase 24 is done: a real project compiles with the Vb.Next compiler, with analyzers on, and the editor runs in Visual Studio 2026 Insiders. Wave 5 is planned as phases 25 to 29. Phase 25 is done. Phase 26 is done: slice 26a locks the asynchronous stream spellings, slice 26b adds `Async Using`, slice 26c adds `Await Each`, and slice 26d adds `Async Iterator Function`. Phase 27 is done: slice 27a locks `Into` for pattern variables, and slice 27b implements it. Phase 28 is done: slice 28a locks `Field` for property backing storage, and slice 28b implements it. Phase 29 is done: slice 29a brings the editor along for the wave 5 forms. Wave 5 is done. Wave 6 is planned as phases 30 to 34, from the gaps that wave 5 code meets next. Phase 30 is done: slice 30a lets `Await Each` take `WithCancellation` and `ConfigureAwait`, and slice 30b lets `Async Using` take `ConfigureAwait`. Phase 31 is done: slice 31a lets a `Catch` block await, and slice 31b lets a `Finally` block await. Phase 32 is done: slice 32a lets an `Async Function` return `ValueTask` and `ValueTask(Of T)`, and slice 32b lets it return any task-like type and choose its builder. Slices 33a and 33b are done: `Async Iterator Function` is locked for lambdas, and `If TypeOf value IsNot T Into name Then ...` is locked as a guard. Phase 33 is done: slice 33c lets an `Async Iterator Function` lambda await and yield, and slice 33d adds the `IsNot ... Into` guard. Phase 34 is done: slice 34a brings the editor along for the wave 6 forms. Wave 6 is done. Wave 7 is planned as phases 35 to 40, from the gaps that wave 6 code meets next. Phase 35 is done: slice 35a reads `ref readonly` returns, including `ReadOnlySpan(Of T)` elements. Phase 36 is done: slice 36a releases a ref structure in `Using` through its `Dispose`. Phase 37 is done: slice 37a lets an `Async` method declare span locals that do not cross an `Await`, and slice 37b applies the same rule to `Yield`. Phase 38 is done: slice 38a lets lambda parameters carry attributes, and slice 38b lets `EnumeratorCancellation` reach an `Async Iterator Function` lambda. Phase 39 is done: slice 39a locks `For Each (a, b) In collection`, slice 39b deconstructs each item of a `For Each`, and slice 39c does the same for `Await Each`. Phase 40 is done: slice 40a brings the editor along for the wave 7 forms. Wave 7 is done. Wave 8 is planned as phases 41 to 46, from the gaps that wave 7 code meets next. Phase 41 is done: slice 41a emits implementations of shared abstract members that .NET can load. Phase 42 is done: slices 42a and 42b reach shared abstract, shared virtual, and generic shared members from any assembly through a type parameter, and slice 42c lets `Object` members such as `ToString()` overload the constraint's members. Phase 43 is done: slice 43a binds operators through a type parameter's constraints, and slice 43b adds compound assignment and checked operators. Phase 44 is done: slice 44a writes through a `ByRef` return whose receiver is a value. Phase 45 is done: slices 45a to 45e make `ByRef` where it has no meaning an error, lock `Dim ByRef name = reference`, declare locals that refer to variables, add `Dim ByRef ReadOnly`, and apply the phase 37 rule in `Async` and `Iterator` methods. Phase 46 is done: slice 46a brings the editor along for the wave 8 forms. Wave 8 is done. Wave 9 is planned as phases 47 to 52, from the gaps that wave 8 code meets next. Phase 47 is done: slice 47a no longer requires shared virtual members that have a default body. Phase 48 is done: slice 48a lets `Shared` properties implement shared abstract properties. Phase 49 is done: slice 49a locks implicit implementation of interface operators by signature, and slice 49b implements it. Slice 50a is done: ref fields are locked as `Private ByRef _value As Integer`, set with `_value = ByRef value` in `Sub New`. Slice 50b is done: a `ByRef Structure` declares ref fields, sets them in `Sub New`, and reads and writes through them on .NET 7 or later. Slice 50c is done: `ByRef ReadOnly` ref fields refer to a variable without writing through it. Phase 50 is done: slice 50d keeps a structure that holds a ref field from outliving the variables it refers to. Phase 51 is done: slice 51a indexes, converts, and iterates inline arrays. Phase 52 is done: slice 52a brings the editor along for the wave 9 forms. Wave 9 is done. Wave 10 is planned as phases 53 to 58, from the gaps that wave 9 code meets next. Phase 53 is done: slice 53a makes delegates to shared abstract and virtual members through a type parameter. Phase 54 is done: slice 54a reads the elements of inline array values. Phase 55 is done: slice 55a reads and writes ref fields declared in C#. Phase 56 is done: slice 56a locks that a write through a `Dim ByRef` local follows its referent, and slice 56b checks it. Phase 57 is done: slice 57a locks when a ref structure value refers to local storage, and slice 57b keeps such values from leaving the method and lets factories return structures that hold ref fields. Phase 58 is done: slice 58a needs no editor change, because phases 56 and 57 add no syntax. Wave 10 is done. Wave 11 is planned as phases 59 to 63, from the gaps that wave 10 code meets next. Phase 59 is done: slice 59a names unbound generic types. Phase 60 is done for methods: slice 60a locks `Shared Overridable`, and slice 60b declares shared virtual interface functions and subs. Phase 61 is done: slice 61a locks `ByRef ReadOnly Function`, and slice 61b declares it. Phase 62 is done: slice 62a locks `ByRef ReadOnly` parameters, and slice 62b declares them. Phase 63 is done: slice 63a brings the editor along for the wave 11 forms. Wave 11 is done. Wave 12 is planned as phases 64 to 71, from the gaps that wave 11 code meets next. Phase 64 is done: slice 64a calls, overrides, and implements members from other assemblies with `in` parameters. Phase 65 is done: slice 65a names qualified unbound generic types. Phase 66 is done: slice 66a converts lambdas to delegates with `ByRef ReadOnly` parameters. Phase 67 is done: slice 67a lets operators take `ByRef ReadOnly` operands. Phase 68 is done: slice 68a declares shared abstract and shared virtual properties in interfaces. Phase 69 is done: slice 69a locks `Shared MustOverride Operator` and `Shared Overridable Operator` for interface operators, and slice 69b declares them. Phase 70 is done: slice 70a locks `ReadOnly Function`, `ReadOnly Sub`, and `ReadOnly Get` for read-only structure members, and slice 70b declares them. Phase 71 is done: slice 71a brings the editor along for the wave 12 forms. Wave 12 is done. Wave 13 is planned as phases 72 to 80, from the gaps that wave 12 code meets next. Phase 72 is done: slice 72a calls C# `params` collections with separate arguments. Phase 73 is done: slice 73a names members of unbound generic types. Phase 74 is done: slice 74a lets conversions, `IsTrue`, and `IsFalse` take `ByRef ReadOnly` operands and lifts such operators. Phase 75 is done: slice 75a declares instance interface properties with a default body. Phase 76 is done: slice 76a declares shared interface members with a body that are not virtual. Phase 77 is done: slice 77a applies generic attributes by their short names. Phase 78 is done: slice 78a reads C# 14 extension properties and static extension members. Phase 79 is done: slice 79a locks interface conversion operators with the phase 69 spelling, and slice 79b declares them and converts through type parameters with them. Phase 80 is done: slice 80a brings the editor along for the wave 13 forms. Wave 13 is done. Wave 14 is planned as phases 81 to 87, from the gaps that wave 13 code meets next. Phase 81 is done: slice 81a calls and declares the other `params` collection types. Phase 82 is done: slice 82a calls C# 14 compound assignment operators. Phase 83 is done: slice 83a uses C# 14 extension operators. Phase 84 is done: slice 84a reads generic C# 14 extension blocks. Phase 85 is done: slice 85a lets ref structures meet interface constraints. Phase 86 is done: slice 86a finds that slice 7m's `Of T As ByRef` already declares type parameters that take ref structures and withdraws `Of ByRef T`, and slice 86b tests it with phase 85. Phase 87 is done: slice 87a brings the editor along for the wave 14 forms. Wave 14 is done. Wave 15 is planned as phases 88 to 94, from the gaps that wave 14 code meets next. Phase 88 is done: slice 88a lets interface members with a body be `Private`, `Friend`, or `Protected`. Phase 89 is done: slice 89a declares shared fields, constants, and a shared constructor in interfaces. Phase 90 is done: slice 90a declares custom events with default accessors in interfaces. Phase 91 is done: slice 91a lets lambda parameters be `Optional` and `ParamArray`. Phase 92 is done: slice 92a uses the operators of generic C# 14 extension blocks. Phase 93 is done: slice 93a names tuple types with `Imports` aliases. Phase 94 is done: slice 94a brings the editor along for the wave 15 forms. Wave 15 is done. Wave 16 is planned as phases 95 to 101, from the gaps that wave 15 code meets next. Phase 95 is done: slice 95a names predefined, array, and nullable types with `Imports` aliases. Phase 96 is done: slice 96a compares tuples with `=` and `<>`. Phase 97 is done: slice 97a copies any structure with `With`. Phase 98 is done: slice 98a copies anonymous types with `With`. Phase 99 is done: slice 99a assigns with compound operators through `?.`. Phase 100 is done: slice 100a builds collection types from array literals. Phase 101 is done: slice 101a brings the editor along for the wave 16 forms. Wave 16 is done. Wave 17 is planned as phases 102 to 107, from the gaps that wave 16 code meets next. Phase 102 is done: slice 102a spreads into collections from array literals. Phase 103 is done: slice 103a compares nullable tuples with VB's lifted `=` and `<>`. Phase 104 is done: slice 104a assigns with `?=` through `?.`. Phase 105 is done: slice 105a calls span extension methods on arrays, spans, and strings. Phase 106 is done: slice 106a passes collection literals to collection parameters. Phase 107 is done: slice 107a brings the editor along for the wave 17 forms. Wave 17 is done. Wave 18 is planned as phases 108 to 112, from the gaps that wave 17 code meets next. Phase 108 is done: slice 108a spreads into collections that have an `IEnumerable(Of T)` constructor instead of `AddRange`. Phase 109 is done: slice 109a passes collection literals as named arguments. Phase 110 is done: slice 110a gives collection literals in `If` the target type. Phase 111 is done: slice 111a infers type arguments from collection literal arguments. Phase 112 is done: slice 112a brings the editor along for the wave 18 forms. Wave 18 is done. Wave 19 is planned as phases 113 to 117, from the gaps that wave 18 code meets next, in records that inherit records and in `AddressOf`. Phase 113 is done: slice 113a lets a derived positional record reuse the properties it inherits and brings the base record's members into its equality and `ToString`. Phase 114 is done: slice 114a declares `=` and `<>` on records. Phase 115 is done: slice 115a keeps records of different runtime types from being equal, through `EqualityContract`. Phase 116 is done: slice 116a infers VB's anonymous delegate type for `Dim handler = AddressOf Hello`. Phase 117 is done: slice 117a brings the editor along for the wave 19 forms. Wave 19 is done. Wave 20 is planned as phases 118 to 122, from the gaps that wave 19 code meets next, in `AddressOf` outside `Dim` and in records that cross between Visual Basic and C#. Phase 118 is done: slice 118a converts `AddressOf` to `Object`, `[Delegate]`, and `MulticastDelegate`. Phase 119 is done: slice 119a infers the type of `If` and array literals from `AddressOf`. Phase 120 is done: slice 120a recognizes C# records from metadata, so Visual Basic records inherit them and `With` copies them. Phase 121 is done: slice 121a declares `PrintMembers`, so C# records inherit Visual Basic records. Phase 122 is done: slice 122a brings the editor along for the wave 20 forms. Wave 20 is done. Wave 21 is planned as phases 123 to 126, from the gaps that wave 20 code meets next, in records and in lambdas that return `AddressOf`. Phase 123 is done: slice 123a implements `IEquatable(Of R)` on records. Phase 124 is done: slice 124a warns about positional parameters that a record never reads. Phase 125 is done: slice 125a infers the return type of lambdas that return `AddressOf`. Phase 126 is done: slice 126a brings the editor along for the wave 21 forms. Wave 21 is done. Wave 22 is planned as phases 127 to 129, from the gaps that wave 21 code meets next, in records under a sealed `ToString` and in a record's declared typed `Equals`. Phase 127 is done: slice 127a keeps a sealed base `ToString` in records that derive from it. Phase 128 is done: slice 128a lets a record's declared `Equals(other As R)` implement `IEquatable(Of R)` without an `Implements` clause. Phase 129 is done: slice 129a brings the editor along for the wave 22 forms. Wave 22 is done. Wave 23 is planned as phases 130 and 131, from the gaps that newer C# and .NET code meets next, in `[Delegate]` and `MulticastDelegate` constraints. Phase 130 is done: slice 130a allows `[Delegate]` and `MulticastDelegate` constraints. Phase 131 is done: slice 131a brings the editor along for the wave 23 forms. Wave 23 is done. Wave 24 is a hardening wave, planned as phases 132 to 134. Phase 132 is done: slice 132a runs the compiler suites on CoreCLR and fixes the two tests that failed only there. Phase 133 is done: slice 133a checks the operation trees of the recent forms and reports tuple comparisons as `ITupleBinaryOperation`; `With` on structures and anonymous types still reports `OperationKind.None`, and slice 133b reports an unbound target-typed `New` as invalid, which fixes the long-failing `CodepageOptionUnicodeMembers01`. Phase 134 is done: slice 134a opens the sample in Visual Studio with no errors and checks quick info, completion, and classification on recent forms. Wave 24 is done. Wave 25 starts with phase 135: slice 135a reports `With` on structures and anonymous types as `IWithOperation`. Phase 136 is done: slice 136a reviews VB-051, the last deferred syntax proposal, which stays deferred. Wave 25 is done. Wave 26 is planned as phases 137 to 139, from a probe of C# 8 to 14 and .NET 8 to 11 features, in `NameOf` of parameters in attributes and in index and range on lists and strings. Phase 137 is done: slice 137a puts a method's parameters in scope for `NameOf` in its attributes.
