using Bunit;
using CodeQuest.Core.Running;
using CodeQuest.Engine;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CodeQuest.Tests.Web;

internal static class TestContextExtensions
{
    public const string LanguageKey = "cq.language";
    public const string ProgressKey = "cq.progress";
    public const string SetItem = "localStorage.setItem";

    public static void AddGameServices(this BunitContext context, string? savedLanguage = null, string? savedProgress = null)
    {
        context.AddGameServicesWithoutStorage();
        context.SetupStorage(savedLanguage, savedProgress);
    }

    public static void AddGameServicesWithoutStorage(this BunitContext context)
    {
        context.Services.AddSingleton(LoadContent());
        context.Services.AddSingleton<ICodeRunner, RoslynCodeRunner>();
        context.Services.AddScoped<UiText>();
        context.Services.AddScoped<ProgressStore>();
    }

    public static GameContent LoadContent() => new(Path.Combine(AppContext.BaseDirectory, "content"));

    public static void SetupStorage(this BunitContext context, string? savedLanguage, string? savedProgress)
    {
        context.JSInterop.Setup<string?>("localStorage.getItem", LanguageKey).SetResult(savedLanguage);
        context.JSInterop.Setup<string?>("localStorage.getItem", ProgressKey).SetResult(savedProgress);
        context.JSInterop.SetupVoid(SetItem, _ => true).SetVoidResult();
    }

    public static string LastSavedValue(this BunitContext context, string key) =>
        context.JSInterop.Invocations[SetItem]
            .Last(invocation => (string?)invocation.Arguments[0] == key)
            .Arguments[1]!.ToString()!;
}

