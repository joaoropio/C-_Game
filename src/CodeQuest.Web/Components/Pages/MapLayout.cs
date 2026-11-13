using System.Globalization;
using System.Text;

namespace CodeQuest.Web.Components.Pages;

/// <summary>Zigzag positions of the rooms on a world floor, plus the corridor path that joins them.</summary>
public sealed class MapLayout
{
    private const int Margin = 120;
    private const int Step = 220;
    private const int LowRow = 420;
    private const int HighRow = 230;
    private const int FloorHeight = 760;

    private MapLayout(IReadOnlyList<RoomPosition> rooms, int width, string pathData)
    {
        Rooms = rooms;
        Width = width;
        PathData = pathData;
    }

    public IReadOnlyList<RoomPosition> Rooms { get; }

    public int Width { get; }

    public int Height => FloorHeight;

    public string PathData { get; }

    public static MapLayout For(int roomCount)
    {
        var width = (2 * Margin) + (Math.Max(roomCount, 1) - 1) * Step;
        var rooms = new List<RoomPosition>(roomCount);
        for (var index = 0; index < roomCount; index++)
        {
            var x = Margin + (index * Step);
            var y = index % 2 == 0 ? LowRow : HighRow;
            rooms.Add(new RoomPosition(x, y, 100.0 * x / width, 100.0 * y / FloorHeight));
        }

        return new MapLayout(rooms, width, BuildPath(rooms));
    }

    private static string BuildPath(IReadOnlyList<RoomPosition> rooms)
    {
        if (rooms.Count == 0)
        {
            return string.Empty;
        }

        var path = new StringBuilder(FormattableString.Invariant($"M{rooms[0].X} {rooms[0].Y}"));
        for (var i = 1; i < rooms.Count; i++)
        {
            var from = rooms[i - 1];
            var to = rooms[i];
            var middle = (from.X + to.X) / 2;
            path.Append(FormattableString.Invariant($" C {middle} {from.Y}, {middle} {to.Y}, {to.X} {to.Y}"));
        }

        return path.ToString();
    }
}

public readonly record struct RoomPosition(int X, int Y, double LeftPercent, double TopPercent)
{
    public string Style => string.Create(CultureInfo.InvariantCulture, $"--x: {LeftPercent:0.###}%; --y: {TopPercent:0.###}%");
}
