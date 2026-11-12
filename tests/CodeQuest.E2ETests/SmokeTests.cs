using Microsoft.Playwright;

namespace CodeQuest.E2ETests;

[Collection(GameServerCollection.Name)]
public class SmokeTests(GameServerFixture server)
{
    [Fact]
    public async Task HomePage_Loads()
    {
        await using var context = await server.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync(server.BaseUrl);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.False(string.IsNullOrWhiteSpace(await page.TitleAsync()));
    }
}
