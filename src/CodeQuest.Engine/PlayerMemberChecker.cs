using CodeQuest.Core;
using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

// At class scope the player may only write methods: no nested types and no other kind of member. At file scope
// (World 3) the player writes types: whatever has no guarded body or could kill the process is rejected.
// An async void method throws on the thread pool and would kill the process, and extern methods have no
// guarded body, so neither modifier is allowed anywhere in the player code.
// Generics are not taught before World 4, and a recursive generic method can grow a struct on every call (a frame
// size no static check sees), so no type parameter list is allowed on any player type, method or local function.
internal static class PlayerMemberChecker
{
    private const string InvalidTokenInMemberErrorId = "CS1519";

    public static CodeRejected? Check(SyntaxNode root, int playerLineOffset, int playerLineCount, PlayerScopeKind scope)
    {
        if (scope != PlayerScopeKind.File
            && FirstStatementOutsideAMethod(root, playerLineOffset, playerLineCount) is { } statementLine)
        {
            return new CodeRejected(PlayerMessages.StatementsBelongInMethods, statementLine);
        }

        var byNode = FirstRejectedNode(root, playerLineOffset, playerLineCount, scope);
        if (scope != PlayerScopeKind.File)
        {
            return byNode;
        }

        var byName = FirstReservedIdentifier(root, playerLineOffset, playerLineCount);
        return byName is not null && (byNode is null || byName.Line < byNode.Line) ? byName : byNode;
    }

    private static CodeRejected? FirstRejectedNode(SyntaxNode root, int playerLineOffset, int playerLineCount, PlayerScopeKind scope)
    {
        foreach (var node in root.DescendantNodes())
        {
            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (line < playerLineOffset || line >= playerLineOffset + playerLineCount)
            {
                continue;
            }

            var playerLine = line + 1 - playerLineOffset;
            if ((scope == PlayerScopeKind.File ? FileReasonToReject(node, playerLine) : ReasonToReject(node, playerLine)) is { } reason)
            {
                return new CodeRejected(reason, playerLine);
            }
        }

        return null;
    }

    // Everything starting with a double underscore belongs to the engine (__LoopGuard, __LimitExceededException,
    // the guard fields): the player may neither declare nor mention it, or could call the guard or throw its exception.
    private static CodeRejected? FirstReservedIdentifier(SyntaxNode root, int playerLineOffset, int playerLineCount)
    {
        foreach (var token in root.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)))
        {
            var line = token.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (line >= playerLineOffset && line < playerLineOffset + playerLineCount
                && token.ValueText.StartsWith("__", StringComparison.Ordinal))
            {
                return new CodeRejected(PlayerMessages.ReservedName(token.ValueText), line + 1 - playerLineOffset);
            }
        }

        return null;
    }

    // A statement at class scope (`x++;`, `return 1;`, `if (...)`) is a syntax error inside a type, not inside a block.
    private static int? FirstStatementOutsideAMethod(SyntaxNode root, int playerLineOffset, int playerLineCount) => root
        .SyntaxTree.GetDiagnostics()
        .Where(diagnostic => diagnostic.Id == InvalidTokenInMemberErrorId)
        .Where(diagnostic => IsAtClassScope(root, diagnostic))
        .Select(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1 - playerLineOffset)
        .Where(playerLine => playerLine >= 1 && playerLine <= playerLineCount)
        .Select(playerLine => (int?)playerLine)
        .Min();

    private static bool IsAtClassScope(SyntaxNode root, Diagnostic diagnostic) => root
        .FindNode(diagnostic.Location.SourceSpan)
        .AncestorsAndSelf()
        .FirstOrDefault(node => node is BlockSyntax or BaseTypeDeclarationSyntax) is BaseTypeDeclarationSyntax;

    private static LocalizedText? ReasonToReject(SyntaxNode node, int playerLine) => node switch
    {
        BaseNamespaceDeclarationSyntax => PlayerMessages.NamespaceNotAllowed,
        TypeParameterListSyntax => PlayerMessages.GenericsNotYet,
        ConstructorDeclarationSyntax { Body: null, ExpressionBody: null } => PlayerMessages.StatementsBelongInMethods,
        ConstructorDeclarationSyntax constructor when IsNotNamedAfterItsClass(constructor)
            => PlayerMessages.MissingReturnType(constructor.Identifier.Text),
        BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => PlayerMessages.TypesNotAllowed,
        BaseFieldDeclarationSyntax or BasePropertyDeclarationSyntax or ConstructorDeclarationSyntax
            or DestructorDeclarationSyntax => PlayerMessages.OnlyMethodsAllowed,
        _ when IsAsync(node) => PlayerMessages.AsyncNotAllowed(playerLine),
        _ when IsExtern(node) => PlayerMessages.ExternNotAllowed(playerLine),
        _ => null,
    };

    // Destructors run on the finalizer thread (an exception there ends the process), operators and conversions
    // are not taught yet, a `this(...)` initializer cannot enter the guard through ref arguments, and the type names
    // Validation (any depth) and System (it would hide the library for the injected code) belong to the engine.
    private static LocalizedText? FileReasonToReject(SyntaxNode node, int playerLine) => node switch
    {
        BaseNamespaceDeclarationSyntax => PlayerMessages.NamespaceNotAllowed,
        TypeParameterListSyntax => PlayerMessages.GenericsNotYet,
        GlobalStatementSyntax => PlayerMessages.OnlyTypesAtFileLevel,
        RecordDeclarationSyntax => PlayerMessages.NotYetInThisWorld("records", "records"),
        DelegateDeclarationSyntax => PlayerMessages.NotYetInThisWorld("delegates", "delegates"),
        DestructorDeclarationSyntax => PlayerMessages.NotYetInThisWorld("destrutores", "destructors"),
        OperatorDeclarationSyntax => PlayerMessages.NotYetInThisWorld("operadores", "operators"),
        ConversionOperatorDeclarationSyntax => PlayerMessages.NotYetInThisWorld("conversões", "conversions"),
        EventDeclarationSyntax or EventFieldDeclarationSyntax => PlayerMessages.NotYetInThisWorld("eventos", "events"),
        ConstructorDeclarationSyntax constructor when IsNotNamedAfterItsClass(constructor)
            => PlayerMessages.MissingReturnType(constructor.Identifier.Text),
        ConstructorInitializerSyntax { RawKind: (int)SyntaxKind.ThisConstructorInitializer } initializer
            when initializer.ArgumentList.Arguments.Any(argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None))
            => PlayerMessages.NotYetInThisWorld("ref, out ou in", "ref, out or in"),
        BaseTypeDeclarationSyntax type when IsReserved(type.Identifier.Text) => PlayerMessages.ReservedName(type.Identifier.Text),
        _ when IsAsync(node) => PlayerMessages.AsyncNotAllowed(playerLine),
        _ when IsExtern(node) => PlayerMessages.ExternNotAllowed(playerLine),
        _ => null,
    };

    private static bool IsReserved(string typeName) =>
        typeName is RoslynCodeRunner.ValidationTypeName or "System";

    // `static Heal(int a) { ... }` has no return type, so Roslyn reads it as a constructor named Heal (CS1520).
    private static bool IsNotNamedAfterItsClass(ConstructorDeclarationSyntax constructor) =>
        constructor.Parent is BaseTypeDeclarationSyntax type && type.Identifier.Text != constructor.Identifier.Text;

    private static bool IsAsync(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.AsyncKeyword),
        LocalFunctionStatementSyntax function => function.Modifiers.Any(SyntaxKind.AsyncKeyword),
        AnonymousFunctionExpressionSyntax function => function.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
        _ => false,
    };

    private static bool IsExtern(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax member => member.Modifiers.Any(SyntaxKind.ExternKeyword),
        BasePropertyDeclarationSyntax member => member.Modifiers.Any(SyntaxKind.ExternKeyword),
        LocalFunctionStatementSyntax function => function.Modifiers.Any(SyntaxKind.ExternKeyword),
        _ => false,
    };
}
