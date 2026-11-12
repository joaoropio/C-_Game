using Bunit;
using CodeQuest.Web.Components.Layout;

namespace CodeQuest.Tests.Web;

public sealed class ReconnectModalTests : BunitContext
{
    [Fact]
    public void ReconnectModal_ShowsBothLanguages()
    {
        var cut = Render<ReconnectModal>();

        Assert.Contains("A religar ao servidor", cut.Markup);
        Assert.Contains("Rejoining the server", cut.Markup);
        Assert.Contains("Tentar de novo", cut.Markup);
        Assert.Contains("Retry", cut.Markup);
        Assert.Contains("Retomar", cut.Markup);
        Assert.Contains("Resume", cut.Markup);
    }

    [Fact]
    public void ReconnectModal_KeepsTheHooksUsedByTheScriptAndBlazor()
    {
        var cut = Render<ReconnectModal>();

        Assert.NotNull(cut.Find("dialog#components-reconnect-modal"));
        Assert.Single(cut.FindAll("#components-seconds-to-next-attempt"));
        Assert.Single(cut.FindAll("button#components-reconnect-button"));
        Assert.Single(cut.FindAll("button#components-resume-button"));
        Assert.NotEmpty(cut.FindAll(".components-reconnect-first-attempt-visible"));
        Assert.NotEmpty(cut.FindAll(".components-reconnect-repeated-attempt-visible"));
        Assert.NotEmpty(cut.FindAll(".components-reconnect-failed-visible"));
        Assert.NotEmpty(cut.FindAll(".components-pause-visible"));
        Assert.NotEmpty(cut.FindAll(".components-resume-failed-visible"));
    }
}
