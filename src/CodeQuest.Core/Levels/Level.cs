namespace CodeQuest.Core.Levels;

public sealed record Level(
    string Id,
    int World,
    int Number,
    LocalizedText Title,
    LocalizedText Story,
    LocalizedText Concept,
    string Template,
    string StarterCode,
    string Solution,
    string ValidationSource,
    IReadOnlyList<LocalizedText> Hints,
    int MaxStatementsForThreeStars,
    string GrimoirePageId);
