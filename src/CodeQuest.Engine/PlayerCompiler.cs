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
    CodeRejected? Rejected,
    PlayerScopeKind Scope);

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
    private const string DuplicateDefinitionErrorId = "CS0102";
    private const string MethodGroupToNonDelegateErrorId = "CS0428";

    // Original compiler messages are always shown in the compiler's built-in English: the invariant culture has no
    // satellite resources, so neither an English player sees Portuguese nor a Portuguese one sees pt-BR.
    private static readonly CultureInfo MessageCulture = CultureInfo.InvariantCulture;

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadReferences);

    public CompileOutcome Compile(RunRequest request)
    {
        var templateLines = request.Template.ReplaceLineEndings("\n").Split('\n');
        var markerIndex = Array.FindIndex(templateLines, PlayerCodeMarker.IsMarkerLine);
        var scope = PlayerScope.Of(request.Template);
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

        var rejected = PlayerMemberChecker.Check(written.GetRoot(), markerIndex, playerLineCount, scope)
            ?? ForbiddenApiChecker.Check(writtenModel, written.GetRoot(), markerIndex, playerLineCount);
        if (rejected is not null)
        {
            return new CompileOutcome(null, null, [], markerIndex, playerLineCount, rejected, scope);
        }

        var playerMembers = PlayerMemberNames(written, markerIndex, playerLineCount);
        var writtenErrors = writtenCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => ToCompilationError(d, written, markerIndex, playerLineCount, playerMembers))
            .ToList();
        writtenErrors = DropErrorsWithoutLineWhenThePlayerHasOne(writtenErrors);
        if (writtenErrors.Count > 0)
        {
            return new CompileOutcome(null, null, writtenErrors, markerIndex, playerLineCount, null, scope);
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
            .Select(d => ToCompilationError(d, playerTree, markerIndex, playerLineCount, playerMembers))
            .ToList();

        return emitResult.Success
            ? new CompileOutcome(assembly.ToArray(), pdb.ToArray(), errors, markerIndex, playerLineCount, null, scope)
            : new CompileOutcome(null, null, errors, markerIndex, playerLineCount, null, scope);
    }

    // One mistake can surface in the player's code (with a line) and again in the hidden validation (without a
    // line, naming internals). When the player has a line to look at, the line-less errors only add noise.
    private static List<CompilationError> DropErrorsWithoutLineWhenThePlayerHasOne(List<CompilationError> errors) =>
        errors.Any(error => error.Line is not null) ? errors.Where(error => error.Line is not null).ToList() : errors;

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
        IReadOnlySet<string> playerMembers)
    {
        var message = diagnostic.GetMessage(MessageCulture);
        var position = diagnostic.Location.GetLineSpan().StartLinePosition;
        var playerLine = position.Line + 1 - lineOffset;
        var insidePlayerCode = diagnostic.Location.SourceTree == playerTree
            && playerLine >= 1
            && playerLine <= playerLineCount;
        var explanation = Explain(diagnostic, insidePlayerCode, playerMembers);
        if (insidePlayerCode)
        {
            return new CompilationError(playerLine, position.Character + 1, message, explanation);
        }

        return new CompilationError(null, null, message, explanation ?? PlayerMessages.UnreadableSpell);
    }

    // What the player declared: method names, and "Type.Member" for fields and properties (a bare name could also be
    // a library member, like string.Length).
    private static HashSet<string> PlayerMemberNames(SyntaxTree tree, int lineOffset, int playerLineCount)
    {
        bool IsPlayers(SyntaxToken identifier) => identifier.GetLocation().GetLineSpan().StartLinePosition.Line is var line
            && line >= lineOffset
            && line < lineOffset + playerLineCount;

        var nodes = tree.GetRoot().DescendantNodes().ToList();
        var methods = nodes.OfType<MethodDeclarationSyntax>()
            .Where(method => IsPlayers(method.Identifier))
            .Select(method => method.Identifier.Text);
        var properties = nodes.OfType<PropertyDeclarationSyntax>()
            .Where(property => IsPlayers(property.Identifier))
            .Select(property => QualifiedName(property, property.Identifier.Text));
        var fields = nodes.OfType<VariableDeclaratorSyntax>()
            .Where(variable => variable.Parent?.Parent is FieldDeclarationSyntax && IsPlayers(variable.Identifier))
            .Select(variable => QualifiedName(variable, variable.Identifier.Text));
        return methods.Concat(properties).Concat(fields).ToHashSet();
    }

    private static string QualifiedName(SyntaxNode member, string name) =>
        member.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() is { } type ? $"{type.Identifier.Text}.{name}" : name;

    private static LocalizedText? Explain(Diagnostic diagnostic, bool insidePlayerCode, IReadOnlySet<string> playerMembers) =>
        diagnostic.Id switch
        {
            MissingNameErrorId => ExplainMissingName(diagnostic),
            MissingReturnErrorId => PlayerMessages.MissingReturn,
            UnassignedVariableErrorId => PlayerMessages.UnassignedVariable(SpanText(diagnostic)),
            CannotConvertErrorId or CannotConvertImplicitlyErrorId => ExplainTypeMismatch(diagnostic),
            CannotConvertArgumentErrorId =>
                (insidePlayerCode ? null : ExplainMissingOverloadForType(diagnostic, playerMembers))
                    ?? ExplainTypeMismatch(diagnostic),
            MissingMemberErrorId or MissingMemberOnTypeErrorId => ExplainMissingMember(diagnostic, insidePlayerCode),
            MethodGroupToNonDelegateErrorId => ExplainCountWithoutParentheses(diagnostic),
            WrongArgumentCountErrorId => ExplainWrongArgumentCount(diagnostic, insidePlayerCode, playerMembers),
            MissingArgumentErrorId => ExplainMissingArguments(diagnostic),
            DuplicateMethodErrorId => ExplainDuplicateMethod(diagnostic),
            DuplicateDefinitionErrorId => ExplainDuplicateDefinition(diagnostic),
            InstanceMemberInStaticClassErrorId => ExplainMissingStaticAtDeclaration(diagnostic),
            ObjectReferenceRequiredErrorId => ExplainObjectReferenceRequired(diagnostic, insidePlayerCode, playerMembers),
            StaticMemberViaInstanceErrorId => ExplainStaticViaInstance(diagnostic, playerMembers),
            _ => ExplainObjectOrientedError(diagnostic, insidePlayerCode),
        };

    // An unknown name that is being called is a missing method, not a missing variable.
    private static LocalizedText ExplainMissingName(Diagnostic diagnostic) =>
        InvokedName(diagnostic) is { } method
            ? PlayerMessages.MissingMethod(method)
            : PlayerMessages.MissingVariable(SpanText(diagnostic));

    // A missing member is a missing method when the player is calling it (`x.Heal()`); when the call has a
    // receiver written by the player, the receiver's type is named. Other members only get the Length/Count hint.
    private static LocalizedText? ExplainMissingMember(Diagnostic diagnostic, bool insidePlayerCode)
    {
        if (NameAt(diagnostic) is not { } name)
        {
            return null;
        }

        var type = QuotedNames(diagnostic) is [var first, ..] ? first : null;
        if (!IsInvoked(name))
        {
            return LengthOrCountHint(name.Identifier.Text, type);
        }

        return HasReceiver(name) && insidePlayerCode && type is not null
            ? PlayerMessages.MissingMethodOnType(type, name.Identifier.Text)
            : PlayerMessages.MissingMethod(name.Identifier.Text);
    }

    // `items.Count` on an array or text finds only the LINQ method `Count()`, which is reported as a method group.
    private static LocalizedText? ExplainCountWithoutParentheses(Diagnostic diagnostic) =>
        NameAt(diagnostic) is { Identifier.Text: "Count" } name && HasReceiver(name) ? PlayerMessages.LengthNotCount : null;

    private static LocalizedText? LengthOrCountHint(string member, string? type) =>
        member == "Length" && type is not null
            && (type.StartsWith("List<", StringComparison.Ordinal) || type.StartsWith("Dictionary<", StringComparison.Ordinal))
            ? PlayerMessages.CountNotLength
            : null;

    private static SimpleNameSyntax? NameAt(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetRoot().FindNode(diagnostic.Location.SourceSpan) as SimpleNameSyntax;

    private static bool HasReceiver(SimpleNameSyntax name) =>
        name.Parent is MemberAccessExpressionSyntax access && access.Name == name;

    private static bool IsInvoked(SimpleNameSyntax name)
    {
        SyntaxNode target = HasReceiver(name) ? name.Parent! : name;
        return target.Parent is InvocationExpressionSyntax invocation && invocation.Expression == target;
    }

    // The name at the diagnostic when it is the target of an invocation, without type arguments.
    private static string? InvokedName(Diagnostic diagnostic) =>
        NameAt(diagnostic) is { } name && IsInvoked(name) ? name.Identifier.Text : null;

    // A call from the hidden validation that the player's methods can't answer: the player has the method, but
    // not the version being called.
    private static LocalizedText? ExplainMissingOverloadForType(Diagnostic diagnostic, IReadOnlySet<string> playerMembers)
    {
        var invocation = diagnostic.Location.SourceTree!.GetRoot()
            .FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();
        var method = invocation?.Expression switch
        {
            SimpleNameSyntax name => name.Identifier.Text,
            MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
            _ => null,
        };
        return method is not null && playerMembers.Contains(method) && QuotedNames(diagnostic) is [var type, ..]
            ? PlayerMessages.MissingOverloadForType(method, type)
            : null;
    }

    private static LocalizedText? ExplainWrongArgumentCount(
        Diagnostic diagnostic,
        bool insidePlayerCode,
        IReadOnlySet<string> playerMembers)
    {
        var match = ArgumentCount().Match(diagnostic.GetMessage(MessageCulture));
        if (QuotedNames(diagnostic) is not [var method, ..] || !match.Success)
        {
            return null;
        }

        var count = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return !insidePlayerCode && playerMembers.Contains(method)
            ? PlayerMessages.MissingOverloadForCount(method, count)
            : PlayerMessages.WrongArgumentCount(method, count);
    }

    // "...required parameter 'b' of 'Spell.Heal<T>(T, T)'": the method is the name before the parenthesis.
    // A constructor is "Golem.Golem(int, int)": its name repeats the type's.
    private static LocalizedText? ExplainMissingArguments(Diagnostic diagnostic)
    {
        if (QuotedNames(diagnostic) is not [var parameter, var signature, ..])
        {
            return null;
        }

        var path = SymbolPath(signature);
        var name = path[^1];
        if (name.Length == 0)
        {
            return null;
        }

        if (path is not [.., var type, _] || type != name)
        {
            return PlayerMessages.MissingArguments(name, parameter);
        }

        // Outside an object creation the call is the implicit or explicit `base(...)` of a derived class.
        return EnclosingTypeName(diagnostic) is { } derived && !IsObjectCreation(diagnostic)
            ? PlayerMessages.MissingBaseConstructorArgument(parameter, type, derived)
            : PlayerMessages.MissingConstructorArguments(type, parameter);
    }

    private static bool IsObjectCreation(Diagnostic diagnostic) => diagnostic.Location.SourceTree!.GetRoot()
        .FindNode(diagnostic.Location.SourceSpan)
        .AncestorsAndSelf()
        .Any(node => node is BaseObjectCreationExpressionSyntax);

    private static string? EnclosingTypeName(Diagnostic diagnostic) => diagnostic.Location.SourceTree!.GetRoot()
        .FindNode(diagnostic.Location.SourceSpan)
        .AncestorsAndSelf()
        .OfType<TypeDeclarationSyntax>()
        .FirstOrDefault()?.Identifier.Text;

    // "Spell.Heal<T>(T, T)" or "Dictionary<string, int>.ContainsKey(string)" -> "Heal" / "ContainsKey".
    private static string MethodNameOf(string signature) => SymbolPath(signature)[^1];

    // "Type 'Spell' already defines a member called 'Heal' with the same parameter types".
    private static LocalizedText? ExplainDuplicateMethod(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [_, var method, ..] ? PlayerMessages.DuplicateMethod(method) : null;

    // "The type 'Spell' already contains a definition for 'Validation'": the hidden check lives in a nested type.
    private static LocalizedText? ExplainDuplicateDefinition(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [_, RoslynCodeRunner.ValidationTypeName]
            ? PlayerMessages.ReservedName(RoslynCodeRunner.ValidationTypeName)
            : null;

    // The span is the declared method name.
    private static LocalizedText? ExplainMissingStaticAtDeclaration(Diagnostic diagnostic)
    {
        var name = SpanText(diagnostic);
        return IsIdentifier(name) ? PlayerMessages.MissingStatic(name) : null;
    }

    // "An object reference is required for the non-static field, method, or property 'Spell.Heal(int)'": only a
    // method written by the player is "missing static"; `string.Length` or `List<int>.Count` are not the player's.
    // A field or property of the player's ("Hero.hp") is an instance member used without an object.
    private static LocalizedText? ExplainObjectReferenceRequired(
        Diagnostic diagnostic,
        bool insidePlayerCode,
        IReadOnlySet<string> playerMembers)
    {
        if (QuotedNames(diagnostic) is not [var symbol, ..])
        {
            return null;
        }

        var path = SymbolPath(symbol);
        if (symbol.Contains('('))
        {
            return playerMembers.Contains(path[^1]) ? PlayerMessages.MissingStatic(path[^1]) : null;
        }

        if (path is not [.., var type, var member] || !playerMembers.Contains($"{type}.{member}"))
        {
            return null;
        }

        return insidePlayerCode
            ? PlayerMessages.InstanceMemberInStaticMember(member)
            : PlayerMessages.MissingStaticMember(member);
    }

    private static List<string> QuotedNames(Diagnostic diagnostic) => QuotedName()
        .Matches(diagnostic.GetMessage(MessageCulture))
        .Select(match => match.Groups[1].Value)
        .ToList();

    private static LocalizedText? ExplainTypeMismatch(Diagnostic diagnostic)
    {
        // Diagnostic arguments are not public API, so the two types are read from the
        // quoted names of the English message, which is stable across compiler cultures.
        var types = QuotedName().Matches(diagnostic.GetMessage(MessageCulture));
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

