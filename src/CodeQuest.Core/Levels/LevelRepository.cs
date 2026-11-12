namespace CodeQuest.Core.Levels;

public static class LevelRepository
{
    public static IReadOnlyList<Level> Load(string levelsDirectory) =>
        Directory.GetDirectories(levelsDirectory)
            .Select(LoadLevel)
            .OrderBy(level => level.World)
            .ThenBy(level => level.Number)
            .ToList();

    private static Level LoadLevel(string folder)
    {
        var name = Path.GetFileName(folder);
        var metadata = ContentJson.Read<LevelMetadata>(RequiredFile(folder, name, "level.json"));
        var template = File.ReadAllText(RequiredFile(folder, name, "Template.cs"));
        EnsureSingleMarker(template, name);

        return new Level(
            metadata.Id,
            metadata.World,
            metadata.Number,
            metadata.Title,
            metadata.Story,
            metadata.Concept,
            template,
            File.ReadAllText(RequiredFile(folder, name, "Starter.cs")),
            File.ReadAllText(RequiredFile(folder, name, "Solution.cs")),
            File.ReadAllText(RequiredFile(folder, name, "Validation.cs")),
            metadata.Hints,
            metadata.MaxStatementsForThreeStars,
            metadata.GrimoirePageId);
    }

    private static string RequiredFile(string folder, string levelName, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException($"Level '{levelName}' is missing file '{fileName}'.", path);
    }

    private static void EnsureSingleMarker(string template, string levelName)
    {
        var markers = template
            .Split('\n')
            .Count(PlayerCodeMarker.IsMarkerLine);

        if (markers != 1)
        {
            throw new InvalidDataException(
                $"Level '{levelName}': Template.cs must contain exactly one '// {{{{PLAYER_CODE}}}}' line, found {markers}.");
        }
    }

    private sealed record LevelMetadata(
        string Id,
        int World,
        int Number,
        LocalizedText Title,
        LocalizedText Story,
        LocalizedText Concept,
        IReadOnlyList<LocalizedText> Hints,
        int MaxStatementsForThreeStars,
        string GrimoirePageId);
}

