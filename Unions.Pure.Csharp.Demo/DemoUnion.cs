using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.Demo;

[Union]
public partial record struct DemoUnion
{
    [JsonInclude]
    [UnionMember]
    internal string? String { get; init; }

    [JsonInclude]
    [UnionMember]
    internal int? Int32 { get; init; }
}

