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
            private static bool _exceeded;

            public static void Tick()
            {
                if (_exceeded || ++_ticks > Limit)
                {
                    _exceeded = true;
                    throw new System.TimeoutException();
                }
            }
        }
        """;

    private static readonly ExpressionStatementSyntax TickStatement = (ExpressionStatementSyntax)SyntaxFactory
        .ParseStatement("global::__LoopGuard.Tick();")
        .WithTrailingTrivia(SyntaxFactory.Space);

    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node) =>
        node.WithStatement(Guard(node, (StatementSyntax)Visit(node.Statement)));

    public override SyntaxNode? VisitGotoStatement(GotoStatementSyntax node) =>
        guardedSpan.Contains(node.SpanStart) ? WithTick(node) : node;

    private StatementSyntax Guard(StatementSyntax loop, StatementSyntax rewrittenBody)
    {
        if (!guardedSpan.Contains(loop.SpanStart))
        {
            return rewrittenBody;
        }

        return rewrittenBody is BlockSyntax block
            ? block.WithStatements(block.Statements.Insert(0, TickStatement))
            : WithTick(rewrittenBody);
    }

    private static BlockSyntax WithTick(StatementSyntax statement) =>
        SyntaxFactory.Block(TickStatement, statement)
            .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.Space))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(SyntaxFactory.Space));
}
