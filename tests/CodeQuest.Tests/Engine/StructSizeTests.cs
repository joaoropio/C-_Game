using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

// A struct local reserves its whole frame before the guard can run, so a struct that is too big would end the
// process with an uncatchable stack overflow. These tests assert the code is rejected BEFORE running: if the
// check regresses, they would crash the test host, so keep the oversized cases small in number and never loosen them.
public class StructSizeTests
{
    private const string Template = "// {{PLAYER_CODE}}";

    private const string Levels =
        "struct L0 { public long a, b, c, d, e, f, g, h; }\n"
        + "struct L1 { public L0 a, b, c, d, e, f, g, h; }\n"
        + "struct L2 { public L1 a, b, c, d, e, f, g, h; }\n"
        + "struct L3 { public L2 a, b, c, d, e, f, g, h; }\n"
        + "struct L4 { public L3 a, b, c, d, e, f, g, h; }\n"
        + "struct L5 { public L4 a, b, c, d, e, f, g, h; }\n"
        + "struct L6 { public L5 a, b, c, d, e, f, g, h; }\n";

    private const string ValidationSource = """
        public static class Validation
        {
            public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
            {
                yield return ("probe", "1", "1");
            }
        }
        """;

    private readonly RoslynCodeRunner _runner = new();

    private Task<RunResult> Run(string playerCode) =>
        _runner.RunAsync(new RunRequest(Template, playerCode, ValidationSource), Language.En, CancellationToken.None);

    private static string Nested(string element, int levels) =>
        string.Concat(Enumerable.Repeat("S<", levels)) + element + new string('>', levels);

    private static string NestedTuple(int levels) =>
        levels == 0 ? "long" : $"({NestedTuple(levels - 1)}, {NestedTuple(levels - 1)})";

    private static void AssertTooBig(RunResult result, string name, int line)
    {
        var rejected = Assert.IsType<CodeRejected>(result);
        Assert.Equal(line, rejected.Line);
        Assert.Equal(
            $"A struct \"{name}\" é grande demais (mais de 16 KB) e rebentaria a pilha: usa uma class ou menos campos.",
            rejected.Reason.Pt);
        Assert.Equal(
            $"The struct \"{name}\" is too big (over 16 KB) and would blow the stack: use a class or fewer fields.",
            rejected.Reason.En);
    }

    [Fact]
    public async Task Run_GenericStructNestedEighteenDeepAsLocal_Rejected() => AssertTooBig(
        await Run(
            "struct S<T> { public T a; public T b; }\n"
            + "class A\n{\n    public static int F()\n    {\n"
            + $"        {Nested("long", 18)} x = default;\n"
            + "        return 1;\n    }\n}"),
        "S",
        6);

    [Fact]
    public async Task Run_NonGenericStructsEightFieldsPerLevelAsLocal_RejectedAtTheFirstOversizedDeclaration() => AssertTooBig(
        await Run(Levels + "class A { public static int F() { L6 x = default; return 1; } }"),
        "L3",
        4);

    [Fact]
    public async Task Run_OversizedStructNeverUsed_Rejected() => AssertTooBig(
        await Run(Levels + "class A { public static int F() => 1; }"),
        "L3",
        4);

    [Fact]
    public async Task Run_OversizedStructThroughAutoProperties_Rejected() => AssertTooBig(
        await Run(
            Levels.Split('\n')[..3].Aggregate((a, b) => a + "\n" + b)
            + "\nstruct Big { public L2 A { get; set; } public L2 B { get; set; } public L2 C { get; set; } public L2 D { get; set; } public L2 E { get; set; } }\n"
            + "class A { public static int F() => 1; }"),
        "Big",
        4);

    [Fact]
    public async Task Run_NestedTupleAsLocal_Rejected()
    {
        var rejected = Assert.IsType<CodeRejected>(
            await Run($"class A {{ public static int F() {{ {NestedTuple(12)} x = default; return 1; }} }}"));

        Assert.Equal(1, rejected.Line);
        Assert.Contains("16 KB", rejected.Reason.En);
    }

    [Fact]
    public async Task Run_OversizedGenericUsedOnlyAsTypeArgumentOfAReferenceType_Rejected() => AssertTooBig(
        await Run(
            "struct S<T> { public T a; public T b; }\n"
            + $"class A {{ public static int F() {{ var list = new List<{Nested("long", 18)}>(); return list.Count; }} }}"),
        "S",
        2);

    [Fact]
    public async Task Run_SelfReferencingGenericStruct_NeverRunsAndDoesNotHang()
    {
        var result = await Run(
            "struct S<T> { public S<S<T>> next; }\nclass A { public static int F() { S<int> x = default; return 1; } }");

        Assert.True(result is CodeRejected or CompilationFailed);
    }

    [Theory]
    [InlineData("struct Rune { public int Power; public string Name; }", "Rune r = default; return 1;")]
    [InlineData("struct P { public long a, b, c, d; public double e; public bool f; }", "P p = default; return 1;")]
    [InlineData("struct Box<T> { public T Value; }\nstruct Pair { public Box<int> a; public Box<long> b; public Box<string> c; }", "Pair p = default; return 1;")]
    [InlineData("enum Color { Red }\nstruct Tile { public Color C; public int? Hp; public (int, int) Pos; public decimal Cost; }", "Tile t = default; return 1;")]
    public async Task Run_NormalStructs_Passed(string declaration, string body) =>
        Assert.IsType<Passed>(await Run($"{declaration}\nclass A {{ public static int F() {{ {body} }} }}"));

    [Fact]
    public async Task Run_StructJustUnderTheLimit_Passed() =>
        Assert.IsType<Passed>(await Run(
            Levels.Split('\n')[..3].Aggregate((a, b) => a + "\n" + b)
            + "\nclass A { public static int F() { L2 x = default; return 1; } }"));
}
