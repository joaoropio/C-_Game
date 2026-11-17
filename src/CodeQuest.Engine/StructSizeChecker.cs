using CodeQuest.Core.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuest.Engine;

// The JIT reserves a method's whole frame before the first statement runs, so a struct local that is too big
// overflows the stack before the recursion guard can say anything, and a stack overflow ends the process. The
// guard only promises about 64 KB of stack, so every value type the player's code mentions (declared, constructed
// from generics, tuples, as a type argument or an inferred type) is measured by its instance fields, recursively.
// A second budget limits what one body keeps on the stack in total (many locals of an acceptable size are as deadly).
// The check lives apart from ForbiddenApiChecker because it is about size, not about which API is named.
internal sealed class StructSizeChecker
{
    internal const int LimitInBytes = 1024;
    internal const int FrameBudgetInBytes = 32 * 1024;

    private const int ReferenceSize = 8;
    private const int MaxDepth = 64;

    private readonly Dictionary<ITypeSymbol, long> _sizes = new(SymbolEqualityComparer.Default);
    private readonly HashSet<ITypeSymbol> _measuring = new(SymbolEqualityComparer.Default);
    private readonly HashSet<ITypeSymbol> _examined = new(SymbolEqualityComparer.Default);

    public static CodeRejected? Check(SemanticModel model, SyntaxNode root, int playerLineOffset, int playerLineCount)
    {
        var checker = new StructSizeChecker();
        foreach (var node in root.DescendantNodes())
        {
            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (line < playerLineOffset || line >= playerLineOffset + playerLineCount)
            {
                continue;
            }

            if (checker.FirstOversizedTypeAt(model, node) is { } tooBig)
            {
                var playerLine = line + 1 - playerLineOffset;
                return new CodeRejected(PlayerMessages.StructTooBig(tooBig.Name), playerLine);
            }
        }

        return checker.FirstBodyOverBudget(model, root, playerLineOffset, playerLineCount);
    }

    // The frame of one body (method, constructor, accessor, local function, lambda) is reserved whole before its first
    // statement, so what counts is the sum over that body of: value-type locals and parameters, plus a temporary for EVERY
    // value-type expression (member, property or element access, call, creation, conditional, cast, assignment, operator
    // result, literal...): a debug build gives every temporary its own slot, so each copy of a struct costs its size.
    // Over-approximation on purpose: a member chain such as `o.P.a.a` counts every link, and a temporary is counted next
    // to the local it initialises. Nested lambdas and local functions have their own frame, so they are counted apart.
    private CodeRejected? FirstBodyOverBudget(SemanticModel model, SyntaxNode root, int playerLineOffset, int playerLineCount)
    {
        var totals = new Dictionary<SyntaxNode, long>();
        foreach (var node in root.DescendantNodes())
        {
            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (line < playerLineOffset || line >= playerLineOffset + playerLineCount
                || node.Ancestors().FirstOrDefault(IsFrame) is not { } frame)
            {
                continue;
            }

            var size = FrameBytesOf(model, node);
            totals[frame] = totals.GetValueOrDefault(frame) + size;
            if (totals[frame] > FrameBudgetInBytes)
            {
                var (name, token) = Describe(frame);
                var tokenLine = token.GetLocation().GetLineSpan().StartLinePosition.Line;
                var reportedLine = tokenLine >= playerLineOffset && tokenLine < playerLineOffset + playerLineCount ? tokenLine : line;
                return new CodeRejected(PlayerMessages.FrameTooBig(name), reportedLine + 1 - playerLineOffset);
            }
        }

        return null;
    }

    private long FrameBytesOf(SemanticModel model, SyntaxNode node)
    {
        ITypeSymbol? type = node switch
        {
            VariableDeclaratorSyntax or SingleVariableDesignationSyntax or ForEachStatementSyntax or ParameterSyntax =>
                model.GetDeclaredSymbol(node) switch { ILocalSymbol local => local.Type, IParameterSymbol parameter => parameter.Type, _ => null },
            ExpressionSyntax expression when !NamesLocalParameterOrType(model, expression) => model.GetTypeInfo(expression).Type,
            _ => null,
        };
        return BytesOf(type) + (node is CommonForEachStatementSyntax loop ? ForEachHiddenBytes(model, loop) : 0);
    }

    // A foreach keeps the enumerator and a copy of the current element on the stack, and neither has an expression node.
    private long ForEachHiddenBytes(SemanticModel model, CommonForEachStatementSyntax loop)
    {
        var info = model.GetForEachStatementInfo(loop);
        return BytesOf(info.ElementType) + BytesOf(info.GetEnumeratorMethod?.ReturnType);
    }

    private long BytesOf(ITypeSymbol? type) =>
        type is { IsValueType: true, SpecialType: not SpecialType.System_Void } ? SizeOf(type, depth: 0) : 0;

    // A bare local or parameter is already counted by its declaration, and a type name is no value at all.
    private static bool NamesLocalParameterOrType(SemanticModel model, ExpressionSyntax expression) =>
        expression is TypeSyntax && model.GetSymbolInfo(expression).Symbol is ILocalSymbol or IParameterSymbol or ITypeSymbol or INamespaceSymbol;

    private static bool IsFrame(SyntaxNode node) => node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax
        or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or TypeDeclarationSyntax
        || node is ArrowExpressionClauseSyntax { Parent: BasePropertyDeclarationSyntax };

    // The name shown to the player and the token whose line the error is reported on.
    private static (string Name, SyntaxToken Token) Describe(SyntaxNode frame) => frame switch
    {
        MethodDeclarationSyntax method => (method.Identifier.Text, method.Identifier),
        ConstructorDeclarationSyntax constructor => (constructor.Identifier.Text, constructor.Identifier),
        DestructorDeclarationSyntax destructor => (destructor.Identifier.Text, destructor.Identifier),
        LocalFunctionStatementSyntax function => (function.Identifier.Text, function.Identifier),
        TypeDeclarationSyntax type => (type.Identifier.Text, type.Identifier),
        AccessorDeclarationSyntax { Parent.Parent: { } owner } => Describe(owner),
        PropertyDeclarationSyntax property => (property.Identifier.Text, property.Identifier),
        ArrowExpressionClauseSyntax { Parent: { } owner } => Describe(owner),
        IndexerDeclarationSyntax indexer => ("this", indexer.ThisKeyword),
        _ when frame.Ancestors().FirstOrDefault(IsFrame) is { } outer => Describe(outer),
        _ => ("?", frame.GetFirstToken()),
    };

    // A declaration is measured on its own line; a use is measured through its type and, for a generic method
    // call, through the type arguments it was given (explicit or inferred).
    private ITypeSymbol? FirstOversizedTypeAt(SemanticModel model, SyntaxNode node) => node switch
    {
        BaseTypeDeclarationSyntax declaration => FirstOversized(model.GetDeclaredSymbol(declaration) as ITypeSymbol),
        TypeSyntax or ExpressionSyntax => OversizedInUse(model, node),
        _ => null,
    };

    private ITypeSymbol? OversizedInUse(SemanticModel model, SyntaxNode node)
    {
        var info = model.GetTypeInfo(node);
        var method = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
        return FirstOversized(info.Type)
            ?? FirstOversized(info.ConvertedType)
            ?? method?.TypeArguments.Select(FirstOversized).FirstOrDefault(found => found is not null);
    }

    private ITypeSymbol? FirstOversized(ITypeSymbol? type)
    {
        if (type is null || !_examined.Add(type))
        {
            return null;
        }

        if (type is IArrayTypeSymbol array)
        {
            return FirstOversized(array.ElementType);
        }

        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        if (named.IsValueType && SizeOf(named, depth: 0) > LimitInBytes)
        {
            return named;
        }

        return named.TypeArguments.Select(FirstOversized).FirstOrDefault(found => found is not null);
    }

    // The sum of the instance fields (padding is at most a small factor, far inside the limit's margin). Counting stops
    // once the limit is passed, and a type that is being measured or nests too deep (an expanding generic) never
    // finishes: the first is a compile error of its own, the second is treated as too big.
    private long SizeOf(ITypeSymbol type, int depth)
    {
        if (_sizes.TryGetValue(type, out var known))
        {
            return known;
        }

        if (depth > MaxDepth)
        {
            return LimitInBytes + 1;
        }

        if (!_measuring.Add(type))
        {
            return 0;
        }

        var size = Measure(type, depth);
        _measuring.Remove(type);
        _sizes[type] = size;
        return size;
    }

    private long Measure(ITypeSymbol type, int depth)
    {
        if (BuiltInSize(type.SpecialType) is { } builtIn)
        {
            return builtIn;
        }

        if (type is INamedTypeSymbol { EnumUnderlyingType: { } underlying })
        {
            return SizeOf(underlying, depth + 1);
        }

        if (type is not INamedTypeSymbol { IsValueType: true } structure)
        {
            return ReferenceSize;
        }

        long total = 0;
        foreach (var field in structure.GetMembers().OfType<IFieldSymbol>().Where(field => field is { IsStatic: false, IsConst: false }))
        {
            total += field.RefKind == RefKind.None ? SizeOf(field.Type, depth + 1) : ReferenceSize;
            if (total > LimitInBytes)
            {
                return total;
            }
        }

        return Math.Max(total, 1);
    }

    private static long? BuiltInSize(SpecialType type) => type switch
    {
        SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
        SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
        SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
        SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double
            or SpecialType.System_IntPtr or SpecialType.System_UIntPtr or SpecialType.System_DateTime => 8,
        SpecialType.System_Decimal => 16,
        _ => null,
    };
}
