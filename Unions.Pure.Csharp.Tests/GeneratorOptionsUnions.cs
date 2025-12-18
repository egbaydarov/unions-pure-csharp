using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Tests;

[Union(GenerateTarget.TryOut)]
public partial record TryOnlyUnion
{
    [JsonInclude]
    [UnionMember]
    internal string? String { get; init; }

    [JsonInclude]
    [UnionMember]
    internal int? Int32 { get; init; }
}

[Union(GenerateTarget.Visitor)]
public partial record VisitorOnlyUnion
{
    [JsonInclude]
    [UnionMember]
    internal string? String { get; init; }

    [JsonInclude]
    [UnionMember]
    internal int? Int32 { get; init; }
}

[Union(GenerateTarget.IndirectLambdas)]
public partial record IndirectOnlyUnion
{
    [JsonInclude]
    [UnionMember]
    internal string? String { get; init; }

    [JsonInclude]
    [UnionMember]
    internal int? Int32 { get; init; }
}

