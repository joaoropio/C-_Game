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
            public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
            {
                yield return ("x = 2", "4", Spell.Twice(2).ToString());
                yield return ("x = 5", "10", Spell.Twice(5).ToString());
            }
        }
        """;

    private readonly RoslynCodeRunner _runner = new();

    private Task<RunResult> Run(string playerCode, string validation = Validation) =>
        _runner.RunAsync(new RunRequest(Template, playerCode, validation), Language.En, CancellationToken.None);

    [Fact]
    public async Task Run_CorrectCode_Passed() =>
        Assert.IsType<Passed>(await Run("return x * 2;"));

    [Theory]
    [InlineData("return Math.Max(x * 2, 1);")]
    [InlineData("Console.WriteLine(\"x\");\nreturn x * 2;")]
    [InlineData("var list = new List<int>();\nlist.Add(x);\nreturn list[0] * 2;")]
    [InlineData("var text = new StringBuilder();\ntext.Append(x);\nreturn int.Parse(text.ToString()) * 2;")]
    [InlineData("return new[] { x, x }.Where(n => n > 0).Sum();")]
    public async Task Run_AllowedNamespacesWithoutUsings_Passed(string playerCode) =>
        Assert.IsType<Passed>(await Run(playerCode));

    [Fact]
    public async Task Run_WrongResult_ChecksFailedWithEveryCheck()
    {
        var result = Assert.IsType<ChecksFailed>(await Run("return x + 1;"));

        Assert.Equal(2, result.Checks.Count);
        Assert.Equal(new CheckOutcome("x = 2", "4", "3"), result.Checks[0]);
        Assert.False(result.Checks[0].Passed);
        Assert.False(result.Checks[1].Passed);
    }

    [Fact]
    public async Task Run_PartlyWrong_ChecksFailedKeepsPassingChecksToo()
    {
        var result = Assert.IsType<ChecksFailed>(await Run("return x == 5 ? 10 : x + 1;"));

        Assert.False(result.Checks[0].Passed);
        Assert.True(result.Checks[1].Passed);
    }

    [Fact]
    public async Task Run_CorrectCode_PassedCarriesEveryCheck()
    {
        var result = Assert.IsType<Passed>(await Run("return x * 2;"));

        Assert.Equal(
            [new CheckOutcome("x = 2", "4", "4"), new CheckOutcome("x = 5", "10", "10")],
            result.Checks);
        Assert.All(result.Checks, check => Assert.True(check.Passed));
    }

    [Fact]
    public async Task Run_ValidationRunWithOldReturnType_ThrowsInvalidOperation() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run(
                "return x;",
                "public static class Validation { public static System.Collections.Generic.IEnumerable<(string Pt, string En)> Run() { yield break; } }"));

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

    [Theory]
    [InlineData("while (new Func<bool>(() => { while (true) { } return false; })()) { }\nreturn x;")]
    [InlineData("for (var b = new Func<bool>(() => { while (true) { } return false; })(); b; ) { }\nreturn x;")]
    [InlineData("for (var i = 0; i < 1; i += new Func<int>(() => { while (true) { } return 1; })()) { }\nreturn x;")]
    [InlineData("foreach (var i in new Func<int[]>(() => { while (true) { } return new int[0]; })()) { }\nreturn x;")]
    [InlineData("foreach (var (a, b) in new Func<(int, int)[]>(() => { while (true) { } return []; })()) { }\nreturn x;")]
    [InlineData("do { } while (new Func<bool>(() => { while (true) { } return false; })());\nreturn x;")]
    public async Task Run_LoopHiddenInLoopHeader_TimedOutByTickLimit(string playerCode)
    {
        // Warm up Roslyn so the bound measures the tick limit, not the first compilation.
        Assert.IsType<Passed>(await Run("return x * 2;"));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var result = await Run(playerCode);

        Assert.IsType<TimedOut>(result);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"took {watch.Elapsed}");
    }

    [Fact]
    public async Task Run_TimeoutExceptionThrownByThePlayer_IsARuntimeErrorNotATimeout()
    {
        var result = await Run("throw new TimeoutException();");

        Assert.IsType<RuntimeError>(result);
    }

    [Fact]
    public async Task Run_BlockedOutsidePlayerCode_TimedOutByWallClock()
    {
        const string blockingValidation = """
            public static class Validation
            {
                public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
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


    // A type the runtime cannot load (here a struct of about 1 GB declared by the validation, never instantiated)
    // makes Assembly.GetTypes throw; the run must still end in a result, never in an exception out of RunAsync.
    [Fact]
    public async Task Run_AssemblyHasATypeThatCannotLoad_RuntimeErrorInsteadOfAnException()
    {
        const string unloadable = """
            struct L0 { public long a, b, c, d, e, f, g, h; }
            struct L1 { public L0 a, b, c, d, e, f, g, h; }
            struct L2 { public L1 a, b, c, d, e, f, g, h; }
            struct L3 { public L2 a, b, c, d, e, f, g, h; }
            struct L4 { public L3 a, b, c, d, e, f, g, h; }
            struct L5 { public L4 a, b, c, d, e, f, g, h; }
            struct L6 { public L5 a, b, c, d, e, f, g, h; }
            struct L7 { public L6 a, b, c, d, e, f, g, h; }
            struct L8 { public L7 a, b, c, d, e, f, g, h; }
            public static class Validation
            {
                public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
                {
                    yield return ("x", "1", "1");
                }
            }
            """;

        var result = Assert.IsType<RuntimeError>(await Run("return x;", unloadable));

        Assert.Equal("The spell failed while running.", result.Explanation.En);
    }
    [Fact]
    public async Task Run_ExceptionInsideValidationRun_RuntimeError()
    {
        const string throwingValidation = """
            public static class Validation
            {
                public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
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

    private async Task<RuntimeError> RunRecursive(string playerCode)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var result = Assert.IsType<RuntimeError>(await Run(playerCode));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
        Assert.Equal("The spell keeps calling itself and never stops (infinite recursion).", result.Explanation.En);
        Assert.Equal("O feitiço chama-se a si próprio sem parar (recursão infinita).", result.Explanation.Pt);
        return result;
    }

    [Fact]
    public async Task Run_EnclosingMethodRecursion_RuntimeErrorWithPlayerLine()
    {
        var result = await RunRecursive("int unused = 1;\nreturn Twice(x);");

        Assert.Equal(2, result.Line);
    }

    [Theory]
    [InlineData("int F(int n) { return F(n); }\nreturn F(x);")]
    [InlineData("int F(int n)\n{\n    return F(n);\n}\nreturn F(x);")]
    [InlineData("int F(int n) => F(n);\nreturn F(x);")]
    [InlineData("int F(int n) => F(n) + 1;\nreturn F(x);")]
    [InlineData("int F(int n) { return G(n); }\nint G(int n) { return F(n); }\nreturn F(x);")]
    [InlineData("Func<int, int> f = null;\nf = n => f(n);\nreturn f(x);")]
    [InlineData("Func<int, int> f = null;\nf = n => { return f(n); };\nreturn f(x);")]
    [InlineData("Func<int, int> f = null;\nf = delegate (int n) { return f(n); };\nreturn f(x);")]
    [InlineData("Action a = null;\na = () => a();\na();\nreturn x;")]
    [InlineData("void Loop(int n) => Loop(n);\nLoop(x);\nreturn x;")]
    [InlineData("int F(int n) { return new[] { n }.Select(k => F(k)).Sum(); }\nreturn F(x);")]
    public async Task Run_InfiniteRecursion_RuntimeErrorWithExplanation(string playerCode) =>
        await RunRecursive(playerCode);

    [Theory]
    [InlineData("int F(int n)\n{\n    try { return F(n); }\n    catch { return 0; }\n}\nreturn F(x);")]
    [InlineData("int F(int n)\n{\n    try { return F(n) + F(n); }\n    catch { return 0; }\n}\nreturn F(x);")]
    [InlineData("int F(int n)\n{\n    try { return F(n); }\n    finally { }\n}\nreturn F(x);")]
    [InlineData("try { return Twice(x); }\ncatch { return 0; }")]
    [InlineData("int F(int n) { try { return F(n); } catch { return F(n); } }\nreturn F(x);")]
    [InlineData("try { return Twice(x); } catch { return Twice(x); }")]
    [InlineData("int F(int n) { try { return F(n); } finally { F(n); } }\nreturn F(x);")]
    [InlineData("try { return Twice(x); } catch { return x * 2; }")]
    [InlineData("try { return Twice(x); } catch (Exception) { return x * 2; }")]
    [InlineData("try { return Twice(x); } catch (Exception e) when (e.Message.Length > 0) { return x * 2; }")]
    [InlineData("try { return Twice(x); } catch (InsufficientExecutionStackException) { return x * 2; }")]
    [InlineData("try { return Twice(x); } finally { Console.WriteLine(x); }")]
    public async Task Run_RecursionInsideTryCatch_RuntimeErrorAndNothingSwallowsIt(string playerCode) =>
        await RunRecursive(playerCode);

    [Fact]
    public async Task Run_RecursionInsideTryCatch_NextRunStillWorks()
    {
        await RunRecursive("try { return Twice(x); } catch { return Twice(x); }");

        Assert.IsType<Passed>(await Run("return x * 2;"));
    }

    [Theory]
    [InlineData("int r;\ntry { Console.Write(\"\"); }\nfinally { r = x * 2; }\nreturn r;")]
    [InlineData("StringBuilder s;\ntry { Console.Write(\"\"); }\nfinally { s = new StringBuilder(); s.Append(x * 2); }\nreturn int.Parse(s.ToString());")]
    [InlineData("int r;\ntry { }\nfinally { int.TryParse(\"4\", out r); }\nreturn r == 4 ? x * 2 : 0;")]
    [InlineData("int r; int q;\ntry { }\nfinally { int t = 1; r = x * 2; q = t; }\nreturn q == 1 ? r : 0;")]
    [InlineData("int @class;\ntry { }\nfinally { @class = x * 2; }\nreturn @class;")]
    [InlineData("int @var; int await;\ntry { }\nfinally { @var = x; await = x; }\nreturn @var + await;")]
    public async Task Run_VariableAssignedOnlyInFinally_StillPasses(string playerCode) =>
        Assert.IsType<Passed>(await Run(playerCode));

    // Known limit: the finally rewrite makes its last statement conditional, so a finally that
    // always throws no longer ends the method for the compiler.
    [Fact]
    public async Task KnownLimit_FinallyThatAlwaysThrows_FailsToCompileInTheRewrittenTree()
    {
        var result = Assert.IsType<CompilationFailed>(
            await Run("try { Console.Write(\"\"); }\nfinally { throw new Exception(\"boom\"); }"));

        Assert.Contains(result.Errors, error => error.Message.Contains("not all code paths return a value"));
    }

    [Theory]
    [InlineData("int zero = 0;\ntry { return x / zero; }\ncatch (DivideByZeroException) { return x * 2; }")]
    [InlineData("int r = 0;\ntry { }\nfinally { r = x * 2; }\nreturn r;")]
    [InlineData("try { return int.Parse(\"a\"); }\ncatch (FormatException e) when (e.Message.Length > 0) { return x * 2; }")]
    public async Task Run_NormalTryCatchFinally_StillWorks(string playerCode) =>
        Assert.IsType<Passed>(await Run(playerCode));

    [Fact]
    public async Task Run_NaiveFibonacci_Passes() =>
        Assert.IsType<Passed>(await Run(
            "int Fib(int k) { return k < 2 ? k : Fib(k - 1) + Fib(k - 2); }\nreturn Fib(25) == 75025 ? x * 2 : 0;"));

    [Fact]
    public async Task Run_MillionLambdaCalls_Passes() =>
        Assert.IsType<Passed>(await Run(
            "return Enumerable.Range(0, 1_000_000).Select(k => k % 2).Sum() == 500_000 ? x * 2 : 0;"));

    [Fact]
    public async Task Run_StackallocInPlayerCode_CodeRejected()
    {
        var result = Assert.IsType<CodeRejected>(await Run("Span<int> s = stackalloc int[10000000];\nreturn x;"));

        Assert.Equal(1, result.Line);
        Assert.Contains("stackalloc", result.Reason.En);
    }

    [Fact]
    public async Task Run_RecursiveBlockBodiedLocalFunction_ReportsTheRecursiveCallLine()
    {
        var result = await RunRecursive("int F(int n)\n{\n    return F(n);\n}\nreturn F(x);");

        Assert.Equal(3, result.Line);
    }

    [Fact]
    public async Task Run_BoundedRecursion_StillPasses() =>
        Assert.IsType<Passed>(await Run(
            "int Factorial(int k) { return k <= 1 ? 1 : k * Factorial(k - 1); }\nreturn Factorial(10) == 3628800 ? x * 2 : 0;"));

    [Fact]
    public async Task Run_BoundedRecursionWithLambdaAndExpressionBody_StillPasses() =>
        Assert.IsType<Passed>(await Run(
            "Func<int, int> sum = null;\nsum = k => k <= 0 ? 0 : k + sum(k - 1);\nint Count(int k) => k <= 0 ? 0 : 1 + Count(k - 1);\nreturn sum(100) == 5050 && Count(50) == 50 ? x * 2 : 0;"));

    [Fact]
    public async Task Run_AfterInfiniteRecursion_NextRunStillWorks()
    {
        await RunRecursive("return Twice(x);");

        Assert.IsType<Passed>(await Run("return x * 2;"));
    }

    [Fact]
    public async Task Run_TwiceInARow_BothPass()
    {
        Assert.IsType<Passed>(await Run("return x * 2;"));
        Assert.IsType<Passed>(await Run("return x + x;"));
    }

    [Theory]
    [InlineData("async void Boom() { throw new Exception(\"boom\"); }\nBoom();\nreturn x * 2;")]
    [InlineData("Action a = async () => { throw new Exception(\"boom\"); };\na();\nreturn x * 2;")]
    public async Task Run_AsyncInPlayerCode_CodeRejected(string playerCode)
    {
        var result = Assert.IsType<CodeRejected>(await Run(playerCode));

        Assert.Equal(1, result.Line);
        Assert.Contains("async", result.Reason.En);
    }

    [Fact]
    public async Task Run_ProgressCallback_CodeRejected()
    {
        var result = Assert.IsType<CodeRejected>(await Run(
            "IProgress<int> p = new Progress<int>(v => throw new Exception(\"boom\"));\np.Report(1);\nreturn x * 2;"));

        Assert.Equal(1, result.Line);
        Assert.Contains("Progress", result.Reason.En);
    }
}
