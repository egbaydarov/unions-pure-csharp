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

2. Annotate a partial type with the union members as properties:

```csharp
using System.Text.Json.Serialization;
using Unions.Pure.Csharp;

namespace Sample;

[Union(GenerateTarget.Visitor | GenerateTarget.TryOut)]
public partial record struct PaymentStatus
{
    [JsonInclude]
    [UnionMember]
    internal string? Message { get; init; }

    [JsonInclude]
    [UnionMember]
    internal int? Code { get; init; }
}
```

3. Use the generated factories and helpers:

```csharp
var paid = PaymentStatus.FromCode(200);

var message = paid.Match(
    onMessage: s => $"Message: {s}",
    onCode: code => $"Code: {code}");
```

`[Union]` and `[UnionMember]` are injected by the generator; no manual reference is required beyond the package itself.

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
using System.Text.Json.Serialization;
using Unions.Pure.Csharp;

namespace Sample;

[Union(GenerateTarget.All)]
public partial record struct DuplicateTypeUnion
{
    [JsonInclude]
    [UnionMember("String1")]
    internal string? String1 { get; init; }

    [JsonInclude]
    [UnionMember("String2")]
    internal string? String2 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal int? Int32 { get; init; }
}

// Usage:
var u = DuplicateTypeUnion.FromString2("hello");
var lenOrInt = u.Match(
    onString1: s => s.Length,
    onString2: s => s.Length,
    onInt32: i => i);
```

## `GenerateTarget` flags

- **`GenerateTarget.None`**: generate nothing extra (still generates tag + factories)
- **`GenerateTarget.IndirectLambdas`**: delegate-based `Match` / `Switch`
- **`GenerateTarget.Visitor`**: visitor-based `Match<TVisitor, TResult>` / `Switch<TVisitor>`
- **`GenerateTarget.TryOut`**: inline `IsX()` / `TryGetX(out ...)`
- **`GenerateTarget.JsonSerialization`**: enable JSON serialization support (used with `System.Text.Json` source generation)
- **`GenerateTarget.All`**: everything above (default when `[Union]` is omitted)

## JSON serialization (`System.Text.Json`)

Unions work seamlessly with `System.Text.Json` source generation. Simply:

1. Define your union with properties annotated with `[JsonInclude]` and `[UnionMember]`
2. Add the union type to your `JsonSerializerContext` with `[JsonSerializable(typeof(YourUnion))]`
3. Use `JsonSerializer.Serialize` and `JsonSerializer.Deserialize` with your context

The wire format is a **single-property object** like `{"string2":"hello"}` or `{"int32":7}`.

### JSON property names

The **JSON property name** is derived from the property name or the `[UnionMember]` name:

- If you used `[UnionMember("CustomName")]`, the JSON property name is `"CustomName"` (then the context naming policy is applied when writing).
- If you omit the custom name, the property name is used (e.g. `String2` → `string2` with camelCase policy).

Example:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Unions.Pure.Csharp;

// Union:
[Union(GenerateTarget.TryOut)]
public partial record struct SomeUnion
{
    [JsonInclude]
    [UnionMember("String1")]
    internal string? String1 { get; init; }

    [JsonInclude]
    [UnionMember("String2")]
    internal string? String2 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal int? Int32 { get; init; }
}

// Context (only need to register the union type, not member types):
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SomeUnion))]
public partial class ApiJsonContext : JsonSerializerContext;

// Use (AOT friendly, no reflection required):
var ctx = ApiJsonContext.Default;
var u = SomeUnion.FromString2("hello");
var json = JsonSerializer.Serialize(u, ctx.SomeUnion);
var back = JsonSerializer.Deserialize(json, ctx.SomeUnion);
```

The `Tag` property is automatically computed from which property is non-null, so it doesn't need to be serialized.

## Swagger / OpenAPI (e.g. AOT apps)

The generator emits Swagger/OpenAPI schema registration whenever the project has union types, but that code is **conditionally compiled** so you don't have to reference Swashbuckle in every project that has unions.

- **Libraries / contracts (unions only):** Do nothing. The Swagger code is wrapped in `#if UNIONS_SWAGGER_SCHEMAS` and is not compiled, so no Swashbuckle reference is required.
- **App project (the one that uses Swagger):** Define `UNIONS_SWAGGER_SCHEMAS`, reference Swashbuckle, and call the generated registration.

1. In the **app** project only, add the define and Swashbuckle:

   ```xml
   <PropertyGroup>
     <DefineConstants>$(DefineConstants);UNIONS_SWAGGER_SCHEMAS</DefineConstants>
   </PropertyGroup>
   <ItemGroup>
     <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
   </ItemGroup>
   ```

2. In your Swagger setup, call the generated static method. The generated class name is **unique per assembly** (`PureUnionsSwaggerGen_<AssemblyName>`). Call it for the app assembly and for any referenced assembly that has unions and was built with `UNIONS_SWAGGER_SCHEMAS` (if you need those in Swagger too):

```csharp
using Unions.Pure.Csharp;

builder.Services.AddSwaggerGen(configure =>
{
    PureUnionsSwaggerGen_MyApi.AddUnionSchemaMappings(configure);
    // If your contracts library was built with UNIONS_SWAGGER_SCHEMAS and refs Swashbuckle:
    // PureUnionsSwaggerGen_MyContracts.AddUnionSchemaMappings(configure);

    configure.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
});
```

Each union is mapped to an `object` schema with optional properties matching the union members (and their types), so the docs show the real shape instead of an empty stub.
