using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class ClassScopeRunnerTests
{
    private const string Template = """
        public static class Spell
        {
            //{{PLAYER_CODE}}

            public static int Cast(int x) => Twice(x);
        }
        """;

    private const string Validation = """
        public static class Validation
        {
            public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
            {
                yield return ("x = 2", "4", Spell.Cast(2).ToString());
            }
        }
        """;

    private const string GoodTwice = "public static int Twice(int x)\n{\n    return x * 2;\n}";

    private readonly RoslynCodeRunner _runner = new();

    private Task<RunResult> Run(string playerCode) =>
        _runner.RunAsync(new RunRequest(Template, playerCode, Validation), Language.En, CancellationToken.None);

    [Fact]
    public async Task Run_PlayerMethod_Passed() =>
        Assert.IsType<Passed>(await Run(GoodTwice));

    [Fact]
    public async Task Run_Passed_CountsStatementsInsideTheMethods()
    {
        var result = Assert.IsType<Passed>(await Run(GoodTwice));

        Assert.Equal(1, result.StatementCount);
    }

    [Fact]
    public async Task Run_ExpressionBodiedMethod_Passed() =>
        Assert.IsType<Passed>(await Run("static int Twice(int x) => x * 2;"));

    [Theory]
    [InlineData("static int Twice(int x)\n{\n    return Twice(x);\n}")]
    [InlineData("static int Twice(int x) => Twice(x) + 1;")]
    [InlineData("static int Twice(int x) { return Other(x); }\nstatic int Other(int x) { return Twice(x); }")]
    [InlineData("static int Twice(int x) => Other(x);\nstatic int Other(int x) => Twice(x);")]
    [InlineData("static int Twice(int x)\n{\n    try { return Twice(x); }\n    catch { return 0; }\n}")]
    [InlineData("static int Twice(int x)\n{\n    try { return Twice(x); }\n    finally { Console.Write(\"\"); }\n}")]
    public async Task Run_InfiniteRecursionBetweenPlayerMethods_RuntimeErrorAndNextRunPasses(string playerCode)
    {
        var result = Assert.IsType<RuntimeError>(await Run(playerCode));

        Assert.Equal("The spell keeps calling itself and never stops (infinite recursion).", result.Explanation.En);
        Assert.IsType<Passed>(await Run(GoodTwice));
    }

    [Fact]
    public async Task Run_RecursionReportsTheCallLineInsideTheMethod()
    {
        var result = Assert.IsType<RuntimeError>(await Run("static int Twice(int x)\n{\n    return Twice(x);\n}"));

        Assert.Equal(3, result.Line);
    }

    [Fact]
    public async Task Run_BoundedRecursion_Passed() =>
        Assert.IsType<Passed>(await Run(
            "static int Twice(int x) => x <= 0 ? 0 : 2 + Twice(x - 1);"));

    [Fact]
    public async Task Run_InfiniteLoopInMethod_TimedOut() =>
        Assert.IsType<TimedOut>(await Run("static int Twice(int x)\n{\n    while (true) { }\n}"));

    [Fact]
    public async Task Run_LocalFunctionAndLambdaInsideMethod_StillGuarded()
    {
        var result = Assert.IsType<RuntimeError>(await Run(
            "static int Twice(int x)\n{\n    int F(int n) => F(n);\n    return F(x);\n}"));

        Assert.Equal(
            "The spell keeps calling itself and never stops (infinite recursion).",
            result.Explanation.En);
    }

    [Theory]
    [InlineData("static class Helper { }")]
    [InlineData("class Helper { }")]
    [InlineData("struct Point { }")]
    [InlineData("record Pair(int A, int B);")]
    [InlineData("interface IThing { }")]
    [InlineData("enum Color { Red }")]
    [InlineData("delegate int Op(int a);")]
    public async Task Run_NestedType_CodeRejected(string playerCode)
    {
        var result = Assert.IsType<CodeRejected>(await Run(GoodTwice + "\n" + playerCode));

        Assert.Equal(5, result.Line);
        Assert.Equal("You can't create types yet: write only static methods.", result.Reason.En);
    }

    [Theory]
    [InlineData("static int counter;")]
    [InlineData("static int Counter { get; set; }")]
    [InlineData("static readonly int Limit = 3;")]
    [InlineData("const int Limit = 3;")]
    [InlineData("static event Action Changed;")]
    [InlineData("static Spell() { }")]
    public async Task Run_NonMethodMember_CodeRejected(string playerCode)
    {
        var result = Assert.IsType<CodeRejected>(await Run(GoodTwice + "\n" + playerCode));

        Assert.Equal(5, result.Line);
        Assert.Equal("For now you can only write static methods, not fields, properties or constructors.", result.Reason.En);
    }

    [Fact]
    public async Task Run_ExtraClosingBrace_CodeRejected() =>
        Assert.IsType<CodeRejected>(await Run(GoodTwice + "\n}"));

    [Fact]
    public async Task Run_ErrorInsideMethod_ReportsLineAndColumn()
    {
        var result = Assert.IsType<CompilationFailed>(await Run("static int Twice(int x)\n{\n    return x * undefined;\n}"));

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.Line);
        Assert.Equal(16, error.Column);
    }

    [Fact]
    public async Task Run_InstanceMethod_CompilationFailed() =>
        Assert.IsType<CompilationFailed>(await Run("int Twice(int x) => x * 2;"));
}
