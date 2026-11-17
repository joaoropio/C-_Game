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

    // L0 = 64 B, L1 = 512 B, L2 = 4 KB. M is exactly 1 KB (the limit): allowed, but expensive to copy.
    private static readonly string Small = string.Join("\n", Levels.Split('\n')[..2])
        + "\nstruct M { public L1 a, b; }\nclass O { public M P { get; set; } public M f; public M g; }\n";

    private static string RecursiveMethodRepeating(string statement, int count, string helper = "") =>
        Small + $"class A {{ {helper} public static long F(O o, int n) {{ long s = 0; "
        + string.Concat(Enumerable.Repeat(statement, count)) + "return n == 0 ? s : F(o, n - 1) + s; } }";

    private static void AssertFrameTooBig(RunResult result) =>
        Assert.Contains("32 KB", Assert.IsType<CodeRejected>(result).Reason.En);

    private static string NestedTuple(int levels) =>
        levels == 0 ? "long" : $"({NestedTuple(levels - 1)}, {NestedTuple(levels - 1)})";

    private static void AssertTooBig(RunResult result, string name, int line)
    {
        var rejected = Assert.IsType<CodeRejected>(result);
        Assert.Equal(line, rejected.Line);
        Assert.Equal(
            $"A struct \"{name}\" é grande demais (mais de 1 KB) e rebentaria a pilha: usa uma class ou menos campos.",
            rejected.Reason.Pt);
        Assert.Equal(
            $"The struct \"{name}\" is too big (over 1 KB) and would blow the stack: use a class or fewer fields.",
            rejected.Reason.En);
    }

    [Fact]
    public async Task Run_NonGenericStructsEightFieldsPerLevelAsLocal_RejectedAtTheFirstOversizedDeclaration() => AssertTooBig(
        await Run(Levels + "class A { public static int F() { L6 x = default; return 1; } }"),
        "L2",
        3);

    [Fact]
    public async Task Run_OversizedStructNeverUsed_Rejected() => AssertTooBig(
        await Run(Levels + "class A { public static int F() => 1; }"),
        "L2",
        3);

    [Fact]
    public async Task Run_OversizedStructThroughAutoProperties_Rejected() => AssertTooBig(
        await Run(
            Levels.Split('\n')[..2].Aggregate((a, b) => a + "\n" + b)
            + "\nstruct Big { public L1 A { get; set; } public L1 B { get; set; } public L1 C { get; set; } }\n"
            + "class A { public static int F() => 1; }"),
        "Big",
        3);

    [Fact]
    public async Task Run_NestedTupleAsLocal_Rejected()
    {
        var rejected = Assert.IsType<CodeRejected>(
            await Run($"class A {{ public static int F() {{ {NestedTuple(12)} x = default; return 1; }} }}"));

        Assert.Equal(1, rejected.Line);
        Assert.Contains("1 KB", rejected.Reason.En);
    }

    [Fact]
    public async Task Run_OversizedGenericUsedOnlyAsTypeArgumentOfAReferenceType_Rejected() => AssertTooBig(
        await Run($"class A {{ public static int F() {{ var list = new List<{NestedTuple(12)}>(); return list.Count; }} }}"),
        "ValueTuple",
        1);

    [Theory]
    [InlineData("struct Rune { public int Power; public string Name; }", "Rune r = default; return 1;")]
    [InlineData("struct P { public long a, b, c, d; public double e; public bool f; }", "P p = default; return 1;")]
    [InlineData("struct Pair { public KeyValuePair<int, long> a; public KeyValuePair<string, int> b; public List<int> c; }", "Pair p = default; return 1;")]
    [InlineData("enum Color { Red }\nstruct Tile { public Color C; public int? Hp; public (int, int) Pos; public decimal Cost; }", "Tile t = default; return 1;")]
    public async Task Run_NormalStructs_Passed(string declaration, string body) =>
        Assert.IsType<Passed>(await Run($"{declaration}\nclass A {{ public static int F() {{ {body} }} }}"));

    [Fact]
    public async Task Run_StructExactlyOneKilobyte_Passed() =>
        Assert.IsType<Passed>(await Run(Small + "class A { public static int F() { M x = default; return 1; } }"));

    [Fact]
    public async Task Run_StructOneKilobytePlusEightBytes_Rejected() => AssertTooBig(
        await Run($"struct B {{ public {NestedTuple(0)} {string.Join(", ", Enumerable.Range(0, 129).Select(i => "f" + i))}; }}\nclass A {{ public static int F() => 1; }}"),
        "B",
        1);

    // Polymorphic recursion: the struct grows with every call, so no static size is ever big. Generics are not
    // taught before World 4, so declaring them is what gets rejected (these programs never run).
    [Theory]
    [InlineData("struct P<T> { public T a; public T b; }\nclass A { public static int F<T>(int n) { P<T> x = default; return n == 0 ? 1 : F<P<T>>(n - 1) + (x.Equals(x) ? 0 : 1); } }")]
    [InlineData("struct P<T> { public T a; public T b; }\nstatic class H<T> { public static int G(int n) { P<T> x = default; return n == 0 ? 1 : H<P<T>>.G(n - 1) + (x.Equals(x) ? 0 : 1); } }")]
    [InlineData("struct P<T> { public T a; public T b; }\nclass A { public static int F<T>(T v, int n) { var x = new P<T> { a = v, b = v }; return n == 0 ? 1 : F(x, n - 1); } }")]
    public async Task Run_GenericRecursionThatGrowsTheStruct_RejectedBecauseOfTheGenericDeclaration(string playerCode)
    {
        var rejected = Assert.IsType<CodeRejected>(await Run(playerCode));

        Assert.Equal(1, rejected.Line);
        Assert.Equal("Ainda não podes escrever genéricos (<T>): chegam no Mundo 4.", rejected.Reason.Pt);
        Assert.Equal("You can't write generics (<T>) yet: they arrive in World 4.", rejected.Reason.En);
    }

    private static string ManyLocals(int count, string type) =>
        Small
        + $"class A {{ public static long F(int n) {{ long s = 0; "
        + string.Concat(Enumerable.Range(0, count).Select(i => $"{type} v{i} = default; s += v{i}.a.a.a; "))
        + "return n == 0 ? s : F(n - 1) + s; } }";

    [Fact]
    public async Task Run_ManyOneKilobyteLocalsInOneRecursiveMethod_RejectedBeforeRunning()
    {
        var rejected = Assert.IsType<CodeRejected>(await Run(ManyLocals(100, "M")));

        Assert.Equal(5, rejected.Line);
        Assert.Equal(
            "O método \"F\" guarda demasiado na pilha (mais de 32 KB em structs): usa class ou menos variáveis.",
            rejected.Reason.Pt);
        Assert.Equal(
            "The method \"F\" keeps too much on the stack (over 32 KB of structs): use a class or fewer variables.",
            rejected.Reason.En);
    }

    [Fact]
    public async Task Run_FewOneKilobyteLocals_Passed() =>
        Assert.IsType<Passed>(await Run(ManyLocals(3, "M")));

    [Fact]
    public async Task Run_ManyBigParameters_Rejected() =>
        Assert.IsType<CodeRejected>(await Run(
            Small
            + "class A { static long H(" + string.Join(", ", Enumerable.Range(0, 60).Select(i => $"M p{i}"))
            + ") => p0.a.a.a; public static long F(int n) => n == 0 ? 0 : F(n - 1); }"));

    [Fact]
    public async Task Run_TemporariesCountTowardsTheBudget_Rejected() =>
        Assert.IsType<CodeRejected>(await Run(
            Small
            + "class A { static M Make() => default; public static long F() { long s = 0; "
            + string.Concat(Enumerable.Repeat("s += Make().a.a.a; ", 40)) + "return s; } }"));

    [Fact]
    public async Task Run_BigLocalInALambda_BudgetIsPerBodyNotPerMethod() =>
        Assert.IsType<Passed>(await Run(
            Small
            + "class A { public static long F() { System.Func<long> a = () => { M x = default; return x.a.a.a; };"
            + " System.Func<long> b = () => { M y = default; return y.a.a.a; }; System.Func<long> c = () => { M z = default; return z.a.a.a; };"
            + " return a() + b() + c(); } }"));

    // Reads that copy a struct (property getter, field, conditional, argument) each get their own slot in a debug build.
    [Fact]
    public async Task Run_PropertyReadsRepeated_Rejected() =>
        AssertFrameTooBig(await Run(RecursiveMethodRepeating("s += o.P.a.a.a; ", 100)));

    [Fact]
    public async Task Run_PropertyReadsPassedAsArguments_Rejected() =>
        AssertFrameTooBig(await Run(RecursiveMethodRepeating("s += H(o.P); ", 50, "static long H(M a) => a.a.a.a;")));

    [Fact]
    public async Task Run_ManyArgumentCopies_Rejected() => AssertFrameTooBig(await Run(RecursiveMethodRepeating(
        "s += H(o.f, o.f, o.f, o.f, o.f, o.f, o.f); ", 15, "static long H(M a, M b, M c, M d, M e, M f, M g) => a.a.a.a;")));

    [Fact]
    public async Task Run_ConditionalCopiesRepeated_Rejected() =>
        AssertFrameTooBig(await Run(RecursiveMethodRepeating("s += (n > 0 ? o.f : o.g).a.a.a; ", 100)));

    [Fact]
    public async Task Run_FewStructReads_Passed() =>
        Assert.IsType<Passed>(await Run(RecursiveMethodRepeating("s += o.P.a.a.a; ", 3)));

    [Fact]
    public async Task Run_ManyIntAndDoubleExpressions_Passed() => Assert.IsType<Passed>(await Run(
        "class A { public static double F(int n) { double s = 0; "
        + string.Concat(Enumerable.Repeat("s += n * 2 + 1.5; ", 300)) + "return s; } }"));

    [Theory]
    [InlineData("class Box<T> { public T Value; }")]
    [InlineData("struct Box<T> { public T Value; }")]
    [InlineData("interface IBox<T> { }")]
    [InlineData("class A { public static T Id<T>(T x) => x; }")]
    [InlineData("class A { public static int F() { void G<T>() { } return 1; } }")]
    public async Task Run_PlayerGenericDeclarations_Rejected(string playerCode)
    {
        var rejected = Assert.IsType<CodeRejected>(await Run(playerCode));

        Assert.Equal("You can't write generics (<T>) yet: they arrive in World 4.", rejected.Reason.En);
    }

    [Fact]
    public async Task Run_LibraryGenerics_StillAllowed() =>
        Assert.IsType<Passed>(await Run(
            "class A { public static int F() { var d = new Dictionary<string, List<int>>(); d[\"a\"] = new List<int> { 1 }; return d.Count + d[\"a\"].Select(x => x).Count(); } }"));
}
