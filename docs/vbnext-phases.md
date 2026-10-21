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
| 7h | VB-037 | Done. On `vbnext`, `Span(Of T)` is a ref-like type. A local of that type is legal and can be emitted. It cannot be boxed or stored as a field of a class. Visual Basic 17.13 still reports that the type is obsolete. |
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
| 33b | VB-072 | `docs/vbnext-vb072-syntax.md` records the proposed forms of a type test that guards the rest of a block, such as `If TypeOf value IsNot T Into name Then Return`, and where the name is in scope, and locks or defers them. No code change. |
| 33c | VB-071 | On `vbnext`, the form locked in 33a is a lambda that both awaits and yields, using the slice 26d lowering. |
| 33d | VB-072 | On `vbnext`, the form locked in 33b declares a name that is definitely assigned where the test guards it. |

## Phase 34 — IDE for wave 6

| Slice | Area | Done when |
| --- | --- | --- |
| 34a | Editor | For each form locked in phase 33: Enter inserts its `End` statement where it has one, its keywords classify and complete only on `vbnext`, and formatting indents its body. |

## What starts now

The direction in `docs/vbnext-roadmap.md` adopts VB-050, VB-053, and VB-060, rejects VB-058, and defers VB-051. Slices 16b, 18b, and 21b are done. Every adopted wave 4 proposal now has an implementation on `vbnext`. Phase 23 brings the editor along, and slices 23a through 23e are done. Phase 24 is done: a real project compiles with the Vb.Next compiler, with analyzers on, and the editor runs in Visual Studio 2026 Insiders. Wave 5 is planned as phases 25 to 29. Phase 25 is done. Phase 26 is done: slice 26a locks the asynchronous stream spellings, slice 26b adds `Async Using`, slice 26c adds `Await Each`, and slice 26d adds `Async Iterator Function`. Phase 27 is done: slice 27a locks `Into` for pattern variables, and slice 27b implements it. Phase 28 is done: slice 28a locks `Field` for property backing storage, and slice 28b implements it. Phase 29 is done: slice 29a brings the editor along for the wave 5 forms. Wave 5 is done. Wave 6 is planned as phases 30 to 34, from the gaps that wave 5 code meets next. Phase 30 is done: slice 30a lets `Await Each` take `WithCancellation` and `ConfigureAwait`, and slice 30b lets `Async Using` take `ConfigureAwait`. Phase 31 is done: slice 31a lets a `Catch` block await, and slice 31b lets a `Finally` block await. Phase 32 is done: slice 32a lets an `Async Function` return `ValueTask` and `ValueTask(Of T)`, and slice 32b lets it return any task-like type and choose its builder. Slice 33a is done: `Async Iterator Function` is locked for lambdas. Slice 33b, the design note for type tests that guard the rest of a block, starts now.
