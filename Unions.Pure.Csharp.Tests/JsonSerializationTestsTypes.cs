using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Tests;

[UnionMember(typeof(string), "String1")]
[UnionMember(typeof(string), "String2")]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.TryOut)]
[UnionSerializationContext(typeof(ApiJsonSerializationContext), caseInsensitivePropertyNameMatching: true)]
public partial record class JsonTestUnion;

[UnionMember(typeof(string), "String1")]
[UnionMember(typeof(string), "String2")]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.TryOut)]
[UnionSerializationContext(typeof(ApiJsonSerializationContext), caseInsensitivePropertyNameMatching: false)]
public partial record class JsonTestUnionCaseSensitive;

[UnionMember(typeof(int), "Payload")]
[UnionMember(typeof(string), "Message")]
[UnionGenerator(GenerateTarget.TryOut)]
[UnionSerializationContext(typeof(ApiJsonSerializationContext), caseInsensitivePropertyNameMatching: false)]
public partial record class JsonPayloadUnionCaseSensitive;

[JsonSourceGenerationOptions(
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
public partial class ApiJsonSerializationContext : JsonSerializerContext;

