using Bunit;
using CodeQuest.Core.Progress;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;

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
}

