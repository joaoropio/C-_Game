using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeQuest.Engine;

internal static class PlayerCodePrecheck
{
    public static CodeRejected? Check(string playerCode)
    {
        var tokens = SyntaxFactory.ParseTokens(playerCode)
            .Where(t => !t.IsKind(SyntaxKind.EndOfFileToken))
            .ToList();
        if (tokens.Count == 0)
        {
            return new CodeRejected(PlayerMessages.NoSpellWritten, null);
        }

        var lines = SourceText.From(playerCode).Lines;
        var openBraceLines = new Stack<int>();
        foreach (var token in tokens)
        {
            var line = lines.GetLineFromPosition(token.SpanStart).LineNumber + 1;
            if (token.IsKind(SyntaxKind.OpenBraceToken))
            {
                openBraceLines.Push(line);
            }
            else if (token.IsKind(SyntaxKind.CloseBraceToken) && !openBraceLines.TryPop(out _))
            {
                return new CodeRejected(PlayerMessages.ExtraClosingBrace(line), line);
            }
        }

        return openBraceLines.Count > 0
            ? new CodeRejected(PlayerMessages.UnclosedBrace(openBraceLines.Peek()), openBraceLines.Peek())
            : null;
    }
}
