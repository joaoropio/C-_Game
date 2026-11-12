using CodeQuest.Core;
using CodeQuest.Core.Grimoire;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Localization;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Content;

public class ContentTests
{
    private const string NeutralTemplate = """
        public static class Sandbox
        {
            public static void Run()
            {
                // {{PLAYER_CODE}}
            }
        }
        """;

    private const string EmptyValidation = """
        public static class Validation
        {
            public static System.Collections.Generic.IEnumerable<(string Pt, string En)> Run()
            {
                yield break;
            }
        }
        """;

    private static readonly string ContentDirectory = Path.Combine(AppContext.BaseDirectory, "content");

    private static readonly IReadOnlyList<GrimoirePage> Pages =
        GrimoireRepository.Load(Path.Combine(ContentDirectory, "grimoire"));

    private static readonly IReadOnlyList<Level> Levels =
        LevelRepository.Load(Path.Combine(ContentDirectory, "levels"));

    private static readonly UiTextCatalog Ui = UiTextCatalog.Load(Path.Combine(ContentDirectory, "ui.json"));

    public static TheoryData<string> PageIds()
    {
        var data = new TheoryData<string>();
        foreach (var page in Pages)
        {
            data.Add(page.Id);
        }

        return data;
    }

    public static TheoryData<string> Keys()
    {
        var data = new TheoryData<string>();
        foreach (var key in UiKeys.All)
        {
            data.Add(key);
        }

        return data;
    }

    private static GrimoirePage Find(string id) => Pages.Single(page => page.Id == id);

    [Fact]
    public void EveryLevel_HasGrimoirePage() =>
        Assert.All(Levels, level => Assert.Contains(Pages, page => page.Id == level.GrimoirePageId));

    [Theory]
    [MemberData(nameof(PageIds))]
    public void EveryPage_HasPtAndEnTexts(string id)
    {
        var page = Find(id);
        var texts = new[] { page.Title, page.WhatItIs }.Concat(page.CommonMistakes);

        Assert.InRange(page.CommonMistakes.Count, 2, 3);
        Assert.False(string.IsNullOrWhiteSpace(page.Syntax));
        Assert.All(texts, text =>
        {
            Assert.False(string.IsNullOrWhiteSpace(text.Pt));
            Assert.False(string.IsNullOrWhiteSpace(text.En));
        });
    }

    [Theory]
    [MemberData(nameof(PageIds))]
    public void EveryPage_LearnUrlIsMicrosoftLearn(string id) =>
        Assert.StartsWith("https://learn.microsoft.com/", Find(id).LearnUrl);

    [Theory]
    [MemberData(nameof(PageIds))]
    public async Task EveryPage_ExampleCompiles(string id)
    {
        var request = new RunRequest(NeutralTemplate, Find(id).Example, EmptyValidation);

        var result = await new RoslynCodeRunner().RunAsync(request, Language.En, CancellationToken.None);

        Assert.IsType<Passed>(result);
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void UiText_HasEveryKey(string key) =>
        Assert.False(string.IsNullOrWhiteSpace(Ui.Get(key, Language.En)));

    [Theory]
    [MemberData(nameof(Keys))]
    public void UiText_AllKeysHavePtAndEn(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(Ui.Get(key, Language.Pt)));
        Assert.False(string.IsNullOrWhiteSpace(Ui.Get(key, Language.En)));
    }

    [Fact]
    public void UiText_UsesTheAgreedTexts()
    {
        Assert.Equal("O teu ciclo nunca acaba: verifica a condição.", Ui.Get("level.timedOut", Language.Pt));
        Assert.Equal("O progresso guardado não pôde ser lido. Começaste de novo.", Ui.Get("progress.unreadable", Language.Pt));
    }
}
