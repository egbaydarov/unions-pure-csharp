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
public partial class NestedUnionJsonSerializationContext : JsonSerializerContext;

/// <summary>
/// Verifies that union members whose types are records nested inside the union's
/// own declaring type are correctly registered into Swashbuckle's schema repository
/// and referenced from the parent union's schema by the same id.
/// </summary>
[Union]
public sealed partial class NestedRecordsTestUnion
{
    [JsonInclude]
    [UnionMember]
    internal AlphaNestedCase? Alpha { get; set; }

    [JsonInclude]
    [UnionMember]
    internal BetaNestedCase? Beta { get; set; }

    public sealed record AlphaNestedCase(string Label);
    public sealed record BetaNestedCase(int Count, bool Enabled);
}

/// <summary>
/// Two unions with nested member types that share the same short C# name; Swashbuckle's
/// default schema id (<c>type.Name</c>) collides unless nested types use a disambiguated id.
/// </summary>
[Union]
public sealed partial class NestCollisionUnionA
{
    [JsonInclude]
    [UnionMember]
    internal SharedNestedName? Branch { get; set; }

    [JsonInclude]
    [UnionMember]
    internal AltBranch? Other { get; set; }

    public sealed record SharedNestedName(int A);
    public sealed record AltBranch();
}

[Union]
public sealed partial class NestCollisionUnionB
{
    [JsonInclude]
    [UnionMember]
    internal SharedNestedName? Branch { get; set; }

    [JsonInclude]
    [UnionMember]
    internal AltBranch? Other { get; set; }

    public sealed record SharedNestedName(string B);
    public sealed record AltBranch();
}

