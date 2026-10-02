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
| 3a | VB-002 | An auto-property can be declared init-only, and a constructor can assign it. |
| 3b | VB-002 | Assignment outside a constructor is an error. Implementing an init-only property matches the accessor. |
| 3c | VB-011 | `required` on a property must be set in an object initializer. |
| 3d | VB-011 | A constructor can opt out with the sets-required-members attribute. |
| 3e | VB-014 | An attribute class may be generic, and a use site may pass type arguments. |
| 3f | VB-018 | `Structure` can be declared readonly. Instance members that mutate are errors. |
| 3g | VB-020 | `nint` and `nuint` are type names with the conversions `Integer` and `Long` already use for native integers. |

## Phase 4 — Index and text

| Slice | Id | Done when |
| --- | --- | --- |
| 4a | VB-006 | `^n` is an index from the end on an array. |
| 4b | VB-006 | `start..end` is a range on an array. `Span` slicing stays in phase 7. |
| 4c | VB-017 | An override may narrow a class return type. The emitted signature carries the covariant return. |
| 4d | VB-033 | A raw string with no holes scans and binds as a string. |
| 4e | VB-033 | A raw string may contain interpolation holes. |
| 4f | VB-034 | A `u8` suffix on a string literal emits a UTF-8 byte span. This slice may emit a byte array if ref structs are not available yet. The span form is finished in phase 7. |

## Phase 5 — Matching

Deconstruction finishes before pattern matching. Records in phase 8 use both.

| Slice | Id | Done when |
| --- | --- | --- |
| 5a | VB-005 | `Dim (x, y) = point` declares locals from a tuple. |
| 5b | VB-005 | Deconstruction assigns into existing variables. |
| 5c | VB-005 | A discard skips one element. |
| 5d | VB-005 | A method named `Deconstruct` is used when the type is not a tuple. |
| 5e | VB-004 | `Case` accepts a logical combination of type and relational tests that already exist separately. |
| 5f | VB-004 | Property patterns. |
| 5g | VB-004 | Positional patterns on tuples and on types with `Deconstruct`. |
| 5h | VB-004 | A `Select Case` expression produces a value. List patterns wait until phase 7. |

5e through 5h each need the syntax sketch from the VB-004 proposal. That proposal is written during phase 2. It does not start parser work early.

## Phase 6 — Type shape

| Slice | Id | Done when |
| --- | --- | --- |
| 6a | VB-015 | A partial property parses, and the definition and implementation match. |
| 6b | VB-015 | Accessors can be split between the two parts. |
| 6c | VB-016 | Partial events use the same match. |
| 6d | VB-016 | Partial constructors use the same match. |
| 6e | VB-032 | Primary constructor parameters are in scope in instance initializers. |
| 6f | VB-032 | A primary constructor can chain to `MyBase` or `Me`. |
| 6g | VB-032 | The same form works on a `Structure`. |

## Phase 7 — Collections and memory

| Slice | Id | Done when |
| --- | --- | --- |
| 7a | VB-035 | A collection expression targets an array. |
| 7b | VB-035 | A collection expression targets a list or a type with a collection initializer. |
| 7c | VB-035 | Spread copies an array or list into that target. |
| 7d | VB-026 | `ParamArray` accepts a list or `IEnumerable` expansion. |
| 7e | VB-019 | A `Function` may return `ByRef`. |
| 7f | VB-019 | A property may return `ByRef`. |
| 7g | VB-037 | A structure can be declared `ref`-like: no boxing, no field of a class, no async or iterator lift. |
| 7h | VB-037 | `Span(Of T)` is legal in the synchronous positions 7g allows. |
| 7i | VB-006 | Range and index syntax from phase 4 apply to `Span(Of T)`. |
| 7j | VB-034 | The UTF-8 literal from 4f has type `ReadOnlySpan(Of Byte)` when 7h is in. |
| 7k | VB-035 | Spread and a collection expression may target a span. |
| 7l | VB-026 | `ParamArray` of `Span(Of T)` is legal. |
| 7m | VB-037 | A type parameter may be constrained to allow a ref-like type. |

## Phase 8 — Records

Depends on phase 3 (init-only, required) and slice 5d (deconstruction). Positional records also depend on slice 6e.

| Slice | Id | Done when |
| --- | --- | --- |
| 8a | VB-031 | A record class emits value equality, `GetHashCode`, and a printed form. |
| 8b | VB-031 | A non-destructive `With` copy compiles and uses the init-only properties from 3a. |
| 8c | VB-031 | A positional record uses a primary constructor and a `Deconstruct` method. |
| 8d | VB-031 | A record structure follows 8a and 8b. |

## Phase 9 — Interfaces

| Slice | Id | Done when |
| --- | --- | --- |
| 9a | VB-038 | An interface method may have a body. A class inherits that body when it does not supply one. |
| 9b | VB-038 | The most specific default body is chosen when two interfaces contribute one. |
| 9c | VB-039 | An extension block may add a property. |
| 9d | VB-039 | An extension block may add an indexer. |
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

Slice 2g, null-conditional assignment, is implemented. The next slice is 3a, an init-only auto-property.
