using CodeQuest.Core;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Levels;

public class LevelSolutionTests
{
    private static readonly IReadOnlyList<Level> Levels = LevelRepository.Load(
        Path.Combine(AppContext.BaseDirectory, "content", "levels"));

    private readonly RoslynCodeRunner _runner = new();

    public static TheoryData<string> LevelIds()
    {
        var data = new TheoryData<string>();
        foreach (var level in Levels)
        {
            data.Add(level.Id);
        }

        return data;
    }

    private static Level Find(string id) => Levels.Single(level => level.Id == id);

    private Task<RunResult> Run(Level level, string playerCode) =>
        _runner.RunAsync(
            new RunRequest(level.Template, playerCode, level.ValidationSource),
            Language.En,
            CancellationToken.None);

    [Fact]
    public void RealContent_HasTheSixWorld1LevelsInOrder() =>
        Assert.Equal(
            ["01-variables", "02-operators", "03-conditions", "04-loops", "05-arrays", "06-strings"],
            Levels.Where(level => level.World == 1).Select(level => level.Id));

    [Theory]
    [MemberData(nameof(LevelIds))]
    public async Task Solution_Passes(string id)
    {
        var level = Find(id);

        var result = await Run(level, level.Solution);

        Assert.IsType<Passed>(result);
    }

    [Theory]
    [MemberData(nameof(LevelIds))]
    public async Task Solution_EveryCheckPasses(string id)
    {
        var level = Find(id);

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.NotEmpty(result.Checks);
        Assert.All(result.Checks, check => Assert.True(check.Passed, $"{check.Case}: {check.Actual} vs {check.Expected}"));
    }

    [Theory]
    [MemberData(nameof(LevelIds))]
    public async Task Starter_DoesNotPass(string id)
    {
        var level = Find(id);

        var result = await Run(level, level.StarterCode);

        Assert.IsNotType<Passed>(result);
    }

    [Theory]
    [MemberData(nameof(LevelIds))]
    public async Task Solution_FitsThreeStarLimit(string id)
    {
        var level = Find(id);

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.True(
            result.StatementCount <= level.MaxStatementsForThreeStars,
            $"{result.StatementCount} statements, limit {level.MaxStatementsForThreeStars}");
    }

    [Theory]
    [MemberData(nameof(LevelIds))]
    public void AllTexts_HavePtAndEn(string id)
    {
        var level = Find(id);
        var texts = new[] { level.Title, level.Story, level.Concept }.Concat(level.Hints);

        Assert.All(texts, text =>
        {
            Assert.False(string.IsNullOrWhiteSpace(text.Pt));
            Assert.False(string.IsNullOrWhiteSpace(text.En));
        });
    }

    [Theory]
    [MemberData(nameof(LevelIds))]
    public void EachLevel_HasTwoHints(string id) =>
        Assert.Equal(2, Find(id).Hints.Count);

    [Theory]
    [InlineData("01-variables", "string name = null;\nint health = 100;\ndouble mana = 50.5;\nbool isAlive = true;", "name")]
    [InlineData("03-conditions", "return null;", "rune = \"fire\"")]
    [InlineData("06-strings", "return null;", "name = \"merlin\"")]
    public async Task NullResult_IsShownAsNullNotAsEmptyString(string id, string playerCode, string failingCase)
    {
        var level = Find(id);

        var result = Assert.IsType<ChecksFailed>(await Run(level, playerCode));

        Assert.Equal("null", result.Checks.First(check => check.Case == failingCase).Actual);
    }

    [Fact]
    public async Task Level2_UnqualifiedMathMax_Passed()
    {
        var level = Find("02-operators");

        var result = await Run(level, "return Math.Max(0, attack * multiplier - defense);");

        Assert.IsType<Passed>(result);
    }

    private async Task<LocalizedText> ExplanationFor(string levelId, string playerCode)
    {
        var level = Find(levelId);

        var result = Assert.IsType<CompilationFailed>(await Run(level, playerCode));

        return Assert.Single(result.Errors).Explanation!;
    }

    [Fact]
    public async Task Level1_DoubleHealth_ExplainsTypeMismatch()
    {
        var explanation = await ExplanationFor(
            "01-variables",
            "string name = \"Merlin\";\ndouble health = 100;\ndouble mana = 50.5;\nbool isAlive = true;");

        Assert.Contains("health", explanation.Pt);
        Assert.Contains("double", explanation.Pt);
        Assert.Contains("int", explanation.Pt);
        Assert.Contains("health", explanation.En);
        Assert.Contains("int", explanation.En);
    }

    [Fact]
    public async Task Level1_DecimalHealthLiteral_ExplainsTypeMismatch()
    {
        var explanation = await ExplanationFor(
            "01-variables",
            "string name = \"Merlin\";\nint health = 50.5;\ndouble mana = 50.5;\nbool isAlive = true;");

        Assert.Contains("\"health\"", explanation.Pt);
        Assert.Contains("double", explanation.Pt);
        Assert.Contains("int", explanation.Pt);
        Assert.Contains("\"health\"", explanation.En);
        Assert.Contains("double", explanation.En);
        Assert.Contains("int", explanation.En);
    }

    [Fact]
    public async Task Level1_UnassignedHealth_ExplainsUnassignedVariable()
    {
        var explanation = await ExplanationFor(
            "01-variables",
            "string name = \"Merlin\";\nint health;\ndouble mana = 50.5;\nbool isAlive = true;");

        Assert.Contains("health", explanation.Pt);
        Assert.Contains("valor", explanation.Pt);
        Assert.Contains("a value", explanation.En);
    }

    [Fact]
    public async Task Level2_InfiniteRecursion_RuntimeErrorAndServerSurvives()
    {
        var level = Find("02-operators");

        var result = Assert.IsType<RuntimeError>(await Run(level, "return Damage(attack, defense, multiplier);"));

        Assert.Equal("The spell keeps calling itself and never stops (infinite recursion).", result.Explanation.En);
        Assert.Equal("O feitiço chama-se a si próprio sem parar (recursão infinita).", result.Explanation.Pt);
        Assert.Equal(1, result.Line);
        Assert.IsType<Passed>(await Run(level, level.Solution));
    }

    [Fact]
    public async Task Level2_MissingReturn_ExplainsReturn()
    {
        var explanation = await ExplanationFor("02-operators", "int x = attack;");

        Assert.Contains("return", explanation.Pt);
        Assert.Contains("return", explanation.En);
    }

    [Fact]
    public async Task Level3_SwitchWithoutDefault_ExplainsReturn()
    {
        var explanation = await ExplanationFor(
            "03-conditions",
            "switch (rune)\n{\n    case \"fire\":\n        return \"left\";\n}");

        Assert.Contains("return", explanation.Pt);
        Assert.Contains("return", explanation.En);
    }

    [Fact]
    public async Task Level4_OffByOne_ExplainsExpectedAndActual()
    {
        var level = Find("04-loops");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "int mana = 0;\nfor (int turn = 1; turn < turns; turn++)\n{\n    mana += turn;\n}\nreturn mana;"));

        Assert.Contains(new CheckOutcome("turns = 3", "6", "3"), result.Checks);
    }

    [Fact]
    public async Task Level4_InfiniteLoop_TimedOut()
    {
        var level = Find("04-loops");

        var result = await Run(level, "while (true) { }");

        Assert.IsType<TimedOut>(result);
    }

    [Fact]
    public async Task Level5_EmptyShelf_IsChecked()
    {
        var level = Find("05-arrays");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "if (potions.Length == 0)\n{\n    return 1;\n}\n\nint total = 0;\nforeach (int count in potions)\n{\n    total += count;\n}\n\nreturn total;"));

        Assert.Contains(new CheckOutcome("potions = { }", "0", "1"), result.Checks);
    }

    [Fact]
    public async Task Level5_IndexPastEnd_ExplainsPosition()
    {
        var level = Find("05-arrays");

        var result = Assert.IsType<RuntimeError>(await Run(level, "return potions[potions.Length];"));

        Assert.Contains("position that doesn't exist", result.Explanation.En);
        Assert.Contains("posição que não existe", result.Explanation.Pt);
    }

    [Fact]
    public async Task Level6_CharPlusInt_ExplainsWhatWentWrong()
    {
        var level = Find("06-strings");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "return (char.ToUpper(name[0]) + name.Length).ToString();"));

        Assert.Contains(new CheckOutcome("name = \"merlin\"", "\"M6\"", "\"83\""), result.Checks);
    }

    [Fact]
    public async Task Level6_CharPlusInt_WithoutToString_ExplainsTypeMismatch()
    {
        var explanation = await ExplanationFor("06-strings", "return char.ToUpper(name[0]) + name.Length;");

        Assert.Contains("int", explanation.Pt);
        Assert.Contains("string", explanation.Pt);
        Assert.Contains("int", explanation.En);
        Assert.Contains("string", explanation.En);
    }

    [Fact]
    public async Task Level6_ThreeStatementSolution_FitsThreeStarLimit()
    {
        var level = Find("06-strings");

        var result = Assert.IsType<Passed>(await Run(
            level,
            "char first = char.ToUpper(name[0]);\nint letters = name.Length;\nreturn $\"{first}{letters}\";"));

        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level6_Interpolation_Passes()
    {
        var level = Find("06-strings");

        var result = await Run(level, "return $\"{char.ToUpper(name[0])}{name.Length}\";");

        Assert.IsType<Passed>(result);
    }

    [Fact]
    public async Task Level6_CharToUpperWithoutArguments_FailsToCompile()
    {
        var level = Find("06-strings");

        var result = await Run(level, "return $\"{name[0].ToUpper()}{name.Length}\";");

        Assert.IsType<CompilationFailed>(result);
    }

    [Fact]
    public async Task Level2_WrongDamage_ReportsExpectedAndObtained()
    {
        var level = Find("02-operators");

        var result = Assert.IsType<ChecksFailed>(await Run(level, "return attack + multiplier - defense;"));

        Assert.Contains(new CheckOutcome("attack = 10, defense = 3, multiplier = 2", "17", "9"), result.Checks);
    }

    [Fact]
    public async Task Level3_WrongDoor_ReportsExpectedAndObtained()
    {
        var level = Find("03-conditions");

        var result = Assert.IsType<ChecksFailed>(await Run(level, "return \"center\";"));

        Assert.Contains(new CheckOutcome("rune = \"fire\"", "\"left\"", "\"center\""), result.Checks);
    }

    [Fact]
    public async Task Level1_WrongHealth_ReportsExpectedAndObtained()
    {
        var level = Find("01-variables");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "string name = \"Merlin\";\nint health = 90;\ndouble mana = 50.5;\nbool isAlive = true;"));

        Assert.Equal(new CheckOutcome("health", "100", "90"), Assert.Single(result.Checks, check => !check.Passed));
        Assert.Equal(["name", "health", "mana", "isAlive"], result.Checks.Select(check => check.Case));
    }

    [Fact]
    public async Task Level1_MissingVariable_ExplainsWhichOne()
    {
        var level = Find("01-variables");

        var result = Assert.IsType<CompilationFailed>(await Run(level, level.StarterCode));

        Assert.Contains(result.Errors, error => error.Explanation?.En.Contains("health") == true);
    }

    [Fact]
    public async Task Level7_WrongCap_ReportsExpectedAndObtained()
    {
        var level = Find("07-methods");

        var result = Assert.IsType<ChecksFailed>(await Run(level, "static int Heal(int health, int potion) => health + potion;"));

        Assert.Contains(new CheckOutcome("Heal(90, 30)", "100", "120"), result.Checks);
    }

    [Fact]
    public async Task Level7_MissingStatic_Explains()
    {
        var level = Find("07-methods");

        var result = Assert.IsType<CompilationFailed>(await Run(level, "int Heal(int health, int potion) => Math.Min(health + potion, 100);"));

        Assert.Equal("The method \"Heal\" needs to be static.", Assert.Single(result.Errors).Explanation!.En);
    }

    [Fact]
    public async Task Level7_WrongName_ExplainsMissingMethod()
    {
        var level = Find("07-methods");

        var result = Assert.IsType<CompilationFailed>(await Run(level, "static int Cure(int health, int potion) => Math.Min(health + potion, 100);"));

        Assert.Equal("The method \"Heal\" is missing.", Assert.Single(result.Errors).Explanation!.En);
    }

    [Fact]
    public async Task Level7_TwoStatementSolution_Passes()
    {
        var level = Find("07-methods");

        var result = Assert.IsType<Passed>(await Run(
            level,
            "static int Heal(int health, int potion)\n{\n    int total = health + potion;\n    return Math.Min(total, 100);\n}"));

        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level8_Solution_RunsTheFourOverloadChecksWithinTheStarLimit()
    {
        var level = Find("08-overloads");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(["Fireball(5)", "Fireball(5, 3)", "Shield(4)", "Shield(4, 3)"], result.Checks.Select(check => check.Case));
        Assert.Equal(3, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level8_WrongTargets_ReportsExpectedAndObtained()
    {
        var level = Find("08-overloads");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "static int Fireball(int power) => power * 2;\nstatic int Fireball(int power, int targets) => power * targets;\nstatic int Shield(int strength, int turns = 1) => strength * turns;"));

        Assert.Contains(new CheckOutcome("Fireball(5, 3)", "30", "15"), result.Checks);
    }

    [Fact]
    public async Task Level8_SameSignatureTwice_Explains()
    {
        var level = Find("08-overloads");

        var result = Assert.IsType<CompilationFailed>(await Run(
            level,
            "static int Fireball(int power) => power * 2;\nstatic int Fireball(int targets) => targets * 2;\nstatic int Shield(int strength, int turns = 1) => strength * turns;"));

        Assert.Equal("There is already a method \"Fireball\" with these same parameters.", result.Errors[0].Explanation!.En);
    }

    [Fact]
    public async Task Level8_MissingOptional_ExplainsArguments()
    {
        var level = Find("08-overloads");

        var result = Assert.IsType<CompilationFailed>(await Run(
            level,
            "static int Fireball(int power) => power * 2;\nstatic int Fireball(int power, int targets) => power * 2 * targets;\nstatic int Shield(int strength, int turns) => strength * turns;"));

        Assert.Equal("Missing arguments in the call to method \"Shield\": no value for \"turns\".", result.Errors[0].Explanation!.En);
    }
}
