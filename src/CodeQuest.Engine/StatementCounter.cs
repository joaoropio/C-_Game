using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

internal static class StatementCounter
{
    public static int Count(string playerCode) =>
        CSharpSyntaxTree.ParseText("{" + playerCode + "}")
            .GetRoot()
            .DescendantNodes()
            .OfType<StatementSyntax>()
            .Count(s => s is not BlockSyntax);
}
