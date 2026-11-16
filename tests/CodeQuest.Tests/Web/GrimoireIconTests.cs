using Bunit;
using CodeQuest.Web.Components;

namespace CodeQuest.Tests.Web;

public sealed class GrimoireIconTests : BunitContext
{
    [Theory]
    [InlineData("variables", "box")]
    [InlineData("operators", "scales")]
    [InlineData("conditions", "fork")]
    [InlineData("loops", "hourglass")]
    [InlineData("arrays", "shelf")]
    [InlineData("strings", "quill")]
    [InlineData("methods", "tome")]
    [InlineData("overloads", "fireballs")]
    [InlineData("exceptions", "flask")]
    [InlineData("lists", "backpack")]
    [InlineData("dictionaries", "cabinet")]
    [InlineData("something-new", "book")]
    public void Icon_IsChosenByPageIdWithABookFallback(string pageId, string expectedKind)
    {
        var cut = Render<GrimoireIcon>(parameters => parameters.Add(icon => icon.PageId, pageId));

        var svg = cut.Find("svg.page-icon");
        Assert.Equal(expectedKind, svg.GetAttribute("data-icon"));
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.NotEmpty(svg.QuerySelectorAll("path, circle, rect"));
    }
}
