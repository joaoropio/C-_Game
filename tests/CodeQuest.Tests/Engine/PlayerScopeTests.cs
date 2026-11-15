using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class PlayerScopeTests
{
    [Fact]
    public void IsClassScope_MarkerInsideMethodBody_False() =>
        Assert.False(PlayerScope.IsClassScope("public static class S\n{\n    public static int F()\n    {\n        //{{PLAYER_CODE}}\n        return 0;\n    }\n}"));

    [Fact]
    public void IsClassScope_MarkerAtTheEndOfMethodBody_False() =>
        Assert.False(PlayerScope.IsClassScope("public static class S\n{\n    public static int F()\n    {\n        //{{PLAYER_CODE}}\n    }\n}"));

    [Fact]
    public void IsClassScope_MarkerDirectlyInsideClass_True() =>
        Assert.True(PlayerScope.IsClassScope("public static class S\n{\n    //{{PLAYER_CODE}}\n\n    public static int G() => 1;\n}"));

    [Fact]
    public void IsClassScope_MarkerBeforeAMember_True() =>
        Assert.True(PlayerScope.IsClassScope("public static class S\n{\n    public static int G() => 1;\n    //{{PLAYER_CODE}}\n    public static int H() => 2;\n}"));

    [Fact]
    public void IsClassScope_NoMarker_False() =>
        Assert.False(PlayerScope.IsClassScope("public static class S { }"));
}
