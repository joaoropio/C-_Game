using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

public class RuntimeErrorExplainerTests
{
    [Theory]
    [MemberData(nameof(Explanations))]
    public void Explain_KnownException_UsesExpectedText(Exception exception, string pt, string en)
    {
        var error = RuntimeErrorExplainer.Explain(exception, 0, 1);

        Assert.Equal(new LocalizedText(pt, en), error.Explanation);
        Assert.Equal(exception.Message, error.Message);
    }

    public static TheoryData<Exception, string, string> Explanations => new()
    {
        { new DivideByZeroException(), "Dividiste por zero.", "You divided by zero." },
        { new NullReferenceException(), "Usaste algo que está vazio (null).", "You used something that is empty (null)." },
        { new IndexOutOfRangeException(), "Tentaste usar uma posição que não existe.", "You tried to use a position that doesn't exist." },
        { new ArgumentOutOfRangeException(), "Tentaste usar uma posição que não existe.", "You tried to use a position that doesn't exist." },
        { new KeyNotFoundException(), "Pediste uma chave que o dicionário não tem. Verifica com ContainsKey ou TryGetValue antes de a ler.", "You asked for a key the dictionary does not have. Check with ContainsKey or TryGetValue before reading it." },
        { new FormatException(), "O texto não tem o formato esperado.", "The text isn't in the expected format." },
        { new InsufficientExecutionStackException(), "O feitiço chama-se a si próprio sem parar (recursão infinita).", "The spell keeps calling itself and never stops (infinite recursion)." },
        { new InvalidOperationException(), "O feitiço falhou durante a execução.", "The spell failed while running." },
    };

    [Fact]
    public void Explain_UnknownException_GenericText()
    {
        var error = RuntimeErrorExplainer.Explain(new InvalidCastException("boom"), 0, 1);

        Assert.Equal("The spell failed while running.", error.Explanation.En);
        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public void Explain_FrameOutsidePlayerCode_NullLine()
    {
        var exception = Record.Exception((Action)(() => throw new DivideByZeroException()))!;

        Assert.Null(RuntimeErrorExplainer.Explain(exception, 0, 1000).Line);
    }

    [Fact]
    public void Explain_FrameInPlayerCode_MapsToPlayerLine()
    {
        const string template = "public static class Spell\n{\n    public static int Cast()\n    {\n        //{{PLAYER_CODE}}\n        return 0;\n    }\n}";
        var outcome = new PlayerCompiler().Compile(
            new RunRequest(template, "int a = 0;\nreturn 1 / a;", "public static class Checks { }"));
        var cast = System.Reflection.Assembly.Load(outcome.Assembly!, outcome.Pdb!).GetType("Spell")!.GetMethod("Cast")!;
        var thrown = Assert.Throws<System.Reflection.TargetInvocationException>(() => cast.Invoke(null, null));

        var error = RuntimeErrorExplainer.Explain(thrown.InnerException!, outcome.PlayerLineOffset, 2);

        Assert.Equal(2, error.Line);
    }
}

