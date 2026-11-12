using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace CodeQuest.E2ETests;

[Collection(GameServerCollection.Name)]
public class GameJourneyTests(GameServerFixture server)
{
    private const string Level1Solution = "string name = \"Merlin\";\nint health = 100;\ndouble mana = 50.5;\nbool isAlive = true;\n";
    private const float Timeout = 15_000;
    private const string HotReloadProbePath = "/_framework/dotnet-browser-tools/";

    [Fact]
    public async Task Level1_TypeSolution_DoorOpensAndStarsShown()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var problems = CollectBrowserProblems(page);

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, Level1Solution);
        await RunAsync(page);

        await Expect(page.Locator(".victory .door.open")).ToBeAttachedAsync(new() { Timeout = Timeout });
        await Expect(page.Locator(".victory .result-stars")).ToHaveAttributeAsync("aria-label", "3 de 3 estrelas", new() { Timeout = Timeout });
        await Expect(page.Locator(".victory .star.filled")).ToHaveCountAsync(3, new() { Timeout = Timeout });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public async Task CompileError_IsUnderlinedInEditor()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, "string name = \"Merlin\";\nint health = ;\n");
        await RunAsync(page);

        await Expect(page.Locator(".monaco-editor .squiggly-error").First).ToBeAttachedAsync(new() { Timeout = Timeout });
        await Expect(page.Locator(".result")).ToContainTextAsync("Linha 2", new() { Timeout = Timeout });

        var markerLines = await page.EvaluateAsync<int[]>(
            "monaco.editor.getModelMarkers({}).map(marker => marker.startLineNumber)");
        Assert.Equal([2], markerLines);

        // The model markers give the line number; this checks the squiggle is really painted on that line:
        // its overlay row (.view-overlays) and the view line holding "int health" share the same top offset.
        var squiggleTop = await page.EvaluateAsync<string>(
            "[...document.querySelectorAll('.view-overlays > div')].find(row => row.querySelector('.squiggly-error')).style.top");
        var statementTop = await page.EvaluateAsync<string>(
            "[...document.querySelectorAll('.view-lines .view-line')].find(line => line.textContent.replace(/\\u00a0/g, ' ').includes('int health')).style.top");
        Assert.Equal(statementTop, squiggleTop);    }

    [Fact]
    public async Task InfiniteLoop_ShowsTimedOutMessage()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, Level1Solution + "while (true) { }\n");
        await RunAsync(page);

        await Expect(page.Locator(".result")).ToContainTextAsync("O teu ciclo nunca acaba", new() { Timeout = Timeout });
    }

    [Fact]
    public async Task Reload_KeepsProgress()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, Level1Solution);
        await RunAsync(page);
        await Expect(page.Locator(".victory")).ToBeVisibleAsync(new() { Timeout = Timeout });
        await page.WaitForFunctionAsync("localStorage.getItem('cq.progress')?.includes('01-variables')");

        await page.GotoAsync(server.BaseUrl);
        await page.ReloadAsync();

        await Expect(page.Locator("a.room-title[href='/level/02-operators']")).ToBeVisibleAsync(new() { Timeout = Timeout });
        await Expect(page.Locator(".room .star.filled")).ToHaveCountAsync(3, new() { Timeout = Timeout });
    }

    [Fact]
    public async Task SwitchToEnglish_PersistsAfterReload()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(server.BaseUrl);
        await Expect(page.Locator("h1")).ToHaveTextAsync("Mapa da masmorra", new() { Timeout = Timeout });
        await page.GetByRole(AriaRole.Button, new() { Name = "EN", Exact = true }).ClickAsync();
        await Expect(page.Locator("h1")).ToHaveTextAsync("Dungeon map", new() { Timeout = Timeout });

        await page.ReloadAsync();

        await Expect(page.Locator("h1")).ToHaveTextAsync("Dungeon map", new() { Timeout = Timeout });
        Assert.Equal("en", await page.EvaluateAsync<string>("localStorage.getItem('cq.language')"));
    }

    [Fact]
    public async Task CorruptedProgress_ShowsWarning()
    {
        await using var context = await server.Browser.NewContextAsync();
        await context.AddInitScriptAsync("localStorage.setItem('cq.progress', '{not valid json')");
        var page = await context.NewPageAsync();

        await page.GotoAsync(server.BaseUrl);

        await Expect(page.Locator(".notice[role=alert]"))
            .ToContainTextAsync("O progresso guardado não pôde ser lido", new() { Timeout = Timeout });
        await Expect(page.Locator(".map .room:not(.locked)")).ToHaveCountAsync(1, new() { Timeout = Timeout });
    }

    [Fact]
    public async Task Grimoire_CopyExample_AppearsInEditor()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await page.Locator(".grimoire-open").ClickAsync();
        await Expect(page.Locator(".grimoire-panel")).ToBeVisibleAsync(new() { Timeout = Timeout });
        await page.Locator(".grimoire-panel .copy").ClickAsync();

        await page.WaitForFunctionAsync(
            "monaco.editor.getEditors()[0].getValue().includes('bool hasKey = false;')",
            null,
            new PageWaitForFunctionOptions { Timeout = Timeout });
    }

    [Fact]
    public async Task CtrlSpace_ShowsNoSuggestions()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, "Con");
        await page.Keyboard.PressAsync("Control+Space");

        // A negative assertion cannot auto-wait for something to appear, so give the widget a short window to show up.
        await page.WaitForTimeoutAsync(750);
        Assert.Equal(0, await page.Locator(".suggest-widget.visible").CountAsync());
        Assert.Equal(0, await page.Locator(".suggest-widget .monaco-list-row").CountAsync());
    }

    private async Task OpenLevelAsync(IPage page, string levelId)
    {
        await page.GotoAsync($"{server.BaseUrl}/level/{levelId}");
        await Expect(page.Locator(".monaco-editor").First).ToBeVisibleAsync(new() { Timeout = Timeout });
    }

    // Monaco auto-closes brackets and quotes, so typing key by key corrupts the code; inserting the text
    // in one go bypasses that. The model value is read back to make sure the editor really holds the code.
    private static async Task TypeCodeAsync(IPage page, string code)
    {
        await page.Locator(".monaco-editor .view-lines").ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.InsertTextAsync(code);
        await page.WaitForFunctionAsync(
            "expected => monaco.editor.getEditors()[0].getValue() === expected",
            code,
            new PageWaitForFunctionOptions { Timeout = Timeout });
    }

    private static Task RunAsync(IPage page) => page.Locator("button.run").ClickAsync();

    // The Development host probes a hot-reload settings file that does not exist; unrelated to the game.
    private static bool IsHotReloadProbe(string? location) => location?.Contains(HotReloadProbePath) == true;

    private static List<string> CollectBrowserProblems(IPage page)
    {
        var problems = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error" && !IsHotReloadProbe(message.Location))
            {
                problems.Add($"console error: {message.Text}");
            }
        };
        page.PageError += (_, error) => problems.Add($"page error: {error}");
        page.RequestFailed += (_, request) => problems.Add($"request failed: {request.Url}");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400 && !response.Url.Contains(HotReloadProbePath))
            {
                problems.Add($"HTTP {response.Status}: {response.Url}");
            }
        };
        return problems;
    }
}


