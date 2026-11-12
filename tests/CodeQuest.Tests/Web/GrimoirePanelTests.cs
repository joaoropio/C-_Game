using Bunit;
using CodeQuest.Core;
using CodeQuest.Core.Progress;
using CodeQuest.Web.Components;
using CodeQuest.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CodeQuest.Tests.Web;

public sealed class GrimoirePanelTests : BunitContext
{
    private readonly GameContent content = TestContextExtensions.LoadContent();
    private readonly PlayerProgress progress = new();

    public GrimoirePanelTests()
    {
        this.AddGameServicesWithoutStorage();
        progress.RecordPass(content.Levels[0].Id, 1);
    }

    private string Title(string pageId) => content.FindPage(pageId)!.Title.Pt;

    private async Task<IRenderedComponent<GrimoirePanel>> RenderAsync(
        bool isOpen = true,
        string? initialPageId = "variables",
        Action<string>? onCopy = null,
        Action? onClose = null)
    {
        this.SetupStorage(null, progress.ToJson());
        await Services.GetRequiredService<ProgressStore>().LoadAsync();
        return Render<GrimoirePanel>(parameters => parameters
            .Add(panel => panel.IsOpen, isOpen)
            .Add(panel => panel.InitialPageId, initialPageId)
            .Add(panel => panel.OnCopyExample, EventCallback.Factory.Create<string>(this, onCopy ?? (_ => { })))
            .Add(panel => panel.OnClose, EventCallback.Factory.Create(this, onClose ?? (() => { }))));
    }

    private static IReadOnlyList<string> ListedTitles(IRenderedComponent<GrimoirePanel> cut) =>
        cut.FindAll("nav button").Select(button => button.TextContent.Trim()).ToList();

    [Fact]
    public async Task Closed_RendersNothing()
    {
        var cut = await RenderAsync(isOpen: false);

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public async Task Open_IsLabelledAside()
    {
        var cut = await RenderAsync();

        Assert.Equal("Grimório", cut.Find("aside").GetAttribute("aria-label"));
        Assert.Single(cut.FindAll("h2"));
        Assert.Empty(cut.FindAll("nav[aria-label]"));
    }

    [Fact]
    public async Task Open_ListsOnlyAvailablePages()
    {
        var cut = await RenderAsync();

        Assert.Equal([Title("operators"), Title("variables")], ListedTitles(cut).Order());
        Assert.DoesNotContain(Title("conditions"), ListedTitles(cut));
    }

    [Fact]
    public async Task Open_GroupsByWorld()
    {
        var directory = CopyContentWithWorldTwoPage();
        try
        {
            Services.AddSingleton(new GameContent(directory));
            progress.MarkPageViewed("sample");
            var cut = await RenderAsync();

            var groups = cut.FindAll("nav section");
            Assert.Equal(["Mundo 1", "Mundo 2"], groups.Select(group => group.QuerySelector("h3")!.TextContent.Trim()));
            Assert.Equal(["Sample"], groups[1].QuerySelectorAll("button").Select(button => button.TextContent.Trim()));
            Assert.Equal(2, groups[0].QuerySelectorAll("button").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Open_MovesFocusIntoPanel()
    {
        await RenderAsync();

        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task Closed_DoesNotMoveFocus()
    {
        await RenderAsync(isOpen: false);

        Assert.DoesNotContain(JSInterop.Invocations, call => call.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public async Task AlreadyViewedPage_IsNotSavedAgain()
    {
        progress.MarkPageViewed("variables");

        await RenderAsync(initialPageId: "variables");

        Assert.DoesNotContain(JSInterop.Invocations, call => call.Identifier == TestContextExtensions.SetItem);
    }

    [Fact]
    public async Task InitialPage_IsShownAndMarkedViewed()
    {
        var page = content.FindPage("variables")!;

        var cut = await RenderAsync(initialPageId: "variables");

        Assert.Equal(page.Title.Pt, cut.Find(".page-detail h3").TextContent);
        Assert.Contains(page.WhatItIs.Pt, cut.Find(".page-detail").TextContent);
        Assert.Equal(page.Syntax.Pt, cut.Find(".page-detail pre.syntax code").TextContent);
        Assert.Equal(page.Example, cut.Find(".page-detail pre.example code").TextContent);
        Assert.Equal(page.CommonMistakes.Count, cut.FindAll(".page-detail .mistakes li").Count);
        Assert.Contains("\"viewedPages\":[\"variables\"]", Services.GetRequiredService<ProgressStore>().Current!.ToJson());
        Assert.Contains("\"viewedPages\":[\"variables\"]", this.LastSavedValue(TestContextExtensions.ProgressKey));
    }

    [Fact]
    public async Task Syntax_IsShownInTheCurrentLanguage()
    {
        var page = content.FindPage("variables")!;
        var cut = await RenderAsync(initialPageId: "variables");

        await cut.InvokeAsync(() => Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En));
        cut.Render();

        Assert.Equal(page.Syntax.En, cut.Find(".page-detail pre.syntax code").TextContent);
        Assert.NotEqual(page.Syntax.Pt, page.Syntax.En);
    }

    [Fact]
    public async Task SelectingPage_ShowsItAndMarksViewed()
    {
        var cut = await RenderAsync(initialPageId: "variables");

        await cut.FindAll("nav button").Single(button => button.TextContent.Contains(Title("operators"))).ClickAsync(new());

        Assert.Equal(Title("operators"), cut.Find(".page-detail h3").TextContent);
        Assert.Contains("\"operators\"", this.LastSavedValue(TestContextExtensions.ProgressKey));
    }

    [Fact]
    public async Task Search_FiltersByTitle()
    {
        var cut = await RenderAsync();

        await cut.Find("input[type=search]").InputAsync(new() { Value = "operad" });

        Assert.Equal([Title("operators")], ListedTitles(cut));
    }

    [Fact]
    public async Task Search_MatchesTextOfPage()
    {
        var cut = await RenderAsync();

        await cut.Find("input[type=search]").InputAsync(new() { Value = "virgula" });

        Assert.Equal([Title("variables")], ListedTitles(cut));
    }

    [Fact]
    public async Task Search_IgnoresCaseAndAccents()
    {
        var cut = await RenderAsync();

        await cut.Find("input[type=search]").InputAsync(new() { Value = "VARIAVEIS" });

        Assert.Equal([Title("variables")], ListedTitles(cut));
    }

    [Fact]
    public async Task Search_NoMatch_ShowsEmptyState()
    {
        var cut = await RenderAsync();

        await cut.Find("input[type=search]").InputAsync(new() { Value = "zzzzzz" });

        Assert.Empty(ListedTitles(cut));
        Assert.Equal("Nenhuma página encontrada.", cut.Find(".empty").TextContent);
    }

    [Fact]
    public async Task Copy_RaisesOnCopyExampleWithExample()
    {
        string? copied = null;
        var cut = await RenderAsync(onCopy: example => copied = example);

        await cut.Find("button.copy").ClickAsync(new());

        Assert.Equal(content.FindPage("variables")!.Example, copied);
    }

    [Fact]
    public async Task LearnMore_LinksToLearnUrl()
    {
        var cut = await RenderAsync();

        var link = cut.Find("a.learn-more");

        Assert.Equal(content.FindPage("variables")!.LearnUrl, link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
    }

    [Fact]
    public async Task CloseButtonAndEscape_RaiseOnClose()
    {
        var closed = 0;
        var cut = await RenderAsync(onClose: () => closed++);

        await cut.Find("button.close").ClickAsync(new());
        await cut.Find("input[type=search]").KeyDownAsync(Key.Escape);

        Assert.Equal(2, closed);
    }

    private static string CopyContentWithWorldTwoPage()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "content");
        var target = Path.Combine(Path.GetTempPath(), "cq-content-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }

        File.WriteAllText(Path.Combine(target, "grimoire", "sample.json"), """
            {
              "id": "sample", "world": 2,
              "title": { "pt": "Sample", "en": "Sample" },
              "whatItIs": { "pt": "x", "en": "x" },
              "syntax": { "pt": "s", "en": "s" }, "example": "e",
              "commonMistakes": [],
              "learnUrl": "https://learn.microsoft.com/"
            }
            """);
        return target;
    }
}
