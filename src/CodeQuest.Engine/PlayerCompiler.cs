using System.Globalization;
using System.Text;
using CodeQuest.Core;
using CodeQuest.Core.Levels;
using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace CodeQuest.Engine;

internal sealed record CompileOutcome(
    byte[]? Assembly,
    byte[]? Pdb,
    IReadOnlyList<CompilationError> Errors,
    int PlayerLineOffset);

internal sealed class PlayerCompiler
{
    private const string MissingNameErrorId = "CS0103";

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadReferences);

    public CompileOutcome Compile(RunRequest request, Language language)
    {
        var templateLines = request.Template.ReplaceLineEndings("\n").Split('\n');
        var markerIndex = Array.FindIndex(templateLines, PlayerCodeMarker.IsMarkerLine);
        var playerLineCount = request.PlayerCode.ReplaceLineEndings("\n").Split('\n').Length;
        var source = string.Join(
            "\n",
            templateLines[..markerIndex].Append(request.PlayerCode).Concat(templateLines[(markerIndex + 1)..]));

        var playerTree = CSharpSyntaxTree.ParseText(source, path: "player.cs", encoding: Encoding.UTF8);
        var validationTree = CSharpSyntaxTree.ParseText(request.ValidationSource, path: "validation.cs", encoding: Encoding.UTF8);
        var compilation = CSharpCompilation.Create(
            "PlayerAssembly_" + Guid.NewGuid().ToString("N"),
            [playerTree, validationTree],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

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
            ? new CompileOutcome(assembly.ToArray(), pdb.ToArray(), errors, markerIndex)
            : new CompileOutcome(null, null, errors, markerIndex);
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
        if (insidePlayerCode)
        {
            return new CompilationError(playerLine, position.Character + 1, message, null);
        }

        var explanation = diagnostic.Id == MissingNameErrorId
            ? PlayerMessages.MissingVariable(MissingName(diagnostic))
            : null;
        return new CompilationError(null, null, message, explanation);
    }

    private static string MissingName(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static IReadOnlyList<MetadataReference> LoadReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
}

