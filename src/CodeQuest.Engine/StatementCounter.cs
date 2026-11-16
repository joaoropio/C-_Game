using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

internal static class StatementCounter
{
    // Outside a method body the player code is parsed where it was written (inside a class, or at file level),
    // so only the statements within member bodies count; an expression body counts as the one statement it stands for.
    public static int Count(string playerCode, PlayerScopeKind scope = PlayerScopeKind.MethodBody)
    {
        var root = CSharpSyntaxTree.ParseText(scope switch
        {
            PlayerScopeKind.ClassBody => "class C {" + playerCode + "}",
            PlayerScopeKind.File => playerCode,
            _ => "{" + playerCode + "}",
        }).GetRoot();
        var nodes = root.DescendantNodes().ToList();
        return nodes.OfType<StatementSyntax>().Count(statement => statement is not BlockSyntax)
            + (scope == PlayerScopeKind.MethodBody
                ? 0
                : nodes.OfType<ArrowExpressionClauseSyntax>().Count(arrow =>
                    arrow.Parent is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or BasePropertyDeclarationSyntax));
    }
}
