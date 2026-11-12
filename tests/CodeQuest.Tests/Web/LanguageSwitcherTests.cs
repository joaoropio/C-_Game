using Bunit;
using CodeQuest.Core;
using CodeQuest.Web.Components;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CodeQuest.Tests.Web;

public sealed class LanguageSwitcherTests : BunitContext
{
    [Fact]
    public void LanguageSwitcher_Click_ChangesTexts()
    {
        this.AddGameServices();
        var cut = Render<LanguageSwitcher>();
        Assert.Equal("Idioma", cut.Find("[role=group]").GetAttribute("aria-label"));

        cut.FindAll("button").Single(button => button.TextContent == "EN").Click();

        Assert.Equal("Language", cut.Find("[role=group]").GetAttribute("aria-label"));
        Assert.Equal(Language.En, Services.GetRequiredService<UiText>().Language);
        Assert.Equal("true", cut.FindAll("button").Single(button => button.TextContent == "EN").GetAttribute("aria-pressed"));
    }
}
