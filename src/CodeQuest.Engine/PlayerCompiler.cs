using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodeQuest.Core;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    private const string CannotConvertImplicitlyErrorId = "CS0266";

    private const string MissingMemberErrorId = "CS0117";
    private const string MissingMemberOnTypeErrorId = "CS1061";
    private const string WrongArgumentCountErrorId = "CS1501";
    private const string MissingArgumentErrorId = "CS7036";
    private const string DuplicateMethodErrorId = "CS0111";
    private const string InstanceMemberInStaticClassErrorId = "CS0708";
    private const string ObjectReferenceRequiredErrorId = "CS0120";

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

        var validationTree = CSharpSyntaxTree.ParseText(request.ValidationSource, path: "validation.cs", encoding: Encoding.UTF8);
        var guardTree = CSharpSyntaxTree.ParseText(LoopGuardRewriter.GuardSource, path: "loopguard.cs", encoding: Encoding.UTF8);
        var usingsTree = CSharpSyntaxTree.ParseText(ImplicitUsingsSource, path: "usings.cs", encoding: Encoding.UTF8);
        SyntaxTree[] otherTrees = [validationTree, guardTree, usingsTree];

        // The player's own code is checked and diagnosed exactly as written, so that error
        // lines and columns are never shifted by the guards that are injected afterwards.
        var written = CSharpSyntaxTree.ParseText(source, path: "player.cs", encoding: Encoding.UTF8);
        var writtenCompilation = CreateCompilation(otherTrees.Prepend(written));
        var writtenModel = writtenCompilation.GetSemanticModel(written);

        var rejected = PlayerMemberChecker.Check(written.GetRoot(), markerIndex, playerLineCount)
            ?? ForbiddenApiChecker.Check(writtenModel, written.GetRoot(), markerIndex, playerLineCount);
        if (rejected is not null)
        {
            return new CompileOutcome(null, null, [], markerIndex, playerLineCount, rejected);
        }

        var culture = CultureInfo.GetCultureInfo(language == Language.Pt ? "pt-BR" : "en");
        var writtenErrors = writtenCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => ToCompilationError(d, written, markerIndex, playerLineCount, culture))
            .ToList();
        writtenErrors = DropRepeatsWithoutLine(writtenErrors);
        if (writtenErrors.Count > 0)
        {
            return new CompileOutcome(null, null, writtenErrors, markerIndex, playerLineCount, null);
        }

        var playerTree = GuardPlayerCode(written, writtenModel, markerIndex, playerLineCount);
        var compilation = CreateCompilation(otherTrees.Prepend(playerTree));

        using var assembly = new MemoryStream();
        using var pdb = new MemoryStream();
        var emitResult = compilation.Emit(
            assembly,
            pdb,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        var errors = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => ToCompilationError(d, playerTree, markerIndex, playerLineCount, culture))
            .ToList();

        return emitResult.Success
            ? new CompileOutcome(assembly.ToArray(), pdb.ToArray(), errors, markerIndex, playerLineCount, null)
            : new CompileOutcome(null, null, errors, markerIndex, playerLineCount, null);
    }

    // One mistake can surface twice: at the declaration (with the player line) and at a call in the validation
    // (without a line, naming internals). Only the first one helps the player.
    private static List<CompilationError> DropRepeatsWithoutLine(List<CompilationError> errors) => errors
        .Where(error => error.Line is not null
            || !errors.Any(other => other.Line is not null && other.Explanation == error.Explanation))
        .ToList();

    private static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> trees) => CSharpCompilation.Create(
        "PlayerAssembly_" + Guid.NewGuid().ToString("N"),
        trees,
        References.Value,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static SyntaxTree GuardPlayerCode(
        SyntaxTree written,
        SemanticModel writtenModel,
        int firstPlayerLine,
        int playerLineCount)
    {
        var lines = written.GetText().Lines;
        var playerSpan = TextSpan.FromBounds(
            lines[firstPlayerLine].Start,
            lines[firstPlayerLine + playerLineCount - 1].End);
        var guarded = new LoopGuardRewriter(playerSpan, writtenModel).Visit(written.GetRoot());
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
        MissingNameErrorId => ExplainMissingName(diagnostic),
        MissingReturnErrorId => PlayerMessages.MissingReturn,
        UnassignedVariableErrorId => PlayerMessages.UnassignedVariable(SpanText(diagnostic)),
        CannotConvertErrorId or CannotConvertArgumentErrorId or CannotConvertImplicitlyErrorId =>
            ExplainTypeMismatch(diagnostic),
        MissingMemberErrorId or MissingMemberOnTypeErrorId => ExplainMissingMethod(diagnostic),
        WrongArgumentCountErrorId => ExplainWrongArgumentCount(diagnostic),
        MissingArgumentErrorId => ExplainMissingArguments(diagnostic),
        DuplicateMethodErrorId => ExplainDuplicateMethod(diagnostic),
        InstanceMemberInStaticClassErrorId or ObjectReferenceRequiredErrorId => ExplainMissingStatic(diagnostic),
        _ => null,
    };

    // An unknown name that is being called is a missing method, not a missing variable.
    private static LocalizedText ExplainMissingName(Diagnostic diagnostic) =>
        InvokedName(diagnostic) is { } method
            ? PlayerMessages.MissingMethod(method)
            : PlayerMessages.MissingVariable(SpanText(diagnostic));

    // A missing member is only a missing method when the player is calling it (`x.Heal()`);
    // `s.Lenght` or `Math.Pii` fall back to the generic explanation.
    private static LocalizedText? ExplainMissingMethod(Diagnostic diagnostic) =>
        InvokedName(diagnostic) is { } method ? PlayerMessages.MissingMethod(method) : null;

    // The name at the diagnostic when it is the target of an invocation, without type arguments.
    private static string? InvokedName(Diagnostic diagnostic)
    {
        var node = diagnostic.Location.SourceTree!.GetRoot().FindNode(diagnostic.Location.SourceSpan);
        if (node is not SimpleNameSyntax name)
        {
            return null;
        }

        SyntaxNode target = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
        return target.Parent is InvocationExpressionSyntax invocation && invocation.Expression == target
            ? name.Identifier.Text
            : null;
    }

    private static LocalizedText? ExplainWrongArgumentCount(Diagnostic diagnostic)
    {
        var count = ArgumentCount().Match(diagnostic.GetMessage(EnglishCulture));
        return QuotedNames(diagnostic) is [var method, ..] && count.Success
            ? PlayerMessages.WrongArgumentCount(method, int.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture))
            : null;
    }

    // "...required parameter 'b' of 'Spell.Heal<T>(T, T)'": the method is the name before the parenthesis.
    private static LocalizedText? ExplainMissingArguments(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var parameter, var signature, ..]
            && signature.Split('(')[0].Split('<')[0].Split('.')[^1] is { Length: > 0 } method
            ? PlayerMessages.MissingArguments(method, parameter)
            : null;

    // "Type 'Spell' already defines a member called 'Heal' with the same parameter types".
    private static LocalizedText? ExplainDuplicateMethod(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [_, var method, ..] ? PlayerMessages.DuplicateMethod(method) : null;

    // The span is `Heal` or, for a call like `Spell.Heal(1)`, the whole `Spell.Heal`.
    private static LocalizedText? ExplainMissingStatic(Diagnostic diagnostic)
    {
        var name = SpanText(diagnostic).Split('.')[^1];
        return IsIdentifier(name) ? PlayerMessages.MissingStatic(name) : null;
    }

    private static List<string> QuotedNames(Diagnostic diagnostic) => QuotedName()
        .Matches(diagnostic.GetMessage(EnglishCulture))
        .Select(match => match.Groups[1].Value)
        .ToList();

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
        var name = IsIdentifier(spanText) ? spanText : AssignedVariable(diagnostic);
        return PlayerMessages.TypeMismatch(name, types[0].Groups[1].Value, types[1].Groups[1].Value);
    }

    // For `int health = 50.5;` the error sits on the literal, so the variable
    // receiving the value is looked up from the surrounding declaration or assignment.
    private static string? AssignedVariable(Diagnostic diagnostic)
    {
        var value = diagnostic.Location.SourceTree!.GetRoot().FindNode(diagnostic.Location.SourceSpan);
        var name = value.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.Text,
            AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment when assignment.Right == value =>
                left.Identifier.Text,
            _ => null,
        };
        return name is not null && IsIdentifier(name) ? name : null;
    }

    private static bool IsIdentifier(string text) =>
        SyntaxFacts.IsValidIdentifier(text) && SyntaxFacts.GetKeywordKind(text) == SyntaxKind.None;

    private static string SpanText(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    [GeneratedRegex(@"takes (\d+) arguments")]
    private static partial Regex ArgumentCount();

    [GeneratedRegex("'([^']+)'")]
    private static partial Regex QuotedName();

    private static IReadOnlyList<MetadataReference> LoadReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
}

