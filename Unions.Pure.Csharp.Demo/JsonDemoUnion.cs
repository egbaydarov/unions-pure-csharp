using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Demo;

public sealed record InnerItem(int Id, string Name);

public sealed record ComplexPayload(
    string Title,
    int[] Numbers,
    InnerItem[] Items,
    Dictionary<string, InnerItem> Map,
    int? Optional);

[Union(GenerateTarget.TryOut)]
public partial record struct JsonDemoUnion
{
    [JsonInclude]
    [UnionMember("Payload")]
    internal ComplexPayload? Payload { get; init; }

    [JsonInclude]
    [UnionMember("Message")]
    internal string? Message { get; init; }
}

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ComplexPayload))]
[JsonSerializable(typeof(InnerItem))]
[JsonSerializable(typeof(JsonDemoUnion))]
public partial class DemoJsonContext : JsonSerializerContext;


