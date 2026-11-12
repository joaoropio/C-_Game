using System.Diagnostics;
using CodeQuest.Core;
using CodeQuest.Core.Running;

namespace CodeQuest.Engine;

internal static class RuntimeErrorExplainer
{
    private const string PlayerFileName = "player.cs";

    public static RuntimeError Explain(Exception exception, int playerLineOffset, int playerLineCount) =>
        new(ExplanationFor(exception), exception.Message, PlayerLine(exception, playerLineOffset, playerLineCount));

    private static LocalizedText ExplanationFor(Exception exception) => exception switch
    {
        DivideByZeroException => new("Dividiste por zero.", "You divided by zero."),
        NullReferenceException => new(
            "Usaste algo que está vazio (null).",
            "You used something that is empty (null)."),
        IndexOutOfRangeException or ArgumentOutOfRangeException => new(
            "Tentaste usar uma posição que não existe.",
            "You tried to use a position that doesn't exist."),
        FormatException => new("O texto não tem o formato esperado.", "The text isn't in the expected format."),
        _ => new("O feitiço falhou durante a execução.", "The spell failed while running."),
    };

    private static int? PlayerLine(Exception exception, int playerLineOffset, int playerLineCount) =>
        new StackTrace(exception, fNeedFileInfo: true)
            .GetFrames()
            .Where(frame => Path.GetFileName(frame.GetFileName()) == PlayerFileName)
            .Select(frame => frame.GetFileLineNumber() - playerLineOffset)
            .Where(line => line >= 1 && line <= playerLineCount)
            .Select(line => (int?)line)
            .FirstOrDefault();
}
