using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class PlayerCodePrecheckTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Check_EmptyCode_Rejected(string code)
    {
        var result = PlayerCodePrecheck.Check(code);

        Assert.NotNull(result);
        Assert.Equal("Ainda não escreveste nenhum feitiço.", result.Reason.Pt);
        Assert.Equal("You haven't written any spell yet.", result.Reason.En);
    }

    [Fact]
    public void Check_OnlyComments_Rejected()
    {
        var result = PlayerCodePrecheck.Check("// nada\n/* ainda nada */");

        Assert.NotNull(result);
        Assert.Equal("You haven't written any spell yet.", result.Reason.En);
    }

    [Fact]
    public void Check_ExtraClosingBrace_RejectedWithLine()
    {
        var result = PlayerCodePrecheck.Check("int a = 1;\n}");

        Assert.NotNull(result);
        Assert.Equal(2, result.Line);
        Assert.Equal("Tens uma \"}\" a mais na linha 2.", result.Reason.Pt);
        Assert.Equal("There's an extra \"}\" on line 2.", result.Reason.En);
    }

    [Fact]
    public void Check_UnclosedBrace_RejectedWithLine()
    {
        var result = PlayerCodePrecheck.Check("int a = 1;\nif (a > 0) {\n  a++;");

        Assert.NotNull(result);
        Assert.Equal(2, result.Line);
        Assert.Equal("Falta fechar uma \"{\" aberta na linha 2.", result.Reason.Pt);
        Assert.Equal("A \"{\" opened on line 2 is never closed.", result.Reason.En);
    }

    [Fact]
    public void Check_BraceInsideString_Accepted()
    {
        Assert.Null(PlayerCodePrecheck.Check("string s = \"}\";"));
    }

    [Fact]
    public void Check_ValidCode_ReturnsNull()
    {
        Assert.Null(PlayerCodePrecheck.Check("int a = 1;\nif (a > 0) { a++; }"));
    }
}
