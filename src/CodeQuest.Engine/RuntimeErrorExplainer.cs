using System.Diagnostics;
using System.Reflection;
using CodeQuest.Core;
using CodeQuest.Core.Running;

namespace CodeQuest.Engine;

internal static class RuntimeErrorExplainer
{
    private const string PlayerFileName = "player.cs";

    // What the validation threw is rarely what failed: Invoke and a type initializer wrap the real cause, and
    // the library wraps an exception thrown by a player comparer. The guard knows best why it tripped (a
    // limit means TimedOut, the stack means recursion), even when the exception did not reach us.
    public static RunResult Classify(Exception thrown, int playerLineOffset, int playerLineCount, int trip)
    {
        if (trip == LoopGuardRewriter.LimitTrip)
        {
            return new TimedOut();
        }

        var chain = ChainOf(thrown).ToList();
        var cause = chain.FirstOrDefault(exception => exception is InsufficientExecutionStackException)
            ?? chain.FirstOrDefault(exception => exception is not (TargetInvocationException or TypeInitializationException))
            ?? thrown;
        if (trip == LoopGuardRewriter.StackTrip && cause is not InsufficientExecutionStackException)
        {
            return AfterTrip(trip)!;
        }

        var failedType = chain.OfType<TypeInitializationException>().FirstOrDefault()?.TypeName;
        return Explain(cause, playerLineOffset, playerLineCount, failedType);
    }

    // The result when the guard tripped but the validation carried on (it caught the exception), else null.
    public static RunResult? AfterTrip(int trip) => trip switch
    {
        LoopGuardRewriter.LimitTrip => new TimedOut(),
        LoopGuardRewriter.StackTrip => new RuntimeError(
            PlayerMessages.InfiniteRecursion,
            new InsufficientExecutionStackException().Message,
            null),
        _ => null,
    };

    public static RuntimeError Explain(Exception exception, int playerLineOffset, int playerLineCount) =>
        Explain(exception, playerLineOffset, playerLineCount, null);

    private static RuntimeError Explain(Exception exception, int playerLineOffset, int playerLineCount, string? failedType) =>
        new(ExplanationFor(exception, failedType), MessageOf(exception), PlayerLine(exception, playerLineOffset, playerLineCount));

    private static IEnumerable<Exception> ChainOf(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            yield return current;
        }
    }

    // The message of an exception type written by the player is player code: it may recurse, loop or throw,
    // and it runs here, outside the validation's own try block.
    private static string MessageOf(Exception exception)
    {
        try
        {
            return exception.Message;
        }
        catch (Exception)
        {
            return exception.GetType().Name;
        }
    }

    private static LocalizedText ExplanationFor(Exception exception, string? failedType) => exception switch
    {
        DivideByZeroException => PlayerMessages.DividedByZero,
        NullReferenceException => PlayerMessages.UsedNull,
        IndexOutOfRangeException or ArgumentOutOfRangeException => PlayerMessages.PositionDoesNotExist,
        KeyNotFoundException => PlayerMessages.KeyNotInDictionary,
        FormatException => PlayerMessages.UnexpectedFormat,
        InsufficientExecutionStackException => PlayerMessages.InfiniteRecursion,
        _ when failedType is not null => PlayerMessages.StaticConstructorFailed(failedType),
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
        frame.GetMethod() is { Name: "Enter" or "EnterValue", DeclaringType.Name: "__LoopGuard" };
}
