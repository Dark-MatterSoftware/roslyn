# VB-172 Date literals as `DateOnly` and `TimeOnly`

This note records the semantics for VB-172. It is a design note for this independent fork. It is not a Microsoft language proposal, and this project is not affiliated with, endorsed by, or supported by Microsoft.

Classification: Interop/runtime support, through VB's own date literal. There is no new syntax.

## Why this is VB

Visual Basic has had date literals since its first version, and C# has none. A date literal already says whether it holds a calendar date, a time of day, or both. .NET 6 added `System.DateOnly` and `System.TimeOnly` for exactly the first two, and VB code reaches them today only through `DateOnly.FromDateTime(#2024-01-15#)`. Letting the literal convert keeps VB's existing spelling and gives it the types that match what it says.

## The literal converts to the type that matches its form

```vb
Dim day As DateOnly = #2024-01-15#          ' a date-only literal becomes DateOnly
Dim time As TimeOnly = #10:30 AM#            ' a time-only literal becomes TimeOnly
Dim due As DateOnly? = #2024-02-01#          ' and lifts to a nullable target
Schedule(#2024-03-01#, #9:00 AM#)            ' an argument converts as an assignment does

Dim stamp = #2024-01-15 10:30 AM#           ' still Date, as today
Dim plain = #2024-01-15#                     ' still Date, as today
```

- Only a date literal converts. A `Date` variable, a `Const` of type `Date`, or any other `Date` expression still needs `DateOnly.FromDateTime` or `TimeOnly.FromDateTime`.
- The form of the literal decides the target it may take, by its text rather than its value: a literal with a date and no time of day converts to `DateOnly`, and a literal with a time of day and no date converts to `TimeOnly`. `#1/1/0001#` is a date, and `#12:00 AM#` is a time.
- A literal with both a date and a time of day, such as `#2024-01-15 10:30 AM#`, does not convert to either type, and reports BC30311 as it does today.
- The conversion is widening, because the literal fits the target exactly, so it compiles under `Option Strict On`.
- It applies wherever a value converts to a target type: a declaration with an `As` clause, an assignment, an argument, a `Return`, an element of an array or collection literal with a target type, and a `Case` value when the `Select Case` value is a `DateOnly` or a `TimeOnly`.
- `DateOnly` and `TimeOnly` are not constant types in .NET, so the converted value is not a constant: `Const d As DateOnly = #2024-01-15#` and `Optional d As DateOnly = #2024-01-15#` report the errors that any non-constant value reports there.
- Visual Basic 17.13 still reports BC30311 for every form.

## What does not change

- A date literal without a `DateOnly` or `TimeOnly` target is a `Date`, so `Dim d = #2024-01-15#` and overload resolution between `Date` and other types keep their meaning.
- Where both a `Date` overload and a `DateOnly` overload apply, the `Date` overload is chosen, because the literal is a `Date` and that conversion is the identity.
- `DateOnly` and `TimeOnly` gain no literal of their own, and `#...#` gains no new form.
