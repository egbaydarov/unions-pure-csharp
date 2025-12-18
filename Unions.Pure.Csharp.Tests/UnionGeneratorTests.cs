using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Unions.Pure.Csharp.Tests;

public class UnionGeneratorTests
{
    private static readonly JsonSerializerOptions options = new()
    {
        WriteIndented = false
    };

    private static string NormalizeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, options);
    }

    private sealed class Context<T>(T value)
    {
        public T Value { get; set; } = value;
    }

    private readonly struct SampleUnionMatchVisitor : SampleUnion.IMatchVisitor<string>
    {
        public string OnString(string? value) => "S:" + value;
        public string OnInt32(int? value) => "I:" + value?.ToString(CultureInfo.InvariantCulture) ?? "null";
    }

    private readonly struct SampleUnionSwitchVisitor(Context<int> context) : SampleUnion.ISwitchVisitor
    {
        public void OnString(string? value) => context.Value = value?.Length ?? 0;
        public void OnInt32(int? value) => context.Value = value ?? 0;
    }

    private readonly struct DuplicateTypeUnionMatchVisitor : DuplicateTypeUnion.IMatchVisitor<string>
    {
        public string OnString1(string? value) => "S1:" + value;
        public string OnString2(string? value) => "S2:" + value;
        public string OnInt32(int? value) => "I:" + value?.ToString(CultureInfo.InvariantCulture) ?? "null";
    }

    [Fact]
    public void FromString_sets_tag_and_matches()
    {
        var union = SampleUnion.FromString("hello");

        union.Tag
            .Should()
            .Be(SampleUnion.UnionTag.String);

        var result = union.Match(
            onString: s => s?.ToUpperInvariant() ?? "",
            onInt32: n => n?.ToString(CultureInfo.InvariantCulture) ?? "");

        result
            .Should()
            .Be("HELLO");
    }

    [Fact]
    public void FromInt_switch_invokes_expected_branch()
    {
        var union = SampleUnion.FromInt32(7);
        var handled = false;

        union.Switch(
            onString: _ => throw new InvalidOperationException("String branch should not be taken."),
            onInt32: n =>
            {
                handled = true;
                n
                    .Should()
                    .Be(7);
            });

        handled
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Duplicate_member_types_can_be_disambiguated_with_custom_names()
    {
        var u1 = DuplicateTypeUnion.FromString1("a");
        var u2 = DuplicateTypeUnion.FromString2("bb");
        var u3 = DuplicateTypeUnion.FromInt32(5);

        u1.Tag
            .Should()
            .Be(DuplicateTypeUnion.UnionTag.String1);
        u2.Tag
            .Should()
            .Be(DuplicateTypeUnion.UnionTag.String2);
        u3.Tag
            .Should()
            .Be(DuplicateTypeUnion.UnionTag.Int32);

        var r1 = u1.Match(
            onString1: s => "S1:" + s,
            onString2: s => "S2:" + s,
            onInt32: n => "I:" + (n?.ToString(CultureInfo.InvariantCulture) ?? "null"));
        r1
            .Should()
            .Be("S1:a");

        var r2 = u2.Match(
            onString1: s => "S1:" + s,
            onString2: s => "S2:" + s,
            onInt32: n => "I:" + (n?.ToString(CultureInfo.InvariantCulture) ?? "null"));
        r2
            .Should()
            .Be("S2:bb");

        var handled = 0;
        u3.Switch(
            onString1: _ => handled = 1,
            onString2: _ => handled = 2,
            onInt32: _ => handled = 3);
        handled
            .Should()
            .Be(3);
    }

    [Fact]
    public void Ten_custom_types_union_generates_and_matches()
    {
        var u = TenCustomTypesUnion.FromC7(new C7());
        u.Tag
            .Should()
            .Be(TenCustomTypesUnion.UnionTag.C7);

        var r = u.Match(
            onC0: _ => 0,
            onC1: _ => 1,
            onC2: _ => 2,
            onC3: _ => 3,
            onC4: _ => 4,
            onC5: _ => 5,
            onC6: _ => 6,
            onC7: _ => 7,
            onC8: _ => 8,
            onC9: _ => 9);
        r
            .Should()
            .Be(7);
    }

    [Fact]
    public void Visitor_match_and_switch_work()
    {
        var s = SampleUnion.FromString("abcd");
        var i = SampleUnion.FromInt32(123);

        var ms = s.Match<SampleUnionMatchVisitor, string>(default);
        var mi = i.Match<SampleUnionMatchVisitor, string>(default);
        ms
            .Should()
            .Be("S:abcd");
        mi
            .Should()
            .Be("I:123");

        var context = new Context<int>(-1);
        s.Switch(new SampleUnionSwitchVisitor(context));
        context.Value
            .Should()
            .Be(4);
        i.Switch(new SampleUnionSwitchVisitor(context));
        context.Value
            .Should()
            .Be(123);
    }

    [Fact]
    public void TryGet_and_Is_helpers_work_including_duplicate_types()
    {
        var s = SampleUnion.FromString("hi");
        s.IsString()
            .Should()
            .BeTrue();
        s.IsInt32()
            .Should()
            .BeFalse();
        s.TryGetString(out var str)
            .Should()
            .BeTrue();
        str
            .Should()
            .Be("hi");
        s.TryGetInt32(out _)
            .Should()
            .BeFalse();

        var du = DuplicateTypeUnion.FromString2("x");
        du.IsString2()
            .Should()
            .BeTrue();
        du.IsString1()
            .Should()
            .BeFalse();
        du.TryGetString2(out var s2)
            .Should()
            .BeTrue();
        s2
            .Should()
            .Be("x");
        du.TryGetString1(out _)
            .Should()
            .BeFalse();

        var r = du.Match<DuplicateTypeUnionMatchVisitor, string>(default);
        r
            .Should()
            .Be("S2:x");
    }

    [Fact]
    public void UnionGenerator_flags_control_emitted_apis()
    {
        static string ReadGenerated(string typeName)
        {
            var baseDir = AppContext.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            var objGenerated = Path.Combine(projectDir, "obj", "Generated");
            var file = Directory.EnumerateFiles(objGenerated, $"*{typeName}.Union.g.cs", SearchOption.AllDirectories).First();
            return File.ReadAllText(file);
        }

        var tryOnly = ReadGenerated(nameof(TryOnlyUnion));
        tryOnly
            .Should()
            .Contain("TryGetString");
        tryOnly
            .Should()
            .NotContain("public T Match<T>(");
        tryOnly
            .Should()
            .NotContain("public interface IMatchVisitor");

        var visitorOnly = ReadGenerated(nameof(VisitorOnlyUnion));
        visitorOnly
            .Should()
            .Contain("public interface IMatchVisitor");
        visitorOnly
            .Should()
            .Contain("public TResult Match<TVisitor, TResult>");
        visitorOnly
            .Should()
            .NotContain("public T Match<T>(");
        visitorOnly
            .Should()
            .NotContain("TryGetString");

        var indirectOnly = ReadGenerated(nameof(IndirectOnlyUnion));
        indirectOnly
            .Should()
            .Contain("public T Match<T>(");
        indirectOnly
            .Should()
            .Contain("public void Switch(");
        indirectOnly
            .Should()
            .NotContain("public interface IMatchVisitor");
        indirectOnly
            .Should()
            .NotContain("TryGetString");
    }

    [Fact]
    public void Union_serialization_STJ_roundtrip()
    {
        var ctx = ApiJsonSerializationContext.Default;

        var u = JsonTestUnion.FromString2("hello");
        var json = JsonSerializer.Serialize(u, ctx.JsonTestUnion);
        var normalizedJson = NormalizeJson(json);

        var expectedJson = NormalizeJson("""
        {
          "string2": "hello"
        }
        """);

        normalizedJson
            .Should()
            .Be(expectedJson);

        var back = JsonSerializer.Deserialize(json, ctx.JsonTestUnion);
        back
            .Should()
            .NotBeNull();
        back.TryGetString2(out var s)
            .Should()
            .BeTrue();
        s
            .Should()
            .Be("hello");

        var u2 = JsonTestUnion.FromInt32(7);
        var json2 = JsonSerializer.Serialize(u2, ctx.JsonTestUnion);
        var normalizedJson2 = NormalizeJson(json2);

        var expectedJson2 = NormalizeJson("""
        {
          "int32": 7
        }
        """);

        normalizedJson2
            .Should()
            .Be(expectedJson2);

        var back2 = JsonSerializer.Deserialize(json2, ctx.JsonTestUnion);
        back2
            .Should()
            .NotBeNull();
        back2.TryGetInt32(out var i)
            .Should()
            .BeTrue();
        i
            .Should()
            .Be(7);
    }

    [Fact]
    public void Union_serialization_STJ_case_sensitive()
    {
        var ctx = ApiJsonSerializationContext.Default;
        var u = JsonTestUnionCaseSensitive.FromString2("hello");
        var json = JsonSerializer.Serialize(u, ctx.JsonTestUnionCaseSensitive);
        var normalizedJson = NormalizeJson(json);

        var expectedJson = NormalizeJson("""
        {
          "string2": "hello"
        }
        """);

        normalizedJson
            .Should()
            .Be(expectedJson);

        var back = JsonSerializer.Deserialize(json, ctx.JsonTestUnionCaseSensitive);
        back
            .Should()
            .NotBeNull();
        back.Tag
            .Should()
            .Be(JsonTestUnionCaseSensitive.UnionTag.String2);
    }

    [Fact]
    public void Union_serialization_STJ_payload()
    {
        var ctx = ApiJsonSerializationContext.Default;
        var u = JsonPayloadUnionCaseSensitive.FromPayload(7);
        var json = JsonSerializer.Serialize(u, ctx.JsonPayloadUnionCaseSensitive);
        var normalizedJson = NormalizeJson(json);

        var expectedJson = NormalizeJson("""
        {
          "payload": 7
        }
        """);

        normalizedJson
            .Should()
            .Be(expectedJson);

        var back = JsonSerializer.Deserialize(json, ctx.JsonPayloadUnionCaseSensitive);
        back
            .Should()
            .NotBeNull();
        back.TryGetPayload(out var p)
            .Should()
            .BeTrue();
        p
            .Should()
            .Be(7);
    }

    [Fact]
    public void Union_serialization_STJ_complex_roundtrip()
    {
        var ctx = ApiJsonComplexSerializationContext.Default;
        var payload = new ComplexPayload(
            Title: "t",
            Numbers: [ 1, 2, 3 ],
            Items: new[] { new InnerItem(1, "a"), new InnerItem(2, "b") },
            Map: new Dictionary<string, InnerItem> { ["k"] = new InnerItem(9, "z") },
            Optional: null);

        var u = JsonComplexUnion.FromPayload(payload);
        var json = JsonSerializer.Serialize(u, ctx.JsonComplexUnion);
        var normalizedJson = NormalizeJson(json);

        var expectedJson = NormalizeJson("""
        {
          "payload": {
            "title": "t",
            "numbers": [1, 2, 3],
            "items": [
              {
                "id": 1,
                "name": "a"
              },
              {
                "id": 2,
                "name": "b"
              }
            ],
            "map": {
              "k": {
                "id": 9,
                "name": "z"
              }
            }
          }
        }
        """);

        normalizedJson
            .Should()
            .Be(expectedJson);

        var back = JsonSerializer.Deserialize(json, ctx.JsonComplexUnion);
        back
            .Should()
            .NotBeNull();
        back.Tag
            .Should()
            .Be(JsonComplexUnion.UnionTag.Payload);
        back.TryGetPayload(out var backPayload)
            .Should()
            .BeTrue();
        backPayload
            .Should()
            .NotBeNull();

        backPayload.Title
            .Should()
            .Be(payload.Title);
        backPayload.Numbers
            .Should()
            .BeEquivalentTo(payload.Numbers);
        backPayload.Items
            .Should()
            .BeEquivalentTo(payload.Items);
        backPayload.Map["k"]
            .Should()
            .BeEquivalentTo(payload.Map["k"]);
        backPayload.Optional
            .Should()
            .BeNull();
    }

    [Fact]
    public void Union_serialization_STJ_nested_union_roundtrip()
    {
        var ctx = NestedUnionJsonSerializationContext.Default;

        var deepUnion1 = DeepNestedUnion.FromDeepString("deep1");
        var deepUnion2 = DeepNestedUnion.FromDeepInt(42);
        var record = new RecordWithUnions("TestRecord", deepUnion1, deepUnion2);
        var innerUnion = InnerNestedUnion.FromRecord(record);
        var outerUnion = OuterNestedUnion.FromInner(innerUnion);

        var json = JsonSerializer.Serialize(outerUnion, ctx.OuterNestedUnion);
        var normalizedJson = NormalizeJson(json);

        var expectedJson = NormalizeJson("""
        {
          "inner": {
            "record": {
              "name": "TestRecord",
              "union1": {
                "deepString": "deep1"
              },
              "union2": {
                "deepInt": 42
              }
            }
          }
        }
        """);

        normalizedJson
            .Should()
            .Be(expectedJson);

        var back = JsonSerializer.Deserialize(json, ctx.OuterNestedUnion);
        back
            .Should()
            .NotBeNull();
        back.Tag
            .Should()
            .Be(OuterNestedUnion.UnionTag.Inner);
        back.TryGetInner(out var backInner)
            .Should()
            .BeTrue();
        backInner
            .Should()
            .NotBeNull();

        backInner.Tag
            .Should()
            .Be(InnerNestedUnion.UnionTag.Record);
        backInner.TryGetRecord(out var backRecord)
            .Should()
            .BeTrue();
        backRecord
            .Should()
            .NotBeNull();

        backRecord.Name
            .Should()
            .Be("TestRecord");
        backRecord.Union1
            .Should()
            .NotBeNull();
        backRecord.Union1.Tag
            .Should()
            .Be(DeepNestedUnion.UnionTag.DeepString);
        backRecord.Union1.TryGetDeepString(out var backDeepString)
            .Should()
            .BeTrue();
        backDeepString
            .Should()
            .Be("deep1");

        backRecord.Union2
            .Should()
            .NotBeNull();
        backRecord.Union2.Tag
            .Should()
            .Be(DeepNestedUnion.UnionTag.DeepInt);
        backRecord.Union2.TryGetDeepInt(out var backDeepInt)
            .Should()
            .BeTrue();
        backDeepInt
            .Should()
            .Be(42);
    }
}

