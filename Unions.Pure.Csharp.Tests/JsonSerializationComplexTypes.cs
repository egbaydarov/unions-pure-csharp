using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Tests;

public sealed record InnerItem(int Id, string Name);

public sealed record ComplexPayload(
    string Title,
    int[] Numbers,
    InnerItem[] Items,
    Dictionary<string, InnerItem> Map,
    int? Optional);

[Union(GenerateTarget.TryOut)]
public partial record class JsonComplexUnion
{
    [JsonInclude]
    [UnionMember("Payload")]
    internal ComplexPayload? Payload { get; init; }

    [JsonInclude]
    [UnionMember("Numbers")]
    internal int[]? Numbers { get; init; }

    [JsonInclude]
    [UnionMember("Message")]
    internal string? Message { get; init; }
}

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(JsonComplexUnion))]
public partial class ApiJsonComplexSerializationContext : JsonSerializerContext;

