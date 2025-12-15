using System;
using System.Text;
using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.AotDemo;

[UnionMember(typeof(string), "String1")]
[UnionMember(typeof(string), "String2")]
[UnionMember(typeof(int))]
[UnionGenerator(GenerateTarget.TryOut)]
[UnionSerializationContext(typeof(ApiJsonSerializationContext), caseInsensitivePropertyNameMatching: true)]
public partial record class JsonTestUnion;

[JsonSourceGenerationOptions(
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
public partial class ApiJsonSerializationContext : JsonSerializerContext;

public static class Program
{
    public static int Main()
    {
        var u1 = JsonTestUnion.FromString2("hello");
        var bytes1 = u1.ToUtf8Bytes();
        Console.WriteLine(Encoding.UTF8.GetString(bytes1));

        var back1 = JsonTestUnion.FromUtf8Bytes(bytes1);
        if (!back1.TryGetString2(out var s) || s != "hello") return 1;

        var u2 = JsonTestUnion.FromInt32(7);
        var bytes2 = u2.ToUtf8Bytes();
        Console.WriteLine(Encoding.UTF8.GetString(bytes2));

        var back2 = JsonTestUnion.FromUtf8Bytes(bytes2);
        if (!back2.TryGetInt32(out var i) || i != 7) return 2;

        // Case-insensitive tag matching
        var back3 = JsonTestUnion.FromUtf8Bytes(Encoding.UTF8.GetBytes("{\"STRING2\":\"hello\"}"));
        if (!back3.TryGetString2(out var s2) || s2 != "hello") return 3;

        return 0;
    }
}


