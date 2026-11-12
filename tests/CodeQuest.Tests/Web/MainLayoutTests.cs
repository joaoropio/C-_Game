using Bunit;
using CodeQuest.Web.Components.Layout;

namespace CodeQuest.Tests.Web;

public sealed class MainLayoutTests : BunitContext
{
    [Fact]
    public void MainLayout_UnreadableProgress_ShowsWarning()
    {
        this.AddGameServices(savedProgress: "{ not json");

        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, "<p id=\"page\">page</p>"));

        cut.WaitForAssertion(() =>
            Assert.Contains("não pôde ser lido", cut.Find("[role=alert]").TextContent));
        Assert.NotNull(cut.Find("#page"));
    }

    [Fact]
    public void MainLayout_ReadableProgress_ShowsNoWarning()
    {
        this.AddGameServices();

        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, "<p id=\"page\">page</p>"));

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("localStorage.getItem", calledTimes: 2));
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public void MainLayout_SavedEnglish_LoadsEnglishTitle()
    {
        this.AddGameServices(savedLanguage: "en");

        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, "<p>page</p>"));

        cut.WaitForAssertion(() =>
            Assert.Equal("Language", cut.Find("[role=group]").GetAttribute("aria-label")));
    }

    [Fact]
    public void MainLayout_DismissWarning_HidesIt()
    {
        this.AddGameServices(savedProgress: "{ not json");
        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, "<p>page</p>"));
        cut.WaitForElement("[role=alert]");

        cut.Find("[role=alert] button").Click();

        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(null, "Ocorreu um erro inesperado.", "Recarregar")]
    [InlineData("en", "An unexpected error occurred.", "Reload")]
    public void MainLayout_ErrorBar_IsLocalized(string? savedLanguage, string message, string reload)
    {
        this.AddGameServices(savedLanguage: savedLanguage);

        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, "<p>page</p>"));

        cut.WaitForAssertion(() =>
        {
            var bar = cut.Find("#blazor-error-ui");
            Assert.Contains(message, bar.TextContent);
            Assert.Contains(reload, bar.TextContent);
        });
    }
}
