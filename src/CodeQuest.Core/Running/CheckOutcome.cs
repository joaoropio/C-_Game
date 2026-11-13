namespace CodeQuest.Core.Running;

public sealed record CheckOutcome(string Case, string Expected, string Actual)
{
    public bool Passed => string.Equals(Expected, Actual, StringComparison.Ordinal);
}
