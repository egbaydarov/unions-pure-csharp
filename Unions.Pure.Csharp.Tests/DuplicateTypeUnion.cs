using Unions.Pure.Csharp;

namespace Unions.Pure.Csharp.Tests;

// Demonstrates duplicate member types with unique names.
[UnionMember(typeof(string), "String1")]
[UnionMember(typeof(string), "String2")]
[UnionMember(typeof(int))]
public partial record struct DuplicateTypeUnion;


