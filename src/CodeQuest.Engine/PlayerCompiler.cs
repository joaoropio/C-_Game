using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodeQuest.Core;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace CodeQuest.Engine;

internal sealed record CompileOutcome(
    byte[]? Assembly,
    byte[]? Pdb,
    IReadOnlyList<CompilationError> Errors,
    int PlayerLineOffset,
    int PlayerLineCount,
    CodeRejected? Rejected);

internal sealed partial class PlayerCompiler
{
    public const string ImplicitUsingsSource = """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using System.Text;
        """;

    private const string MissingNameErrorId = "CS0103";
    private const string MissingReturnErrorId = "CS0161";
    private const string UnassignedVariableErrorId = "CS0165";
    private const string CannotConvertErrorId = "CS0029";
    private const string CannotConvertArgumentErrorId = "CS1503";

    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadReferences);

    public CompileOutcome Compile(RunRequest request, Language language)
    {
        var templateLines = request.Template.ReplaceLineEndings("\n").Split('\n');
        var markerIndex = Array.FindIndex(templateLines, PlayerCodeMarker.IsMarkerLine);
        var playerLineCount = request.PlayerCode.ReplaceLineEndings("\n").Split('\n').Length;
        var source = string.Join(
            "\n",
            templateLines[..markerIndex].Append(request.PlayerCode).Concat(templateLines[(markerIndex + 1)..]));

        var playerTree = GuardLoops(source, markerIndex, playerLineCount);
        var validationTree = CSharpSyntaxTree.ParseText(request.ValidationSource, path: "validation.cs", encoding: Encoding.UTF8);
        var guardTree = CSharpSyntaxTree.ParseText(LoopGuardRewriter.GuardSource, path: "loopguard.cs", encoding: Encoding.UTF8);
        var usingsTree = CSharpSyntaxTree.ParseText(ImplicitUsingsSource, path: "usings.cs", encoding: Encoding.UTF8);
        var compilation = CSharpCompilation.Create(
            "PlayerAssembly_" + Guid.NewGuid().ToString("N"),
            [playerTree, validationTree, guardTree, usingsTree],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var rejected = ForbiddenApiChecker.Check(
            compilation.GetSemanticModel(playerTree),
            playerTree.GetRoot(),
            markerIndex,
            playerLineCount);
        if (rejected is not null)
        {
            return new CompileOutcome(null, null, [], markerIndex, playerLineCount, rejected);
        }

        using var assembly = new MemoryStream();
        using var pdb = new MemoryStream();
        var emitResult = compilation.Emit(
            assembly,
            pdb,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        var culture = CultureInfo.GetCultureInfo(language == Language.Pt ? "pt-BR" : "en");
        var errors = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => ToCompilationError(d, playerTree, markerIndex, playerLineCount, culture))
            .ToList();

        return emitResult.Success
            ? new CompileOutcome(assembly.ToArray(), pdb.ToArray(), errors, markerIndex, playerLineCount, null)
            : new CompileOutcome(null, null, errors, markerIndex, playerLineCount, null);
    }

    private static SyntaxTree GuardLoops(string source, int firstPlayerLine, int playerLineCount)
    {
        var parsed = CSharpSyntaxTree.ParseText(source, path: "player.cs", encoding: Encoding.UTF8);
        var lines = parsed.GetText().Lines;
        var playerSpan = TextSpan.FromBounds(
            lines[firstPlayerLine].Start,
            lines[firstPlayerLine + playerLineCount - 1].End);
        var guarded = new LoopGuardRewriter(playerSpan).Visit(parsed.GetRoot());
        return CSharpSyntaxTree.ParseText(guarded.ToFullString(), path: "player.cs", encoding: Encoding.UTF8);
    }

    private static CompilationError ToCompilationError(
        Diagnostic diagnostic,
        SyntaxTree playerTree,
        int lineOffset,
        int playerLineCount,
        CultureInfo culture)
    {
        var message = diagnostic.GetMessage(culture);
        var position = diagnostic.Location.GetLineSpan().StartLinePosition;
        var playerLine = position.Line + 1 - lineOffset;
        var insidePlayerCode = diagnostic.Location.SourceTree == playerTree
            && playerLine >= 1
            && playerLine <= playerLineCount;
        var explanation = Explain(diagnostic);
        if (insidePlayerCode)
        {
            return new CompilationError(playerLine, position.Character + 1, message, explanation);
        }

        return new CompilationError(null, null, message, explanation ?? PlayerMessages.UnreadableSpell);
    }

    private static LocalizedText? Explain(Diagnostic diagnostic) => diagnostic.Id switch
    {
        MissingNameErrorId => PlayerMessages.MissingVariable(SpanText(diagnostic)),
        MissingReturnErrorId => PlayerMessages.MissingReturn,
        UnassignedVariableErrorId => PlayerMessages.UnassignedVariable(SpanText(diagnostic)),
        CannotConvertErrorId or CannotConvertArgumentErrorId => ExplainTypeMismatch(diagnostic),
        _ => null,
    };

    private static LocalizedText? ExplainTypeMismatch(Diagnostic diagnostic)
    {
        // Diagnostic arguments are not public API, so the two types are read from the
        // quoted names of the English message, which is stable across compiler cultures.
        var types = QuotedName().Matches(diagnostic.GetMessage(EnglishCulture));
        if (types.Count < 2)
        {
            return null;
        }

        var spanText = SpanText(diagnostic);
        var name = IsIdentifier(spanText) ? spanText : null;
        return PlayerMessages.TypeMismatch(name, types[0].Groups[1].Value, types[1].Groups[1].Value);
    }

    private static bool IsIdentifier(string text) =>
        SyntaxFacts.IsValidIdentifier(text) && SyntaxFacts.GetKeywordKind(text) == SyntaxKind.None;

    private static string SpanText(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    [GeneratedRegex("'([^']+)'")]
    private static partial Regex QuotedName();

    private static IReadOnlyList<MetadataReference> LoadReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
}

