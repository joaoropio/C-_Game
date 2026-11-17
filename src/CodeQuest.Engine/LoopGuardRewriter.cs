using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeQuest.Engine;

internal sealed class LoopGuardRewriter(TextSpan guardedSpan, SemanticModel model) : CSharpSyntaxRewriter
{
    private int _guardFields;

    // The guard throws its own exception type so that a TimeoutException thrown by the player's code is not
    // mistaken for a tripped limit.
    public const string LimitExceptionName = "__LimitExceededException";

    // Why the guard tripped, so the runner can report it even when a Validation swallowed the exception
    // (the same values as the constants inside GuardSource).
    public const int LimitTrip = 1;
    public const int StackTrip = 2;

    public const string GuardTypeName = "__LoopGuard";
    public const string TripKindName = "TripKind";

    public const string GuardSource = $$"""
        internal sealed class {{LimitExceptionName}} : global::System.Exception
        {
        }

        // The type of the injected guard fields: empty, so a struct's equality and hash code are not affected.
        internal readonly struct __Entered
        {
        }

        internal static class {{GuardTypeName}}
        {
            public const int Limit = 1_000_000;
            public const int CallLimit = 10_000_000;
            public const int LimitTrip = 1;
            public const int StackTrip = 2;

            private static int _ticks;
            private static int _calls;

            // Set once, by the first limit or stack exhaustion; the rewriter makes the player's catch and
            // finally blocks inert from then on, so nothing swallows the failure.
            public static int {{TripKindName}} { get; private set; }

            public static bool Tripped => {{TripKindName}} != 0;

            public static void Tick()
            {
                if (Tripped || ++_ticks > Limit)
                {
                    Trip(LimitTrip);
                    throw new {{LimitExceptionName}}();
                }
            }

            public static void Enter()
            {
                if (Tripped || ++_calls > CallLimit)
                {
                    Trip(LimitTrip);
                    throw new {{LimitExceptionName}}();
                }

                if (!global::System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
                {
                    Trip(StackTrip);
                    throw new global::System.InsufficientExecutionStackException();
                }
            }

            // For the guard fields, which run before the constructor body and the static constructor.
            public static __Entered EnterValue()
            {
                Enter();
                return default;
            }

            // For the first argument of `this(...)`, which is evaluated before the constructor body.
            public static bool Entering()
            {
                Enter();
                return true;
            }

            private static void Trip(int kind)
            {
                if ({{TripKindName}} == 0)
                {
                    {{TripKindName}} = kind;
                }
            }
        }
        """;

    private static readonly ExpressionStatementSyntax TickStatement = (ExpressionStatementSyntax)SyntaxFactory
        .ParseStatement("global::__LoopGuard.Tick();")
        .WithTrailingTrivia(SyntaxFactory.Space);

    private static readonly SyntaxToken NoToken = SyntaxFactory.Token(SyntaxKind.None);

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
        return guardedSpan.Contains(node.SpanStart)
            && GuardedBody(visited.Body, visited.ExpressionBody, visited.SemicolonToken, ReturnsValue(visited.ReturnType)) is { } body
                ? visited.WithExpressionBody(null).WithSemicolonToken(NoToken).WithBody(body)
                : visited;
    }

    // When the template lets the player write whole members, no template method encloses the player
    // code, so each member body is entered through its own guard.
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        return guardedSpan.Contains(node.SpanStart)
            && GuardedBody(visited.Body, visited.ExpressionBody, visited.SemicolonToken, ReturnsValue(visited.ReturnType)) is { } body
                ? visited.WithExpressionBody(null).WithSemicolonToken(NoToken).WithBody(body)
                : visited;
    }

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        return guardedSpan.Contains(node.SpanStart)
            && GuardedBody(visited.Body, visited.ExpressionBody, visited.SemicolonToken, returnsValue: false) is { } body
                ? visited.WithExpressionBody(null).WithSemicolonToken(NoToken).WithBody(body)
                : visited;
    }

    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        var visited = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;
        var returnsValue = visited.IsKind(SyntaxKind.GetAccessorDeclaration);
        return guardedSpan.Contains(node.SpanStart)
            && GuardedBody(visited.Body, visited.ExpressionBody, visited.SemicolonToken, returnsValue) is { } body
                ? visited.WithExpressionBody(null).WithSemicolonToken(NoToken).WithBody(body)
                : visited;
    }

    // `int P => x;` has no accessor to enter through, so it becomes `int P { get { Enter(); return x; } }`.
    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
        return guardedSpan.Contains(node.SpanStart) && visited.ExpressionBody is { } arrow
            ? visited.WithExpressionBody(null).WithSemicolonToken(NoToken).WithAccessorList(GetterOf(arrow, visited.SemicolonToken))
            : visited;
    }

    public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        var visited = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node)!;
        return guardedSpan.Contains(node.SpanStart) && visited.ExpressionBody is { } arrow
            ? visited.WithExpressionBody(null).WithSemicolonToken(NoToken).WithAccessorList(GetterOf(arrow, visited.SemicolonToken))
            : visited;
    }

    private static AccessorListSyntax GetterOf(ArrowExpressionClauseSyntax arrow, SyntaxToken semicolon)
    {
        var block = GuardedBlock(arrow.ArrowToken, arrow.Expression, semicolon.WithTrailingTrivia(), returnsValue: true);
        var getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, block)
            .WithKeyword(SyntaxFactory.Token(SyntaxKind.GetKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        return SyntaxFactory.AccessorList(
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.SingletonList(getter),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(semicolon.TrailingTrivia));
    }

    // Initializers and constructor-initializer arguments run before any constructor body, and a type without a
    // written constructor still runs them (`class A { A next = new A(); }` recurses through the implicit
    // constructor), so a guard field placed first runs before them: instance field initializers execute in
    // textual order on entry to each constructor, before the base call and its arguments
    // (https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/classes#15113-instance-variable-initializers),
    // and static ones in textual order when the type is initialized. The fields sit on the line of the
    // opening brace, so no line moves; the static one also stops `C<T>.S = C<C<T>>.S`, which recurses through
    // type initializers of ever new generic instantiations without running a single member.
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) =>
        WithGuardFields(node, (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!);

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) =>
        WithGuardFields(node, (StructDeclarationSyntax)base.VisitStructDeclaration(node)!);

    public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) =>
        WithGuardFields(node, (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!);

    private T WithGuardFields<T>(T original, T visited)
        where T : TypeDeclarationSyntax
    {
        if (!guardedSpan.Contains(original.SpanStart) || visited.OpenBraceToken.IsMissing)
        {
            return visited;
        }

        var fields = new List<MemberDeclarationSyntax>();
        if (original.Members.Any(HasStaticInitializer))
        {
            fields.Add(GuardField("private static readonly", "__sguard"));
        }

        if (HasInstanceConstructor(original))
        {
            fields.Add(GuardField("private readonly", "__guard"));
        }

        if (fields.Count == 0)
        {
            return visited;
        }

        var brace = visited.OpenBraceToken;
        fields[0] = fields[0].WithLeadingTrivia(SyntaxFactory.Space);
        fields[^1] = fields[^1].WithTrailingTrivia(brace.TrailingTrivia);
        return (T)visited
            .WithOpenBraceToken(brace.WithTrailingTrivia())
            .WithMembers(visited.Members.InsertRange(0, fields));
    }

    private MemberDeclarationSyntax GuardField(string modifiers, string name) => SyntaxFactory
        .ParseMemberDeclaration(
            $"[global::System.Runtime.CompilerServices.CompilerGenerated] {modifiers} global::__Entered {name}{_guardFields++} = global::{GuardTypeName}.EnterValue();")!
        .WithTrailingTrivia(SyntaxFactory.Space);

    // A class always has a constructor (written or implicit) unless it is static (on any of its partial parts,
    // hence the symbol); a struct only runs field
    // initializers through a written or primary constructor (and may not declare them otherwise).
    private bool HasInstanceConstructor(TypeDeclarationSyntax type) => type switch
    {
        ClassDeclarationSyntax => model.GetDeclaredSymbol(type) is { IsStatic: false },
        StructDeclarationSyntax => type.ParameterList is not null
            || type.Members.OfType<ConstructorDeclarationSyntax>().Any(constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword)),
        _ => false,
    };

    private static bool HasStaticInitializer(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.StaticKeyword)
            && field.Declaration.Variables.Any(variable => variable.Initializer is not null),
        PropertyDeclarationSyntax property => property.Modifiers.Any(SyntaxKind.StaticKeyword) && property.Initializer is not null,
        _ => false,
    };

    // The arguments of `this(...)` run before the body, and no field initializer runs on that path, so the
    // first argument that is not a constant is entered through the guard:
    // `this(Entering() ? (arg) : default, ...)`. Arguments are evaluated left to right and a constant cannot run
    // player code, so nothing of the player's runs before the guard. Constants stay untouched, or a conditional
    // would turn `this(5)` into a non-constant and lose its implicit conversion to `byte`.
    public override SyntaxNode? VisitConstructorInitializer(ConstructorInitializerSyntax node)
    {
        var visited = (ConstructorInitializerSyntax)base.VisitConstructorInitializer(node)!;
        if (!guardedSpan.Contains(node.SpanStart) || !node.IsKind(SyntaxKind.ThisConstructorInitializer))
        {
            return visited;
        }

        var index = node.ArgumentList.Arguments.IndexOf(argument => !model.GetConstantValue(argument.Expression).HasValue);
        if (index < 0)
        {
            return visited;
        }

        var target = visited.ArgumentList.Arguments[index];
        var value = target.Expression;
        var entered = SyntaxFactory.ConditionalExpression(
            SyntaxFactory.ParseExpression($"global::{GuardTypeName}.Entering()")
                .WithLeadingTrivia(value.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.QuestionToken).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.ParenthesizedExpression(value.WithoutTrivia()),
            SyntaxFactory.Token(SyntaxKind.ColonToken).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword)))
            .WithTrailingTrivia(value.GetTrailingTrivia());
        var arguments = visited.ArgumentList.Arguments.Replace(target, target.WithExpression(entered));
        return visited.WithArgumentList(visited.ArgumentList.WithArguments(arguments));
    }

    // Returns the body with Enter in front, turning an expression body into a block; null when there is no body.
    private static BlockSyntax? GuardedBody(
        BlockSyntax? body,
        ArrowExpressionClauseSyntax? arrow,
        SyntaxToken semicolon,
        bool returnsValue) =>
        body is not null
            ? WithEnterBefore(body, 0)
            : arrow is null
                ? null
                : GuardedBlock(arrow.ArrowToken, arrow.Expression, semicolon, returnsValue);

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

    private static bool ReturnsValue(TypeSyntax returnType) =>
        returnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword };

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
