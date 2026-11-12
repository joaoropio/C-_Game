using CodeQuest.Core.Grimoire;

namespace CodeQuest.Tests.Core;

public class GrimoireRepositoryTests
{
    [Fact]
    public void GrimoireRepository_Load_ReadsAllPages()
    {
        using var dir = new TempDirectory();
        dir.Write("variables.json", """
            {
              "id": "variables", "world": 1,
              "title": { "pt": "Variáveis", "en": "Variables" },
              "whatItIs": { "pt": "Caixas", "en": "Boxes" },
              "syntax": "int x = 1;",
              "example": "var y = 2;",
              "commonMistakes": [ { "pt": "Erro", "en": "Mistake" } ],
              "learnUrl": "https://example.com/variables"
            }
            """);
        dir.Write("loops.json", """
            {
              "id": "loops", "world": 2,
              "title": { "pt": "Ciclos", "en": "Loops" },
              "whatItIs": { "pt": "Repetir", "en": "Repeat" },
              "syntax": "for (;;) { }",
              "example": "while (true) { }",
              "commonMistakes": [],
              "learnUrl": "https://example.com/loops"
            }
            """);

        var pages = GrimoireRepository.Load(dir.Path);

        Assert.Equal(["loops", "variables"], pages.Select(p => p.Id).Order());
        var variables = pages.Single(p => p.Id == "variables");
        Assert.Equal(1, variables.World);
        Assert.Equal("Variables", variables.Title.En);
        Assert.Equal("Caixas", variables.WhatItIs.Pt);
        Assert.Equal("int x = 1;", variables.Syntax);
        Assert.Equal("var y = 2;", variables.Example);
        Assert.Equal("Mistake", Assert.Single(variables.CommonMistakes).En);
        Assert.Equal("https://example.com/variables", variables.LearnUrl);
    }
}
