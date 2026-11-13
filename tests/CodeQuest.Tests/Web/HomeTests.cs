using Bunit;
using CodeQuest.Core;
using CodeQuest.Core.Progress;
using CodeQuest.Web.Components.Pages;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CodeQuest.Tests.Web;

public sealed class HomeTests : BunitContext
{
    private readonly GameContent Content = TestContextExtensions.LoadContent();

    private async Task<IRenderedComponent<Home>> RenderHomeAsync(PlayerProgress? progress = null)
    {
        this.AddGameServices(savedProgress: progress?.ToJson());
        await Services.GetRequiredService<ProgressStore>().LoadAsync();
        return Render<Home>();
    }

    [Fact]
    public async Task Home_NewPlayer_FirstLevelOpenOthersLocked()
    {
        var cut = await RenderHomeAsync();

        var rooms = cut.FindAll("li.room");
        Assert.Equal(Content.Levels.Count, rooms.Count);
        var link = Assert.Single(cut.FindAll("li.room a"));
        Assert.Equal($"/level/{Content.Levels[0].Id}", link.GetAttribute("href"));
        Assert.Contains("Mago", link.TextContent);
        Assert.Equal(rooms.Count - 1, cut.FindAll("li.room.locked").Count);
    }

    [Fact]
    public async Task Home_CompletedLevel_ShowsStars()
    {
        var progress = new PlayerProgress();
        progress.RecordPass(Content.Levels[0].Id, stars: 2);

        var cut = await RenderHomeAsync(progress);

        var first = cut.FindAll("li.room")[0];
        Assert.Equal(2, first.QuerySelectorAll(".star.filled").Length);
        Assert.Equal(3, first.QuerySelectorAll(".star").Length);
        Assert.Contains("done", first.ClassName);
        Assert.Equal("2 de 3 estrelas", first.QuerySelector("[role=img]")!.GetAttribute("aria-label"));
        Assert.Equal(2, cut.FindAll("li.room a").Count);
    }

    [Fact]
    public async Task Home_LockedLevel_HasNoLinkButSaysLocked()
    {
        var cut = await RenderHomeAsync();

        var locked = cut.FindAll("li.room.locked")[0];
        Assert.Empty(locked.QuerySelectorAll("a"));
        Assert.Contains("Trancada", locked.TextContent);
    }

    [Fact]
    public async Task Home_NewPlayer_FirstRoomIsCurrentAndLabelled()
    {
        var cut = await RenderHomeAsync();

        var current = Assert.Single(cut.FindAll("li.room.current"));
        Assert.Equal(cut.FindAll("li.room")[0].OuterHtml, current.OuterHtml);
        Assert.Contains("Sala atual", current.TextContent);
        Assert.Empty(cut.FindAll("li.room.done"));
    }

    [Fact]
    public async Task Home_CurrentRoom_IsFirstUnlockedNotCompleted()
    {
        var progress = new PlayerProgress();
        progress.RecordPass(Content.Levels[0].Id, stars: 3);
        progress.RecordPass(Content.Levels[1].Id, stars: 1);

        var cut = await RenderHomeAsync(progress);

        var rooms = cut.FindAll("li.room");
        Assert.Equal(2, cut.FindAll("li.room.done").Count);
        Assert.Contains("current", rooms[2].ClassName);
        Assert.Single(cut.FindAll("li.room.current"));
        Assert.Contains("Sala atual", rooms[2].TextContent);
        Assert.DoesNotContain("Sala atual", rooms[0].TextContent);
        Assert.Contains("locked", rooms[3].ClassName);
    }

    [Fact]
    public async Task Home_AllCompleted_HasNoCurrentRoom()
    {
        var progress = new PlayerProgress();
        foreach (var level in Content.Levels)
        {
            progress.RecordPass(level.Id, stars: 3);
        }

        var cut = await RenderHomeAsync(progress);

        Assert.Empty(cut.FindAll("li.room.current"));
        Assert.Equal(Content.Levels.Count, cut.FindAll("li.room a").Count);
    }

    [Fact]
    public async Task Home_ShowsWorldTitleAndStarTotal()
    {
        var progress = new PlayerProgress();
        progress.RecordPass(Content.Levels[0].Id, stars: 3);
        progress.RecordPass(Content.Levels[1].Id, stars: 2);

        var cut = await RenderHomeAsync(progress);

        Assert.Contains("Mundo 1 · Primeiro andar", cut.Find(".world-floor").TextContent);
        Assert.Equal("As Fundações", cut.Find("h2.world-name").TextContent);
        Assert.Equal($"5 de {Content.Levels.Count * 3} estrelas", cut.Find(".world-stars").TextContent.Trim());
    }

    [Fact]
    public async Task Home_ShowsNextWorldTeaserInsideTheFloor()
    {
        var cut = await RenderHomeAsync();

        Assert.Contains("em breve", cut.Find(".floor .next-world").TextContent);
    }

    [Fact]
    public async Task Home_Corridor_IsDecorativeSvgWithPath()
    {
        var cut = await RenderHomeAsync();

        var corridor = cut.Find("svg.corridor");
        Assert.Equal("true", corridor.GetAttribute("aria-hidden"));
        Assert.StartsWith("M", cut.Find("svg.corridor path.corridor-path").GetAttribute("d"));
    }

    [Fact]
    public async Task Home_Trail_StopsAtTheCurrentRoom()
    {
        var progress = new PlayerProgress();
        progress.RecordPass(Content.Levels[0].Id, stars: 3);

        var cut = await RenderHomeAsync(progress);

        var trail = cut.Find("svg.corridor path.trail").GetAttribute("d")!;
        Assert.Equal(1, trail.Split(" C ").Length - 1);
    }

    [Fact]
    public async Task Home_Trail_CoversWholeCorridorWhenAllDone()
    {
        var progress = new PlayerProgress();
        foreach (var level in Content.Levels)
        {
            progress.RecordPass(level.Id, stars: 3);
        }

        var cut = await RenderHomeAsync(progress);

        Assert.Equal(
            cut.Find("svg.corridor path.corridor-path").GetAttribute("d"),
            cut.Find("svg.corridor path.trail").GetAttribute("d"));
    }

    [Fact]
    public async Task Home_LaterWorldWithAllRoomsLocked_HasNoTrail()
    {
        var progress = new PlayerProgress();
        foreach (var level in Content.Levels.Take(3))
        {
            progress.RecordPass(level.Id, stars: 3);
        }

        var cut = await RenderTwoWorldsAsync(progress);

        var worlds = cut.FindAll("section.world");
        Assert.Equal(2, worlds.Count);
        Assert.Single(worlds[0].QuerySelectorAll("path.trail"));
        Assert.Empty(worlds[1].QuerySelectorAll("path.trail"));
        Assert.Equal(2, worlds[1].QuerySelectorAll("li.room.locked").Length);
    }

    [Fact]
    public async Task Home_LaterWorldFirstRoomCurrent_HasNoTrailYet()
    {
        var progress = new PlayerProgress();
        foreach (var level in Content.Levels.Take(4))
        {
            progress.RecordPass(level.Id, stars: 3);
        }

        var cut = await RenderTwoWorldsAsync(progress);

        var second = cut.FindAll("section.world")[1];
        Assert.Empty(second.QuerySelectorAll("path.trail"));
        Assert.Single(second.QuerySelectorAll("li.room.current"));
    }

    private async Task<IRenderedComponent<Home>> RenderTwoWorldsAsync(PlayerProgress progress)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cq-two-worlds-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Path.Combine(AppContext.BaseDirectory, "content"), directory);
        foreach (var id in new[] { "05-arrays", "06-strings" })
        {
            var file = Path.Combine(directory, "levels", id, "level.json");
            File.WriteAllText(file, File.ReadAllText(file).Replace("\"world\": 1", "\"world\": 2"));
        }

        var ui = Path.Combine(directory, "ui.json");
        File.WriteAllText(ui, File.ReadAllText(ui).Replace(
            "\"map.world.1\":",
            "\"map.world.2\": { \"pt\": \"Mundo dois\", \"en\": \"World two\" },\n  \"map.world.1\":"));

        this.AddGameServices(savedProgress: progress.ToJson());
        Services.AddSingleton(new GameContent(directory));
        await Services.GetRequiredService<ProgressStore>().LoadAsync();
        return Render<Home>();
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    [Fact]
    public async Task Home_EnglishLanguage_UsesEnglishTexts()
    {
        this.AddGameServices();
        await Services.GetRequiredService<ProgressStore>().LoadAsync();
        await Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En);

        var cut = Render<Home>();

        Assert.Equal("The Foundations", cut.Find("h2.world-name").TextContent);
        Assert.Contains("Current room", cut.Find("li.room.current").TextContent);
        Assert.Contains("Locked", cut.FindAll("li.room.locked")[0].TextContent);
    }

    [Fact]
    public void Home_ProgressNotLoadedYet_RendersNoRooms()
    {
        this.AddGameServices();

        var cut = Render<Home>();

        Assert.Empty(cut.FindAll("li.room"));
    }

    [Fact]
    public async Task Home_ProgressLoadedLater_ShowsRooms()
    {
        this.AddGameServices();
        var cut = Render<Home>();

        await Services.GetRequiredService<ProgressStore>().LoadAsync();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("li.room")));
    }
}
