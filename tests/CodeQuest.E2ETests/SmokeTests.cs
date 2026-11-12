using Microsoft.Playwright;

namespace CodeQuest.E2ETests;

public class SmokeTests(GameServerFixture server) : IClassFixture<GameServerFixture>
{
    [Fact]
    public async Task HomePage_Loads()
    {
        var page = await server.Browser.NewPageAsync();

        var response = await page.GotoAsync(server.BaseUrl);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.False(string.IsNullOrWhiteSpace(await page.TitleAsync()));
    }
}
