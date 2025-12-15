using Unions.Pure.Csharp;

namespace Unions.Pure.Csharp.Demo;

[UnionMember(typeof(string))]
[UnionMember(typeof(int))]
public partial record struct DemoUnion;


