using CodeQuest.Core;
using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

// At class scope the player may only write methods: no nested types and no other kind of member.
// An async void method throws on the thread pool and would kill the process, and extern methods have no
// guarded body, so neither modifier is allowed anywhere in the player code.
internal static class PlayerMemberChecker
{
    public static CodeRejected? Check(SyntaxNode root, int playerLineOffset, int playerLineCount)
    {
        foreach (var node in root.DescendantNodes())
        {
            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (line < playerLineOffset || line >= playerLineOffset + playerLineCount)
            {
                continue;
            }

            var playerLine = line + 1 - playerLineOffset;
            if (ReasonToReject(node, playerLine) is { } reason)
            {
                return new CodeRejected(reason, playerLine);
            }
        }

        return null;
    }

    private static LocalizedText? ReasonToReject(SyntaxNode node, int playerLine) => node switch
    {
        BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => PlayerMessages.TypesNotAllowed,
        BaseFieldDeclarationSyntax or BasePropertyDeclarationSyntax or ConstructorDeclarationSyntax
            or DestructorDeclarationSyntax => PlayerMessages.OnlyMethodsAllowed,
        _ when IsAsync(node) => PlayerMessages.AsyncNotAllowed(playerLine),
        _ when IsExtern(node) => PlayerMessages.ExternNotAllowed(playerLine),
        _ => null,
    };

    private static bool IsAsync(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.AsyncKeyword),
        LocalFunctionStatementSyntax function => function.Modifiers.Any(SyntaxKind.AsyncKeyword),
        AnonymousFunctionExpressionSyntax function => function.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
        _ => false,
    };

    private static bool IsExtern(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.ExternKeyword),
        LocalFunctionStatementSyntax function => function.Modifiers.Any(SyntaxKind.ExternKeyword),
        _ => false,
    };
}
