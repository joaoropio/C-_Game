using CodeQuest.Core;
using Microsoft.JSInterop;

namespace CodeQuest.Web.Services;

public sealed class UiText(GameContent content, IJSRuntime js)
{
    private const string StorageKey = "cq.language";

    public Language Language { get; private set; } = Language.Pt;

    public string this[string key] => content.Ui.Get(key, Language);

    public event Action? Changed;

    public async Task LoadAsync()
    {
        try
        {
            var saved = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            Language = saved == Code(Language.En) ? Language.En : Language.Pt;
            Changed?.Invoke();
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async Task SetLanguageAsync(Language language)
    {
        Language = language;
        Changed?.Invoke();
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, Code(language));
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private static string Code(Language language) => language.ToString().ToLowerInvariant();
}
