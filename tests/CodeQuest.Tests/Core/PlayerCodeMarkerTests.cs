using CodeQuest.Core.Levels;

namespace CodeQuest.Tests.Core;

public class PlayerCodeMarkerTests
{
    [Theory]
    [InlineData("// {{PLAYER_CODE}}")]
    [InlineData("    //{{PLAYER_CODE}}  ")]
    [InlineData("\t//   {{ PLAYER_CODE }}\r")]
    public void IsMarkerLine_WithSpaces_True(string line) =>
        Assert.True(PlayerCodeMarker.IsMarkerLine(line));

    [Fact]
    public void IsMarkerLine_InlineWithOtherCode_False() =>
        Assert.False(PlayerCodeMarker.IsMarkerLine("{ // {{PLAYER_CODE}} }"));

    [Fact]
    public void IsMarkerLine_OtherComment_False() =>
        Assert.False(PlayerCodeMarker.IsMarkerLine("// something else"));
}
