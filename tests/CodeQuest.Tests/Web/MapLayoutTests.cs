using System.Text.RegularExpressions;
using CodeQuest.Web.Components.Pages;

namespace CodeQuest.Tests.Web;

public sealed class MapLayoutTests
{
    public static TheoryData<int> RoomCounts => new() { 1, 2, 6, 7, 12, 13 };

    [Theory]
    [MemberData(nameof(RoomCounts))]
    public void Rooms_DoNotOverlapAndStayInsideTheFloor(int count)
    {
        var layout = MapLayout.For(count);
        var boxes = layout.Rooms.Select(MapLayout.BoxOf).ToList();

        Assert.Equal(count, boxes.Count);
        foreach (var box in boxes)
        {
            Assert.InRange(box.Left, 0, layout.Width);
            Assert.InRange(box.Right, 0, layout.Width);
            Assert.InRange(box.Top, 0, layout.Height);
            Assert.InRange(box.Bottom, 0, layout.Height);
        }

        for (var a = 0; a < boxes.Count; a++)
        {
            for (var b = a + 1; b < boxes.Count; b++)
            {
                var separate = boxes[a].Right <= boxes[b].Left || boxes[b].Right <= boxes[a].Left
                    || boxes[a].Bottom <= boxes[b].Top || boxes[b].Bottom <= boxes[a].Top;
                Assert.True(separate, $"rooms {a} and {b} overlap");
            }
        }
    }

    [Theory]
    [MemberData(nameof(RoomCounts))]
    public void Corridor_PassesThroughEveryRoomCentreInOrder(int count)
    {
        var layout = MapLayout.For(count);

        var endpoints = Regex.Matches(layout.PathData, @"(-?\d+) (-?\d+)(?=(?: C|$))")
            .Select(match => (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)))
            .ToList();

        Assert.Equal(layout.Rooms.Select(room => (room.X, room.Y)), endpoints);
    }

    [Fact]
    public void Floor_HasAtMostSixRoomsPerRowAndGrowsTallerNotWider()
    {
        var one = MapLayout.For(1);
        var twelve = MapLayout.For(12);

        Assert.Equal(one.Width, twelve.Width);
        Assert.Equal(MapLayout.RowHeight, twelve.Height - one.Height);
        Assert.Equal(6, twelve.Rooms.Count(room => room.Y < MapLayout.RowHeight));
    }

    [Fact]
    public void SecondRow_RunsRightToLeft()
    {
        var layout = MapLayout.For(8);

        Assert.Equal(layout.Rooms[5].X, layout.Rooms[6].X);
        Assert.True(layout.Rooms[7].X < layout.Rooms[6].X);
        Assert.True(layout.Rooms[1].X > layout.Rooms[0].X);
    }

    [Fact]
    public void Rooms_ZigzagBetweenLowAndHighPositions()
    {
        var layout = MapLayout.For(4);

        Assert.True(layout.Rooms[0].Y > layout.Rooms[1].Y);
        Assert.Equal(layout.Rooms[0].Y, layout.Rooms[2].Y);
    }

    [Fact]
    public void Trail_StopsAtTheGivenRoom()
    {
        var layout = MapLayout.For(5);

        Assert.Empty(layout.Trail(0));
        Assert.Equal(2, Regex.Matches(layout.Trail(2), " C ").Count);
        Assert.Equal(layout.PathData, layout.Trail(4));
    }

    [Fact]
    public void SingleRoom_HasNoPathSegments() =>
        Assert.DoesNotContain('C', MapLayout.For(1).PathData);
}
