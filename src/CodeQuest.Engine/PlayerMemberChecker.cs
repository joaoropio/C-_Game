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
    private const string InvalidTokenInMemberErrorId = "CS1519";

    public static CodeRejected? Check(SyntaxNode root, int playerLineOffset, int playerLineCount)
    {
        if (FirstStatementOutsideAMethod(root, playerLineOffset, playerLineCount) is { } statementLine)
        {
            return new CodeRejected(PlayerMessages.StatementsBelongInMethods, statementLine);
        }

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

    // A statement at class scope (`x++;`, `return 1;`, `if (...)`) is a syntax error inside a type, not inside a block.
    private static int? FirstStatementOutsideAMethod(SyntaxNode root, int playerLineOffset, int playerLineCount) => root
        .SyntaxTree.GetDiagnostics()
        .Where(diagnostic => diagnostic.Id == InvalidTokenInMemberErrorId)
        .Where(diagnostic => IsAtClassScope(root, diagnostic))
        .Select(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1 - playerLineOffset)
        .Where(playerLine => playerLine >= 1 && playerLine <= playerLineCount)
        .Select(playerLine => (int?)playerLine)
        .Min();

    private static bool IsAtClassScope(SyntaxNode root, Diagnostic diagnostic) => root
        .FindNode(diagnostic.Location.SourceSpan)
        .AncestorsAndSelf()
        .FirstOrDefault(node => node is BlockSyntax or BaseTypeDeclarationSyntax) is BaseTypeDeclarationSyntax;

    private static LocalizedText? ReasonToReject(SyntaxNode node, int playerLine) => node switch
    {
        BaseNamespaceDeclarationSyntax => PlayerMessages.NamespaceNotAllowed,
        ConstructorDeclarationSyntax { Body: null, ExpressionBody: null } => PlayerMessages.StatementsBelongInMethods,
        ConstructorDeclarationSyntax constructor when IsNotNamedAfterItsClass(constructor)
            => PlayerMessages.MissingReturnType(constructor.Identifier.Text),
        BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => PlayerMessages.TypesNotAllowed,
        BaseFieldDeclarationSyntax or BasePropertyDeclarationSyntax or ConstructorDeclarationSyntax
            or DestructorDeclarationSyntax => PlayerMessages.OnlyMethodsAllowed,
        _ when IsAsync(node) => PlayerMessages.AsyncNotAllowed(playerLine),
        _ when IsExtern(node) => PlayerMessages.ExternNotAllowed(playerLine),
        _ => null,
    };

    // `static Heal(int a) { ... }` has no return type, so Roslyn reads it as a constructor named Heal (CS1520).
    private static bool IsNotNamedAfterItsClass(ConstructorDeclarationSyntax constructor) =>
        constructor.Parent is BaseTypeDeclarationSyntax type && type.Identifier.Text != constructor.Identifier.Text;

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
