using CodeQuest.Core.Running;
using CodeQuest.Engine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeQuest.Tests.Engine;

public class ForbiddenApiCheckerTests
{
    private const string Header = """
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;
        public class Hero { public int Health; }
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
        var usings = CSharpSyntaxTree.ParseText(PlayerCompiler.ImplicitUsingsSource);
        var compilation = CSharpCompilation.Create(
            "CheckerTests",
            [tree, usings],
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
    public void Check_ArrayList_Rejected() => AssertRejected("new System.Collections.ArrayList();", "System.Collections.ArrayList");

    [Fact]
    public void Check_NonGenericIEnumerable_Rejected() =>
        AssertRejected("System.Collections.IEnumerable e = new int[0];", "System.Collections.IEnumerable");

    [Fact]
    public void Check_TimeProvider_Rejected() => AssertRejected("var t = TimeProvider.System;", "System.TimeProvider");

    [Fact]
    public void Check_AsParallel_Rejected() => AssertRejected("new[] { 1 }.AsParallel();", "AsParallel");

    [Fact]
    public void Check_ConsoleSetOut_Rejected() => AssertRejected("Console.SetOut(null!);", "System.Console.SetOut");

    [Fact]
    public void Check_Dynamic_Rejected() => AssertRejected("dynamic d = 1;", "dynamic");

    [Fact]
    public void Check_DelegateCreateDelegate_Rejected() =>
        AssertRejected("Delegate.CreateDelegate(null, null, \"x\");", "System.Delegate");

    [Fact]
    public void Check_XmlDocument_Rejected() => AssertRejected("new System.Xml.XmlDocument();", "System.Xml");

    [Fact]
    public void Check_Registry_Rejected() => AssertRejected("Microsoft.Win32.Registry.CurrentUser.GetType();", "Microsoft");

    [Fact]
    public void Check_ConsoleReadLine_Rejected() => AssertRejected("Console.ReadLine();", "System.Console.ReadLine");

    [Fact]
    public void Check_TypeGetType_Rejected() => AssertRejected("Type.GetType(\"x\");", "System.Type");

    [Theory]
    [InlineData("var m = Math.Max(1, 2);")]
    [InlineData("var s = string.Join(\",\", \"a\", \"b\");")]
    [InlineData("Console.WriteLine(\"hi\");")]
    [InlineData("var l = new List<int>(); l.Add(1);")]
    [InlineData("var b = new StringBuilder(); b.Append(1);")]
    [InlineData("var q = new[] { 1, 2 }.Where(x => x > 1).ToList();")]
    [InlineData("var h = new Hero(); h.Health++;")]
    [InlineData("var l = new System.Collections.Generic.List<int>();")]
    public void Check_AllowedApis_Accepted(string playerCode) => Assert.Null(Check(playerCode));

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

    [Theory]
    [InlineData("Span<int> s = stackalloc int[10];")]
    [InlineData("Span<int> s = stackalloc[] { 1, 2 };")]
    [InlineData("int a = 1;\nvar s = stackalloc int[a];")]
    public void Check_Stackalloc_RejectedOnItsLine(string playerCode)
    {
        var rejected = Check(playerCode);

        Assert.NotNull(rejected);
        Assert.Equal(playerCode.Split('\n').Length, rejected.Line);
        Assert.Equal("stackalloc não é permitido nesta masmorra.", rejected.Reason.Pt);
        Assert.Equal("stackalloc is not allowed in this dungeon.", rejected.Reason.En);
    }
}
