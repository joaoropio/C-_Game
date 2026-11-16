using CodeQuest.Core;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class ClearerMessagesTests
{
    private const string Template = """
        public static class Spell
        {
            // {{PLAYER_CODE}}
        }
        """;

    private const string PartialTemplate = """
        public static partial class Spell
        {
            // {{PLAYER_CODE}}
        }
        """;

    private const string MethodTemplate = """
        public static class Spell
        {
            public static int Cast()
            {
                // {{PLAYER_CODE}}
            }
        }
        """;

    private const string Heal = "public static int Heal(int amount) => amount;";

    private const string Validation = """
        public static class Validation
        {
            public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
            {
                yield return ("case", "1", Spell.Heal(1).ToString());
            }
        }
        """;

    private const string NestedValidation = """
        public static partial class Spell
        {
            public static class Validation
            {
                public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
                {
                    yield break;
                }
            }
        }
        """;

    private readonly PlayerCompiler _compiler = new();

    private IReadOnlyList<CompilationError> PlayerErrors(string playerCode) =>
        _compiler.Compile(new RunRequest(Template, playerCode + "\n" + Heal, Validation))
            .Errors.Where(e => e.Line is not null).ToList();

    private CodeRejected Rejected(string template, string playerCode) =>
        _compiler.Compile(new RunRequest(template, playerCode, Validation)).Rejected!;

    [Theory]
    [InlineData("public static int Cast(string s) => s.Length;")]
    [InlineData("public static int Cast(System.Collections.Generic.List<int> l) => l.Count;")]
    [InlineData("public static bool Cast(System.Collections.Generic.Dictionary<string, int> d) => d.ContainsKey(\"x\");")]
    public void BclMembersUsedOnInstances_Compile(string playerCode) =>
        Assert.Empty(PlayerErrors(playerCode));

    [Theory]
    [InlineData("public static int Cast() => string.Length;")]
    [InlineData("public static int Cast() => List<int>.Count;")]
    [InlineData("public static bool Cast() => Dictionary<string, int>.ContainsKey(\"x\");")]
    public void BclInstanceMembersUsedStatically_AreNotToldToBeStatic(string playerCode)
    {
        var error = Assert.Single(PlayerErrors(playerCode));

        Assert.DoesNotContain("static", error.Explanation?.En ?? string.Empty);
        Assert.DoesNotContain("static", error.Explanation?.Pt ?? string.Empty);
    }

    [Fact]
    public void MissingMethodOnAType_NamesTheTypeAndTheMethod()
    {
        var error = Assert.Single(PlayerErrors("public static int Cast() => Math.Mim(1, 2);"));

        Assert.Equal("\"Math\" has no method \"Mim\". Check the name.", error.Explanation!.En);
        Assert.Equal("\"Math\" não tem nenhum método \"Mim\". Verifica o nome.", error.Explanation.Pt);
    }

    [Fact]
    public void MissingMethodOnAString_NamesTheTypeAndTheMethod()
    {
        var error = Assert.Single(PlayerErrors("public static string Cast(string name) => name.ToUpperr();"));

        Assert.Equal("\"string\" has no method \"ToUpperr\". Check the name.", error.Explanation!.En);
    }

    [Fact]
    public void LengthOnAList_SuggestsCount()
    {
        var error = Assert.Single(PlayerErrors(
            "public static int Cast(System.Collections.Generic.List<int> items) => items.Length;"));

        Assert.Equal("On a list use Count, not Length.", error.Explanation!.En);
        Assert.Equal("Numa lista usa Count, não Length.", error.Explanation.Pt);
    }

    [Fact]
    public void LengthOnADictionary_SuggestsCount()
    {
        var error = Assert.Single(PlayerErrors(
            "public static int Cast(System.Collections.Generic.Dictionary<string, int> d) => d.Length;"));

        Assert.Equal("On a list use Count, not Length.", error.Explanation!.En);
    }

    [Theory]
    [InlineData("public static int Cast(int[] items) => items.Count;")]
    [InlineData("public static int Cast(string text) => text.Count;")]
    public void CountOnAnArrayOrString_SuggestsLength(string playerCode)
    {
        var error = Assert.Single(PlayerErrors(playerCode));

        Assert.Equal("On an array (or text) use Length, not Count.", error.Explanation!.En);
        Assert.Equal("Num array (ou texto) usa Length, não Count.", error.Explanation.Pt);
    }

    [Theory]
    [InlineData("Heal(2);", 2)]
    [InlineData("int x = 1;\nx = 2;", 3)]
    [InlineData("return 1;", 2)]
    [InlineData("if (true) { }", 2)]
    [InlineData("Console.WriteLine(1);", 2)]
    public void StatementOutsideAMethod_AtClassScope_TellsToPutItInsideAMethod(string statement, int line)
    {
        var rejected = Rejected(Template, Heal + "\n" + statement);

        Assert.Equal("Only methods go here: put statements inside a method.", rejected.Reason.En);
        Assert.Equal("Aqui só podes escrever métodos: põe as instruções dentro de um método.", rejected.Reason.Pt);
        Assert.Equal(line, rejected.Line);
    }

    [Theory]
    [InlineData("static Heal(int a) { return a; }")]
    [InlineData("Heal(int a) => a;")]
    [InlineData("static Heal() { }")]
    [InlineData("public Heal(int a) { }")]
    public void MethodWithoutReturnType_ExplainsTheMissingReturnType(string method)
    {
        var rejected = Rejected(PartialTemplate, Heal + "\n" + method);

        Assert.Equal("The method \"Heal\" is missing its return type (for example int, or void if it returns nothing).", rejected.Reason.En);
        Assert.Equal("Falta o tipo do resultado do método \"Heal\" (por exemplo int, ou void se não devolver nada).", rejected.Reason.Pt);
        Assert.Equal(2, rejected.Line);
    }

    [Theory]
    [InlineData("static Spell() { }")]
    [InlineData("Spell() { }")]
    public void RealConstructor_KeepsTheConstructorMessage(string constructor)
    {
        var rejected = Rejected(PartialTemplate, Heal + "\n" + constructor);

        Assert.Equal("For now you can only write static methods, not fields, properties or constructors.", rejected.Reason.En);
        Assert.Equal(2, rejected.Line);
    }

    [Fact]
    public void Level7Starter_WithoutReturnType_ExplainsTheMissingReturnType()
    {
        var level = LevelRepository.Load(Path.Combine(AppContext.BaseDirectory, "content", "levels"))
            .Single(l => l.Id == "07-methods");

        var rejected = _compiler.Compile(
            new RunRequest(level.Template, level.StarterCode.Replace("static int Heal", "static Heal"), level.ValidationSource)).Rejected!;

        Assert.Equal("The method \"Heal\" is missing its return type (for example int, or void if it returns nothing).", rejected.Reason.En);
        Assert.Equal(1, rejected.Line);
    }

    [Fact]
    public void StatementsInAMethodBody_AreNotRejected()
    {
        var outcome = _compiler.Compile(
            new RunRequest(MethodTemplate, "Console.WriteLine(1);\nreturn 1;", Validation.Replace("Spell.Heal(1)", "Spell.Cast()")));

        Assert.Null(outcome.Rejected);
    }

    [Fact]
    public void Level8Starter_ExplainsWhichVersionsAreMissing()
    {
        var level = LevelRepository.Load(Path.Combine(AppContext.BaseDirectory, "content", "levels"))
            .Single(l => l.Id == "08-overloads");

        var errors = _compiler.Compile(new RunRequest(level.Template, level.StarterCode, level.ValidationSource)).Errors;

        Assert.Contains(errors, e => e.Explanation?.En == "There's no version of \"Fireball\" that takes string.");
        Assert.Contains(errors, e => e.Explanation?.En == "There's no version of \"Shield\" that takes 2 arguments.");
        Assert.Contains(errors, e => e.Explanation?.Pt == "Falta uma versão de \"Fireball\" que receba string.");
        Assert.Contains(errors, e => e.Explanation?.Pt == "Falta uma versão de \"Shield\" que receba 2 argumentos.");
    }

    [Fact]
    public void WrongArgumentType_CalledByValidation_ExplainedAsMissingOverload()
    {
        var error = Assert.Single(
            _compiler.Compile(new RunRequest(Template, Heal, Validation.Replace("Spell.Heal(1)", "Spell.Heal(\"goblin\")"))).Errors);

        Assert.Equal("There's no version of \"Heal\" that takes string.", error.Explanation!.En);
    }

    [Fact]
    public void PlayerMethodNamedValidation_ExplainedAsReserved()
    {
        var errors = _compiler.Compile(new RunRequest(PartialTemplate, "static int Validation() => 1;", NestedValidation)).Errors;

        var error = Assert.Single(errors, e => e.Line == 1);
        Assert.Equal("The name Validation is reserved; pick another name.", error.Explanation!.En);
        Assert.Equal("O nome Validation está reservado; escolhe outro nome.", error.Explanation.Pt);
    }

    [Fact]
    public void OriginalMessage_IsEnglishWhateverTheLanguage()
    {
        var request = new RunRequest(Template, Heal + "\npublic static int Cast() => nope;", Validation);

        var message = Assert.Single(_compiler.Compile(request).Errors, e => e.Line is not null).Message;

        Assert.Equal("The name 'nope' does not exist in the current context", message);
    }
}
