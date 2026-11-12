using Bunit;
using CodeQuest.Core;
using CodeQuest.Web.Components.Pages;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CodeQuest.Tests.Web;

public sealed class NotFoundTests : BunitContext
{
    [Theory]
    [InlineData(null, "Página não encontrada", "não existe")]
    [InlineData("en", "Page not found", "does not exist")]
    public async Task NotFound_ShowsLocalizedText(string? savedLanguage, string title, string message)
    {
        this.AddGameServices(savedLanguage: savedLanguage);
        await Services.GetRequiredService<UiText>().LoadAsync();

        var cut = Render<NotFound>();

        Assert.Equal(title, cut.Find("h1").TextContent);
        Assert.Contains(message, cut.Find("p").TextContent);
    }

    [Fact]
    public async Task NotFound_LanguageChanges_UpdatesText()
    {
        this.AddGameServices();
        var cut = Render<NotFound>();

        await Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En);

        cut.WaitForAssertion(() => Assert.Equal("Page not found", cut.Find("h1").TextContent));
    }
}