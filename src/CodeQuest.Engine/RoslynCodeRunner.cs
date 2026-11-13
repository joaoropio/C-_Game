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

        var execution = Task.Factory.StartNew(
            () => Execute(outcome, request.PlayerCode),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
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
            var run = FindValidationMethod(context.LoadFromStream(assemblyStream, pdbStream));
            return RunValidation(run, outcome, playerCode);
        }
        finally
        {
            context.Unload();
        }
    }

    private static RunResult RunValidation(MethodInfo run, CompileOutcome outcome, string playerCode)
    {
        try
        {
            var results = (IEnumerable<(string Case, string Expected, string Actual)>)run.Invoke(null, null)!;
            var checks = results
                .Select(result => new CheckOutcome(result.Case, result.Expected, result.Actual))
                .ToList();
            return checks.Any(check => !check.Passed)
                ? new ChecksFailed(checks)
                : new Passed(StatementCounter.Count(playerCode), checks);
        }
        catch (Exception exception)
        {
            var cause = exception is TargetInvocationException { InnerException: { } inner }
                ? inner
                : exception;
            return cause is TimeoutException
                ? new TimedOut()
                : RuntimeErrorExplainer.Explain(cause, outcome.PlayerLineOffset, outcome.PlayerLineCount);
        }
    }
    private static MethodInfo FindValidationMethod(Assembly assembly)
    {
        var validation = assembly.GetType(ValidationTypeName)
            ?? throw new InvalidOperationException($"The validation source declares no type named '{ValidationTypeName}'.");
        var run = validation.GetMethod(ValidationMethodName, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"'{ValidationTypeName}' has no public static parameterless method named '{ValidationMethodName}'.");
        return typeof(IEnumerable<(string, string, string)>).IsAssignableFrom(run.ReturnType)
            ? run
            : throw new InvalidOperationException(
                $"'{ValidationTypeName}.{ValidationMethodName}' must return IEnumerable<(string Case, string Expected, string Actual)>.");
    }
}