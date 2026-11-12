using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

internal static class ForbiddenApiChecker
{
    private static readonly string[] ForbiddenNamespaces =
    [
        "System.IO",
        "System.Threading",
        "System.Diagnostics",
        "System.Net",
        "System.Reflection",
        "System.Runtime.InteropServices",
    ];

    private static readonly string[] ForbiddenTypes =
    [
        "System.Environment",
        "System.AppDomain",
        "System.GC",
        "System.Activator",
    ];

    public static CodeRejected? Check(SemanticModel model, SyntaxNode root, int playerLineOffset, int playerLineCount)
    {
        var firstPlayerLine = playerLineOffset;
        var lastPlayerLine = playerLineOffset + playerLineCount - 1;

        foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var line = name.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (line < firstPlayerLine || line > lastPlayerLine)
            {
                continue;
            }

            var symbol = model.GetSymbolInfo(name).Symbol;
            if (symbol is not null && IsForbidden(symbol))
            {
                var playerLine = line + 1 - playerLineOffset;
                return new CodeRejected(PlayerMessages.ForbiddenSpell(symbol.ToDisplayString(), playerLine), playerLine);
            }
        }

        return null;
    }

    private static bool IsForbidden(ISymbol symbol)
    {
        var ns = symbol as INamespaceSymbol ?? symbol.ContainingNamespace;
        var namespaceName = ns?.ToDisplayString() ?? string.Empty;
        if (ForbiddenNamespaces.Any(f => namespaceName == f || namespaceName.StartsWith(f + ".", StringComparison.Ordinal)))
        {
            return true;
        }

        var type = symbol as ITypeSymbol ?? symbol.ContainingType;
        return type is not null && ForbiddenTypes.Contains(type.OriginalDefinition.ToDisplayString());
    }
}
