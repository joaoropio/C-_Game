using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class MethodErrorExplanationTests
{
    private const string Template = """
        public static class Spell
        {
            // {{PLAYER_CODE}}
        }
        """;

    private const string Heal = "public static int Heal(int amount) => amount;";

    private readonly PlayerCompiler _compiler = new();

    private static string Validation(string call) => $$"""
        public static class Validation
        {
            public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
            {
                yield return ("case", "1", {{call}}.ToString());
            }
        }
        """;

    private IReadOnlyList<CompilationError> Errors(string playerCode, string validationCall = "Spell.Heal(1)", string template = Template, Language language = Language.En) =>
        _compiler.Compile(new RunRequest(template, playerCode, Validation(validationCall)), language).Errors;

    private static CompilationError Single(IReadOnlyList<CompilationError> errors, bool insidePlayerCode)
    {
        var matching = errors.Where(e => (e.Line is not null) == insidePlayerCode).ToList();
        return Assert.Single(matching);
    }

    [Fact]
    public void MissingMethod_CalledByValidation_Explained()
    {
        var error = Single(Errors("public static int Hurt(int amount) => amount;"), insidePlayerCode: false);

        Assert.Equal("The method \"Heal\" is missing.", error.Explanation!.En);
        Assert.Equal("Falta o método \"Heal\".", error.Explanation.Pt);
    }

    [Fact]
    public void MisspeltMethod_CalledByValidation_Explained()
    {
        var error = Single(Errors("public static int Hael(int amount) => amount;"), insidePlayerCode: false);

        Assert.Equal("The method \"Heal\" is missing.", error.Explanation!.En);
    }

    [Fact]
    public void MissingMethod_CalledByTheTemplate_Explained()
    {
        const string template = "public static class Spell\n{\n    // {{PLAYER_CODE}}\n    public static int Cast() => Heal(1);\n}";

        var error = Single(Errors("public static int Hurt(int a) => a;", "Spell.Cast()", template), insidePlayerCode: false);

        Assert.Equal("The method \"Heal\" is missing.", error.Explanation!.En);
    }

    [Fact]
    public void MissingMethod_CalledByThePlayer_ExplainedWithLine()
    {
        var error = Single(
            Errors("public static int Cast() => Heal(1);\npublic static int Heal(int a) => a;\npublic static int Hurt() => Fly(2);"),
            insidePlayerCode: true);

        Assert.Equal(3, error.Line);
        Assert.Equal("The method \"Fly\" is missing.", error.Explanation!.En);
        Assert.Equal("Falta o método \"Fly\".", error.Explanation.Pt);
    }

    [Fact]
    public void MissingMethodOnAnotherType_ExplainedWithLine()
    {
        var error = Single(Errors("public static int Cast(string s) => s.Heal();\n" + Heal), insidePlayerCode: true);

        Assert.Equal(1, error.Line);
        Assert.Equal("The method \"Heal\" is missing.", error.Explanation!.En);
    }

    [Fact]
    public void WrongArgumentCount_CalledByValidation_Explained()
    {
        var error = Single(Errors(Heal, "Spell.Heal(1, 2)"), insidePlayerCode: false);

        Assert.Equal("The method \"Heal\" doesn't take 2 arguments.", error.Explanation!.En);
        Assert.Equal("O método \"Heal\" não recebe 2 argumentos.", error.Explanation.Pt);
    }

    [Fact]
    public void SingleArgument_ReadsInTheSingular()
    {
        var error = Single(Errors("public static int Heal() => 1;"), insidePlayerCode: false);

        Assert.Equal("The method \"Heal\" doesn't take 1 argument.", error.Explanation!.En);
        Assert.Equal("O método \"Heal\" não recebe 1 argumento.", error.Explanation.Pt);
    }

    [Fact]
    public void MissingArguments_CalledByValidation_Explained()
    {
        var error = Single(Errors("public static int Heal(int a, int b) => a + b;"), insidePlayerCode: false);

        Assert.Equal("The call to the method \"Heal\" is missing arguments.", error.Explanation!.En);
        Assert.Equal("Faltam argumentos na chamada ao método \"Heal\".", error.Explanation.Pt);
    }

    [Fact]
    public void WrongArgumentCount_CalledByThePlayer_ExplainedWithLine()
    {
        var error = Single(Errors(Heal + "\npublic static int Twice() => Heal(1, 2, 3);"), insidePlayerCode: true);

        Assert.Equal(2, error.Line);
        Assert.Equal("The method \"Heal\" doesn't take 3 arguments.", error.Explanation!.En);
    }

    [Fact]
    public void DuplicateMethod_Explained()
    {
        var error = Single(Errors(Heal + "\npublic static int Heal(int amount) => 0;"), insidePlayerCode: true);

        Assert.Equal(2, error.Line);
        Assert.Equal("There is already a method \"Heal\" with these same parameters.", error.Explanation!.En);
        Assert.Equal("Já existe um método \"Heal\" com estes mesmos parâmetros.", error.Explanation.Pt);
    }

    [Fact]
    public void DuplicateMethodAgainstTemplate_Explained()
    {
        const string template = "public static class Spell\n{\n    // {{PLAYER_CODE}}\n    public static int Heal(int a) => a;\n}";

        var errors = Errors(Heal, template: template);

        Assert.Contains(errors, e => e.Explanation!.En == "There is already a method \"Heal\" with these same parameters.");
    }

    [Fact]
    public void InstanceMethodInStaticClass_Explained()
    {
        var error = Assert.Single(Errors("public int Heal(int amount) => amount;", "Spell.Heal(1)"), e => e.Line is not null);

        Assert.Equal(1, error.Line);
        Assert.Equal("The method \"Heal\" needs static.", error.Explanation!.En);
        Assert.Equal("Falta static no método \"Heal\".", error.Explanation.Pt);
    }

    [Fact]
    public void InstanceMethodCalledFromStaticMethod_Explained()
    {
        const string template = "public class Spell\n{\n    // {{PLAYER_CODE}}\n}";

        var errors = Errors("int Heal(int a) => a;\npublic static int Cast() => Heal(1);", "Spell.Cast()", template);

        var error = Assert.Single(errors, e => e.Line == 2);
        Assert.Equal("The method \"Heal\" needs static.", error.Explanation!.En);
    }

    [Fact]
    public void Namespace_InPlayerCode_RejectedWithOneClearMessage()
    {
        var outcome = _compiler.Compile(
            new RunRequest(Template, "namespace Foo\n{\n" + Heal + "\n}", Validation("Spell.Heal(1)")),
            Language.En);

        Assert.Equal(1, outcome.Rejected!.Line);
        Assert.Equal("Don't use a namespace: write only the static methods, with nothing around them.", outcome.Rejected.Reason.En);
        Assert.StartsWith("Não uses namespace", outcome.Rejected.Reason.Pt);
    }
}
