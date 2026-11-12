using CodeQuest.Engine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeQuest.Tests.Engine;

public class LoopGuardRewriterTests
{
    private const string Tick = "global::__LoopGuard.Tick();";

    private static SyntaxNode Rewrite(string code)
    {
        var root = CSharpSyntaxTree.ParseText(code).GetRoot();
        return new LoopGuardRewriter(root.FullSpan).Visit(root);
    }

    private static int TickCount(SyntaxNode node) => node.ToFullString().Split(Tick).Length - 1;

    [Fact]
    public void Rewrite_WhileTrue_InsertsTick()
    {
        var rewritten = Rewrite("while (true) { x++; }");

        Assert.Equal(1, TickCount(rewritten));
        Assert.Contains("{ " + Tick, rewritten.ToFullString());
    }

    [Fact]
    public void Rewrite_ForWithoutBlock_WrapsBodyAndInsertsTick()
    {
        var rewritten = Rewrite("for (;;) x++;");

        var loop = rewritten.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ForStatementSyntax>().Single();
        var body = Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.BlockSyntax>(loop.Statement);
        Assert.Equal(2, body.Statements.Count);
        Assert.Equal(Tick, body.Statements[0].ToString());
        Assert.Empty(rewritten.GetDiagnostics());
    }

    [Fact]
    public void Rewrite_DoWhile_InsertsTick()
    {
        var rewritten = Rewrite("do { x++; } while (true);");

        Assert.Equal(1, TickCount(rewritten));
        Assert.Empty(rewritten.GetDiagnostics());
    }

    [Fact]
    public void Rewrite_Foreach_InsertsTick()
    {
        var rewritten = Rewrite("foreach (var i in items) x += i;");

        Assert.Equal(1, TickCount(rewritten));
        Assert.Empty(rewritten.GetDiagnostics());
    }

    [Fact]
    public void Rewrite_ForeachDeconstruction_InsertsTick()
    {
        var rewritten = Rewrite("foreach (var (a, b) in pairs) x += a;");

        Assert.Equal(1, TickCount(rewritten));
        Assert.Empty(rewritten.GetDiagnostics());
    }

    [Fact]
    public void Rewrite_Goto_InsertsTickBeforeJump()
    {
        var rewritten = Rewrite("a: goto a;");

        Assert.Equal(1, TickCount(rewritten));
        Assert.Contains("a: { " + Tick + " goto a; }", rewritten.ToFullString());
        Assert.Empty(rewritten.GetDiagnostics());
    }

    [Fact]
    public void Rewrite_GotoCaseAndDefault_InsertTick()
    {
        var rewritten = Rewrite("switch (1) { case 1: goto case 2; case 2: goto default; default: break; }");

        Assert.Equal(2, TickCount(rewritten));
        Assert.Empty(rewritten.GetDiagnostics());
    }

    [Fact]
    public void Rewrite_LabeledDeclaration_StaysInScope()
    {
        var rewritten = Rewrite("{ a: int x = 1; x++; }");

        Assert.Equal(0, TickCount(rewritten));
        Assert.Equal("{ a: int x = 1; x++; }", rewritten.ToFullString());
    }

    [Fact]
    public void Rewrite_NestedLoops_InsertsTickInEach()
    {
        var rewritten = Rewrite("while (a) { for (var i = 0; i < 3; i++) { x++; } }");

        Assert.Equal(2, TickCount(rewritten));
        Assert.Empty(rewritten.GetDiagnostics());
    }

    [Fact]
    public void Rewrite_PreservesPlayerLineNumbers()
    {
        var rewritten = Rewrite("while (a)\n{\n    int b = ;\n}");

        var error = Assert.Single(rewritten.GetDiagnostics());
        Assert.Equal(2, error.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public void Rewrite_LoopOutsidePlayerSpan_IsUntouched()
    {
        var root = CSharpSyntaxTree.ParseText("while (a) { }\nwhile (b) { }").GetRoot();
        var secondLine = root.GetText().Lines[1].Span;

        var rewritten = new LoopGuardRewriter(secondLine).Visit(root);

        Assert.Equal(1, TickCount(rewritten));
        Assert.StartsWith("while (a) { }", rewritten.ToFullString());
    }
}

