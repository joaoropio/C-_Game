using System.Text.RegularExpressions;
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
            public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
            {
                yield break;
            }
        }
        """;

    private const string ClassScopeTemplate = """
        public static partial class Sandbox
        {
            // {{PLAYER_CODE}}
        }
        """;

    private const string NestedEmptyValidation = """
        public static partial class Sandbox
        {
            public static class Validation
            {
                public static System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
                {
                    yield break;
                }
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

    private static HashSet<string> DeclaredNames(string code) =>
        Regex.Matches(code, @"\b(?:string|int|double|bool|var)\s+(\w+)\s*[=;]")
            .Select(match => match.Groups[1].Value)
            .Concat(MethodNames(code))
            .Concat(ParameterNames(code))
            .ToHashSet();

    private static HashSet<string> ParameterNames(string code) =>
        MethodSignatures(code)
            .SelectMany(signature => signature.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(parameter => parameter.Trim().Split(' ').Last())
            .ToHashSet();

    private static HashSet<string> MethodNames(string code) =>
        MethodSignatures(code).Select(signature => signature.Groups[1].Value).ToHashSet();

    private static MatchCollection MethodSignatures(string code) =>
        Regex.Matches(code, @"static\s+[^(=]+?\s(\w+)\s*\(([^)]*)\)");

    [Fact]
    public void EveryLevelWorld_HasAMapWorldNameAndFloorInBothLanguages()
    {
        Assert.All(Levels.Select(level => level.World).Distinct(), world =>
            Assert.All(new[] { Language.Pt, Language.En }, language =>
            {
                Assert.False(string.IsNullOrWhiteSpace(Ui.Get($"map.world.{world}", language)));
                Assert.False(string.IsNullOrWhiteSpace(Ui.Get($"map.floor.{world}", language)));
            }));
    }

    [Fact]
    public void EveryLevel_HasASafeDecorativeSceneAndALocalizedLabel()
    {
        Assert.NotEmpty(Levels);
        Assert.All(Levels, level =>
        {
            Assert.StartsWith("<svg", level.SceneSvg);
            Assert.Contains("aria-hidden=\"true\"", level.SceneSvg);
            Assert.Empty(SceneSvgValidator.Problems(level.SceneSvg));
            Assert.False(string.IsNullOrWhiteSpace(level.SceneLabel.Pt));
            Assert.False(string.IsNullOrWhiteSpace(level.SceneLabel.En));
        });
    }

    [Fact]
    public void EveryLevel_HasGrimoirePage()
    {
        Assert.NotEmpty(Levels);
        Assert.All(Levels, level => Assert.Contains(Pages, page => page.Id == level.GrimoirePageId));
    }

    [Fact]
    public void EveryExample_DoesNotReuseItsLevelIdentifiers()
    {
        Assert.NotEmpty(Levels);
        Assert.All(Levels, level =>
        {
            var example = Pages.Single(page => page.Id == level.GrimoirePageId).Example;
            var levelNames = DeclaredNames(level.Solution).Union(ParameterNames(level.Template));

            Assert.NotEqual(level.Solution.Trim(), example.Trim());
            Assert.Empty(DeclaredNames(example).Intersect(levelNames));
        });
    }

    [Theory]
    [MemberData(nameof(PageIds))]
    public void EveryPage_HasPtAndEnTexts(string id)
    {
        var page = Find(id);
        var texts = new[] { page.Title, page.WhatItIs, page.Syntax }.Concat(page.CommonMistakes);

        Assert.InRange(page.CommonMistakes.Count, 2, 3);
        Assert.False(string.IsNullOrWhiteSpace(page.Example));
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
        var page = Find(id);
        var request = page.World >= 2
            ? new RunRequest(ClassScopeTemplate, page.Example, NestedEmptyValidation)
            : new RunRequest(NeutralTemplate, page.Example, EmptyValidation);

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
        Assert.Equal("O feitiço demora demasiado: verifica ciclos ou chamadas que nunca acabam.", Ui.Get("level.timedOut", Language.Pt));
        Assert.Equal("O progresso guardado não pôde ser lido. Começaste de novo.", Ui.Get("progress.unreadable", Language.Pt));
    }
}
