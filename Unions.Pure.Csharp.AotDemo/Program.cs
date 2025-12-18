using System.Text;
using System.Text.Json.Serialization;

namespace Unions.Pure.Csharp.AotDemo;

[Union(GenerateTarget.TryOut)]
public partial record class JsonTestUnion
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

[JsonSourceGenerationOptions(
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(JsonTestUnion))]
public partial class ApiJsonSerializationContext : JsonSerializerContext;

public static class Program
{
    public static int Main()
    {
        var ctx = ApiJsonSerializationContext.Default;

        var u1 = JsonTestUnion.FromString2("hello");
        var bytes1 = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(u1, ctx.JsonTestUnion);
        Console.WriteLine(Encoding.UTF8.GetString(bytes1));

        var back1 = System.Text.Json.JsonSerializer.Deserialize(bytes1, ctx.JsonTestUnion);
        if (back1 == null || !back1.TryGetString2(out var s) || s != "hello") return 1;

        var u2 = JsonTestUnion.FromInt32(7);
        var bytes2 = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(u2, ctx.JsonTestUnion);
        Console.WriteLine(Encoding.UTF8.GetString(bytes2));

        var back2 = System.Text.Json.JsonSerializer.Deserialize(bytes2, ctx.JsonTestUnion);
        if (back2 == null || !back2.TryGetInt32(out var i) || i != 7) return 2;

        // Case-insensitive tag matching
        var back3 = System.Text.Json.JsonSerializer.Deserialize(Encoding.UTF8.GetBytes("{\"string2\":\"hello\"}"), ctx.JsonTestUnion);
        if (back3 == null || !back3.TryGetString2(out var s2) || s2 != "hello") return 3;

        return 0;
    }
}


