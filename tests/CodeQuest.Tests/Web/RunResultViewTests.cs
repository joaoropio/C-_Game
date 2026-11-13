using Bunit;
using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Web.Components;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;

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
    public void ChecksFailed_ListsEachFailedCheckWithCaseActualAndExpected()
    {
        var cut = RenderResult(new ChecksFailed([
            new CheckOutcome("turns = 1", "1", "1"),
            new CheckOutcome("turns = 3", "6", "3"),
            new CheckOutcome("name = \"ana\"", "\"A3\"", "\"83\""),
        ]));

        var items = cut.FindAll("li");
        Assert.Equal(2, items.Count);
        Assert.Contains("Ainda não está certo", cut.Markup);
        Assert.Equal("turns = 3: deu 3 · esperado 6", items[0].TextContent);
        Assert.Equal("name = \"ana\": deu \"83\" · esperado \"A3\"", items[1].TextContent);
    }

    [Fact]
    public async Task ChecksFailed_InEnglish_UsesGotAndExpected()
    {
        await Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En);
        var cut = RenderResult(new ChecksFailed([new CheckOutcome("turns = 3", "6", "3")]));

        Assert.Equal("turns = 3: got 3 · expected 6", cut.Find("li").TextContent);
    }

    [Fact]
    public void TimedOut_ShowsLoopMessage()
    {
        var cut = RenderResult(new TimedOut());

        Assert.Contains("O feitiço demora demasiado", cut.Markup);
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
        var cut = RenderResult(new Passed(3, []));

        Assert.Empty(cut.Markup.Trim());
    }
}
