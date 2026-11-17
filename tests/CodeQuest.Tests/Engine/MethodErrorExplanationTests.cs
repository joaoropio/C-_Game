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

    private IReadOnlyList<CompilationError> Errors(string playerCode, string validationCall = "Spell.Heal(1)", string template = Template) =>
        _compiler.Compile(new RunRequest(template, playerCode, Validation(validationCall))).Errors;

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
        Assert.Equal("\"string\" has no method \"Heal\". Check the name.", error.Explanation!.En);
    }

    [Fact]
    public void WrongArgumentCount_CalledByValidation_Explained()
    {
        var error = Single(Errors(Heal, "Spell.Heal(1, 2)"), insidePlayerCode: false);

        Assert.Equal("There's no version of \"Heal\" that takes 2 arguments.", error.Explanation!.En);
        Assert.Equal("Falta uma versão de \"Heal\" que receba 2 argumentos.", error.Explanation.Pt);
    }

    [Fact]
    public void SingleArgument_ReadsInTheSingular()
    {
        var error = Single(Errors("public static int Heal() => 1;"), insidePlayerCode: false);

        Assert.Equal("There's no version of \"Heal\" that takes 1 argument.", error.Explanation!.En);
        Assert.Equal("Falta uma versão de \"Heal\" que receba 1 argumento.", error.Explanation.Pt);
    }

    [Fact]
    public void MissingArguments_CalledByValidation_Explained()
    {
        var error = Single(Errors("public static int Heal(int a, int b) => a + b;"), insidePlayerCode: false);

        Assert.Equal("Missing arguments in the call to method \"Heal\": no value for \"b\".", error.Explanation!.En);
        Assert.Equal("Falta o valor de \"b\" na chamada ao método \"Heal\".", error.Explanation.Pt);
    }

    [Fact]
    public void WrongArgumentCount_CalledByThePlayer_ExplainedWithLine()
    {
        var error = Single(Errors(Heal + "\npublic static int Twice() => Heal(1, 2, 3);"), insidePlayerCode: true);

        Assert.Equal(2, error.Line);
        Assert.Equal("The method \"Heal\" doesn't take 3 arguments.", error.Explanation!.En);
        Assert.Equal("O método \"Heal\" não recebe 3 argumentos.", error.Explanation.Pt);
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

        Assert.Contains(errors, e => e.Explanation?.En == "There is already a method \"Heal\" with these same parameters.");
    }

    [Fact]
    public void InstanceMethodInStaticClass_ReportedOnlyOnceAtTheDeclaration()
    {
        var error = Assert.Single(Errors("public int Heal(int amount) => amount;", "Spell.Heal(1)"));

        Assert.Equal(1, error.Line);
    }

    [Fact]
    public void InstanceMethodInStaticClass_Explained()
    {
        var error = Assert.Single(Errors("public int Heal(int amount) => amount;", "Spell.Heal(1)"), e => e.Line is not null);

        Assert.Equal(1, error.Line);
        Assert.Equal("The method \"Heal\" needs to be static.", error.Explanation!.En);
        Assert.Equal("Falta static no método \"Heal\".", error.Explanation.Pt);
    }

    [Fact]
    public void InstanceMethodCalledFromStaticMethod_Explained()
    {
        const string template = "public class Spell\n{\n    // {{PLAYER_CODE}}\n}";

        var errors = Errors("int Heal(int a) => a;\npublic static int Cast() => Heal(1);", "Spell.Cast()", template);

        var error = Assert.Single(errors, e => e.Line == 2);
        Assert.Equal("The method \"Heal\" needs to be static.", error.Explanation!.En);
    }

    [Fact]
    public void Namespace_InPlayerCode_RejectedWithOneClearMessage()
    {
        var outcome = _compiler.Compile(
            new RunRequest(Template, "namespace Foo\n{\n" + Heal + "\n}", Validation("Spell.Heal(1)")));

        Assert.Equal(1, outcome.Rejected!.Line);
        Assert.Equal("Don't use a namespace here.", outcome.Rejected.Reason.En);
        Assert.Equal("Não uses namespace aqui.", outcome.Rejected.Reason.Pt);
    }

    [Fact]
    public void FileScopedNamespace_InPlayerCode_Rejected()
    {
        var outcome = _compiler.Compile(
            new RunRequest(Template, "namespace Foo;\n" + Heal, Validation("Spell.Heal(1)")));

        Assert.Equal("Don't use a namespace here.", outcome.Rejected!.Reason.En);
    }

    [Theory]
    [InlineData("public static int Cast(string s) => s.Lenght;")]
    [InlineData("public static double Cast() => Math.Pii;")]
    [InlineData("public static int Cast() => int.MaxValu;")]
    public void MissingNonMethodMember_IsNotCalledAMissingMethod(string playerCode)
    {
        var error = Single(Errors(playerCode + "\n" + Heal), insidePlayerCode: true);

        Assert.Null(error.Explanation);
    }

    [Fact]
    public void MissingMethodCalledOnAnObject_StillExplained()
    {
        var error = Single(Errors("public static int Cast(string s) => s.Lenghtt();\n" + Heal), insidePlayerCode: true);

        Assert.Equal("\"string\" has no method \"Lenghtt\". Check the name.", error.Explanation!.En);
        Assert.Equal("\"string\" não tem nenhum método \"Lenghtt\". Verifica o nome.", error.Explanation.Pt);
    }

    [Fact]
    public void MissingGenericMethod_NameKeepsNoTypeArguments()
    {
        var error = Single(Errors("public static int Cast() => Heal<int>(2);"), insidePlayerCode: true);

        Assert.Equal("The method \"Heal\" is missing.", error.Explanation!.En);
    }

    [Fact]
    public void InstanceMethodCalledByValidation_Explained()
    {
        const string template = "public class Spell\n{\n    // {{PLAYER_CODE}}\n}";

        var error = Single(Errors("public int Heal(int a) => a;", template: template), insidePlayerCode: false);

        Assert.Equal("The method \"Heal\" needs to be static.", error.Explanation!.En);
        Assert.Equal("Falta static no método \"Heal\".", error.Explanation.Pt);
    }

    [Fact]
    public void MissingVariableInTemplate_StillSaysVariable()
    {
        const string template = "public static class Spell\n{\n    // {{PLAYER_CODE}}\n    public static int Cast() => health;\n}";

        var error = Single(Errors(Heal, "Spell.Cast()", template), insidePlayerCode: false);

        Assert.Equal("You still need to declare the variable \"health\".", error.Explanation!.En);
    }
}
