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
| 10. Nullability | 35 | Annotations, then flow, then metadata. Last. |

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
| 5a | VB-005 | Done. On `vbnext`, `Dim (x, y) = point` declares locals from a tuple. The tuple is evaluated once, and each name takes the element in that position. A value that is not a tuple of the same length is an error. Visual Basic 17.13 still treats the parentheses as array bounds. |
| 5b | VB-005 | Done. On `vbnext`, `(x, y) = point` assigns each element into an existing variable. The tuple is evaluated once. A value that is not a tuple of the same length is an error. Visual Basic 17.13 still reports a syntax error on `(`. |
| 5c | VB-005 | Done. On `vbnext`, `Dim (x, , z) = point` and `(x, , z) = point` skip the empty slot. The empty slot still counts toward the tuple length, and the tuple is evaluated once. Visual Basic 17.13 still treats the parentheses as array bounds or a syntax error. |
| 5d | VB-005 | Done. On `vbnext`, a type that is not a tuple is deconstructed by an accessible method named `Deconstruct` whose `ByRef` parameters match the slots. An extension method is used when the type has no instance method of that name. The value is evaluated once, and an empty slot is still passed to the method. A value that is neither a tuple of that length nor a match for `Deconstruct` is an error. |
| 5e | VB-004 | Done. On `vbnext`, `Case Is TypeOf Integer AndAlso Is > 0` matches when the Select expression has that type and passes the relational test. `Case Is TypeOf String` is the type test alone. The Select expression is evaluated once. Visual Basic 17.13 still reports an expected relational operator. |
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
| 8b | VB-031 | Done. On `vbnext`, `a With { .Y = 3 }` copies record `a` and assigns the listed members. An init-only property can be set on the copy, and the original record is unchanged. Visual Basic 17.13 still rejects `With` after an expression. |
| 8c | VB-031 | Done. On `vbnext`, `Record Class Point(X As Integer, Y As Integer)` stores each parameter in an init-only property. `New Point(1, 2)` constructs it, and `Dim (x, y) = point` deconstructs it. |
| 8d | VB-031 | Done. On `vbnext`, `Record Structure Point` emits value equality, `GetHashCode`, and `ToString` of the form `Point { X = 1, Y = 2 }`. `a With { .Y = 3 }` copies it and can assign an init-only property. Visual Basic 17.13 still rejects `Record` before `Structure`. |

## Phase 9 — Interfaces

| Slice | Id | Done when |
| --- | --- | --- |
| 9a | VB-038 | Done. On `vbnext`, an interface method may have a body. A class that implements the interface and does not supply the method uses that body. The class may still supply its own with `Implements`. A method without a body still must be implemented. Visual Basic 17.13 still rejects a body inside an interface. |
| 9b | VB-038 | Done. On `vbnext`, when a class implements a derived interface, the most specific default body is the implementation. A derived body covers an abstract base method. Two equally specific bodies still leave the base method unimplemented. |
| 9c | VB-039 | Done. On `vbnext`, `Extension(s As String)` ... `End Extension` inside a module may declare a property. `text.WordCount` calls that property with `text` as the receiver. Visual Basic 17.13 does not parse `Extension` as a block. |
| 9d | VB-039 | Done. On `vbnext`, an extension block may declare a parameterized property. `box(index)` calls it when the type has no default property of its own, and both the receiver and the index are passed. |
| 9e | VB-039 | An extension block may add a shared member. |
| 9f | VB-036 | An interface may declare a `Shared MustOverride` member. |
| 9g | VB-036 | A type parameter constrained to that interface can call the shared member. |

## Phase 10 — Nullability

The spelling is decided in 10a. Parser work starts only after that note. `?` on a value type remains `Nullable(Of T)`.

| Slice | Id | Done when |
| --- | --- | --- |
| 10a | VB-030 | A design note chooses the annotation spelling and the project switch. No code change. |
| 10b | VB-030 | Annotations are stored on locals, parameters, and return types. No warnings yet. |
| 10c | VB-030 | Flow analysis warns on the experimental version. |
| 10d | VB-030 | The compiler emits the nullable metadata attributes. |
| 10e | VB-030 | Annotations work on generics and on overridden signatures. |

## What starts now

Slice 9d, an extension indexer, is implemented. The next slice is 9e, an extension block that adds a shared member.
