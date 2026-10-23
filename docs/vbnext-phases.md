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
| 56b | VB-092 | On `vbnext`, the rules that 56a locks are checked. |

## Phase 57 — Returning ref field structures

Wave 10 row 5.

| Slice | Id | Done when |
| --- | --- | --- |
| 57a | VB-093, VB-094 | A design note locks when a function or property may return a structure that holds a ref field, or a span over an inline array, and what each may refer to. No code change. |
| 57b | VB-093 | On `vbnext`, the rules that 57a locks replace BC37414 for the cases they allow. |

## Phase 58 — IDE for wave 10

| Slice | Area | Done when |
| --- | --- | --- |
| 58a | Editor | For any syntax locked in phases 56 and 57: its keywords classify and complete only on `vbnext`. |

## What starts now

The direction in `docs/vbnext-roadmap.md` adopts VB-050, VB-053, and VB-060, rejects VB-058, and defers VB-051. Slices 16b, 18b, and 21b are done. Every adopted wave 4 proposal now has an implementation on `vbnext`. Phase 23 brings the editor along, and slices 23a through 23e are done. Phase 24 is done: a real project compiles with the Vb.Next compiler, with analyzers on, and the editor runs in Visual Studio 2026 Insiders. Wave 5 is planned as phases 25 to 29. Phase 25 is done. Phase 26 is done: slice 26a locks the asynchronous stream spellings, slice 26b adds `Async Using`, slice 26c adds `Await Each`, and slice 26d adds `Async Iterator Function`. Phase 27 is done: slice 27a locks `Into` for pattern variables, and slice 27b implements it. Phase 28 is done: slice 28a locks `Field` for property backing storage, and slice 28b implements it. Phase 29 is done: slice 29a brings the editor along for the wave 5 forms. Wave 5 is done. Wave 6 is planned as phases 30 to 34, from the gaps that wave 5 code meets next. Phase 30 is done: slice 30a lets `Await Each` take `WithCancellation` and `ConfigureAwait`, and slice 30b lets `Async Using` take `ConfigureAwait`. Phase 31 is done: slice 31a lets a `Catch` block await, and slice 31b lets a `Finally` block await. Phase 32 is done: slice 32a lets an `Async Function` return `ValueTask` and `ValueTask(Of T)`, and slice 32b lets it return any task-like type and choose its builder. Slices 33a and 33b are done: `Async Iterator Function` is locked for lambdas, and `If TypeOf value IsNot T Into name Then ...` is locked as a guard. Phase 33 is done: slice 33c lets an `Async Iterator Function` lambda await and yield, and slice 33d adds the `IsNot ... Into` guard. Phase 34 is done: slice 34a brings the editor along for the wave 6 forms. Wave 6 is done. Wave 7 is planned as phases 35 to 40, from the gaps that wave 6 code meets next. Phase 35 is done: slice 35a reads `ref readonly` returns, including `ReadOnlySpan(Of T)` elements. Phase 36 is done: slice 36a releases a ref structure in `Using` through its `Dispose`. Phase 37 is done: slice 37a lets an `Async` method declare span locals that do not cross an `Await`, and slice 37b applies the same rule to `Yield`. Phase 38 is done: slice 38a lets lambda parameters carry attributes, and slice 38b lets `EnumeratorCancellation` reach an `Async Iterator Function` lambda. Phase 39 is done: slice 39a locks `For Each (a, b) In collection`, slice 39b deconstructs each item of a `For Each`, and slice 39c does the same for `Await Each`. Phase 40 is done: slice 40a brings the editor along for the wave 7 forms. Wave 7 is done. Wave 8 is planned as phases 41 to 46, from the gaps that wave 7 code meets next. Phase 41 is done: slice 41a emits implementations of shared abstract members that .NET can load. Phase 42 is done: slices 42a and 42b reach shared abstract, shared virtual, and generic shared members from any assembly through a type parameter, and slice 42c lets `Object` members such as `ToString()` overload the constraint's members. Phase 43 is done: slice 43a binds operators through a type parameter's constraints, and slice 43b adds compound assignment and checked operators. Phase 44 is done: slice 44a writes through a `ByRef` return whose receiver is a value. Phase 45 is done: slices 45a to 45e make `ByRef` where it has no meaning an error, lock `Dim ByRef name = reference`, declare locals that refer to variables, add `Dim ByRef ReadOnly`, and apply the phase 37 rule in `Async` and `Iterator` methods. Phase 46 is done: slice 46a brings the editor along for the wave 8 forms. Wave 8 is done. Wave 9 is planned as phases 47 to 52, from the gaps that wave 8 code meets next. Phase 47 is done: slice 47a no longer requires shared virtual members that have a default body. Phase 48 is done: slice 48a lets `Shared` properties implement shared abstract properties. Phase 49 is done: slice 49a locks implicit implementation of interface operators by signature, and slice 49b implements it. Slice 50a is done: ref fields are locked as `Private ByRef _value As Integer`, set with `_value = ByRef value` in `Sub New`. Slice 50b is done: a `ByRef Structure` declares ref fields, sets them in `Sub New`, and reads and writes through them on .NET 7 or later. Slice 50c is done: `ByRef ReadOnly` ref fields refer to a variable without writing through it. Phase 50 is done: slice 50d keeps a structure that holds a ref field from outliving the variables it refers to. Phase 51 is done: slice 51a indexes, converts, and iterates inline arrays. Phase 52 is done: slice 52a brings the editor along for the wave 9 forms. Wave 9 is done. Wave 10 is planned as phases 53 to 58, from the gaps that wave 9 code meets next. Phase 53 is done: slice 53a makes delegates to shared abstract and virtual members through a type parameter. Phase 54 is done: slice 54a reads the elements of inline array values. Phase 55 is done: slice 55a reads and writes ref fields declared in C#. Phase 56, `ByRef` local lifetime, starts now with the design note in slice 56a.
