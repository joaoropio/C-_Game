namespace CodeQuest.Core.Grimoire;

public static class GrimoireRepository
{
    public static IReadOnlyList<GrimoirePage> Load(string grimoireDirectory) =>
        Directory.GetFiles(grimoireDirectory, "*.json")
            .Select(ContentJson.Read<GrimoirePage>)
            .OrderBy(page => page.World)
            .ThenBy(page => page.Id, StringComparer.Ordinal)
            .ToList();
}
