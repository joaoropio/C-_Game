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

    [Fact]
    public void StatementCounter_ClassScope_CountsStatementsInsideMethodsOnly()
    {
        const string code = "static int Twice(int x)\n{\n    int doubled = x * 2;\n    return doubled;\n}\nstatic void Nothing() { }";

        Assert.Equal(2, StatementCounter.Count(code, atClassScope: true));
    }

    [Fact]
    public void StatementCounter_ClassScope_CountsAnExpressionBodyAsOneStatement()
    {
        Assert.Equal(
            2,
            StatementCounter.Count("static int Twice(int x) => x * 2;\nstatic int Thrice(int x) => x * 3;", atClassScope: true));
    }

    [Fact]
    public void StatementCounter_ClassScope_CountsNestedStatements()
    {
        const string code = "static int F(int x)\n{\n    if (x > 0) { x++; }\n    return x;\n}";

        Assert.Equal(3, StatementCounter.Count(code, atClassScope: true));
    }

    [Fact]
    public void StatementCounter_MethodScope_CountsLocalFunctionAndItsCall()
    {
        Assert.Equal(2, StatementCounter.Count("int F(int n) => n;\nreturn F(1);"));
    }
}
