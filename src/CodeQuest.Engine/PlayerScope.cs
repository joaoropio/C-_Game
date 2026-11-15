using CodeQuest.Core.Levels;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

internal static class PlayerScope
{
    // World 1 templates put the marker inside a method body; from World 2 it sits directly inside a type,
    // so the player writes whole methods instead of statements.
    public static bool IsClassScope(string template)
    {
        var tree = CSharpSyntaxTree.ParseText(template);
        var marker = tree.GetText().Lines.FirstOrDefault(line => PlayerCodeMarker.IsMarkerLine(line.ToString()));
        if (marker.Span.IsEmpty)
        {
            return false;
        }

        var position = marker.Start;
        var enclosing = tree.GetRoot()
            .DescendantNodes(node => node.Span.Contains(position))
            .LastOrDefault(node => node is BlockSyntax or BaseTypeDeclarationSyntax && node.Span.Contains(position));
        return enclosing is BaseTypeDeclarationSyntax;
    }
}
