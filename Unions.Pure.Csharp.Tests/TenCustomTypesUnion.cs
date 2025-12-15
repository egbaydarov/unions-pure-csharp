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

[UnionMember(typeof(C0))]
[UnionMember(typeof(C1))]
[UnionMember(typeof(C2))]
[UnionMember(typeof(C3))]
[UnionMember(typeof(C4))]
[UnionMember(typeof(C5))]
[UnionMember(typeof(C6))]
[UnionMember(typeof(C7))]
[UnionMember(typeof(C8))]
[UnionMember(typeof(C9))]
public partial record struct TenCustomTypesUnion;


