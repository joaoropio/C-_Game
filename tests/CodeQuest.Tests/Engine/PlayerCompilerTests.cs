using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class PlayerCompilerTests
{
    private const string Template = """
        public static class Spell
        {
            public static int Cast()
            {
                //{{PLAYER_CODE}}
                return 0;
            }
        }
        """;

    private const string Validation = "public static class Checks { }";

    private readonly PlayerCompiler _compiler = new();

    private static RunRequest Request(string playerCode, string template = Template) =>
        new(template, playerCode, Validation);

    [Fact]
    public void Compile_ValidCode_ReturnsAssemblyAndPdb()
    {
        var outcome = _compiler.Compile(Request("int a = 1;"), Language.En);

        Assert.NotNull(outcome.Assembly);
        Assert.NotNull(outcome.Pdb);
        Assert.Empty(outcome.Errors);
        Assert.Equal(4, outcome.PlayerLineOffset);
    }

    [Fact]
    public void Compile_SyntaxErrorOnPlayerLine2_ReportsLine2()
    {
        var outcome = _compiler.Compile(Request("int a = 1;\nint b = ;"), Language.En);

        Assert.Null(outcome.Assembly);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal(2, error.Line);
        Assert.Equal(9, error.Column);
    }

    [Fact]
    public void Compile_ErrorOutsidePlayerCode_ReportsNullLine()
    {
        var outcome = _compiler.Compile(Request("int a = 1;", Template.Replace("return 0;", "return \"x\";")), Language.En);

        var error = Assert.Single(outcome.Errors);
        Assert.Null(error.Line);
        Assert.Null(error.Column);
    }

    [Fact]
    public void Compile_ErrorInValidationSource_ReportsNullLine()
    {
        var request = new RunRequest(Template, "int a = 1;", "public static class Checks { static int X = nope; }");

        var error = Assert.Single(_compiler.Compile(request, Language.En).Errors);

        Assert.Null(error.Line);
    }

    [Fact]
    public void Compile_MissingVariable_ExplainsWhichOne()
    {
        var template = Template.Replace("return 0;", "return health;");

        var error = Assert.Single(_compiler.Compile(Request("int a = 1;", template), Language.Pt).Errors);

        Assert.Contains("health", error.Explanation!.Pt);
        Assert.Contains("health", error.Explanation.En);
    }

    [Fact]
    public void Compile_DoubleAssignedToInt_ExplainsTypeMismatch()
    {
        var error = Assert.Single(_compiler.Compile(Request("int health = 50.5;"), Language.En).Errors);

        foreach (var text in new[] { error.Explanation!.Pt, error.Explanation.En })
        {
            Assert.Contains("health", text);
            Assert.Contains("double", text);
            Assert.Contains("int", text);
        }

        Assert.Contains("is of type double", error.Explanation.En);
    }

    [Fact]
    public void Compile_DoubleAssignedToExistingInt_NamesTheVariable()
    {
        var error = Assert.Single(_compiler.Compile(Request("int health = 1;\nhealth = 50.5;"), Language.En).Errors);

        Assert.Contains("\"health\"", error.Explanation!.En);
    }

    [Fact]
    public void Compile_UnexplainedErrorOutsidePlayerCode_GetsGenericExplanation()
    {
        var template = Template.Replace("return 0;", "return 0; int;");

        var errors = _compiler.Compile(Request("int a = 1;", template), Language.En).Errors;

        Assert.All(errors.Where(error => error.Line is null), error => Assert.NotNull(error.Explanation));
        Assert.Contains(errors, error => error.Explanation!.En.StartsWith("There is an error in the spell"));
    }

    [Fact]
    public void Compile_Portuguese_UsesLocalizedMessage()
    {
        var request = Request("int a = ;");

        var pt = Assert.Single(_compiler.Compile(request, Language.Pt).Errors).Message;
        var en = Assert.Single(_compiler.Compile(request, Language.En).Errors).Message;

        Assert.NotEqual(en, pt);
    }

    [Fact]
    public void Compile_ForbiddenApi_ReturnsRejectionAndNoAssembly()
    {
        var outcome = _compiler.Compile(Request("int a = 1;\nSystem.Environment.Exit(a);"), Language.En);

        Assert.Null(outcome.Assembly);
        Assert.Empty(outcome.Errors);
        Assert.NotNull(outcome.Rejected);
        Assert.Equal(2, outcome.Rejected.Line);
    }

    [Fact]
    public void Compile_InfiniteLoop_ThrowsTimeoutWhenRun()
    {
        var outcome = _compiler.Compile(Request("while (true) { }"), Language.En);

        var cast = System.Reflection.Assembly.Load(outcome.Assembly!).GetType("Spell")!.GetMethod("Cast")!;
        var thrown = Assert.Throws<System.Reflection.TargetInvocationException>(() => cast.Invoke(null, null));
        Assert.IsType<TimeoutException>(thrown.InnerException);
    }

    private static Exception? RunCast(CompileOutcome outcome)
    {
        var cast = System.Reflection.Assembly.Load(outcome.Assembly!).GetType("Spell")!.GetMethod("Cast")!;
        var run = Task.Run(() => cast.Invoke(null, null));
        Assert.True(((IAsyncResult)run).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)), "player code did not stop");
        return Assert.IsType<AggregateException>(Record.Exception(run.Wait)).InnerException?.InnerException;
    }

    [Fact]
    public void Compile_GotoLoop_ThrowsTimeoutWhenRun()
    {
        var outcome = _compiler.Compile(Request("a: goto a;"), Language.En);

        Assert.IsType<TimeoutException>(RunCast(outcome));
    }

    [Fact]
    public void Compile_GotoCaseLoop_ThrowsTimeoutWhenRun()
    {
        var outcome = _compiler.Compile(Request("switch (1) { case 1: goto case 1; }"), Language.En);

        Assert.IsType<TimeoutException>(RunCast(outcome));
    }

    [Fact]
    public void Compile_LabeledDeclaration_StillCompiles()
    {
        var outcome = _compiler.Compile(Request("a: int x = 1;\nreturn x;"), Language.En);

        Assert.Empty(outcome.Errors);
        Assert.NotNull(outcome.Assembly);
    }

    [Fact]
    public void Compile_CatchAllInsideGotoLoop_StillTimesOut()
    {
        var outcome = _compiler.Compile(Request("a: try { while (true) { } } catch { } goto a;"), Language.En);

        Assert.IsType<TimeoutException>(RunCast(outcome));
    }

    [Fact]
    public void Compile_PlayerDeclaredLoopGuardClass_DoesNotShadowTheGuard()
    {
        const string code = """
            while (true) { }
            return 0;
            }
            class __LoopGuard { public static void Tick() { throw new System.InvalidOperationException(); } }
            static int Other()
            {
            """;

        var outcome = _compiler.Compile(Request(code), Language.En);

        Assert.IsType<TimeoutException>(RunCast(outcome));
    }

    [Fact]
    public void Compile_ErrorOnFirstPlayerLine_KeepsLineAndColumn()
    {
        var error = Assert.Single(_compiler.Compile(Request("int b = ;"), Language.En).Errors);

        Assert.Equal(1, error.Line);
        Assert.Equal(9, error.Column);
    }

    [Theory]
    [InlineData("Func<int, int> f = k => k + undefined;\nreturn f(1);", 1, 29)]
    [InlineData("int F(int k) => k + undefined;\nreturn F(1);", 1, 21)]
    [InlineData("int F(int k) { return k + undefined; }\nreturn F(1);", 1, 27)]
    [InlineData("while (true) { int a = undefined; }", 1, 24)]
    [InlineData("int a = 1;\nfor (int i = 0; i < 3; i++) a += undefined;", 2, 34)]
    [InlineData("try { return 1; } catch (Exception e) { int a = undefined; }", 1, 49)]
    public void Compile_ErrorsInsideGuardedConstructs_KeepOriginalLineAndColumn(string playerCode, int line, int column)
    {
        var error = Assert.Single(_compiler.Compile(Request(playerCode), Language.En).Errors);

        Assert.Equal(line, error.Line);
        Assert.Equal(column, error.Column);
    }

    [Theory]
    [InlineData("int F(int n) => F(n);\nreturn F(1);")]
    [InlineData("int F(int n) { return F(n); }\nreturn F(1);")]
    [InlineData("Func<int, int> f = null;\nf = n => f(n);\nreturn f(1);")]
    [InlineData("Func<int, int> f = null;\nf = n => { return f(n); };\nreturn f(1);")]
    public void Compile_RecursionInPlayerFunctions_ThrowsInsufficientStackWhenRun(string playerCode)
    {
        var outcome = _compiler.Compile(Request(playerCode), Language.En);

        Assert.Null(outcome.Rejected);
        Assert.IsType<InsufficientExecutionStackException>(RunCast(outcome));
    }

    [Fact]
    public void Compile_EnclosingMethodRecursion_ThrowsInsufficientStackWhenRun()
    {
        var outcome = _compiler.Compile(Request("return Cast();"), Language.En);

        Assert.IsType<InsufficientExecutionStackException>(RunCast(outcome));
    }

    [Fact]
    public void Compile_RecursionGuardCalls_AreNotRejectedAsForbiddenApis()
    {
        var outcome = _compiler.Compile(
            Request("Func<int, int> f = n => n + 1;\nint G(int n) => n;\nreturn f(G(1));"),
            Language.En);

        Assert.Null(outcome.Rejected);
        Assert.NotNull(outcome.Assembly);
    }

    [Fact]
    public void Compile_ForbiddenApiInsideLambdaBody_StillRejectedOnItsLine()
    {
        var outcome = _compiler.Compile(
            Request("int a = 1;\nFunc<int> f = () => Environment.TickCount;\nreturn f();"),
            Language.En);

        Assert.NotNull(outcome.Rejected);
        Assert.Equal(2, outcome.Rejected.Line);
    }

    [Fact]
    public void Compile_OnlyErrorsAreReported()
    {
        var outcome = _compiler.Compile(Request("int unused = 1;"), Language.En);

        Assert.NotNull(outcome.Assembly);
        Assert.Empty(outcome.Errors);
    }
}

