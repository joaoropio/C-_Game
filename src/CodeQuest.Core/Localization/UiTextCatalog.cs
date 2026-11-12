namespace CodeQuest.Core.Localization;

public sealed class UiTextCatalog
{
    private readonly IReadOnlyDictionary<string, LocalizedText> _texts;

    private UiTextCatalog(IReadOnlyDictionary<string, LocalizedText> texts) => _texts = texts;

    public static UiTextCatalog Load(string path) =>
        new(ContentJson.Read<Dictionary<string, LocalizedText>>(path));

    public string Get(string key, Language language) =>
        _texts.TryGetValue(key, out var text)
            ? text.Get(language)
            : throw new KeyNotFoundException($"UI text key '{key}' not found.");
}
