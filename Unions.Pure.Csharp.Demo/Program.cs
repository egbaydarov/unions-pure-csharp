using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using OneOf;

namespace Unions.Pure.Csharp.Demo;

internal static class Program
{
    private static readonly Func<string?, int> OnStringLen = static s => s!.Length;
    private static readonly Func<int?, int> OnIntIdentity = static i => i!.Value;

    private static readonly Action<string?> OnStringNoop = static _ => { };
    private static readonly Action<int?> OnIntNoop = static _ => { };

    private readonly struct DemoUnionMatchVisitor : DemoUnion.IMatchVisitor<int>
    {
        public int OnString(string? value) => value!.Length;
        public int OnInt32(int? value) => value!.Value;
    }

    private readonly struct DemoUnionSwitchVisitor : DemoUnion.ISwitchVisitor
    {
        public void OnString(string? value) => s_switchSink = 111;
        public void OnInt32(int? value) => s_switchSink = 222;
    }

    // Used to demonstrate allocation-free Switch (no capturing lambdas).
    private static int s_switchSink;
    private static readonly Action<string?> OnStringSet111 = static _ => s_switchSink = 111;
    private static readonly Action<int?> OnIntSet222 = static _ => s_switchSink = 222;

    private static int s_oneOfSwitchSink;
    private static readonly Action<string> OnStringSet111_OneOf = static _ => s_oneOfSwitchSink = 111;
    private static readonly Action<int> OnIntSet222_OneOf = static _ => s_oneOfSwitchSink = 222;

    public static int Main(string[] args)
    {
        // Drive JIT compilation of the Asm_* methods.
        var x = args.Length + Environment.TickCount;

        var r0 = Asm_Match(DemoUnion.FromString("hello"));
        var r1 = Asm_Switch_NoAlloc(DemoUnion.FromInt32(123));
        var r1b = Asm_Switch_CapturingAlloc(DemoUnion.FromInt32(123));
        var r2 = Asm_MatchAndSwitch(x);
        var r3 = Asm_Match_Visitor(DemoUnion.FromString("hello"));
        var r4 = Asm_Switch_Visitor(DemoUnion.FromInt32(123));
        var r5 = Asm_TryGet_Inline(DemoUnion.FromString("hello"));

        var o0 = Asm_OneOf_Match((OneOf<string, int>)"hello");
        var o1 = Asm_OneOf_Switch_NoAlloc((OneOf<string, int>)123);
        var o1b = Asm_OneOf_Switch_CapturingAlloc((OneOf<string, int>)123);
        var o2 = Asm_OneOf_MatchAndSwitch(x);

        var j0 = Asm_Json_WithResolver();
        var j1 = Asm_Json_WithoutResolver();

        // Prevent the optimizer from discarding results.
        if ((r0 ^ r1 ^ r1b ^ r2 ^ r3 ^ r4 ^ r5 ^ o0 ^ o1 ^ o1b ^ o2 ^ j0 ^ j1) == int.MinValue)
            Console.WriteLine("unreachable");

        return r0 + r1 + r1b + r2 + r3 + r4 + r5 + o0 + o1 + o1b + o2 + j0 + j1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_Match(DemoUnion u)
        => u.Match(onString: OnStringLen, onInt32: OnIntIdentity);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_Match_Visitor(DemoUnion u)
        => u.Match<DemoUnionMatchVisitor, int>(default);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_Switch_NoAlloc(DemoUnion u)
    {
        s_switchSink = 0;
        u.Switch(onString: OnStringSet111, onInt32: OnIntSet222);
        return s_switchSink;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_TryGet_Inline(DemoUnion u)
    {
        // Fully inline usage: no delegates, no visitors, no indirect calls.
        if (u.TryGetString(out var s))
            return s.Length;

        if (u.TryGetInt32(out var i))
            return i!.Value;

        return -1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_Switch_Visitor(DemoUnion u)
    {
        s_switchSink = 0;
        u.Switch<DemoUnionSwitchVisitor>(default);
        return s_switchSink;
    }

    // This intentionally allocates (closure + delegates) and is here as a cautionary example.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_Switch_CapturingAlloc(DemoUnion u)
    {
        var result = 0;
        u.Switch(
            onString: _ => result = 111,
            onInt32: _ => result = 222);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_MatchAndSwitch(int x)
    {
        // Make Tag unknown to the JIT by selecting based on runtime input.
        var u = (x & 1) == 0
            ? DemoUnion.FromString("world")
            : DemoUnion.FromInt32(7);

        var a = u.Match(onString: OnStringLen, onInt32: OnIntIdentity);

        // Also exercise Switch in the same method.
        u.Switch(onString: OnStringNoop, onInt32: OnIntNoop);

        return a;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_OneOf_Match(OneOf<string, int> u)
        => u.Match(s => s.Length, i => i);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_OneOf_Switch_NoAlloc(OneOf<string, int> u)
    {
        s_oneOfSwitchSink = 0;
        u.Switch(OnStringSet111_OneOf, OnIntSet222_OneOf);
        return s_oneOfSwitchSink;
    }

    // This intentionally allocates (closure + delegates) and is here as a cautionary example.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_OneOf_Switch_CapturingAlloc(OneOf<string, int> u)
    {
        var result = 0;
        u.Switch(
            _ => result = 111,
            _ => result = 222);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_OneOf_MatchAndSwitch(int x)
    {
        OneOf<string, int> u = (x & 1) == 0 ? "world" : 7;
        var a = u.Match(s => s.Length, i => i);
        u.Switch(_ => { }, _ => { });
        return a;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_Json_WithResolver()
    {
        var payload = new ComplexPayload(
            Title: "t",
            Numbers: [ 1, 2, 3 ],
            Items: new[] { new InnerItem(1, "a"), new InnerItem(2, "b") },
            Map: new Dictionary<string, InnerItem> { ["k"] = new InnerItem(9, "z") },
            Optional: null);

        var ctx = DemoJsonContext.Default;
        var u = JsonDemoUnion.FromPayload(payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(u, ctx.JsonDemoUnion);

        // Roundtrip (also keeps the JIT from dead-code eliminating JSON work).
        var back = JsonSerializer.Deserialize(bytes, ctx.JsonDemoUnion);
        if (!back.TryGetPayload(out _))
            return -1;

        return bytes.Length;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Asm_Json_WithoutResolver()
    {
        var payload = new ComplexPayload(
            Title: "t",
            Numbers: [1, 2, 3],
            Items: new[] { new InnerItem(1, "a"), new InnerItem(2, "b") },
            Map: new Dictionary<string, InnerItem> { ["k"] = new InnerItem(9, "z") },
            Optional: null);

        // Classic STJ reflection-based serialization (no source-gen context).
        // Kept as a comparison point in the demo.
        var json = JsonSerializer.Serialize(payload);
        return Encoding.UTF8.GetByteCount(json);
    }
}


