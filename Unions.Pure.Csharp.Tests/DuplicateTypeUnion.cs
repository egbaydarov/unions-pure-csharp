using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Tests;

[Union]
public partial record DuplicateTypeUnion
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

