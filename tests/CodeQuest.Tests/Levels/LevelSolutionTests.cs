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

    private const string Level8Shield = "static int Shield(int strength, int turns = 1) => strength * turns;";

    [Fact]
    public async Task Level8_Solution_RunsTheFiveOverloadChecksWithinTheStarLimit()
    {
        var level = Find("08-overloads");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(["Fireball(5)", "Fireball(2)", "Fireball(\"goblin\")", "Shield(4)", "Shield(4, 3)"], result.Checks.Select(check => check.Case));
        Assert.Equal(3, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level8_WrongText_ReportsExpectedAndObtained()
    {
        var level = Find("08-overloads");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "static int Fireball(int power) => power * 2;\nstatic string Fireball(string target) => \"Fireball \" + target;\n" + Level8Shield));

        Assert.Contains(new CheckOutcome("Fireball(\"goblin\")", "\"Fireball at goblin\"", "\"Fireball goblin\""), result.Checks);
    }

    [Theory]
    [InlineData("static int Fireball(int power, int targets = 1) => power * 2 * targets;\n" + Level8Shield)]
    [InlineData("static int Fireball(int power) => power * 2;\n" + Level8Shield)]
    public async Task Level8_WithoutTheTextOverload_DoesNotPass(string playerCode)
    {
        var level = Find("08-overloads");

        var result = await Run(level, playerCode);

        Assert.IsType<CompilationFailed>(result);
    }

    [Fact]
    public async Task Level8_SameSignatureTwice_Explains()
    {
        var level = Find("08-overloads");

        var result = Assert.IsType<CompilationFailed>(await Run(
            level,
            "static int Fireball(int power) => power * 2;\nstatic int Fireball(int energy) => energy * 2;\nstatic string Fireball(string target) => \"Fireball at \" + target;\n" + Level8Shield));

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.Line);
        Assert.Equal("There is already a method \"Fireball\" with these same parameters.", error.Explanation!.En);
    }

    [Fact]
    public async Task Level8_MissingOptional_ExplainsArguments()
    {
        var level = Find("08-overloads");

        var result = Assert.IsType<CompilationFailed>(await Run(
            level,
            "static int Fireball(int power) => power * 2;\nstatic string Fireball(string target) => \"Fireball at \" + target;\nstatic int Shield(int strength, int turns) => strength * turns;"));

        Assert.Equal("Missing arguments in the call to method \"Shield\": no value for \"turns\".", result.Errors[0].Explanation!.En);
    }

    private const string SplitManaThatThrows =
        "static int SplitMana(int mana, int wizards)\n{\n    if (wizards <= 0)\n    {\n        throw new ArgumentException(\"no wizards\");\n    }\n\n    return mana / wizards;\n}\n";

    private const string SafeSplitWithRealCatch =
        "static int SafeSplit(int mana, int wizards)\n{\n    try { return SplitMana(mana, wizards); }\n    catch (ArgumentException) { return 0; }\n}";

    [Fact]
    public async Task Level9_Solution_RunsTheFiveExceptionChecksWithinTheStarLimit()
    {
        var level = Find("09-exceptions");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(
            ["SplitMana(10, 2)", "SplitMana(10, 0)", "SplitMana(10, -1)", "SafeSplit(10, 0)", "SafeSplit(9, 3)"],
            result.Checks.Select(check => check.Case));
        Assert.Equal(
            ["returns 5", "throws ArgumentException", "throws ArgumentException", "catches ArgumentException, returns 0", "returns 3"],
            result.Checks.Select(check => check.Expected));
        Assert.Equal(6, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level9_CompactThrowExpressionSolution_FitsTheStarLimit()
    {
        var level = Find("09-exceptions");

        var result = Assert.IsType<Passed>(await Run(
            level,
            "static int SplitMana(int mana, int wizards) => wizards <= 0 ? throw new ArgumentException(\"no wizards\") : mana / wizards;\n" + SafeSplitWithRealCatch));

        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level9_NoGuardClause_ReportsWhichExceptionWasThrownInstead()
    {
        var level = Find("09-exceptions");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "static int SplitMana(int mana, int wizards) => mana / wizards;\nstatic int SafeSplit(int mana, int wizards) => 0;"));

        Assert.Contains(new CheckOutcome("SplitMana(10, 0)", "throws ArgumentException", "throws DivideByZeroException"), result.Checks);
        Assert.Contains(new CheckOutcome("SplitMana(10, -1)", "throws ArgumentException", "returns -10"), result.Checks);
    }

    [Theory]
    [InlineData("static int SafeSplit(int mana, int wizards)\n{\n    if (wizards <= 0)\n    {\n        return 0;\n    }\n\n    return SplitMana(mana, wizards);\n}")]
    [InlineData("static int SafeSplit(int mana, int wizards) => wizards <= 0 ? 0 : SplitMana(mana, wizards);")]
    public async Task Level9_IfInsteadOfCatch_IsRejected(string safeSplit)
    {
        var level = Find("09-exceptions");

        var result = Assert.IsType<ChecksFailed>(await Run(level, SplitManaThatThrows + safeSplit));

        Assert.Contains(
            new CheckOutcome("SafeSplit(10, 0)", "catches ArgumentException, returns 0", "returns 0 without catching anything"),
            result.Checks);
    }

    [Fact]
    public async Task Level9_CatchingTheWrongType_ReportsTheExceptionThatEscaped()
    {
        var level = Find("09-exceptions");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            SplitManaThatThrows +
            "static int SafeSplit(int mana, int wizards)\n{\n    try { return SplitMana(mana, wizards); }\n    catch (DivideByZeroException) { return 0; }\n}"));

        Assert.Contains(
            new CheckOutcome("SafeSplit(10, 0)", "catches ArgumentException, returns 0", "throws ArgumentException"),
            result.Checks);
    }

    [Fact]
    public async Task Level9_UncaughtException_IsReportedPerCaseNotACrash()
    {
        var level = Find("09-exceptions");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            SplitManaThatThrows + "static int SafeSplit(int mana, int wizards) => SplitMana(mana, wizards);"));

        Assert.Contains(
            new CheckOutcome("SafeSplit(10, 0)", "catches ArgumentException, returns 0", "throws ArgumentException"),
            result.Checks);
        Assert.Contains(new CheckOutcome("SafeSplit(9, 3)", "returns 3", "returns 3"), result.Checks);
    }

    [Fact]
    public async Task Level9_ConsecutiveRuns_EachWatchTheirOwnCatch()
    {
        var level = Find("09-exceptions");
        var ifVersion = SplitManaThatThrows + "static int SafeSplit(int mana, int wizards) => wizards <= 0 ? 0 : SplitMana(mana, wizards);";

        Assert.IsType<Passed>(await Run(level, level.Solution));
        Assert.IsType<ChecksFailed>(await Run(level, ifVersion));
        Assert.IsType<Passed>(await Run(level, level.Solution));
        Assert.IsType<ChecksFailed>(await Run(level, ifVersion));
    }

    [Fact]
    public async Task Level9_GuardTripInsideAChecked_StaysARuntimeErrorBecauseOfTheValidationFilter()
    {
        var level = Find("09-exceptions");

        var result = Assert.IsType<RuntimeError>(await Run(
            level,
            "static int SplitMana(int mana, int wizards) => SplitMana(mana, wizards);\n" + SafeSplitWithRealCatch));

        Assert.Equal("The spell keeps calling itself and never stops (infinite recursion).", result.Explanation.En);
    }

    [Fact]
    public async Task Level9_InfiniteLoopInCheckedSpell_IsTimedOutNotAThrowsText()
    {
        var level = Find("09-exceptions");

        Assert.IsType<TimedOut>(await Run(
            level,
            "static int SplitMana(int mana, int wizards)\n{\n    while (true) { }\n}\nstatic int SafeSplit(int mana, int wizards) => 0;"));
    }

    [Fact]
    public async Task Level10_Solution_RunsTheFourListChecksWithinTheStarLimit()
    {
        var level = Find("10-lists");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(
            ["PackBag({ \"ruby\", \"stone\", \"key\" })", "PackBag({ \"stone\" })", "PackBag({ })", "PackBag({ \"key\", \"key\" })"],
            result.Checks.Select(check => check.Case));
        Assert.Equal(["{ \"ruby\", \"key\" }", "{ }", "{ }", "{ \"key\", \"key\" }"], result.Checks.Select(check => check.Expected));
        Assert.Equal(5, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level10_ReturnsNull_ShowsNull()
    {
        var level = Find("10-lists");

        var result = Assert.IsType<ChecksFailed>(await Run(level, "static List<string> PackBag(string[] loot)\n{\n    return null;\n}"));

        Assert.Equal("null", result.Checks.First().Actual);
    }

    [Fact]
    public async Task Level10_KeepsEverything_ReportsExpectedAndObtained()
    {
        var level = Find("10-lists");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "static List<string> PackBag(string[] loot)\n{\n    return new List<string>(loot);\n}"));

        Assert.Contains(new CheckOutcome("PackBag({ \"ruby\", \"stone\", \"key\" })", "{ \"ruby\", \"key\" }", "{ \"ruby\", \"stone\", \"key\" }"), result.Checks);
        Assert.Contains(new CheckOutcome("PackBag({ })", "{ }", "{ }"), result.Checks);
    }

    [Fact]
    public async Task Level10_ReversedOrder_DoesNotPass()
    {
        var level = Find("10-lists");

        Assert.IsType<ChecksFailed>(await Run(
            level,
            "static List<string> PackBag(string[] loot)\n{\n    var bag = new List<string>();\n    foreach (var item in loot)\n    {\n        if (item != \"stone\")\n        {\n            bag.Insert(0, item);\n        }\n    }\n\n    return bag;\n}"));
    }

    [Fact]
    public async Task Level10_LinqOneLiner_Passes()
    {
        var level = Find("10-lists");

        var result = Assert.IsType<Passed>(await Run(
            level,
            "static List<string> PackBag(string[] loot)\n{\n    return loot.Where(item => item != \"stone\").ToList();\n}"));

        Assert.Equal(1, result.StatementCount);
    }

    [Theory]
    [InlineData("static string[] PackBag(string[] loot) => Array.FindAll(loot, item => item != \"stone\");")]
    [InlineData("static IEnumerable<string> PackBag(string[] loot)\n{\n    foreach (var item in loot)\n    {\n        if (item != \"stone\")\n        {\n            yield return item;\n        }\n    }\n}")]
    public async Task Level10_ArrayInsteadOfList_IsRejected(string playerCode)
    {
        var level = Find("10-lists");

        var result = Assert.IsType<ChecksFailed>(await Run(level, playerCode));

        Assert.All(result.Checks, check => Assert.Equal("not a List<string>", check.Actual));
    }

    [Fact]
    public async Task Level10_NullItem_IsShownAsNull()
    {
        var level = Find("10-lists");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "static List<string> PackBag(string[] loot)\n{\n    return new List<string> { null };\n}"));

        Assert.Equal("{ null }", result.Checks.First().Actual);
    }

    [Fact]
    public async Task Level11_Solution_RunsTheFourDictionaryChecksWithinTheStarLimit()
    {
        var level = Find("11-dictionaries");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(
            ["CountPotions({ \"heal\", \"mana\", \"heal\" })", "CountPotions({ })", "CountPotions({ \"fire\" })", "CountPotions({ \"mana\", \"fire\", \"mana\", \"mana\" })"],
            result.Checks.Select(check => check.Case));
        Assert.Equal(["{ [\"heal\"] = 2, [\"mana\"] = 1 }", "{ }", "{ [\"fire\"] = 1 }", "{ [\"fire\"] = 1, [\"mana\"] = 3 }"], result.Checks.Select(check => check.Expected));
        Assert.Equal(6, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level11_ReturnsNull_ShowsNull()
    {
        var level = Find("11-dictionaries");

        var result = Assert.IsType<ChecksFailed>(await Run(level, "static Dictionary<string, int> CountPotions(string[] potions)\n{\n    return null;\n}"));

        Assert.Equal("null", result.Checks.First().Actual);
    }

    [Fact]
    public async Task Level11_CountsEverythingAsOne_ReportsExpectedAndObtained()
    {
        var level = Find("11-dictionaries");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "static Dictionary<string, int> CountPotions(string[] potions)\n{\n    var counts = new Dictionary<string, int>();\n    foreach (var potion in potions)\n    {\n        counts[potion] = 1;\n    }\n\n    return counts;\n}"));

        Assert.Contains(new CheckOutcome("CountPotions({ \"heal\", \"mana\", \"heal\" })", "{ [\"heal\"] = 2, [\"mana\"] = 1 }", "{ [\"heal\"] = 1, [\"mana\"] = 1 }"), result.Checks);
        Assert.Contains(new CheckOutcome("CountPotions({ })", "{ }", "{ }"), result.Checks);
    }

    [Fact]
    public async Task Level11_LinqOneLiner_Passes()
    {
        var level = Find("11-dictionaries");

        var result = Assert.IsType<Passed>(await Run(
            level,
            "static Dictionary<string, int> CountPotions(string[] potions)\n{\n    return potions.GroupBy(potion => potion).ToDictionary(group => group.Key, group => group.Count());\n}"));

        Assert.Equal(1, result.StatementCount);
    }

    [Fact]
    public async Task Level11_TryGetValueSolution_Passes()
    {
        var level = Find("11-dictionaries");

        var result = Assert.IsType<Passed>(await Run(
            level,
            "static Dictionary<string, int> CountPotions(string[] potions)\n{\n    var counts = new Dictionary<string, int>();\n    foreach (var potion in potions)\n    {\n        counts.TryGetValue(potion, out var seen);\n        counts[potion] = seen + 1;\n    }\n\n    return counts;\n}"));

        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Theory]
    [InlineData("static SortedDictionary<string, int> CountPotions(string[] potions)\n{\n    var counts = new SortedDictionary<string, int>();\n    foreach (var potion in potions)\n    {\n        counts[potion] = counts.GetValueOrDefault(potion) + 1;\n    }\n\n    return counts;\n}")]
    [InlineData("static List<KeyValuePair<string, int>> CountPotions(string[] potions)\n{\n    return potions.GroupBy(potion => potion).Select(group => new KeyValuePair<string, int>(group.Key, group.Count())).ToList();\n}")]
    [InlineData("static IEnumerable<IGrouping<string, string>> CountPotions(string[] potions)\n{\n    return potions.GroupBy(potion => potion);\n}")]
    public async Task Level11_NotADictionary_IsRejected(string playerCode)
    {
        var level = Find("11-dictionaries");

        var result = Assert.IsType<ChecksFailed>(await Run(level, playerCode));

        Assert.All(result.Checks, check => Assert.Equal("not a Dictionary<string, int>", check.Actual));
    }

    [Fact]
    public async Task Level11_KeyNotFound_Explains()
    {
        var level = Find("11-dictionaries");

        var result = Assert.IsType<RuntimeError>(await Run(
            level,
            "static Dictionary<string, int> CountPotions(string[] potions)\n{\n    var counts = new Dictionary<string, int>();\n    foreach (var potion in potions)\n    {\n        counts[potion]++;\n    }\n\n    return counts;\n}"));

        Assert.Equal("You asked for a key the dictionary does not have. Check with ContainsKey or TryGetValue before reading it.", result.Explanation.En);
        Assert.Equal(6, result.Line);
    }

    private const string StaticHealthSlime = "class Slime\n{\n    public string Name;\n    public static int Health;\n\n    public void Hit(int damage)\n    {\n        Health -= damage;\n        if (Health < 0)\n        {\n            Health = 0;\n        }\n    }\n\n    public bool IsAlive()\n    {\n        return Health > 0;\n    }\n}";

    [Fact]
    public async Task Level12_Solution_RunsTheTenSlimeChecksWithinTheStarLimit()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(
            [
                "typeof(Slime)",
                "new Slime { Health = 10 }; Hit(3); Health",
                "new Slime { Health = 10 }; Hit(20); Health",
                "new Slime { Health = 6 }; Hit(2); Health",
                "new Slime { Health = 10 }; Hit(3); Hit(3); Health",
                "new Slime { Health = 0 }.IsAlive()",
                "new Slime { Health = 4 }.IsAlive()",
                "a, b = new Slime { Health = 10 }; a.Hit(5); a.Health",
                "a, b = new Slime { Health = 10 }; a.Hit(5); b.Health",
                "a = new Slime { Name = \"Blob\" }, b = new Slime { Name = \"Goo\" }; a.Name",
            ],
            result.Checks.Select(check => check.Case));
        Assert.Equal(["class", "7", "0", "4", "4", "false", "true", "5", "10", "\"Blob\""], result.Checks.Select(check => check.Expected));
        Assert.Equal(4, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level12_MathMaxSolution_Passes()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<Passed>(await Run(
            level,
            "class Slime\n{\n    public string Name;\n    public int Health;\n\n    public void Hit(int damage)\n    {\n        Health = Math.Max(0, Health - damage);\n    }\n\n    public bool IsAlive() => Health > 0;\n}"));

        Assert.Equal(2, result.StatementCount);
    }

    [Fact]
    public async Task Level12_HitWithoutClamp_ReportsExpectedAndObtained()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "class Slime\n{\n    public string Name;\n    public int Health;\n\n    public void Hit(int damage)\n    {\n        Health -= damage;\n    }\n\n    public bool IsAlive()\n    {\n        return Health > 0;\n    }\n}"));

        Assert.Contains(new CheckOutcome("new Slime { Health = 10 }; Hit(20); Health", "0", "-10"), result.Checks);
    }

    [Fact]
    public async Task Level12_StaticHealth_IsRejected()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<ChecksFailed>(await Run(level, StaticHealthSlime));

        Assert.Contains(new CheckOutcome("a, b = new Slime { Health = 10 }; a.Hit(5); b.Health", "10", "5"), result.Checks);
    }

    [Fact]
    public async Task Level12_IsAliveAlwaysTrue_IsRejected()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "class Slime\n{\n    public string Name;\n    public int Health;\n\n    public void Hit(int damage)\n    {\n        Health = Math.Max(0, Health - damage);\n    }\n\n    public bool IsAlive()\n    {\n        return true;\n    }\n}"));

        Assert.Contains(new CheckOutcome("new Slime { Health = 0 }.IsAlive()", "false", "true"), result.Checks);
    }

    [Fact]
    public async Task Level12_PrivateFields_AreReportedAsNotPublic()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            "class Slime\n{\n    string Name;\n    int Health;\n\n    public void Hit(int damage)\n    {\n        Health = Math.Max(0, Health - damage);\n    }\n\n    public bool IsAlive()\n    {\n        return Health > 0;\n    }\n}"));

        Assert.Contains(result.Checks, check => check.Actual == "no public field Health");
    }

    [Fact]
    public async Task Level12_MissingClass_Explains()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<CompilationFailed>(await Run(level, "class Blob\n{\n    public int Health;\n}"));

        Assert.All(result.Errors, error =>
        {
            Assert.Equal("The class \"Slime\" is missing.", error.Explanation!.En);
            Assert.Equal("Falta a classe \"Slime\".", error.Explanation.Pt);
        });
    }

    [Theory]
    [InlineData("Health = Math.Max(0, 10 - damage);")]
    [InlineData("Health = damage == 3 ? 7 : damage == 20 ? 0 : 10;")]
    public async Task Level12_IgnoresCurrentHealth_IsRejected(string hitBody)
    {
        var level = Find("12-classes");

        var result = Assert.IsType<ChecksFailed>(await Run(
            level,
            $"class Slime\n{{\n    public string Name;\n    public int Health;\n\n    public void Hit(int damage)\n    {{\n        {hitBody}\n    }}\n\n    public bool IsAlive()\n    {{\n        return Health > 0;\n    }}\n}}"));

        Assert.Contains(result.Checks, check => check.Case == "new Slime { Health = 6 }; Hit(2); Health" && !check.Passed);
        Assert.Contains(result.Checks, check => check.Case == "new Slime { Health = 10 }; Hit(3); Hit(3); Health" && !check.Passed);
    }

    [Fact]
    public async Task Level12_StaticName_IsRejected()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<ChecksFailed>(await Run(level, level.Solution.Replace("public string Name;", "public static string Name;")));

        Assert.Contains(new CheckOutcome("a = new Slime { Name = \"Blob\" }, b = new Slime { Name = \"Goo\" }; a.Name", "\"Blob\"", "\"Goo\""), result.Checks);
    }

    [Fact]
    public async Task Level12_StructSlime_IsToldItMustBeAClass()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<ChecksFailed>(await Run(level, level.Solution.Replace("class Slime", "struct Slime")));

        Assert.Contains(new CheckOutcome("typeof(Slime)", "class", "struct"), result.Checks);
    }

    [Fact]
    public async Task Level12_StaticMethods_Explains()
    {
        var level = Find("12-classes");

        var result = Assert.IsType<CompilationFailed>(await Run(level, level.StarterCode.Replace("public void Hit", "public static void Hit")));

        Assert.Single(result.Errors);
        Assert.All(result.Errors, error => Assert.Equal("\"Hit\" é static: pertence à classe e não a cada objeto. Tira o static.", error.Explanation!.Pt));
        Assert.All(result.Errors, error => Assert.Null(error.Line));
    }

    private const string GolemName = "public string Name { get; }";
    private const string GolemArmor = "public int Armor { get; private set; }";
    private const string GolemConstructorBody = "Name = name;\n        Armor = armor < 0 ? 0 : armor;";
    private const string GolemCrackBody = "if (Armor > 0)\n        {\n            Armor--;\n        }";

    private static string Golem(
        string name = GolemName,
        string armor = GolemArmor,
        string constructorBody = GolemConstructorBody,
        string crackBody = GolemCrackBody,
        string crackModifier = "public",
        string constructorModifier = "public") =>
        $"class Golem\n{{\n    {name}\n    {armor}\n\n    {constructorModifier} Golem(string name, int armor)\n    {{\n        {constructorBody}\n    }}\n\n    {crackModifier} void Crack()\n    {{\n        {crackBody}\n    }}\n}}";

    [Fact]
    public async Task Level13_Solution_RunsTheSixteenGolemChecksWithinTheStarLimit()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(
            [
                "typeof(Golem)",
                "new Golem(\"Rocky\", 3); Armor",
                "new Golem(\"Pebble\", 5); Armor",
                "new Golem(\"Pebble\", -2); Armor",
                "new Golem(\"Rocky\", 3); Name",
                "new Golem(\"Pebble\", 5); Name",
                "new Golem(\"Rocky\", 3); Crack(); Armor",
                "new Golem(\"Pebble\", 5); Crack(); Crack(); Armor",
                "new Golem(\"Rocky\", 3); Crack() x4; Armor",
                "new Golem(\"Chip\", 0); Crack(); Armor",
                "a = new Golem(\"A\", 4), b = new Golem(\"B\", 4); a.Crack(); a.Armor",
                "a = new Golem(\"A\", 4), b = new Golem(\"B\", 4); a.Crack(); b.Armor",
                "a = new Golem(\"A\", 4), b = new Golem(\"B\", 4); a.Crack(); a.Name",
                "Armor setter",
                "Name setter",
                "public fields",
            ],
            result.Checks.Select(check => check.Case));
        Assert.Equal(["class", "3", "5", "0", "\"Rocky\"", "\"Pebble\"", "2", "3", "0", "0", "3", "4", "\"A\"", "private set", "no public set", "none"], result.Checks.Select(check => check.Expected));
        Assert.Equal(4, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level13_MathMaxSolution_Passes()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<Passed>(await Run(level, Golem(constructorBody: "Name = name;\n        Armor = Math.Max(0, armor);", crackBody: "Armor = Math.Max(0, Armor - 1);")));

        Assert.Equal(3, result.StatementCount);
    }

    [Fact]
    public async Task Level13_NegativeArmorKept_ReportsExpectedAndObtained()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(constructorBody: "Name = name;\n        Armor = armor;")));

        Assert.Contains(new CheckOutcome("new Golem(\"Pebble\", -2); Armor", "0", "-2"), result.Checks);
    }

    [Fact]
    public async Task Level13_PublicSetter_IsRejected()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(armor: "public int Armor { get; set; }")));

        Assert.Contains(new CheckOutcome("Armor setter", "private set", "public set"), result.Checks);
    }

    [Fact]
    public async Task Level13_PublicNameSetter_IsRejected()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(name: "public string Name { get; set; }")));

        Assert.Contains(new CheckOutcome("Name setter", "no public set", "public set"), result.Checks);
    }

    [Fact]
    public async Task Level13_PublicFieldsInsteadOfProperties_AreReported()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(name: "public string Name;", armor: "public int Armor;")));

        Assert.Contains(new CheckOutcome("Armor setter", "private set", "no property Armor"), result.Checks);
        Assert.Contains(new CheckOutcome("new Golem(\"Rocky\", 3); Armor", "3", "no public property Armor"), result.Checks);
    }

    [Fact]
    public async Task Level13_ConstructorIgnoringItsArguments_IsRejected()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(constructorBody: "Name = \"Rocky\";\n        Armor = 3;")));

        Assert.Contains(result.Checks, check => check.Case == "new Golem(\"Pebble\", 5); Armor" && !check.Passed);
        Assert.Contains(result.Checks, check => check.Case == "new Golem(\"Pebble\", 5); Name" && !check.Passed);
    }

    [Fact]
    public async Task Level13_CrackIgnoringCurrentArmor_IsRejected()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(crackBody: "Armor = 2;")));

        Assert.Contains(result.Checks, check => check.Case == "new Golem(\"Pebble\", 5); Crack(); Crack(); Armor" && !check.Passed);
        Assert.Contains(result.Checks, check => check.Case == "new Golem(\"Rocky\", 3); Crack() x4; Armor" && !check.Passed);
    }

    [Fact]
    public async Task Level13_CrackWithoutFloor_ReportsExpectedAndObtained()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(crackBody: "Armor--;")));

        Assert.Contains(new CheckOutcome("new Golem(\"Rocky\", 3); Crack() x4; Armor", "0", "-1"), result.Checks);
    }

    [Fact]
    public async Task Level13_StaticArmor_IsRejected()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(armor: "public static int Armor { get; private set; }")));

        Assert.Contains(new CheckOutcome("a = new Golem(\"A\", 4), b = new Golem(\"B\", 4); a.Crack(); b.Armor", "4", "3"), result.Checks);
    }

    [Fact]
    public async Task Level13_MissingConstructor_Explains()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<CompilationFailed>(await Run(level, "class Golem\n{\n    public string Name { get; }\n    public int Armor { get; private set; }\n\n    public void Crack()\n    {\n    }\n}"));

        Assert.All(result.Errors, error =>
        {
            Assert.Equal("\"Golem\" has no constructor that takes 2 arguments.", error.Explanation!.En);
            Assert.Equal("\"Golem\" não tem um construtor que receba 2 argumentos.", error.Explanation.Pt);
        });
    }

    [Fact]
    public async Task Level13_PrivateMemberUsedOutside_Explains()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Golem(crackModifier: "")));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.Equal("\"Crack\" não é public, por isso não se pode usar aqui, fora da classe \"Golem\". Falta public?", error.Explanation!.Pt));
    }

    private const string CreatureClass = "class Creature\n{\n    public string Name { get; }\n    public int Health { get; }\n\n    public Creature(string name, int health)\n    {\n        Name = name;\n        Health = health;\n    }\n\n    public string Describe()\n    {\n        return Name + \" (\" + Health + \")\";\n    }\n}\n\n";

    private static string Dragon(
        string creature = CreatureClass,
        string header = "class Dragon : Creature",
        string baseCall = " : base(name, health)",
        string extraMembers = "",
        string breatheBody = "return fire * 2;") =>
        $"{creature}{header}\n{{\n    private int fire;\n{extraMembers}\n    public Dragon(string name, int health, int fire){baseCall}\n    {{\n        this.fire = fire;\n    }}\n\n    public int Breathe()\n    {{\n        {breatheBody}\n    }}\n}}";

    [Fact]
    public async Task Level14_Solution_RunsTheThirteenDragonChecksWithinTheStarLimit()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(
            [
                "new Dragon(\"Smaug\", 50, 7).Describe()",
                "new Dragon(\"Ember\", 30, 4).Describe()",
                "new Creature(\"Rat\", 5).Describe()",
                "new Dragon(\"Smaug\", 50, 7).Breathe()",
                "new Dragon(\"Ember\", 30, 4).Breathe()",
                "new Dragon(\"Wisp\", 9, 0).Breathe()",
                "new Dragon(\"Smaug\", 50, 7).Name",
                "new Dragon(\"Smaug\", 50, 7).Health",
                "new Dragon(\"Ember\", 30, 4).Health",
                "new Dragon(\"Smaug\", 50, 7) is Creature",
                "typeof(Creature)",
                "typeof(Dragon).GetMethod(\"Describe\").DeclaringType",
                "typeof(Dragon).GetMethod(\"Breathe\").DeclaringType",
            ],
            result.Checks.Select(check => check.Case));
        Assert.Equal(["\"Smaug (50)\"", "\"Ember (30)\"", "\"Rat (5)\"", "14", "8", "0", "\"Smaug\"", "50", "30", "true", "class", "Creature", "Dragon"], result.Checks.Select(check => check.Expected));
        Assert.Equal(5, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level14_FieldsInsteadOfProperties_Pass()
    {
        var level = Find("14-inheritance");
        var creature = CreatureClass.Replace("Name { get; }", "Name;", StringComparison.Ordinal).Replace("Health { get; }", "Health;", StringComparison.Ordinal);

        Assert.IsType<Passed>(await Run(level, Dragon(creature: creature)));
    }

    [Fact]
    public async Task Level14_HardCodedBreathe_IsRejected()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Dragon(breatheBody: "return 14;")));

        Assert.Contains(new CheckOutcome("new Dragon(\"Ember\", 30, 4).Breathe()", "8", "14"), result.Checks);
        Assert.Contains(new CheckOutcome("new Dragon(\"Wisp\", 9, 0).Breathe()", "0", "14"), result.Checks);
    }

    [Fact]
    public async Task Level14_BreatheIgnoringTheDoubling_ReportsExpectedAndObtained()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Dragon(breatheBody: "return fire;")));

        Assert.Contains(new CheckOutcome("new Dragon(\"Smaug\", 50, 7).Breathe()", "14", "7"), result.Checks);
    }

    [Fact]
    public async Task Level14_CopiedInsteadOfInherited_IsRejected()
    {
        var level = Find("14-inheritance");
        var copied = Dragon(
            header: "class Dragon",
            baseCall: "",
            extraMembers: "    public string Name { get; }\n    public int Health { get; }\n\n    public string Describe()\n    {\n        return Name + \" (\" + Health + \")\";\n    }\n")
            .Replace("this.fire = fire;", "Name = name;\n        Health = health;\n        this.fire = fire;", StringComparison.Ordinal);

        var result = Assert.IsType<ChecksFailed>(await Run(level, copied));

        Assert.Contains(new CheckOutcome("new Dragon(\"Smaug\", 50, 7) is Creature", "true", "false"), result.Checks);
        Assert.Contains(new CheckOutcome("typeof(Dragon).GetMethod(\"Describe\").DeclaringType", "Creature", "Dragon"), result.Checks);
        Assert.Contains(new CheckOutcome("new Dragon(\"Smaug\", 50, 7).Describe()", "\"Smaug (50)\"", "not a Creature"), result.Checks);
    }

    [Theory]
    [InlineData("public new string Describe() { return Name + \" (\" + Health + \")\"; }", false)]
    [InlineData("public override string Describe() { return Name + \" (\" + Health + \")\"; }", true)]
    public async Task Level14_DescribeDeclaredAgainInDragon_IsRejected(string member, bool virtualInCreature)
    {
        var level = Find("14-inheritance");
        var creature = virtualInCreature
            ? CreatureClass.Replace("public string Describe", "public virtual string Describe", StringComparison.Ordinal)
            : CreatureClass;

        var result = Assert.IsType<ChecksFailed>(await Run(level, Dragon(creature: creature, extraMembers: $"    {member}\n")));

        Assert.Contains(new CheckOutcome("typeof(Dragon).GetMethod(\"Describe\").DeclaringType", "Creature", "Dragon"), result.Checks);
    }

    [Fact]
    public async Task Level14_BaseCallWithWrongHealth_IsRejected()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Dragon(baseCall: " : base(name, 0)")));

        Assert.Contains(new CheckOutcome("new Dragon(\"Smaug\", 50, 7).Describe()", "\"Smaug (50)\"", "\"Smaug (0)\""), result.Checks);
        Assert.Contains(new CheckOutcome("new Dragon(\"Ember\", 30, 4).Health", "30", "0"), result.Checks);
    }

    [Fact]
    public async Task Level14_MissingBaseCall_Explains()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Dragon(baseCall: "")));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error =>
        {
            Assert.Equal("Falta passar \"name\" ao construtor de \"Creature\": usa : base(...) no construtor de \"Dragon\".", error.Explanation!.Pt);
            Assert.Equal("\"name\" must be passed to the constructor of \"Creature\": use : base(...) in the constructor of \"Dragon\".", error.Explanation.En);
        });
    }

    [Fact]
    public async Task Level14_NoColonCreature_ShowsThatTheDragonIsNotACreature()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Dragon(header: "class Dragon", baseCall: "")));

        Assert.Contains(new CheckOutcome("new Dragon(\"Smaug\", 50, 7) is Creature", "true", "false"), result.Checks);
        Assert.Contains(new CheckOutcome("new Dragon(\"Smaug\", 50, 7).Describe()", "\"Smaug (50)\"", "not a Creature"), result.Checks);
    }

    [Fact]
    public async Task Level14_BaseCallWithoutColonCreature_Explains()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Dragon(header: "class Dragon")));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error =>
        {
            Assert.Equal("\"Dragon\" ainda não herda de nenhuma classe, por isso base(...) não tem para onde ir: acrescenta \": ClasseBase\" depois do nome.", error.Explanation!.Pt);
            Assert.Equal("\"Dragon\" doesn't inherit from any class yet, so base(...) has nowhere to go: add \": BaseClass\" after its name.", error.Explanation.En);
        });
    }

    [Fact]
    public async Task Level14_DragonConstructorNotPublic_Explains()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Dragon().Replace("public Dragon(", "Dragon(", StringComparison.Ordinal)));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.Equal("O construtor de \"Dragon\" não é public, por isso new não o pode usar fora da classe. Põe public antes de Dragon(...).", error.Explanation!.Pt));
    }

    [Fact]
    public async Task Level14_CreatureConstructorNotPublic_Explains()
    {
        var level = Find("14-inheritance");
        var creature = CreatureClass.Replace("public Creature(", "Creature(", StringComparison.Ordinal);

        var result = Assert.IsType<CompilationFailed>(await Run(level, Dragon(creature: creature)));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.Equal("O construtor de \"Creature\" não é public, por isso new não o pode usar fora da classe. Põe public antes de Creature(...).", error.Explanation!.Pt));
    }

    [Fact]
    public async Task Level14_StaticFire_IsRejected()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Dragon().Replace("private int fire;", "private static int fire;", StringComparison.Ordinal).Replace("this.fire", "Dragon.fire", StringComparison.Ordinal)));

        Assert.Contains(new CheckOutcome("new Dragon(\"Smaug\", 50, 7).Breathe()", "14", "0"), result.Checks);
    }

    [Fact]
    public async Task Level14_BreatheLeftToCreature_IsRejected()
    {
        var level = Find("14-inheritance");
        var creature = CreatureClass.Replace("    public string Describe", "    public int Breathe()\n    {\n        return 14;\n    }\n\n    public string Describe", StringComparison.Ordinal);
        var withoutOwnBreathe = Dragon(creature: creature).Replace("    public int Breathe()\n    {\n        return fire * 2;\n    }", "", StringComparison.Ordinal);

        var result = Assert.IsType<ChecksFailed>(await Run(level, withoutOwnBreathe));

        Assert.Contains(new CheckOutcome("typeof(Dragon).GetMethod(\"Breathe\").DeclaringType", "Dragon", "Creature"), result.Checks);
    }

    [Fact]
    public async Task Level14_NameHiddenInDragon_IsReportedNotThrown()
    {
        var level = Find("14-inheritance");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Dragon(extraMembers: "    public new int Name { get; }\n")));

        Assert.Contains(new CheckOutcome("new Dragon(\"Smaug\", 50, 7).Name", "\"Smaug\"", "Name is declared twice"), result.Checks);
    }

    [Fact]
    public async Task Level14_CreatureAsStruct_IsReportedByAShapeCheck()
    {
        var level = Find("14-inheritance");
        var structCreature = CreatureClass.Replace("class Creature", "struct Creature", StringComparison.Ordinal);

        var result = Assert.IsType<ChecksFailed>(await Run(level, Dragon(creature: structCreature, header: "class Dragon", baseCall: "")));

        Assert.Contains(new CheckOutcome("typeof(Creature)", "class", "struct"), result.Checks);
    }

    private static string Creatures(
        string creatureSpeak = "public virtual string Speak() { return \"...\"; }",
        string catHeader = "class Cat : Creature",
        string catSpeak = "public override string Speak() { return \"Meow\"; }",
        string owlHeader = "class Owl : Creature",
        string owlSpeak = "public override string Speak() { return \"Hoot\"; }") =>
        $"class Creature\n{{\n    {creatureSpeak}\n}}\n\n{catHeader}\n{{\n    {catSpeak}\n}}\n\n{owlHeader}\n{{\n    {owlSpeak}\n}}";

    [Fact]
    public async Task Level15_Solution_RunsTheElevenChoirChecksWithinTheStarLimit()
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(
            [
                "typeof(Creature)",
                "typeof(Cat)",
                "typeof(Owl)",
                "typeof(Cat).BaseType",
                "typeof(Owl).BaseType",
                "creatures[0].Speak()",
                "creatures[1].Speak()",
                "creatures[2].Speak()",
                "Creature.Speak",
                "Cat.Speak",
                "Owl.Speak",
            ],
            result.Checks.Select(check => check.Case));
        Assert.Equal(["class", "class", "class", "Creature", "Creature", "\"...\"", "\"Meow\"", "\"Hoot\"", "virtual", "override", "override"], result.Checks.Select(check => check.Expected));
        Assert.Equal(3, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Theory]
    [InlineData("public new string Speak() { return \"Meow\"; }")]
    [InlineData("public string Speak() { return \"Meow\"; }")]
    public async Task Level15_HidingInsteadOfOverride_ShowsWhy(string catSpeak)
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Creatures(catSpeak: catSpeak)));

        Assert.Contains(new CheckOutcome("Cat.Speak", "override", "hides (missing override)"), result.Checks);
        Assert.Contains(new CheckOutcome("creatures[1].Speak()", "\"Meow\"", "\"...\""), result.Checks);
        Assert.Contains(new CheckOutcome("Owl.Speak", "override", "override"), result.Checks);
    }

    [Fact]
    public async Task Level15_NewVirtualInsteadOfOverride_IsStillHiding()
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Creatures(owlSpeak: "public new virtual string Speak() { return \"Hoot\"; }")));

        Assert.Contains(new CheckOutcome("Owl.Speak", "override", "hides (missing override)"), result.Checks);
    }

    [Fact]
    public async Task Level15_SpeakNotVirtual_IsReported()
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Creatures(
            creatureSpeak: "public string Speak() { return \"...\"; }",
            catSpeak: "public new string Speak() { return \"Meow\"; }",
            owlSpeak: "public new string Speak() { return \"Hoot\"; }")));

        Assert.Contains(new CheckOutcome("Creature.Speak", "virtual", "not virtual"), result.Checks);
    }

    [Fact]
    public async Task Level15_OverrideWithoutVirtual_Explains()
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Creatures(creatureSpeak: "public string Speak() { return \"...\"; }")));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error =>
        {
            Assert.Equal("\"Speak\" in the base class is neither virtual nor abstract, so it can't be overridden.", error.Explanation!.En);
            Assert.Equal("\"Speak\" na classe base não é virtual nem abstract, por isso não se pode fazer override.", error.Explanation.Pt);
        });
    }

    [Theory]
    [InlineData("override string Speak() { return \"Meow\"; }")]
    [InlineData("protected override string Speak() { return \"Meow\"; }")]
    public async Task Level15_OverrideWithoutPublic_Explains(string catSpeak)
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Creatures(catSpeak: catSpeak)));

        var error = Assert.Single(result.Errors);
        Assert.Equal(8, error.Line);
        Assert.Contains("public", error.Explanation!.En, StringComparison.Ordinal);
        Assert.DoesNotContain("inherits", error.Explanation.En, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Level15_VirtualWithoutPublic_Explains()
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Creatures(creatureSpeak: "virtual string Speak() { return \"...\"; }")));

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.Line);
        Assert.Equal("\"Speak\" is virtual, abstract or override, so it must be public.", error.Explanation!.En);
    }

    // Accepted: the base field plus base.Speak() is real overriding (each class still has its own override), it only
    // costs the third star because the solution grows past the limit.
    [Fact]
    public async Task Level15_BaseSpeakWithAProtectedField_PassesWithoutThreeStars()
    {
        var level = Find("15-polymorphism");
        const string code = """
            class Creature
            {
                protected string sound = "...";

                public virtual string Speak()
                {
                    return sound;
                }
            }

            class Cat : Creature
            {
                public Cat()
                {
                    sound = "Meow";
                }

                public override string Speak()
                {
                    return base.Speak();
                }
            }

            class Owl : Creature
            {
                public Owl()
                {
                    sound = "Hoot";
                }

                public override string Speak()
                {
                    return base.Speak();
                }
            }
            """;

        var result = Assert.IsType<Passed>(await Run(level, code));

        Assert.True(result.StatementCount > level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level15_OverrideOfAMissingMethod_Explains()
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Creatures(creatureSpeak: "public virtual string Talk() { return \"...\"; }")));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.NotNull(error.Explanation));
    }

    [Fact]
    public async Task Level15_CatAndOwlNotDerivingFromCreature_AreToldSo()
    {
        var level = Find("15-polymorphism");
        var notDeriving = Creatures(catHeader: "class Cat", owlHeader: "class Owl")
            .Replace("override ", "", StringComparison.Ordinal);

        var result = Assert.IsType<ChecksFailed>(await Run(level, notDeriving));

        Assert.Contains(new CheckOutcome("typeof(Cat).BaseType", "Creature", "Object"), result.Checks);
        Assert.Contains(new CheckOutcome("creatures[1].Speak()", "\"Meow\"", "not a Creature"), result.Checks);
        Assert.Contains(new CheckOutcome("creatures[2].Speak()", "\"Hoot\"", "not a Creature"), result.Checks);
        Assert.Contains(new CheckOutcome("Cat.Speak", "override", "not a Creature"), result.Checks);
        Assert.Contains(new CheckOutcome("Owl.Speak", "override", "not a Creature"), result.Checks);
    }

    [Fact]
    public async Task Level15_TypeSwitchInsideCreature_IsRejected()
    {
        var level = Find("15-polymorphism");
        var typeSwitch = Creatures(
            creatureSpeak: "public virtual string Speak() { return this is Cat ? \"Meow\" : this is Owl ? \"Hoot\" : \"...\"; }",
            catSpeak: "",
            owlSpeak: "");

        var result = Assert.IsType<ChecksFailed>(await Run(level, typeSwitch));

        Assert.Contains(new CheckOutcome("creatures[1].Speak()", "\"Meow\"", "\"Meow\""), result.Checks);
        Assert.Contains(new CheckOutcome("Cat.Speak", "override", "inherited (missing override)"), result.Checks);
        Assert.Contains(new CheckOutcome("Owl.Speak", "override", "inherited (missing override)"), result.Checks);
    }

    [Fact]
    public async Task Level15_WrongOverrideText_ReportsExpectedAndObtained()
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Creatures(owlSpeak: "public override string Speak() { return \"Woof\"; }")));

        Assert.Contains(new CheckOutcome("creatures[2].Speak()", "\"Hoot\"", "\"Woof\""), result.Checks);
    }

    [Fact]
    public async Task Level15_EveryoneSharingTheBaseSpeech_IsRejected()
    {
        var level = Find("15-polymorphism");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Creatures(catSpeak: "", owlSpeak: "")));

        Assert.Contains(new CheckOutcome("creatures[1].Speak()", "\"Meow\"", "\"...\""), result.Checks);
        Assert.Contains(new CheckOutcome("Cat.Speak", "override", "inherited (missing override)"), result.Checks);
    }

    private const string FlyerInterface = "interface IFlyer\n{\n    int FlySpeed();\n}";

    private const string AbstractBeast =
        "abstract class Beast\n{\n    public abstract string Sound();\n\n    public string Roar()\n    {\n        return Sound().ToUpper() + \"!\";\n    }\n}";

    private static string Griffins(
        string flyer = FlyerInterface,
        string beast = AbstractBeast,
        string griffin = "class Griffin : Beast, IFlyer\n{\n    public override string Sound()\n    {\n        return \"screech\";\n    }\n\n    public int FlySpeed()\n    {\n        return 30;\n    }\n}") =>
        $"{flyer}\n\n{beast}\n\n{griffin}";

    private static string GriffinWith(string header = "class Griffin : Beast, IFlyer", string sound = "public override string Sound() { return \"screech\"; }", string speed = "public int FlySpeed() { return 30; }") =>
        $"{header}\n{{\n    {sound}\n\n    {speed}\n}}";

    [Fact]
    public async Task Level16_Solution_RunsTheTenMenagerieChecksWithinTheStarLimit()
    {
        var level = Find("16-interfaces");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(
            [
                "typeof(Beast)",
                "typeof(IFlyer)",
                "typeof(Griffin)",
                "typeof(Griffin).BaseType",
                "new Griffin() is IFlyer",
                "Beast.Sound",
                "typeof(Griffin).GetMethod(\"Roar\").DeclaringType",
                "new Griffin().Sound()",
                "new Griffin().Roar()",
                "((IFlyer)new Griffin()).FlySpeed()",
            ],
            result.Checks.Select(check => check.Case));
        Assert.Equal(["abstract class", "interface", "class", "Beast", "true", "abstract", "Beast", "\"screech\"", "\"SCREECH!\"", "30"], result.Checks.Select(check => check.Expected));
        Assert.Equal(3, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level16_ProtectedSound_Passes()
    {
        var level = Find("16-interfaces");
        var code = Griffins(
            beast: AbstractBeast.Replace("public abstract", "protected abstract", StringComparison.Ordinal),
            griffin: GriffinWith(sound: "protected override string Sound() { return \"screech\"; }"));

        Assert.IsType<Passed>(await Run(level, code));
    }

    [Fact]
    public async Task Level16_ConcreteBeast_IsRejected()
    {
        var level = Find("16-interfaces");
        var concrete = AbstractBeast.Replace("abstract class", "class", StringComparison.Ordinal)
            .Replace("public abstract string Sound();", "public virtual string Sound() { return \"\"; }", StringComparison.Ordinal);

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(beast: concrete, griffin: GriffinWith(sound: "public override string Sound() { return \"screech\"; }"))));

        Assert.Contains(new CheckOutcome("typeof(Beast)", "abstract class", "class"), result.Checks);
        Assert.Contains(new CheckOutcome("Beast.Sound", "abstract", "virtual"), result.Checks);
    }

    [Fact]
    public async Task Level16_VirtualSoundInAnAbstractBeast_IsRejected()
    {
        var level = Find("16-interfaces");
        var virtualSound = AbstractBeast.Replace("public abstract string Sound();", "public virtual string Sound() { return \"\"; }", StringComparison.Ordinal);

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(beast: virtualSound)));

        Assert.Contains(new CheckOutcome("Beast.Sound", "abstract", "virtual"), result.Checks);
        Assert.Contains(new CheckOutcome("typeof(Beast)", "abstract class", "abstract class"), result.Checks);
    }

    [Fact]
    public async Task Level16_SoundWithoutAbstractAndWithoutBody_Explains()
    {
        var level = Find("16-interfaces");
        var bodiless = AbstractBeast.Replace("public abstract string Sound();", "public string Sound();", StringComparison.Ordinal);

        var result = Assert.IsType<CompilationFailed>(await Run(level, Griffins(beast: bodiless)));

        Assert.All(result.Errors, error => Assert.NotNull(error.Explanation));
    }

    [Fact]
    public async Task Level16_AbstractMemberInANonAbstractClass_Explains()
    {
        var level = Find("16-interfaces");
        var notAbstract = AbstractBeast.Replace("abstract class", "class", StringComparison.Ordinal);

        var result = Assert.IsType<CompilationFailed>(await Run(level, Griffins(beast: notAbstract)));

        var error = Assert.Single(result.Errors);
        Assert.Equal(8, error.Line);
        Assert.Contains("abstract", error.Explanation!.En, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Level16_AbstractSoundWithoutPublic_Explains()
    {
        var level = Find("16-interfaces");
        var hidden = AbstractBeast.Replace("public abstract string Sound();", "abstract string Sound();", StringComparison.Ordinal);

        var result = Assert.IsType<CompilationFailed>(await Run(level, Griffins(beast: hidden)));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.Equal("\"Sound\" is virtual, abstract or override, so it must be public.", error.Explanation!.En));
        Assert.Equal(8, Assert.Single(result.Errors).Line);
    }

    [Theory]
    [InlineData("abstract class IFlyer\n{\n    public abstract int FlySpeed();\n}", "class Griffin : Beast")]
    [InlineData("class IFlyer\n{\n    public int FlySpeed() { return 30; }\n}", "class Griffin : Beast")]
    public async Task Level16_FlyerAsAClass_IsRejected(string flyer, string header)
    {
        var level = Find("16-interfaces");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(flyer: flyer, griffin: GriffinWith(header: header, speed: "public int FlySpeed() { return 30; }"))));

        Assert.Single(result.Checks, check => check.Case == "typeof(IFlyer)" && check.Expected == "interface" && check.Actual != "interface");
        Assert.Contains(new CheckOutcome("((IFlyer)new Griffin()).FlySpeed()", "30", "not an IFlyer"), result.Checks);
    }

    [Fact]
    public async Task Level16_FlyerAsAClassListedAsBase_Explains()
    {
        var level = Find("16-interfaces");
        var result = Assert.IsType<CompilationFailed>(await Run(level, Griffins(flyer: "abstract class IFlyer\n{\n    public abstract int FlySpeed();\n}")));

        Assert.Equal("A class inherits from a single base, and \"IFlyer\" is a class: to list it after the base, \"IFlyer\" must be an interface.", Assert.Single(result.Errors).Explanation!.En);
    }

    [Fact]
    public async Task Level16_GriffinNotImplementingTheInterface_IsToldSo()
    {
        var level = Find("16-interfaces");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(griffin: GriffinWith(header: "class Griffin : Beast"))));

        Assert.Contains(new CheckOutcome("new Griffin() is IFlyer", "true", "false"), result.Checks);
        Assert.Contains(new CheckOutcome("((IFlyer)new Griffin()).FlySpeed()", "30", "not an IFlyer"), result.Checks);
    }

    [Fact]
    public async Task Level16_RoarOverriddenInGriffin_IsRejected()
    {
        var level = Find("16-interfaces");
        var virtualRoar = AbstractBeast.Replace("public string Roar", "public virtual string Roar", StringComparison.Ordinal);
        var griffin = GriffinWith(sound: "public override string Sound() { return \"screech\"; }\n\n    public override string Roar() { return \"SCREECH!\"; }");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(beast: virtualRoar, griffin: griffin)));

        Assert.Contains(new CheckOutcome("typeof(Griffin).GetMethod(\"Roar\").DeclaringType", "Beast", "Griffin"), result.Checks);
        Assert.Contains(new CheckOutcome("new Griffin().Roar()", "\"SCREECH!\"", "\"SCREECH!\""), result.Checks);
    }

    [Fact]
    public async Task Level16_RoarHiddenInGriffin_IsRejected()
    {
        var level = Find("16-interfaces");
        var griffin = GriffinWith(sound: "public override string Sound() { return \"screech\"; }\n\n    public new string Roar() { return \"SCREECH!\"; }");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(griffin: griffin)));

        Assert.Contains(new CheckOutcome("typeof(Griffin).GetMethod(\"Roar\").DeclaringType", "Beast", "Griffin"), result.Checks);
    }

    [Fact]
    public async Task Level16_RoarMissing_IsReportedNotThrown()
    {
        var level = Find("16-interfaces");
        var noRoar = "abstract class Beast\n{\n    public abstract string Sound();\n}";

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(beast: noRoar)));

        Assert.Contains(new CheckOutcome("new Griffin().Roar()", "\"SCREECH!\"", "no public Roar()"), result.Checks);
        Assert.Contains(new CheckOutcome("typeof(Griffin).GetMethod(\"Roar\").DeclaringType", "Beast", "no public Roar()"), result.Checks);
    }

    [Fact]
    public async Task Level16_RoarNotUsingSound_ReportsExpectedAndObtained()
    {
        var level = Find("16-interfaces");
        var lower = AbstractBeast.Replace("Sound().ToUpper() + \"!\"", "Sound() + \"!\"", StringComparison.Ordinal);

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(beast: lower)));

        Assert.Contains(new CheckOutcome("new Griffin().Roar()", "\"SCREECH!\"", "\"screech!\""), result.Checks);
    }

    [Fact]
    public async Task Level16_WrongFlySpeed_ReportsExpectedAndObtained()
    {
        var level = Find("16-interfaces");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(griffin: GriffinWith(speed: "public int FlySpeed() { return 31; }"))));

        Assert.Contains(new CheckOutcome("((IFlyer)new Griffin()).FlySpeed()", "30", "31"), result.Checks);
    }

    [Fact]
    public async Task Level16_GriffinAsAStruct_IsRejected()
    {
        var level = Find("16-interfaces");
        var griffin = "struct Griffin : IFlyer\n{\n    public string Sound() { return \"screech\"; }\n\n    public int FlySpeed() { return 30; }\n}";

        var result = Assert.IsType<ChecksFailed>(await Run(level, Griffins(griffin: griffin)));

        Assert.Contains(new CheckOutcome("typeof(Griffin)", "class", "struct"), result.Checks);
        Assert.Contains(new CheckOutcome("new Griffin().Roar()", "\"SCREECH!\"", "not a Beast"), result.Checks);
    }

    [Fact]
    public async Task Level16_MissingImplementation_Explains()
    {
        var level = Find("16-interfaces");

        var noSound = Assert.IsType<CompilationFailed>(await Run(level, Griffins(griffin: GriffinWith(sound: ""))));
        var noSpeed = Assert.IsType<CompilationFailed>(await Run(level, Griffins(griffin: GriffinWith(speed: ""))));

        Assert.Equal("\"Griffin\" doesn't implement \"Sound\" yet (required by \"Beast\").", Assert.Single(noSound.Errors).Explanation!.En);
        Assert.Equal("\"Griffin\" doesn't implement \"FlySpeed\" yet (required by \"IFlyer\").", Assert.Single(noSpeed.Errors).Explanation!.En);
    }

    [Fact]
    public async Task Level16_InterfaceMethodWithoutPublic_Explains()
    {
        var level = Find("16-interfaces");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Griffins(griffin: GriffinWith(speed: "int FlySpeed() { return 30; }"))));

        var error = Assert.Single(result.Errors);
        Assert.Equal("\"FlySpeed\" comes from an interface, so it must be public.", error.Explanation!.En);
        Assert.Equal("\"FlySpeed\" vem de uma interface, por isso tem de ser public.", error.Explanation.Pt);
        Assert.Equal(16, error.Line);
    }

    [Fact]
    public async Task Level16_NewAbstract_Explains()
    {
        var level = Find("16-interfaces");
        var abstractGriffin = GriffinWith(header: "abstract class Griffin : Beast, IFlyer");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Griffins(griffin: abstractGriffin)));

        var error = Assert.Single(result.Errors);
        Assert.Equal("You can't use new on \"Griffin\": it is abstract (or an interface).", error.Explanation!.En);
    }


    private static string RuneMathWith(string total, string strongest) =>
        $"static class RuneMath\n{{\n    public static int Total(Rune[] runes)\n    {{\n        {total}\n    }}\n\n    public static Element Strongest(Rune[] runes)\n    {{\n        {strongest}\n    }}\n}}";

    private const string EnumAndStruct =
        "enum Element { Fire, Water, Earth }\n\nstruct Rune\n{\n    public Element Element;\n    public int Power;\n}\n\n";

    private const string SumRunes = "int total = 0; foreach (Rune rune in runes) { total += rune.Power; } return total;";

    private const string PickStrongest =
        "Rune best = runes[0]; foreach (Rune rune in runes) { if (rune.Power > best.Power) { best = rune; } } return best.Element;";

    [Fact]
    public async Task Level17_Solution_RunsTheThirteenRuneChecksWithinTheStarLimit()
    {
        var level = Find("17-static-enum-struct");

        var result = Assert.IsType<Passed>(await Run(level, level.Solution));

        Assert.Equal(13, result.Checks.Count);
        Assert.Equal(
            ["enum", "struct", "static class", "1", "10", "4", "20", "0", "Element.Water", "Element.Fire", "Element.Earth", "Element.Fire", "Element.Water"],
            result.Checks.Select(check => check.Expected));
        Assert.Equal(9, level.MaxStatementsForThreeStars);
        Assert.True(result.StatementCount <= level.MaxStatementsForThreeStars);
    }

    [Fact]
    public async Task Level17_RuneAsClass_IsRejected()
    {
        var level = Find("17-static-enum-struct");

        var result = Assert.IsType<ChecksFailed>(await Run(level, level.Solution.Replace("struct Rune", "class Rune")));

        Assert.Contains(new CheckOutcome("typeof(Rune)", "struct", "class"), result.Checks);
        Assert.Contains(new CheckOutcome("var a = new Rune { Power = 1 }; var b = a; b.Power = 9; a.Power", "1", "9"), result.Checks);
    }

    [Fact]
    public async Task Level17_RuneMathNotStatic_IsRejected()
    {
        var level = Find("17-static-enum-struct");
        var code = level.Solution.Replace("static class RuneMath", "class RuneMath").Replace("public static ", "public ");

        var result = Assert.IsType<ChecksFailed>(await Run(level, code));

        Assert.Contains(new CheckOutcome("typeof(RuneMath)", "static class", "class"), result.Checks);
        Assert.Contains(new CheckOutcome("RuneMath.Total([Earth 4])", "4", "Total is not static"), result.Checks);
    }

    [Fact]
    public async Task Level17_ElementAsAClassOfConstants_IsRejected()
    {
        var level = Find("17-static-enum-struct");
        var code = level.Solution.Replace(
            "enum Element\n{\n    Fire,\n    Water,\n    Earth,\n}",
            "class Element\n{\n    public static Element Fire = new Element();\n    public static Element Water = new Element();\n    public static Element Earth = new Element();\n}");

        var result = Assert.IsType<ChecksFailed>(await Run(level, code));

        Assert.Contains(new CheckOutcome("typeof(Element)", "enum", "class"), result.Checks);
    }

    [Fact]
    public async Task Level17_StrongestReturningAFixedElement_IsRejected()
    {
        var level = Find("17-static-enum-struct");
        var code = EnumAndStruct + RuneMathWith(SumRunes, "return Element.Water;");

        var result = Assert.IsType<ChecksFailed>(await Run(level, code));

        Assert.Contains(new CheckOutcome("RuneMath.Strongest([Fire 7, Water 2, Earth 4])", "Element.Fire", "Element.Water"), result.Checks);
        Assert.Contains(new CheckOutcome("RuneMath.Strongest([Fire 1, Earth 9, Water 4])", "Element.Earth", "Element.Water"), result.Checks);
    }

    [Fact]
    public async Task Level17_TotalIgnoringTheInput_IsRejected()
    {
        var level = Find("17-static-enum-struct");
        var code = EnumAndStruct + RuneMathWith("return 10;", PickStrongest);

        var result = Assert.IsType<ChecksFailed>(await Run(level, code));

        Assert.Contains(new CheckOutcome("RuneMath.Total([Earth 4])", "4", "10"), result.Checks);
        Assert.Contains(new CheckOutcome("RuneMath.Total([])", "0", "10"), result.Checks);
    }

    [Fact]
    public async Task Level17_TieGoingToTheLastRune_IsRejected()
    {
        var level = Find("17-static-enum-struct");
        var code = EnumAndStruct + RuneMathWith(SumRunes, PickStrongest.Replace(">", ">="));

        var result = Assert.IsType<ChecksFailed>(await Run(level, code));

        Assert.Contains(new CheckOutcome("RuneMath.Strongest([Earth 1, Fire 8, Water 8])", "Element.Fire", "Element.Water"), result.Checks);
    }

    [Fact]
    public async Task Level17_StaticMethodCalledThroughAnObject_Explains()
    {
        var level = Find("17-static-enum-struct");
        var code = level.Solution.Replace("public int Power;", "public int Power;\n    public static int Twice(int n) { return n * 2; }")
            .Replace("return best.Element;", "int unused = best.Twice(1);\n        return best.Element;");

        var result = Assert.IsType<CompilationFailed>(await Run(level, code));

        var error = Assert.Single(result.Errors);
        Assert.Equal("\"Twice\" é static: usa-o pelo nome da classe (\"Rune.Twice\") e não por um objeto, ou tira o static.", error.Explanation!.Pt);
        Assert.NotNull(error.Line);
    }

    [Fact]
    public async Task Level17_InstanceMethodInTheStaticClass_Explains()
    {
        var level = Find("17-static-enum-struct");

        var result = Assert.IsType<CompilationFailed>(await Run(level, level.Solution.Replace("public static int Total", "public int Total")));

        var error = Assert.Single(result.Errors);
        Assert.Equal("\"Total\" está numa classe static, por isso também tem de ser static.", error.Explanation!.Pt);
        Assert.NotNull(error.Line);
    }


    [Fact]
    public async Task Level17_ElementAsIntConstants_IsRejected()
    {
        var level = Find("17-static-enum-struct");
        var code = level.Solution
            .Replace("enum Element\n{\n    Fire,\n    Water,\n    Earth,\n}", "static class Element\n{\n    public const int Fire = 0;\n    public const int Water = 1;\n    public const int Earth = 2;\n}")
            .Replace("public Element Element;", "public int Element;")
            .Replace("Element Strongest", "int Strongest");

        var result = Assert.IsType<ChecksFailed>(await Run(level, code));

        Assert.Contains(new CheckOutcome("typeof(Element)", "enum", "static class"), result.Checks);
    }

    [Fact]
    public async Task Level17_ElementAsStrings_IsRejected()
    {
        var level = Find("17-static-enum-struct");
        var code = level.Solution
            .Replace("enum Element\n{\n    Fire,\n    Water,\n    Earth,\n}\n\n", "")
            .Replace("public Element Element;", "public string Element;")
            .Replace("Element Strongest", "string Strongest");

        var result = Assert.IsType<ChecksFailed>(await Run(level, code));

        Assert.Contains(new CheckOutcome("typeof(Element)", "enum", "no Element"), result.Checks);
    }
    [Fact]
    public async Task Level13_PrivateConstructor_Explains()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<CompilationFailed>(await Run(level, Golem(constructorModifier: "")));

        var error = Assert.Single(result.Errors);
        Assert.Equal("O construtor de \"Golem\" não é public, por isso new não o pode usar fora da classe. Põe public antes de Golem(...).", error.Explanation!.Pt);
    }

    [Fact]
    public async Task Level13_StructGolem_IsToldItMustBeAClass()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, level.Solution.Replace("class Golem", "struct Golem")));

        Assert.Contains(new CheckOutcome("typeof(Golem)", "class", "struct"), result.Checks);
    }

    [Fact]
    public async Task Level13_StaticName_IsRejected()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(name: "public static string Name { get; private set; }")));

        Assert.Contains(new CheckOutcome("a = new Golem(\"A\", 4), b = new Golem(\"B\", 4); a.Crack(); a.Name", "\"A\"", "\"B\""), result.Checks);
    }

    [Fact]
    public async Task Level13_PublicBackingField_IsReported()
    {
        var level = Find("13-constructors");

        var result = Assert.IsType<ChecksFailed>(await Run(level, Golem(armor: "public int backing;\n    public int Armor { get => backing; private set => backing = value; }")));

        Assert.Contains(new CheckOutcome("public fields", "none", "backing"), result.Checks);
    }
}
