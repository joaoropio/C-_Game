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
        KeyNotFoundException => PlayerMessages.KeyNotInDictionary,
        FormatException => PlayerMessages.UnexpectedFormat,
        InsufficientExecutionStackException => PlayerMessages.InfiniteRecursion,
        _ => PlayerMessages.SpellFailedWhileRunning,
    };

    // The frame that called __LoopGuard.Enter is the function being entered, positioned at its guard
    // (often its opening brace); the useful line is the one that called that function.
    private static int? PlayerLine(Exception exception, int playerLineOffset, int playerLineCount)
    {
        var frames = new StackTrace(exception, fNeedFileInfo: true).GetFrames();
        return frames
            .Where((_, index) => index == 0 || !IsGuardEntry(frames[index - 1]))
            .Where(frame => Path.GetFileName(frame.GetFileName()) == PlayerFileName)
            .Select(frame => frame.GetFileLineNumber() - playerLineOffset)
            .Where(line => line >= 1 && line <= playerLineCount)
            .Select(line => (int?)line)
            .FirstOrDefault();
    }

    private static bool IsGuardEntry(StackFrame frame) =>
        frame.GetMethod() is { Name: "Enter", DeclaringType.Name: "__LoopGuard" };
}
