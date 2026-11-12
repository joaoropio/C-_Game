using CodeQuest.Core.Levels;

namespace CodeQuest.Tests.Core;

public class LevelRepositoryTests
{
    private const string Template = "class T\n{\n    // {{PLAYER_CODE}}\n}\n";

    private static void WriteLevel(TempDirectory dir, string folder, int world, int number, string template = Template)
    {
        dir.Write($"{folder}/level.json", $$"""
            {
              "id": "{{folder}}", "world": {{world}}, "number": {{number}},
              "title": { "pt": "Título", "en": "Title" },
              "story": { "pt": "História", "en": "Story" },
              "concept": { "pt": "Conceito", "en": "Concept" },
              "hints": [ { "pt": "Dica 1", "en": "Hint 1" }, { "pt": "Dica 2", "en": "Hint 2" } ],
              "maxStatementsForThreeStars": 3,
              "grimoirePageId": "page-{{folder}}"
            }
            """);
        dir.Write($"{folder}/Template.cs", template);
        dir.Write($"{folder}/Starter.cs", "// starter");
        dir.Write($"{folder}/Solution.cs", "var x = 1;");
        dir.Write($"{folder}/Validation.cs", "public static class Validation { }");
    }

    [Fact]
    public void Load_ReturnsLevelsOrderedByWorldThenNumber()
    {
        using var dir = new TempDirectory();
        WriteLevel(dir, "b-middle", world: 1, number: 2);
        WriteLevel(dir, "a-last", world: 2, number: 1);
        WriteLevel(dir, "c-first", world: 1, number: 1);

        var levels = LevelRepository.Load(dir.Path);

        Assert.Equal(["c-first", "b-middle", "a-last"], levels.Select(l => l.Id));
    }

    [Fact]
    public void Load_ReadsTemplateStarterSolutionAndValidationFiles()
    {
        using var dir = new TempDirectory();
        WriteLevel(dir, "01-a", world: 1, number: 1);

        var level = Assert.Single(LevelRepository.Load(dir.Path));

        Assert.Equal(Template, level.Template);
        Assert.Equal("// starter", level.StarterCode);
        Assert.Equal("var x = 1;", level.Solution);
        Assert.Equal("public static class Validation { }", level.ValidationSource);
        Assert.Equal("Title", level.Title.En);
        Assert.Equal("Conceito", level.Concept.Pt);
        Assert.Equal("Story", level.Story.En);
        Assert.Equal(["Dica 1", "Dica 2"], level.Hints.Select(h => h.Pt));
        Assert.Equal(3, level.MaxStatementsForThreeStars);
        Assert.Equal("page-01-a", level.GrimoirePageId);
    }

    [Fact]
    public void Load_TemplateWithoutMarker_Throws()
    {
        using var dir = new TempDirectory();
        WriteLevel(dir, "01-a", 1, 1, template: "class T { }");

        var ex = Assert.Throws<InvalidDataException>(() => LevelRepository.Load(dir.Path));

        Assert.Contains("01-a", ex.Message);
    }

    [Fact]
    public void Load_TemplateWithTwoMarkers_Throws()
    {
        using var dir = new TempDirectory();
        WriteLevel(dir, "01-a", 1, 1, template: "// {{PLAYER_CODE}}\n//{{PLAYER_CODE}}\n");

        Assert.Throws<InvalidDataException>(() => LevelRepository.Load(dir.Path));
    }

    [Fact]
    public void Load_MissingFile_ThrowsWithLevelFolderName()
    {
        using var dir = new TempDirectory();
        WriteLevel(dir, "01-a", 1, 1);
        File.Delete(Path.Combine(dir.Path, "01-a", "Solution.cs"));

        var ex = Assert.Throws<FileNotFoundException>(() => LevelRepository.Load(dir.Path));

        Assert.Contains("01-a", ex.Message);
    }
}


