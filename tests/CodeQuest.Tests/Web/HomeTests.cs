using Bunit;
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
        Assert.Equal(2, cut.FindAll("li.room a").Count);
    }

    [Fact]
    public async Task Home_LockedLevel_HasNoLink()
    {
        var cut = await RenderHomeAsync();

        var locked = cut.FindAll("li.room.locked")[0];
        Assert.Empty(locked.QuerySelectorAll("a"));
        Assert.NotNull(locked.QuerySelector("[role=img]"));
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

