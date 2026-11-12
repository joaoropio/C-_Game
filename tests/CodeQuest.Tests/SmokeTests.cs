namespace CodeQuest.Tests;

public class SmokeTests
{
    [Fact]
    public void ContentFolder_IsCopiedToOutput()
    {
        var contentFolder = Path.Combine(AppContext.BaseDirectory, "content");

        Assert.True(Directory.Exists(contentFolder), $"Missing folder: {contentFolder}");
    }
}
