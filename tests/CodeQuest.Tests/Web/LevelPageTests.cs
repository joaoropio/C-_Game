using Bunit;
using Bunit.TestDoubles;
using CodeQuest.Core;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Progress;
using CodeQuest.Core.Running;
using CodeQuest.Web.Components.Pages;
using CodeQuest.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodeQuest.Tests.Web;

public sealed class LevelPageTests : BunitContext
{
    private const string EditorCode = "return 1;";
    private const string FocusCall = "Blazor._internal.domWrapper.focus";

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
        editor.SetupVoid("insert", _ => true).SetVoidResult();
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

    private static string ReferenceOf(IRenderedComponent<LevelPage> cut, string selector) =>
        cut.Find(selector).GetAttribute("blazor:elementReference")!;

    private void AssertLastFocused(string reference)
    {
        var focus = JSInterop.Invocations.Last(call => call.Identifier == FocusCall);
        Assert.Equal(reference, ((ElementReference)focus.Arguments[0]!).Id);
    }

    private void AssertNothingFocused() =>
        Assert.DoesNotContain(JSInterop.Invocations, call => call.Identifier == FocusCall);

    private string LastNavigation() => Services.GetRequiredService<BunitNavigationManager>().History.First().Uri;

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

        await RunAsync(cut, new ChecksFailed([new CheckOutcome("x = 1", "2", "3")]));

        Assert.Equal("deu 3 · esperado 2", cut.Find(".case-detail").TextContent);
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

        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars, []));

        Assert.NotEmpty(cut.FindAll(".door.open"));
        Assert.Equal(3, cut.FindAll(".result-stars .star.filled").Count);
        Assert.Equal("3 de 3 estrelas", cut.Find(".result-stars").GetAttribute("aria-label"));
        Assert.Equal(3, Saved().StarsFor(First.Id));
    }

    [Fact]
    public async Task Run_Passed_OpensModalVictoryDialog()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars, []));

        var dialog = cut.Find(".victory");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        var heading = cut.Find($"#{dialog.GetAttribute("aria-labelledby")}");
        Assert.Equal("Nível concluído!", heading.TextContent);
    }

    [Fact]
    public async Task Victory_Open_FocusesPrimaryButton()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new Passed(1, []));

        AssertLastFocused(ReferenceOf(cut, ".victory button.next"));
    }

    [Fact]
    public async Task Victory_Next_NavigatesToNextLevel()
    {
        var cut = await RenderLevelAsync(First);
        await RunAsync(cut, new Passed(1, []));

        Assert.Contains("Próximo nível", cut.Find(".victory button.next").TextContent);
        await cut.Find(".victory button.next").ClickAsync(new());

        Assert.Equal($"/level/{content.Levels[1].Id}", LastNavigation());
    }

    [Fact]
    public async Task Victory_Stay_ClosesDialogAndKeepsEditor()
    {
        var cut = await RenderLevelAsync(First);
        await RunAsync(cut, new Passed(1, []));

        Assert.Contains("Ficar aqui", cut.Find(".victory button.stay").TextContent);
        await cut.Find(".victory button.stay").ClickAsync(new());

        Assert.Empty(cut.FindAll(".victory"));
        Assert.Single(cut.FindAll(".editor-frame"));
        Assert.Single(editor.Invocations["create"]);
        Assert.DoesNotContain(editor.Invocations, call => call.Identifier == "dispose");
        Assert.Empty(Services.GetRequiredService<BunitNavigationManager>().History);
    }

    [Fact]
    public async Task Victory_Escape_ClosesDialogAndKeepsEditor()
    {
        var cut = await RenderLevelAsync(First);
        await RunAsync(cut, new Passed(1, []));

        await cut.Find(".victory button.next").KeyDownAsync(Key.Escape);

        Assert.Empty(cut.FindAll(".victory"));
        Assert.Single(editor.Invocations["create"]);
        Assert.DoesNotContain(editor.Invocations, call => call.Identifier == "dispose");
    }

    [Fact]
    public async Task Victory_MakesPageBehindInertUntilClosed()
    {
        var cut = await RenderLevelAsync(First);
        Assert.False(cut.Find(".page-content").HasAttribute("inert"));
        await RunAsync(cut, new Passed(1, []));

        Assert.True(cut.Find(".page-content").HasAttribute("inert"));
        Assert.Empty(cut.FindAll(".page-content .victory"));
        Assert.Single(cut.FindAll(".page-content .editor-frame"));

        await cut.Find(".victory button.stay").ClickAsync(new());

        Assert.False(cut.Find(".page-content").HasAttribute("inert"));
        Assert.Single(editor.Invocations["create"]);
        Assert.DoesNotContain(editor.Invocations, call => call.Identifier == "dispose");
    }

    [Fact]
    public async Task Victory_Escape_RemovesInert()
    {
        var cut = await RenderLevelAsync(First);
        await RunAsync(cut, new Passed(1, []));

        await cut.Find(".victory button.next").KeyDownAsync(Key.Escape);

        Assert.False(cut.Find(".page-content").HasAttribute("inert"));
    }

    [Fact]
    public async Task Victory_Stay_ReturnsFocusToRunButton()
    {
        var cut = await RenderLevelAsync(First);
        var runReference = ReferenceOf(cut, "button.run");
        await RunAsync(cut, new Passed(1, []));

        await cut.Find(".victory button.stay").ClickAsync(new());

        AssertLastFocused(runReference);
    }

    [Fact]
    public async Task Victory_Escape_ReturnsFocusToRunButton()
    {
        var cut = await RenderLevelAsync(First);
        var runReference = ReferenceOf(cut, "button.run");
        await RunAsync(cut, new Passed(1, []));

        await cut.Find(".victory button.next").KeyDownAsync(Key.Escape);

        AssertLastFocused(runReference);
    }

    [Fact]
    public async Task Victory_OtherKey_KeepsDialogOpen()
    {
        var cut = await RenderLevelAsync(First);
        await RunAsync(cut, new Passed(1, []));

        await cut.Find(".victory button.next").KeyDownAsync(Key.Enter);

        Assert.Single(cut.FindAll(".victory"));
    }

    [Fact]
    public async Task Run_FailingAfterVictory_KeepsDialogClosed()
    {
        var cut = await RenderLevelAsync(First);
        await RunAsync(cut, new Passed(1, []));
        await cut.Find(".victory button.stay").ClickAsync(new());

        await RunAsync(cut, new TimedOut());

        Assert.Empty(cut.FindAll(".victory"));
    }

    [Fact]
    public async Task Run_Failure_FocusesResultBlock()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new ChecksFailed([new CheckOutcome("x = 1", "2", "3")]));

        Assert.Equal("-1", cut.Find(".result-block").GetAttribute("tabindex"));
        Assert.Equal("deu 3 · esperado 2", cut.Find(".result-block .case-detail").TextContent);
        AssertLastFocused(ReferenceOf(cut, ".result-block"));
    }

    [Fact]
    public async Task Run_Failure_FocusesResultOnlyOnceAndNotWhileRunning()
    {
        var cut = await RenderLevelAsync(First);
        var gate = new TaskCompletionSource<RunResult>();
        runner.Pending = gate.Task;

        var click = cut.Find("button.run").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.True(cut.Find("button.run").HasAttribute("disabled")));
        AssertNothingFocused();

        gate.SetResult(new TimedOut());
        await click;
        cut.WaitForAssertion(() => JSInterop.VerifyFocusAsyncInvoke());
        await cut.Find("button.hint").ClickAsync(new());

        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task Run_Passed_DoesNotFocusResultBlock()
    {
        var cut = await RenderLevelAsync(First);

        await RunAsync(cut, new Passed(1, []));

        Assert.Empty(cut.FindAll(".result-block"));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task Run_Cancelled_DoesNotFocusResultBlock()
    {
        progress.RecordPass(First.Id, 1);
        var cut = await RenderLevelAsync(First);
        var gate = new TaskCompletionSource<RunResult>();
        runner.Pending = gate.Task;
        var click = cut.Find("button.run").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotNull(runner.Request));

        cut.Render(parameters => parameters.Add(page => page.Id, content.Levels[1].Id));
        gate.SetResult(new TimedOut());
        await click;

        AssertNothingFocused();
    }

    [Fact]
    public async Task Run_Passed_World1WithHint_GivesTwoOrThreeStars()
    {
        var cut = await RenderLevelAsync(First);
        await cut.Find("button.hint").ClickAsync(new());

        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars + 1, []));
        Assert.Equal(2, cut.FindAll(".result-stars .star.filled").Count);

        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars, []));
        Assert.Equal(3, cut.FindAll(".result-stars .star.filled").Count);
    }

    [Fact]
    public async Task Tabs_AreARealTablistWithStoryOpenByDefault()
    {
        var cut = await RenderLevelAsync(First);

        Assert.Equal("tablist", cut.Find("[role=tablist]").GetAttribute("role"));
        Assert.Equal("Secções do nível", cut.Find("[role=tablist]").GetAttribute("aria-label"));
        var tabs = cut.FindAll("[role=tab]");
        Assert.Equal(["História", "Conceito", "Dicas"], tabs.Select(tab => tab.TextContent.Trim()));
        Assert.Equal(["true", "false", "false"], tabs.Select(tab => tab.GetAttribute("aria-selected")));
        Assert.All(tabs, tab => Assert.Equal("button", tab.TagName.ToLowerInvariant()));
        var panels = cut.FindAll("[role=tabpanel]");
        Assert.Equal(3, panels.Count);
        for (var index = 0; index < 3; index++)
        {
            Assert.Equal(panels[index].Id, tabs[index].GetAttribute("aria-controls"));
            Assert.Equal(tabs[index].Id, panels[index].GetAttribute("aria-labelledby"));
        }

        Assert.Equal([false, true, true], panels.Select(panel => panel.HasAttribute("hidden")));
    }

    [Fact]
    public async Task Tabs_ClickingConceptShowsConceptPanel()
    {
        var cut = await RenderLevelAsync(First);

        await cut.FindAll("[role=tab]")[1].ClickAsync(new());

        Assert.Equal(["false", "true", "false"], cut.FindAll("[role=tab]").Select(tab => tab.GetAttribute("aria-selected")));
        Assert.False(cut.Find(".concept").HasAttribute("hidden"));
        Assert.True(cut.Find(".story").HasAttribute("hidden"));
    }

    [Fact]
    public async Task Tabs_OnlySelectedTabIsInTheTabOrder()
    {
        var cut = await RenderLevelAsync(First);

        Assert.Equal(["0", "-1", "-1"], cut.FindAll("[role=tab]").Select(tab => tab.GetAttribute("tabindex")));

        await cut.FindAll("[role=tab]")[1].ClickAsync(new());

        Assert.Equal(["-1", "0", "-1"], cut.FindAll("[role=tab]").Select(tab => tab.GetAttribute("tabindex")));
    }

    [Theory]
    [InlineData(0, "ArrowRight", 1)]
    [InlineData(2, "ArrowRight", 0)]
    [InlineData(1, "ArrowLeft", 0)]
    [InlineData(0, "ArrowLeft", 2)]
    [InlineData(1, "Home", 0)]
    [InlineData(0, "End", 2)]
    public async Task Tabs_ArrowKeysHomeAndEndMoveSelectionAndFocus(int from, string key, int expected)
    {
        var cut = await RenderLevelAsync(First);
        await cut.FindAll("[role=tab]")[from].ClickAsync(new());

        await cut.FindAll("[role=tab]")[from].KeyDownAsync(new KeyboardEventArgs { Key = key });

        var tabs = cut.FindAll("[role=tab]");
        Assert.Equal(expected, tabs.ToList().FindIndex(tab => tab.GetAttribute("aria-selected") == "true"));
        Assert.Equal("0", tabs[expected].GetAttribute("tabindex"));
        Assert.Contains(JSInterop.Invocations, call => call.Identifier == FocusCall);
    }

    [Fact]
    public async Task Tabs_KeyboardMovesFocusToTheTabThatBecomesSelected()
    {
        var cut = await RenderLevelAsync(First);
        string FocusedAfter(int pressedOn, string key)
        {
            cut.FindAll("[role=tab]")[pressedOn].KeyDown(new KeyboardEventArgs { Key = key });
            var focus = JSInterop.Invocations.Last(call => call.Identifier == FocusCall);
            return ((ElementReference)focus.Arguments[0]!).Id;
        }

        var last = FocusedAfter(0, "End");
        var first = FocusedAfter(2, "Home");
        var middle = FocusedAfter(0, "ArrowRight");

        Assert.Equal(3, new[] { first, middle, last }.Distinct().Count());
        Assert.Equal(last, FocusedAfter(1, "ArrowRight"));
        Assert.Equal(first, FocusedAfter(2, "ArrowRight"));
        Assert.Equal(last, FocusedAfter(0, "ArrowLeft"));
        Assert.Equal(middle, FocusedAfter(2, "ArrowLeft"));
    }

    [Fact]
    public async Task Tabs_OtherKeysDoNotChangeSelection()
    {
        var cut = await RenderLevelAsync(First);

        await cut.FindAll("[role=tab]")[0].KeyDownAsync(new KeyboardEventArgs { Key = "a" });

        Assert.Equal(["true", "false", "false"], cut.FindAll("[role=tab]").Select(tab => tab.GetAttribute("aria-selected")));
        AssertNothingFocused();
    }

    [Fact]
    public async Task HintsTab_ExplainsHowToGetHintsWhenNoneRevealed()
    {
        var cut = await RenderLevelAsync(First);

        await cut.FindAll("[role=tab]")[2].ClickAsync(new());

        Assert.Contains("botão Pista", cut.Find(".hints-panel").TextContent);
        Assert.Empty(cut.FindAll(".hints li"));
    }

    [Fact]
    public async Task Hint_SwitchesToHintsTabAndCountsRevealedHints()
    {
        var cut = await RenderLevelAsync(First);

        await cut.Find("button.hint").ClickAsync(new());

        Assert.Equal("true", cut.FindAll("[role=tab]")[2].GetAttribute("aria-selected"));
        Assert.Equal("Dicas (1)", cut.FindAll("[role=tab]")[2].TextContent.Trim());
        Assert.False(cut.Find(".hints-panel").HasAttribute("hidden"));
    }

    [Fact]
    public async Task Vignette_IsALabelledImageWithTheLevelsOwnDecorativeSvg()
    {
        var cut = await RenderLevelAsync(First);

        var figure = cut.Find(".vignette");
        Assert.Equal("img", figure.GetAttribute("role"));
        Assert.Equal(First.SceneLabel.Pt, figure.GetAttribute("aria-label"));
        Assert.Equal("true", figure.QuerySelector("svg")!.GetAttribute("aria-hidden"));

        await cut.InvokeAsync(() => Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En));

        Assert.Equal(First.SceneLabel.En, cut.Find(".vignette").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task RunButton_ShowsDecorativeStarIcon()
    {
        var cut = await RenderLevelAsync(First);

        var button = cut.Find("button.run");
        Assert.Contains("btn-primary", button.ClassList);
        Assert.Equal("true", button.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        Assert.Contains("Lançar feitiço", button.TextContent);
    }

    [Fact]
    public async Task Spellbook_HoldsEditorAndActionsAndResultsFollowIt()
    {
        var cut = await RenderLevelAsync(First);
        await RunAsync(cut, new TimedOut());

        Assert.Equal("Livro de feitiços", cut.Find(".spellbook h2").TextContent);
        Assert.Single(cut.FindAll(".spellbook .editor-frame"));
        Assert.Single(cut.FindAll(".spellbook .actions button.run"));
        Assert.Equal("Resultado do feitiço", cut.Find(".result-block h2").TextContent);
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
    public async Task Hint_AnnouncesTheRevealedHintInAPoliteLiveRegion()
    {
        var cut = await RenderLevelAsync(First);
        Assert.Equal("status", cut.Find(".hint-announcer").GetAttribute("role"));
        Assert.Equal("", cut.Find(".hint-announcer").TextContent);

        await cut.Find("button.hint").ClickAsync(new());

        Assert.Equal(First.Hints[0].Pt, cut.Find(".hint-announcer").TextContent);
        Assert.Equal("polite", cut.Find(".hint-announcer").GetAttribute("aria-live"));
    }

    [Fact]
    public async Task Hint_LastHintRemovesButtonAndMovesFocusToHintsPanel()
    {
        var cut = await RenderLevelAsync(First);
        var panelReference = ReferenceOf(cut, ".hints-panel");

        foreach (var _ in First.Hints)
        {
            await cut.Find("button.hint").ClickAsync(new());
        }

        Assert.Equal("-1", cut.Find(".hints-panel").GetAttribute("tabindex"));
        AssertLastFocused(panelReference);
    }

    [Fact]
    public async Task Header_ShowsWorldAndRoomCrumb()
    {
        var cut = await RenderLevelAsync(First);

        var worldRooms = content.Levels.Count(level => level.World == First.World);
        Assert.Equal($"Mundo {First.World} · Sala {First.Number} de {worldRooms}", cut.Find(".crumb").TextContent);
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

        await RunAsync(cut, new Passed(1, []));

        Assert.Empty(cut.FindAll(".result-stars .star.filled"));
        Assert.Equal("0 de 3 estrelas", cut.Find(".result-stars").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task LastLevel_Passed_NextGoesToMap()
    {
        CompleteAllBefore(Last);
        var cut = await RenderLevelAsync(Last);

        await RunAsync(cut, new Passed(1, []));

        Assert.Contains("Voltar ao mapa", cut.Find(".victory button.next").TextContent);
        await cut.Find(".victory button.next").ClickAsync(new());

        Assert.Equal("/", LastNavigation());
    }

    [Fact]
    public async Task RunButton_DisabledWhileRunning()
    {
        var cut = await RenderLevelAsync(First);
        var gate = new TaskCompletionSource<RunResult>();
        runner.Pending = gate.Task;

        var click = cut.Find("button.run").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.True(cut.Find("button.run").HasAttribute("disabled")));
        Assert.Contains("A lançar", cut.Find("button.run").TextContent);

        gate.SetResult(new TimedOut());
        await click;

        cut.WaitForAssertion(() => Assert.False(cut.Find("button.run").HasAttribute("disabled")));
    }

    [Fact]
    public async Task Run_FinishingAfterLevelChange_IsDiscarded()
    {
        progress.RecordPass(First.Id, 1);
        var cut = await RenderLevelAsync(First);
        var gate = new TaskCompletionSource<RunResult>();
        runner.Pending = gate.Task;
        var click = cut.Find("button.run").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotNull(runner.Request));
        var second = content.Levels[1];

        cut.Render(parameters => parameters.Add(page => page.Id, second.Id));
        gate.SetResult(new Passed(1, []));
        await click;

        Assert.DoesNotContain(JSInterop.Invocations, call => call.Identifier == TestContextExtensions.SetItem);
        var current = Services.GetRequiredService<ProgressStore>().Current!;
        Assert.False(current.IsCompleted(second.Id));
        Assert.Equal(1, current.StarsFor(First.Id));
        Assert.Empty(cut.FindAll(".victory"));
        Assert.Empty(cut.FindAll(".result"));
    }

    [Fact]
    public async Task Run_WhilePending_IgnoresSecondRun()
    {
        var cut = await RenderLevelAsync(First);
        var gate = new TaskCompletionSource<RunResult>();
        runner.Pending = gate.Task;
        var button = cut.Find("button.run");

        var first = button.ClickAsync(new());
        var second = cut.InvokeAsync(() => button.Click());
        gate.SetResult(new TimedOut());
        await first;
        await second;

        Assert.Equal(1, runner.Calls);
        Assert.Single(JSInterop.Invocations[TestContextExtensions.SetItem]);
    }

    [Fact]
    public async Task SolutionButton_HiddenOnceLevelCompleted()
    {
        var cut = await RenderLevelAsync(First);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await RunAsync(cut, new TimedOut());
        }

        await RunAsync(cut, new Passed(1, []));

        Assert.Empty(cut.FindAll("button.solution"));
    }

    [Fact]
    public async Task Victory_ShowsBestSavedStars()
    {
        var cut = await RenderLevelAsync(First);
        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars, []));

        await RunAsync(cut, new Passed(First.MaxStatementsForThreeStars + 1, []));

        Assert.Equal(3, cut.FindAll(".result-stars .star.filled").Count);
        Assert.Equal("dialog", cut.Find(".victory").GetAttribute("role"));
    }

    [Fact]
    public async Task NavigatingToAnotherLevel_ResetsHintsResultAndStars()
    {
        progress.RecordPass(First.Id, 1);
        var cut = await RenderLevelAsync(First);
        await cut.Find("button.hint").ClickAsync(new());
        await RunAsync(cut, new Passed(1, []));

        cut.Render(parameters => parameters.Add(page => page.Id, content.Levels[1].Id));

        Assert.Empty(cut.FindAll(".hints li"));
        Assert.Empty(cut.FindAll(".result"));
        Assert.Empty(cut.FindAll(".victory"));
    }

    [Fact]
    public async Task GrimoireCopy_InsertsIntoEditor()
    {
        var cut = await RenderLevelAsync(First);
        await cut.Find("button.grimoire-open").ClickAsync(new());

        await cut.Find("aside button.copy").ClickAsync(new());

        var insert = Assert.Single(editor.Invocations["insert"]);
        Assert.Equal(content.FindPage(First.GrimoirePageId)!.Example, insert.Arguments[1]);
    }

    [Fact]
    public async Task OpeningGrimoire_KeepsEditorCode()
    {
        var cut = await RenderLevelAsync(First);
        Assert.Empty(cut.FindAll("aside"));

        await cut.Find("button.grimoire-open").ClickAsync(new());

        Assert.Single(cut.FindAll("aside"));
        Assert.Single(cut.FindAll(".editor-frame"));
        Assert.Single(editor.Invocations["create"]);
        Assert.DoesNotContain(editor.Invocations, call => call.Identifier == "dispose");

        await cut.Find("aside button.close").ClickAsync(new());

        Assert.Empty(cut.FindAll("aside"));
        Assert.Single(editor.Invocations["create"]);
    }

    [Fact]
    public async Task NavigatingToAnotherLevel_ClosesGrimoire()
    {
        progress.RecordPass(First.Id, 1);
        var cut = await RenderLevelAsync(First);
        await cut.Find("button.grimoire-open").ClickAsync(new());
        Assert.Single(cut.FindAll("aside"));

        cut.Render(parameters => parameters.Add(page => page.Id, content.Levels[1].Id));

        Assert.Empty(cut.FindAll("aside"));
    }

    [Fact]
    public async Task LanguageChange_RerendersInEnglish()
    {
        var cut = await RenderLevelAsync(First);

        await cut.InvokeAsync(() => Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En));

        Assert.Contains(First.Story.En, cut.Find(".story").TextContent);
        Assert.Contains("Cast spell", cut.Find("button.run").TextContent);
    }

    private sealed class FakeRunner : ICodeRunner
    {
        public RunResult Result { get; set; } = new TimedOut();

        public Task<RunResult>? Pending { get; set; }

        public RunRequest? Request { get; private set; }

        public int Calls { get; private set; }

        public Task<RunResult> RunAsync(RunRequest request, Language language, CancellationToken cancellationToken)
        {
            Request = request;
            Calls++;
            return Pending ?? Task.FromResult(Result);
        }
    }
}
