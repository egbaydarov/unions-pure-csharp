using Unions.Pure.Csharp;

namespace Unions.Pure.Csharp.Tests;

[UnionMember(typeof(string))]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.TryOut)]
public partial record struct TryOnlyUnion;

[UnionMember(typeof(string))]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.Visitor)]
public partial record struct VisitorOnlyUnion;

[UnionMember(typeof(string))]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.IndirectLambdas)]
public partial record struct IndirectOnlyUnion;


