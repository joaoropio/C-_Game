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
    public void ChecksFailed_ShowsEveryCaseWithGotAndExpectedAndTheScore()
    {
        var cut = RenderResult(new ChecksFailed([
            new CheckOutcome("turns = 1", "1", "1"),
            new CheckOutcome("turns = 3", "6", "3"),
            new CheckOutcome("name = \"ana\"", "\"A3\"", "\"83\""),
        ]));

        Assert.Equal("Resultado do feitiço", cut.Find("h2").TextContent);
        Assert.Contains("1 de 3 certos", cut.Find(".result-meta").TextContent);
        Assert.Contains("1 de 3 certos — ainda não está certo", System.Text.RegularExpressions.Regex.Replace(cut.Find(".result-meta").TextContent, @"\s+", " "));
        Assert.Equal("status", cut.Find(".result").GetAttribute("role"));
        var cases = cut.FindAll(".case");
        Assert.Equal(3, cases.Count);
        Assert.Single(cut.FindAll(".case.pass"));
        Assert.Equal(2, cut.FindAll(".case.fail").Count);
        Assert.Equal("turns = 1", cases[0].QuerySelector(".case-name")!.TextContent);
        Assert.Equal("deu 1 certo", cases[0].QuerySelector(".case-detail")!.TextContent.Replace("✓", "").Trim());
        Assert.Equal("deu 3 · esperado 6", cases[1].QuerySelector(".case-detail")!.TextContent);
        Assert.Equal("deu \"83\" · esperado \"A3\"", cases[2].QuerySelector(".case-detail")!.TextContent);
    }

    [Fact]
    public void ChecksFailed_DecorativePartsAreHiddenFromScreenReaders()
    {
        var cut = RenderResult(new ChecksFailed([
            new CheckOutcome("a", "1", "1"),
            new CheckOutcome("b", "2", "3"),
        ]));

        Assert.Equal("true", cut.Find(".case.fail .sep").GetAttribute("aria-hidden"));
        Assert.Equal("true", cut.Find(".case.pass .mark").GetAttribute("aria-hidden"));
        Assert.Equal("certo", cut.Find(".case.pass .visually-hidden").TextContent);
    }

    [Fact]
    public async Task ChecksFailed_InEnglish_UsesGotExpectedAndScore()
    {
        await Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En);
        var cut = RenderResult(new ChecksFailed([new CheckOutcome("turns = 3", "6", "3")]));

        Assert.Equal("got 3 · expected 6", cut.Find(".case-detail").TextContent);
        Assert.Contains("0 of 1 correct", cut.Find(".result-meta").TextContent);
        Assert.Equal("Spell result", cut.Find("h2").TextContent);
    }

    public static TheoryData<RunResult, string> ResultIcons() => new()
    {
        { new ChecksFailed([new CheckOutcome("a", "1", "2")]), "icon-balance" },
        { new CompilationFailed([new CompilationError(1, 1, "CS1002", null)]), "icon-rune" },
        { new TimedOut(), "icon-hourglass" },
        { new RuntimeError(new LocalizedText("Falhou.", "Failed."), "boom", 1), "icon-spiral" },
        { new CodeRejected(new LocalizedText("Proibido.", "Forbidden."), 1), "icon-seal" },
    };

    [Theory]
    [MemberData(nameof(ResultIcons))]
    public void EveryResultType_HasTitleAndItsOwnDecorativeIcon(RunResult result, string iconClass)
    {
        var cut = RenderResult(result);

        Assert.Equal("Resultado do feitiço", cut.Find(".result-head h2").TextContent);
        var icon = cut.Find($".result-head svg.{iconClass}");
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
        Assert.Single(cut.FindAll(".result-head svg"));
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
    public void Passed_ShowsAllGreenCasesWithScoreAndSuccessPhrase()
    {
        var cut = RenderResult(new Passed(3, [
            new CheckOutcome("turns = 1", "1", "1"),
            new CheckOutcome("turns = 3", "3", "3"),
        ]));

        Assert.Equal("Resultado do feitiço", cut.Find(".result-head h2").TextContent);
        Assert.Equal("2 de 2 certos — o feitiço funcionou", System.Text.RegularExpressions.Regex.Replace(cut.Find(".result-meta").TextContent, @"\s+", " ").Trim());
        Assert.Equal(2, cut.FindAll(".case.pass").Count);
        Assert.Empty(cut.FindAll(".case.fail"));
        Assert.Equal("deu 3 certo", cut.FindAll(".case")[1].QuerySelector(".case-detail")!.TextContent.Replace("✓", "").Trim());
        Assert.Contains("passed", cut.Find(".result").ClassList);
    }

    [Fact]
    public void Passed_HasOneDecorativeSuccessIcon()
    {
        var cut = RenderResult(new Passed(1, [new CheckOutcome("a", "1", "1")]));

        Assert.Equal("true", cut.Find(".result-head svg.icon-star").GetAttribute("aria-hidden"));
        Assert.Single(cut.FindAll(".result-head svg"));
    }

    [Fact]
    public async Task Passed_InEnglish_UsesSuccessPhrase()
    {
        await Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En);
        var cut = RenderResult(new Passed(1, [new CheckOutcome("a", "1", "1")]));

        Assert.Contains("1 of 1 correct", cut.Find(".result-meta").TextContent);
        Assert.Contains("the spell worked", cut.Find(".result-meta").TextContent);
    }
}
