# C# Pure Unions

[![NuGet Version](https://img.shields.io/nuget/v/Unions.Pure.Csharp)](https://www.nuget.org/packages/Unions.Pure.Csharp)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Unions.Pure.Csharp)](https://www.nuget.org/packages/Unions.Pure.Csharp)
[![Build](https://github.com/egbaydarov/unions-pure-csharp/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/egbaydarov/unions-pure-csharp/actions/workflows/ci.yml)

Roslyn source generator for minimal-overhead discriminated unions. Add `[UnionMember]` attributes to a partial type and the generator emits a compact tag enum, factory helpers, and `Match`/`Switch` helpers without extra allocations.

## Getting started

1. Install the package in your project:

```bash
dotnet add package Unions.Pure.Csharp
```

2. Annotate a partial type with the union members you need:

```csharp
using Unions.Pure.Csharp;

namespace Sample;

[UnionMember(typeof(string))]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.Visitor | GenerateTarget.TryOut)]
public partial record struct PaymentStatus;
```

3. Use the generated factories and helpers:

```csharp
var paid = PaymentStatus.FromInt32(200);

var message = paid.Match(
    onString: s => $"Message: {s}",
    onInt32: code => $"Code: {code}");
```

`[UnionMember]` is injected by the generator; no manual reference is required beyond the package itself.

## API choices (performance vs ergonomics)

Generated unions expose **three** ways to consume values:

- **`GenerateTarget.IndirectLambdas`: delegate-based `Match` / `Switch`** (`Func<>` / `Action<>`)
  - **Pros**: nicest call site; familiar functional style.
  - **Cons**: still does delegate `Invoke` (typically an *indirect* call); **capturing lambdas allocate** (closures/delegates).

- **`GenerateTarget.Visitor`: visitor-based `Match<TVisitor, TResult>` / `Switch<TVisitor>`** (generic `struct` visitors)
  - **Pros**: avoids delegate `Invoke`; JIT can inline; no allocations.
  - **Cons**: requires a small `struct` visitor type (can be local/private).

- **`GenerateTarget.TryOut`: inline `IsX()` / `TryGetX(out ...)`**
  - **Pros**: “plain C#” `if/else`; no delegates; no visitors; no indirect calls.
  - **Cons**: more boilerplate at call site vs `Match`.

### Note on OneOf (comparison)
`OneOf` is a solid library, but for hot paths it has the same core drawback as any delegate-based API: **`Match`/`Switch` ultimately invoke delegates** (indirect calls), and **capturing lambdas allocate**. It also needs to handle invalid state (cold path that throws).

## Duplicate member types example

If you use the **same type multiple times**, provide distinct names:

```csharp
using Unions.Pure.Csharp;

namespace Sample;

[UnionMember(typeof(string), "String1")]
[UnionMember(typeof(string), "String2")]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.All)]
public partial record struct DuplicateTypeUnion;

// Usage:
var u = DuplicateTypeUnion.FromString2("hello");
var lenOrInt = u.Match(
    onString1: s => s.Length,
    onString2: s => s.Length,
    onInt32: i => i);
```

## `GenerateTarget` flags

- **`GenerateTarget.None`**: generate nothing extra (still generates tag + fields + factories)
- **`GenerateTarget.IndirectLambdas`**: delegate-based `Match` / `Switch`
- **`GenerateTarget.Visitor`**: visitor-based `Match<TVisitor, TResult>` / `Switch<TVisitor>`
- **`GenerateTarget.TryOut`**: inline `IsX()` / `TryGetX(out ...)`
- **`GenerateTarget.All`**: everything above (default when `[UnionGenerator]` is omitted)

## JSON serialization (`System.Text.Json`)

The generator can emit AOT-friendly JSON helpers when you specify a source-generated `JsonSerializerContext`:

- Add **`[UnionSerializationContext(typeof(YourJsonSerializerContext))]`** on the union
- The generator will emit **`ToUtf8Bytes()`** and **`FromUtf8Bytes(...)`**
- The wire format is a **single-property object** like `{"string2":"hello"}` or `{"int32":7}`

### Disclaimer (why the `JsonSerializerContext` lives outside generated code)

This JSON support is a pragmatic workaround for the fact that **you can’t reliably “chain” source generators**: this generator can emit union code, but it can’t also ensure that `System.Text.Json`’s source generator sees and processes *new types/attributes emitted by another generator* in the same compilation in a dependable way.

Because of that, the **`JsonSerializerContext` (and its `[JsonSerializable(...)]` roots)** must be **written in your project** (non-generated code) so the **STJ source generator** runs over it normally. The union generator then consumes that context at runtime via the `JsonTypeInfo` APIs.

### JSON field name mirrors `[UnionMember]` name

The **JSON property name** is derived from the union member’s tag name:

- If you used `[UnionMember(typeof(T), "CustomName")]`, the JSON property name is `"CustomName"` (then the context naming policy is applied when writing).
- If you omit the custom name, the generator uses its inferred tag identifier (e.g. `string` → `String`, `int` → `Int32`, arrays → `Int32Array`), and that is what gets mirrored to JSON (again, with naming policy applied when writing).

When reading, `FromUtf8Bytes` matches the single property name **case-sensitively by default**, and it accepts both:

- The raw tag name (e.g. `Payload`)
- The tag name after applying the context’s `PropertyNamingPolicy` (e.g. `payload` for camelCase)

If you want case-insensitive behavior, enable it:

```csharp
[UnionSerializationContext(typeof(ApiJsonContext), caseInsensitivePropertyNameMatching: true)]
```

Example:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Unions.Pure.Csharp;

// Union:
[UnionMember(typeof(string), "String1")]
[UnionMember(typeof(string), "String2")]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.TryOut)]
[UnionSerializationContext(typeof(ApiJsonContext))]
public partial record struct SomeUnion;

// Context (add all payload/root types you plan to serialize):
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
public partial class ApiJsonContext : JsonSerializerContext;

// Use (AOT friendly, no reflection required):
var bytes = SomeUnion.FromString2("hello").ToUtf8Bytes();
var back = SomeUnion.FromUtf8Bytes(bytes);
```

`[UnionSerializationContext]` is injected by the generator, just like `[UnionMember]`.
