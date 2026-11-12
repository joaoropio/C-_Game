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
        var saved = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        Language = saved == Code(Language.En) ? Language.En : Language.Pt;
        Changed?.Invoke();
    }

    public async Task SetLanguageAsync(Language language)
    {
        Language = language;
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, Code(language));
        Changed?.Invoke();
    }

    private static string Code(Language language) => language.ToString().ToLowerInvariant();
}
