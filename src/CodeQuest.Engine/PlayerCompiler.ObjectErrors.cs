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
    private const string MissingAbstractImplementationErrorId = "CS0534";
    private const string MissingInterfaceImplementationErrorId = "CS0535";
    private const string AbstractInstanceErrorId = "CS0144";

    private static LocalizedText? ExplainObjectOrientedError(Diagnostic diagnostic, bool insidePlayerCode) =>
        diagnostic.Id switch
        {
            MissingTypeErrorId => ExplainMissingType(diagnostic, insidePlayerCode),
            NoConstructorForArgumentCountErrorId => ExplainNoConstructorForArgumentCount(diagnostic),
            InaccessibleMemberErrorId => ExplainInaccessibleMember(diagnostic),
            ReadOnlyMemberErrorId or InaccessibleSetterErrorId => ExplainAssignmentFromOutside(diagnostic),
            NothingToOverrideErrorId => ExplainNothingToOverride(diagnostic),
            NotOverridableErrorId => ExplainNotOverridable(diagnostic),
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
        var match = ArgumentCount().Match(diagnostic.GetMessage(MessageCulture));
        return QuotedNames(diagnostic) is [var type, ..] && match.Success
            ? PlayerMessages.NoConstructorForCount(
                WithoutTypeArguments(type),
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            : null;
    }

    // "'Golem.Armor' is inaccessible due to its protection level". A private constructor is left alone: "Golem is
    // private inside Golem" would only confuse.
    private static LocalizedText? ExplainInaccessibleMember(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, ..] && SymbolPath(symbol) is [.., var owner, var member] && owner != member
            ? PlayerMessages.PrivateMember(member, owner)
            : null;

    // "Property or indexer 'Golem.Armor' cannot be assigned to -- it is read only" and the private-set twin.
    private static LocalizedText? ExplainAssignmentFromOutside(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, ..] ? PlayerMessages.CannotAssignFromOutside(MethodNameOf(symbol)) : null;

    // "'Griffin.Speak()': no suitable method found to override".
    private static LocalizedText? ExplainNothingToOverride(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, ..] ? PlayerMessages.NothingToOverride(MethodNameOf(symbol)) : null;

    // "'Griffin.Speak()' cannot override inherited member 'Beast.Speak()' because it is not marked virtual...".
    private static LocalizedText? ExplainNotOverridable(Diagnostic diagnostic) =>
        QuotedNames(diagnostic) is [var symbol, ..] ? PlayerMessages.NotOverridable(MethodNameOf(symbol)) : null;

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

    // "Dictionary<string, int>.ContainsKey(string)" -> ["Dictionary", "ContainsKey"].
    private static string[] SymbolPath(string symbol) => WithoutTypeArguments(symbol.Split('(')[0]).Split('.');

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
