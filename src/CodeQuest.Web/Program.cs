using CodeQuest.Core.Running;
using CodeQuest.Engine;
using CodeQuest.Web.Components;
using CodeQuest.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(new GameContent(Path.Combine(AppContext.BaseDirectory, "content")));
builder.Services.AddSingleton<ICodeRunner, RoslynCodeRunner>();
builder.Services.AddScoped<UiText>();
builder.Services.AddScoped<ProgressStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }