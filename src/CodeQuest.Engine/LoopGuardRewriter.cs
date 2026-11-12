using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeQuest.Engine;

internal sealed class LoopGuardRewriter(TextSpan guardedSpan) : CSharpSyntaxRewriter
{
    public const string GuardSource = """
        internal static class __LoopGuard
        {
            public const int Limit = 1_000_000;

            private static int _ticks;

            public static void Tick()
            {
                if (++_ticks > Limit)
                {
                    throw new System.TimeoutException();
                }
            }
        }
        """;

    private static readonly ExpressionStatementSyntax TickStatement = (ExpressionStatementSyntax)SyntaxFactory
        .ParseStatement("__LoopGuard.Tick();")
        .WithTrailingTrivia(SyntaxFactory.Space);

    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    private StatementSyntax Guard(StatementSyntax loop, StatementSyntax rewrittenBody)
    {
        if (!guardedSpan.Contains(loop.SpanStart))
        {
            return rewrittenBody;
        }

        return rewrittenBody is BlockSyntax block
            ? block.WithStatements(block.Statements.Insert(0, TickStatement))
            : SyntaxFactory.Block(TickStatement, rewrittenBody)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.Space))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(SyntaxFactory.Space));
    }
}
