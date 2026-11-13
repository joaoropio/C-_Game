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
    public async Task Home_ShowsNextWorldTeaser()
    {
        var cut = await RenderHomeAsync();

        Assert.Contains("em breve", cut.Find(".next-world").TextContent);
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
