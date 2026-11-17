using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class PlayerScopeTests
{
    [Fact]
    public void Of_MarkerInsideMethodBody_MethodBody() =>
        Assert.Equal(PlayerScopeKind.MethodBody, PlayerScope.Of("public static class S\n{\n    public static int F()\n    {\n        //{{PLAYER_CODE}}\n        return 0;\n    }\n}"));

    [Fact]
    public void Of_MarkerAtTheEndOfMethodBody_MethodBody() =>
        Assert.Equal(PlayerScopeKind.MethodBody, PlayerScope.Of("public static class S\n{\n    public static int F()\n    {\n        //{{PLAYER_CODE}}\n    }\n}"));

    [Fact]
    public void Of_MarkerDirectlyInsideClass_ClassBody() =>
        Assert.Equal(PlayerScopeKind.ClassBody, PlayerScope.Of("public static class S\n{\n    //{{PLAYER_CODE}}\n\n    public static int G() => 1;\n}"));

    [Fact]
    public void Of_MarkerBeforeAMember_ClassBody() =>
        Assert.Equal(PlayerScopeKind.ClassBody, PlayerScope.Of("public static class S\n{\n    public static int G() => 1;\n    //{{PLAYER_CODE}}\n    public static int H() => 2;\n}"));

    [Fact]
    public void Of_NoMarker_MethodBody() =>
        Assert.Equal(PlayerScopeKind.MethodBody, PlayerScope.Of("public static class S { }"));

    [Fact]
    public void Of_MarkerAloneAtFileLevel_File() =>
        Assert.Equal(PlayerScopeKind.File, PlayerScope.Of("// {{PLAYER_CODE}}\n"));

    [Fact]
    public void Of_MarkerAtFileLevelBetweenTypes_File() =>
        Assert.Equal(PlayerScopeKind.File, PlayerScope.Of("class A { }\n// {{PLAYER_CODE}}\nclass B { }"));
}
