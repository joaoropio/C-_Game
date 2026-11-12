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
    public void RealContent_HasTheFirstThreeLevelsInOrder() =>
        Assert.Equal(["01-variables", "02-operators", "03-conditions"], Levels.Take(3).Select(level => level.Id));

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

    [Fact]
    public async Task Level1_MissingVariable_ExplainsWhichOne()
    {
        var level = Find("01-variables");

        var result = Assert.IsType<CompilationFailed>(await Run(level, level.StarterCode));

        Assert.Contains(result.Errors, error => error.Explanation?.En.Contains("health") == true);
    }
}
