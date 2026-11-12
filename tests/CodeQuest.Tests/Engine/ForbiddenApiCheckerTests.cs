using CodeQuest.Core.Running;
using CodeQuest.Engine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeQuest.Tests.Engine;

public class ForbiddenApiCheckerTests
{
    private const string Header = """
        using System;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;
        public static class Spell
        {
            public static void Cast()
            {

        """;

    private static readonly int PlayerLineOffset = Header.Split('\n').Length - 1;

    private static readonly IReadOnlyList<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

    private static CodeRejected? Check(string playerCode)
    {
        var tree = CSharpSyntaxTree.ParseText(Header + playerCode + "\n}\n}");
        var compilation = CSharpCompilation.Create(
            "CheckerTests",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return ForbiddenApiChecker.Check(
            compilation.GetSemanticModel(tree),
            tree.GetRoot(),
            PlayerLineOffset,
            playerCode.Split('\n').Length);
    }

    private static void AssertRejected(string playerCode, string expectedSymbol)
    {
        var rejected = Check(playerCode);

        Assert.NotNull(rejected);
        Assert.Contains(expectedSymbol, rejected.Reason.En);
        Assert.Contains(expectedSymbol, rejected.Reason.Pt);
    }

    [Fact]
    public void Check_ThreadSleep_Rejected() => AssertRejected("Thread.Sleep(10);", "System.Threading.Thread");

    [Fact]
    public void Check_EnvironmentExit_Rejected() => AssertRejected("Environment.Exit(0);", "System.Environment");

    [Fact]
    public void Check_FileReadAllText_Rejected() => AssertRejected("var t = File.ReadAllText(\"x\");", "System.IO.File");

    [Fact]
    public void Check_TaskRun_Rejected() => AssertRejected("Task.Run(() => 1);", "System.Threading.Tasks.Task");

    [Fact]
    public void Check_FullyQualifiedProcessStart_Rejected() =>
        AssertRejected("System.Diagnostics.Process.Start(\"x\");", "System.Diagnostics");

    [Fact]
    public void Check_MathAndStrings_Accepted()
    {
        var rejected = Check("var m = Math.Max(1, 2);\nvar s = string.Join(\",\", \"a\", \"b\");\nConsole.WriteLine(s);");

        Assert.Null(rejected);
    }

    [Fact]
    public void Check_ReportsPlayerLine()
    {
        var rejected = Check("int a = 1;\nint b = 2;\nEnvironment.Exit(a + b);");

        Assert.NotNull(rejected);
        Assert.Equal(3, rejected.Line);
        Assert.Contains("(linha 3)", rejected.Reason.Pt);
        Assert.Contains("(line 3)", rejected.Reason.En);
    }
}
