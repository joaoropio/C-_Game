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

    private int WorldOneRooms => Content.Levels.Count(level => level.World == 1);

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
        Assert.Equal($"5 de {Content.Levels.Count(level => level.World == 1) * 3} estrelas", cut.Find(".world-stars").TextContent.Trim());
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
        foreach (var level in Content.Levels.Take(WorldOneRooms - 1))
        {
            progress.RecordPass(level.Id, stars: 3);
        }

        var cut = await RenderTwoWorldsAsync(progress);

        var worlds = cut.FindAll("section.world");
        Assert.Equal(Content.Levels.Select(level => level.World).Distinct().Count(), worlds.Count);
        Assert.Single(worlds[0].QuerySelectorAll("path.trail"));
        Assert.Empty(worlds[1].QuerySelectorAll("path.trail"));
        Assert.NotEmpty(worlds[1].QuerySelectorAll("li.room"));
        Assert.Equal(worlds[1].QuerySelectorAll("li.room").Length, worlds[1].QuerySelectorAll("li.room.locked").Length);
    }

    [Fact]
    public async Task Home_LaterWorldFirstRoomCurrent_HasNoTrailYet()
    {
        var progress = new PlayerProgress();
        foreach (var level in Content.Levels.Take(WorldOneRooms))
        {
            progress.RecordPass(level.Id, stars: 3);
        }

        var cut = await RenderTwoWorldsAsync(progress);

        var second = cut.FindAll("section.world")[1];
        Assert.Empty(second.QuerySelectorAll("path.trail"));
        Assert.Single(second.QuerySelectorAll("li.room.current"));
    }

    private async Task<IRenderedComponent<Home>> RenderTwoWorldsAsync(PlayerProgress progress, Language language = Language.Pt)
    {
        this.AddGameServices(savedProgress: progress.ToJson());
        await Services.GetRequiredService<ProgressStore>().LoadAsync();
        await Services.GetRequiredService<UiText>().SetLanguageAsync(language);
        return Render<Home>();
    }

    [Fact]
    public async Task Home_RoomNumbers_RestartInEachWorld()
    {
        var cut = await RenderTwoWorldsAsync(new PlayerProgress());

        var worlds = cut.FindAll("section.world");
        Assert.Equal(6, worlds[0].QuerySelectorAll("li.room").Length);
        Assert.Equal(5, worlds[1].QuerySelectorAll("li.room").Length);
        Assert.StartsWith("1-1", worlds[0].QuerySelectorAll(".room-number")[0].TextContent);
        Assert.StartsWith("2-1", worlds[1].QuerySelectorAll(".room-number")[0].TextContent);
        Assert.StartsWith("2-5", worlds[1].QuerySelectorAll(".room-number")[4].TextContent);
    }

    [Fact]
    public async Task Home_SecondWorld_IsASecondFloorBelowTheFirst()
    {
        var cut = await RenderTwoWorldsAsync(new PlayerProgress());

        var worlds = cut.FindAll("section.world");
        Assert.Equal(Content.Levels.Select(level => level.World).Distinct().Count(), worlds.Count);
        Assert.Equal("Mundo 1 · Primeiro andar", worlds[0].QuerySelector(".world-floor")!.TextContent);
        Assert.Equal("As Fundações", worlds[0].QuerySelector("h2.world-name")!.TextContent);
        Assert.Equal("Mundo 2 · Segundo andar", worlds[1].QuerySelector(".world-floor")!.TextContent);
        Assert.Equal("A Biblioteca Arcana", worlds[1].QuerySelector("h2.world-name")!.TextContent);
    }

    [Fact]
    public async Task Home_SecondFloorRooms_AreAllLockedUntilWorldOneIsDone()
    {
        var progress = new PlayerProgress();
        foreach (var level in Content.Levels.Take(WorldOneRooms - 1))
        {
            progress.RecordPass(level.Id, stars: 3);
        }

        var cut = await RenderTwoWorldsAsync(progress);

        var second = cut.FindAll("section.world")[1];
        Assert.NotEmpty(second.QuerySelectorAll("li.room"));
        Assert.Equal(second.QuerySelectorAll("li.room").Length, second.QuerySelectorAll("li.room.locked").Length);
        Assert.Empty(second.QuerySelectorAll("li.room a"));
    }

    [Fact]
    public async Task Home_WorldOneDone_FirstRoomOfSecondFloorIsCurrent()
    {
        var progress = new PlayerProgress();
        foreach (var level in Content.Levels.Take(WorldOneRooms))
        {
            progress.RecordPass(level.Id, stars: 3);
        }

        var cut = await RenderTwoWorldsAsync(progress);

        var worlds = cut.FindAll("section.world");
        Assert.Empty(worlds[0].QuerySelectorAll("li.room.current"));
        var rooms = worlds[1].QuerySelectorAll("li.room");
        Assert.Contains("current", rooms[0].ClassList);
        Assert.Contains("locked", rooms[1].ClassList);
    }

    [Fact]
    public async Task Home_StarTotals_AreCountedPerWorld()
    {
        var progress = new PlayerProgress();
        progress.RecordPass(Content.Levels[0].Id, stars: 3);
        progress.RecordPass(Content.Levels[1].Id, stars: 2);
        foreach (var level in Content.Levels.Skip(2).Take(2))
        {
            progress.RecordPass(level.Id, stars: 1);
        }

        var cut = await RenderTwoWorldsAsync(progress);

        var totals = cut.FindAll(".world-stars").Select(element => element.TextContent.Trim()).ToArray();
        var untouchedWorlds = Content.Levels.GroupBy(level => level.World).Skip(1).Select(world => $"0 de {world.Count() * 3} estrelas");
        Assert.Equal([$"7 de {WorldOneRooms * 3} estrelas", .. untouchedWorlds], totals);
    }

    [Fact]
    public async Task Home_NextWorldTeaser_AppearsOnlyAfterTheLastWorld()
    {
        var cut = await RenderTwoWorldsAsync(new PlayerProgress());

        var worlds = cut.FindAll("section.world");
        Assert.All(worlds.SkipLast(1), world => Assert.Empty(world.QuerySelectorAll(".next-world")));
        Assert.Contains("Mundo 4 · Genéricos, lambdas e LINQ — em breve", worlds.Last().QuerySelector(".next-world")!.TextContent);
    }

    [Fact]
    public async Task Home_TwoWorlds_EnglishFloorAndWorldNames()
    {
        var cut = await RenderTwoWorldsAsync(new PlayerProgress(), Language.En);

        var worlds = cut.FindAll("section.world");
        Assert.Equal("World 1 · First floor", worlds[0].QuerySelector(".world-floor")!.TextContent);
        Assert.Equal("World 2 · Second floor", worlds[1].QuerySelector(".world-floor")!.TextContent);
        Assert.Equal("The Arcane Library", worlds[1].QuerySelector("h2.world-name")!.TextContent);
        Assert.Contains("World 4 · Generics, lambdas and LINQ — coming soon", worlds.Last().QuerySelector(".next-world")!.TextContent);
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

    [Fact]
    public async Task Home_ThirdWorld_IsAThirdFloorWithItsOwnNumbering()
    {
        var cut = await RenderTwoWorldsAsync(new PlayerProgress());

        var third = cut.FindAll("section.world")[2];
        Assert.Equal("Mundo 3 · Terceiro andar", third.QuerySelector(".world-floor")!.TextContent);
        Assert.Equal("O Bestiário Encantado", third.QuerySelector("h2.world-name")!.TextContent);
        Assert.StartsWith("3-1", third.QuerySelectorAll(".room-number")[0].TextContent);
        Assert.Equal(third.QuerySelectorAll("li.room").Length, third.QuerySelectorAll("li.room.locked").Length);
    }
}
