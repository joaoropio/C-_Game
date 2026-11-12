using Bunit;
using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Web.Components;

namespace CodeQuest.Tests.Web;

public sealed class RunResultViewTests : BunitContext
{
    public RunResultViewTests() => this.AddGameServices();

    private IRenderedComponent<RunResultView> RenderResult(RunResult result) =>
        Render<RunResultView>(parameters => parameters.Add(view => view.Result, result));

    [Fact]
    public void CodeRejected_ShowsReasonAndLine()
    {
        var cut = RenderResult(new CodeRejected(new LocalizedText("Proibido.", "Forbidden."), 4));

        Assert.Contains("Este código não é permitido", cut.Markup);
        Assert.Contains("Linha 4:", cut.Markup);
        Assert.Contains("Proibido.", cut.Markup);
    }

    [Fact]
    public void CompilationError_WithLine_ShowsLineNumber()
    {
        var cut = RenderResult(new CompilationFailed([new CompilationError(3, 1, "CS1002: ; expected", null)]));

        var item = Assert.Single(cut.FindAll("li"));
        Assert.Contains("Linha 3:", item.TextContent);
        Assert.Contains("CS1002: ; expected", item.TextContent);
    }

    [Fact]
    public void CompilationError_WithoutLine_ShowsNoLineNumber()
    {
        var cut = RenderResult(new CompilationFailed([new CompilationError(null, null, "CS1035: comment not terminated", null)]));

        Assert.DoesNotContain("Linha", cut.Find("li").TextContent);
        Assert.Contains("CS1035", cut.Find("li").TextContent);
    }

    [Fact]
    public void CompilationError_WithExplanation_ShowsExplanationAndOriginal()
    {
        var explanation = new LocalizedText("Falta um ponto e vírgula.", "A semicolon is missing.");
        var cut = RenderResult(new CompilationFailed([new CompilationError(2, 5, "CS1002: ; expected", explanation)]));

        var item = cut.Find("li");
        Assert.Contains("Falta um ponto e vírgula.", item.TextContent);
        Assert.Contains("Mensagem original", item.TextContent);
        Assert.Contains("CS1002: ; expected", item.TextContent);
        Assert.Contains("CS1002: ; expected", item.QuerySelector(".original")!.TextContent);
    }

    [Fact]
    public void ChecksFailed_ListsEachFailure()
    {
        var cut = RenderResult(new ChecksFailed([
            new LocalizedText("Primeira falha.", "First failure."),
            new LocalizedText("Segunda falha.", "Second failure."),
        ]));

        Assert.Equal(2, cut.FindAll("li").Count);
        Assert.Contains("Ainda não está certo", cut.Markup);
        Assert.Contains("Segunda falha.", cut.Markup);
    }

    [Fact]
    public void TimedOut_ShowsLoopMessage()
    {
        var cut = RenderResult(new TimedOut());

        Assert.Contains("O teu ciclo nunca acaba", cut.Markup);
    }

    [Fact]
    public void RuntimeError_ShowsExplanationLineAndOriginal()
    {
        var cut = RenderResult(new RuntimeError(new LocalizedText("Dividiste por zero.", "Divided by zero."), "Attempted to divide by zero.", 6));

        Assert.Contains("O código falhou ao executar", cut.Markup);
        Assert.Contains("Linha 6:", cut.Markup);
        Assert.Contains("Dividiste por zero.", cut.Markup);
        Assert.Contains("Mensagem original", cut.Markup);
        Assert.Contains("Attempted to divide by zero.", cut.Find(".original").TextContent);
    }

    [Fact]
    public void RuntimeError_WithoutLine_ShowsNoLineNumber()
    {
        var cut = RenderResult(new RuntimeError(new LocalizedText("Falhou.", "Failed."), "boom", null));

        Assert.DoesNotContain("Linha", cut.Markup);
    }

    [Fact]
    public void Passed_RendersNothing()
    {
        var cut = RenderResult(new Passed(3));

        Assert.Empty(cut.Markup.Trim());
    }
}
