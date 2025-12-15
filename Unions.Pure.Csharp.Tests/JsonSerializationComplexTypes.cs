using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Tests;

public sealed record InnerItem(int Id, string Name);

public sealed record ComplexPayload(
    string Title,
    int[] Numbers,
    InnerItem[] Items,
    Dictionary<string, InnerItem> Map,
    int? Optional);

[UnionMember(typeof(ComplexPayload), "Payload")]
[UnionMember(typeof(int[]), "Numbers")]
[UnionMember(typeof(string), "Message")]
[UnionGenerator(GenerateTarget.TryOut)]
[UnionSerializationContext(typeof(ApiJsonComplexSerializationContext), caseInsensitivePropertyNameMatching: true)]
public partial record class JsonComplexUnion;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ComplexPayload))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(string))]
public partial class ApiJsonComplexSerializationContext : JsonSerializerContext;


