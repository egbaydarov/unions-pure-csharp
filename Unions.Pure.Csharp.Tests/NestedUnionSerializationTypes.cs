using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Tests;

[Union(GenerateTarget.TryOut)]
public partial record class DeepNestedUnion
{
    [JsonInclude]
    [UnionMember("DeepString")]
    internal string? DeepString { get; init; }

    [JsonInclude]
    [UnionMember("DeepInt")]
    internal int? DeepInt { get; init; }
}

public sealed record RecordWithUnions(
    string Name,
    DeepNestedUnion? Union1,
    DeepNestedUnion? Union2);

[Union(GenerateTarget.TryOut)]
public partial record class InnerNestedUnion
{
    [JsonInclude]
    [UnionMember("Record")]
    internal RecordWithUnions? Record { get; init; }

    [JsonInclude]
    [UnionMember("SimpleValue")]
    internal string? SimpleValue { get; init; }
}

[Union(GenerateTarget.TryOut)]
public partial record class OuterNestedUnion
{
    [JsonInclude]
    [UnionMember("Inner")]
    internal InnerNestedUnion? Inner { get; init; }

    [JsonInclude]
    [UnionMember("Message")]
    internal string? Message { get; init; }
}

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(OuterNestedUnion))]
[JsonSerializable(typeof(InnerNestedUnion))]
[JsonSerializable(typeof(DeepNestedUnion))]
[JsonSerializable(typeof(RecordWithUnions))]
public partial class NestedUnionJsonSerializationContext : JsonSerializerContext;

