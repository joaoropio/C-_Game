using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeQuest.Engine;

internal sealed class LoopGuardRewriter(TextSpan guardedSpan, Func<SemanticModel>? semanticModel = null) : CSharpSyntaxRewriter
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

            public static void Enter()
            {
                Tick();
                global::System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();
            }
        }
        """;

    private readonly Lazy<SemanticModel?> _model = new(() => semanticModel?.Invoke());

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

        // Without a semantic model the delegate's return type is unknown, so the lambda stays as written.
        if (ReturnsValue(original) is not { } returnsValue)
        {
            return visited;
        }

        var guarded = GuardedBlock(default, visited.ExpressionBody!, default, returnsValue);
        return (T)visited.WithExpressionBody(null).WithBlock(guarded);
    }

    // Statement lambdas cannot be converted to expression trees, so those stay untouched.
    private bool IsExpressionTree(LambdaExpressionSyntax lambda) =>
        guardedSpan.Contains(lambda.SpanStart)
        && lambda.ExpressionBody is not null
        && _model.Value?.GetTypeInfo(lambda).ConvertedType?.OriginalDefinition.ToDisplayString()
            == "System.Linq.Expressions.Expression<TDelegate>";

    private static bool ReturnsValue(LocalFunctionStatementSyntax function) =>
        !(function.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }
            || (function.Modifiers.Any(SyntaxKind.AsyncKeyword) && IsNonGenericTask(function.ReturnType)));

    private static bool IsNonGenericTask(TypeSyntax type) =>
        (type is QualifiedNameSyntax qualified ? qualified.Right : type)
            is IdentifierNameSyntax { Identifier.ValueText: "Task" or "ValueTask" };

    private bool? ReturnsValue(LambdaExpressionSyntax lambda)
    {
        if (_model.Value?.GetSymbolInfo(lambda).Symbol is not IMethodSymbol method)
        {
            return null;
        }

        return !(method.ReturnsVoid
            || (method.IsAsync && method.ReturnType is INamedTypeSymbol { Arity: 0, Name: "Task" or "ValueTask" }));
    }

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
