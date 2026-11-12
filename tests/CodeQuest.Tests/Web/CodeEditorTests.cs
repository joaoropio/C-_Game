using Bunit;
using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Web.Components;
using CodeQuest.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CodeQuest.Tests.Web;

public sealed class CodeEditorTests : BunitContext
{
    private const string ModulePath = "./Components/CodeEditor.razor.js";

    private readonly BunitJSModuleInterop module;

    public CodeEditorTests()
    {
        this.AddGameServices();
        module = JSInterop.SetupModule(ModulePath);
        module.Setup<string>("getValue", _ => true).SetResult("int x = 1;");
        module.SetupVoid("setValue", _ => true).SetVoidResult();
        module.SetupVoid("insert", _ => true).SetVoidResult();
        module.SetupVoid("setErrors", _ => true).SetVoidResult();
        module.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private JSRuntimeInvocationHandler<int?> SetupCreate() => module.Setup<int?>("create", _ => true);

    private IRenderedComponent<CodeEditor> RenderEditor()
    {
        SetupCreate().SetResult(7);
        return Render<CodeEditor>();
    }

    private void RenderEditorWith(Action<ComponentParameterCollectionBuilder<CodeEditor>> parameters)
    {
        SetupCreate().SetResult(7);
        Render(parameters);
    }

    [Fact]
    public void FirstRender_CreatesEditorWithInitialCode()
    {
        RenderEditorWith(parameters => parameters.Add(editor => editor.InitialCode, "Console.WriteLine(1);"));

        var create = Assert.Single(module.Invocations["create"]);
        Assert.Equal("Console.WriteLine(1);", create.Arguments[1]);
    }

    [Fact]
    public async Task GetCode_ReturnsValueFromJs()
    {
        var cut = RenderEditor();

        var code = await cut.InvokeAsync(() => cut.Instance.GetCodeAsync());

        Assert.Equal("int x = 1;", code);
        Assert.Equal(7, Assert.Single(module.Invocations["getValue"]).Arguments[0]);
    }

    [Fact]
    public async Task SetCode_CallsJsSetValue()
    {
        var cut = RenderEditor();

        await cut.InvokeAsync(() => cut.Instance.SetCodeAsync("new code"));

        var call = Assert.Single(module.Invocations["setValue"]);
        Assert.Equal([7, "new code"], call.Arguments);
    }

    [Fact]
    public async Task ShowErrors_SendsOnlyErrorsWithLine()
    {
        var cut = RenderEditor();
        var errors = new[]
        {
            new CompilationError(3, 5, "technical", new LocalizedText("explicação", "explanation")),
            new CompilationError(null, null, "no line", null),
            new CompilationError(4, null, "plain", null),
        };

        await cut.InvokeAsync(() => cut.Instance.ShowErrorsAsync(errors));

        var sent = Assert.Single(module.Invocations["setErrors"]).Arguments[1];
        var markers = Assert.IsAssignableFrom<System.Collections.IEnumerable>(sent).Cast<object>()
            .Select(marker => marker.ToString()!).ToList();
        Assert.Equal(2, markers.Count);
        Assert.Contains("Line = 3, Column = 5, Message = explicação", markers[0]);
        Assert.Contains("Line = 4, Column = 1, Message = plain", markers[1]);
    }

    [Fact]
    public async Task ShowErrors_UsesExplanationInCurrentLanguage()
    {
        var cut = RenderEditor();
        await Services.GetRequiredService<UiText>().SetLanguageAsync(Language.En);

        await cut.InvokeAsync(() => cut.Instance.ShowErrorsAsync(
            [new CompilationError(1, 1, "technical", new LocalizedText("explicação", "explanation"))]));

        var marker = ((System.Collections.IEnumerable)module.Invocations["setErrors"].Single().Arguments[1]!)
            .Cast<object>().Single();
        Assert.Contains("Message = explanation", marker.ToString());
    }

    [Fact]
    public async Task ClearErrors_SendsEmptyList()
    {
        var cut = RenderEditor();

        await cut.InvokeAsync(() => cut.Instance.ClearErrorsAsync());

        var sent = Assert.Single(module.Invocations["setErrors"]).Arguments[1];
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(sent));
    }

    [Fact]
    public async Task Insert_CallsJsInsert()
    {
        var cut = RenderEditor();

        await cut.InvokeAsync(() => cut.Instance.InsertAsync("if (x)"));

        Assert.Equal([7, "if (x)"], Assert.Single(module.Invocations["insert"]).Arguments);
    }

    [Fact]
    public async Task Dispose_DisposesModule()
    {
        var cut = RenderEditor();

        await cut.Instance.DisposeAsync();

        Assert.Equal(7, Assert.Single(module.Invocations["dispose"]).Arguments[0]);
    }

    [Fact]
    public async Task Dispose_WhenDisconnected_DoesNotThrow()
    {
        var cut = RenderEditor();
        module.SetupVoid("dispose", _ => true).SetException(new JSDisconnectedException("gone"));

        await cut.Instance.DisposeAsync();
    }

    [Fact]
    public async Task Methods_BeforeCreateCompletes_WaitAndThenWork()
    {
        var create = SetupCreate();
        var cut = Render<CodeEditor>();

        var pending = cut.InvokeAsync(() => cut.Instance.GetCodeAsync());
        await Task.Delay(50);
        Assert.False(pending.IsCompleted);
        create.SetResult(7);

        Assert.Equal("int x = 1;", await pending);
    }

    [Fact]
    public void Create_WhenItFails_ShowsLocalizedMessage()
    {
        SetupCreate().SetException(new JSException("boom"));

        var cut = Render<CodeEditor>();

        cut.WaitForAssertion(() =>
            Assert.Contains("Não foi possível carregar o editor", cut.Find(".code-editor-error").TextContent));
    }

    [Fact]
    public async Task Methods_AfterCreateFailed_DoNotThrowAndReturnInitialCode()
    {
        SetupCreate().SetException(new JSException("boom"));
        var cut = Render<CodeEditor>(parameters => parameters.Add(editor => editor.InitialCode, "start"));

        Assert.Equal("start", await cut.InvokeAsync(() => cut.Instance.GetCodeAsync()));
        await cut.InvokeAsync(() => cut.Instance.SetCodeAsync("x"));
        await cut.InvokeAsync(() => cut.Instance.InsertAsync("x"));
        await cut.InvokeAsync(() => cut.Instance.ShowErrorsAsync([new CompilationError(1, 1, "m", null)]));
        await cut.InvokeAsync(() => cut.Instance.ClearErrorsAsync());
        await cut.Instance.DisposeAsync();

        Assert.Empty(module.Invocations["setValue"]);
        Assert.Empty(module.Invocations["setErrors"]);
    }

    [Fact]
    public async Task Dispose_CalledTwice_DisposesOnce()
    {
        var cut = RenderEditor();

        await cut.Instance.DisposeAsync();
        await cut.Instance.DisposeAsync();

        Assert.Single(module.Invocations["dispose"]);
    }

    [Fact]
    public async Task Create_WhenItTimesOut_ShowsMessageAndMethodsReturnSafely()
    {
        SetupCreate().SetCanceled();
        var cut = Render<CodeEditor>(parameters => parameters.Add(editor => editor.InitialCode, "start"));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".code-editor-error")));
        Assert.Equal("start", await cut.InvokeAsync(() => cut.Instance.GetCodeAsync()));
        await cut.InvokeAsync(() => cut.Instance.SetCodeAsync("x"));
    }
}
