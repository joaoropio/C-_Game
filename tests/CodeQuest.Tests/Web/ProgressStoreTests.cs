using Bunit;
using CodeQuest.Core.Progress;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CodeQuest.Tests.Web;

public sealed class ProgressStoreTests : BunitContext
{
    private ProgressStore CreateStore(string? savedProgress)
    {
        this.AddGameServices(savedProgress: savedProgress);
        return Services.GetRequiredService<ProgressStore>();
    }

    [Fact]
    public async Task ProgressStore_Load_Empty_ReturnsNewProgress()
    {
        var store = CreateStore(savedProgress: null);

        var result = await store.LoadAsync();

        Assert.False(result.WasUnreadable);
        Assert.False(result.Progress.IsCompleted("any"));
        Assert.Same(result.Progress, store.Current);
        Assert.False(store.WasUnreadable);
    }

    [Fact]
    public async Task ProgressStore_SaveThenLoad_RoundTrips()
    {
        var progress = new PlayerProgress();
        progress.RecordPass("01-variables", stars: 2);
        await CreateStore(savedProgress: null).SaveAsync(progress);
        var saved = this.LastSavedValue(TestContextExtensions.ProgressKey);

        using var nextVisit = new BunitContext();
        nextVisit.AddGameServices(savedProgress: saved);
        var reloaded = await nextVisit.Services.GetRequiredService<ProgressStore>().LoadAsync();

        Assert.True(reloaded.Progress.IsCompleted("01-variables"));
        Assert.Equal(2, reloaded.Progress.StarsFor("01-variables"));
    }

    [Fact]
    public async Task ProgressStore_Corrupted_ReturnsUnreadable()
    {
        var store = CreateStore(savedProgress: "{ not json");

        var result = await store.LoadAsync();

        Assert.True(result.WasUnreadable);
        Assert.True(store.WasUnreadable);
        Assert.False(result.Progress.IsCompleted("01-variables"));
    }

    [Fact]
    public async Task ProgressStore_Unreadable_OverwritesStorageWithValidProgress()
    {
        var store = CreateStore(savedProgress: "{ not json");

        await store.LoadAsync();

        var saved = this.LastSavedValue(TestContextExtensions.ProgressKey);
        using var nextVisit = new BunitContext();
        nextVisit.AddGameServices(savedProgress: saved);
        var reloaded = await nextVisit.Services.GetRequiredService<ProgressStore>().LoadAsync();
        Assert.False(reloaded.WasUnreadable);
    }

    [Fact]
    public async Task ProgressStore_Save_UpdatesCurrentAndRaisesChanged()
    {
        var store = CreateStore(savedProgress: null);
        var progress = new PlayerProgress();
        var changes = 0;
        store.Changed += () => changes++;

        await store.SaveAsync(progress);

        Assert.Same(progress, store.Current);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task ProgressStore_CircuitDisconnected_DoesNotThrow()
    {
        this.AddGameServicesWithoutStorage();
        JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetException(new JSDisconnectedException("gone"));
        JSInterop.SetupVoid(TestContextExtensions.SetItem, _ => true).SetException(new JSDisconnectedException("gone"));
        var store = Services.GetRequiredService<ProgressStore>();

        await store.LoadAsync();
        await store.SaveAsync(new PlayerProgress());
    }
}
