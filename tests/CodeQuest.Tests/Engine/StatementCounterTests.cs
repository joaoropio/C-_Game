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

        Assert.Equal(2, StatementCounter.Count(code, PlayerScopeKind.ClassBody));
    }

    [Fact]
    public void StatementCounter_ClassScope_CountsAnExpressionBodyAsOneStatement()
    {
        Assert.Equal(
            2,
            StatementCounter.Count("static int Twice(int x) => x * 2;\nstatic int Thrice(int x) => x * 3;", PlayerScopeKind.ClassBody));
    }

    [Fact]
    public void StatementCounter_ClassScope_CountsNestedStatements()
    {
        const string code = "static int F(int x)\n{\n    if (x > 0) { x++; }\n    return x;\n}";

        Assert.Equal(3, StatementCounter.Count(code, PlayerScopeKind.ClassBody));
    }

    [Fact]
    public void StatementCounter_MethodScope_CountsLocalFunctionAndItsCall()
    {
        Assert.Equal(2, StatementCounter.Count("int F(int n) => n;\nreturn F(1);"));
    }

    [Fact]
    public void StatementCounter_FileScope_CountsStatementsInMethodsConstructorsAndAccessors()
    {
        const string code = "class Hero\n{\n    int hp;\n    public Hero(int hp) { this.hp = hp; }\n    public int Hp { get { return hp; } set { hp = value; } }\n    public void Hit() { hp--; if (hp < 0) { hp = 0; } }\n}";

        Assert.Equal(6, StatementCounter.Count(code, PlayerScopeKind.File));
    }

    [Fact]
    public void StatementCounter_FileScope_CountsAnExpressionBodyAsOneStatement()
    {
        const string code = "class Hero\n{\n    int hp;\n    public Hero() => hp = 1;\n    public int Hp => hp;\n    public int Twice() => hp * 2;\n    public int Other { get => hp; set => hp = value; }\n}";

        Assert.Equal(5, StatementCounter.Count(code, PlayerScopeKind.File));
    }

    [Fact]
    public void StatementCounter_FileScope_AnAutoPropertyAndFieldsCountZero() =>
        Assert.Equal(0, StatementCounter.Count("class Hero { public int Hp { get; set; } = 3; int x = 1; }", PlayerScopeKind.File));
}
