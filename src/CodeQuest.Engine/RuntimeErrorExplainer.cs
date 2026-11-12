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
        DivideByZeroException => PlayerMessages.DividedByZero,
        NullReferenceException => PlayerMessages.UsedNull,
        IndexOutOfRangeException or ArgumentOutOfRangeException => PlayerMessages.PositionDoesNotExist,
        FormatException => PlayerMessages.UnexpectedFormat,
        InsufficientExecutionStackException => PlayerMessages.InfiniteRecursion,
        _ => PlayerMessages.SpellFailedWhileRunning,
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
