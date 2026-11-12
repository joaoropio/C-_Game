using CodeQuest.Core;

namespace CodeQuest.Tests.Core;

public class LocalizedTextTests
{
    [Fact]
    public void LocalizedText_Get_ReturnsRequestedLanguage()
    {
        var text = new LocalizedText("Olá", "Hello");

        Assert.Equal("Olá", text.Get(Language.Pt));
        Assert.Equal("Hello", text.Get(Language.En));
    }
}
