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
    public void Compile_Portuguese_UsesLocalizedMessage()
    {
        var request = Request("int a = ;");

        var pt = Assert.Single(_compiler.Compile(request, Language.Pt).Errors).Message;
        var en = Assert.Single(_compiler.Compile(request, Language.En).Errors).Message;

        Assert.NotEqual(en, pt);
    }

    [Fact]
    public void Compile_OnlyErrorsAreReported()
    {
        var outcome = _compiler.Compile(Request("int unused = 1;"), Language.En);

        Assert.NotNull(outcome.Assembly);
        Assert.Empty(outcome.Errors);
    }
}

