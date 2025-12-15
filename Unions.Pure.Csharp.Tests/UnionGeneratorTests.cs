using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using Xunit;

namespace Unions.Pure.Csharp.Tests;

public class UnionGeneratorTests
{
    private sealed class Context<T>(T value)
    {
        public T Value { get; set; } = value;
    }

    private readonly struct SampleUnionMatchVisitor : SampleUnion.IMatchVisitor<string>
    {
        public string OnString(string value) => "S:" + value;
        public string OnInt32(int value) => "I:" + value.ToString(CultureInfo.InvariantCulture);
    }

    private readonly struct SampleUnionSwitchVisitor(Context<int> context) : SampleUnion.ISwitchVisitor
    {
        public void OnString(string value) => context.Value = value.Length;
        public void OnInt32(int value) => context.Value = value;
    }

    private readonly struct DuplicateTypeUnionMatchVisitor : DuplicateTypeUnion.IMatchVisitor<string>
    {
        public string OnString1(string value) => "S1:" + value;
        public string OnString2(string value) => "S2:" + value;
        public string OnInt32(int value) => "I:" + value.ToString(CultureInfo.InvariantCulture);
    }

    [Fact]
    public void FromString_sets_tag_and_matches()
    {
        var union = SampleUnion.FromString("hello");

        Assert.Equal(SampleUnion.UnionTag.String, union.Tag);

        var result = union.Match(
            onString: s => s.ToUpperInvariant(),
            onInt32: n => n.ToString(CultureInfo.InvariantCulture));

        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void FromInt_switch_invokes_expected_branch()
    {
        var union = SampleUnion.FromInt32(7);
        var handled = false;

        union.Switch(
            onString: _ => Assert.Fail("String branch should not be taken."),
            onInt32: n =>
            {
                handled = true;
                Assert.Equal(7, n);
            });

        Assert.True(handled);
    }

    [Fact]
    public void Duplicate_member_types_can_be_disambiguated_with_custom_names()
    {
        var u1 = DuplicateTypeUnion.FromString1("a");
        var u2 = DuplicateTypeUnion.FromString2("bb");
        var u3 = DuplicateTypeUnion.FromInt32(5);

        Assert.Equal(DuplicateTypeUnion.UnionTag.String1, u1.Tag);
        Assert.Equal(DuplicateTypeUnion.UnionTag.String2, u2.Tag);
        Assert.Equal(DuplicateTypeUnion.UnionTag.Int32, u3.Tag);

        var r1 = u1.Match(
            onString1: s => "S1:" + s,
            onString2: s => "S2:" + s,
            onInt32: n => "I:" + n.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("S1:a", r1);

        var r2 = u2.Match(
            onString1: s => "S1:" + s,
            onString2: s => "S2:" + s,
            onInt32: n => "I:" + n.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("S2:bb", r2);

        var handled = 0;
        u3.Switch(
            onString1: _ => handled = 1,
            onString2: _ => handled = 2,
            onInt32: _ => handled = 3);
        Assert.Equal(3, handled);
    }

    [Fact]
    public void Ten_custom_types_union_generates_and_matches()
    {
        var u = TenCustomTypesUnion.FromC7(new C7());
        Assert.Equal(TenCustomTypesUnion.UnionTag.C7, u.Tag);

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
        Assert.Equal(7, r);
    }

    [Fact]
    public void Visitor_match_and_switch_work()
    {
        var s = SampleUnion.FromString("abcd");
        var i = SampleUnion.FromInt32(123);

        var ms = s.Match<SampleUnionMatchVisitor, string>(default);
        var mi = i.Match<SampleUnionMatchVisitor, string>(default);
        Assert.Equal("S:abcd", ms);
        Assert.Equal("I:123", mi);

        var context = new Context<int>(-1);
        s.Switch(new SampleUnionSwitchVisitor(context));
        Assert.Equal(4, context.Value);
        i.Switch(new SampleUnionSwitchVisitor(context));
        Assert.Equal(123, context.Value);
    }

    [Fact]
    public void TryGet_and_Is_helpers_work_including_duplicate_types()
    {
        var s = SampleUnion.FromString("hi");
        Assert.True(s.IsString());
        Assert.False(s.IsInt32());
        Assert.True(s.TryGetString(out var str));
        Assert.Equal("hi", str);
        Assert.False(s.TryGetInt32(out _));

        var du = DuplicateTypeUnion.FromString2("x");
        Assert.True(du.IsString2());
        Assert.False(du.IsString1());
        Assert.True(du.TryGetString2(out var s2));
        Assert.Equal("x", s2);
        Assert.False(du.TryGetString1(out _));

        var r = du.Match<DuplicateTypeUnionMatchVisitor, string>(default);
        Assert.Equal("S2:x", r);
    }

    [Fact]
    public void UnionGenerator_flags_control_emitted_apis()
    {
        // We validate by inspecting the generated .g.cs text (compile-time shaping).
        static string ReadGenerated(string typeName)
        {
            // bin/{cfg}/{tfm}/ -> project/obj/Generated/**/Type.Union.g.cs
            var baseDir = AppContext.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            var objGenerated = Path.Combine(projectDir, "obj", "Generated");
            var file = Directory.EnumerateFiles(objGenerated, $"*{typeName}.Union.g.cs", SearchOption.AllDirectories).First();
            return File.ReadAllText(file);
        }

        var tryOnly = ReadGenerated(nameof(TryOnlyUnion));
        Assert.Contains("TryGetString", tryOnly);
        Assert.DoesNotContain("public T Match<T>(", tryOnly);
        Assert.DoesNotContain("public interface IMatchVisitor", tryOnly);

        var visitorOnly = ReadGenerated(nameof(VisitorOnlyUnion));
        Assert.Contains("public interface IMatchVisitor", visitorOnly);
        Assert.Contains("public TResult Match<TVisitor, TResult>", visitorOnly);
        Assert.DoesNotContain("public T Match<T>(", visitorOnly);
        Assert.DoesNotContain("TryGetString", visitorOnly);

        var indirectOnly = ReadGenerated(nameof(IndirectOnlyUnion));
        Assert.Contains("public T Match<T>(", indirectOnly);
        Assert.Contains("public void Switch(", indirectOnly);
        Assert.DoesNotContain("public interface IMatchVisitor", indirectOnly);
        Assert.DoesNotContain("TryGetString", indirectOnly);
    }

    [Fact]
    public void Union_serialization_ToUtf8Bytes_and_FromUtf8Bytes_roundtrip_and_is_case_insensitive()
    {
        var u = JsonTestUnion.FromString2("hello");
        var bytes = u.ToUtf8Bytes();
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Equal("{\"string2\":\"hello\"}", json);

        var back = JsonTestUnion.FromUtf8Bytes(bytes);
        Assert.Equal(JsonTestUnion.UnionTag.String2, back.Tag);
        Assert.True(back.TryGetString2(out var s2));
        Assert.Equal("hello", s2);

        // case-insensitive key matching
        var backUpper = JsonTestUnion.FromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes("{\"STRING2\":\"hello\"}"));
        Assert.Equal(JsonTestUnion.UnionTag.String2, backUpper.Tag);
        Assert.True(backUpper.TryGetString2(out var s2b));
        Assert.Equal("hello", s2b);

        var u2 = JsonTestUnion.FromInt32(7);
        var bytes2 = u2.ToUtf8Bytes();
        var json2 = System.Text.Encoding.UTF8.GetString(bytes2);
        Assert.Equal("{\"int32\":7}", json2);

        var back2 = JsonTestUnion.FromUtf8Bytes(bytes2);
        Assert.Equal(JsonTestUnion.UnionTag.Int32, back2.Tag);
        Assert.True(back2.TryGetInt32(out var i));
        Assert.Equal(7, i);
    }

    [Fact]
    public void Union_serialization_can_be_configured_to_be_case_sensitive_on_deserialize()
    {
        var u = JsonTestUnionCaseSensitive.FromString2("hello");
        var bytes = u.ToUtf8Bytes();

        // Matches exact tag name and naming-policy variant, but not different casing.
        var back1 = JsonTestUnionCaseSensitive.FromUtf8Bytes(Encoding.UTF8.GetBytes("{\"String2\":\"hello\"}"));
        Assert.Equal(JsonTestUnionCaseSensitive.UnionTag.String2, back1.Tag);

        var back2 = JsonTestUnionCaseSensitive.FromUtf8Bytes(Encoding.UTF8.GetBytes("{\"string2\":\"hello\"}"));
        Assert.Equal(JsonTestUnionCaseSensitive.UnionTag.String2, back2.Tag);

        Assert.Throws<JsonException>(() =>
            JsonTestUnionCaseSensitive.FromUtf8Bytes(Encoding.UTF8.GetBytes("{\"STRING2\":\"hello\"}")));
    }

    [Fact]
    public void Union_serialization_case_sensitive_still_accepts_naming_policy_variant_payload_to_payload()
    {
        var u = JsonPayloadUnionCaseSensitive.FromPayload(7);
        var bytes = u.ToUtf8Bytes();
        var json = Encoding.UTF8.GetString(bytes);

        // CamelCase policy on the context: "Payload" -> "payload" on the wire.
        Assert.Equal("{\"payload\":7}", json);

        // Case-sensitive matching still accepts:
        // - raw tag name ("Payload")
        // - naming-policy converted name ("payload")
        var back1 = JsonPayloadUnionCaseSensitive.FromUtf8Bytes(Encoding.UTF8.GetBytes("{\"Payload\":7}"));
        Assert.True(back1.TryGetPayload(out var p1));
        Assert.Equal(7, p1);

        var back2 = JsonPayloadUnionCaseSensitive.FromUtf8Bytes(Encoding.UTF8.GetBytes("{\"payload\":7}"));
        Assert.True(back2.TryGetPayload(out var p2));
        Assert.Equal(7, p2);

        // But not different casing.
        Assert.Throws<JsonException>(() =>
            JsonPayloadUnionCaseSensitive.FromUtf8Bytes(Encoding.UTF8.GetBytes("{\"PAYLOAD\":7}")));
    }

    [Fact]
    public void Union_serialization_complex_roundtrip_with_arrays_and_nested_objects()
    {
        var payload = new ComplexPayload(
            Title: "t",
            Numbers: new[] { 1, 2, 3 },
            Items: new[] { new InnerItem(1, "a"), new InnerItem(2, "b") },
            Map: new Dictionary<string, InnerItem> { ["k"] = new InnerItem(9, "z") },
            Optional: null);

        var u = JsonComplexUnion.FromPayload(payload);
        var bytes = u.ToUtf8Bytes();
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("{\"payload\":", json);

        var back = JsonComplexUnion.FromUtf8Bytes(bytes);
        Assert.Equal(JsonComplexUnion.UnionTag.Payload, back.Tag);
        Assert.True(back.TryGetPayload(out var backPayload));

        Assert.Equal(payload.Title, backPayload.Title);
        Assert.Equal(payload.Numbers, backPayload.Numbers);
        Assert.Equal(payload.Items, backPayload.Items);
        Assert.Equal(payload.Map["k"], backPayload.Map["k"]);
        Assert.Null(backPayload.Optional);

        // case-insensitive key matching
        var backUpper = JsonComplexUnion.FromUtf8Bytes(System.Text.Encoding.UTF8.GetBytes("{\"PAYLOAD\":" + json.Substring("{\"payload\":".Length)));
        Assert.Equal(JsonComplexUnion.UnionTag.Payload, backUpper.Tag);
    }
}

