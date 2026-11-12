using CodeQuest.Core;

namespace CodeQuest.Engine;

internal static class PlayerMessages
{
    public static readonly LocalizedText NoSpellWritten = new(
        "Ainda não escreveste nenhum feitiço.",
        "You haven't written any spell yet.");

    public static LocalizedText ExtraClosingBrace(int line) => new(
        $"Tens uma `}}` a mais na linha {line}.",
        $"There's an extra `}}` on line {line}.");

    public static LocalizedText UnclosedBrace(int line) => new(
        $"Falta fechar uma `{{` aberta na linha {line}.",
        $"A `{{` opened on line {line} is never closed.");

    public static LocalizedText MissingVariable(string name) => new(
        $"Falta declarar a variável `{name}`.",
        $"You still need to declare the variable `{name}`.");

    public static LocalizedText ForbiddenSpell(string symbol, int line) => new(
        $"Esse feitiço é proibido nesta masmorra: `{symbol}` (linha {line}).",
        $"That spell is forbidden in this dungeon: `{symbol}` (line {line}).");

    public static readonly LocalizedText DividedByZero = new(
        "Dividiste por zero.",
        "You divided by zero.");

    public static readonly LocalizedText UsedNull = new(
        "Usaste algo que está vazio (null).",
        "You used something that is empty (null).");

    public static readonly LocalizedText PositionDoesNotExist = new(
        "Tentaste usar uma posição que não existe.",
        "You tried to use a position that doesn't exist.");

    public static readonly LocalizedText UnexpectedFormat = new(
        "O texto não tem o formato esperado.",
        "The text isn't in the expected format.");

    public static readonly LocalizedText SpellFailedWhileRunning = new(
        "O feitiço falhou durante a execução.",
        "The spell failed while running.");
}