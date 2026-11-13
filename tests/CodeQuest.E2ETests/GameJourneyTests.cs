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
        var browser = CollectBrowserProblems(context, page);

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, Level1Solution);
        await RunAsync(page);

        await Expect(page.Locator(".victory[role=dialog][aria-modal=true]")).ToBeInViewportAsync(new() { Timeout = Timeout });
        await Expect(page.Locator(".victory button.next")).ToBeFocusedAsync(new() { Timeout = Timeout });
        await Expect(page.Locator(".victory .door.open")).ToBeAttachedAsync(new() { Timeout = Timeout });
        await Expect(page.Locator(".victory .result-stars")).ToHaveAttributeAsync("aria-label", "3 de 3 estrelas", new() { Timeout = Timeout });
        await Expect(page.Locator(".victory .star.filled")).ToHaveCountAsync(3, new() { Timeout = Timeout });

        // Monaco starts its worker lazily: require positive proof that workerMain.js was served before trusting the problem list.
        await browser.WorkerLoaded.WaitAsync(TimeSpan.FromMilliseconds(Timeout));
        Assert.True(browser.Problems.Count == 0, string.Join(Environment.NewLine, browser.Problems));
    }

    [Fact]
    public async Task Level1_VictoryDialog_NextLevelGoesToLevel2()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, Level1Solution);
        await RunAsync(page);
        await Expect(page.Locator(".victory")).ToBeInViewportAsync(new() { Timeout = Timeout });
        await page.GetByRole(AriaRole.Button, new() { Name = "Próximo nível" }).ClickAsync();

        await Expect(page).ToHaveURLAsync($"{server.BaseUrl}/level/02-operators", new() { Timeout = Timeout });
        await Expect(page.Locator(".victory")).ToHaveCountAsync(0, new() { Timeout = Timeout });
    }

    [Fact]
    public async Task Level1_VictoryDialog_StayKeepsCode()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, Level1Solution);
        await RunAsync(page);
        await Expect(page.Locator(".victory")).ToBeInViewportAsync(new() { Timeout = Timeout });
        await page.Keyboard.PressAsync("Escape");

        await Expect(page.Locator(".victory")).ToHaveCountAsync(0, new() { Timeout = Timeout });
        Assert.Equal(Level1Solution, await page.EvaluateAsync<string>("monaco.editor.getEditors()[0].getValue()"));
    }

    [Fact]
    public async Task FailingRun_ShowsErrorInViewportWithoutScrolling()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, "string name = \"Merlin\";\nint health = ;\n");
        await RunAsync(page);

        await Expect(page.Locator(".result-block .result")).ToBeInViewportAsync(new() { Ratio = 1, Timeout = Timeout });
    }

    [Fact]
    public async Task Level1_VictoryDialog_KeyboardNeverReachesPageBehind()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, Level1Solution);
        await RunAsync(page);
        await Expect(page.Locator(".victory button.next")).ToBeFocusedAsync(new() { Timeout = Timeout });

        for (var presses = 0; presses < 4; presses++)
        {
            await page.Keyboard.PressAsync("Shift+Tab");
            Assert.False(await page.EvaluateAsync<bool>("document.querySelector('.page-content').contains(document.activeElement)"));
        }
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
        Assert.Equal(statementTop, squiggleTop);
    }

    [Fact]
    public async Task InfiniteLoop_ShowsTimedOutMessage()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, Level1Solution + "while (true) { }\n");
        await RunAsync(page);

        await Expect(page.Locator(".result")).ToContainTextAsync("O feitiço demora demasiado", new() { Timeout = Timeout });
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
        await page.WaitForFunctionAsync(
            "localStorage.getItem('cq.progress')?.includes('01-variables')",
            null,
            new PageWaitForFunctionOptions { Timeout = Timeout });

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
        await page.EvaluateAsync("monaco.editor.getEditors()[0].setPosition({ lineNumber: 1, column: 8 })");
        await page.Locator(".grimoire-open").ClickAsync();
        await Expect(page.Locator(".grimoire-panel")).ToBeVisibleAsync(new() { Timeout = Timeout });
        await page.Locator(".grimoire-panel .copy").ClickAsync();

        await page.WaitForFunctionAsync(
            "monaco.editor.getEditors()[0].getValue().includes('bool hasKey = false;')",
            null,
            new PageWaitForFunctionOptions { Timeout = Timeout });
        var lines = await page.EvaluateAsync<string[]>(
            "monaco.editor.getEditors()[0].getModel().getLinesContent()");
        Assert.Equal("string title = \"Aprendiz\";", lines[0]);
        Assert.Equal("bool hasKey = false;", lines[3]);
        Assert.Equal("string name = \"Merlin\";", lines[4]);
    }

    [Fact]
    public async Task CtrlSpace_ShowsNoSuggestions()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var suggestWidget = page.Locator(".suggest-widget.visible");

        await OpenLevelAsync(page, "01-variables");
        await TypeCodeAsync(page, "Console\nCon");
        await page.Keyboard.PressAsync("Control+Space");

        // Negative check: the widget gets a 500 ms window to show up and must not.
        await Assert.ThrowsAsync<PlaywrightException>(
            () => Expect(suggestWidget).ToBeVisibleAsync(new() { Timeout = 500 }));

        // Positive control: the same selector does catch a suggest widget when one is opened programmatically.
        await page.EvaluateAsync("monaco.editor.getEditors()[0].trigger('test', 'editor.action.triggerSuggest', {})");
        await Expect(suggestWidget).ToBeVisibleAsync(new() { Timeout = Timeout });
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

    private static BrowserWatch CollectBrowserProblems(IBrowserContext context, IPage page)
    {
        var watch = new BrowserWatch();
        page.Console += (_, message) =>
        {
            var failure = message.Type == "error"
                || (message.Type == "warning" && message.Text.Contains("worker", StringComparison.OrdinalIgnoreCase));
            if (failure && !IsHotReloadProbe(message.Location))
            {
                watch.Problems.Add($"console {message.Type}: {message.Text}");
            }
        };
        page.PageError += (_, error) => watch.Problems.Add($"page error: {error}");
        page.RequestFailed += (_, request) => watch.Problems.Add($"request failed: {request.Url}");
        context.Response += (_, response) =>
        {
            if (response.Status >= 400 && !response.Url.Contains(HotReloadProbePath))
            {
                watch.Problems.Add($"HTTP {response.Status}: {response.Url}");
            }

            if (response.Status == 200 && response.Url.Contains("workerMain.js"))
            {
                watch.WorkerLoadedSource.TrySetResult();
            }
        };
        return watch;
    }

    private sealed class BrowserWatch
    {
        public List<string> Problems { get; } = [];

        public TaskCompletionSource WorkerLoadedSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WorkerLoaded => WorkerLoadedSource.Task;
    }
}
