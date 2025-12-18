using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Tests;

[Union(GenerateTarget.TryOut)]
public partial record class JsonTestUnion
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

[Union(GenerateTarget.TryOut)]
public partial record class JsonTestUnionCaseSensitive
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

[Union(GenerateTarget.TryOut)]
public partial record class JsonPayloadUnionCaseSensitive
{
    [JsonInclude]
    [UnionMember("Payload")]
    internal int? Payload { get; init; }

    [JsonInclude]
    [UnionMember("Message")]
    internal string? Message { get; init; }
}

[JsonSourceGenerationOptions(
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(JsonTestUnion))]
[JsonSerializable(typeof(JsonTestUnionCaseSensitive))]
[JsonSerializable(typeof(JsonPayloadUnionCaseSensitive))]
public partial class ApiJsonSerializationContext : JsonSerializerContext;

