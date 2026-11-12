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
        var result = await ReadStoredProgressAsync();
        Current = result.Progress;
        WasUnreadable = result.WasUnreadable;
        Changed?.Invoke();

        if (result.WasUnreadable)
        {
            await WriteAsync(result.Progress);
        }

        return result;
    }

    public async Task SaveAsync(PlayerProgress progress)
    {
        Current = progress;
        Changed?.Invoke();
        await WriteAsync(progress);
    }

    private async Task<ProgressLoadResult> ReadStoredProgressAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return PlayerProgress.FromJson(json);
        }
        catch (JSDisconnectedException)
        {
            return new ProgressLoadResult(new PlayerProgress(), false);
        }
    }

    private async Task WriteAsync(PlayerProgress progress)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, progress.ToJson());
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
