using System.Globalization;
using System.Text;
using CodeQuest.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

// Explanations for the mistakes of the object-oriented world. Names come from the quoted parts of the English message.
internal sealed partial class PlayerCompiler
{
    private const string MissingTypeErrorId = "CS0246";
    private const string NoConstructorForArgumentCountErrorId = "CS1729";
    private const string InaccessibleMemberErrorId = "CS0122";
    private const string ReadOnlyMemberErrorId = "CS0200";
    private const string InaccessibleSetterErrorId = "CS0272";
    private const string NothingToOverrideErrorId = "CS0115";
    private const string NotOverridableErrorId = "CS0506";
    private const string VirtualMemberNotPublicErrorId = "CS0621";
    private const string OverrideChangesAccessErrorId = "CS0507";
    private const string MissingAbstractImplementationErrorId = "CS0534";
    private const string MissingInterfaceImplementationErrorId = "CS0535";
    private const string AbstractInstanceErrorId = "CS0144";
    private const string StaticMemberViaInstanceErrorId = "CS0176";
    private const string MemberNamedLikeItsTypeErrorId = "CS0542";

    private static LocalizedText? ExplainObjectOrientedError(Diagnostic diagnostic, bool insidePlayerCode) =>
        diagnostic.Id switch
        {
            MissingTypeErrorId => ExplainMissingType(diagnostic, insidePlayerCode),
            NoConstructorForArgumentCountErrorId => ExplainNoConstructorForArgumentCount(diagnostic),
            InaccessibleMemberErrorId => ExplainInaccessibleMember(diagnostic),
            ReadOnlyMemberErrorId or InaccessibleSetterErrorId => ExplainAssignment(diagnostic),
            MemberNamedLikeItsTypeErrorId => ExplainConstructorWithReturnType(diagnostic),
            NothingToOverrideErrorId => ExplainNothingToOverride(diagnostic),
            NotOverridableErrorId => ExplainNotOverridable(diagnostic),
            VirtualMemberNotPublicErrorId => ExplainVirtualNotPublic(diagnostic),
            OverrideChangesAccessErrorId => ExplainOverrideChangesAccess(diagnostic),
            MissingAbstractImplementationErrorId or MissingInterfaceImplementationErrorId =>
                ExplainMissingImplementation(diagnostic),
            AbstractInstanceErrorId => ExplainAbstractInstance(diagnostic),
            _ => null,
        };

    // The hidden validation names a class the player has to write. In the player's own code the name may just be a
    // misspelt library type (`Lsit<int>`), so there the message only asks to check the name.
    private static LocalizedText? ExplainMissingType(Diagnostic diagnostic, bool insidePlayerCode)
    {
        if (QuotedNames(diagnostic) is not [var written, ..] || IsInUsingDirective(diagnostic))
        {
            return null;
        }

        var name = WithoutTypeArguments(written);
        return insidePlayerCode ? PlayerMessages.UnknownType(name) : PlayerMessages.MissingClass(name);
    }

    private static bool IsInUsingDirective(Diagnostic diagnostic) => diagnostic.Location.SourceTree!.GetRoot()
        .FindNode(diagnostic.Location.SourceSpan)
        .AncestorsAndSelf()
        .Any(node => node is UsingDirectiveSyntax);

    // "'Golem' does not contain a constructor that takes 2 arguments".
    private static LocalizedText? ExplainNoConstructorForArgumentCount(Diagnostic diagnostic)
    {
        if (QuotedNames(diagnostic) is ["object", ..] && BaseCallOwner(diagnostic) is { } derived)
        {
            return PlayerMessages.BaseCallWithoutBaseClass(derived);
        }

        var match = ArgumentCount().Match(diagnostic.GetMessage(MessageCulture));
        return QuotedNames(diagnostic) is [var type, ..] && match.Success
            ? PlayerMessages.NoConstructorForCount(
                WithoutTypeArguments(type),
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            : null;
    }

    // A `: base(...)` in a class that has no base class calls the constructor of object.
    private static string? BaseCallOwner(Diagnostic diagnostic)
    {
        var node = diagnostic.Location.SourceTree!.GetRoot().FindNode(diagnostic.Location.SourceSpan);
        return node.AncestorsAndSelf().Any(ancestor => ancestor is ConstructorInitializerSyntax)
            ? EnclosingTypeName(diagnostic)
            : null;
    }

    // "'Golem.Armor' is inaccessible due to its protection level"; for a constructor the member is named like its
    // type ("'Golem.Golem(string, int)'").
    private static LocalizedText? ExplainInaccessibleMember(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, ..] && SymbolPath(symbol) is [.., var owner, var member]
            ? (owner == member ? PlayerMessages.ConstructorNotPublic(owner) : PlayerMessages.NotPublic(member, owner))
            : null;

    // "'Golem': member names cannot be the same as their enclosing type": `public void Golem(...)` in class Golem.
    private static LocalizedText? ExplainConstructorWithReturnType(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var name, ..] ? PlayerMessages.ConstructorWithReturnType(name) : null;

    // "Property or indexer 'Golem.Armor' cannot be assigned to -- it is read only" (the set may also be missing from
    // inside the class) and, for a private set, "...because the set accessor is inaccessible". An indexer such as
    // `text[0] = 'c'` is a Worlds 1-2 mistake, so it gets no explanation here.
    private static LocalizedText? ExplainAssignment(Diagnostic diagnostic)
    {
        if (QuotedNames(diagnostic) is not [var symbol, ..] || MethodNameOf(symbol).StartsWith("this[", StringComparison.Ordinal))
        {
            return null;
        }

        var member = MethodNameOf(symbol);
        return diagnostic.Id == ReadOnlyMemberErrorId
            ? PlayerMessages.CannotAssignHere(member)
            : PlayerMessages.CannotAssignFromOutside(member);
    }

    // "'Griffin.Speak()': no suitable method found to override".
    private static LocalizedText? ExplainNothingToOverride(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, ..] ? PlayerMessages.NothingToOverride(MethodNameOf(symbol)) : null;

    // "'Griffin.Speak()' cannot override inherited member 'Beast.Speak()' because it is not marked virtual...".
    private static LocalizedText? ExplainNotOverridable(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, ..] ? PlayerMessages.NotOverridable(MethodNameOf(symbol)) : null;

    // "'Beast.Speak()': virtual or abstract members cannot be private": the member has no access modifier (or says private).
    private static LocalizedText? ExplainVirtualNotPublic(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, ..] ? PlayerMessages.VirtualNotPublic(MethodNameOf(symbol)) : null;

    // "'Griffin.Speak()': cannot change access modifiers when overriding 'public' inherited member 'Beast.Speak()'".
    private static LocalizedText? ExplainOverrideChangesAccess(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, var access, ..] ? PlayerMessages.OverrideChangesAccess(MethodNameOf(symbol), access) : null;

    // A virtual or override method without `public` is reported on its own (CS0621), and then again by the
    // overrides that cannot see it (CS0115) or by the access mismatch (CS0507). Once the missing `public` is
    // explained, those follow-ups for the same method name only mislead, e.g. "check that the class inherits".
    private static List<Diagnostic> DropFollowUpsOfMissingPublic(List<Diagnostic> diagnostics)
    {
        var notPublic = diagnostics
            .Where(d => d.Id == VirtualMemberNotPublicErrorId && QuotedNames(d) is [_, ..])
            .Select(d => MethodNameOf(QuotedNames(d)[0]))
            .ToHashSet();
        return notPublic.Count == 0
            ? diagnostics
            : diagnostics
                .Where(d => !(d.Id is NothingToOverrideErrorId or OverrideChangesAccessErrorId
                    && QuotedNames(d) is [var symbol, ..]
                    && notPublic.Contains(MethodNameOf(symbol))))
                .ToList();
    }

    // "'Griffin' does not implement inherited abstract member 'Beast.Sound()'"; for a property the member is
    // "Beast.Sound.get".
    private static LocalizedText? ExplainMissingImplementation(Diagnostic diagnostic)
    {
        if (QuotedNames(diagnostic) is not [var type, var member, ..])
        {
            return null;
        }

        var path = SymbolPath(member);
        if (path is [.., "get" or "set" or "init"])
        {
            path = path[..^1];
        }

        return path is [.., var required, var name]
            ? PlayerMessages.MissingImplementation(SymbolPath(type)[^1], name, required)
            : null;
    }

    private static LocalizedText? ExplainAbstractInstance(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var type, ..] ? PlayerMessages.AbstractInstance(SymbolPath(type)[^1]) : null;

    // "Member 'Slime.Hit(int)' cannot be accessed with an instance reference; qualify it with a type name instead".
    // Only the player's own members: `text.Format(...)` is a library mistake with its own advice.
    private static LocalizedText? ExplainStaticViaInstance(Diagnostic diagnostic, IReadOnlySet<string> playerMembers)
    {
        if (QuotedNames(diagnostic) is not [var symbol, ..])
        {
            return null;
        }

        var path = SymbolPath(symbol);
        var isPlayers = symbol.Contains('(')
            ? playerMembers.Contains(path[^1])
            : path is [.., var type, var name] && playerMembers.Contains($"{type}.{name}");
        return isPlayers ? PlayerMessages.StaticMemberViaInstance(path[^1]) : null;
    }

    // "Dictionary<string, int>.ContainsKey(string)" -> ["Dictionary", "ContainsKey"].
    private static string[] SymbolPath(string symbol) => WithoutTypeArguments(symbol).Split('(')[0].Split('.');

    // "Box<List<int>>.Get" -> "Box.Get".
    private static string WithoutTypeArguments(string text)
    {
        var result = new StringBuilder();
        var depth = 0;
        foreach (var character in text)
        {
            depth += character switch { '<' => 1, '>' => -1, _ => 0 };
            if (depth == 0 && character != '>')
            {
                result.Append(character);
            }
        }

        return result.ToString();
    }
}
