using System.Reflection;
using CodeQuest.Core;
using CodeQuest.Core.Running;

namespace CodeQuest.Engine;

public sealed class RoslynCodeRunner : ICodeRunner
{
    internal const string ValidationTypeName = "Validation";
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

        var outcome = _compiler.Compile(request);
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

            // A validation that caught the guard's exception must not turn the failure into a pass.
            return RuntimeErrorExplainer.AfterTrip(TripKind(run))
                ?? (checks.Any(check => !check.Passed)
                    ? new ChecksFailed(checks)
                    : new Passed(StatementCounter.Count(playerCode, outcome.Scope), checks));
        }
        catch (Exception exception)
        {
            return RuntimeErrorExplainer.Classify(exception, outcome.PlayerLineOffset, outcome.PlayerLineCount, TripKind(run));
        }
    }

    private static int TripKind(MethodInfo run) => (int)run.DeclaringType!.Assembly
        .GetType(LoopGuardRewriter.GuardTypeName)!
        .GetProperty(LoopGuardRewriter.TripKindName, BindingFlags.Public | BindingFlags.Static)!
        .GetValue(null)!;

    private static MethodInfo FindValidationMethod(Assembly assembly)
    {
        var validation = assembly.GetTypes().Where(type => type.Name == ValidationTypeName).ToList() is [var single]
            ? single
            : throw new InvalidOperationException($"The validation source must declare exactly one type named '{ValidationTypeName}'.");
        var run = validation.GetMethod(ValidationMethodName, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"'{ValidationTypeName}' has no public static parameterless method named '{ValidationMethodName}'.");
        return typeof(IEnumerable<(string, string, string)>).IsAssignableFrom(run.ReturnType)
            ? run
            : throw new InvalidOperationException(
                $"'{ValidationTypeName}.{ValidationMethodName}' must return IEnumerable<(string Case, string Expected, string Actual)>.");
    }
}