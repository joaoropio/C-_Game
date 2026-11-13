using System.Globalization;
using System.Text;

namespace CodeQuest.Web.Components.Pages;

/// <summary>
/// Positions of the rooms on a world floor, in user units of a fixed-width floor, plus the corridor path that joins them.
/// Rooms fill rows of <see cref="RoomsPerRow"/> in a serpentine (odd rows run right to left), zigzagging up and down.
/// </summary>
public sealed class MapLayout
{
    public const int FloorWidth = 1312;
    public const int RoomsPerRow = 6;
    public const int RoomWidth = 200;
    public const int RoomHeight = 200;
    public const int RowHeight = 440;

    /// <summary>Free band under the last row, for the corner tag.</summary>
    public const int FooterHeight = 80;

    private const int HighOffset = 190;
    private const int LowOffset = 270;
    private const int DoorTop = 60;
    private const int TurnBulge = 90;

    private readonly IReadOnlyList<int> _rowOfRoom;

    private MapLayout(IReadOnlyList<RoomPosition> rooms, IReadOnlyList<int> rowOfRoom)
    {
        Rooms = rooms;
        _rowOfRoom = rowOfRoom;
        Height = (Math.Max(1, (rooms.Count + RoomsPerRow - 1) / RoomsPerRow) * RowHeight) + FooterHeight;
    }

    public IReadOnlyList<RoomPosition> Rooms { get; }

    public int Width => FloorWidth;

    public int Height { get; }

    public double RoomWidthPercent => 100.0 * RoomWidth / FloorWidth;

    /// <summary>Corridor through every room, in order.</summary>
    public string PathData => PathThrough(Rooms.Count - 1);

    public static MapLayout For(int roomCount)
    {
        var slot = (double)FloorWidth / RoomsPerRow;
        var rooms = new List<RoomPosition>(roomCount);
        var rows = new List<int>(roomCount);
        for (var index = 0; index < roomCount; index++)
        {
            var row = index / RoomsPerRow;
            var column = index % RoomsPerRow;
            if (row % 2 == 1)
            {
                column = RoomsPerRow - 1 - column;
            }

            var x = (int)Math.Round(slot * (column + 0.5));
            var y = (row * RowHeight) + (index % 2 == 0 ? LowOffset : HighOffset);
            rooms.Add(new RoomPosition(x, y));
            rows.Add(row);
        }

        return new MapLayout(rooms, rows);
    }

    /// <summary>Corridor from the first room up to and including the room at <paramref name="lastIndex"/>.</summary>
    public string Trail(int lastIndex) => lastIndex < 1 ? string.Empty : PathThrough(lastIndex);

    public string PathThrough(int lastIndex)
    {
        if (Rooms.Count == 0 || lastIndex < 0)
        {
            return string.Empty;
        }

        lastIndex = Math.Min(lastIndex, Rooms.Count - 1);
        var path = new StringBuilder(FormattableString.Invariant($"M{Rooms[0].X} {Rooms[0].Y}"));
        for (var i = 1; i <= lastIndex; i++)
        {
            var from = Rooms[i - 1];
            var to = Rooms[i];
            if (_rowOfRoom[i] != _rowOfRoom[i - 1])
            {
                var side = from.X > FloorWidth / 2 ? TurnBulge : -TurnBulge;
                path.Append(FormattableString.Invariant($" C {from.X + side} {from.Y}, {to.X + side} {to.Y}, {to.X} {to.Y}"));
            }
            else
            {
                var middle = (from.X + to.X) / 2;
                path.Append(FormattableString.Invariant($" C {middle} {from.Y}, {middle} {to.Y}, {to.X} {to.Y}"));
            }
        }

        return path.ToString();
    }

    public string StyleOf(int index) => string.Create(
        CultureInfo.InvariantCulture,
        $"--x: {100.0 * Rooms[index].X / FloorWidth:0.###}%; --y: {100.0 * Rooms[index].Y / Height:0.###}%");

    public static (int Left, int Top, int Right, int Bottom) BoxOf(RoomPosition room) =>
        (room.X - (RoomWidth / 2), room.Y - DoorTop, room.X + (RoomWidth / 2), room.Y - DoorTop + RoomHeight);
}

public readonly record struct RoomPosition(int X, int Y);
