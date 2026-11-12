using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class RoslynCodeRunnerTests
{
    private const string Template = """
        public static class Spell
        {
            public static int Twice(int x)
            {
                //{{PLAYER_CODE}}
            }
        }
        """;

    private const string Validation = """
        public static class Validation
        {
            public static System.Collections.Generic.IEnumerable<(string Pt, string En)> Run()
            {
                if (Spell.Twice(2) != 4)
                {
                    yield return ("Twice(2) devia ser 4.", "Twice(2) should be 4.");
                }
            }
        }
        """;

    private readonly RoslynCodeRunner _runner = new();

    private Task<RunResult> Run(string playerCode, string validation = Validation) =>
        _runner.RunAsync(new RunRequest(Template, playerCode, validation), Language.En, CancellationToken.None);

    [Fact]
    public async Task Run_CorrectCode_Passed() =>
        Assert.IsType<Passed>(await Run("return x * 2;"));

    [Fact]
    public async Task Run_WrongResult_ChecksFailedWithMessages()
    {
        var result = Assert.IsType<ChecksFailed>(await Run("return x + 1;"));

        var failure = Assert.Single(result.Failures);
        Assert.Equal("Twice(2) should be 4.", failure.En);
        Assert.Equal("Twice(2) devia ser 4.", failure.Pt);
    }

    [Fact]
    public async Task Run_CompileError_CompilationFailed()
    {
        var result = Assert.IsType<CompilationFailed>(await Run("return x *;"));

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Run_EmptyCode_CodeRejected() =>
        Assert.IsType<CodeRejected>(await Run("   "));

    [Fact]
    public async Task Run_ClosingBrace_CodeRejected() =>
        Assert.IsType<CodeRejected>(await Run("return x * 2; }"));

    [Fact]
    public async Task Run_ForbiddenApi_CodeRejected() =>
        Assert.IsType<CodeRejected>(await Run("System.Environment.Exit(1);\nreturn x;"));

    [Fact]
    public async Task Run_WhileTrue_TimedOutUnder5Seconds()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var result = await Run("while (true) { }");

        Assert.IsType<TimedOut>(result);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }

    [Theory]
    [InlineData("for (;;) { }")]
    [InlineData("for (int i = 0; i < 2000; i++) { for (int j = 0; j < 2000; j++) { } }\nreturn x;")]
    public async Task Run_ForEverAndNestedLoops_TimedOut(string playerCode)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var result = await Run(playerCode);

        Assert.IsType<TimedOut>(result);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }

    [Fact]
    public async Task Run_BlockedOutsidePlayerCode_TimedOutByWallClock()
    {
        const string blockingValidation = """
            public static class Validation
            {
                public static System.Collections.Generic.IEnumerable<(string Pt, string En)> Run()
                {
                    System.Threading.Thread.Sleep(4000);
                    yield break;
                }
            }
            """;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var result = await Run("return x;", blockingValidation);

        Assert.IsType<TimedOut>(result);
        Assert.InRange(watch.Elapsed, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Run_ValidationSourceWithoutValidationType_ThrowsInvalidOperation()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run("return x;", "public static class Other { }"));

        Assert.Contains("Validation", exception.Message);
    }

    [Fact]
    public async Task Run_ValidationWithoutRunMethod_ThrowsInvalidOperation() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run("return x;", "public static class Validation { }"));

    [Fact]
    public async Task Run_ValidationRunWithWrongReturnType_ThrowsInvalidOperation() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run("return x;", "public static class Validation { public static int Run() => 0; }"));

    [Fact]
    public async Task Run_ExceptionInsideValidationRun_RuntimeError()
    {
        const string throwingValidation = """
            public static class Validation
            {
                public static System.Collections.Generic.IEnumerable<(string Pt, string En)> Run()
                {
                    throw new System.FormatException("bad");
                    yield break;
                }
            }
            """;

        var result = Assert.IsType<RuntimeError>(await Run("return x;", throwingValidation));

        Assert.Equal("bad", result.Message);
        Assert.Null(result.Line);
    }

    [Fact]
    public async Task Run_DivideByZero_RuntimeErrorWithPlayerLine()
    {
        var result = Assert.IsType<RuntimeError>(await Run("int zero = 0;\nreturn x / zero;"));

        Assert.Equal(2, result.Line);
        Assert.Equal("You divided by zero.", result.Explanation.En);
    }

    [Fact]
    public async Task Run_Passed_ReportsStatementCount()
    {
        var result = Assert.IsType<Passed>(await Run("int doubled = x * 2;\nreturn doubled;"));

        Assert.Equal(StatementCounter.Count("int doubled = x * 2;\nreturn doubled;"), result.StatementCount);
        Assert.Equal(2, result.StatementCount);
    }

    [Fact]
    public async Task Run_TwiceInARow_BothPass()
    {
        Assert.IsType<Passed>(await Run("return x * 2;"));
        Assert.IsType<Passed>(await Run("return x + x;"));
    }
}
