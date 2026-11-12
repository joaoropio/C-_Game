using CodeQuest.Core;
using CodeQuest.Core.Localization;

namespace CodeQuest.Tests.Core;

public class UiTextCatalogTests
{
    private static string WriteCatalog(TempDirectory dir) =>
        dir.Write("ui.json", """{ "run": { "pt": "Executar", "en": "Run" } }""");

    [Fact]
    public void UiTextCatalog_Get_ReturnsTextForLanguage()
    {
        using var dir = new TempDirectory();
        var catalog = UiTextCatalog.Load(WriteCatalog(dir));

        Assert.Equal("Executar", catalog.Get("run", Language.Pt));
        Assert.Equal("Run", catalog.Get("run", Language.En));
    }

    [Fact]
    public void UiTextCatalog_Get_MissingKey_Throws()
    {
        using var dir = new TempDirectory();
        var catalog = UiTextCatalog.Load(WriteCatalog(dir));

        var ex = Assert.Throws<KeyNotFoundException>(() => catalog.Get("missing", Language.En));

        Assert.Contains("missing", ex.Message);
    }
}
