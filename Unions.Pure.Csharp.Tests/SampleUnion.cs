using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Tests;

[Union]
public partial record SampleUnion
{
    [JsonInclude]
    [UnionMember]
    internal string? String { get; init; }

    [JsonInclude]
    [UnionMember]
    internal int? Int32 { get; init; }
}

