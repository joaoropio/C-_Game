using CodeQuest.Core.Progress;
using Microsoft.JSInterop;

namespace CodeQuest.Web.Services;

public sealed class ProgressStore(IJSRuntime js)
{
    private const string StorageKey = "cq.progress";

    public PlayerProgress? Current { get; private set; }

    public bool WasUnreadable { get; private set; }

    public event Action? Changed;

    public async Task<ProgressLoadResult> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        var result = PlayerProgress.FromJson(json);
        Current = result.Progress;
        WasUnreadable = result.WasUnreadable;
        Changed?.Invoke();
        return result;
    }

    public async Task SaveAsync(PlayerProgress progress) =>
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, progress.ToJson());
}
