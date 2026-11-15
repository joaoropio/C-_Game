using CodeQuest.Core;
using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

// At class scope the player may only write methods: no nested types and no other kind of member.
internal static class PlayerMemberChecker
{
    public static CodeRejected? Check(SyntaxNode root, int playerLineOffset, int playerLineCount)
    {
        foreach (var node in root.DescendantNodes())
        {
            var reason = ReasonToReject(node);
            if (reason is null)
            {
                continue;
            }

            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (line >= playerLineOffset && line < playerLineOffset + playerLineCount)
            {
                var playerLine = line + 1 - playerLineOffset;
                return new CodeRejected(reason, playerLine);
            }
        }

        return null;
    }

    private static LocalizedText? ReasonToReject(SyntaxNode node) => node switch
    {
        BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => PlayerMessages.TypesNotAllowed,
        BaseFieldDeclarationSyntax or BasePropertyDeclarationSyntax or ConstructorDeclarationSyntax
            or DestructorDeclarationSyntax => PlayerMessages.OnlyMethodsAllowed,
        _ => null,
    };
}
