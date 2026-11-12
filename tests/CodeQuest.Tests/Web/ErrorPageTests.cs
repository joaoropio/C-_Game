using Bunit;
using CodeQuest.Web.Components.Pages;

namespace CodeQuest.Tests.Web;

public sealed class ErrorPageTests : BunitContext
{
    [Fact]
    public void ErrorPage_ShowsBothLanguages()
    {
        this.AddGameServices();

        var cut = Render<Error>();

        Assert.Contains("Algo correu mal", cut.Markup);
        Assert.Contains("Something went wrong", cut.Markup);
        Assert.DoesNotContain("ASPNETCORE_ENVIRONMENT", cut.Markup);
    }
}