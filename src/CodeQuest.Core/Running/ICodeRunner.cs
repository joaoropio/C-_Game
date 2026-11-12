namespace CodeQuest.Core.Running;

public interface ICodeRunner
{
    Task<RunResult> RunAsync(RunRequest request, Language language, CancellationToken cancellationToken);
}
