using System.Collections.Generic;
using System.Text.Json.Serialization;
using Unions.Pure.Csharp;

namespace Unions.Pure.Csharp.Demo;

public sealed record InnerItem(int Id, string Name);

public sealed record ComplexPayload(
    string Title,
    int[] Numbers,
    InnerItem[] Items,
    Dictionary<string, InnerItem> Map,
    int? Optional);

[UnionMember(typeof(ComplexPayload), "Payload")]
[UnionMember(typeof(string), "Message")]
[UnionGenerator(GenerateTarget.TryOut)]
[UnionSerializationContext(typeof(DemoJsonContext), caseInsensitivePropertyNameMatching: true)]
public partial record struct JsonDemoUnion;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ComplexPayload))]
[JsonSerializable(typeof(InnerItem))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(InnerItem[]))]
[JsonSerializable(typeof(Dictionary<string, InnerItem>))]
[JsonSerializable(typeof(string))]
public partial class DemoJsonContext : JsonSerializerContext;


