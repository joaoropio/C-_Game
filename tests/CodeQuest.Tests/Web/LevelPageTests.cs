using Bunit;
using Bunit.TestDoubles;
using CodeQuest.Core;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Progress;
using CodeQuest.Core.Running;
using CodeQuest.Web.Components.Pages;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CodeQuest.Tests.Web;

public sealed class LevelPageTests : BunitContext
{
    private const string EditorCode = "return 1;";

    private readonly GameContent content = TestContextExtensions.LoadContent();
    private readonly FakeRunner runner = new();
    private readonly BunitJSModuleInterop editor;
    private PlayerProgress progress = new();

    public LevelPageTests()
    {
        this.AddGameServicesWithoutStorage();
        Services.AddSingleton<ICodeRunner>(runner);
        editor = JSInterop.SetupModule("./Components/CodeEditor.razor.js");
        editor.Setup<int?>("create", _ => true).SetResult(1);
        editor.Setup<string>("getValue", _ => true).SetResult(EditorCode);
        editor.SetupVoid("setValue", _ => true).SetVoidResult();
        editor.SetupVoid("setErrors", _ => true).SetVoidResult();
        editor.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private Level First => content.Levels[0];

    private Level Last => content.Levels[^1];

    private async Task<IRenderedComponent<LevelPage>> RenderLevelAsync(Level level)
    {
        this.SetupStorage(null, progress.ToJson());
        await Services.GetRequiredService<ProgressStore>().LoadAsync();
        return Render<LevelPage>(parameters => parameters.Add(page => page.Id, level.Id));
    }

    private void CompleteAllBefore(Level level)
    {
        foreach (var earlier in content.Levels.TakeWhile(candidate => candidate.Id != level.Id))
        {
            progress.RecordPass(earlier.Id, 1);
        }
    }

    private async Task RunAsync(IRenderedComponent<LevelPage> cut, RunResult result)
    {
        runner.Result = result;
        await cut.Find("button.run").ClickAsync(new());
    }

    private void AssertRedirectedHome() =>
        Assert.Equal("/", Assert.Single(Services.GetRequiredService<BunitNavigationManager>().History).Uri);

    private PlayerProgress Saved() => PlayerProgress.FromJson(this.LastSavedValue(TestContextExtensions.ProgressKey)).Progress;

    [Fact]
    public async Task LockedLevel_RedirectsHome()
    {
        await RenderLevelAsync(content.Levels[1]);

        AssertRedirectedHome();
    }

    [Fact]
    public async Task UnknownLevel_RedirectsHome()
    {
        this.SetupStorage(null, null);
        await Services.GetRequiredService<ProgressStore>().LoadAsync();

        Render<LevelPage>(parameters => parameters.Add(page => page.Id, "nope"));

        AssertRedirectedHome();
    }

    [Fact]
    public async Task UnlockedLevel_DoesNotRedirect()
    {
        await RenderLevelAsync(First);

        Assert.Empty(Services.GetRequiredService<BunitNavigationManager>().History);
    }

    [Fact]
    public async Task ShowsStoryConceptAndStarter()
    {
        var cut = await RenderLevelAsync(First);

        Assert.Contains(First.Title.Pt, cut.Markup);
        Assert.Contains(First.Story.Pt, cut.Find(".story").TextContent);
        Assert.Contains(First.Concept.Pt, cut.Find(".concept").TextContent);
        var create = Assert.Single(editor.Invocations["create"]);
        Assert.Equal(First.StarterCode, create.Arguments[1]);
    }

    [Fact]
    public async Task Run_SendsEditorCodeToRunner()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new TimedOut());

        Assert.Equal(new RunRequest(First.Template, EditorCode, First.ValidationSource), runner.Request);
    }

    [Fact]
    public async Task Run_Failure_RecordsFailureAndSaves()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new ChecksFailed([new LocalizedText("Falhou.", "Failed.")]));

        Assert.Contains("Falhou.", cut.Markup);
        Assert.Empty(cut.FindAll(".door.open"));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await RunAsync(cut, new TimedOut());
        }

        Assert.True(Saved().CanSeeSolution(First.Id));
    }

    [Fact]
    public async Task Run_CompilationFailed_ShowsErrorsInEditor()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new CompilationFailed([new CompilationError(2, 3, "CS1002: ; expected", null)]));

        var call = Assert.Single(editor.Invocations["setErrors"]);
        Assert.NotNull(call.Arguments[1]);
        Assert.Contains("CS1002", cut.Markup);
    }

    [Fact]
    public async Task Run_Passed_ShowsStarsAndOpensDoor()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars));

        Assert.NotEmpty(cut.FindAll(".door.open"));
        Assert.Equal(3, cut.FindAll(".result-stars .star.filled").Count);
        Assert.Equal("3 de 3 estrelas", cut.Find(".result-stars").GetAttribute("aria-label"));
        Assert.Equal(3, Saved().StarsFor(First.Id));
        Assert.Equal($"/level/{content.Levels[1].Id}", cut.Find("a.next").GetAttribute("href"));
    }

    [Fact]
    public async Task Run_Passed_World1WithHint_GivesTwoOrThreeStars()
    {
        var cut = await RenderLevelAsync(First);
        await cut.Find("button.hint").ClickAsync(new());

        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars + 1));
        Assert.Equal(2, cut.FindAll(".result-stars .star.filled").Count);

        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars));
        Assert.Equal(3, cut.FindAll(".result-stars .star.filled").Count);
    }

    [Fact]
    public async Task Hint_ShowsNextHintAndRecordsUse()
    {
        var cut = await RenderLevelAsync(First);
        Assert.Empty(cut.FindAll(".hints li"));

        await cut.Find("button.hint").ClickAsync(new());

        Assert.Equal(First.Hints[0].Pt, Assert.Single(cut.FindAll(".hints li")).TextContent);
        Assert.True(Saved().UsedHint(First.Id));

        await cut.Find("button.hint").ClickAsync(new());

        Assert.Equal(2, cut.FindAll(".hints li").Count);
        Assert.Equal(First.Hints[1].Pt, cut.FindAll(".hints li")[1].TextContent);
    }

    [Fact]
    public async Task Hint_ButtonDisappearsAfterLastHint()
    {
        var cut = await RenderLevelAsync(First);

        foreach (var _ in First.Hints)
        {
            await cut.Find("button.hint").ClickAsync(new());
        }

        Assert.Empty(cut.FindAll("button.hint"));
    }

    [Fact]
    public async Task SolutionButton_HiddenBeforeThreeFailures()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new TimedOut());
        await RunAsync(cut, new TimedOut());

        Assert.Empty(cut.FindAll("button.solution"));
    }

    [Fact]
    public async Task SolutionButton_AfterThreeFailures_PutsSolutionInEditorAndZeroStars()
    {
        var cut = await RenderLevelAsync(First);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await RunAsync(cut, new TimedOut());
        }

        await cut.Find("button.solution").ClickAsync(new());

        var set = Assert.Single(editor.Invocations["setValue"]);
        Assert.Equal(First.Solution, set.Arguments[1]);
        Assert.True(Saved().SawSolution(First.Id));

        await RunAsync(cut, new Passed(1));

        Assert.Empty(cut.FindAll(".result-stars .star.filled"));
        Assert.Equal("0 de 3 estrelas", cut.Find(".result-stars").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task LastLevel_Passed_NextGoesToMap()
    {
        CompleteAllBefore(Last);
        var cut = await RenderLevelAsync(Last);

        await RunAsync(cut, new Passed(1));

        Assert.Equal("/", cut.Find("a.next").GetAttribute("href"));
        Assert.Contains("Voltar ao mapa", cut.Find("a.next").TextContent);
    }

    [Fact]
    public async Task RunButton_DisabledWhileRunning()
    {
        var cut = await RenderLevelAsync(First);
        var gate = new TaskCompletionSource<RunResult>();
        runner.Pending = gate.Task;

        var click = cut.Find("button.run").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.True(cut.Find("button.run").HasAttribute("disabled")));
        Assert.Contains("A executar", cut.Find("button.run").TextContent);

        gate.SetResult(new TimedOut());
        await click;

        cut.WaitForAssertion(() => Assert.False(cut.Find("button.run").HasAttribute("disabled")));
    }

    [Fact]
    public async Task LanguageChange_RerendersInEnglish()
    {
        var cut = await RenderLevelAsync(First);

        await cut.InvokeAsync(() => Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En));

        Assert.Contains(First.Story.En, cut.Find(".story").TextContent);
        Assert.Contains("Run", cut.Find("button.run").TextContent);
    }

    private sealed class FakeRunner : ICodeRunner
    {
        public RunResult Result { get; set; } = new TimedOut();

        public Task<RunResult>? Pending { get; set; }

        public RunRequest? Request { get; private set; }

        public Task<RunResult> RunAsync(RunRequest request, Language language, CancellationToken cancellationToken)
        {
            Request = request;
            return Pending ?? Task.FromResult(Result);
        }
    }
}
