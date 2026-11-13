namespace CodeQuest.Core.Running;

public abstract record RunResult;

public sealed record CodeRejected(LocalizedText Reason, int? Line) : RunResult;

public sealed record CompilationFailed(IReadOnlyList<CompilationError> Errors) : RunResult;

public sealed record ChecksFailed(IReadOnlyList<CheckOutcome> Checks) : RunResult;

public sealed record Passed(int StatementCount, IReadOnlyList<CheckOutcome> Checks) : RunResult;

public sealed record TimedOut() : RunResult;

public sealed record RuntimeError(LocalizedText Explanation, string Message, int? Line) : RunResult;
