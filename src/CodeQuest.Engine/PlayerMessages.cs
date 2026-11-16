using CodeQuest.Core;

namespace CodeQuest.Engine;

internal static class PlayerMessages
{
    public static readonly LocalizedText NoSpellWritten = new(
        "Ainda não escreveste nenhum feitiço.",
        "You haven't written any spell yet.");

    public static LocalizedText ExtraClosingBrace(int line) => new(
        $"Tens uma \"}}\" a mais na linha {line}.",
        $"There's an extra \"}}\" on line {line}.");

    public static LocalizedText UnclosedBrace(int line) => new(
        $"Falta fechar uma \"{{\" aberta na linha {line}.",
        $"A \"{{\" opened on line {line} is never closed.");

    public static LocalizedText MissingVariable(string name) => new(
        $"Falta declarar a variável \"{name}\".",
        $"You still need to declare the variable \"{name}\".");

    public static readonly LocalizedText MissingReturn = new(
        "O feitiço tem de devolver um valor com return.",
        "The spell must return a value with return.");

    public static LocalizedText UnassignedVariable(string name) => new(
        $"Dá um valor à variável \"{name}\" antes de a usares.",
        $"Give the variable \"{name}\" a value before you use it.");

    public static LocalizedText TypeMismatch(string? name, string actualType, string expectedType) => name is null
        ? new LocalizedText(
            $"O valor é do tipo {actualType}, mas aqui é preciso {expectedType}.",
            $"The value is of type {actualType}, but {expectedType} is needed here.")
        : new LocalizedText(
            $"O valor de \"{name}\" é do tipo {actualType}, mas aqui é preciso {expectedType}.",
            $"The value of \"{name}\" is of type {actualType}, but {expectedType} is needed here.");

    public static readonly LocalizedText UnreadableSpell = new(
        "Há um erro no feitiço que a porta não consegue ler. Verifica tipos e nomes.",
        "There is an error in the spell that the door can't read. Check types and names.");

    public static LocalizedText MissingMethod(string name) => new(
        $"Falta o método \"{name}\".",
        $"The method \"{name}\" is missing.");

    public static LocalizedText WrongArgumentCount(string name, int count) => count == 1
        ? new LocalizedText(
            $"O método \"{name}\" não recebe 1 argumento.",
            $"The method \"{name}\" doesn't take 1 argument.")
        : new LocalizedText(
            $"O método \"{name}\" não recebe {count} argumentos.",
            $"The method \"{name}\" doesn't take {count} arguments.");

    public static LocalizedText MissingArguments(string name, string parameter) => new(
        $"Faltam argumentos na chamada ao método \"{name}\": falta o valor de \"{parameter}\".",
        $"Missing arguments in the call to method \"{name}\": no value for \"{parameter}\".");

    public static LocalizedText DuplicateMethod(string name) => new(
        $"Já existe um método \"{name}\" com estes mesmos parâmetros.",
        $"There is already a method \"{name}\" with these same parameters.");

    public static LocalizedText MissingStatic(string name) => new(
        $"Falta static no método \"{name}\".",
        $"The method \"{name}\" needs to be static.");

    public static readonly LocalizedText NamespaceNotAllowed = new(
        "Não uses namespace aqui.",
        "Don't use a namespace here.");

    public static LocalizedText ForbiddenSpell(string symbol, int line) => new(
        $"Esse feitiço é proibido nesta masmorra: \"{symbol}\" (linha {line}).",
        $"That spell is forbidden in this dungeon: \"{symbol}\" (line {line}).");

    public static LocalizedText StackallocNotAllowed(int line) => new(
        $"stackalloc não é permitido nesta masmorra (linha {line}).",
        $"stackalloc is not allowed in this dungeon (line {line}).");

    public static readonly LocalizedText TypesNotAllowed = new(
        "Ainda não podes criar tipos: escreve só métodos static.",
        "You can't create types yet: write only static methods.");

    public static readonly LocalizedText OnlyMethodsAllowed = new(
        "Para já só podes escrever métodos static, sem campos, propriedades nem construtores.",
        "For now you can only write static methods, not fields, properties or constructors.");

    public static LocalizedText AsyncNotAllowed(int line) => new(
        $"Ainda não podes usar async nesta masmorra (linha {line}).",
        $"You can't use async in this dungeon yet (line {line}).");

    public static LocalizedText ExternNotAllowed(int line) => new(
        $"Ainda não podes usar extern nesta masmorra (linha {line}).",
        $"You can't use extern in this dungeon yet (line {line}).");

    public static readonly LocalizedText DividedByZero = new(
        "Dividiste por zero.",
        "You divided by zero.");

    public static readonly LocalizedText UsedNull = new(
        "Usaste algo que está vazio (null).",
        "You used something that is empty (null).");

    public static readonly LocalizedText PositionDoesNotExist = new(
        "Tentaste usar uma posição que não existe.",
        "You tried to use a position that doesn't exist.");

    public static readonly LocalizedText KeyNotInDictionary = new(
        "Pediste uma chave que o dicionário não tem. Verifica com ContainsKey ou TryGetValue antes de a ler.",
        "You asked for a key the dictionary does not have. Check with ContainsKey or TryGetValue before reading it.");

    public static readonly LocalizedText UnexpectedFormat = new(
        "O texto não tem o formato esperado.",
        "The text isn't in the expected format.");

    public static readonly LocalizedText InfiniteRecursion = new(
        "O feitiço chama-se a si próprio sem parar (recursão infinita).",
        "The spell keeps calling itself and never stops (infinite recursion).");

    public static readonly LocalizedText SpellFailedWhileRunning = new(
        "O feitiço falhou durante a execução.",
        "The spell failed while running.");
}
