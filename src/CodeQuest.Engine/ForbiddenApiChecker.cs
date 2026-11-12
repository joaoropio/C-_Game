using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

internal static class ForbiddenApiChecker
{
    private static readonly string[] AllowedNamespaces =
    [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
    ];

    private static readonly string[] ForbiddenTypes =
    [
        "System.Environment",
        "System.AppDomain",
        "System.GC",
        "System.Activator",
        "System.Type",
        "System.Delegate",
        "System.TimeProvider",
        "System.Linq.ParallelEnumerable",
    ];

    private static readonly string[] ForbiddenConsoleMembers =
    [
        "ReadLine",
        "ReadKey",
        "Read",
        "In",
        "OpenStandardInput",
        "SetIn",
        "SetOut",
        "SetError",
    ];

    public static CodeRejected? Check(SemanticModel model, SyntaxNode root, int playerLineOffset, int playerLineCount)
    {
        var firstPlayerLine = playerLineOffset;
        var lastPlayerLine = playerLineOffset + playerLineCount - 1;

        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var line = name.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (line < firstPlayerLine || line > lastPlayerLine)
            {
                continue;
            }

            var symbol = model.GetSymbolInfo(name).Symbol;
            if (symbol is not null && !IsAllowed(symbol))
            {
                var playerLine = line + 1 - playerLineOffset;
                return new CodeRejected(PlayerMessages.ForbiddenSpell(symbol.ToDisplayString(), playerLine), playerLine);
            }
        }

        return null;
    }

    private static bool IsAllowed(ISymbol symbol)
    {
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Dynamic })
        {
            return false;
        }

        if (symbol.Locations.Any(location => location.IsInSource))
        {
            return true;
        }

        if (symbol is INamespaceSymbol ns)
        {
            return IsAllowedNamespace(ns, allowPrefixes: true);
        }

        return IsAllowedNamespace(symbol.ContainingNamespace, allowPrefixes: false) && !IsForbiddenMember(symbol);
    }

    private static bool IsAllowedNamespace(INamespaceSymbol? ns, bool allowPrefixes)
    {
        if (ns is null || ns.IsGlobalNamespace)
        {
            return true;
        }

        var name = ns.ToDisplayString();
        return AllowedNamespaces.Any(allowed => allowed == name
            || (allowPrefixes && allowed.StartsWith(name + ".", StringComparison.Ordinal)));
    }

    private static bool IsForbiddenMember(ISymbol symbol)
    {
        var type = symbol as ITypeSymbol ?? symbol.ContainingType;
        while (type is not null)
        {
            var typeName = type.OriginalDefinition.ToDisplayString();
            if (ForbiddenTypes.Contains(typeName))
            {
                return true;
            }

            if (typeName == "System.Console" && symbol is not ITypeSymbol && ForbiddenConsoleMembers.Contains(symbol.Name))
            {
                return true;
            }

            type = type.ContainingType;
        }

        return false;
    }
}
