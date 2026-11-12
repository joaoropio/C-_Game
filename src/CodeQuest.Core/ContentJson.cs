using System.Text.Json;

namespace CodeQuest.Core;

internal static class ContentJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static T Read<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidDataException($"File '{path}' is empty or null.");
    }
}
