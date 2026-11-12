using CodeQuest.Engine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeQuest.Tests.Engine;

public class LoopGuardRewriterTests
{
    private const string Tick = "global::__LoopGuard.Tick();";
    private const string Enter = "global::__LoopGuard.Enter();";
    private const string NotTripped = "!global::__LoopGuard.Tripped";

    private const int FirstPlayerLine = 6;

    private const string PlayerPrefix = "class C\n{\n    static int M(int n)\n    {\n";
    private const string PlayerSuffix = "\n    }\n}\n";

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList());

    private static SyntaxNode RewriteSpan(SyntaxTree tree, TextSpan span)
    {
        var compilation = CSharpCompilation.Create(
            "RewriterTests",
            [tree, CSharpSyntaxTree.ParseText(LoopGuardRewriter.GuardSource)],
            References.Value);
        return new LoopGuardRewriter(span, compilation.GetSemanticModel(tree)).Visit(tree.GetRoot())!;
    }

    private static SyntaxNode Rewrite(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return RewriteSpan(tree, tree.GetRoot().FullSpan);
    }

    private static string Player(string playerCode) => PlayerPrefix + playerCode + PlayerSuffix;

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
        var tree = CSharpSyntaxTree.ParseText("while (a) { }\nwhile (b) { }");

        var rewritten = RewriteSpan(tree, tree.GetText().Lines[1].Span);

        Assert.Equal(1, TickCount(rewritten));
        Assert.StartsWith("while (a) { }", rewritten.ToFullString());
    }

    // The player code sits on the lines after the template's opening brace, like a real level.
    private static SyntaxNode RewritePlayer(string playerCode)
    {
        var tree = CSharpSyntaxTree.ParseText(
            "using System;\nusing System.Linq;\n" + Player(playerCode));
        var lines = tree.GetText().Lines;
        var playerLines = playerCode.Split('\n').Length;
        var span = TextSpan.FromBounds(lines[FirstPlayerLine].Start, lines[FirstPlayerLine + playerLines - 1].End);
        return RewriteSpan(tree, span);
    }

    private static string EnterCalls(string playerCode) => RewritePlayer(playerCode).ToFullString();

    [Fact]
    public void Rewrite_PlayerCode_StartsWithEnterOnTheTemplateLine()
    {
        var text = RewritePlayer("return M(n);").ToFullString();

        Assert.Contains("    { " + Enter + "\nreturn M(n);\n", text);
        Assert.Equal(1, text.Split(Enter).Length - 1);
    }

    [Fact]
    public void Rewrite_PlayerCodeAfterOtherStatements_InsertsEnterOnThePreviousLine()
    {
        var text = RewritePlayer("int a = 1; // note\nreturn M(a);").ToFullString();

        Assert.Contains("    { " + Enter + "\nint a = 1; // note\nreturn M(a);\n", text);
    }

    [Fact]
    public void Rewrite_PlayerCode_KeepsPlayerLinesAndColumns()
    {
        const string code = "int a = 1;\n    int b = a + undefined;\nreturn b;";

        var text = RewritePlayer(code).ToFullString();

        var token = CSharpSyntaxTree.ParseText(text).GetRoot().DescendantTokens().Single(t => t.ValueText == "undefined");
        var position = token.GetLocation().GetLineSpan().StartLinePosition;
        Assert.Equal(FirstPlayerLine + 1, position.Line);
        Assert.Equal("    int b = a + ".Length, position.Character);
        Assert.Equal(("using System;\nusing System.Linq;\n" + Player(code)).Split('\n').Length, text.Split('\n').Length);
    }

    [Fact]
    public void Rewrite_TemplateMethodsOutsidePlayerSpan_AreUntouched()
    {
        const string code = "class C\n{\n    static int A(int n) { return A(n); }\n    static int B(int n)\n    {\nreturn B(n);\n    }\n    static Func<int, int> L = k => L(k);\n}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var span = tree.GetText().Lines[5].Span;

        var text = RewriteSpan(tree, span).ToFullString();

        Assert.Equal(1, text.Split(Enter).Length - 1);
        Assert.Contains("static int A(int n) { return A(n); }", text);
        Assert.Contains("k => L(k);", text);
    }

    [Fact]
    public void Rewrite_BlockBodiedLocalFunction_StartsWithEnter()
    {
        var text = EnterCalls("int F(int k)\n{\n    return F(k);\n}\nreturn F(n);");

        Assert.Contains("int F(int k)\n{ " + Enter + "\n    return F(k);\n}", text);
        Assert.Equal(2, text.Split(Enter).Length - 1);
    }

    [Fact]
    public void Rewrite_SingleLineBlockBodiedLocalFunction_StartsWithEnter()
    {
        var text = EnterCalls("int F(int k) { return F(k); }\nreturn F(n);");

        Assert.Contains("int F(int k) { " + Enter + " return F(k); }", text);
    }

    [Fact]
    public void Rewrite_ExpressionBodiedLocalFunction_BecomesBlockWithReturn()
    {
        var text = EnterCalls("int F(int k) => F(k);\nreturn F(n);");

        Assert.Contains("int F(int k) { " + Enter + " return F(k); }\n", text);
    }

    [Fact]
    public void Rewrite_ExpressionBodiedVoidLocalFunction_BecomesBlockWithStatement()
    {
        var text = EnterCalls("void G(int k) => G(k);\nG(n);\nreturn n;");

        Assert.Contains("void G(int k) { " + Enter + " G(k); }\n", text);
    }

    [Fact]
    public void Rewrite_ExpressionBodiedLocalFunctionThrowing_BecomesThrowStatement()
    {
        var text = EnterCalls("int F(int k) => throw new Exception();\nreturn n;");

        Assert.Contains("int F(int k) { " + Enter + " throw new Exception(); }\n", text);
    }

    [Fact]
    public void Rewrite_BlockBodiedLambdas_StartWithEnter()
    {
        var text = EnterCalls(
            "Func<int, int> f = k => { return k; };\nFunc<int, int> g = (k) => { return k; };\nFunc<int, int> h = delegate (int k) { return k; };\nreturn f(n) + g(n) + h(n);");

        Assert.Contains("k => { " + Enter + " return k; }", text);
        Assert.Contains("(k) => { " + Enter + " return k; }", text);
        Assert.Contains("delegate (int k) { " + Enter + " return k; }", text);
    }

    [Fact]
    public void Rewrite_ExpressionBodiedLambdaReturningValue_BecomesBlockWithReturn()
    {
        var text = EnterCalls("Func<int, int> f = k => k + 1;\nFunc<int, int> g = (k) => k + 1;\nFunc<int> h = () => n++;\nreturn f(n) + g(n) + h();");

        Assert.Contains("k => { " + Enter + " return k + 1; };", text);
        Assert.Contains("(k) => { " + Enter + " return k + 1; };", text);
        Assert.Contains("() => { " + Enter + " return n++; };", text);
    }

    [Fact]
    public void Rewrite_ExpressionBodiedLambdaReturningVoid_BecomesBlockWithStatement()
    {
        var text = EnterCalls("Action<int> a = k => Console.WriteLine(k);\nAction b = () => n++;\nreturn n;");

        Assert.Contains("k => { " + Enter + " Console.WriteLine(k); };", text);
        Assert.Contains("() => { " + Enter + " n++; };", text);
    }

    [Fact]
    public void Rewrite_ExpressionBodiedLambdaThrowing_BecomesThrowStatement()
    {
        var text = EnterCalls("Func<int> f = () => throw new Exception();\nreturn n;");

        Assert.Contains("() => { " + Enter + " throw new Exception(); };", text);
    }

    [Fact]
    public void Rewrite_NestedLambdas_GuardsEveryLevel()
    {
        var text = EnterCalls("Func<int, Func<int, int>> f = a => b => a + b;\nreturn f(1)(n);");

        Assert.Equal(3, text.Split(Enter).Length - 1);
        Assert.Empty(CSharpSyntaxTree.ParseText(text).GetDiagnostics());
    }

    [Fact]
    public void Rewrite_LambdaInsideLinqCall_IsGuarded()
    {
        var text = EnterCalls("return new[] { n }.Select(k => k * 2).Sum();");

        Assert.Contains("k => { " + Enter + " return k * 2; }", text);
    }

    [Fact]
    public void Rewrite_ExpressionTreeLambda_IsLeftAlone()
    {
        var text = EnterCalls(
            "System.Linq.Expressions.Expression<Func<int, bool>> e = k => new[] { 1 }.Any(j => j > k);\nreturn n;");

        Assert.Contains("k => new[] { 1 }.Any(j => j > k);", text);
        Assert.Equal(1, text.Split(Enter).Length - 1);
    }

    [Fact]
    public void Rewrite_MultiLineExpressionBody_PreservesLineNumbers()
    {
        const string code = "Func<int, int> f =\n    k =>\n        k +\n        undefined;\nreturn f(n);";

        var text = RewritePlayer(code).ToFullString();

        var tree = CSharpSyntaxTree.ParseText(text);
        Assert.Empty(tree.GetDiagnostics());
        var token = tree.GetRoot().DescendantTokens().Single(t => t.ValueText == "undefined");
        Assert.Equal(FirstPlayerLine + 3, token.GetLocation().GetLineSpan().StartLinePosition.Line);
        Assert.Equal(("using System;\nusing System.Linq;\n" + Player(code)).Split('\n').Length, text.Split('\n').Length);
    }

    [Fact]
    public void Rewrite_CatchWithoutFilter_GetsTrippedFilter()
    {
        var text = EnterCalls("try { return n; } catch { return 0; }");

        Assert.Contains("catch when (" + NotTripped + ") { return 0; }", text);
    }

    [Fact]
    public void Rewrite_CatchWithDeclaration_GetsTrippedFilter()
    {
        var text = EnterCalls("try { return n; } catch (Exception e) { return 0; }");

        Assert.Contains("catch (Exception e) when (" + NotTripped + ") { return 0; }", text);
    }

    [Fact]
    public void Rewrite_CatchWithFilter_CombinesBothConditions()
    {
        var text = EnterCalls("try { return n; } catch (Exception e) when (e.Message.Length > 0) { return 0; }");

        Assert.Contains("catch (Exception e) when (" + NotTripped + " && (e.Message.Length > 0)) { return 0; }", text);
    }

    [Fact]
    public void Rewrite_FinallyBody_RunsOnlyWhenNotTripped()
    {
        var text = EnterCalls("try { return n; } finally { n++; }");

        Assert.Contains("finally { if (" + NotTripped + ") { n++; } }", text);
    }

    [Fact]
    public void Rewrite_MultiLineHandlers_PreserveLineNumbers()
    {
        const string code = "try\n{\n    n++;\n}\ncatch (Exception e)\n    when (e.Message.Length > 0)\n{\n    int a = undefined;\n}\nfinally\n{\n    int b = undefined;\n}\nreturn n;";

        var text = RewritePlayer(code).ToFullString();

        var tree = CSharpSyntaxTree.ParseText(text);
        Assert.Empty(tree.GetDiagnostics());
        var lines = tree.GetRoot().DescendantTokens().Where(t => t.ValueText == "undefined")
            .Select(t => t.GetLocation().GetLineSpan().StartLinePosition.Line);
        Assert.Equal([FirstPlayerLine + 7, FirstPlayerLine + 11], lines);
        Assert.Equal(("using System;\nusing System.Linq;\n" + Player(code)).Split('\n').Length, text.Split('\n').Length);
    }

    [Fact]
    public void Rewrite_HandlersOutsidePlayerSpan_AreUntouched()
    {
        const string code = "class C\n{\n    static int A(int n)\n    {\n        try { return n; } catch { return 0; } finally { n++; }\n    }\n    static int B(int n)\n    {\nreturn n;\n    }\n}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var span = tree.GetText().Lines[8].Span;

        var text = RewriteSpan(tree, span).ToFullString();

        Assert.Contains("try { return n; } catch { return 0; } finally { n++; }", text);
    }
}
