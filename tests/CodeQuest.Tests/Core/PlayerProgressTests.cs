using CodeQuest.Core;
using CodeQuest.Core.Grimoire;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Progress;

namespace CodeQuest.Tests.Core;

public class PlayerProgressTests
{
    private static readonly LocalizedText Text = new("t", "t");

    private static Level MakeLevel(string id, int number, string pageId) =>
        new(id, 1, number, Text, Text, Text, "", "", "", "", [], 4, pageId);

    private static GrimoirePage MakePage(string id) =>
        new(id, 1, Text, Text, "", "", [], "");

    private static readonly IReadOnlyList<Level> Levels =
    [
        MakeLevel("1-1", 1, "variables"),
        MakeLevel("1-2", 2, "types"),
    ];

    [Fact]
    public void NewProgress_OnlyFirstLevelUnlocked()
    {
        var progress = new PlayerProgress();

        Assert.True(progress.IsUnlocked(Levels, "1-1"));
        Assert.False(progress.IsUnlocked(Levels, "1-2"));
    }

    [Fact]
    public void RecordPass_UnlocksNextLevel()
    {
        var progress = new PlayerProgress();

        progress.RecordPass("1-1", 3);

        Assert.True(progress.IsCompleted("1-1"));
        Assert.True(progress.IsUnlocked(Levels, "1-2"));
    }

    [Fact]
    public void RecordPass_KeepsBestStars()
    {
        var progress = new PlayerProgress();

        progress.RecordPass("1-1", 3);
        progress.RecordPass("1-1", 1);

        Assert.Equal(3, progress.StarsFor("1-1"));
    }

    [Fact]
    public void CanSeeSolution_FalseAfterTwoFailures()
    {
        var progress = new PlayerProgress();

        progress.RecordFailure("1-1");
        progress.RecordFailure("1-1");

        Assert.False(progress.CanSeeSolution("1-1"));
    }

    [Fact]
    public void CanSeeSolution_TrueAfterThreeFailures()
    {
        var progress = new PlayerProgress();

        progress.RecordFailure("1-1");
        progress.RecordFailure("1-1");
        progress.RecordFailure("1-1");

        Assert.True(progress.CanSeeSolution("1-1"));
    }

    [Fact]
    public void ToJson_FromJson_RoundTrips()
    {
        var progress = new PlayerProgress();
        progress.RecordFailure("1-1");
        progress.RecordFailure("1-1");
        progress.RecordFailure("1-1");
        progress.RecordHintUsed("1-1");
        progress.RecordSolutionSeen("1-1");
        progress.RecordPass("1-1", 2);
        progress.MarkPageViewed("loops");

        var result = PlayerProgress.FromJson(progress.ToJson());

        Assert.False(result.WasUnreadable);
        Assert.Equal(2, result.Progress.StarsFor("1-1"));
        Assert.True(result.Progress.IsCompleted("1-1"));
        Assert.True(result.Progress.UsedHint("1-1"));
        Assert.True(result.Progress.SawSolution("1-1"));
        Assert.True(result.Progress.CanSeeSolution("1-1"));
        Assert.True(result.Progress.IsPageAvailable(MakePage("loops"), Levels));
    }

    [Fact]
    public void ToJson_IncludesVersion1()
    {
        using var document = System.Text.Json.JsonDocument.Parse(new PlayerProgress().ToJson());

        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void FromJson_Null_ReturnsEmptyAndReadable()
    {
        var result = PlayerProgress.FromJson(null);

        Assert.False(result.WasUnreadable);
        Assert.False(result.Progress.IsCompleted("1-1"));
    }

    [Fact]
    public void FromJson_Corrupted_ReturnsEmptyAndUnreadable()
    {
        var result = PlayerProgress.FromJson("{nope");

        Assert.True(result.WasUnreadable);
        Assert.False(result.Progress.IsCompleted("1-1"));
    }

    [Fact]
    public void FromJson_UnknownVersion_ReturnsEmptyAndUnreadable()
    {
        var result = PlayerProgress.FromJson("""{"version":99,"levels":{"1-1":{"completed":true}}}""");

        Assert.True(result.WasUnreadable);
        Assert.False(result.Progress.IsCompleted("1-1"));
    }

    [Fact]
    public void FromJson_MissingVersion_ReturnsEmptyAndUnreadable()
    {
        var result = PlayerProgress.FromJson("""{"levels":{}}""");

        Assert.True(result.WasUnreadable);
    }

    [Theory]
    [InlineData("""{"version":1,"levels":null}""")]
    [InlineData("""{"version":1,"viewedPages":null}""")]
    [InlineData("""{"version":1,"levels":{"1-1":null}}""")]
    [InlineData("""{"version":1,"viewedPages":[null]}""")]
    public void FromJson_StructurallyCorrupt_ReturnsEmptyAndUnreadable(string json)
    {
        var result = PlayerProgress.FromJson(json);

        Assert.True(result.WasUnreadable);
        Assert.False(result.Progress.IsCompleted("1-1"));
    }

    [Fact]
    public void IsPageAvailable_PageOfUnlockedLevel()
    {
        var progress = new PlayerProgress();

        Assert.True(progress.IsPageAvailable(MakePage("variables"), Levels));
    }

    [Fact]
    public void IsPageAvailable_LockedButViewedPageStaysAvailable()
    {
        var progress = new PlayerProgress();
        progress.MarkPageViewed("types");

        Assert.True(progress.IsPageAvailable(MakePage("types"), Levels));
    }

    [Fact]
    public void IsPageAvailable_LockedAndNotViewed_False()
    {
        var progress = new PlayerProgress();

        Assert.False(progress.IsPageAvailable(MakePage("types"), Levels));
    }
}
