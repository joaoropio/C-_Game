using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

internal static class StatementCounter
{
    // At class scope the player code is a list of methods, so it is parsed inside a class and only the
    // statements within the bodies count; an expression body counts as the one statement it stands for.
    public static int Count(string playerCode, bool atClassScope = false)
    {
        var root = CSharpSyntaxTree.ParseText(atClassScope ? "class C {" + playerCode + "}" : "{" + playerCode + "}").GetRoot();
        var nodes = root.DescendantNodes().ToList();
        return nodes.OfType<StatementSyntax>().Count(statement => statement is not BlockSyntax)
            + (atClassScope ? nodes.OfType<ArrowExpressionClauseSyntax>().Count(arrow => arrow.Parent is BaseMethodDeclarationSyntax) : 0);
    }
}
