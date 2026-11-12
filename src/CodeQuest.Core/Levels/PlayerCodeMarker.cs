namespace CodeQuest.Core.Levels;

public static class PlayerCodeMarker
{
    private const string Marker = "//{{PLAYER_CODE}}";

    public static bool IsMarkerLine(string line) =>
        string.Concat(line.Where(c => !char.IsWhiteSpace(c))) == Marker;
}
