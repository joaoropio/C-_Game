namespace CodeQuest.Core.Running;

public sealed record CompilationError(int? Line, int? Column, string Message, LocalizedText? Explanation);
