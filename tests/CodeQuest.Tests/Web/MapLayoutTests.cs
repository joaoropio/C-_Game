using CodeQuest.Web.Components.Pages;

namespace CodeQuest.Tests.Web;

public sealed class MapLayoutTests
{
    [Fact]
    public void Rooms_ZigzagBetweenLowAndHighRows()
    {
        var layout = MapLayout.For(4);

        Assert.Equal(4, layout.Rooms.Count);
        Assert.True(layout.Rooms[0].Y > layout.Rooms[1].Y);
        Assert.Equal(layout.Rooms[0].Y, layout.Rooms[2].Y);
        Assert.Equal(layout.Rooms[1].Y, layout.Rooms[3].Y);
        Assert.True(layout.Rooms[0].X < layout.Rooms[1].X && layout.Rooms[1].X < layout.Rooms[2].X);
    }

    [Fact]
    public void Path_StartsAtFirstRoomAndEndsAtLast()
    {
        var layout = MapLayout.For(3);

        var first = layout.Rooms[0];
        var last = layout.Rooms[^1];
        Assert.StartsWith($"M{first.X} {first.Y}", layout.PathData);
        Assert.EndsWith($"{last.X} {last.Y}", layout.PathData);
    }

    [Fact]
    public void Percentages_StayInsideTheFloor()
    {
        foreach (var room in MapLayout.For(6).Rooms)
        {
            Assert.InRange(room.LeftPercent, 0, 100);
            Assert.InRange(room.TopPercent, 0, 100);
        }
    }

    [Fact]
    public void SingleRoom_HasNoPathSegments()
    {
        var layout = MapLayout.For(1);

        Assert.DoesNotContain('C', layout.PathData);
    }
}
