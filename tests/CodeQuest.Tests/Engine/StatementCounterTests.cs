using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class StatementCounterTests
{
    [Fact]
    public void StatementCounter_CountsNestedStatements()
    {
        Assert.Equal(3, StatementCounter.Count("int a = 1; if (a > 0) { a++; }"));
    }

    [Fact]
    public void StatementCounter_EmptyCode_IsZero()
    {
        Assert.Equal(0, StatementCounter.Count(""));
    }
}
