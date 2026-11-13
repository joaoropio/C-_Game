using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeQuest.Engine;

internal sealed class LoopGuardRewriter(TextSpan guardedSpan, SemanticModel model) : CSharpSyntaxRewriter
{
    public const string GuardSource = """
        internal static class __LoopGuard
        {
            public const int Limit = 1_000_000;
            public const int CallLimit = 10_000_000;

            private static int _ticks;
            private static int _calls;

            // Once either limit or the stack is exhausted this stays true; the rewriter makes
            // the player's catch and finally blocks inert from then on, so nothing swallows the failure.
            public static bool Tripped { get; private set; }

            public static void Tick()
            {
                if (Tripped || ++_ticks > Limit)
                {
                    Trip();
                    throw new System.TimeoutException();
                }
            }

            public static void Enter()
            {
                if (Tripped || ++_calls > CallLimit)
                {
                    Trip();
                    throw new System.TimeoutException();
                }

                if (!global::System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
                {
                    Trip();
                    throw new System.InsufficientExecutionStackException();
                }
            }

            private static void Trip() => Tripped = true;
        }
        """;

    private static readonly ExpressionStatementSyntax TickStatement = (ExpressionStatementSyntax)SyntaxFactory
        .ParseStatement("global::__LoopGuard.Tick();")
        .WithTrailingTrivia(SyntaxFactory.Space);

    private static readonly ExpressionStatementSyntax EnterStatement = (ExpressionStatementSyntax)SyntaxFactory
        .ParseStatement("global::__LoopGuard.Enter();");

    // The block holding the player code is the body of the template method, so an Enter
    // placed before the first player statement guards recursion of the enclosing method.
    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        var visited = (BlockSyntax)base.VisitBlock(node)!;
        if (guardedSpan.Contains(node.OpenBraceToken.SpanStart))
        {
            return visited;
        }

        var first = node.Statements.IndexOf(statement => guardedSpan.Contains(statement.SpanStart));
        return first < 0 ? visited : WithEnterBefore(visited, first);
    }

    // Player handlers run on top of the stack that already failed, so once the guard has tripped
    // they must neither catch the exception nor run: a catch gets a filter, a finally body a condition.
    public override SyntaxNode? VisitCatchClause(CatchClauseSyntax node)
    {
        var visited = (CatchClauseSyntax)base.VisitCatchClause(node)!;
        if (!guardedSpan.Contains(node.SpanStart))
        {
            return visited;
        }

        var notTripped = SyntaxFactory.ParseExpression("!global::__LoopGuard.Tripped");
        var previous = visited.Declaration?.CloseParenToken ?? visited.CatchKeyword;
        var separator = previous.TrailingTrivia.Count == 0 ? SyntaxFactory.TriviaList(SyntaxFactory.Space) : default;
        var filter = visited.Filter is { } existing
            ? existing.WithFilterExpression(SyntaxFactory.BinaryExpression(
                SyntaxKind.LogicalAndExpression,
                notTripped.WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.Token(SyntaxKind.AmpersandAmpersandToken).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.ParenthesizedExpression(existing.FilterExpression)))
            : SyntaxFactory.CatchFilterClause(
                SyntaxFactory.Token(SyntaxKind.WhenKeyword).WithLeadingTrivia(separator).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                notTripped,
                SyntaxFactory.Token(SyntaxKind.CloseParenToken).WithTrailingTrivia(SyntaxFactory.Space));
        return visited.WithFilter(filter);
    }

    public override SyntaxNode? VisitFinallyClause(FinallyClauseSyntax node)
    {
        var visited = (FinallyClauseSyntax)base.VisitFinallyClause(node)!;
        if (!guardedSpan.Contains(node.SpanStart))
        {
            return visited;
        }

        // The skipped body must not hide assignments from the compiler's definite-assignment analysis,
        // so the else branch declares every variable the body always assigns as assigned.
        var assigned = model.AnalyzeDataFlow(node.Block);
        var skipInits = assigned is { Succeeded: true }
            ? assigned.AlwaysAssigned
                .Except(assigned.VariablesDeclared, SymbolEqualityComparer.Default)
                .Except(assigned.DefinitelyAssignedOnEntry, SymbolEqualityComparer.Default)
                .Where(symbol => symbol is ILocalSymbol or IParameterSymbol)
                .Select(symbol => SyntaxFactory.ParseStatement(
                    $"global::System.Runtime.CompilerServices.Unsafe.SkipInit(out {Escaped(symbol.Name)}); ").WithLeadingTrivia(SyntaxFactory.Space))
                .ToList()
            : [];
        var skipped = skipInits.Count == 0
            ? null
            : SyntaxFactory.ElseClause(
                SyntaxFactory.Token(SyntaxKind.ElseKeyword).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.Block(
                    SyntaxFactory.Token(SyntaxKind.OpenBraceToken),
                    SyntaxFactory.List(skipInits),
                    SyntaxFactory.Token(SyntaxKind.CloseBraceToken)));
        var guarded = SyntaxFactory.IfStatement(
            SyntaxFactory.Token(SyntaxKind.IfKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.OpenParenToken),
            SyntaxFactory.ParseExpression("!global::__LoopGuard.Tripped"),
            SyntaxFactory.Token(SyntaxKind.CloseParenToken).WithTrailingTrivia(SyntaxFactory.Space),
            visited.Block.WithoutTrailingTrivia(),
            skipped);
        return visited.WithBlock(SyntaxFactory.Block(
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.SingletonList<StatementSyntax>(guarded),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(visited.Block.GetTrailingTrivia())));
    }

    private static string Escaped(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : "@" + identifier;

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var visited = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
        if (!guardedSpan.Contains(node.SpanStart))
        {
            return visited;
        }

        if (visited.Body is { } body)
        {
            return visited.WithBody(WithEnterBefore(body, 0));
        }

        var arrow = visited.ExpressionBody!;
        var block = GuardedBlock(arrow.ArrowToken, arrow.Expression, visited.SemicolonToken, ReturnsValue(visited));
        return visited
            .WithExpressionBody(null)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
            .WithBody(block);
    }

    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        var visited = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node)!;
        return guardedSpan.Contains(node.SpanStart) ? visited.WithBlock(WithEnterBefore(visited.Block, 0)) : visited;
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) =>
        IsExpressionTree(node)
            ? node
            : GuardLambda(node, (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!);

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) =>
        IsExpressionTree(node)
            ? node
            : GuardLambda(node, (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node)!);

    private T GuardLambda<T>(T original, T visited)
        where T : LambdaExpressionSyntax
    {
        if (!guardedSpan.Contains(original.SpanStart))
        {
            return visited;
        }

        if (visited.Block is { } block)
        {
            return (T)visited.WithBlock(WithEnterBefore(block, 0));
        }

        var returnsValue = model.GetSymbolInfo(original).Symbol is not IMethodSymbol { ReturnsVoid: true };
        var guarded = GuardedBlock(default, visited.ExpressionBody!, default, returnsValue);
        return (T)visited.WithExpressionBody(null).WithBlock(guarded);
    }

    // Statement lambdas cannot be converted to expression trees, so those stay untouched.
    private bool IsExpressionTree(LambdaExpressionSyntax lambda) =>
        guardedSpan.Contains(lambda.SpanStart)
        && lambda.ExpressionBody is not null
        && model.GetTypeInfo(lambda).ConvertedType?.OriginalDefinition.ToDisplayString()
            == "System.Linq.Expressions.Expression<TDelegate>";

    private static bool ReturnsValue(LocalFunctionStatementSyntax function) =>
        function.ReturnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword };

    private static BlockSyntax GuardedBlock(
        SyntaxToken arrow,
        ExpressionSyntax expression,
        SyntaxToken semicolon,
        bool returnsValue)
    {
        var enter = EnterStatement
            .WithLeadingTrivia(expression.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.Space);
        var value = expression.WithoutLeadingTrivia();
        StatementSyntax body = value switch
        {
            ThrowExpressionSyntax thrown => SyntaxFactory.ThrowStatement(
                SyntaxFactory.Token(SyntaxKind.ThrowKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                thrown.Expression,
                SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            _ when returnsValue => SyntaxFactory.ReturnStatement(
                SyntaxFactory.Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                value,
                SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            _ => SyntaxFactory.ExpressionStatement(value, SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
        };

        // The arrow's trivia moves onto the brace when the arrow itself is dropped (local functions);
        // a lambda keeps its arrow, so its brace only needs a separating space.
        var afterBrace = arrow.TrailingTrivia.Count == 0
            ? SyntaxFactory.TriviaList(SyntaxFactory.Space)
            : arrow.TrailingTrivia;
        return SyntaxFactory.Block(
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                .WithLeadingTrivia(arrow.LeadingTrivia)
                .WithTrailingTrivia(afterBrace),
            SyntaxFactory.List([enter, body]),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                .WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space).AddRange(semicolon.LeadingTrivia))
                .WithTrailingTrivia(semicolon.TrailingTrivia));
    }

    // When the token before the statement ends its line, Enter goes at the end of that line, so the
    // player's own columns are not shifted; otherwise it goes inline before the statement.
    private static BlockSyntax WithEnterBefore(BlockSyntax block, int index)
    {
        var previous = index == 0 ? block.OpenBraceToken : block.Statements[index - 1].GetLastToken();
        var enter = EnterStatement.WithTrailingTrivia(SyntaxFactory.Space);
        if (previous.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia))
        {
            enter = EnterStatement
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(previous.TrailingTrivia);
            block = block.ReplaceToken(previous, previous.WithTrailingTrivia(SyntaxFactory.TriviaList()));
        }

        return block.WithStatements(block.Statements.Insert(index, enter));
    }

    // Loops hidden in a header (a condition, initializer, incrementor or iterated expression, via lambdas
    // or local functions) are visited too, so the whole node is rewritten before its body is guarded.
    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        var visited = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        return visited.WithStatement(Guard(node, visited.Statement));
    }

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        var visited = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return visited.WithStatement(Guard(node, visited.Statement));
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var visited = (ForStatementSyntax)base.VisitForStatement(node)!;
        return visited.WithStatement(Guard(node, visited.Statement));
    }

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var visited = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
        return visited.WithStatement(Guard(node, visited.Statement));
    }

    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        var visited = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
        return visited.WithStatement(Guard(node, visited.Statement));
    }

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
