using CodeQuest.Core.Grimoire;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Localization;

namespace CodeQuest.Web.Services;

public sealed class GameContent(string contentDirectory)
{
    public IReadOnlyList<Level> Levels { get; } = LevelRepository.Load(Path.Combine(contentDirectory, "levels"));

    public IReadOnlyList<GrimoirePage> Pages { get; } = GrimoireRepository.Load(Path.Combine(contentDirectory, "grimoire"));

    public UiTextCatalog Ui { get; } = UiTextCatalog.Load(Path.Combine(contentDirectory, "ui.json"));

    public Level? FindLevel(string id) => Levels.FirstOrDefault(level => level.Id == id);

    public GrimoirePage? FindPage(string id) => Pages.FirstOrDefault(page => page.Id == id);
}
