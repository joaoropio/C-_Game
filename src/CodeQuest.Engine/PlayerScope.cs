using CodeQuest.Core.Levels;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

internal enum PlayerScopeKind
{
    MethodBody,
    ClassBody,
    File,
}

internal static class PlayerScope
{
    // World 1 templates put the marker inside a method body; World 2 directly inside a type, so the player
    // writes whole methods; World 3 alone at file level, so the player writes whole types.
    public static PlayerScopeKind Of(string template)
    {
        var tree = CSharpSyntaxTree.ParseText(template);
        var marker = tree.GetText().Lines.FirstOrDefault(line => PlayerCodeMarker.IsMarkerLine(line.ToString()));
        if (marker.Span.IsEmpty)
        {
            return PlayerScopeKind.MethodBody;
        }

        var position = marker.Start;
        var enclosing = tree.GetRoot()
            .DescendantNodes(node => node.Span.Contains(position))
            .LastOrDefault(node => node is BlockSyntax or BaseTypeDeclarationSyntax && node.Span.Contains(position));
        return enclosing switch
        {
            BaseTypeDeclarationSyntax => PlayerScopeKind.ClassBody,
            BlockSyntax => PlayerScopeKind.MethodBody,
            _ => PlayerScopeKind.File,
        };
    }
}
