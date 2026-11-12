using System.Reflection;
using CodeQuest.Core;
using CodeQuest.Core.Running;

namespace CodeQuest.Engine;

public sealed class RoslynCodeRunner : ICodeRunner
{
    private const string ValidationTypeName = "Validation";
    private const string ValidationMethodName = "Run";

    private static readonly TimeSpan WallClockLimit = TimeSpan.FromSeconds(3);

    private readonly PlayerCompiler _compiler = new();

    public async Task<RunResult> RunAsync(RunRequest request, Language language, CancellationToken cancellationToken)
    {
        var rejected = PlayerCodePrecheck.Check(request.PlayerCode);
        if (rejected is not null)
        {
            return rejected;
        }

        var outcome = _compiler.Compile(request, language);
        if (outcome.Rejected is not null)
        {
            return outcome.Rejected;
        }

        if (outcome.Assembly is null || outcome.Pdb is null)
        {
            return new CompilationFailed(outcome.Errors);
        }

        var execution = Task.Run(() => Execute(outcome, request.PlayerCode), CancellationToken.None);
        try
        {
            return await execution.WaitAsync(WallClockLimit, cancellationToken);
        }
        catch (TimeoutException)
        {
            return new TimedOut();
        }
    }

    private static RunResult Execute(CompileOutcome outcome, string playerCode)
    {
        var context = new PlayerLoadContext();
        try
        {
            using var assemblyStream = new MemoryStream(outcome.Assembly!);
            using var pdbStream = new MemoryStream(outcome.Pdb!);
            var assembly = context.LoadFromStream(assemblyStream, pdbStream);
            var failures = Validate(assembly);
            return failures.Count > 0
                ? new ChecksFailed(failures)
                : new Passed(StatementCounter.Count(playerCode));
        }
        catch (Exception exception)
        {
            var cause = exception is TargetInvocationException { InnerException: { } inner }
                ? inner
                : exception;
            return cause is TimeoutException
                ? new TimedOut()
                : RuntimeErrorExplainer.Explain(cause, outcome.PlayerLineOffset, LineCount(playerCode));
        }
        finally
        {
            context.Unload();
        }
    }

    private static List<LocalizedText> Validate(Assembly assembly)
    {
        var run = assembly.GetType(ValidationTypeName)!.GetMethod(ValidationMethodName)!;
        var results = (IEnumerable<(string Pt, string En)>)run.Invoke(null, null)!;
        return results.Select(result => new LocalizedText(result.Pt, result.En)).ToList();
    }

    private static int LineCount(string playerCode) => playerCode.ReplaceLineEndings("\n").Split('\n').Length;
}

