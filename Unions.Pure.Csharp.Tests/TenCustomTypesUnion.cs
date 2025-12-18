using System.Text.Json.Serialization;
using Unions.Pure.Csharp;

namespace Unions.Pure.Csharp.Tests;

public sealed record C0;
public sealed record C1;
public sealed record C2;
public sealed record C3;
public sealed record C4;
public sealed record C5;
public sealed record C6;
public sealed record C7;
public sealed record C8;
public sealed record C9;

[Union]
public partial record TenCustomTypesUnion
{
    [JsonInclude]
    [UnionMember]
    internal C0? C0 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C1? C1 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C2? C2 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C3? C3 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C4? C4 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C5? C5 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C6? C6 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C7? C7 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C8? C8 { get; init; }

    [JsonInclude]
    [UnionMember]
    internal C9? C9 { get; init; }
}
