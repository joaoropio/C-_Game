using Bunit;
using CodeQuest.Core;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CodeQuest.Tests.Web;

public sealed class UiTextTests : BunitContext
{
    private UiText CreateUiText(string? savedLanguage)
    {
        this.AddGameServices(savedLanguage: savedLanguage);
        return Services.GetRequiredService<UiText>();
    }

    [Fact]
    public void UiText_DefaultsToPortuguese()
    {
        var ui = CreateUiText(savedLanguage: null);

        Assert.Equal(Language.Pt, ui.Language);
        Assert.Equal("Mapa da masmorra", ui["map.title"]);
    }

    [Fact]
    public async Task UiText_LoadsSavedLanguage()
    {
        var ui = CreateUiText(savedLanguage: "en");

        await ui.LoadAsync();

        Assert.Equal(Language.En, ui.Language);
        Assert.Equal("Dungeon map", ui["map.title"]);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("EN ")]
    public async Task UiText_InvalidSavedLanguage_FallsBackToPortuguese(string saved)
    {
        var ui = CreateUiText(savedLanguage: saved);

        await ui.LoadAsync();

        Assert.Equal(Language.Pt, ui.Language);
    }

    [Fact]
    public async Task UiText_SetLanguage_SavesAndRaisesChanged()
    {
        var ui = CreateUiText(savedLanguage: null);
        var changes = 0;
        ui.Changed += () => changes++;

        await ui.SetLanguageAsync(Language.En);

        Assert.Equal(Language.En, ui.Language);
        Assert.Equal(1, changes);
        Assert.Equal("en", this.LastSavedValue(TestContextExtensions.LanguageKey));
    }

    [Fact]
    public async Task UiText_CircuitDisconnected_StillChangesLanguageAndRaisesChanged()
    {
        this.AddGameServicesWithoutStorage();
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetException(new JSDisconnectedException("gone"));
        JSInterop.SetupVoid(TestContextExtensions.SetItem, _ => true).SetException(new JSDisconnectedException("gone"));
        var ui = Services.GetRequiredService<UiText>();
        var changes = 0;
        ui.Changed += () => changes++;

        await ui.LoadAsync();
        await ui.SetLanguageAsync(Language.En);

        Assert.Equal(Language.En, ui.Language);
        Assert.Equal(1, changes);
    }
}
