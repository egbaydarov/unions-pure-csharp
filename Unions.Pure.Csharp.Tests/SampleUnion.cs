using Unions.Pure.Csharp;

namespace Unions.Pure.Csharp.Tests;

[UnionMember(typeof(string))]
[UnionMember(typeof(int))]
public partial record struct SampleUnion;

